using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems.AI
{
    /// <summary>
    /// สมองฝั่ง Killer (Step 4) — state machine 5 phase:
    ///   Patrolling → SeekingWeapon → SeekingOpportunity → Executing → BuildingAlibi
    ///
    /// กฎสำคัญ (Design Decision ที่ปิดแล้ว):
    ///  • ฆ่าผ่าน NpcDirectorSystem.TryEliminate/CanEliminate เท่านั้น — ไม่มีกฎฆ่าเส้นที่สอง
    ///  • TryEliminate เป็น economy-agnostic: การเช็คอาวุธเป็นหน้าที่ caller
    ///    (ที่นี่ = HasWeapon ก่อนเข้า phase Executing) อาวุธไม่ถูกหักตอนฆ่า
    ///    (role-enabling condition — durability เป็น TODO อนาคต)
    ///  • KillCooldownRemaining semantics: cooldown นับถอยใน planner
    ///    (ลดด้วย dt ทุก tick ตอน phase อื่น ไม่ใช่เฉพาะตอนพยายามฆ่า)
    ///  • Information Hiding: phase/cooldown/inventory เป็น ground truth —
    ///    ห้ามหลุด NpcObservableView / MCP response ใดๆ
    /// </summary>
    public class KillerPlanner
    {
        public enum KillerPhase
        {
            Patrolling,
            SeekingWeapon,
            SeekingOpportunity,
            Executing,
            BuildingAlibi,
        }

        /// <summary>cooldown หลังฆ่าสำเร็จ (วินาที) — เดิมของ TryAttemptElimination (180s)</summary>
        public float KillCooldownSeconds = 180f;

        /// <summary>เวลาสูงสุดของ phase BuildingAlibi ก่อนกลับ Patrolling</summary>
        public float AlibiTimeoutSeconds = 30f;

        /// <summary>
        /// พักสั้นหลัง abort (เช่น มีพยาน) ก่อนหาโอกาสใหม่ — กัน loop
        /// Executing→abort→SeekingOpportunity→Executing ทุกเฟรม (log spam + พฤติกรรมไม่สมจริง)
        /// </summary>
        public float OpportunityRetrySeconds = 2f;

        private readonly UtilityContext _ctx;
        private readonly Random _rng = new();

        // state ต่อ NPC killer
        private readonly Dictionary<string, KillerPhase> _phases = new();
        private readonly Dictionary<string, float> _alibiTimers = new();
        private readonly Dictionary<string, float> _retryTimers = new();

        public KillerPlanner(UtilityContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>เรียกทุก tick จาก NpcDirectorSystem.TickBehavior (killer ที่ยังมีชีวิต)</summary>
        public void Tick(NpcState killer, float deltaSeconds)
        {
            // เริ่มรอบใหม่ → Patrolling (+ รับ cooldown เริ่มต้น 0)
            if (!_phases.TryGetValue(killer.Id, out var phase))
            {
                phase = KillerPhase.Patrolling;
                _phases[killer.Id] = phase;
                UnityEngine.Debug.Log($"[KillerAI] {killer.Id} phase -> Patrolling");
            }

            // cooldown นับถอยทุก tick (semantics ย้ายมาอยู่ที่ planner แล้ว)
            if (killer.KillCooldownRemaining > 0f)
                killer.KillCooldownRemaining = Math.Max(0f, killer.KillCooldownRemaining - deltaSeconds);

            // retry timer หลัง abort — นับถอยทุก tick
            if (_retryTimers.TryGetValue(killer.Id, out var retry) && retry > 0f)
                _retryTimers[killer.Id] = Math.Max(0f, retry - deltaSeconds);

            switch (phase)
            {
                case KillerPhase.Patrolling: TickPatrolling(killer); break;
                case KillerPhase.SeekingWeapon: TickSeekingWeapon(killer); break;
                case KillerPhase.SeekingOpportunity: TickSeekingOpportunity(killer); break;
                case KillerPhase.Executing: TickExecuting(killer); break;
                case KillerPhase.BuildingAlibi: TickBuildingAlibi(killer, deltaSeconds); break;
            }
        }

        // ---- phases ----

        private void TickPatrolling(NpcState killer)
        {
            // เดินสุ่มเหมือน innocent — reuse IdleWander logic ผ่าน Wander helper
            if (!Wander.HasPendingTarget(killer))
            {
                if (_rng.NextDouble() < 0.2 && Wander.MoveToRandomConnectedZone(killer, _ctx, _rng))
                    return;
                Wander.SetRandomTargetInZone(killer, _ctx, _rng);
            }

            // เช็คความพร้อมทุก tick: มีอาวุธไหม?
            if (HasWeapon(killer))
                Transition(killer, KillerPhase.SeekingOpportunity);
            else
                Transition(killer, KillerPhase.SeekingWeapon);
        }

        private void TickSeekingWeapon(NpcState killer)
        {
            // มีอาวุธแล้ว (เช่น เพิ่งเก็บ) → ไปหาโอกาส
            if (HasWeapon(killer))
            {
                Transition(killer, KillerPhase.SeekingOpportunity);
                return;
            }

            // กำลังเดินไปโซนอาวุธอยู่ → รอ
            if (Wander.HasPendingTarget(killer)) return;

            // หาโซนที่ loot table มีการ์ด Weapon แล้วเดินไป (adjacent เท่านั้น — เดินทีละก้าว)
            var weaponZones = Wander.FindZonesWithCardCategory(_ctx, Marooned.Shared.CardCategory.Weapon);
            if (weaponZones.Count == 0)
            {
                // ไม่มีโซนไหนมีอาวุธใน loot เลย → กลับไป patrol (รอโลกเปลี่ยน)
                Transition(killer, KillerPhase.Patrolling);
                return;
            }

            var target = weaponZones
                .OrderBy(z => SqrDistanceToZone(killer, z))
                .First();

            if (!Wander.MoveToZone(killer, _ctx, target, _rng, requireConnection: true))
            {
                // โซนอาวุธใกล้สุดไม่ adjacent — เดินไปโซนข้างๆ ก่อน (เข้าใกล้ทีละก้าว)
                if (!Wander.MoveToRandomConnectedZone(killer, _ctx, _rng))
                    Transition(killer, KillerPhase.Patrolling);
            }
        }

        private void TickSeekingOpportunity(NpcState killer)
        {
            // หมดอาวุธ (อนาคต durability) → กลับไปหาอาวุธ
            if (!HasWeapon(killer))
            {
                Transition(killer, KillerPhase.SeekingWeapon);
                return;
            }

            // เคารพ cooldown — ระหว่างรอก็ patrol ไปเรื่อยๆ
            if (killer.KillCooldownRemaining > 0f)
            {
                if (!Wander.HasPendingTarget(killer))
                    Wander.SetRandomTargetInZone(killer, _ctx, _rng);
                return;
            }

            // พักหลัง abort — รอ OpportunityRetrySeconds ก่อนหาโอกาสใหม่ (กัน spam loop)
            if (_retryTimers.TryGetValue(killer.Id, out var retry) && retry > 0f)
            {
                if (!Wander.HasPendingTarget(killer))
                    Wander.SetRandomTargetInZone(killer, _ctx, _rng);
                return;
            }

            // หาเหยื่อใน location เดียวกัน (no-witness จะถูกเช็คจริงที่ CanEliminate ตอน Executing)
            var victim = FindVictimInZone(killer);
            if (victim != null)
            {
                UnityEngine.Debug.Log($"[KillerAI] {killer.Id} found victim {victim.Id} in {killer.CurrentLocationId} -> Executing");
                Transition(killer, KillerPhase.Executing);
            }
            else
            {
                // ไม่มีเหยื่อ — เดินหา (โซนอื่นผ่าน idle wander เดิม)
                if (!Wander.HasPendingTarget(killer))
                {
                    if (_rng.NextDouble() < 0.2 && Wander.MoveToRandomConnectedZone(killer, _ctx, _rng))
                        return;
                    Wander.SetRandomTargetInZone(killer, _ctx, _rng);
                }
            }
        }

        private void TickExecuting(NpcState killer)
        {
            // เช็คซ้ำก่อนลงมือ (dry-run) — ผ่านเท่านั้นถึงฆ่าจริง
            var victim = FindVictimInZone(killer);
            if (victim == null)
            {
                Transition(killer, KillerPhase.SeekingOpportunity);
                return;
            }

            var (can, reason) = _ctx.NpcDirector.CanEliminate(killer.Id, victim.Id, killer.CurrentLocationId);
            if (!can)
            {
                // เช่น witnessed — กฎไม่ถูก bypass: กลับไปรอโอกาส + พักสั้นกัน loop ทุกเฟรม
                UnityEngine.Debug.Log($"[KillerAI] {killer.Id} abort (CanEliminate: {reason}) -> SeekingOpportunity");
                _retryTimers[killer.Id] = OpportunityRetrySeconds;
                Transition(killer, KillerPhase.SeekingOpportunity);
                return;
            }

            var (success, _) = _ctx.NpcDirector.TryEliminate(killer.Id, victim.Id, killer.CurrentLocationId);
            if (success)
            {
                killer.KillCooldownRemaining = KillCooldownSeconds;
                UnityEngine.Debug.Log($"[KillerAI] {killer.Id} eliminated {victim.Id} -> BuildingAlibi (cooldown {KillCooldownSeconds}s)");
                Transition(killer, KillerPhase.BuildingAlibi);
            }
            else
            {
                Transition(killer, KillerPhase.SeekingOpportunity);
            }
        }

        private void TickBuildingAlibi(NpcState killer, float deltaSeconds)
        {
            // นับ timeout ทุก tick — BuildingAlibi มีอายุรวม AlibiTimeoutSeconds
            // (ทางหนีถูกเลือกแล้วครั้งเดียวตอน Transition เข้า phase)
            _alibiTimers.TryGetValue(killer.Id, out var timer);
            timer -= deltaSeconds;
            _alibiTimers[killer.Id] = timer;
            if (timer <= 0f)
            {
                UnityEngine.Debug.Log($"[KillerAI] {killer.Id} alibi timeout -> Patrolling");
                Transition(killer, KillerPhase.Patrolling);
                return;
            }

            // ระหว่างรอ — เดินวนในโซนที่หนีมา (ไม่ข้ามโซนซ้ำทุกเฟรม)
            if (!Wander.HasPendingTarget(killer))
                Wander.SetRandomTargetInZone(killer, _ctx, _rng);
        }

        // ---- helpers ----

        private void Transition(NpcState killer, KillerPhase next)
        {
            if (_phases.TryGetValue(killer.Id, out var current) && current == next) return;
            _phases[killer.Id] = next;
            if (next == KillerPhase.BuildingAlibi)
            {
                _alibiTimers[killer.Id] = AlibiTimeoutSeconds;

                // เลือกทางหนี "ครั้งเดียวตอนเข้า phase" — เดินไป connected location
                // ที่มี NPC มีชีวิตอื่น ≥ 1 (fallback: โซนข้างๆ อันไหนก็ได้)
                // ห้ามเลือกใหม่ทุก tick (bug เดิม): MoveNpc seed Target=Position ทำให้
                // HasPendingTarget=false ทันที → ย้ายโซนทุกเฟรม = teleport storm
                var escape = FindConnectedZoneWithAliveNpc(killer);
                if (escape == null || !Wander.MoveToZone(killer, _ctx, escape, _rng, requireConnection: true))
                    Wander.MoveToRandomConnectedZone(killer, _ctx, _rng); // อาจล้มเหลว (ไม่มีทางออก) — เดินในโซนเดิมจน timeout
            }
            UnityEngine.Debug.Log($"[KillerAI] {killer.Id} phase {current} -> {next}");
        }

        /// <summary>มีการ์ด Category==Weapon ใน inventory หรือไม่ (economy check ฝั่ง caller)</summary>
        private bool HasWeapon(NpcState killer)
        {
            return killer.Inventory.GetCardIds()
                .Any(id => _ctx.Data.CardDefs.TryGetValue(id, out var def) && def.Category == Marooned.Shared.CardCategory.Weapon);
        }

        private NpcState FindVictimInZone(NpcState killer)
        {
            return _ctx.NpcDirector.Npcs.Values
                .FirstOrDefault(n => n.IsAlive && n.Id != killer.Id && n.CurrentLocationId == killer.CurrentLocationId);
        }

        private float SqrDistanceToZone(NpcState killer, string zoneId)
        {
            if (!_ctx.Data.LocationDefs.TryGetValue(zoneId, out var def)) return float.MaxValue;
            var dx = def.WorldX - killer.PositionX;
            var dy = def.WorldY - killer.PositionY;
            return dx * dx + dy * dy;
        }

        /// <summary>หา connected location ที่มี NPC มีชีวิตอื่น ≥ 1 (สำหรับ BuildingAlibi)</summary>
        private string FindConnectedZoneWithAliveNpc(NpcState killer)
        {
            if (!_ctx.Data.LocationDefs.TryGetValue(killer.CurrentLocationId, out var currentDef))
                return null;
            var connections = currentDef.ConnectedLocationIds;
            if (connections == null) return null;

            foreach (var zoneId in connections)
            {
                var hasAlive = _ctx.NpcDirector.Npcs.Values.Any(n =>
                    n.IsAlive && n.Id != killer.Id && n.CurrentLocationId == zoneId);
                if (hasAlive) return zoneId;
            }
            return null;
        }

        /// <summary>ตรวจสอบ (เทส): phase ปัจจุบันของ killer</summary>
        public KillerPhase? CurrentPhase(string npcId) =>
            _phases.TryGetValue(npcId, out var p) ? p : (KillerPhase?)null;
    }
}
