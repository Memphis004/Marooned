using Marooned.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Marooned.UI.Views
{
    /// <summary>
    /// Placeholder card visual (ยังไม่มี art): พื้นหลังสีตาม CardCategory +
    /// ไอคอนกล่องขาว + ป้ายชื่อที่ขอบล่าง (โชว์ count เมื่อซ้อน) — pop/pulse ด้วย local scale
    /// </summary>
    public class CardSlotUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;

        /// <summary>จำนวนปัจจุบันที่แสดง — CardHandView ใช้เทียบว่าต้อง pulse ไหม</summary>
        public int Count { get; private set; }

        private float _animT = -1f; // <0 = idle
        private const float PopDuration = 0.18f;
        private const float PopScale = 0.25f;

        public void SetCard(string cardId, string displayName, CardCategory category, int count)
        {
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
