using Marooned.Shared;
using UnityEngine;

namespace Marooned.Core.Visual
{
    /// <summary>
    /// Interface กลางของ visual layer — ChibiSpawnerView ไม่ต้องรู้ว่า
    /// backend ข้างใต้เป็น Animator / Spine / paperdoll ในอนาคต
    /// </summary>
    public interface IChibiVisual
    {
        void Bind(NpcActivityState state);
        void SetFacing(bool facingRight);
        Transform Transform { get; }

        /// <summary>Lab B Phase 3: เล่น animation "เก็บของ" แบบ one-shot (ถ้า asset มี state นั้น)</summary>
        void PlayPickup();
    }
}
