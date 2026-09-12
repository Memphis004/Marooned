using System;
using System.Collections.Generic;
using Marooned.Shared;
using Marooned.Systems;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    ///
    /// Lab B Phase 5 (click-to-use): คลิก slot → ส่งต่อ cardId ให้ Presenter ผ่าน
    /// SetSlotClickHandler (View ไม่รู้จัก Presenter โดยตรง — ผูกด้วย delegate)
    /// + ShowFeedback สำหรับข้อความ Success/Failure (auto-create Text ถ้ายังไม่ผูกใน Inspector)
    /// </summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform cardSlotContainer;
        [SerializeField] private GameObject cardSlotPrefab; // pooled, per reference project's grid button pooling lesson
        [SerializeField] private TMP_Text feedbackText;     // optional — auto-create ถ้าไม่ผูก

        private readonly Dictionary<string, CardSlotUI> _slots = new(); // cardId -> slot
        private Action<string> _slotClickHandler;
        private TMP_Text _feedback;
        private float _feedbackHideAtTime;
        private const float FeedbackDuration = 3f;

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

        /// <summary>
        /// MVP Lite: Presenter ผ่าน delegate เข้ามาตอน Initialize — คลิก slot แล้ว
        /// view แค่ส่งต่อ cardId (view ไม่รู้จัก presenter class โดยตรง)
        /// </summary>
        public void SetSlotClickHandler(Action<string> handler)
        {
            _slotClickHandler = handler;
            foreach (var kv in _slots)
                if (kv.Value != null) WireSlot(kv.Value);
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
                WireSlot(slot); // idempotent (-= ก่อน +=) — slot pooled ถูก wire ซ้ำได้ปลอดภัย
                var countChanged = slot.Count != card.Count;
                slot.SetCard(card.CardId, card.DisplayName, card.Category, card.Count);
                if (countChanged) slot.PlayPop(); // การ์ดใหม่ / count เพิ่ม → เด้ง (ไม่ draft slot ซ้ำ)
                seen.Add(slot);
            }

            foreach (var kv in _slots)
                if (!seen.Contains(kv.Value))
                    kv.Value.gameObject.SetActive(false); // pooled: ปิดไว้ ไม่ destroy
        }

        /// <summary>แสดงข้อความ Success/Failure (เช่น "มีคนเห็น!") — หายเองใน ~3 วิ</summary>
        public void ShowFeedback(string message)
        {
            EnsureFeedbackText();
            if (_feedback == null)
            {
                Debug.Log($"[CardHandView] feedback: {message}");
                return;
            }
            _feedback.text = message;
            _feedbackHideAtTime = Time.unscaledTime + FeedbackDuration;
        }

        private void WireSlot(CardSlotUI slot)
        {
            slot.Clicked -= OnSlotClicked; // กัน subscribe ซ้ำเมื่อ render รอบถัดไป
            slot.Clicked += OnSlotClicked;
        }

        private void OnSlotClicked(string cardId) => _slotClickHandler?.Invoke(cardId);

        /// <summary>หา TMP_Text จาก Inspector ก่อน — ไม่มี then สร้างเองใต้ Canvas (THSarabunPSK SDF)</summary>
        private void EnsureFeedbackText()
        {
            if (_feedback != null) return;
            if (feedbackText != null) { _feedback = feedbackText; return; }

            var canvas = cardSlotContainer != null ? cardSlotContainer.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                Debug.LogWarning("[CardHandView] หา Canvas ไม่เจอ — แสดง feedback ทาง Console แทน", this);
                return;
            }

            var go = new GameObject("CardHandFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f); // กึ่งกลางจอ เหนือมือการ์ด
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 150f);
            rt.sizeDelta = new Vector2(900f, 40f);

            _feedback = go.GetComponent<TextMeshProUGUI>();
            var font = WorldItemSystem.LoadLabelFont();
            if (font != null) _feedback.font = font;
            _feedback.fontSize = 22;
            _feedback.alignment = TextAlignmentOptions.Center;
            _feedback.color = Color.white;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f); // outline กันพื้นหลังสว่าง
        }

        private void Update()
        {
            if (_feedback != null && _feedbackHideAtTime > 0f && Time.unscaledTime >= _feedbackHideAtTime)
            {
                _feedback.text = string.Empty;
                _feedbackHideAtTime = 0f;
            }
        }
    }
}
