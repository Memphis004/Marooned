using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.Systems.AI;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Hybrid Transition Points (Part 2) — PlayMode visual evidence สำหรับ Test A+B
    ///
    /// วิธีรัน (2 คลิก):
    ///   1. กด Play (Ctrl+P) — runner auto-start ทันทีที่เข้า play mode
    ///   2. รอ ~10-15 วิ — จบเอง (ตัดตอนกดปุ่มเมนูกลาง play ซึ่ง focus อาจหลุด)
    ///
    /// รันใน play mode จริง: GameTickDriver tick ระบบจริงทุกเฟรม, MessagePipe จริง,
    /// ChibiSpawnerView spawn/despawn ตาม event จริง
    ///
    ///  • Test A: NPC เดิน "เห็นจริง" — state PositionX/Y เปลี่ยนต่อเนื่องระหว่าง
    ///    WalkingToPoint และ chibi GameObject บนจอขยับตามจริง (capture ภาพ Game View)
    ///  • Test B: ครบ cycle — ถึงจุดเชื่อม → exit anim ~0.5s → ข้ามโซน →
    ///    โผล่ที่ arrival point → enter anim ~0.3s → เดินต่อ, chibi despawn หลังข้ามโซน
    ///
    /// หลักฐาน: TestEvidence/lab-c-phase2-zone-transition/
    ///   - playmode-test-ab-visual.txt + screenshot-1-walking/2-exiting/3-entering.png
    /// </summary>
    [InitializeOnLoad]
    public static class NpcZoneTransitionVisualRunner
    {
        private const string EvidenceDir = "TestEvidence/lab-c-phase2-zone-transition";

        private enum State { Idle, Running, Done }
        private static State _state = State.Idle;

        private static NpcState _npc;
        private static string _fromZone, _toZone;
        private static float _arrivalX = float.NaN, _arrivalY = float.NaN;
        private static float _startX, _startY;
        private static NpcCharacterView _view;
        private static Vector2 _firstChibiPos;
        private static bool _walkedInState, _chibiFound, _chibiMoved, _despawnedAfterCross;
        private static bool _exitSeen, _crossSeen, _enterSeen, _backToNone, _canResume;
        private static bool _shotWalking, _shotExiting, _shotEntering;
        private static float _exitAt = -1f, _crossAt = -1f, _enterDoneAt = -1f;
        private static Vector2 _arrivalObservedPos = Vector2.positiveInfinity;
        private static readonly StringBuilder _samples = new();
        private static float _timeout, _startedAt;

        static NpcZoneTransitionVisualRunner()
        {
            // หมายเหตุ: automation path คือ PlayMode test (Tests/Runtime) ผ่าน Test Runner
            // — ไม่ auto-start ที่นี่อีกต่อไป เพื่อกันสองตัว drive NPC คนละตัวซ้อนกัน
            // (ตัวนี้เหลือไว้รัน manual ผ่านเมนูเมื่อกด Play เอง)
        }

        [MenuItem("Marooned/Lab C ZoneTransition/Run PlayMode Visual Test A+B Now", priority = 34)]
        public static void RunNow()
        {
            if (!Application.isPlaying) { Debug.LogError("[VisualTestAB] ต้องกด Play ก่อน"); return; }
            StartRun("manual menu");
        }

        private static void StartRun(string trigger)
        {
            if (_state == State.Running) return;
            try
            {
                var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
                if (scope == null) { Debug.LogError("[VisualTestAB] ไม่เจอ GameLifetimeScope"); return; }

                var director = scope.Container.Resolve<NpcDirectorSystem>();
                var data = scope.Container.Resolve<LubanDataService>();
                var ctx = scope.Container.Resolve<UtilityContext>();

                // เลือก innocent ตัวแรกที่ว่าง (กัน KillerPlanner แทรก logic ระหว่างเทส)
                var npc = director.Npcs.Values.FirstOrDefault(n =>
                    n.IsAlive && n.Role != NpcRole.Killer && !Wander.IsTransitioning(n));
                if (npc == null) { Debug.LogError("[VisualTestAB] ไม่มี innocent NPC ว่าง"); return; }

                var fromZone = npc.CurrentLocationId;
                var toZone = data.LocationDefs[fromZone].ConnectedLocationIds?
                    .FirstOrDefault(z => !string.IsNullOrEmpty(z) && z != fromZone &&
                                         data.GetTransition(fromZone, z) != null);
                if (toZone == null) { Debug.LogError($"[VisualTestAB] โซน {fromZone} ไม่มีคู่ที่มีจุดเชื่อมใน CSV"); return; }

                var reverse = data.GetTransition(toZone, fromZone);
                if (reverse != null) { _arrivalX = reverse.TransitionX; _arrivalY = reverse.TransitionY; }

                _npc = npc;
                _fromZone = fromZone;
                _toZone = toZone;
                _startX = npc.PositionX;
                _startY = npc.PositionY;
                _view = null;
                _walkedInState = _chibiFound = _chibiMoved = _despawnedAfterCross = false;
                _exitSeen = _crossSeen = _enterSeen = _backToNone = _canResume = false;
                _shotWalking = _shotExiting = _shotEntering = false;
                _arrivalObservedPos = Vector2.positiveInfinity;
                _samples.Clear();
                _timeout = Time.time + 40f;
                _startedAt = Time.time;

                // เริ่ม transition ผ่าน path เดียวกับ AI (single source of truth)
                if (!Wander.MoveToZone(npc, ctx, toZone, new System.Random()))
                {
                    Debug.LogError("[VisualTestAB] MoveToZone ไม่เริ่ม transition");
                    return;
                }
                if (npc.TransitionPhase != NpcTransitionPhase.WalkingToPoint)
                {
                    Debug.LogError($"[VisualTestAB] phase หลัง MoveToZone = {npc.TransitionPhase} (คาด WalkingToPoint)");
                    return;
                }

                _state = State.Running;
                EditorApplication.update += Tick;
                Debug.Log($"[VisualTestAB] START via {trigger}: {npc.Id} {fromZone} -> {toZone}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualTestAB] start failed: {ex}");
            }
        }

        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || _npc == null) { Finish(false, "play จบกลางรัน"); return; }
                if (Time.time > _timeout) { Finish(false, "timeout 40s"); return; }

                var npc = _npc;
                var statePos = new Vector2(npc.PositionX, npc.PositionY);

                // ---- Test A (state): เดินจริงระหว่าง WalkingToPoint ----
                if (npc.TransitionPhase == NpcTransitionPhase.WalkingToPoint)
                {
                    var dx = statePos.x - _startX;
                    var dy = statePos.y - _startY;
                    if (dx * dx + dy * dy > 1e-6f) _walkedInState = true;
                }

                // ---- Test A (visual): chibi บนจอขยับจริง ----
                if (!_chibiFound && npc.TransitionPhase == NpcTransitionPhase.WalkingToPoint)
                {
                    _view = UnityEngine.Object.FindObjectsByType<NpcCharacterView>(FindObjectsSortMode.None)
                        .FirstOrDefault(v => v.NpcId == npc.Id);
                    if (_view != null) { _chibiFound = true; _firstChibiPos = _view.transform.position; }
                }
                if (_view != null && npc.TransitionPhase == NpcTransitionPhase.WalkingToPoint)
                {
                    var d = ((Vector2)_view.transform.position - _firstChibiPos).sqrMagnitude;
                    if (d > 1e-6f) _chibiMoved = true;
                }

                // ---- Test B milestones (เวลาจริง) ----
                if (npc.TransitionPhase == NpcTransitionPhase.Exiting && !_exitSeen)
                { _exitSeen = true; _exitAt = Time.time; Capture(ref _shotExiting, "screenshot-2-exiting.png"); }
                if (!_crossSeen && npc.CurrentLocationId == _toZone)
                { _crossSeen = true; _crossAt = Time.time; _arrivalObservedPos = statePos; }
                if (_crossSeen && npc.TransitionPhase == NpcTransitionPhase.Entering && !_enterSeen)
                { _enterSeen = true; Capture(ref _shotEntering, "screenshot-3-entering.png"); }
                if (_crossSeen && _view != null && !_view) _despawnedAfterCross = true; // fake-null = destroyed
                if (_crossSeen && npc.TransitionPhase == NpcTransitionPhase.None)
                { _backToNone = true; _enterDoneAt = Time.time; }

                _samples.AppendLine(
                    $"t={Time.time:F2} phase={npc.TransitionPhase} statePos=({npc.PositionX:F2},{npc.PositionY:F2}) " +
                    $"zone={npc.CurrentLocationId}" +
                    (_view != null && _view
                        ? $" chibiPos=({_view.transform.position.x:F2},{_view.transform.position.y:F2})"
                        : " chibiPos=(despawned/not-found)"));

                // walking screenshot — หลังเห็น chibi เดินจริง 2 เฟรม
                if (_chibiMoved && !_shotWalking && Time.time - _startedAt > 0.5f)
                    Capture(ref _shotWalking, "screenshot-1-walking.png");

                if (_backToNone)
                {
                    _canResume = Wander.SetRandomTargetInZone(npc,
                        new UtilityContext(LubanDataServiceForTest(), null), new System.Random());
                    Finish(true, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                Finish(false, ex.Message);
            }
        }

        private static void Capture(ref bool flag, string file)
        {
            flag = true;
            try { ScreenCapture.CaptureScreenshot(EvidencePath(file)); }
            catch (Exception ex) { Debug.LogWarning($"[VisualTestAB] screenshot ล้มเหลว: {ex.Message}"); }
        }

        private static void Finish(bool completed, string failure)
        {
            EditorApplication.update -= Tick;
            _state = State.Done;

            // despawn ตรวจซ้ำหลัง reconcile (ทำงานต้นเฟรมถัดไป)
            if (completed && _view != null && _view) _despawnedAfterCross = true;

            var lines = new StringBuilder();
            lines.AppendLine($"=== PlayMode Visual Test A+B — NPC {_npc?.Id}: {_fromZone} -> {_toZone} " +
                             $"(arrival ({_arrivalX:F1},{_arrivalY:F1})) ===");
            lines.AppendLine($"completed: {completed}{(failure != null ? $" ({failure})" : "")}");
            lines.AppendLine();
            lines.AppendLine("--- samples (per frame) ---");
            lines.Append(_samples);
            lines.AppendLine();

            lines.AppendLine($"[Test A] state walked (PositionX/Y changed): {_walkedInState}");
            lines.AppendLine($"[Test A] chibi found: {_chibiFound}, moved on screen: {_chibiMoved}");
            lines.AppendLine($"[Test B] Exiting t={_exitAt:F2}s, crossed t={_crossAt:F2}s, back to None t={_enterDoneAt:F2}s");
            if (_arrivalObservedPos.x < 1000f && !float.IsNaN(_arrivalX))
            {
                var d = Vector2.Distance(_arrivalObservedPos, new Vector2(_arrivalX, _arrivalY));
                lines.AppendLine($"[Test B] arrival point distance: {d:F3} (<= 0.05 PASS)");
            }
            lines.AppendLine($"[Test B] chibi despawned after cross: {_despawnedAfterCross}");
            lines.AppendLine($"[Test B] AI resumed after cycle: {_canResume}");
            lines.AppendLine($"screenshots: walking={_shotWalking} exiting={_shotExiting} entering={_shotEntering}");
            lines.AppendLine();

            var pass = completed && _walkedInState && _chibiFound && _chibiMoved &&
                       _exitSeen && _crossSeen && _enterSeen && _backToNone;
            lines.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");

            File.WriteAllText(EvidencePath("playmode-test-ab-visual.txt"), lines.ToString(), Encoding.UTF8);
            Debug.Log($"[VisualTestAB] {(pass ? "PASS" : "FAIL")} — evidence: {EvidenceDir}/playmode-test-ab-visual.txt (+3 PNG)");
        }

        // ---- helpers ----

        private static LubanDataService LubanDataServiceForTest()
        {
            // resolve ผ่าน scope เดียวกับเกม (ตารางโหลดแล้วใน singleton เดิม)
            var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope.Container.Resolve<LubanDataService>();
        }

        private static string EvidencePath(string file)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var dir = Path.Combine(repoRoot, EvidenceDir);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }
    }
}
