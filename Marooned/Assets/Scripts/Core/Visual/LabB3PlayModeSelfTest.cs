using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Marooned.Core.Visual;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// [ชั่วคราว เฉพาะเทส Lab B Phase 2+3]
    /// mode 0 = Phase 2 evidence: backend=Spine → spawn สลับ Elena/Derek, 5→0→5,
    ///          บังคับ Traveling/Talking → เช็ค AnimationState
    /// mode 1 = Phase 3 A–D: เดิน (keyboard window), เก็บ InteractKey, WalkOver,
    ///          NPC rotation + move_to_location + item respawn
    /// ผล → LabB3_result_mode{mode}.txt ; bump SessionState "LabB3Phase" แล้วออก play
    /// </summary>
    public class LabB3PlayModeSelfTest : MonoBehaviour
    {
        [SerializeField] private int mode; // default 0 = Phase 2 evidence (set 1 ผ่าน Inspector สำหรับ Phase 3 A–D)

        private async void Start()
        {
            var scope = GetComponentInParent<GameLifetimeScope>();
            var sb = new StringBuilder();

            try
            {
                var moveTo = scope.Container.Resolve<IAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse>>();
                var gameState = scope.Container.Resolve<IAsyncRequestHandler<GetGameStateRequest, GetGameStateResponse>>();
                var spawner = FindFirstObjectByType<ChibiSpawnerView>();
                var worldItems = scope.Container.Resolve<WorldItemSystem>();
                var input = scope.Container.Resolve<PlayerInputService>();
                var pickup = scope.Container.Resolve<ItemPickupSystem>();
                var state = scope.Container.Resolve<GameStateProvider>().Player;

                string ChildSummary()
                {
                    var names = new StringBuilder();
                    for (int i = 0; i < spawner.transform.childCount; i++)
                        names.Append(spawner.transform.GetChild(i).name).Append(" ");
                    return $"count={spawner.transform.childCount} [{names}]";
                }

                if (mode == 0)
                {
                    // ---------- Phase 2 evidence (Spine backend) ----------
                    await UniTask.Delay(5000, Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime); // screenshot window
                    sb.AppendLine($"[P2] backend={spawner.Backend}");
                    sb.AppendLine($"[P2-A1] spawn ครั้งแรก: {ChildSummary()} (ต้องสลับ Elena/Derek, count=5)");

                    var m1 = await moveTo.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    sb.AppendLine($"[P2-A2] move jungle_edge success={m1.Success}: count={spawner.transform.childCount} (ต้อง 0)");

                    var m2 = await moveTo.InvokeAsync(new MoveToLocationRequest { LocationId = "beach" });
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    sb.AppendLine($"[P2-A3] move beach success={m2.Success}: count={spawner.transform.childCount} (ต้อง 5)");

                    // Phase 2 Test B: บังคับ state → เช็ค AnimationState (Elena และ Derek)
                    for (int i = 0; i < 2 && i < spawner.transform.childCount; i++)
                    {
                        var child = spawner.transform.GetChild(i).gameObject;
                        var visual = child.GetComponent<IChibiVisual>();
                        var spine = child.GetComponent<SpineVisualController>();
                        if (spine == null) { sb.AppendLine($"[P2-B] {child.name}: ไม่ใช่ Spine"); continue; }
                        sb.Append($"[P2-B] {child.name}: current={spine.CurrentAnimationName}");
                        visual.Bind(NpcActivityState.Traveling);
                        await UniTask.NextFrame(); await UniTask.NextFrame();
                        sb.Append($" → Traveling={spine.CurrentAnimationName}");
                        visual.Bind(NpcActivityState.Talking);
                        await UniTask.NextFrame(); await UniTask.NextFrame();
                        sb.AppendLine($" → Talking={spine.CurrentAnimationName}");
                    }
                }
                else
                {
                    // ---------- Phase 3 A: เดินด้วย WASD (keyboard window 6 วิ) ----------
                    sb.AppendLine("[A] KEYWINDOW-START (ส่ง WASD ผ่านคีย์บอร์ดจริง)");
                    float startX = state.PositionX, startY = state.PositionY;
                    await UniTask.Delay(30000, Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime); // ขยายหน้าต่างเป็น 30s เผื่อ timing ของ key events จากภายนอก
                    input.EnableTestInputOverride(); // หลัง window ปิด สลับไปใช้ injected input
                    sb.AppendLine($"[A] KEYWINDOW-END: pos ({startX:F2},{startY:F2}) → ({state.PositionX:F2},{state.PositionY:F2}) activity={state.Activity} (ต้องขยับ + Traveling)");

                    // ---------- Phase 3 D (ส่วน NPC): rotation + move_to_location + item respawn ----------
                    sb.AppendLine($"[D1] NPC rotation: {ChildSummary()} (ต้องสลับ Wizard/CollegeStudent)");
                    var m1 = await moveTo.InvokeAsync(new MoveToLocationRequest { LocationId = "jungle_edge" });
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    sb.AppendLine($"[D2] move jungle_edge success={m1.Success}: NPC count={spawner.transform.childCount} (ต้อง 0), items={worldItems.ActiveItems.Count} (โซน mat_vine, ต้อง {worldItems.ItemsPerZone})");
                    var firstItemId = worldItems.ActiveItems.Count > 0 ? worldItems.ActiveItems[0].CardId : "(none)";
                    sb.AppendLine($"[D2] item ในโซนนี้: {firstItemId} (ต้องเป็น mat_vine)");

                    var m2 = await moveTo.InvokeAsync(new MoveToLocationRequest { LocationId = "beach" });
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    sb.AppendLine($"[D3] move beach success={m2.Success}: NPC count={spawner.transform.childCount} (ต้อง 5), items={worldItems.ActiveItems.Count} (respawn food_coconut)");

                    // ---------- Phase 3 B: เก็บแบบ InteractKey ----------
                    int beforeItems = worldItems.ActiveItems.Count;
                    var invBefore = state.Inventory.TryGetValue("food_coconut", out var c0) ? c0 : 0;
                    var target = worldItems.ActiveItems[0];
                    state.PositionX = target.GameObject.transform.position.x; // teleport ไปที่ตัวไอเท็ม
                    state.PositionY = target.GameObject.transform.position.y;
                    await UniTask.NextFrame();
                    input.InjectTestInput(Vector2.zero, interactPressed: true); // กด E (จำลอง)
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    var invAfter = state.Inventory.TryGetValue("food_coconut", out var c1) ? c1 : 0;
                    sb.AppendLine($"[B] InteractKey: items {beforeItems}→{worldItems.ActiveItems.Count} (ต้อง -1), food_coconut {invBefore}→{invAfter} (ต้อง +1)");

                    // ---------- Phase 3 C: โหมด WalkOver ----------
                    pickup.Mode = PickupMode.WalkOver;
                    int beforeWalkOver = worldItems.ActiveItems.Count;
                    var target2 = worldItems.ActiveItems[0];
                    state.PositionX = target2.GameObject.transform.position.x;
                    state.PositionY = target2.GameObject.transform.position.y;
                    await UniTask.NextFrame(); await UniTask.NextFrame();
                    sb.AppendLine($"[C] WalkOver: items {beforeWalkOver}→{worldItems.ActiveItems.Count} (ต้อง -1)");

                    var gs = await gameState.InvokeAsync(new GetGameStateRequest());
                    sb.AppendLine($"[D4] get_game_state inventory: {{{string.Join(", ", gs.Player.Inventory.Select(kv => kv.Key + "=" + kv.Value))}}}");
                }

                sb.AppendLine("DONE");
            }
            catch (System.Exception ex)
            {
                sb.AppendLine("EXCEPTION: " + ex);
            }

            var outPath = Path.Combine(Application.dataPath, "..", $"LabB3_result_mode{mode}.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[LabB3SelfTest mode={mode}] เขียนผลแล้ว: {outPath}");

#if UNITY_EDITOR
            await UniTask.Delay(1500, Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime);
            UnityEditor.SessionState.SetInt("LabB3Run2Phase", mode + 1);
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
