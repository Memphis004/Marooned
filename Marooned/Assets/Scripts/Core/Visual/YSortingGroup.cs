using UnityEngine;
using UnityEngine.Rendering;

namespace Marooned.Core.Visual
{
    /// <summary>
    /// อัปเดต SortingGroup.sortingOrder ตามตำแหน่ง Y ของตัวเองทุกเฟรม (2D top-down
    /// depth sorting) — Y ยิ่งต่ำ (อยู่ด้านล่างจอ/ใกล้กล้องมากกว่า) ยิ่งวาดทับคนอื่น
    /// ต้องมี SortingGroup component อยู่บน GameObject เดียวกัน (หรือ root ของตัวละคร)
    /// เพื่อให้ sprite ย่อยทั้งหมดใน hierarchy sort เป็นก้อนเดียว
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public class YSortingGroup : MonoBehaviour
    {
        [Tooltip("คูณค่า Y ก่อนแปลงเป็น int (ยิ่งมากยิ่งละเอียด กัน rounding ชนกัน)")]
        [SerializeField] private float precisionMultiplier = 100f;

        [Tooltip("เพิ่ม offset ท้ายสุด เผื่อ layer อื่นต้อง sort แทรก")]
        [SerializeField] private int baseOrder = 0;

        private SortingGroup _sortingGroup;

        private void Awake() => _sortingGroup = GetComponent<SortingGroup>();

        private void LateUpdate()
        {
            // Y ต่ำ (อยู่ล่างจอ) ต้องวาดทับ Y สูง (อยู่บนจอ) → sortingOrder แปรผกผันกับ Y
            _sortingGroup.sortingOrder = baseOrder - Mathf.RoundToInt(transform.position.y * precisionMultiplier);
        }
    }
}