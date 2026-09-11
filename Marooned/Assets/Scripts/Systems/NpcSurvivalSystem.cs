using System;
using System.Collections.Generic;
using Marooned.Shared;
using Marooned.Systems.AI;
using MessagePipe;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab C Phase 2.5A — Living NPCs: ทำให้ stats ของ NPC "รู้สึก" ถึงโลกจริง
    ///
    /// หน้าที่ 2 อย่าง:
    ///  1. Decay/Drain tick — ทุก NPC ที่ยังมีชีวิต:
    ///     • Hunger += 2*dt (หิวช้า ๆ)
    ///     • Fear -= 6*dt (decay กลับ baseline — เต็ม 100 หมดใน ~17 วิ)
    ///     • Curiosity -= 3*dt (decay กลับ baseline — เต็ม 100 หมดใน ~33 วิ)
    ///     สเกล 0-100 ตาม NpcSurvivalState เดิม (Key 0-2) — ห้ามใช้ 0-1
    ///  2. Motive event hooks (discrete event ผ่าน MessagePipe):
    ///     subscribe NpcEliminatedMessage — NPC ในโซนเดียวกับเหยื่อ "เห็นศพ"
    ///     (Fear +50), NPC ใน connected location "ได้ยินเสียง"
    ///     (Curiosity +30 + จด LastNoiseLocationId ให้ InvestigateNoiseAction ใช้)
    ///
    /// สถาปัตยกรรม:
    ///  • Plain C# singleton (Lifetime.Singleton) implement IDisposable — pattern
    ///    เดียวกับ WorldItemSystem (subscribe เก็บ IDisposable ไว้ dispose ตอน
    ///    container ปิด)
    ///  • แก้เฉพาะ ground truth (Survival.*) — ห้ามหลุด NpcObservableView /
    ///    MCP response เด็ดขาด (Information Hiding เดิมของ Survival)
    ///  • ไม่สร้าง state "finished" พิเศษ — spike แล้วปล่อยให้ decay พากลับ,
    ///    AI re-evaluate ตามเวลาจะเปลี่ยน action เองเมื่อคะแนนเปลี่ยน
    ///  • Tick order (GameTickDriver): ระบบนี้ → NpcDirectorSystem →
    ///    NpcMovementSystem → NpcZoneTransitionSystem (motive ใหม่ต้องพร้อมก่อน
    ///    AI ตัดสินใจในเฟรมเดียวกัน)
    ///
    /// TODO: ย้ายค่าคงที่ทั้งหมดไปตาราง Luban (เช่น SurvivalTuningDef) ทีหลัง
    /// </summary>
    public class NpcSurvivalSystem : IDisposable
    {
        // ---- decay rates (0-100 scale) — TODO ย้ายไป Luban ----
        /// <summary>Hunger เพิ่มต่อวินาที (หิวจาก 0 ถึง 100 ใน ~50 วิ)</summary>
        public float HungerPerSecond = 2f;

        /// <summary>Fear decay ต่อวินาที (100 → 0 ใน ~17 วิ)</summary>
        public float FearDecayPerSecond = 6f;

        /// <summary>Curiosity decay ต่อวินาที (100 → 0 ใน ~33 วิ)</summary>
        public float CuriosityDecayPerSecond = 3f;

        // ---- motive spikes (discrete events) ----
        /// <summary>เห็นศพในโซนเดียวกัน → Fear เพิ่มทันที</summary>
        public float FearSpikeWitness = 50f;

        /// <summary>ได้ยินเสียงจาก connected location → Curiosity เพิ่มทันที</summary>
        public float CuriositySpikeHearNoise = 30f;

        /// <summary>
        /// Curiosity ต่ำกว่านี้ = เลิกสนใจจุดเสียง → ล้าง LastNoiseLocationId
        /// (ต้องต่ำกว่า CuriositySpikeHearNoise มาก — ไม่งั้น hint ถูกล้างก่อน AI
        /// มีโอกาสเลือก InvestigateNoise เลย; 30 → 5 ให้เวลาสำรวจ ~8 วิ)
        /// </summary>
        public float CuriosityInvestigateThreshold = 5f;

        /// <summary>ช่วงเวลา log สรุป stats ทั้งหมด (Test A — log ทุก 30 วิเกม)</summary>
        public float StatusLogIntervalSeconds = 30f;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly LubanDataService _data;
        private readonly UtilityContext _ctx;
        private readonly IDisposable _eliminatedSubscription;
        private float _statusLogTimer;

        public NpcSurvivalSystem(NpcDirectorSystem npcDirector, LubanDataService data,
            UtilityContext ctx, ISubscriber<NpcEliminatedMessage> eliminatedSubscriber)
        {
            _npcDirector = npcDirector;
            _data = data;
            _ctx = ctx;
            _eliminatedSubscription = eliminatedSubscriber.Subscribe(OnNpcEliminated);
        }

        public void Dispose() => _eliminatedSubscription?.Dispose();

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                if (!npc.IsAlive) continue;

                var s = npc.Survival;
                s.Hunger = Math.Clamp(s.Hunger + HungerPerSecond * deltaSeconds, 0f, 100f);
                s.Fear = Math.Clamp(s.Fear - FearDecayPerSecond * deltaSeconds, 0f, 100f);
                s.Curiosity = Math.Clamp(s.Curiosity - CuriosityDecayPerSecond * deltaSeconds, 0f, 100f);

                // Test D: กัน target หลุดนอกรัศมีโซน (เฉพาะ TransitionPhase == None — ระหว่าง
                // ข้ามโซน target = จุดเชื่อมที่ตั้งใจให้นอกรัศมี ห้าม clamp; position ไม่ clamp
                // เพราะจุด arrival ตั้งใจให้อยู่ขอบโซน — clamp = teleport ต่อหน้าผู้เล่น)
                Wander.ClampTargetToZone(npc, _ctx);

                // หมดความสนใจจุดเสียง (decay ต่ำกว่า threshold) → ล้าง hint
                // (กัน LastNoiseLocationId ค้างตลอดรอบโดยไม่มีใครสนใจอีก)
                if (s.LastNoiseLocationId != null && s.Curiosity < CuriosityInvestigateThreshold)
                    s.LastNoiseLocationId = null;
            }

            // log สรุปเป็นระยะ (Test A) — ไม่ log ทุกเฟรม
            _statusLogTimer += deltaSeconds;
            if (_statusLogTimer >= StatusLogIntervalSeconds)
            {
                _statusLogTimer = 0f;
                foreach (var npc in _npcDirector.Npcs.Values)
                {
                    if (!npc.IsAlive) continue;
                    var s = npc.Survival;
                    Debug.Log($"[NpcSurvival] {npc.Id} @ {npc.CurrentLocationId}: " +
                              $"Hunger={s.Hunger:F0} Fear={s.Fear:F0} Curiosity={s.Curiosity:F0}" +
                              (s.LastNoiseLocationId != null ? $" noise={s.LastNoiseLocationId}" : ""));
                }
            }
        }

        /// <summary>
        /// Motive ripple จากการฆ่า (discrete event):
        ///  • โซนเดียวกับเหยื่อ = เห็นศพ → Fear +50
        ///  • connected location = ได้ยินเสียง → Curiosity +30 + จดจุดเสียง
        /// เหตุการณ์เดียวกระทบได้ทั้งสองกลุ่มพร้อมกัน (if/else if — กันนับซ้ำ)
        /// </summary>
        private void OnNpcEliminated(NpcEliminatedMessage msg)
        {
            int witnesses = 0, hearers = 0;
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                if (!npc.IsAlive || npc.Id == msg.VictimNpcId) continue;

                if (npc.CurrentLocationId == msg.LocationId)
                {
                    var before = npc.Survival.Fear;
                    npc.Survival.Fear = Math.Clamp(before + FearSpikeWitness, 0f, 100f);
                    witnesses++;
                    Debug.Log($"[NpcSurvival] {npc.Id} WITNESS body in {msg.LocationId}: " +
                              $"Fear {before:F0} -> {npc.Survival.Fear:F0}");
                }
                else if (IsConnected(npc.CurrentLocationId, msg.LocationId))
                {
                    var before = npc.Survival.Curiosity;
                    npc.Survival.Curiosity = Math.Clamp(before + CuriositySpikeHearNoise, 0f, 100f);
                    npc.Survival.LastNoiseLocationId = msg.LocationId;
                    hearers++;
                    Debug.Log($"[NpcSurvival] {npc.Id} heard noise from {msg.LocationId}: " +
                              $"Curiosity {before:F0} -> {npc.Survival.Curiosity:F0} (wants to investigate)");
                }
            }

            if (witnesses + hearers > 0)
                Debug.Log($"[NpcSurvival] elimination ripple — victim {msg.VictimNpcId} @ {msg.LocationId}: " +
                          $"{witnesses} witness(es), {hearers} hearer(s)");
        }

        private bool IsConnected(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return false;
            if (!_data.LocationDefs.TryGetValue(from, out var def)) return false;
            return def.ConnectedLocationIds != null && def.ConnectedLocationIds.Contains(to);
        }
    }
}
