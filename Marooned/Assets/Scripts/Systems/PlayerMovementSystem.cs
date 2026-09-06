using Marooned.Core;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab B Phase 3 — อัปเดตตำแหน่งผู้เล่นจาก PlayerInputService (Tick-driven
    /// ผ่าน GameTickDriver เดิม) — เก็บ WorldX/WorldY ใน PlayerSurvivalState
    /// (PositionX/PositionY) ไม่มีการ publish message ทุกเฟรม — View อ่าน state
    /// ตรงผ่าน GameStateProvider (บทเรียน Lab 13: ของที่ต่อเนื่องเรียกตรง)
    /// </summary>
    public class PlayerMovementSystem
    {
        private readonly PlayerInputService _input;
        private readonly PlayerSurvivalState _player;

        /// <summary>ความเร็วเดิน (world unit/วินาที)</summary>
        public float Speed = 3.5f;

        // ขอบเขตโลก (top-down lite) — clamp ไม่ให้เดินหลุดกรอบ
        public float MinX = -11f, MaxX = 11f, MinY = -3.5f, MaxY = 5f;

        public PlayerMovementSystem(PlayerInputService input, GameStateProvider stateProvider)
        {
            _input = input;
            _player = stateProvider.GetPlayer();
        }

        public void Tick(float deltaSeconds)
        {
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
