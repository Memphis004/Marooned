using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.UI.Views;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// [ชั่วคราว เฉพาะเทส Lab B Phase 5 — Card Hand UI]
    /// แนบที่ CardHandSystem โดย UiSetupAutomation (ไม่ถูก save ลง scene)
    /// explore beach 6 ครั้งผ่าน MCP handler จริง → การ์ดเด้งเข้ามือ +
    /// เจอการ์ดซ้ำ → count เพิ่ม + pulse (ไม่ draft slot ใหม่)
    /// ผล → LabB5_result.txt + LabB5_cardhand_screenshot.png
    /// bump SessionState "LabB5Phase" แล้วออก play
    /// </summary>
    public class CardHandPlayModeSelfTest : MonoBehaviour
    {
        private async void Start()
        {
            var scope = GetComponentInParent<GameLifetimeScope>();
            var sb = new StringBuilder();

            try
            {
                var explore = scope.Container.Resolve<IAsyncRequestHandler<ExploreLocationRequest, ExploreLocationResponse>>();
                var gameState = scope.Container.Resolve<IAsyncRequestHandler<GetGameStateRequest, GetGameStateResponse>>();
                var stateProvider = scope.Container.Resolve<GameStateProvider>();
                var view = GetComponent<CardHandView>();
                var state = stateProvider.GetPlayer();

                string Inv() => "{" + string.Join(", ", state.Inventory.Select(kv => kv.Key + "=" + kv.Value)) + "}";

                await UniTask.Delay(2000, DelayType.UnscaledDeltaTime); // รอ initial render + TCP worker
                sb.AppendLine($"[0] initial: activeSlots={view.ActiveSlotCount} inventory={Inv()}");

                for (int i = 1; i <= 6; i++)
                {
                    var resp = await explore.InvokeAsync(new ExploreLocationRequest { LocationId = "beach" });
                    await UniTask.NextFrame();
                    await UniTask.NextFrame(); // ให้ MessagePipe → Presenter → View → Layout ทำงาน 1 เฟรม
                    sb.AppendLine($"[{i}] explore success={resp.Success} found=[{string.Join(",", resp.FoundCardIds)}] activeSlots={view.ActiveSlotCount} inventory={Inv()}");
                }

                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);
                ScreenCapture.CaptureScreenshot("LabB5_cardhand_screenshot.png");
                sb.AppendLine("[shot] สั่งจับ Game View → LabB5_cardhand_screenshot.png");
                await UniTask.Delay(1000, DelayType.UnscaledDeltaTime); // รอไฟล์ screenshot เขียนจบ

                var gs = await gameState.InvokeAsync(new GetGameStateRequest());
                sb.AppendLine($"[final] get_game_state inventory: {{{string.Join(", ", gs.Player.Inventory.Select(kv => kv.Key + "=" + kv.Value))}}}");
                sb.AppendLine("DONE");
            }
            catch (System.Exception ex)
            {
                sb.AppendLine("EXCEPTION: " + ex);
            }

            // var outPath = Path.Combine(Application.dataPath, "..", "LabB5_result.txt");
            var outPath = Path.Combine(Application.persistentDataPath, "LabB5_result.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[LabB5SelfTest] เขียนผลแล้ว: {outPath}");

#if UNITY_EDITOR
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            UnityEditor.SessionState.SetInt("LabB5Phase", 1);
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
