using System;
using System.Collections.Generic;
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
    /// </summary>
    public class CardHandPresenter : IInitializable, IDisposable
    {
        private readonly GameStateProvider _stateProvider;
        private readonly LubanDataService _data;
        private readonly CardHandView _view;
        private readonly ISubscriber<CardInventoryChangedMessage> _subscriber;
        private IDisposable _subscription;

        public CardHandPresenter(GameStateProvider stateProvider, LubanDataService data,
            CardHandView view, ISubscriber<CardInventoryChangedMessage> subscriber)
        {
            _stateProvider = stateProvider;
            _data = data;
            _view = view;
            _subscriber = subscriber;
        }

        public void Initialize()
        {
            _subscription = _subscriber.Subscribe(OnInventoryChanged);
            Render(); // มือเริ่มต้น (เช่น food_coconut=1) ต้องโชว์ตั้งแต่เข้าเกม
        }

        public void Dispose() => _subscription?.Dispose();

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
