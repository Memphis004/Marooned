using Marooned.Shared;
using Marooned.Systems;
using Marooned.Systems.AI;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Marooned.Tests.Editor
{
    /// <summary>
    /// Lab C Phase 2.5B — Debug query API (Task 1) contract เทส:
    ///  • InnocentUtilityAI.GetCurrentActionId(npcId) — null เมื่อยังไม่เคย Tick
    ///  • KillerPlanner.GetCurrentPhase(npcId) — Patrolling เมื่อยังไม่เคย Tick
    /// (method ทั้งคู่อ่านอย่างเดียว — ใช้โดย NpcDebugOverlay และเทส)
    /// </summary>
    public class NpcDebugQueryEditModeTests
    {
        LubanDataService _data;
        UtilityContext _ctx;
        InnocentUtilityAI _innocentAi;
        KillerPlanner _killer;

        [SetUp]
        public void SetUp()
        {
            _data = new LubanDataService();
            _ctx = new UtilityContext(_data, new GameStateProvider());
            _innocentAi = new InnocentUtilityAI(_ctx);
            _killer = new KillerPlanner(_ctx);
        }

        [Test]
        public void GetCurrentActionId_BeforeFirstTick_ReturnsNull()
        {
            Assert.IsNull(_innocentAi.GetCurrentActionId("npc_01"),
                "ยังไม่เคย Tick — ยังไม่มี action ที่ชนะ re-evaluate");
        }

        [Test]
        public void GetCurrentPhase_BeforeFirstTick_ReturnsPatrolling()
        {
            Assert.AreEqual(KillerPlanner.KillerPhase.Patrolling, _killer.GetCurrentPhase("killer_01"),
                "ยังไม่เคย Tick — default phase = Patrolling (Tick แรกเป็นผู้ seed state)");
        }

        [Test]
        public void GetCurrentActionId_AfterTick_ReflectsChosenAction()
        {
            var npc = new NpcState { Id = "npc_01", CurrentLocationId = "beach" };
            npc.Survival.Fear = 90f; // FleeToSafeZone ชนะชัวร์ (Fear/100 = 0.9 > IdleWander 0.1)

            _innocentAi.Tick(npc, 0.1f);

            Assert.AreEqual("FleeToSafeZone", _innocentAi.GetCurrentActionId("npc_01"),
                "หลัง Tick ต้องได้ action ที่ชนะ re-evaluate ล่าสุด");
        }

        [Test]
        public void GetCurrentPhase_AfterTick_MatchesInternalState()
        {
            var killer = new NpcState { Id = "killer_01", CurrentLocationId = "beach" };

            // TickPatrolling อาจทอยข้ามโซน 20% แล้ว return ก่อนเช็คอาวุธ (tick เดียวจึงไม่ deterministic)
            // — แต่ weapon check ไม่ผูกกับ wander branch: tick ถัดไปเช็คอาวุธทันทีเสมอ
            for (var i = 0; i < 5 && _killer.GetCurrentPhase("killer_01") != KillerPlanner.KillerPhase.SeekingWeapon; i++)
                _killer.Tick(killer, 0.1f);

            Assert.AreEqual(KillerPlanner.KillerPhase.SeekingWeapon, _killer.GetCurrentPhase("killer_01"),
                "ไม่มีอาวุธ → ภายในไม่กี่ tick ต้องเข้า SeekingWeapon (GetCurrentPhase สะท้อน state จริง)");
        }
    }
}
