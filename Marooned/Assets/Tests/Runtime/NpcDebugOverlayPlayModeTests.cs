#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.Systems.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Lab C Phase 2.5B — NpcDebugOverlay PlayMode evidence (Tests A-C) ใน scene จริง:
    ///  • A: overlay toggle เปิด/ปิดได้ — เทสเรียก Toggle() ซึ่งเป็น method เดียวกับ
    ///    ที่ Update เรียกเมื่อกด F12 (deterministic เท่ากัน)
    ///  • B: Hunger ที่โชว์ขยับตามเวลาจริง (อ่านจาก npc.Survival.Hunger — ยืนยันว่า
    ///    NpcSurvivalSystem tick อยู่ ไม่ใช่ overlay implement decay เอง)
    ///  • C: Hunger > 55 → AI column เปลี่ยนเป็น SeekFood
    ///
    /// หลักฐาน: TestEvidence/lab-c-phase2-5b-debug-overlay/ (txt + png)
    /// </summary>
    public class NpcDebugOverlayPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/lab-c-phase2-5b-debug-overlay";

        GameLifetimeScope _scope;
        NpcDirectorSystem _director;
        NpcSurvivalSystem _survival;
        NpcDebugOverlay _overlay;
        readonly StringBuilder _log = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _log.Clear(); // evidence ของแต่ละเทสต้องโดด ๆ ไม่ปน log ของเทสก่อนหน้า
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
            {
                var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene");
                while (load != null && !load.isDone) yield return null;
            }

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
                if (_scope != null && Application.isPlaying)
                {
                    _director = _scope.Container.Resolve<NpcDirectorSystem>();
                    if (_director != null && _director.Npcs.Count > 0) break;
                }
                yield return null;
            }
            Assert.That(_scope != null, "GameLifetimeScope ไม่เจอใน play mode");
            Assert.That(_director != null && _director.Npcs.Count > 0, "ไม่มี NPC ถูก register");
            _survival = _scope.Container.Resolve<NpcSurvivalSystem>();

            _overlay = UnityEngine.Object.FindFirstObjectByType<NpcDebugOverlay>();
            Assert.That(_overlay != null, "NpcDebugOverlay ไม่ได้ถูกใส่ใน SampleScene (บน GameObject GameLifetimeScope)");

            // normalize ให้แต่ละเทสเริ่มจาก overlay ปิด (กัน state ค้างจากเทสก่อนหน้า)
            if (_overlay.Visible) _overlay.Toggle();

            // แช่แข็ง killer — กัน AI ฆ่าต่อระหว่างเทส
            var killer = _director.Npcs.Values.FirstOrDefault(n => n.Role == NpcRole.Killer && n.IsAlive);
            if (killer != null) killer.KillCooldownRemaining = 9999f;
        }

        [UnityTest]
        public IEnumerator Toggle_OnOff_Works()
        {
            Assert.IsFalse(_overlay.Visible, "เริ่มต้นต้องปิด");

            _overlay.Toggle();
            yield return null;
            Assert.IsTrue(_overlay.Visible, "Toggle ครั้งแรก = เปิด");
            _log.AppendLine("[A] F12/Toggle -> ON — overlay แสดงตาราง Id|Role|Loc|Pos|Act|Hunger|AI");

            // อยู่เปิดค้าง 2 วิ — ยืนยัน OnGUI วาดได้โดยไม่ error ขณะ world tick ต่อ
            yield return new WaitForSeconds(2f);
            _log.AppendLine("[A] overlay ค้างเปิด 2 วิ ขณะเกม tick ต่อ — ไม่มี error");
            ScreenCapture.CaptureScreenshot(EvidencePath("overlay-on.png"));

            _overlay.Toggle();
            yield return null;
            Assert.IsFalse(_overlay.Visible, "Toggle ครั้งที่สอง = ปิด");
            _log.AppendLine("[A] F12/Toggle -> OFF");
            WriteEvidence("DebugOverlay_ToggleOnOff", _log.ToString());
        }

        [UnityTest]
        public IEnumerator HungerDisplayed_MovesWithRealTime()
        {
            var npc = _director.Npcs.Values.First(n => n.IsAlive && n.Role != NpcRole.Killer);
            npc.Survival.Hunger = 10f;

            _overlay.Toggle(); // เปิด overlay ระหว่างวัด — ให้เห็นค่าในภาพด้วย

            var before = npc.Survival.Hunger;
            yield return new WaitForSeconds(3f);
            var after = npc.Survival.Hunger;

            _log.AppendLine($"[B] Hunger (โชว์บน overlay) 3 วิจริง: {before:F1} -> {after:F1} " +
                            $"(expect ~+6 = NpcSurvivalSystem tick จริง, ไม่ใช่ overlay implement เอง)");
            Assert.GreaterOrEqual(after - before, 4f, "Hunger ต้องเพิ่ม ~2/วิ ผ่าน tick จริงของ NpcSurvivalSystem");
            Assert.LessOrEqual(after - before, 9f, "Hunger ไม่ควรเร็วผิดปกติ");

            _overlay.Toggle(); // ปิดคืน
            WriteEvidence("DebugOverlay_HungerLive", _log.ToString());
        }

        [UnityTest]
        public IEnumerator AiColumn_ShowsSeekFood_WhenHungerAbove55()
        {
            var npc = _director.Npcs.Values.First(n => n.IsAlive && n.Role != NpcRole.Killer);
            var innocentAi = _scope.Container.Resolve<InnocentUtilityAI>();

            // รอให้ NPC พ้นช่วง transition ก่อน — ระหว่างข้ามโซน InnocentUtilityAI.Tick
            // ถูก gate ด้วย IsTransitioning (ไม่ re-evaluate) ทำให้ choice เก่าค้าง
            // (ห้าม mutate TransitionPhase เอง — จะ desync timer ของ NpcZoneTransitionSystem)
            var settle = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < settle && Wander.IsTransitioning(npc))
                yield return null;
            Assert.IsFalse(Wander.IsTransitioning(npc), "NPC ต้องพ้น transition ภายใน 15 วิ ก่อนวัด AI column");

            npc.Survival.Hunger = 80f; // > 55 → SeekFoodAction ชนะ (0.8 > IdleWander 0.1)
            npc.Survival.Fear = 0f;
            npc.Survival.Curiosity = 0f;
            npc.Survival.LastNoiseLocationId = null;
            npc.TargetX = npc.PositionX; // ล้าง pending target — ให้ action ตั้งเป้าใหม่ได้
            npc.TargetY = npc.PositionY;

            _overlay.Toggle(); // เปิด overlay ให้ภาพหลักฐานเห็นคอลัมน์ AI

            // รอ re-evaluate (≤1.5 วิ) + action เริ่มเดิน (bounded 8 วิ)
            var deadline = Time.realtimeSinceStartup + 8f;
            var sawSeekFood = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (innocentAi.GetCurrentActionId(npc.Id) == "SeekFood")
                {
                    sawSeekFood = true;
                    break;
                }
            }

            _log.AppendLine($"[C] Hunger=80 (>55) -> AI column = '{innocentAi.GetCurrentActionId(npc.Id)}' (sawSeekFood={sawSeekFood})");
            Assert.IsTrue(sawSeekFood, "Hunger > 55 ต้องเห็น SeekFood ในคอลัมน์ AI ภายใน 5 วิ");
            ScreenCapture.CaptureScreenshot(EvidencePath("overlay-seek-food.png"));

            _overlay.Toggle(); // ปิดคืน
            WriteEvidence("DebugOverlay_SeekFoodColumn", _log.ToString());
        }

        static string EvidencePath(string file)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var dir = Path.Combine(repoRoot, EvidenceDir);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }

        private void WriteEvidence(string test, string body)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceDir));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{test}.txt"),
                $"=== {test} — {DateTime.Now:HH:mm:ss} ===\n{body}\nRESULT: PASS\n");
            Debug.Log($"[DebugOverlayPlayMode] {test} PASS — evidence: {EvidenceDir}/");
        }
    }
}
#endif
