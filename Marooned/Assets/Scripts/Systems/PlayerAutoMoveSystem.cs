using Marooned.Shared;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>เหตุผลที่การเดินอัตโนมัติจบลง (อ่านโดย waiter เมื่อ generation ยังตรง)</summary>
    public enum AutoMoveEndReason
    {
        /// <summary>เดินถึงปลายทางปกติ (Tick เห็นระยะ ≤ ArrivalThreshold)</summary>
        Arrived,
        /// <summary>ถูกยกเลิก/แทนที่กลางทาง — waiter ห้าม commit ผลของตัวเอง</summary>
        Cancelled,
    }

    /// <summary>
    /// เดินผู้เล่นเข้าหา TargetX/Y อัตโนมัติ (Auto-Move) — ผลของคำสั่ง
    /// MoveToLocation (MCP) ที่ตั้ง TargetX/TargetY + IsAutoMoving ไว้
    ///
    /// ทำไมต้องแยกระบบ: PlayerMovementSystem อ่านคีย์บอร์ด ส่วนระบบนี้อ่าน
    /// state ล้วนๆ — สองระบบแย่งกันเขียน PositionX/Y ได้ จึงกันด้วย flag
    /// IsAutoMoving (PlayerMovementSystem return ทันทีเมื่อ flag ติด)
    /// → Single Source of Truth: ต่อเฟรมมี "ระบบเดียว" เขียน PositionX/Y
    ///
    /// เป็น plain C# singleton (VContainer Lifetime.Singleton) ถูก tick โดย
    /// GameTickDriver ก่อน PlayerMovementSystem เสมอ — pattern เดียวกับ
    /// NpcMovementSystem (MoveTowardTarget แบบ frame-rate independent +
    /// กัน overshoot หลังเป้า)
    /// </summary>
    public class PlayerAutoMoveSystem
    {
        private readonly PlayerSurvivalState _player;

        /// <summary>ความเร็วเดิน (world unit/วินาที) — ให้ตรงกับ PlayerMovementSystem</summary>
        public float Speed = 3.5f;

        /// <summary>ระยะถือว่า "ถึงเป้าแล้ว" (world unit)</summary>
        public float ArrivalThreshold = 0.1f;

        /// <summary>
        /// รุ่น (generation) ของการเดินปัจจุบัน — เพิ่มทุกครั้งที่ Begin/Cancel
        /// waiter (handler ที่ await อยู่) ใช้เทียบ: ถ้า generation เปลี่ยน =
        /// การเดินของตัวเองถูกแทนที่/ยกเลิก → ห้าม commit โซน
        /// (กัน race: handler เก่ามาปลุกจากคำสั่งใหม่)
        /// </summary>
        public int MoveGeneration { get; private set; }

        /// <summary>เหตุผลล่าสุดที่การเดินจบ — waiter อ่านหลัง generation ตรง</summary>
        public AutoMoveEndReason LastEndReason { get; private set; } = AutoMoveEndReason.Arrived;

        public PlayerAutoMoveSystem(GameStateProvider stateProvider)
        {
            _player = stateProvider.GetPlayer();
        }

        /// <summary>
        /// เริ่มเดินหา TargetX/Y — จุดเดียวที่อนุญาตให้ตั้ง IsAutoMoving = true
        /// (การเดินใหม่แทนที่การเดินเดิมเสมอ: bump generation ให้ waiter เก่ารู้ตัว)
        /// คืน generation ของการเดินนี้สำหรับ waiter เก็บไว้เทียบ
        /// </summary>
        public int Begin(float targetX, float targetY)
        {
            _player.TargetX = targetX;
            _player.TargetY = targetY;
            _player.IsAutoMoving = true;
            return ++MoveGeneration;
        }

        /// <summary>
        /// ยกเลิกการเดินทันที (ไม่เปลี่ยนตำแหน่ง/โซน) — bump generation ให้ waiter
        /// เก่ารู้ว่าถูก supersede + ปลดล็อกคีย์บอร์ด (movement กลับมาคุมเฟรมถัดไป)
        /// คืน false ถ้าไม่ได้เดินอยู่
        /// </summary>
        public bool Cancel()
        {
            if (!_player.IsAutoMoving) return false;
            _player.IsAutoMoving = false;
            _player.Activity = NpcActivityState.Idle;
            LastEndReason = AutoMoveEndReason.Cancelled;
            MoveGeneration++;
            return true;
        }

        /// <summary>
        /// เลื่อน PositionX/Y เข้าหา TargetX/Y เมื่อ IsAutoMoving เท่านั้น —
        /// ถึงเป้า (≤ ArrivalThreshold) แล้วปิด flag + Activity = Idle
        /// (MoveToLocationHandler ที่ await อยู่จะปลุกทันทีในเฟรมเดียวกัน)
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!_player.IsAutoMoving) return;

            var dx = _player.TargetX - _player.PositionX;
            var dy = _player.TargetY - _player.PositionY;
            var distance = Mathf.Sqrt(dx * dx + dy * dy);

            // ถึงปลายทางแล้ว — ปิด auto-move ก่อนอย่างอื่น
            // (ห้าม bump MoveGeneration ตรงนี้ — waiter ใช้ gen != ปัจจุบัน
            //  แยก "ถูก supersede" ออกจาก "เดินถึงเอง"; ถ้า bump ตอนถึง
            //  ทุกการถึงจะโดนมองว่า superseded)
            if (distance <= ArrivalThreshold)
            {
                _player.IsAutoMoving = false;
                _player.Activity = NpcActivityState.Idle;
                LastEndReason = AutoMoveEndReason.Arrived;
                return;
            }

            var step = Speed * deltaSeconds;
            if (step >= distance)
            {
                // กัน overshoot หลังเป้า
                _player.PositionX = _player.TargetX;
                _player.PositionY = _player.TargetY;
            }
            else
            {
                var inv = step / distance;
                _player.PositionX += dx * inv;
                _player.PositionY += dy * inv;
            }

            // หันหน้าตามทิศทางแกน x (top-down lite — flip เฉพาะซ้าย/ขวา, dx = 0 คงเดิม)
            if (dx > 0f) _player.FacingRight = true;
            else if (dx < 0f) _player.FacingRight = false;

            _player.Activity = NpcActivityState.Traveling;
        }
    }
}
