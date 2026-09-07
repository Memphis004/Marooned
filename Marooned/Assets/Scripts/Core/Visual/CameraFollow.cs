using UnityEngine;

namespace Marooned.Core.Visual
{
    /// <summary>
    /// Lab B Phase 6 — View layer ของกล้อง: ตาม Player แบบนุ่มนวลด้วย Vector3.Lerp
    /// ทุก LateUpdate (เรียกหลัง Update ของ PlayerCharacterView ที่ apply ตำแหน่ง state
    /// แล้ว กล้องจึงตามตำแหน่งเฟรมล่าสุดเสมอ)
    ///
    /// ตามบทเรียน Lab 13: การตามกล้องเป็น continuous state → อ่าน Transform ตรงๆ
    /// ไม่ pub/sub และไม่ resolve อะไรจาก VContainer (วางบน Main Camera ที่อยู่
    /// นอก hierarchy ของ scope ก็ได้)
    ///
    /// ติดตั้งบน Main Camera แล้วลาก Player GameObject (PlayerCharacter) ใส่ช่อง
    /// Target — ถ้าลืมลาก จะพยายามหาเองจาก tag "Player" ตอน Start
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        /// <summary>0 = หยุดนิ่ง, 1 = สแนปทันที (ค่าต่ำ = เคลื่อนนุ่มแต่ช้า)</summary>
        [SerializeField] private float smoothSpeed = 0.125f;

        private void Start()
        {
            if (target != null) return;

            // กันลืมลาก reference: หาเองจาก tag "Player" (tag  builtin ของ Unity)
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                target = playerGo.transform;
                Debug.Log("[CameraFollow] auto-attach กล้องเข้ากับ Player จาก tag \"Player\"");
            }
            else
            {
                Debug.LogWarning(
                    "[CameraFollow] ยังไม่มี Target — ลาก Player GameObject ใส่ช่อง Target ใน Inspector " +
                    "(หรือตั้ง tag \"Player\" ให้ PlayerCharacter) กล้องจึงจะตามผู้เล่น");
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // ตามเฉพาะแกน X/Y — คง Z เดิมของกล้องไว้ (ปกติ -10) ไม่ให้กล้องวิ่ง
            // เข้าไปในระนาบของสไปรต์ (z=0)
            var desiredPosition = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        }
    }
}
