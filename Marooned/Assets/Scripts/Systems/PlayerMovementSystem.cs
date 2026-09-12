using Marooned.Core;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab B Phase 6 — ขอบเขตเดินของ "แผนที่รวม" (config object เท่านั้น ไม่มี logic)
    /// PlayerMovementSystem เป็น plain C# singleton (VContainer) ใส่ [SerializeField]
    /// ไม่ได้ จึงให้ GameLifetimeScope (MonoBehaviour ประจำ scope) เก็บค่าไว้ใน
    /// Inspector แล้ว inject เข้ามาทาง constructor ผ่าน struct นี้
    /// </summary>
    public readonly struct WorldBounds
    {
        public WorldBounds(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }
    }

    /// <summary>
    /// Lab B Phase 3 — อัปเดตตำแหน่งผู้เล่นจาก PlayerInputService (Tick-driven
    /// ผ่าน GameTickDriver เดิม) — เก็บ WorldX/WorldY ใน PlayerSurvivalState
    /// (PositionX/PositionY) ไม่มีการ publish message ทุกเฟรม — View อ่าน state
    /// ตรงผ่าน GameStateProvider (บทเรียน Lab 13: ของที่ต่อเนื่องเรียกตรง)
    ///
    /// Auto-Move (MoveToLocation fix): ขณะ IsAutoMoving = true ระบบนี้ return
    /// ทันที — การควบคุมย้ายไปอยู่กับ PlayerAutoMoveSystem จนกว่าจะถึงปลายทาง
    /// (Test C: กด WASD ระหว่างเดินอัตโนมัติ → ไม่ตอบสนอง)
    /// </summary>
    public class PlayerMovementSystem
    {
        private readonly PlayerInputService _input;
        private readonly PlayerSurvivalState _player;

        /// <summary>ความเร็วเดิน (world unit/วินาที)</summary>
        public float Speed = 3.5f;

        // ขอบเขตโลก (top-down lite) — clamp ไม่ให้เดินหลุดกรอบ
        // Phase 6: ค่า inject จาก GameLifetimeScope ([SerializeField] worldMinX ฯลฯ
        // บน scope) — ปรับขนาด "แผนที่รวม" ได้จาก Inspector แทนค่า hardcoded
        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }

        public PlayerMovementSystem(PlayerInputService input, GameStateProvider stateProvider, WorldBounds worldBounds)
        {
            _input = input;
            _player = stateProvider.GetPlayer();
            MinX = worldBounds.MinX;
            MaxX = worldBounds.MaxX;
            MinY = worldBounds.MinY;
            MaxY = worldBounds.MaxY;
        }

        public void Tick(float deltaSeconds)
        {
            // Auto-Move กำลังควบคุมตัวละครอยู่ — คีย์บอร์ดห้ามแย่ง control
            // (PlayerInputService ยัง Tick ปกติ — แค่ค่า MoveAxis ไม่ถูกใช้ตอนนี้)
            if (_player.IsAutoMoving) return;

            var axis = _input.MoveAxis;

            if (axis.sqrMagnitude > 0.001f)
            {
                _player.PositionX = Mathf.Clamp(_player.PositionX + axis.x * Speed * deltaSeconds, MinX, MaxX);
                _player.PositionY = Mathf.Clamp(_player.PositionY + axis.y * Speed * deltaSeconds, MinY, MaxY);

                // หันซ้าย/ขวาตามแกน x เท่านั้น (ขึ้น/ลงใช้ walk เดิม — top-down lite)
                if (Mathf.Abs(axis.x) > 0.01f)
                    _player.FacingRight = axis.x > 0f;

                _player.Activity = NpcActivityState.Traveling;
            }
            else
            {
                _player.Activity = NpcActivityState.Idle;
            }
        }
    }
}
