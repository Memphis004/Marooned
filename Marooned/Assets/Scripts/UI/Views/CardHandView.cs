using System.Collections.Generic;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>ข้อมูล 1 ช่องการ์ดที่ Presenter ส่งให้ View (View ไม่รู้จัก CardDef โดยตรง)</summary>
    public struct CardSlotData
    {
        public string CardId;
        public string DisplayName;
        public CardCategory Category;
        public int Count;
    }

    /// <summary>
    /// Displays the player's card hand/inventory; passive MVP Lite view —
    /// Presenter calls RenderHand(...) to refresh slots. Slots are pooled
    /// per cardId (per reference project's grid button pooling lesson): การ์ดที่หมด
    /// จะถูกปิด ไม่ destroy เพื่อลด GC และคง layout ที่ rebuild ไว้แล้ว
    /// </summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform cardSlotContainer;
        [SerializeField] private GameObject cardSlotPrefab; // pooled, per reference project's grid button pooling lesson

        private readonly Dictionary<string, CardSlotUI> _slots = new(); // cardId -> slot

        /// <summary>จำนวน slot ที่กำลังแสดงอยู่ (ใช้โดย self-test เป็นหลักฐาน)</summary>
        public int ActiveSlotCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _slots)
                    if (kv.Value != null && kv.Value.gameObject.activeSelf) n++;
                return n;
            }
        }

        /// <summary>Presenter calls RenderHand(...) to refresh slots.</summary>
        public void RenderHand(IReadOnlyList<CardSlotData> cards)
        {
            if (cardSlotContainer == null || cardSlotPrefab == null)
            {
                Debug.LogError("[CardHandView] cardSlotContainer/cardSlotPrefab ยังไม่ถูกอ้างอิง (รัน Marooned/Setup CardHand UI ก่อน)", this);
                return;
            }

            var seen = new HashSet<CardSlotUI>();
            foreach (var card in cards)
            {
                if (!_slots.TryGetValue(card.CardId, out var slot) || slot == null)
                {
                    var go = Instantiate(cardSlotPrefab, cardSlotContainer);
                    go.name = $"CardSlot_{card.CardId}";
                    slot = go.GetComponent<CardSlotUI>();
                    _slots[card.CardId] = slot;
                }

                slot.gameObject.SetActive(true);
                var countChanged = slot.Count != card.Count;
                slot.SetCard(card.CardId, card.DisplayName, card.Category, card.Count);
                if (countChanged) slot.PlayPop(); // การ์ดใหม่ / count เพิ่ม → เด้ง (ไม่ draft slot ซ้ำ)
                seen.Add(slot);
            }

            foreach (var kv in _slots)
                if (!seen.Contains(kv.Value))
                    kv.Value.gameObject.SetActive(false); // pooled: ปิดไว้ ไม่ destroy
        }
    }
}
