using System.Text;
using Marooned.Shared;
using Spine.Unity;
using UnityEngine;

namespace Marooned.Core.Visual
{
    /// <summary>
    /// Spine backend — คุม AnimationState ของ SkeletonAnimation
    /// Controller ตัวเดียวใช้ร่วมกันทั้ง Elena และ Derek (ผ่าน prefab variant)
    ///
    /// ชื่อ animation ตรงนี้อ่านจาก SkeletonData จริง (Assets/Gamelauncher Studio/
    /// {Elena,Derek}/Source/*.json) ไม่ได้เดา — ตอนเขียน inspect ได้ 29 ชื่อเหมือนกัน
    /// ทั้งสองตัว: Idle, Walking, Running, Talking, Die, Die2, Hurt, Crouch,
    /// Jump*, Kick, Laugh, Slash, Sneak, Stab, Sheathing/Unsheathing knife ฯลฯ
    /// (Awake ยัง log รายชื่อทั้งหมด 1 ครั้งเพื่อ cross-check ว่า skeleton ไหนต่างจากนี้)
    ///
    /// แนวทางแก้ตาม spec ที่ให้มา: ใช้ enum NpcActivityState จริงของโปรเจกต์
    /// {Idle, Gathering, Resting, Traveling, Talking} — ไม่มี Walking/Dead ใน enum
    /// จึง map Traveling/Gathering→"Walking" และไม่มี auto-Dead (การตายคือ IsAlive=false)
    /// </summary>
    [RequireComponent(typeof(SkeletonAnimation))]
    public class SpineVisualController : MonoBehaviour, IChibiVisual
    {
        private SkeletonAnimation _skeleton;
        private bool _loggedAnimations;

        public Transform Transform => transform;

        /// <summary>ชื่อ animation ที่กำลังเล่นอยู่ track 0 (ใช้สำหรับ verification)</summary>
        public string CurrentAnimationName => _skeleton != null
            ? _skeleton.AnimationState?.GetTrack(0)?.Animation?.Name
            : null;

        private void Awake()
        {
            _skeleton = GetComponent<SkeletonAnimation>();
            if (_skeleton == null)
            {
                Debug.LogError($"[SpineVisualController] '{name}' ไม่มี SkeletonAnimation component");
                return;
            }

            LogAnimationNamesOnce();
        }

        /// <summary>
        /// Log รายชื่อ animation ทั้งหมดจาก SkeletonData จริง 1 ครั้งต่อ instance
        /// เพื่อ cross-check ว่า Elena กับ Derek มี set เหมือนกันหรือต่างกันตรงไหน
        /// </summary>
        private void LogAnimationNamesOnce()
        {
            if (_loggedAnimations) return;
            var data = _skeleton.skeletonDataAsset != null
                ? _skeleton.skeletonDataAsset.GetSkeletonData(true)
                : null;
            if (data == null)
            {
                Debug.LogWarning($"[SpineVisualController] '{name}' โหลด SkeletonData ไม่สำเร็จ — เช็ค SkeletonDataAsset/Atlas");
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"[SpineVisualController] '{name}' SkeletonData '{data.Name}' animations ({data.Animations.Count}): ");
            for (int i = 0; i < data.Animations.Count; i++)
                sb.Append(data.Animations.Items[i].Name).Append(i < data.Animations.Count - 1 ? ", " : "");
            Debug.Log(sb.ToString());
            _loggedAnimations = true;
        }

        public void Bind(NpcActivityState state)
        {
            if (_skeleton == null) return;

            // Map ตาม enum จริงของโปรเจกต์ + ชื่อ animation จริงของ Elena/Derek
            var target = state switch
            {
                NpcActivityState.Traveling => "Walking",
                NpcActivityState.Gathering => "Walking", // ยังไม่มี animation เฉพาะ รอ schedule system
                NpcActivityState.Talking => "Talking",
                NpcActivityState.Resting => "Idle",
                _ => "Idle",
            };

            PlayIfChanged(target);
        }

        private void PlayIfChanged(string target)
        {
            var current = _skeleton.AnimationState.GetTrack(0);
            if (current != null && current.Animation != null && current.Animation.Name == target)
                return; // กัน restart ทุกครั้งที่ Bind ถูกเรียกซ้ำด้วย state เดิม

            // กันเรียกชื่อที่ไม่มีใน skeleton — fallback ไป Idle พร้อม log ชัดเจน
            if (_skeleton.skeletonDataAsset.GetSkeletonData(true).FindAnimation(target) == null)
            {
                Debug.LogWarning($"[SpineVisualController] '{name}' ไม่มี animation '{target}' — fallback เป็น Idle");
                target = "Idle";
            }

            _skeleton.AnimationState.SetAnimation(0, target, loop: true);
        }

        public void SetFacing(bool facingRight)
        {
            if (_skeleton?.Skeleton == null) return;
            // Spine flip ผ่าน Skeleton.ScaleX ไม่แตะ Transform (กันพัง mesh bounds/sorting)
            _skeleton.Skeleton.ScaleX = facingRight ? 1f : -1f;
        }

        /// <summary>IChibiVisual: Spine characters (Elena/Derek) ไม่มี animation pickup
        /// ตรงชื่อ — player เป็น Student 1 (Animator) ตัวเดียวที่เก็บของ จึงเป็น no-op</summary>
        public void PlayPickup()
        {
            Debug.Log($"[SpineVisualController] '{name}' ไม่มี animation pickup — ข้าม");
        }

        /// <summary>
        /// IChibiVisual (Phase 4 Step 8): one-shot action — ใช้ชื่อ animation จริงของ
        /// skeleton (เช่น "Slash", "Stab") หรือ alias เชิง semantics ที่ Elena/Derek มีจริง:
        /// "attack" → "Slash" (ชื่อจากการ inspect SkeletonData ทั้งสองตัว)
        /// ไม่มี animation นั้น → log + ข้าม (ไม่ fallback Idle เพราะ one-shot ควรเงียบ)
        /// </summary>
        public void PlayAction(string actionName)
        {
            if (_skeleton == null) return;

            var target = actionName switch
            {
                "attack" => "Slash",
                _ => actionName,
            };

            if (_skeleton.skeletonDataAsset.GetSkeletonData(true).FindAnimation(target) == null)
            {
                Debug.Log($"[SpineVisualController] '{name}' ไม่มี animation '{target}' — ข้าม PlayAction");
                return;
            }

            _skeleton.AnimationState.SetAnimation(0, target, loop: false);
        }
    }
}
