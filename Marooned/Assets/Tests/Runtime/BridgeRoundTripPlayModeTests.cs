using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Marooned.Core;
using Marooned.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using Debug = UnityEngine.Debug;

namespace Marooned.EditorTools
{
    /// <summary>
    /// End-to-end round trip ผ่าน McpBridge จริง (bridge process ↔ TCP 3216 ↔ Unity):
    /// spawn McpBridge เป็น MCP stdio client → initialize → tools/list → tools/call
    /// get_game_state → tools/call move_to_location(jungle_edge) → วัดเวลา response
    ///
    /// พิสูจน์:
    ///  1. MessagePack contract ฝั่ง bridge (Shared ใหม่: TargetX/TargetY/IsAutoMoving
    ///     Key 15-17) ตรงกับฝั่ง Unity — call ได้โดยไม่ error
    ///  2. move_to_location ผ่าน bridge ใช้เวลา ≈ ระยะทาง/Speed (เดินจริง — ไม่ใช่
    ///     teleport ตอบทันที) และ response กลับ "หลัง" เดินถึงเท่านั้น (handler await)
    ///  3. state จริงหลัง call: CurrentLocationId = jungle_edge, PositionX/Y = WorldX/Y
    ///
    /// ⚠️ บทสนทนา stdio ทั้งหมดรันบน threadpool (UniTask.Run) — ห้าม block main
    /// thread ไม่งั้น GameTickDriver หยุดเดิน → handler รอเดินไม่จบ (เจอตอนเทสจริง)
    ///
    /// หลักฐาน: TestEvidence/movetolocation-fix/BridgeRoundTrip.txt
    /// </summary>
    public class BridgeRoundTripPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/movetolocation-fix";

        static readonly string RepoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static readonly string BridgeDll = Path.Combine(RepoRoot, "McpBridge", "bin", "Debug", "net8.0", "McpBridge.dll");

