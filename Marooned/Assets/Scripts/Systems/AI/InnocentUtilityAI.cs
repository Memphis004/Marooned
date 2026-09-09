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
    ///  • InvestigateNoiseAction / FleeToSafeZoneAction — stub score 0 (เปิดใช้ทีหลัง)
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
                new InvestigateNoiseAction(), // stub score 0
                new FleeToSafeZoneAction(),   // stub score 0
            };
        }

        /// <summary>เรียกทุก tick จาก NpcDirectorSystem.TickBehavior (NPC ที่ยังมีชีวิต)</summary>
        public void Tick(NpcState npc, float deltaSeconds)
        {
            // ลบ state ของ NPC ที่โดนเก็บออก (SetupRound ใหม่) — กัน dict โต
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

        /// <summary>ตรวจสอบ (เทส): action ที่ถูกเลือกอยู่ของ NPC</summary>
        public string CurrentActionId(string npcId) =>
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

    /// <summary>stub — เดินไปสำรวจจุดที่ได้ยินเสียง (ยังไม่มีระบบเสียง/เหตุการณ์)</summary>
    public class InvestigateNoiseAction : IUtilityAction
    {
        public string Id => "InvestigateNoise";
        public float Score(NpcState npc, UtilityContext ctx) => 0f; // ยังไม่เปิดใช้
        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds) { }
    }

    /// <summary>stub — หนีไปโซนปลอดภัยเมื่อ Fear สูง (จะเปิดใช้กับระบบเห็นศพ)</summary>
    public class FleeToSafeZoneAction : IUtilityAction
    {
        public string Id => "FleeToSafeZone";
        public float Score(NpcState npc, UtilityContext ctx) => 0f; // ยังไม่เปิดใช้
        public void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds) { }
    }
}
