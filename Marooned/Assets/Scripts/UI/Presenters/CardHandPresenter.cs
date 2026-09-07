using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.UI.Views;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.UI.Presenters
{
    /// <summary>
    /// Plain C# singleton (VContainer entry point — ไม่ใช่ MonoBehaviour).
    /// Subscribe CardInventoryChangedMessage → re-render มือการ์ดแบบ event-driven
    /// (แทน polling) แล้ว push เป็น List&lt;CardSlotData&gt; ให้ CardHandView (passive) render
    ///
    /// Lab B Phase 5 (click-to-use — minimum viable):
    ///  - คลิกการ์ด (CardSlotUI.OnPointerClick → CardHandView ส่งต่อ) → UseCardFromSlot
    ///  - CardTargetType.Self → เรียก UseCardHandler ทันที (เช่น กินอาหาร)
    ///  - CardTargetType.SingleTarget → เข้าโหมดเลือกเป้าหมาย (คลิก NPC ผ่าน
    ///    ChibiSpawnerView, คลิกที่ว่าง = ยกเลิก)
    ///  - ผลลัพธ์ Success/Failure แสดงผ่าน CardHandView.ShowFeedback
    ///    (FailureReason → GetLocalizedReason) — การ์ดที่ใช้สำเร็จหายจากมือเอง
    ///    เพราะ CardInventorySystem publish CardInventoryChangedMessage → Render()
    /// </summary>
    public class CardHandPresenter : IInitializable, IDisposable
    {
        private readonly GameStateProvider _stateProvider;
        private readonly LubanDataService _data;
        private readonly CardHandView _view;
        private readonly ChibiSpawnerView _spawner;
        private readonly IAsyncRequestHandler<UseCardRequest, UseCardResponse> _useCard;
        private readonly ISubscriber<CardInventoryChangedMessage> _subscriber;
        private IDisposable _subscription;

        // ---- Target selection mode (weapon/SingleTarget cards) ----
        private bool _isSelectingTarget;
        private string _pendingCardId;

        public CardHandPresenter(GameStateProvider stateProvider, LubanDataService data,
            CardHandView view, ChibiSpawnerView spawner,
            IAsyncRequestHandler<UseCardRequest, UseCardResponse> useCard,
            ISubscriber<CardInventoryChangedMessage> subscriber)
        {
            _stateProvider = stateProvider;
            _data = data;
            _view = view;
            _spawner = spawner;
            _useCard = useCard;
            _subscriber = subscriber;
        }

        public void Initialize()
        {
            _subscription = _subscriber.Subscribe(OnInventoryChanged);
            _view.SetSlotClickHandler(UseCardFromSlot); // MVP Lite: view ส่ง event มา, presenter ตัดสินใจ
            _spawner.NpcClicked += OnNpcClicked;
            _spawner.WorldClicked += CancelTargetSelection;
            Render(); // มือเริ่มต้น (เช่น food_coconut=1) ต้องโชว์ตั้งแต่เข้าเกม
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _view.SetSlotClickHandler(null);
            _spawner.NpcClicked -= OnNpcClicked;
            _spawner.WorldClicked -= CancelTargetSelection;
            EndTargetSelection(); // คืนสี chibi ถ้าถูกทำลายระหว่างเลือกเป้าหมาย
        }

        private void OnInventoryChanged(CardInventoryChangedMessage msg)
        {
            Debug.Log($"[CardHandPresenter] {msg.CardId} → {msg.NewCount} (Δ{msg.Delta}) → re-render");
            Render();
        }

        private void Render()
        {
            var inventory = _stateProvider.GetPlayer().Inventory;
            var cardDefs = _data.CardDefs;
            var slots = new List<CardSlotData>(inventory.Count);
            foreach (var kv in inventory)
            {
                if (!cardDefs.TryGetValue(kv.Key, out var def)) continue; // การ์ดไม่มี def — ข้าม
                slots.Add(new CardSlotData
                {
                    CardId = kv.Key,
                    DisplayName = def.DisplayName,
                    Category = def.Category,
                    Count = kv.Value,
                });
            }
            _view.RenderHand(slots);
        }

        // ---- Lab B Phase 5: click-to-use ----

        /// <summary>
        /// จุดรับ click จากมือการ์ด (ผ่าน CardHandView.SetSlotClickHandler)
        /// Self → ใช้ทันที, SingleTarget → เข้าโหมดเลือกเป้าหมาย
        /// </summary>
        public void UseCardFromSlot(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (_isSelectingTarget) EndTargetSelection(); // คลิกการ์ดอื่นระหว่างเลือกเป้าหมาย → เปลี่ยนการ์ดที่จะใช้

            if (!_data.CardDefs.TryGetValue(cardId, out var def))
            {
                _view.ShowFeedback(GetLocalizedReason("unknown_card"));
                return;
            }

            if (def.TargetType == CardTargetType.SingleTarget)
            {
                BeginTargetSelection(cardId, def);
                return;
            }

            _ = UseCardAsync(cardId, null, def);
        }

        private void BeginTargetSelection(string cardId, CardDef def)
        {
            _pendingCardId = cardId;
            _isSelectingTarget = true;
            _spawner.SetTargetSelectionMode(true); // highlight NPC ทั้งหมด + เปิดรับ click
            _view.ShowFeedback($"เลือกเป้าหมาย: {def.DisplayName} — คลิกที่ NPC (คลิกที่ว่างเพื่อยกเลิก)");
            Debug.Log($"[CardHandPresenter] เข้าโหมดเลือกเป้าหมาย: {cardId}");
        }

        private void EndTargetSelection()
        {
            if (!_isSelectingTarget) return;
            _isSelectingTarget = false;
            _pendingCardId = null;
            _spawner.SetTargetSelectionMode(false);
        }

        /// <summary>คลิกที่ว่างระหว่างโหมดเลือกเป้าหมาย → ยกเลิก (ChibiSpawnerView.WorldClicked)</summary>
        private void CancelTargetSelection()
        {
            if (!_isSelectingTarget) return;
            EndTargetSelection();
            _view.ShowFeedback("ยกเลิกการเลือกเป้าหมาย");
        }

        /// <summary>คลิกโดน NPC ระหว่างโหมดเลือกเป้าหมาย → ใช้การ์ดกับเป้าหมายนั้น</summary>
        private void OnNpcClicked(string npcId)
        {
            if (!_isSelectingTarget || string.IsNullOrEmpty(npcId)) return;
            var cardId = _pendingCardId;
            EndTargetSelection();
            if (cardId == null || !_data.CardDefs.TryGetValue(cardId, out var def)) return;
            _ = UseCardAsync(cardId, npcId, def);
        }

        /// <summary>เรียก UseCardHandler ผ่าน MessagePipe request/response แล้วแสดงผลบน UI</summary>
        private async UniTaskVoid UseCardAsync(string cardId, string targetId, CardDef def)
        {
            try
            {
                var response = await _useCard.InvokeAsync(new UseCardRequest { CardId = cardId, TargetId = targetId });

                if (response.Success)
                {
                    // การ์ดหายจากมือเอง — CardInventorySystem publish CardInventoryChangedMessage → Render()
                    _view.ShowFeedback(FormatSuccess(response.ResultText, def));
                    Debug.Log($"[CardHandPresenter] ใช้การ์ดสำเร็จ: {cardId} target={targetId ?? "self"}");
                }
                else
                {
                    _view.ShowFeedback(GetLocalizedReason(response.FailureReason));
                    Debug.Log($"[CardHandPresenter] ใช้การ์ดไม่สำเร็จ: {cardId} → {response.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardHandPresenter] UseCard error: {ex}");
                _view.ShowFeedback("เกิดข้อผิดพลาดในการใช้การ์ด");
            }
        }

        private static string FormatSuccess(string resultText, CardDef def)
        {
            if (string.IsNullOrEmpty(resultText)) return $"ใช้ {def.DisplayName} สำเร็จ";
            // UseCardHandler คืน "eliminated_<npcId>" เมื่อฆ่าสำเร็จ
            const string prefix = "eliminated_";
            return resultText.StartsWith(prefix, StringComparison.Ordinal)
                ? $"กำจัด {resultText.Substring(prefix.Length)} สำเร็จ"
                : resultText;
        }

        // ---- Phase 4 Step 7: use_card feedback ----
        // เมื่อ UI flow ใช้การ์ด (เรียก UseCardHandler ผ่าน MessagePipe request/response)
        // ให้ map FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยก่อนแสดงผล
        // ครอบคลุมทุก reason ที่ UseCardHandler/CanEliminate คืนได้จริง:
        //   unknown_card, missing_target, invalid_target_type,
        //   unknown_target, target_already_dead, target_not_same_location,
        //   witnessed, not_in_inventory

        /// <summary>แปลง FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยสำหรับผู้เล่น</summary>
        public string GetLocalizedReason(string reason) => reason switch
        {
            "witnessed" => "มีคนเห็น! ไม่สามารถลงมือได้",
            "target_not_same_location" => "เป้าหมายไม่ได้อยู่ในโซนเดียวกัน",
            "target_already_dead" => "เป้าหมายนี้ไม่อยู่แล้ว",
            "missing_target" => "ต้องระบุเป้าหมายสำหรับไอเท็มนี้",
            "invalid_target_type" => "การ์ดนี้ใช้กับเป้าหมายไม่ได้",
            "not_in_inventory" => "ไม่มีการ์ดนี้ในมือ",
            "unknown_card" => "ไม่รู้จักการ์ดนี้",
            "unknown_target" => "ไม่พบเป้าหมายนี้",
            _ => "ใช้งานไม่ได้"
        };
    }
}
