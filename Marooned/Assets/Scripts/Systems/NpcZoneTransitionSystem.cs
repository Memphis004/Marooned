using System;
using System.Collections.Generic;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>
    /// Hybrid Transition Points (Part 2) — FSM ข้ามโซนแบบเดินผ่านจุดเชื่อมต่อจาก
    /// WalkingToPoint (ตั้งโดย Wander.MoveToZone) → Exiting (เล่น anim ออกที่จุดเชื่อม)
    /// → MoveNpc ข้ามโซน + override ตำแหน่งเป็น arrival point → Entering (anim เข้า)
    /// → None (เดินอิสระปกติ)
    ///
    /// ข้อจำกัดสถาปัตยกรรมที่ระบบนี้ยึด:
    ///  • Plain C# ไม่มี Coroutine — นับเวลา anim ด้วย Dictionary&lt;string,float&gt; ต่อ NPC
    ///  • ห้ามแตะ GameObject/View — เขียนเฉพาะ ground truth fields (TransitionPhase/
    ///    PendingTransitionTargetZoneId/Position/Target) แล้ว NpcCharacterView ตรวจจับ
    ///    phase เปลี่ยนเองแล้วเรียก IChibiVisual.PlayAction("zone_exit"/"zone_enter")
    ///  • CurrentLocationId ยังแก้ผ่าน NpcDirectorSystem.MoveNpc() จุดเดียว
    ///    (escape hatch: override PositionX/Y หลังเรียก = Position Seeding Rule เดิม)
    ///  • Tick order: NpcDirectorSystem → NpcMovementSystem → ระบบนี้ (GameTickDriver)
    /// </summary>
    public class NpcZoneTransitionSystem
    {
        /// <summary>ระยะเวลา anim เดินออกนอกจอที่จุดเชื่อม (วินาที)</summary>
        public float ExitAnimSeconds = 0.5f;

        /// <summary>ระยะเวลา anim เดินเข้าจากจุดเชื่อมของโซนปลายทาง (วินาที)</summary>
        public float EnterAnimSeconds = 0.3f;

        /// <summary>ระยะถือว่าถึงจุดเชื่อมแล้ว (world unit)</summary>
        public float ArrivalThreshold = 0.1f;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly LubanDataService _data;

        /// <summary>ตัวนับถอยเวลา anim ต่อ NPC (plain C# — แทน Coroutine)</summary>
        private readonly Dictionary<string, float> _timers = new();

        public NpcZoneTransitionSystem(NpcDirectorSystem npcDirector, LubanDataService data)
        {
            _npcDirector = npcDirector;
            _data = data;
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                // ตายกลาง transition → เคลียร์ phase/timer ให้สะอาด (Test E)
                if (!npc.IsAlive)
                {
                    if (npc.TransitionPhase != NpcTransitionPhase.None)
                    {
                        npc.TransitionPhase = NpcTransitionPhase.None;
                        npc.PendingTransitionTargetZoneId = null;
                        _timers.Remove(npc.Id);
                        Debug.Log($"[NpcZoneTransition] {npc.Id} died mid-transition — phase/timer cleared (Test E)");
                    }
                    continue;
                }

                switch (npc.TransitionPhase)
                {
                    case NpcTransitionPhase.WalkingToPoint: TickWalkingToPoint(npc); break;
                    case NpcTransitionPhase.Exiting: TickTimer(npc, deltaSeconds, CompleteExit); break;
                    case NpcTransitionPhase.Entering: TickTimer(npc, deltaSeconds, CompleteEnter); break;
                }
            }
        }

        /// <summary>เดินเข้าหาจุดเชื่อม (NpcMovementSystem ทำการเดินอยู่แล้ว) — ถึงแล้ว → Exiting</summary>
        private void TickWalkingToPoint(NpcState npc)
        {
            var dx = npc.TargetX - npc.PositionX;
            var dy = npc.TargetY - npc.PositionY;
            if (dx * dx + dy * dy > ArrivalThreshold * ArrivalThreshold) return;

            // ถึงจุดเชื่อมแล้ว — เริ่มเล่น exit anim (View จะจับ phase เปลี่ยนเอง)
            npc.TransitionPhase = NpcTransitionPhase.Exiting;
            _timers[npc.Id] = ExitAnimSeconds;
            Debug.Log($"[NpcZoneTransition] {npc.Id} reached transition point ({npc.PositionX:F1},{npc.PositionY:F1}) — Exiting {ExitAnimSeconds}s");
        }

        /// <summary>นับถอยเวลา anim แล้วเรียก onComplete เมื่อหมด (timer หาย = ผ่านทันที กันค้าง)</summary>
        private void TickTimer(NpcState npc, float deltaSeconds, Action<NpcState> onComplete)
        {
            if (!_timers.TryGetValue(npc.Id, out var remaining)) { onComplete(npc); return; }

            remaining -= deltaSeconds;
            if (remaining <= 0f)
            {
                _timers.Remove(npc.Id);
                onComplete(npc);
            }
            else
            {
                _timers[npc.Id] = remaining;
            }
        }

        /// <summary>exit anim จบ → ข้ามโซนจริง (MoveNpc) + วางที่ arrival point + เริ่ม enter anim</summary>
        private void CompleteExit(NpcState npc)
        {
            var targetZone = npc.PendingTransitionTargetZoneId;
            var previousZone = npc.CurrentLocationId;

            // จุดเดียวที่อนุญาตให้แก้ CurrentLocationId + publish NpcLocationChangedMessage
            // (ChibiSpawnerView reconcile ตาม event เดิม) — MoveNpc seed ตำแหน่งกลางโซน
            _npcDirector.MoveNpc(npc.Id, targetZone);

            // Escape hatch ตาม Position Seeding Rule: override ตำแหน่งหลัง MoveNpc —
            // วาง NPC ที่ "จุดเชื่อมฝั่งโซนปลายทาง" (เดินเข้าจากขอบ ไม่ใช่โผล่กลางโซน)
            var arrival = _data.GetTransition(targetZone, previousZone);
            if (arrival != null)
            {
                npc.PositionX = arrival.TransitionX;
                npc.PositionY = arrival.TransitionY;
                npc.TargetX = npc.PositionX; // ยืนนิ่งระหว่าง enter anim (Target=Position)
                npc.TargetY = npc.PositionY;
            }

            npc.TransitionPhase = NpcTransitionPhase.Entering;
            npc.PendingTransitionTargetZoneId = null;
            _timers[npc.Id] = EnterAnimSeconds;
            Debug.Log($"[NpcZoneTransition] {npc.Id} crossed {previousZone} -> {targetZone} at ({npc.PositionX:F1},{npc.PositionY:F1}) — Entering {EnterAnimSeconds}s");
        }

        /// <summary>enter anim จบ → กลับสู่การเดินอิสระปกติ (AI เริ่มตั้ง target ใหม่ได้)</summary>
        private void CompleteEnter(NpcState npc)
        {
            npc.TransitionPhase = NpcTransitionPhase.None;
            Debug.Log($"[NpcZoneTransition] {npc.Id} enter complete — back to normal wandering in {npc.CurrentLocationId}");
        }
    }
}
