using System.IO;
using System.Linq;
using System.Text;
using Marooned.Core;
using Marooned.Systems;
using UnityEditor;
using VContainer;
using UnityEngine;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Lab C Phase 1 — runtime test runner (Editor menu) สำหรับ Test A/C/D:
    ///   - Run Test A: capture initial biome spawn (snapshot logs + scene objects)
    ///   - Run Test C: teleport player next to nearest node → harvest → เทียบ inventory
    ///   - Run Test D: เก็บ bush_berry (regrow 30s) ซ้ำจนหมด durability → ตรวจ regrow
    ///   - Dump Play Logs: เขียน log ทั้งหมดของ play session ลงไฟล์
    /// ไฟล์หลักฐานเขียนไปที่ TestEvidence/lab-c-phase1/ (repo root)
    /// ต้องกด Play ก่อน — Test C/D ต้องมี node spawn แล้ว (หลัง Test A)
    /// </summary>
    public static class BiomeScatterTestRunner
    {
        private const string EvidenceDir = "TestEvidence/lab-c-phase1";
        private const string RegrowTargetNodeId = "bush_berry"; // node เดียวใน CSV ที่ regrow > 0

        private static string EvidencePath(string file)
        {
            // Application.dataPath = <repo>/Marooned/Assets → repo root = ../../
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var dir = Path.Combine(repoRoot, EvidenceDir);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }

        [MenuItem("Marooned/Lab C/Run Test A — Initial Spawn", priority = 10)]
        public static void RunTestA()
        {
            if (!Application.isPlaying) { Debug.LogError("[TestA] ต้องกด Play ก่อน"); return; }

            var lines = new StringBuilder();
            lines.AppendLine($"=== Test A — Initial biome spawn ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            var layer = GameObject.Find("BiomeScatterLayer");
            var ground = GameObject.Find("BiomeGround_beach");
            var nodes = Object.FindObjectsByType<HarvestableNodeComponent>(FindObjectsSortMode.None);

            lines.AppendLine($"BiomeScatterLayer children (props+nodes): {(layer != null ? layer.transform.childCount : 0)}");
            lines.AppendLine($"Ground tint object 'BiomeGround_beach': {(ground != null ? "FOUND" : "MISSING")}");
            lines.AppendLine($"HarvestableNodeComponent count: {nodes.Length}");
            foreach (var n in nodes)
                lines.AppendLine($"  node: {n.gameObject.name} pos=({n.transform.position.x:F2},{n.transform.position.y:F2}) durability={n.RemainingDurability} regrowing={n.IsRegrowing}");

            var logs = DiagnosticLogRecorder.FilterByTags("[BiomeScatterSystem]", "[BiomeScatterView]", "[LubanDataService]");
            lines.AppendLine("--- related logs ---");
            foreach (var l in logs) lines.AppendLine(l);

            var pass = layer != null && layer.transform.childCount > 0 && ground != null && nodes.Length > 0;
            lines.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL (ดู MISSING ด้านบน)");
            File.WriteAllText(EvidencePath("test-a-initial-spawn.txt"), lines.ToString(), Encoding.UTF8);
            Debug.Log($"[TestA] {(pass ? "PASS" : "FAIL")} — evidence: {EvidenceDir}/test-a-initial-spawn.txt");
        }

        [MenuItem("Marooned/Lab C/Run Test C — Harvest With Auto Tool", priority = 12)]
        public static async void RunTestC()
        {
            if (!Application.isPlaying) { Debug.LogError("[TestC] ต้องกด Play ก่อน (และรัน Test A ก่อนให้ node spawn)"); return; }

            var lines = new StringBuilder();
            lines.AppendLine($"=== Test C — Harvest with auto tool ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            var harvest = scope.Container.Resolve<NodeHarvestSystem>();
            var state = scope.Container.Resolve<GameStateProvider>().GetPlayer();

            var node = Object.FindObjectsByType<HarvestableNodeComponent>(FindObjectsSortMode.None)
                .Where(n => !n.IsRegrowing)
                .OrderBy(n => (new Vector2(n.transform.position.x, n.transform.position.y) - new Vector2(state.PositionX, state.PositionY)).sqrMagnitude)
                .FirstOrDefault();
            if (node == null) { Debug.LogError("[TestC] ไม่มี node ที่เก็บได้ใน scene — รัน Test A ก่อน"); return; }

            // เทเลพอตผู้เล่นไปข้าง ๆ node (จำลอง "เดินเข้าใกล้") แล้วเรียก path เดียวกับกด E
            var before = string.Join(";", state.Inventory.Select(kv => $"{kv.Key}x{kv.Value}").OrderBy(kv => kv));
            state.PositionX = node.transform.position.x + 0.5f;
            state.PositionY = node.transform.position.y;

            lines.AppendLine($"player teleported next to '{node.NodeId}' ({node.gameObject.name})");
            lines.AppendLine($"inventory before: {before}");

            var result = harvest.TryHarvestNearest(); // path เดียวกับ InteractKey ใน Tick()
            await System.Threading.Tasks.Task.Delay(100); // ให้ publish chain (UI re-render) วิ่งให้จบ

            var after = string.Join(";", state.Inventory.Select(kv => $"{kv.Key}x{kv.Value}").OrderBy(kv => kv));
            lines.AppendLine($"harvest call: success={result.Success} reason={result.FailureReason ?? "-"} node={result.NodeId} tool={result.UsedToolId ?? "(bare hands)"}");
            lines.AppendLine($"inventory after:  {after}");
            lines.AppendLine(result.Success ? $"yield visible in inventory: {after.Contains(result.ItemId)}" : "no yield expected");
            lines.AppendLine($"node durability now: {node.RemainingDurability}");

            var logs = DiagnosticLogRecorder.FilterByTags("[NodeHarvestSystem]", "[CardInventorySystem]");
            lines.AppendLine("--- related logs ---");
            foreach (var l in logs) lines.AppendLine(l);

            var pass = result.Success && after != before;
            lines.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(EvidencePath("test-c-harvest.txt"), lines.ToString(), Encoding.UTF8);
            Debug.Log($"[TestC] {(pass ? "PASS" : "FAIL")} — evidence: {EvidenceDir}/test-c-harvest.txt");
        }

        [MenuItem("Marooned/Lab C/Run Test D — Deplete Until Regrow", priority = 13)]
        public static async void RunTestD()
        {
            if (!Application.isPlaying) { Debug.LogError("[TestD] ต้องกด Play ก่อน"); return; }

            var lines = new StringBuilder();
            lines.AppendLine($"=== Test D — Deplete until regrow ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            var harvest = scope.Container.Resolve<NodeHarvestSystem>();
            var state = scope.Container.Resolve<GameStateProvider>().GetPlayer();

            // เลือก node ที่ regrow ได้จริงตาม CSV (tree_wood/rock_stone regrowTime=0 = หายถาวร)
            var node = Object.FindObjectsByType<HarvestableNodeComponent>(FindObjectsSortMode.None)
                .FirstOrDefault(n => n.NodeId == RegrowTargetNodeId && !n.IsRegrowing);
            if (node == null) { Debug.LogError($"[TestD] ไม่มี {RegrowTargetNodeId} ใน scene — รัน Test A ก่อน (หรือ biome ปัจจุบันไม่มี node นี้)"); return; }

            // เทเลพอตไปข้าง node แล้วเก็บ "ต้นนี้" โดยเฉพาะ (ไม่ใช่ nearest ต้นอื่น)
            state.PositionX = node.transform.position.x + 0.5f;
            state.PositionY = node.transform.position.y;

            lines.AppendLine($"target node: {node.gameObject.name} durability={node.RemainingDurability} (regrowTime จาก CSV = 30s)");
            var attempts = 0;
            while (node != null && !node.IsRegrowing && attempts < 40)
            {
                var result = harvest.TryHarvest(node, toolItemId: null);
                attempts++;
                lines.AppendLine($"  attempt {attempts}: success={result.Success} reason={result.FailureReason ?? "-"} durability={(node != null ? node.RemainingDurability.ToString() : "0")}");
                if (!result.Success) break;
            }

            if (node == null)
            {
                lines.AppendLine("node ถูก Destroy (regrowTime=0) — ไม่ใช่พฤติกรรมที่เทสนี้ครอบคลุม");
                lines.AppendLine("RESULT: FAIL");
                File.WriteAllText(EvidencePath("test-d-regrow.txt"), lines.ToString(), Encoding.UTF8);
                Debug.LogError("[TestD] FAIL — node หายถาวรทั้งที่ CSV ระบุ regrow > 0");
                return;
            }

            lines.AppendLine($"regrowing after deplete: {node.IsRegrowing}");

            if (node.IsRegrowing)
            {
                var blocked = harvest.TryHarvest(node, toolItemId: null);
                lines.AppendLine($"harvest while regrowing: success={blocked.Success} reason={blocked.FailureReason} (ต้องเป็น regrowing)");
                lines.AppendLine($"state: durability={node.RemainingDurability} regrowing={node.IsRegrowing} (ป้ายใน Game view ควรจาง + '(กำลังงอกใหม่…)')");
            }

            var logs = DiagnosticLogRecorder.FilterByTags("[NodeHarvestSystem]");
            lines.AppendLine("--- related logs (tail) ---");
            foreach (var l in logs.TakeLast(12)) lines.AppendLine(l);

            var pass = node.IsRegrowing;
            lines.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(EvidencePath("test-d-regrow.txt"), lines.ToString(), Encoding.UTF8);
            Debug.Log($"[TestD] {(pass ? "PASS" : "FAIL")} — evidence: {EvidenceDir}/test-d-regrow.txt");
        }

        [MenuItem("Marooned/Lab C/Dump Play Logs", priority = 20)]
        public static void DumpLogs()
        {
            var path = EvidencePath("play-logs.txt");
            var snapshot = DiagnosticLogRecorder.Snapshot();
            File.WriteAllLines(path, snapshot, Encoding.UTF8);
            Debug.Log($"[TestRunner] dumped {snapshot.Length} log lines → {EvidenceDir}/play-logs.txt");
        }
    }
}
