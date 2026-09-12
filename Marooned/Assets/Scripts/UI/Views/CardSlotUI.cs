using System;
using Marooned.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Marooned.UI.Views
{
    /// <summary>
    /// Placeholder card visual (ยังไม่มี art): พื้นหลังสีตาม CardCategory +
    /// ไอคอนกล่องขาว + ป้ายชื่อที่ขอบล่าง (โชว์ count เมื่อซ้อน) — pop/pulse ด้วย local scale
    ///
    /// Lab B Phase 5 (click-to-use): ติด IPointerClickHandler — คลิกซ้าย → ส่งต่อ cardId
    /// ผ่าน event Clicked (View passive — Presenter เป็นคนตัดสินใจว่าจะใช้การ์ดยังไง)
    /// </summary>
    public class CardSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        /// <summary>จำนวนปัจจุบันที่แสดง — CardHandView ใช้เทียบว่าต้อง pulse ไหม</summary>
        public int Count { get; private set; }

        /// <summary>cardId ที่ slot นี้กำลังแสดง (null = pooled slot ว่าง — คลิกแล้วเฉยๆ)</summary>
        public string CardId { get; private set; }

        /// <summary>MVP Lite: คลิก → ส่งต่อ cardId ให้ผู้ subscribe (Presenter) ตัดสินใจเอง</summary>
        public event Action<string> Clicked;

        private float _animT = -1f; // <0 = idle
        private const float PopDuration = 0.18f;
        private const float PopScale = 0.25f;

        public void SetCard(string cardId, string displayName, CardCategory category, int count)
        {
            CardId = cardId;
            Count = count;
            if (background != null) background.color = ColorFor(category);
            if (label != null)
            {
                // ฟอนต์ built-in ไม่มี glyph ไทย → fallback เป็น cardId (กล่องข้อความอ่านได้ใน screenshot)
                var shown = displayName;
                if (shown.Length > 0 && label.font != null && !label.font.HasCharacter(shown[0]))
                    shown = cardId;
                label.text = count > 1 ? $"{shown} x{count}" : shown;
            }
            // icon sprite: รอ SpritePath จริงจาก art pipeline (Lab A ยังไม่มี art)
        }

        /// <summary>
        /// IPointerClickHandler: คลิกซ้ายบนการ์ด → ยิง event Clicked(cardId)
        /// (event จาก child Graphic เช่น Icon bubble ขึ้นมาถึง root ได้เองตาม EventSystem)
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (string.IsNullOrEmpty(CardId)) return; // pooled slot ว่าง — ไม่ตอบสนอง
            Clicked?.Invoke(CardId);
        }

        /// <summary>เด้ง 1 ครั้ง (การ์ดใหม่ หรือ count เพิ่ม)</summary>
        public void PlayPop() => _animT = 0f;

        private void Update()
        {
            if (_animT < 0f) return;
            _animT += Time.unscaledDeltaTime;
            if (_animT >= PopDuration)
            {
                transform.localScale = Vector3.one;
                _animT = -1f;
                return;
            }
            var k = 1f - _animT / PopDuration; // 1 → 0
            var s = 1f + PopScale * k * k;     // ease-out: 1.25 → 1.0
            transform.localScale = new Vector3(s, s, 1f);
        }

        public static Color ColorFor(CardCategory category) => category switch
        {
            CardCategory.Resource   => new Color(0.42f, 0.78f, 0.35f), // เขียว
            CardCategory.Craftable  => new Color(0.92f, 0.58f, 0.20f), // ส้ม
            CardCategory.Consumable => new Color(0.95f, 0.82f, 0.35f),
            CardCategory.Tool       => new Color(0.55f, 0.68f, 0.85f),
            CardCategory.Weapon     => new Color(0.48f, 0.48f, 0.55f),
            CardCategory.Clue       => new Color(0.62f, 0.48f, 0.88f),
            CardCategory.Illness    => new Color(0.75f, 0.28f, 0.28f),
            CardCategory.Injury     => new Color(0.85f, 0.35f, 0.25f),
            _ => Color.white,
        };
    }
}
