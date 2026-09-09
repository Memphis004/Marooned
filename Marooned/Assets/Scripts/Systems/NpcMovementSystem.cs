using System;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab C Phase 2 Step 4 — เดิน NPC เข้าหา Target ที่มีอยู่ใน state เท่านั้น
    ///
    /// ⚠️ DEPRECATED (Step 4.1): re-target-on-arrival (PickNextTarget + ทอยข้ามโซน +
    /// idle-pause) ถูกถอดออกแล้ว — การเลือก target ทั้งหมดย้ายไปอยู่ที่ AI
    /// (InnocentUtilityAI / KillerPlanner ใน namespace Marooned.Systems.AI)
    /// กันสองระบบแย่งกันเซ็ต TargetX/Y (undefined behavior)
    ///
    /// หน้าที่เหลือ:
    ///  1. Tick(dt): เลื่อน PositionX/Y เข้าหา TargetX/Y ด้วย MovementSpeed
    ///     (normalize + กัน overshoot, frame-rate independent)
    ///  2. อัปเดต Activity ตามสถานะการเดิน: Traveling ขณะกำลังเคลื่อน, Idle เมื่อถึงเป้า
    ///     (continuous-state description — ไม่ใช่การเลือก target)
    ///
    /// ยังคงเป็น plain C# singleton (VContainer Lifetime.Singleton) ถูก tick โดย
    /// GameTickDriver หลัง NpcDirectorSystem.Tick() เสมอ (target ที่ AI ตั้งใน
    /// เฟรมนี้ต้องถูกเดินทันที ไม่ดีเลย์ 1 เฟรม)
    /// </summary>
    public class NpcMovementSystem
    {
        /// <summary>ระยะถือว่า "ถึงเป้าแล้ว" (world unit)</summary>
        public float ArrivalThreshold = 0.1f;

        private readonly NpcDirectorSystem _npcDirector;

        public NpcMovementSystem(NpcDirectorSystem npcDirector)
        {
            _npcDirector = npcDirector;
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                if (!npc.IsAlive) continue;

                MoveTowardTarget(npc, deltaSeconds);

                // Activity เป็นคำอธิบายสถานะการเดิน — ไม่ใช่การเลือก target
                npc.Activity = HasArrived(npc) ? NpcActivityState.Idle : NpcActivityState.Traveling;
            }
        }

        /// <summary>เลื่อน PositionX/Y เข้าหา TargetX/Y ด้วย MovementSpeed (frame-rate independent)</summary>
        private void MoveTowardTarget(NpcState npc, float deltaSeconds)
        {
            var dx = npc.TargetX - npc.PositionX;
            var dy = npc.TargetY - npc.PositionY;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= ArrivalThreshold) return;

            var step = npc.MovementSpeed * deltaSeconds;
            if (step >= distance)
            {
                // กัน overshoot หลังเป้า
                npc.PositionX = npc.TargetX;
                npc.PositionY = npc.TargetY;
                return;
            }

            var inv = step / distance;
            npc.PositionX += dx * inv;
            npc.PositionY += dy * inv;
        }

        private bool HasArrived(NpcState npc)
        {
            var dx = npc.TargetX - npc.PositionX;
            var dy = npc.TargetY - npc.PositionY;
            return dx * dx + dy * dy <= ArrivalThreshold * ArrivalThreshold;
        }
    }
}
