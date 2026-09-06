using Marooned.Core.Visual;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Wrapper สำหรับตระกูล asset "Generic Cute 2D" (Unity Animator/PSB skeletal)
    /// — ใช้ทั้ง Student 1 (player), Wizard, College Student (NPC)
    ///
    /// สำคัญ (Lab B Phase 3): แต่ละตัวในตระกูล**ไม่ได้แชร์ state names**
    ///   - Student 1 (Basic.controller): idle / walk / interact / pick up (ตัวพิมพ์เล็ก)
    ///   - Wizard (Wizard Demo.controller): Idle / Run / Attack / ... 
    ///   - CollegeStudent (AnimationDemo.controller): Idle / Run / Attack / ...
    /// จึงให้ชื่อ state เป็น SerializeField ต่อ prefab (default = ของ Student 1)
    /// — ห้ามเดา ให้ inspect จาก .controller ของแต่ละ asset จริง
    ///
    /// หมายเหตุ: controller ของ Student 1 ไม่มี Animator Parameter เลย
    /// (m_AnimatorParameters: []) จึงใช้ Animator.Play("stateName") ตรงๆ
    ///
    /// เป็น View แบบ passive — ไม่ Resolve ระบบใดๆ เอง
    /// </summary>
    public class GenericCuteVisualController : MonoBehaviour, IChibiVisual
    {
        [Header("State names ของ AnimatorController ที่ prefab นี้ใช้ (inspect จาก .controller)")]
        [SerializeField] private string idleAnim = "idle";
        [SerializeField] private string walkAnim = "walk";
        [SerializeField] private string interactAnim = "interact";
        [SerializeField] private string pickupAnim = "pick up";

        private Animator _animator;
        private string _currentAnim;

        public Transform Transform => transform;

        /// <summary>ชื่อ state ที่กำลังเล่น (สำหรับ verification)</summary>
        public string CurrentAnim => _currentAnim;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                Debug.LogError($"[GenericCuteVisualController] '{name}' ไม่มี Animator component");
        }

        /// <summary>IChibiVisual: apply animation ตาม activity (เล่นซ้ำเฉพาะเมื่อ state เปลี่ยน)</summary>
        public void Bind(NpcActivityState state)
        {
            var target = state switch
            {
                NpcActivityState.Traveling => walkAnim,
                NpcActivityState.Gathering => walkAnim, // ยังไม่มี animation เฉพาะ รอ schedule system
                NpcActivityState.Talking => interactAnim,
                _ => idleAnim, // Idle / Resting
            };
            PlayIfAvailable(target);
        }

        /// <summary>IChibiVisual: เล่น animation เก็บของแบบ one-shot (ถ้า prefab นี้มี state)</summary>
        public void PlayPickup() => PlayIfAvailable(pickupAnim);

        /// <summary>IChibiVisual: หันซ้าย/ขวาด้วยการ flip localScale.x</summary>
        public void SetFacing(bool facingRight)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingRight ? 1f : -1f);
            transform.localScale = scale;
        }

        /// <summary>Play โดยเช็คว่า state มีจริงใน controller + ข้ามถ้าเล่นอยู่แล้ว (กัน restart)</summary>
        private void PlayIfAvailable(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;
            if (_currentAnim == stateName) return;

            if (_animator.runtimeAnimatorController != null &&
                _animator.HasState(0, Animator.StringToHash(stateName)))
            {
                _animator.Play(stateName, 0, 0f);
                _currentAnim = stateName;
            }
            else if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[GenericCuteVisualController] '{name}' Animator ยังไม่มี Controller — ผูก controller ใน prefab ก่อน");
            }
            // state ไม่มีใน controller: เงียบไว้ (prefab ต่างตระกูลไม่มีบาง state เช่น interact)
        }
    }
}
