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
    /// Lab C Phase 2.5A — Living NPCs: PlayMode evidence ใน scene จริง
    /// (GameTickDriver tick จริงทุกเฟรม: Survival → Director → Movement → ZoneTransition,
    /// MessagePipe จริง, KillerPlanner จริง)
    ///
    ///  • Test A: decay ทำงานกับเวลาจริง — Hunger เพิ่ม ~2/วิ, Fear decay จากค่าที่ตั้ง
    ///  • Test B: kill ripple แบบ deterministic — เคลียร์โซน (no-witness rule),
    ///    TryEliminate ผ่าน director (publish จริงผ่าน MessagePipe จริง) →
    ///    NPC ใน connected location ได้ Curiosity +30 + hint → InvestigateNoiseAction
    ///    ชนะ re-evaluate → เดินผ่านจุดเชื่อม (FSM เดิม) ไปโซนจุดเสียงจริง ๆ
    ///
    /// หลักฐาน: TestEvidence/lab-c-phase2-5-survival/ (txt + png)
    /// </summary>
    public class NpcSurvivalPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/lab-c-phase2-5-survival";

        GameLifetimeScope _scope;
        NpcDirectorSystem _director;
        LubanDataService _data;
        NpcSurvivalSystem _survival;
        readonly StringBuilder _log = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // UTF เข้า play ด้วย empty test scene — โหลด scene เกมเอง (pattern เดียวกับ
            // NpcZoneTransitionPlayModeTests)
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

            _data = _scope.Container.Resolve<LubanDataService>();
            _survival = _scope.Container.Resolve<NpcSurvivalSystem>();
            Assert.That(_survival != null, "resolve NpcSurvivalSystem ไม่ได้");

            // แช่แข็ง killer ตัวจริงของรอบ — กัน AI ฆ่าต่อระหว่างเทส (cooldown 9999 วิ)
            var killer = _director.Npcs.Values.FirstOrDefault(n => n.Role == NpcRole.Killer && n.IsAlive);
            if (killer != null) killer.KillCooldownRemaining = 9999f;
        }

        [UnityTest]
        public IEnumerator SurvivalTick_DecayInLiveScene()
        {
            var npc = _director.Npcs.Values.First(n => n.IsAlive && n.Role != NpcRole.Killer);

            // Test A: Hunger ต้องเพิ่มตามเวลาจริง (~2/วิ ผ่าน GameTickDriver จริง)
            npc.Survival.Hunger = 10f;
            npc.Survival.Fear = 80f;
            var hungerBefore = npc.Survival.Hunger;
            var fearBefore = npc.Survival.Fear;

            yield return new WaitForSeconds(3f);

            var hungerAfter = npc.Survival.Hunger;
            var fearAfter = npc.Survival.Fear;
            _log.AppendLine($"[A] t+3s real: Hunger {hungerBefore:F1} -> {hungerAfter:F1} (expect ~+6), " +
                            $"Fear {fearBefore:F1} -> {fearAfter:F1} (expect ~-18)");

            Assert.GreaterOrEqual(hungerAfter - hungerBefore, 4f,
                "Hunger ต้องเพิ่ม ~2/วิ ผ่าน GameTickDriver จริง (3 วิ = +6, ยอม margin)");
            Assert.LessOrEqual(hungerAfter - hungerBefore, 9f, "Hunger ไม่ควรเร็วผิดปกติ");
            Assert.Less(fearAfter, fearBefore, "Fear ต้อง decay ลง");
            Assert.GreaterOrEqual(fearAfter, 0f, "Fear clamp ที่ 0");

            WriteEvidence("SurvivalTick_DecayInLiveScene", "PASS (decay ทำงานกับเวลาจริง)");
        }

        [UnityTest]
        public IEnumerator KillRipple_HearerInvestigates_InLiveScene()
        {
            // ---- เตรียม scene แบบ deterministic ----
            var killer = _director.Npcs.Values.First(n => n.Role == NpcRole.Killer && n.IsAlive);
            var alive = _director.Npcs.Values.Where(n => n.IsAlive && n != killer).ToList();
            Assert.That(alive.Count >= 2, "ต้องมี innocent อย่างน้อย 2 (เหยื่อ + ผู้สังเกต)");

            var victim = alive[0];
            var observer = alive[1];

            // เคลียร์ beach: ย้ายทุกคน (ยกเว้นเหยื่อ) ไป deep_jungle — ไม่ connected กับ beach
            // (beach connections = jungle_edge/cave_entrance; deep_jungle connections = jungle_edge)
            foreach (var n in alive)
                if (n != victim) _director.MoveNpc(n.Id, "deep_jungle");
            _director.MoveNpc(victim.Id, "beach");

            // observer ไป jungle_edge (connected กับ beach → "ได้ยินเสียง")
            _director.MoveNpc(observer.Id, "jungle_edge");
            observer.Survival.Curiosity = 0f;
            observer.Survival.Fear = 0f;

            yield return null; // ให้ reconcile จบก่อนหนึ่งเฟรม

            // ---- kill ผ่าน method กลาง (publish NpcEliminatedMessage จริง) ----
            var (success, reason) = _director.TryEliminate(killer.Id, victim.Id, "beach");
            Assert.IsTrue(success, $"kill ต้องสำเร็จ (reason: {reason})");

            // ---- ripple: observer (connected) ต้องได้ Curiosity +30 + hint ----
            Assert.GreaterOrEqual(observer.Survival.Curiosity, 30f,
                "observer ใน connected location ต้องได้ Curiosity +30 จาก hook จริง");
            Assert.AreEqual("beach", observer.Survival.LastNoiseLocationId, "ต้องจดจุดเสียง");
            _log.AppendLine($"[B] kill {victim.Id} @ beach -> observer {observer.Id} " +
                            $"Curiosity={observer.Survival.Curiosity:F0} noise={observer.Survival.LastNoiseLocationId}");

            // ผู้รอดชีวิตใน deep_jungle ต้องไม่โดน ripple (ไม่ same/connected zone)
            var isolated = alive.FirstOrDefault(n => n != victim && n != observer && n.IsAlive);
            if (isolated != null)
                Assert.LessOrEqual(isolated.Survival.Curiosity, 0.01f,
                    "NPC โซนที่ไม่ connected ต้องไม่โดน ripple");

            // ---- เร่งความสนใจให้เดินทัน decay (ground truth ของเรา — deterministic) ----
            observer.Survival.Curiosity = 90f;

            // ---- รอ observer เดินไป beach ผ่าน FSM จริง (bounded 30 วิเกม) ----
            var screenshotTaken = false;
            var elapsed = 0f;
            while (elapsed < 30f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (!screenshotTaken && observer.CurrentLocationId != "jungle_edge")
                {
                    screenshotTaken = true; // ออกจากโซนเดิมแล้ว = กำลังเดินผ่านจุดเชื่อม
                    ScreenCapture.CaptureScreenshot(EvidencePath("playmode-survival-investigating.png"));
                }
                if (observer.CurrentLocationId == "beach") break;
            }

            _log.AppendLine($"[B] observer reached '{observer.CurrentLocationId}' in {elapsed:F1}s " +
                            $"(transition phase at end: {observer.TransitionPhase}, " +
                            $"Curiosity now {observer.Survival.Curiosity:F0})");
            Assert.AreEqual("beach", observer.CurrentLocationId,
                $"InvestigateNoiseAction ต้องพาเดินผ่านจุดเชื่อมไปโซนจุดเสียงภายใน 30 วิ (อยู่ {observer.CurrentLocationId})");
            Assert.IsTrue(screenshotTaken, "ต้องได้ภาพช่วงกำลังเดินสำรวจ");

            // killer ถูกแช่แข็ง — ต้องไม่แตะ cooldown ของตัวเองนอกจาก tick ลด
            Assert.Greater(killer.KillCooldownRemaining, 9000f, "killer ต้องไม่ฆ่าเพิ่มระหว่างเทส");

            WriteEvidence("KillRipple_HearerInvestigates_InLiveScene", _log.ToString());
        }

        private void WriteEvidence(string test, string body)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceDir));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{test}.txt"),
                $"=== {test} — {DateTime.Now:HH:mm:ss} ===\n{body}\nRESULT: PASS\n");
            Debug.Log($"[SurvivalPlayMode] {test} PASS — evidence: {EvidenceDir}/");
        }

        static string EvidencePath(string file)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var dir = Path.Combine(repoRoot, EvidenceDir);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }
    }
}
