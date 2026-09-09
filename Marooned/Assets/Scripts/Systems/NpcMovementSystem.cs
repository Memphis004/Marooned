using System;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab C Phase 2 — เดิน NPC ด้วย Target ที่ถูกตั้งไว้ใน NpcState (ground truth)
    ///
    /// หน้าที่เดียว: Tick(dt) เลื่อน PositionX/Y เข้าหา TargetX/Y ด้วย MovementSpeed
    /// แล้วเมื่อถึงเป้า (≤ 0.1f) เรียก PickNextTarget() เพื่อสุ่มจุด wander ใหม่
    /// (จุดนี้จะถูก deprecate ใน Step 4 — AI เป็นคนเลือก target เอง จึงแยก method
    /// อิสระไว้ให้ปิดง่าย ไม่กระทบ logic เดินใน Tick)
    ///
    /// การข้ามโซน (20% ตอนตั้ง target ใหม่): เรียก NpcDirectorSystem.MoveNpc()
    /// ทันที "ตอนตั้ง target" ไม่ใช่ detect ข้ามขอบระหว่างทาง — CurrentLocationId
    /// เปลี่ยนผ่าน MoveNpc() เท่านั้น (Single Source of Truth) และ MoveNpc เป็นคน
    /// seed PositionX/Y ที่จุดกึ่งกลางโซนใหม่ให้เอง (Position Seeding Rule)
    ///
    /// ขอบเขต wander ยึด pattern เดียกับ WorldItemSystem.ZoneSpread — สุ่มรัศมี
    /// รอบ LocationDef.WorldX/Y ของโซนปัจจุบัน ไม่เดา bounds ที่ไม่มีจริง
    ///
    /// Plain C# singleton (VContainer Lifetime.Singleton) — ถูก tick โดย
    /// GameTickDriver หลัง NpcDirectorSystem.Tick() เสมอ (target ที่ AI ตั้งใน
    /// เฟรมนี้ต้องถูกเดินทันที ไม่ดีเลย์ 1 เฟรม)
    /// </summary>
    public class NpcMovementSystem
    {
        /// <summary>ระยะถือว่า "ถึงเป้าแล้ว" (world unit)</summary>
        public float ArrivalThreshold = 0.1f;

        /// <summary>รัศมี wander รอบจุดกึ่งกลางโซน (world unit) — อิงแนวคิด ZoneSpread ของ WorldItemSystem</summary>
        public float WanderRadius = 2.5f;

        /// <summary>โอกาส (0-1) ที่จะตั้ง target ในโซนเพื่อนบ้านแทนโซนปัจจุบัน</summary>
        public float CrossZoneChance = 0.2f;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly LubanDataService _data;
        private readonly Random _rng = new();

        public NpcMovementSystem(NpcDirectorSystem npcDirector, LubanDataService dataService)
        {
            _npcDirector = npcDirector;
            _data = dataService;
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                if (!npc.IsAlive) continue;

                MoveTowardTarget(npc, deltaSeconds);

                if (HasArrived(npc))
                    PickNextTarget(npc);
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

        /// <summary>
        /// เลือก target ถัดไปเมื่อถึงเป้าแล้ว (Step 2 wander behavior):
        ///  • ทอย 20% ข้ามโซน → สุ่ม ConnectedLocationId เรียก MoveNpc() ทันทีตอนตั้ง target
        ///    (ไม่ใช่ detect ข้ามขอบระหว่างทาง) — MoveNpc seed ตำแหน่งที่จุดกึ่งกลางโซนใหม่ให้
        ///  • ไม่งั้น → สุ่มจุดในรัศมี WanderRadius รอบ LocationDef.WorldX/Y ของโซนปัจจุบัน
        ///    (pattern เดียวกับ WorldItemSystem.ZoneSpread — ไม่เดา bounds ที่ไม่มีจริง)
        ///  • ตั้ง Activity ตามสถานะ: Traveling ขณะมีเป้าให้เดิน, Idle เมื่อไม่มี
        ///
        /// ⚠️ Step 4 note: method นี้ (re-target-on-arrival) จะถูก deprecate —
        /// การเลือก target ทั้งหมดย้ายไป IUtilityAction / KillerPlanner
        /// NpcMovementSystem จะเหลือหน้าที่ "เดินเข้าหา target ที่มีอยู่" เท่านั้น
        /// </summary>
        public void PickNextTarget(NpcState npc)
        {
            var locationId = npc.CurrentLocationId;
            if (string.IsNullOrEmpty(locationId)) return;
            if (!_data.LocationDefs.TryGetValue(locationId, out var locationDef)) return;

            // ทอยข้ามโซน — ต้องมี connected location ให้ไปจริง
            var connections = locationDef.ConnectedLocationIds;
            if (connections != null && connections.Count > 0 && _rng.NextDouble() < CrossZoneChance)
            {
                var destinationId = connections[_rng.Next(connections.Count)];
                if (destinationId != locationId)
                {
                    // MoveNpc ทันทีตอนตั้ง target — CurrentLocationId เปลี่ยนที่นี่จุดเดียว
                    // และ seed Position ที่จุดกึ่งกลางโซนใหม่ (กัน warp (0,0)) แล้วค่อย
                    // สุ่ม target ใหม่ในโซนนั้น (โค้ดด้านล่างอ่าน CurrentLocationId ที่เพิ่งเปลี่ยน)
                    _npcDirector.MoveNpc(npc.Id, destinationId);
                    if (!_data.LocationDefs.TryGetValue(npc.CurrentLocationId, out locationDef))
                        return;
                }
            }

            // สุ่มจุดในรัศมี wander รอบจุดกึ่งกลางโซน — สูตร offset เดียวกับ
            // WorldItemSystem.RollLoot (ZoneSpread)
            var offsetX = (float)(_rng.NextDouble() * 2 - 1) * WanderRadius;
            var offsetY = (float)(_rng.NextDouble() * 2 - 1) * (WanderRadius * 0.6f);

            npc.TargetX = locationDef.WorldX + offsetX;
            npc.TargetY = locationDef.WorldY + offsetY;
            npc.Activity = NpcActivityState.Traveling;
        }
    }
}
