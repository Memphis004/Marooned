using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems.AI
{
    /// <summary>
    /// สมองฝั่ง Innocent (Step 4) — utility scoring แบบ stateless ต่อ action:
    /// re-evaluate ทุก ~1.5 วิต่อตัว (กัน jitter การเปลี่ยนใจ) แล้ว execute
    /// action ที่ชนะทุก tick จนถึงรอบ re-evaluate ถัดไป
    ///
    ///  • IdleWanderAction — สุ่มจุดใน location ปัจจุบัน (baseline, score ต่ำ)
    ///  • SeekFoodAction — score = Hunger normalize; execute = เดินไป location ที่มี
    ///    food; ถ้า inventory มี food card → consume + คืน Hunger
    ///  • InvestigateNoiseAction — score = Curiosity×(1-Fear) normalize; เดินไปจุดเสียง
    ///    (Phase 2.5A: จุดเสียงจาก NpcSurvivalSystem motive hook)
    ///  • FleeToSafeZoneAction — score = Fear normalize; ออกจากโซนที่เห็นศพ
    ///    (Phase 2.5A: Fear จาก NpcSurvivalSystem motive hook)
    ///
    /// Information Hiding: อ่าน NpcState.Survival/Inventory (ground truth) ได้
    /// เพราะระบบนี้รันใน Unity — ห้ามผลลัพธ์ใดๆ ของระบบนี้หลุดออก MCP response
    /// </summary>
    public class InnocentUtilityAI
    {
        /// <summary>ระยะห่างการ re-evaluate ต่อ NPC (วินาที) — กันเปลี่ยนใจทุกเฟรม</summary>
        public float ReEvaluateInterval = 1.5f;

        private readonly List<IUtilityAction> _actions;
        private readonly UtilityContext _ctx;
        private readonly Random _rng = new();

        // state ต่อ NPC: action ที่ชนะล่าสุด + ตัวนับถอย re-evaluate
        private readonly Dictionary<string, IUtilityAction> _currentChoice = new();
        private readonly Dictionary<string, float> _reEvalTimers = new();

        public InnocentUtilityAI(UtilityContext ctx)
        {
            _ctx = ctx;
            _actions = new List<IUtilityAction>
            {
                new IdleWanderAction(),
                new SeekFoodAction(),
                new InvestigateNoiseAction(), // สำรวจจุดเสียง (Phase 2.5A: ใช้จริงแล้ว)
                new FleeToSafeZoneAction(),   // หนีเมื่อกลัว (Phase 2.5A: ใช้จริงแล้ว)
            };
        }

        /// <summary>เรียกทุก tick จาก NpcDirectorSystem.TickBehavior (NPC ที่ยังมีชีวิต)</summary>
        public void Tick(NpcState npc, float deltaSeconds)
        {
            // Hybrid Transition Points (Part 2): ระหว่างข้ามโซน (เดินเข้าจุดเชื่อม/exit/
            // enter) — ห้าม re-evaluate/execute action กัน AI ตั้ง target ซ้อนทับ
            // transition target (Test C evidence: log นี้จะไม่ปรากฏระหว่าง transition)
            if (Wander.IsTransitioning(npc)) return;

            // ลบ state ของ NPC ที่โดนเก็บอۆออก (SetupRound ใหม่) — กัน dict โต
            if (_reEvalTimers.Count > 0 && !_reEvalTimers.ContainsKey(npc.Id) && _currentChoice.ContainsKey(npc.Id))
                _currentChoice.Remove(npc.Id);

            // --- re-evaluate ทุก ~1.5 วิ ---
            _reEvalTimers.TryGetValue(npc.Id, out var timer);
            timer -= deltaSeconds;
            if (timer <= 0f || !_currentChoice.ContainsKey(npc.Id))
            {
                timer = ReEvaluateInterval;

                IUtilityAction best = null;
                var bestScore = 0f;
                foreach (var action in _actions)
                {
                    var score = action.Score(npc, _ctx);
                    if (score > bestScore) { bestScore = score; best = action; }
                }

                var previous = _currentChoice.TryGetValue(npc.Id, out var prev) ? prev : null;
                if (best != null && !ReferenceEquals(best, previous))
                {
                    _currentChoice[npc.Id] = best;
                    // log การตัดสินใจ (Test A ต้องใช้) — ตัด log ซ้ำถ้าเปลี่ยนกลับไปมาเร็ว
                    UnityEngine.Debug.Log($"[InnocentAI] {npc.Id} chose {best.Id} (score={bestScore:F2})");
                }
                else if (best == null)
                {
                    _currentChoice[npc.Id] = null; // ไม่มี action สนใจ — เดี๋ยวว่างไว้
                }
            }
            _reEvalTimers[npc.Id] = timer;

            // --- execute ทุก tick ---
            if (_currentChoice.TryGetValue(npc.Id, out var chosen) && chosen != null)
                chosen.Execute(npc, _ctx, deltaSeconds);
        }

        /// <summary>
        /// Debug/เทส (Lab C Phase 2.5B) — action id ที่ชนะ re-evaluate ล่าสุดของ NPC
        /// (null = ยังไม่เลือก / ไม่มี action สนใจ) อ่านอย่างเดียวไม่ mutate —
        /// ใช้โดย NpcDebugOverlay (F12) และเทส
        /// </summary>
        public string GetCurrentActionId(string npcId) =>
            _currentChoice.TryGetValue(npcId, out var a) ? a?.Id : null;
    }

    /// <summary>baseline — สุ่มจุดในโซนปัจจุบัน (รวมทอยข้ามโซนเบาๆ)</summary>
    public class IdleWanderAction : IUtilityAction
    {
        public string Id => "IdleWander";
        private Random _rng = new();

        public float Score(NpcState npc, UtilityContext ctx) => 0.1f; // เตี้ยสุด — fallback

        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds)
        {
            // กำลังเดินอยู่ → ปล่อยให้ NpcMovementSystem ทำงาน ไม่แตะ target (กันแย่ง)
            if (Wander.HasPendingTarget(npc)) return;

            // ทอยข้ามโซน 20% — pattern เดิมของ Step 2 แต่ตัดสินใจที่ AI แล้ว
            if (_rng.NextDouble() < 0.2 && Wander.MoveToRandomConnectedZone(npc, ctx, _rng))
                return;

            Wander.SetRandomTargetInZone(npc, ctx, _rng);
        }
    }

    /// <summary>
    /// หิว → หาอาหาร: score = Hunger (0-100, ยิ่งสูงยิ่งหิว)
    /// execute: ถ้ามีการ์ด food ใน inventory → consume + คืน Hunger ทันที
    /// ยังไม่มี → เดินไปโซนที่ loot table มี food card
    /// </summary>
    public class SeekFoodAction : IUtilityAction
    {
        public string Id => "SeekFood";

        private readonly Random _rng = new();

        /// <summary>การ์ดที่ถือว่า "อาหาร" (cardId → คืน Hunger ต่อชิ้น)</summary>
        private static readonly Dictionary<string, float> FoodCards = new()
        {
            ["food_coconut"] = 15f,
            ["cooked_fish"] = 25f,
            ["raw_fish"] = 10f,
            ["berry"] = 5f,
        };

        public float Score(NpcState npc, UtilityContext ctx)
        {
            // Hunger สูง = หิวมาก — เริ่มสนใจเมื่อเกิน 55, ชนะ IdleWander ชัวร์ที่ 80+
            return npc.Survival.Hunger <= 55f ? 0f : npc.Survival.Hunger / 100f;
        }

        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds)
        {
            // 1) มีอาหารในกระเป๋า → กินเลย (ไม่ต้องเดิน)
            var carried = FoodCards.Keys.FirstOrDefault(FoodHasKey(npc));
            if (carried != null)
            {
                npc.Inventory.RemoveItem(carried);
                npc.Survival.Hunger = Math.Clamp(npc.Survival.Hunger - FoodCards[carried], 0f, 100f);
                UnityEngine.Debug.Log($"[InnocentAI] {npc.Id} ate {carried} (Hunger -> {npc.Survival.Hunger:F0})");
                return;
            }

            // 2) กำลังเดินหาอยู่ → ไม่แตะ target
            if (Wander.HasPendingTarget(npc)) return;

            // 3) เดินไปโซนที่มี food card ใน loot table (โซนไหนก็ได้ที่เชื่อมถึง — เลือกใกล้สุดตามระยะ)
            var foodZones = FindFoodZones(ctx);
            if (foodZones.Count == 0) return;

            var target = foodZones
                .OrderBy(z => DistanceTo(ctx, npc, z))
                .First();

            if (!Wander.MoveToZone(npc, ctx, target, _rng, requireConnection: true))
            {
                // เชื่อมตรงไม่ถึง (ไม่ใช่ adjacent) — fallback: ขยับไปโซนข้างๆ ก่อน
                Wander.MoveToRandomConnectedZone(npc, ctx, _rng);
            }
        }

        private static Func<string, bool> FoodHasKey(NpcState npc) => id => npc.Inventory.HasItem(id);

        private static List<string> FindFoodZones(UtilityContext ctx)
        {
            var zones = new List<string>();
            foreach (var kv in ctx.Data.LocationDefs)
            {
                if (kv.Value.LootTable == null) continue;
                if (kv.Value.LootTable.Keys.Any(FoodCards.ContainsKey))
                    zones.Add(kv.Key);
            }
            return zones;
        }

        private static float DistanceTo(UtilityContext ctx, NpcState npc, string zoneId)
        {
            if (!ctx.Data.LocationDefs.TryGetValue(zoneId, out var def)) return float.MaxValue;
            var dx = def.WorldX - npc.PositionX;
            var dy = def.WorldY - npc.PositionY;
            return dx * dx + dy * dy;
        }
    }

    /// <summary>
    /// หนีภัยเมื่อกลัวสุด ๆ (Lab C Phase 2.5A — เดิมเป็น stub score 0):
    /// score = Fear normalize (0-1 เหมือน SeekFoodAction) — Fear สูง → ชนะ IdleWander
    /// execute: ขอทางออกจากโซนปัจจุบันผ่าน Wander.MoveToRandomConnectedZone เท่านั้น
    /// (ห้ามเขียน movement/teleport เอง) — ไม่มี state "หนีสำเร็จ" พิเศษ: ปล่อยให้
    /// NpcSurvivalSystem decay พา Fear ลง แล้วรอบ re-evaluate ถัดไป IdleWander ชนะเอง
    /// </summary>
    public class FleeToSafeZoneAction : IUtilityAction
    {
        public string Id => "FleeToSafeZone";

        private readonly Random _rng = new();

        // Fear 0-100 normalize เป็น 0-1 (เหมือน SeekFoodAction) — Fear > 10 ชนะ IdleWander
        public float Score(NpcState npc, UtilityContext ctx) => npc.Survival.Fear / 100f;

        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds)
        {
            // กำลังเดินหนีอยู่ → ไม่แตะ target (กันแย่ง — stateless ต่อ NPC)
            if (Wander.HasPendingTarget(npc)) return;

            // ทิศทาง "ปลอดภัย" = ออกจากโซนที่เห็นศพ — โซน connected ใดก็ได้
            // (NpcSurvivalSystem เป็นคนเพิ่ม Fear ตอนเห็นศพ — action นี้แค่เดินหนี)
            Wander.MoveToRandomConnectedZone(npc, ctx, _rng);
            // ไม่ return true/finished — ปล่อยให้ decay ทำให้ Fear ตกแล้ว action เปลี่ยนเอง
        }
    }

    /// <summary>
    /// สำรวจจุดที่ได้ยินเสียง (Lab C Phase 2.5A — เดิมเป็น stub score 0):
    /// score = Curiosity normalize × กลัวน้อย (1 - Fear normalize) — อยากรู้แต่ต้องไม่กลัวสุด ๆ
    /// (เช่น Curiosity=80, Fear=30 → 0.8 × 0.7 = 0.56)
    /// execute: เดินไปโซนจุดเสียง (LastNoiseLocationId จดโดย NpcSurvivalSystem ตอน
    /// ได้ยินเสียงจาก NpcEliminatedMessage) ผ่าน Wander.MoveToZone เท่านั้น —
    /// ถึงแล้วให้ NpcMovementSystem idle ธรรมชาติ + decay พาไป action อื่น
    /// (ห้ามเพิ่ม timer "หยุดดู N วิ" — ขัดกับกฎ stateless ของ IUtilityAction)
    /// </summary>
    public class InvestigateNoiseAction : IUtilityAction
    {
        public string Id => "InvestigateNoise";

        private readonly Random _rng = new();

        public float Score(NpcState npc, UtilityContext ctx)
        {
            // ไม่มีจุดเสียงให้สำรวจ → ไม่สนใจเลย (เคลียร์โดย decay threshold ของ NpcSurvivalSystem)
            if (npc.Survival.LastNoiseLocationId == null) return 0f;

            // Curiosity normalize × ความกลัวน้อย — กลัวมาก (= เห็นศพเอง) จะไม่ไปสำรวจ
            return (npc.Survival.Curiosity / 100f) * (1f - npc.Survival.Fear / 100f);
        }

        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds)
        {
            // กำลังเดินไปจุดเสียงอยู่ → ไม่แตะ target
            if (Wander.HasPendingTarget(npc)) return;

            var target = npc.Survival.LastNoiseLocationId;
            if (target == null) return;

            // ถึงโซนจุดเสียงแล้ว → MoveToZone สุ่มจุดในโซนให้เดินเข้าไป "ดูรอบ ๆ" ต่อ
            // (ยังไม่ adjacent → MoveToZone คืน false ไม่ mutate — รอ decay พาไป action อื่น)
            Wander.MoveToZone(npc, ctx, target, _rng, requireConnection: true);
        }
    }
}
