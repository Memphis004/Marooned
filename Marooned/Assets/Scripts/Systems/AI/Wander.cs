using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems.AI
{
    /// <summary>
    /// Helper การเดินร่วมของ AI (Step 4) — ห่อหุ้ม "ตั้ง target ในโซน" + "ตั้ง
    /// Activity" ให้ action ทุกตัวใช้ pattern เดียวกัน
    ///
    ///  • จุด wander สุ่มในรัศมีรอบ LocationDef.WorldX/Y (ZoneSpread pattern เดียวกับ
    ///    WorldItemSystem — ไม่เดา bounds ที่ไม่มีจริง)
    ///  • ข้ามโซน "ตอนตั้ง target" ผ่าน MoveNpc() เท่านั้น (Single Source of Truth)
    ///    และ MoveNpc เป็นคน seed Position/Target ที่กึ่งกลางโซนใหม่ (Seeding Rule)
    ///  • ป้องกัน "แย่ง target": caller ควรเช็ค HasPendingTarget ก่อนตั้งเป้าใหม่
    ///
    /// Static class (ไม่มี state) — Random แยกต่อผู้เรียก (AI แต่ละตัวมีของตัวเอง)
    /// </summary>
    public static class Wander
    {
        /// <summary>npc กำลังเดินไปเป้าที่ยังไม่ถึง (ระยะ > threshold) อยู่หรือไม่</summary>
        public static bool HasPendingTarget(NpcState npc, float arrivalThreshold = 0.1f)
        {
            var dx = npc.TargetX - npc.PositionX;
            var dy = npc.TargetY - npc.PositionY;
            return dx * dx + dy * dy > arrivalThreshold * arrivalThreshold;
        }

        /// <summary>
        /// ตั้ง target สุ่มในรัศมี wander รอบจุดกึ่งกลางโซนปัจจุบันของ NPC
        /// (โค้ดเดิมของ PickNextTarget ส่วนในโซน — ย้ายมาเป็น helper กลาง)
        /// คืน false ถ้าหา location def ของโซนปัจจุบันไม่เจอ (ไม่ mutate)
        /// </summary>
        public static bool SetRandomTargetInZone(NpcState npc, UtilityContext ctx, Random rng, float wanderRadius = 2.5f)
        {
            var locationId = npc.CurrentLocationId;
            if (string.IsNullOrEmpty(locationId)) return false;
            if (!ctx.Data.LocationDefs.TryGetValue(locationId, out var locDef)) return false;

            npc.TargetX = locDef.WorldX + (float)(rng.NextDouble() * 2 - 1) * wanderRadius;
            npc.TargetY = locDef.WorldY + (float)(rng.NextDouble() * 2 - 1) * (wanderRadius * 0.6f);
            npc.Activity = NpcActivityState.Traveling;
            return true;
        }

        /// <summary>
        /// เดินไปโซนอื่น: สุ่ม connected location แล้ว MoveNpc ทันที (จบการตัดสินใจที่นี่ —
        /// MoveNpc seed ตำแหน่งที่กึ่งกลางโซนใหม่ + Target=Position)
        /// คืน false ถ้าโซนปัจจุบันไม่มีทางออก (ไม่ mutate อะไร)
        /// </summary>
        public static bool MoveToRandomConnectedZone(NpcState npc, UtilityContext ctx, Random rng)
        {
            if (!ctx.Data.LocationDefs.TryGetValue(npc.CurrentLocationId, out var locDef))
                return false;
            var connections = locDef.ConnectedLocationIds;
            if (connections == null || connections.Count == 0) return false;

            var destination = connections[rng.Next(connections.Count)];
            if (string.IsNullOrEmpty(destination) || destination == npc.CurrentLocationId) return false;

            ctx.NpcDirector.MoveNpc(npc.Id, destination);
            return true;
        }

        /// <summary>
        /// เดินไปยังโซนที่ระบุ (requireConnection=true จะเดินได้เฉพาะโซนที่เชื่อมกันจริง)
        /// ใช้โดย SeekFoodAction / KillerPlanner.SeekingWeapon / BuildingAlibi
        /// ถ้าอยู่ในโซนเป้าหมายแล้ว → สุ่มจุดในโซนให้เดินเข้าไปข้างใน
        /// คืน false ถ้าไปไม่ได้/หาโซนไม่เจอ (ไม่ mutate)
        /// </summary>
        public static bool MoveToZone(NpcState npc, UtilityContext ctx, string destinationId, Random rng, bool requireConnection = true)
        {
            if (string.IsNullOrEmpty(destinationId)) return false;
            if (!ctx.Data.LocationDefs.TryGetValue(destinationId, out var destinationDef)) return false;

            if (destinationId == npc.CurrentLocationId)
                return SetRandomTargetInZone(npc, ctx, rng);

            if (requireConnection)
            {
                if (!ctx.Data.LocationDefs.TryGetValue(npc.CurrentLocationId, out var currentDef))
                    return false;
                var connections = currentDef.ConnectedLocationIds;
                if (connections == null || !connections.Contains(destinationId))
                    return false;
            }

            ctx.NpcDirector.MoveNpc(npc.Id, destinationDef.Id);
            return true;
        }

        /// <summary>หาโซนที่ loot table มีการ์ดตาม category (สำหรับ SeekingWeapon)</summary>
        public static List<string> FindZonesWithCardCategory(UtilityContext ctx, Marooned.Shared.CardCategory category)
        {
            var cardIds = ctx.Data.CardDefs.Values
                .Where(c => c.Category == category)
                .Select(c => c.Id)
                .ToHashSet();

            var zones = new List<string>();
            foreach (var kv in ctx.Data.LocationDefs)
            {
                if (kv.Value.LootTable == null) continue;
                if (kv.Value.LootTable.Keys.Any(id => cardIds.Contains(id)))
                    zones.Add(kv.Key);
            }
            return zones;
        }
    }
}
