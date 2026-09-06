using System.Collections.Generic;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B — Wrapper เฉพาะสำหรับ asset "Generic Cute 2D - 001 Student 1"
    /// (Unity Animator/PSB skeletal) — แยกจาก ChibiAnimatedRenderer (frame-swap
    /// paperdoll เดิม) เพื่อพิสูจน์ Lab B flow ก่อน
    ///
    /// หมายเหตุสำคัญจากการตรวจ asset จริง: Basic.controller ของ asset นี้
    /// **ไม่มี Animator Parameter เลย** (m_AnimatorParameters: []) แต่มี state
    /// ชื่อ idle / walk / interact / run / dig ฯลฯ ดังนั้นการ map จึงใช้
    /// Animator.Play("stateName") แทนการ set parameter int "State"
    ///
    /// เป็น View แบบ passive — รับ NpcState ผ่าน Bind() ไม่ Resolve ระบบใดๆ เอง
    /// (การ DI ทั้งหมดอยู่ที่ ChibiSpawnerView ผู้เดียว)
    /// </summary>
    public class GenericCuteVisualController : MonoBehaviour
    {
        // Map NpcState.Activity → ชื่อ state ใน Basic.controller (state จริงของ asset)
        // - Idle/Resting → "idle"
        // - Traveling/Gathering → "walk" (Gathering ยังไม่มี animation เฉพาะ รอ schedule system)
        // - Talking → "interact"
        private static readonly Dictionary<NpcActivityState, string> ActivityToAnimState = new()
        {
            { NpcActivityState.Idle, "idle" },
            { NpcActivityState.Resting, "idle" },
            { NpcActivityState.Traveling, "walk" },
            { NpcActivityState.Gathering, "walk" },
            { NpcActivityState.Talking, "interact" },
        };

        private Animator _animator;
        private NpcState _npc;
        private NpcActivityState _lastAppliedActivity = (NpcActivityState)(-1);

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                Debug.LogError($"[GenericCuteVisualController] '{name}' ไม่มี Animator component");
        }

        /// <summary>ผูก NpcState (ground truth จาก NpcDirectorSystem) เข้ากับ visual นี้</summary>
        public void Bind(NpcState npc)
        {
            _npc = npc;
            _lastAppliedActivity = (NpcActivityState)(-1); // บังคับ apply ใหม่
            ApplyActivity();
        }

        /// <summary>
        /// Map NpcState.Activity ปัจจุบัน → animation state ของ Basic.controller
        /// เรียกเมื่อ Bind() เท่านั้น (ไม่ polling ใน Update) — เมื่ออนาคตมี
        /// NpcActivityChangedMessage จะเปลี่ยนมา subscribe แทน
        /// </summary>
        private void ApplyActivity()
        {
            if (_npc == null || _animator == null) return;
            if (_npc.Activity == _lastAppliedActivity) return;
            _lastAppliedActivity = _npc.Activity;

            if (!ActivityToAnimState.TryGetValue(_npc.Activity, out var stateName)) return;

            // เช็คว่า state นี้มีอยู่จริงใน controller ก่อน Play กัน warning รัวๆ
            if (_animator.runtimeAnimatorController != null &&
                _animator.HasState(0, Animator.StringToHash(stateName)))
            {
                _animator.Play(stateName, 0, 0f);
            }
            else if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[GenericCuteVisualController] '{name}' Animator ยังไม่มี Controller — ผูก Basic.controller ใน prefab ก่อน");
            }
        }
    }
}