        GameLifetimeScope _scope;
        GameStateProvider _stateProvider;

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
                    if (_stateProvider != null) break;
                }
                await UniTask.Yield();
            }
            Assert.That(_scope != null, "GameLifetimeScope ไม่เจอใน play mode");
            Assert.That(File.Exists(BridgeDll), $"bridge ยังไม่ build: {BridgeDll}");
        });

        [UnityTest]
        [Timeout(360000)] // default 180s ไม่พอสำหรับบทสนทนา bridge — เคยโดน abort กลางทาง
        public IEnumerator Bridge_MoveToLocation_WalkDuration_RoundTrip() => UniTask.ToCoroutine(async () =>
        {
            var player = _stateProvider.GetPlayer();

            // seed ให้ผู้เล่นอยู่ beach (0,0) — ไกลจาก jungle_edge (10,5) พอ
            player.CurrentLocationId = "beach";
            player.PositionX = 0f;
            player.PositionY = 0f;
            player.IsAutoMoving = false;
            await UniTask.Yield();

            // ---- บทสนทนา MCP ทั้งหมดบน threadpool — main thread ปล่อยให้เกมเดิน ----
            StringBuilder log = null;
            double moveSeconds = 0;
            Exception convError = null;
            await UniTask.Run(() =>
            {
                try { (log, moveSeconds) = RunBridgeConversation(); }
                catch (Exception ex) { convError = ex; }
            });
            if (convError != null) throw convError;

            // ---- assertions (main thread — เกมหยุดเดินแล้ว อ่าน state ปลอดภัย) ----
            log.AppendLine($"[move_to_location jungle_edge] responded in {moveSeconds:F1}s " +
                           $"(walk dist≈11.09 @ speed 3.5 → expect ~3.2s)");

            Assert.GreaterOrEqual(moveSeconds, 1.5,
                $"response กลับเร็วเกิน ({moveSeconds:F2}s) — นี่คือ teleport ไม่ใช่เดิน");
            Assert.AreEqual("jungle_edge", player.CurrentLocationId, "CurrentLocationId ต้องเปลี่ยนหลัง bridge call");
            Assert.That(player.PositionX, Is.EqualTo(10f).Within(0.11f), "PositionX ต้องถึง WorldX ของ jungle_edge");
            Assert.That(player.PositionY, Is.EqualTo(5f).Within(0.11f), "PositionY ต้องถึง WorldY ของ jungle_edge");
            Assert.IsFalse(player.IsAutoMoving, "flag ต้องปิดหลังถึง");
            log.AppendLine($"[state] location={player.CurrentLocationId}, " +
                           $"pos=({player.PositionX:F2},{player.PositionY:F2}), activity={player.Activity} → PASS");

            var evidencePath = Path.Combine(RepoRoot, EvidenceDir, "BridgeRoundTrip.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));
            File.WriteAllText(evidencePath, $"=== BridgeRoundTrip — {DateTime.Now:HH:mm:ss} ===\n{log}\nRESULT: PASS\n");
            Debug.Log($"[BridgeRoundTrip] PASS — evidence: {EvidenceDir}/BridgeRoundTrip.txt");
        });

        /// <summary>บทสนทนา MCP ทั้งหมด (รันบน threadpool — block ได้) คืน (log, เวลา move)</summary>
        static (StringBuilder log, double moveSeconds) RunBridgeConversation()
        {
            var log = new StringBuilder();
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{BridgeDll}\"",
                WorkingDirectory = Path.Combine(RepoRoot, "McpBridge"),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            var bridge = new Process { StartInfo = psi };
            Assert.IsTrue(bridge.Start(), "spawn McpBridge ไม่สำเร็จ");

            var stderr = new StringBuilder();
            bridge.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            bridge.BeginErrorReadLine();

            try
            {
                using var writer = bridge.StandardInput;
                using var reader = bridge.StandardOutput;
                int nextId = 0;

                // ⚠️ Reader pump: มี thread เดียวอ่าน stdout แล้วกระจาย response
                // ไปตาม id (TaskCompletionSource) — กัน race ตอนยิง RPC คู่ขนาน
                // (move ค้างระหว่างรอ + cancel แทรกกลาง) ที่เคยแย่ง ReadLine
                // กันเองจนบทสนทนาค้าง (เจอตอนเทสจริง)
                var pending = new System.Collections.Concurrent.ConcurrentDictionary<
                    int, System.Threading.Tasks.TaskCompletionSource<string>>();
                var pump = System.Threading.Tasks.Task.Run(() =>
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var idIdx = line.IndexOf("\"id\":", StringComparison.Ordinal);
                        if (idIdx < 0) continue;
                        var start = idIdx + 5;
                        var end = start;
                        while (end < line.Length && line[end] >= '0' && line[end] <= '9') end++;
                        if (!int.TryParse(line.Substring(start, end - start), out var id)) continue;
                        if (pending.TryGetValue(id, out var tcs)) tcs.TrySetResult(line);
                    }
                });

                string Rpc(string method, object payload)
                {
                    var id = ++nextId;
                    var sw = Stopwatch.StartNew();
                    UnityEngine.Debug.Log($"[BridgeRoundTrip] → {method} (id={id})");
                    var tcs = pending.GetOrAdd(id, _ => new System.Threading.Tasks.TaskCompletionSource<string>());
                    writer.WriteLine($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{MiniSerialize(payload)}}}");
                    writer.Flush();
                    if (!tcs.Task.Wait(40000))
                        throw new TimeoutException($"{method} ไม่ตอบภายใน 40 วิ\nstderr: {stderr}");
                    var line = tcs.Task.Result;
                    UnityEngine.Debug.Log($"[BridgeRoundTrip] ← {method} ใน {sw.ElapsedMilliseconds}ms");
                    if (line.Contains("\"error\""))
                        throw new Exception($"{method} คืน error: {Truncate(line, 400)}\nstderr: {stderr}");
                    return line;
                }

                // ---- initialize ----
                var init = Rpc("initialize", new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "unity-playmode-test", version = "0.1" },
                });
                Assert.IsNotNull(init, "initialize ไม่ตอบ");
                writer.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                writer.Flush();
                log.AppendLine($"[init] ok: {Truncate(init, 120)}");

                // ---- tools/list — ชื่อ tool เป็น snake_case จาก SDK ----
                var listRaw = Rpc("tools/list", new { });
                var gameStateTool = FindToolName(listRaw, "gamestate");
                var moveTool = FindToolName(listRaw, "movetolocation");
                Assert.IsNotNull(gameStateTool, $"ไม่เจอ game state tool ใน: {Truncate(listRaw, 400)}");
                Assert.IsNotNull(moveTool, $"ไม่เจอ move tool ใน: {Truncate(listRaw, 400)}");
                log.AppendLine($"[tools] state='{gameStateTool}', move='{moveTool}'");

                // ---- get_game_state (sanity: bridge ↔ Unity TCP จริง) ----
                var gs = Rpc("tools/call", new { name = gameStateTool, arguments = new { } });
                Assert.IsFalse(gs.Contains("\"isError\":true"), $"get_game_state ล้มเหลว: {Truncate(gs, 300)}");
                log.AppendLine("[get_game_state] ok (bridge ↔ Unity TCP round trip)");

                var cancelTool = FindToolName(listRaw, "cancelmove");

                // ---- สถานการณ์ 1: cancel_move ตอนไม่เดิน → not_moving ----
                if (cancelTool != null)
                {
                    var idle = Rpc("tools/call", new { name = cancelTool, arguments = new { } });
                    Assert.IsFalse(idle.Contains("\"isError\":true"), $"cancel_move (idle) ล้มเหลว: {Truncate(idle, 300)}");
                    Assert.IsTrue(idle.Contains("not_moving"), $"cancel ตอน idle ต้อง not_moving: {Truncate(idle, 300)}");
                    log.AppendLine("[cancel_move idle] not_moving ✓");
                }

                // ---- สถานการณ์ 2: move + cancel กลางทาง (ผ่าน bridge จริงทั้งคู่) ----
                if (cancelTool != null)
                {
                    string moveResult = null;
                    var moveThread = Task.Run(() =>
                        moveResult = Rpc("tools/call", new { name = moveTool, arguments = new { locationId = "jungle_edge" } }));
                    Thread.Sleep(1000); // ให้เดินไปกลางทางก่อน (~3.5 unit)

                    var cancel = Rpc("tools/call", new { name = cancelTool, arguments = new { } });
                    Assert.IsFalse(cancel.Contains("\"isError\":true"), $"cancel_move ล้มเหลว: {Truncate(cancel, 300)}");
                    Assert.IsTrue(cancel.Contains("Move cancelled"), $"cancel ต้องสำเร็จ: {Truncate(cancel, 300)}");

                    moveThread.Wait(20000);
                    Assert.IsNotNull(moveResult, "move ที่ถูก cancel ต้องตอบกลับ");
                    Assert.IsTrue(moveResult.Contains("Move failed: superseded"),
                        $"move ที่ถูก cancel ต้อง superseded: {Truncate(moveResult, 300)}");
                    log.AppendLine("[cancel_move mid-walk] cancel ✓ + move → superseded ✓ (ผ่าน bridge ↔ TCP จริง)");

                    // ---- สถานการณ์ 3: move ปกติหลัง cancel — วัดเวลาเดินเต็ม ----
                }

                // ---- move_to_location(jungle_edge) — วัดเวลา ----
                var sw = Stopwatch.StartNew();
                var move = Rpc("tools/call", new { name = moveTool, arguments = new { locationId = "jungle_edge" } });
                var elapsed = sw.Elapsed.TotalSeconds;
                Assert.IsFalse(move.Contains("\"isError\":true"), $"move_to_location ล้มเหลว: {Truncate(move, 300)}");
                Assert.IsTrue(move.Contains("Moved to jungle_edge"), $"move ต้องสำเร็จ: {Truncate(move, 300)}");

                return (log, elapsed);
            }
            finally
            {
                try { bridge.Kill(); } catch { }
                bridge.Dispose();
            }
        }

        // ---- MCP stdio helpers (newline-delimited JSON-RPC) ----

        static string MiniSerialize(object obj)
        {
            switch (obj)
            {
                case null: return "null";
                case string s: return $"\"{s}\"";
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString();
                default:
                    var sb = new StringBuilder("{");
                    var first = true;
                    foreach (var p in obj.GetType().GetProperties())
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append($"\"{p.Name}\":{MiniSerialize(p.GetValue(obj))}");
                    }
                    sb.Append('}');
                    return sb.ToString();
            }
        }

        /// <summary>หาชื่อ tool จาก tools/list response ที่ name มี keyword (case-insensitive, ตัด _ ออก)</summary>
        static string FindToolName(string toolsListResponse, string keyword)
        {
            if (toolsListResponse == null) return null;
            var needle = keyword.Replace("_", "");
            var idx = 0;
            while (true)
            {
                idx = toolsListResponse.IndexOf("\"name\":\"", idx, StringComparison.Ordinal);
                if (idx < 0) return null;
                idx += 8; // ความยาวของ "name":" = 8 ตัวอักษร
                var end = toolsListResponse.IndexOf('"', idx);
                if (end < 0) return null;
                var name = toolsListResponse.Substring(idx, end - idx);
                if (name.ToLowerInvariant().Replace("_", "").Contains(needle)) return name;
            }
        }

        static string Truncate(string s, int max) => s != null && s.Length > max ? s.Substring(0, max) + "…" : s;
    }
}
