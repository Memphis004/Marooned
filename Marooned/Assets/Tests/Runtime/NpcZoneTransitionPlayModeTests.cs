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
    /// Hybrid Transition Points (Part 2) — PlayMode visual evidence สำหรับ Test A+B
    /// รันผ่าน Test Runner (testMode=PlayMode) — Unity เข้า play mode เอง
    ///   • Test A: NPC เดิน "เห็นจริง" — PositionX/Y ต่อเนื่องระหว่าง WalkingToPoint
    ///     และ chibi GameObject บนจอขยับตาม (screenshot 1)
    ///   • Test B: ถึงจุดเชื่อม → exit ~0.5s (screenshot 2) → ข้ามโซน →
    ///     โผล่ที่ arrival point → enter ~0.3s (screenshot 3) → chibi despawn → เดินต่อได้
    /// หลักฐาน: TestEvidence/lab-c-phase2-zone-transition/
    /// </summary>
    public class NpcZoneTransitionPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/lab-c-phase2-zone-transition";

        GameLifetimeScope _scope;
        NpcDirectorSystem _director;
        LubanDataService _data;
        UtilityContext _ctx;
        NpcState _npc;
        string _fromZone, _toZone;
        float _arrivalX = float.NaN, _arrivalY = float.NaN;
        NpcCharacterView _view;
        Vector2 _firstChibiPos;
        readonly StringBuilder _samples = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // UTF เข้า play ด้วย empty test scene — ต้องโหลด scene เกมเองก่อน
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
            _ctx = _scope.Container.Resolve<UtilityContext>();
            Assert.That(_data != null && _ctx != null, "resolve LubanDataService/UtilityContext ไม่ได้");
        }

        [UnityTest]
        public IEnumerator VisualTestAB_WalkExitCrossEnter_WithScreenshots()
        {
            try
            {
                // ---- เลือก innocent ที่ว่าง และโซนปัจจุบันมีคู่เชื่อมใน CSV ----
                foreach (var n in _director.Npcs.Values)
                {
                    if (!n.IsAlive || n.Role == NpcRole.Killer || Wander.IsTransitioning(n)) continue;
                    var conn = _data.LocationDefs[n.CurrentLocationId].ConnectedLocationIds?
                        .FirstOrDefault(z => !string.IsNullOrEmpty(z) && z != n.CurrentLocationId &&
                                             _data.GetTransition(n.CurrentLocationId, z) != null);
                    if (conn != null) { _npc = n; _toZone = conn; break; }
                }
                Assert.That(_npc != null, "ไม่มี innocent NPC ที่โซนมีจุดเชื่อมใน CSV");
                _fromZone = _npc.CurrentLocationId;

                var reverse = _data.GetTransition(_toZone, _fromZone);
                Assert.That(reverse != null, "ไม่มี reverse row (arrival point)");
                _arrivalX = reverse.TransitionX;
                _arrivalY = reverse.TransitionY;

                var startX = _npc.PositionX;
                var startY = _npc.PositionY;
                _samples.AppendLine($"START npc={_npc.Id} {_fromZone} -> {_toZone} " +
                                    $"start=({startX:F2},{startY:F2}) arrival=({_arrivalX:F2},{_arrivalY:F2})");

                // ---- เริ่ม transition ผ่าน path เดียวกับ AI (single source of truth) ----
                Assert.That(Wander.MoveToZone(_npc, _ctx, _toZone, new System.Random()),
                            "MoveToZone ไม่เริ่ม transition");
                Assert.That(_npc.TransitionPhase == NpcTransitionPhase.WalkingToPoint,
                            $"phase หลัง MoveToZone = {_npc.TransitionPhase}");

                bool walked = false, chibiFound = false, chibiMoved = false;
                bool exitSeen = false, crossSeen = false, enterSeen = false, backToNone = false;
                bool shotWalking = false, shotExiting = false, shotEntering = false;
                float exitAt = -1f, crossAt = -1f, doneAt = -1f;
                Vector2 arrivalPos = Vector2.positiveInfinity;
                var timeout = Time.time + 40f;

                while (!backToNone && Time.time < timeout)
                {
                    yield return null;
                    var pos = new Vector2(_npc.PositionX, _npc.PositionY);

                    // Test A (state): เดินจริงระหว่าง WalkingToPoint
                    if (_npc.TransitionPhase == NpcTransitionPhase.WalkingToPoint)
                    {
                        var dx = pos.x - startX;
                        var dy = pos.y - startY;
                        if (dx * dx + dy * dy > 1e-6f) walked = true;

                        // Test A (visual): chibi บนจอขยับจริง
                        if (!chibiFound)
                        {
                            _view = UnityEngine.Object.FindObjectsByType<NpcCharacterView>(FindObjectsSortMode.None)
                                .FirstOrDefault(v => v.NpcId == _npc.Id);
                            if (_view != null) { chibiFound = true; _firstChibiPos = _view.transform.position; }
                        }
                        if (chibiFound && _view != null)
                        {
                            var d = ((Vector2)_view.transform.position - _firstChibiPos).sqrMagnitude;
                            if (d > 1e-6f) chibiMoved = true;
                        }
                        if (chibiMoved && !shotWalking)
                        { shotWalking = true; Shot("playmode-screenshot-1-walking.png"); }
                    }

                    // Test B milestones (เวลาจริง)
                    if (_npc.TransitionPhase == NpcTransitionPhase.Exiting && !exitSeen)
                    { exitSeen = true; exitAt = Time.time; shotExiting = true; Shot("playmode-screenshot-2-exiting.png"); }
                    if (!crossSeen && _npc.CurrentLocationId == _toZone)
                    { crossSeen = true; crossAt = Time.time; arrivalPos = pos; }
                    if (crossSeen && _npc.TransitionPhase == NpcTransitionPhase.Entering && !enterSeen)
                    { enterSeen = true; shotEntering = true; Shot("playmode-screenshot-3-entering.png"); }
                    if (crossSeen && _npc.TransitionPhase == NpcTransitionPhase.None)
                    { backToNone = true; doneAt = Time.time; }

                    var chibiTxt = (chibiFound && _view != null)
                        ? $"chibiPos=({_view.transform.position.x:F2},{_view.transform.position.y:F2})"
                        : "chibiPos=(despawned/not-found)";
                    _samples.AppendLine($"t={Time.time:F2} phase={_npc.TransitionPhase} " +
                                        $"statePos=({_npc.PositionX:F2},{_npc.PositionY:F2}) " +
                                        $"zone={_npc.CurrentLocationId} {chibiTxt}");
                }

                // chibi despawn หลังข้ามโซน (destroy จบสิ้นเฟรม — เช็คหลัง loop)
                yield return null;
                yield return null;
                var chibiStillThere = UnityEngine.Object.FindObjectsByType<NpcCharacterView>(FindObjectsSortMode.None)
                    .Any(v => v.NpcId == _npc.Id);

                // AI เดินต่อได้หลัง cycle จบ
                var canResume = Wander.SetRandomTargetInZone(_npc, _ctx, new System.Random());

                var arrivalDist = float.NaN;
                if (!float.IsNaN(_arrivalX) && arrivalPos.x < 1000f)
                    arrivalDist = Vector2.Distance(arrivalPos, new Vector2(_arrivalX, _arrivalY));

                // ---- เขียนหลักฐาน ----
                var lines = new StringBuilder();
                lines.AppendLine($"=== PlayMode Visual Test A+B — NPC {_npc.Id}: {_fromZone} -> {_toZone} " +
                                 $"(arrival ({_arrivalX:F1},{_arrivalY:F1})) ===");
                lines.AppendLine();
                lines.AppendLine("--- samples (per frame) ---");
                lines.Append(_samples);
                lines.AppendLine();
                lines.AppendLine($"[Test A] state walked (PositionX/Y changed): {walked}");
                lines.AppendLine($"[Test A] chibi found: {chibiFound}, moved on screen: {chibiMoved}");
                lines.AppendLine($"[Test B] Exiting t={exitAt:F2}s, crossed t={crossAt:F2}s, back to None t={doneAt:F2}s");
                lines.AppendLine($"[Test B] arrival point distance: {arrivalDist:F3} (<= 0.05 PASS)");
                lines.AppendLine($"[Test B] chibi despawned after cross: {!chibiStillThere}");
                lines.AppendLine($"[Test B] AI resumed after cycle: {canResume}");
                lines.AppendLine($"screenshots: walking={shotWalking} exiting={shotExiting} entering={shotEntering}");
                lines.AppendLine();

                var pass = walked && chibiFound && chibiMoved && exitSeen && crossSeen &&
                           enterSeen && backToNone && !chibiStillThere && canResume &&
                           arrivalDist <= 0.05f;
                lines.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");
                File.WriteAllText(EvidencePath("playmode-test-ab-visual.txt"), lines.ToString(), Encoding.UTF8);
                Debug.Log($"[PlayModeTestAB] {(pass ? "PASS" : "FAIL")} — evidence: {EvidenceDir}/playmode-test-ab-visual.txt (+3 PNG)");

                Assert.That(walked, "Test A: state ไม่เดินระหว่าง WalkingToPoint");
                Assert.That(chibiFound, "Test A: ไม่เจอ chibi view");
                Assert.That(chibiMoved, "Test A: chibi ไม่ขยับบนจอ");
                Assert.That(exitSeen && crossSeen && enterSeen && backToNone,
                            "Test B: FSM cycle ไม่ครบ (exit→cross→enter→none)");
                Assert.That(arrivalDist <= 0.05f, $"Test B: ไปไม่ถึง arrival point (d={arrivalDist:F3})");
                Assert.That(!chibiStillThere, "Test B: chibi ยังไม่ถูก despawn หลังข้ามโซน");
                Assert.That(canResume, "Test B: AI เดินต่อไม่ได้หลังจบ cycle");
            }
            finally
            {
            }
        }

        static void Shot(string file)
        {
            try { ScreenCapture.CaptureScreenshot(EvidencePath(file)); }
            catch (Exception ex) { Debug.LogWarning($"[PlayModeTestAB] screenshot ล้มเหลว: {ex.Message}"); }
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
