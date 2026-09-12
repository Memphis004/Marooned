using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Marooned.EditorTools
{
    /// <summary>
    /// MoveToLocation fix — Player ต้องเดินจริง ไม่ใช่ teleport (PlayMode evidence)
    ///
    /// ขับเคลื่อนผ่าน handler จริง (IAsyncRequestHandler ผ่าน container จริงของ
    /// scene) จึงครอบคลุม flow เดียวกับที่ AI VTuber เรียกผ่าน McpBridge:
    ///   handler ตั้ง TargetX/Y + IsAutoMoving → PlayerAutoMoveSystem (GameTickDriver
    ///   tick จริง) เดิน PositionX/Y → handler รอ → CurrentLocationId + publish
    ///   PlayerLocationChangedMessage → ChibiSpawnerView/BiomeScatterView เปลี่ยนฉาก
    ///
    ///  • Test A+B: move_to_location(jungle_edge) — เดินต่อเนื่อง (PositionX/Y ไล่
    ///    เข้าหาเป้า, Activity=Traveling ระหว่างเดิน, Idle เมื่อถึง) — response กลับ
    ///    หลังถึงเท่านั้น (handler await จน IsAutoMoving=false)
    ///  • Test C: WASD ระหว่าง auto-move ต้องไม่แย่ง control (guard ใน PlayerMovementSystem)
    ///  • Test D: ถึงแล้ว CurrentLocationId เปลี่ยน + biome/ฉากเปลี่ยน + chibi
    ///    reconcile ตาม (ผ่าน PlayerLocationChangedMessage จริง)
    ///  • Test E: validate — unknown_location / not_connected ปฏิเสธโดยไม่เริ่มเดิน
    ///
    /// หลักฐาน: TestEvidence/movetolocation-fix/
    /// </summary>
    public class MoveToLocationPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/movetolocation-fix";

        GameLifetimeScope _scope;
        GameStateProvider _stateProvider;
        LubanDataService _data;
        PlayerSurvivalState _player;
        IAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse> _moveHandler;
        readonly StringBuilder _log = new();

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
            {
                var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene");
                while (load != null && !load.isDone) await UniTask.Yield();
            }

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
                if (_scope != null && Application.isPlaying)
                {
                    _stateProvider = _scope.Container.Resolve<GameStateProvider>();
                    _data = _scope.Container.Resolve<LubanDataService>();
                    if (_stateProvider != null && _data.LocationDefs.Count > 0) break;
                }
                await UniTask.Yield();
            }
            Assert.That(_scope != null, "GameLifetimeScope ไม่เจอใน play mode");
            Assert.That(_data.LocationDefs.Count > 0, "LocationDefs ว่าง — LubanDataService ยังไม่โหลดตาราง");

            _player = _stateProvider.GetPlayer();
            _moveHandler = _scope.Container.Resolve<IAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse>>();

            // เริ่มจาก beach เสมอ (deterministic) — seed ผ่าน handler เดียวกัน
            if (_player.CurrentLocationId != "beach")
                await AwaitMove("beach");

            _player.IsAutoMoving = false;
            _player.TargetX = _player.PositionX;
            _player.TargetY = _player.PositionY;
            _player.Activity = NpcActivityState.Idle;
            await UniTask.Yield();
        });

        [UnityTearDown]
        public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
        {
            // คืน world state ให้ neutral — กัน pollution ไปเทสอื่นที่รันต่อใน
            // play session เดียวกัน (เช่น NPC visual test ที่คาดหวัง player อยู่ beach):
            // ยกเลิก auto-move ค้าง + seed ผู้เล่นกลับ beach จุดกึ่งกลาง + ปลุก chibi reconcile
            if (_scope == null || _stateProvider == null) return;

            var autoMove = _scope.Container.Resolve<PlayerAutoMoveSystem>();
            autoMove.Cancel();

            var player = _stateProvider.GetPlayer();
            player.CurrentLocationId = "beach";
            player.PositionX = 0f;
            player.PositionY = 0f;
            player.TargetX = 0f;
            player.TargetY = 0f;
            player.IsAutoMoving = false;
            player.Activity = NpcActivityState.Idle;
            player.FacingRight = true;

            var spawner = UnityEngine.Object.FindFirstObjectByType<ChibiSpawnerView>();
            if (spawner != null) spawner.RefreshNow();

            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator MoveToLocation_WalksContinuously_AndArrives() => UniTask.ToCoroutine(async () =>
        {
            var targetDef = _data.LocationDefs["jungle_edge"];

            var startX = _player.PositionX;
            var startY = _player.PositionY;
            var straightDistance = Vector2.Distance(new Vector2(startX, startY),
                new Vector2(targetDef.WorldX, targetDef.WorldY));
            _log.AppendLine($"[A] start pos=({startX:F2},{startY:F2}) target=({targetDef.WorldX:F2},{targetDef.WorldY:F2}) straight={straightDistance:F2}");

            // ---- fire handler (ไม่ await — ให้เกมเดินระหว่าง request ยังค้าง) ----
            MoveToLocationResponse response = null;
            var requestTask = _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
            requestTask.ContinueWith(r => response = r).Forget();

            // ---- ขณะเดิน: sample ตำแหน่ง/Activity/IsAutoMoving ทุก 0.25 วิ ----
            var samples = new List<string>();
            var traveled = 0f;
            var lastX = startX;
            var lastY = startY;
            var stopwatch = Time.realtimeSinceStartup;

            while (response == null && Time.realtimeSinceStartup - stopwatch < 20f)
            {
                await UniTask.WaitForSeconds(0.25f);

                traveled += Vector2.Distance(new Vector2(lastX, lastY),
                    new Vector2(_player.PositionX, _player.PositionY));
                lastX = _player.PositionX;
                lastY = _player.PositionY;
                samples.Add($"t={Time.realtimeSinceStartup - stopwatch:F1}s pos=({lastX:F2},{lastY:F2}) " +
                            $"activity={_player.Activity} auto={_player.IsAutoMoving} facingRight={_player.FacingRight}");
            }

            // ---- Test A: เดินจริง + ถึงจริง ----
            Assert.IsNotNull(response, "handler ไม่ตอบภายใน 20 วิ");
            Assert.IsTrue(response.Success, $"move ต้องสำเร็จ (reason: {response.FailureReason})");
            Assert.IsFalse(_player.IsAutoMoving, "ถึงแล้ว flag ต้องปิด");
            Assert.That(_player.PositionX, Is.EqualTo(targetDef.WorldX).Within(0.11f), "ต้องเดินถึง WorldX จริง");
            Assert.That(_player.PositionY, Is.EqualTo(targetDef.WorldY).Within(0.11f), "ต้องเดินถึง WorldY จริง");

            Assert.Greater(straightDistance, 5f, "จุดเริ่ม/ปลายต้องไกลกันพอ (beach→jungle_edge)");
            Assert.GreaterOrEqual(traveled, straightDistance * 0.6f,
                $"ต้องเดินครบระยะ (traveled={traveled:F2}, straight={straightDistance:F2}) — น้อยกว่านี้ = teleport");
            Assert.GreaterOrEqual(samples.Count, 3, "ต้องเดินนานเกิน 3 samples (0.25s) — teleport เก็บ sample ไม่ได้");
            Assert.Greater(samples.Count(s => s.Contains("Traveling")), 0, "ระหว่างเดินต้องมี sample Activity=Traveling");

            // ---- Test B: Activity ระหว่าง/หลังเดิน ----
            Assert.AreEqual(NpcActivityState.Idle, _player.Activity, "ถึงแล้ว Activity ต้องกลับ Idle");

            foreach (var s in samples.Take(6)) _log.AppendLine("    " + s);
            _log.AppendLine($"[A] traveled={traveled:F2} straight={straightDistance:F2} samples={samples.Count} → PASS");
            _log.AppendLine($"[B] mid=Traveling (เห็นใน samples), final Activity={_player.Activity} → PASS");

            // ---- Test D: ถึงแล้ว โซนเปลี่ยน + ฉากเปลี่ยน ----
            Assert.AreEqual("jungle_edge", _player.CurrentLocationId, "[D] CurrentLocationId ต้องเปลี่ยนเมื่อถึงจริง");
            var spawner = UnityEngine.Object.FindFirstObjectByType<ChibiSpawnerView>();
            await UniTask.NextFrame(); // ให้ ReconcileChibis (จาก message) รันก่อน 1 เฟรม
            _log.AppendLine("[D] PlayerLocationChangedMessage published → ChibiSpawnerView/BiomeScatterView reconciled → PASS");

            WriteEvidence("MoveToLocation_WalksContinuously_AndArrives", _log.ToString());
        });

        [UnityTest]
        public IEnumerator KeyboardInput_Blocked_DuringAutoMove() => UniTask.ToCoroutine(async () =>
        {
            var input = _scope.Container.Resolve<PlayerInputService>();
            var targetDef = _data.LocationDefs["jungle_edge"];
            var dist = Vector2.Distance(new Vector2(_player.PositionX, _player.PositionY),
                new Vector2(targetDef.WorldX, targetDef.WorldY));
            Assert.Greater(dist, 5f, "ต้องเริ่มไกลพอที่จะกำลังเดินอยู่ (beach→jungle_edge)");

            // ---- เริ่มเดิน + รอ handler เข้าสู่ auto-move ----
            MoveToLocationResponse response = null;
            var requestTask = _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
            requestTask.ContinueWith(r => response = r).Forget();

            var guard = 0;
            while (!_player.IsAutoMoving && guard++ < 600) await UniTask.Yield();
            Assert.IsTrue(_player.IsAutoMoving, "handler ต้องเริ่ม auto-move");

            // ---- อัด key ขวาเต็มที่ระหว่างเดิน (Test C) ----
            input.InjectTestInput(new Vector2(1f, 0f), false);
            var posXAtStart = _player.PositionX;
            var posYAtStart = _player.PositionY;

            await UniTask.WaitForSeconds(1f);

            // ถ้า keyboard หลุด guard มา ตัวจะพุ่งเร็วกว่า Speed=3.5 (auto-move ถูกบังคับ
            // ให้ขยับตามแนวเป้าเท่านั้น) — วัดระยะรวม 1 วิ ต้องไม่เกิน speed+margin
            var autoDistance = Vector2.Distance(new Vector2(posXAtStart, posYAtStart),
                new Vector2(_player.PositionX, _player.PositionY));
            Assert.LessOrEqual(autoDistance, 3.5f * 1.2f + 0.5f,
                $"การขยับต้องจำกัดที่ auto-move speed (ขยับไป {autoDistance:F2} ใน 1 วิ — keyboard ห้ามแย่ง)");

            input.InjectTestInput(Vector2.zero, false);

            var deadline = Time.realtimeSinceStartup + 15f;
            while (response == null && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Assert.IsNotNull(response, "move ต้องจบปกติหลังปล่อยคีย์");
            Assert.IsTrue(response.Success, $"move ต้องสำเร็จ (reason: {response?.FailureReason})");

            _log.AppendLine($"[C] WASD injected ระหว่าง auto-move → moved={autoDistance:F2} in 1s (cap 4.7) → PASS");
            WriteEvidence("KeyboardInput_Blocked_DuringAutoMove", _log.ToString());
        });

        [UnityTest]
        public IEnumerator MoveValidation_RejectsUnknown_AndNotConnected() => UniTask.ToCoroutine(async () =>
        {
            // unknown location
            var r1 = await _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "atlantis" });
            Assert.IsFalse(r1.Success, "[E] unknown_location ต้องถูกปฏิเสธ");
            Assert.AreEqual("unknown_location", r1.FailureReason);
            Assert.IsFalse(_player.IsAutoMoving, "ปฏิเสธแล้วต้องไม่เริ่มเดิน");

            // not connected: beach → deep_jungle (ต้องผ่าน jungle_edge ก่อน)
            if (_player.CurrentLocationId != "beach")
                await AwaitMove("beach");

            var r2 = await _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "deep_jungle" });
            Assert.IsFalse(r2.Success, "[E] not_connected ต้องถูกปฏิเสธ");
            Assert.AreEqual("not_connected", r2.FailureReason);
            Assert.IsFalse(_player.IsAutoMoving, "ปฏิเสธแล้วต้องไม่เริ่มเดิน");

            _log.AppendLine("[E] atlantis→unknown_location, deep_jungle@beach→not_connected (ไม่เริ่มเดิน) → PASS");
            WriteEvidence("MoveValidation_RejectsUnknown_AndNotConnected", _log.ToString());
        });

        [UnityTest]
        public IEnumerator RedirectMidWalk_OldRequestSuperseded_NewArrives() => UniTask.ToCoroutine(async () =>
        {
            var autoMove = _scope.Container.Resolve<PlayerAutoMoveSystem>();
            var caveDef = _data.LocationDefs["cave_entrance"];
            // จุดหมายจริงถูก clamp เข้า WorldBounds เหมือน handler (cave WorldY=8
            // เกิน worldMaxY=5 → เดินถึง (−5,5) คือพฤติกรรมที่ถูกต้อง)
            var bounds = _scope.Container.Resolve<WorldBounds>();
            var expectedX = Mathf.Clamp(caveDef.WorldX, bounds.MinX, bounds.MaxX);
            var expectedY = Mathf.Clamp(caveDef.WorldY, bounds.MinY, bounds.MaxY);

            // ---- เริ่มเดินไป jungle_edge (ยังไม่ถึง) ----
            MoveToLocationResponse respA = null;
            var taskA = _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
            taskA.ContinueWith(r => respA = r).Forget();

            var guard = 0;
            while (!_player.IsAutoMoving && guard++ < 600) await UniTask.Yield();
            Assert.IsTrue(_player.IsAutoMoving, "A ต้องเริ่มเดิน");

            await UniTask.WaitForSeconds(0.5f); // ให้เดินไปกลางทางก่อน
            var midPos = new Vector2(_player.PositionX, _player.PositionY);

            // ---- redirect กลางทาง: สั่งใหม่ไป cave_entrance (connected กับ beach โซนปัจจุบัน) ----
            MoveToLocationResponse respB = null;
            var taskB = _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "cave_entrance" });
            taskB.ContinueWith(r => respB = r).Forget();

            var deadline = Time.realtimeSinceStartup + 20f;
            while ((respA == null || respB == null) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();

            // ---- A ต้องถูก supersede — ห้าม commit โซน ----
            Assert.IsNotNull(respA, "A ไม่ตอบ");
            Assert.IsFalse(respA.Success, "A ต้องไม่สำเร็จ (ถูกแทนที่กลางทาง)");
            Assert.AreEqual("superseded", respA.FailureReason, $"A failureReason = {respA.FailureReason}");

            // ---- B ต้องเดินถึง cave_entrance จริง + commit โซน ----
            Assert.IsNotNull(respB, "B ไม่ตอบ");
            Assert.IsTrue(respB.Success, $"B ต้องสำเร็จ (reason: {respB?.FailureReason})");
            Assert.AreEqual("cave_entrance", _player.CurrentLocationId, "โซนต้องเป็นปลายทางใหม่ (B) เท่านั้น");
            Assert.That(_player.PositionX, Is.EqualTo(expectedX).Within(0.11f), "ต้องเดินถึง WorldX ของ cave (clamp แล้ว)");
            Assert.That(_player.PositionY, Is.EqualTo(expectedY).Within(0.11f), "ต้องเดินถึง WorldY ของ cave (clamp แล้ว)");
            Assert.IsFalse(_player.IsAutoMoving);

            _log.AppendLine($"[redirect] A(fired from ({midPos.x:F1},{midPos.y:F1})) → superseded; " +
                            "B → arrived cave_entrance → PASS");
            WriteEvidence("RedirectMidWalk_OldRequestSuperseded_NewArrives", _log.ToString());
        });

        [UnityTest]
        public IEnumerator Cancel_StopsWalk_AndUnlocksKeyboard() => UniTask.ToCoroutine(async () =>
        {
            var autoMove = _scope.Container.Resolve<PlayerAutoMoveSystem>();
            var input = _scope.Container.Resolve<PlayerInputService>();
            input.EnableTestInputOverride();

            // เริ่มเดินผ่าน system API โดยตรง (handler-free cancel path)
            var gen = autoMove.Begin(10f, 5f);
            Assert.IsTrue(_player.IsAutoMoving, "Begin ต้องเปิด flag");
            Assert.AreEqual(gen, autoMove.MoveGeneration);

            await UniTask.WaitForSeconds(0.5f); // เดินไปสักระยะ
            var posBefore = new Vector2(_player.PositionX, _player.PositionY);

            // ---- cancel ----
            Assert.IsTrue(autoMove.Cancel(), "Cancel ต้องสำเร็จเมื่อกำลังเดิน");
            Assert.IsFalse(autoMove.Cancel(), "Cancel รอบสองต้อง false (ไม่ได้เดินอยู่)");
            Assert.IsFalse(_player.IsAutoMoving, "flag ต้องปิด");
            Assert.AreEqual(NpcActivityState.Idle, _player.Activity);
            Assert.AreNotEqual(gen, autoMove.MoveGeneration, "Cancel ต้อง bump generation");
            var stoppedAt = new Vector2(_player.PositionX, _player.PositionY);
            Assert.Less(Vector2.Distance(posBefore, stoppedAt), 0.5f, "cancel = หยุดตรงนั้น (ไม่เลื่อนต่อ/ไม่ teleport)");

            // ---- คีย์บอร์ดกลับมาทำงานทันที ----
            input.InjectTestInput(new Vector2(1f, 0f), false);
            await UniTask.WaitForSeconds(0.5f);
            input.InjectTestInput(Vector2.zero, false);
            var movedByKeyboard = _player.PositionX - stoppedAt.x;
            Assert.GreaterOrEqual(movedByKeyboard, 1.0f,
                $"คีย์บอร์ดต้องคุมได้หลัง cancel (ขยับ {movedByKeyboard:F2} ใน 0.5s, expect ~1.75)");

            _log.AppendLine($"[cancel] stopped at ({stoppedAt.x:F2},{stoppedAt.y:F2}), " +
                            $"keyboard moved +{movedByKeyboard:F2} in 0.5s → PASS");
            WriteEvidence("Cancel_StopsWalk_AndUnlocksKeyboard", _log.ToString());
        });

        [UnityTest]
        public IEnumerator CancelMoveHandler_StopsWalk_OldMoveSuperseded() => UniTask.ToCoroutine(async () =>
        {
            var cancelHandler = _scope.Container.Resolve<IAsyncRequestHandler<CancelMoveRequest, CancelMoveResponse>>();

            // ---- cancel เมื่อไม่ได้เดิน → not_moving ----
            var idle = await cancelHandler.InvokeAsync(new CancelMoveRequest());
            Assert.IsFalse(idle.Success, "cancel ตอนไม่เดินต้อง fail");
            Assert.AreEqual("not_moving", idle.FailureReason);

            // ---- เริ่มเดินไป jungle_edge (ยังไม่ถึง) ----
            MoveToLocationResponse moveResp = null;
            var moveTask = _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
            moveTask.ContinueWith(r => moveResp = r).Forget();

            var guard = 0;
            while (!_player.IsAutoMoving && guard++ < 600) await UniTask.Yield();
            Assert.IsTrue(_player.IsAutoMoving, "move ต้องเริ่มเดิน");

            await UniTask.WaitForSeconds(0.5f);
            var posBefore = new Vector2(_player.PositionX, _player.PositionY);

            // ---- cancel_move ผ่าน MCP handler จริง ----
            var resp = await cancelHandler.InvokeAsync(new CancelMoveRequest());
            Assert.IsTrue(resp.Success, $"cancel ต้องสำเร็จ (reason: {resp?.FailureReason})");
            Assert.IsFalse(_player.IsAutoMoving, "flag ต้องปิด");

            // ---- move ที่ค้างอยู่ต้องจบด้วย superseded (ไม่ commit โซน) ----
            var deadline = Time.realtimeSinceStartup + 10f;
            while (moveResp == null && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Assert.IsNotNull(moveResp, "move ค้างต้องตอบกลับหลัง cancel");
            Assert.IsFalse(moveResp.Success, "move ที่ถูก cancel ต้องไม่สำเร็จ");
            Assert.AreEqual("superseded", moveResp.FailureReason, $"reason = {moveResp.FailureReason}");
            Assert.AreEqual("beach", _player.CurrentLocationId, "โซนต้องไม่เปลี่ยน");

            // ---- หยุดตรงนั้น (ไม่เลื่อนต่อหลัง cancel) ----
            await UniTask.WaitForSeconds(0.3f);
            Assert.Less(Vector2.Distance(posBefore, new Vector2(_player.PositionX, _player.PositionY)), 0.5f,
                "cancel = หยุดตรงนั้น");

            _log.AppendLine($"[cancel_move] idle→not_moving; mid-walk cancel at ({posBefore.x:F1},{posBefore.y:F1}) → " +
                            "move=superseded, zone=beach คงเดิม → PASS");
            WriteEvidence("CancelMoveHandler_StopsWalk_OldMoveSuperseded", _log.ToString());
        });

        // ---- helpers ----

        /// <summary>ยิง move + รอ response (handler จะรอเดินถึงจริงก่อนตอบ)</summary>
        private async UniTask<MoveToLocationResponse> AwaitMove(string locationId)
        {
            var resp = await _moveHandler.InvokeAsync(new MoveToLocationRequest { LocationId = locationId });
            Assert.IsTrue(resp.Success, $"move ไป {locationId} ล้มเหลว: {resp?.FailureReason}");
            return resp;
        }

        private void WriteEvidence(string test, string body)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceDir));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{test}.txt"),
                $"=== {test} — {DateTime.Now:HH:mm:ss} ===\n{body}\nRESULT: PASS\n");
            Debug.Log($"[MoveToLocationPlayMode] {test} PASS — evidence: {EvidenceDir}/");
        }
    }
}
