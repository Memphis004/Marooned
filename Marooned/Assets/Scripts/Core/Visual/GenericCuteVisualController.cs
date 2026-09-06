using System.Collections.Generic;
using Marooned.Core.Visual;
using Marooned.Shared;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B — Wrapper เฉพาะสำหรับ asset "Generic Cute 2D - 001 Student 1"
    /// (Unity Animator/PSB skeletal) — backend เริ่มต้นของ ChibiSpawnerView
    ///
    /// Phase 2: implement IChibiVisual เพื่อให้ spawner สลับ backend ได้
    /// (Spine backend ดูที่ SpineVisualController)
    ///
    /// หมายเหตุสำคัญจากการตรวจ asset จริง: Basic.controller ของ asset นี้
    /// **ไม่มี Animator Parameter เลย** (m_AnimatorParameters: []) แต่มี state
    /// ชื่อ idle / walk / interact / run ฯลฯ ดังนั้นการ map จึงใช้
    /// Animator.Play("stateName") แทนการ set parameter
    ///
    /// เป็น View แบบ passive — ไม่ Resolve ระบบใดๆ เอง
    /// (การ DI ทั้งหมดอยู่ที่ ChibiSpawnerView ผู้เดียว)
    /// </summary>
    public class GenericCuteVisualController : MonoBehaviour, IChibiVisual
    {
        // Map NpcActivityState → ชื่อ state ใน Basic.controller (state จริงของ asset)
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
        private NpcActivityState _lastAppliedActivity = (NpcActivityState)(-1);

        public Transform Transform => transform;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                Debug.LogError($"[GenericCuteVisualController] '{name}' ไม่มี Animator component");
        }

        /// <summary>IChibiVisual: apply animation ตาม activity (เล่นซ้ำเฉพาะเมื่อ state เปลี่ยน)</summary>
        public void Bind(NpcActivityState state)
        {
            _lastAppliedActivity = (NpcActivityState)(-1); // บังคับ apply
            ApplyActivity(state);
        }

        /// <summary>IChibiVisual: หันซ้าย/ขวาด้วยการ flip localScale.x</summary>
        public void SetFacing(bool facingRight)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingRight ? 1f : -1f);
            transform.localScale = scale;
        }

        /// <summary>
        /// Map activity → animation state ของ Basic.controller
        /// เรียกเมื่อ Bind() เท่านั้น (ไม่ polling ใน Update) — เมื่ออนาคตมี
        /// NpcActivityChangedMessage จะเปลี่ยนมา subscribe แทน
        /// </summary>
        private void ApplyActivity(NpcActivityState activity)
        {
            if (_animator == null) return;
            if (activity == _lastAppliedActivity) return;
            _lastAppliedActivity = activity;

            if (!ActivityToAnimState.TryGetValue(activity, out var stateName)) return;

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
