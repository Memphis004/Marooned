using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B Phase 3 — ห่อ Legacy Input (UnityEngine.Input) เป็น service
    /// plain C# ไม่ใช่ MonoBehaviour — Tick() ถูกเรียกจาก GameTickDriver ทุกเฟรม
    ///
    ///  - WASD/Arrow keys → MoveAxis (Vector2 raw)
    ///  - E หรือ Space → InteractPressed แบบ edge-triggered (กดครั้งเดียว = 1 event
    ///    อ่านด้วย ConsumeInteractPressed() แล้วค่าถูกเคลียร์)
    ///
    /// Project Settings: activeInputHandler = 2 (Both) → Legacy Input ใช้ได้
    /// </summary>
    public class PlayerInputService
    {
        /// <summary>แกนเดินปัจจุบัน (raw: -1/0/1 ต่อแกน)</summary>
        public Vector2 MoveAxis { get; private set; }

        private bool _interactPressed;
        private bool _testOverride;

        /// <summary>เรียกทุกเฟรมจาก GameTickDriver — อ่านคีย์บอร์ดเข้า state ภายใน</summary>
        public void Tick()
        {
            if (_testOverride) return; // โหมดเทส: คงค่าจาก InjectTestInput ไว้ ไม่อ่านคีย์บอร์ด

            MoveAxis = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                _interactPressed = true;
        }

        /// <summary>edge-triggered: คืน true ครั้งเดียวต่อการกด 1 ครั้ง</summary>
        public bool ConsumeInteractPressed()
        {
            var pressed = _interactPressed;
            _interactPressed = false;
            return pressed;
        }

        /// <summary>
        /// [เฉพาะเทส] อัดค่า input ตรงเข้า service แทนคีย์บอร์ด
        /// เพื่อให้ Play Mode test ควบคุมได้ deterministic
        /// </summary>
        public void InjectTestInput(Vector2 moveAxis, bool interactPressed)
        {
            _testOverride = true;
            MoveAxis = moveAxis;
            if (interactPressed) _interactPressed = true;
        }

        /// <summary>[เฉพาะเทส] ล็อคโหมด override ถาวร (Tick จะไม่อ่านคีย์บอร์ดอีก)</summary>
        public void EnableTestInputOverride() => _testOverride = true;
    }
}
