using Marooned.Core.Visual;
using Marooned.Shared;
using Marooned.Systems;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab C Phase 2 Step 3 — View ต่อ 1 chibi NPC: sync transform/animation กับ
    /// NpcState (ground truth) ทุกเฟรม
    ///
    /// MVP Lite (บทเรียน Lab 13): การเดินเป็น continuous per-frame state จึงอ่าน
    /// state ตรงแบบ direct method call ไม่ใช้ MessagePipe ต่อเฟรม — MessagePipe
    /// (NpcLocationChangedMessage) ใช้เฉพาะ discrete event ข้ามโซนที่ flow เดิม
    /// ของ ChibiSpawnerView จัดการอยู่แล้ว
    ///
    /// ติดตั้งโดย ChibiSpawnerView ตอน SpawnChibi ผ่าน Init(npcId, npcDirector)
    /// แบบ direct call — ไม่ Resolve container เอง (View แบบ passive)
    ///
    /// Information Hiding: อ่านเฉพาะสิ่งที่ "ตาเห็นอยู่แล้ว" — Position/Activity/
    /// IsAlive ห้าม render หรือ expose Inventory/Fear/Curiosity เด็ดขาด
    /// (สอดคล้อง NpcObservableView: position อยู่โซนเดียวกันย่อมมองเห็นได้)
    /// </summary>
    public class NpcCharacterView : MonoBehaviour
    {
        private string _npcId;
        private NpcDirectorSystem _npcDirector;
        private IChibiVisual _visual;

        /// <summary>id ของ NPC ที่ view นี้ผูกอยู่ (อ่านอย่างเดียว — ใช้ตรวจสอบ/เทส)</summary>
        public string NpcId => _npcId;

        // facing ล่าสุดที่ apply แล้ว (กัน flip ซ้ำทุกเฟรม)
        private bool _lastFacingRight = true;
        private bool _facingInitialized;

        /// <summary>
        /// เรียกโดย ChibiSpawnerView ทันทีหลัง Instantiate — wiring แบบ direct call
        /// (MVP Lite: ไม่ resolve container / ไม่ลาก reference ใน Inspector)
        /// </summary>
        public void Init(string npcId, NpcDirectorSystem npcDirector)
        {
            _npcId = npcId;
            _npcDirector = npcDirector;
        }

        /// <summary>ทุกเฟรม: อ่าน NpcState ตรง → transform + facing + animation</summary>
        private void Update()
        {
            if (string.IsNullOrEmpty(_npcId) || _npcDirector == null) return;
            if (!_npcDirector.Npcs.TryGetValue(_npcId, out var npc)) return; // ถูกลบออกจากรอบแล้ว

            if (_visual == null)
                _visual = GetComponentInChildren<IChibiVisual>();

            // --- position (ตาเห็นอยู่แล้ว — ground truth PositionX/Y) ---
            transform.position = new Vector3(npc.PositionX, npc.PositionY, 0f);

            // --- facing: flip จากเครื่องหมาย delta แนวนอนของการเคลื่อนที่ ---
            var dx = npc.TargetX - npc.PositionX;
            if (Mathf.Abs(dx) > 0.02f)
            {
                var facingRight = dx > 0f;
                if (!_facingInitialized || facingRight != _lastFacingRight)
                {
                    _visual?.SetFacing(facingRight);
                    _lastFacingRight = facingRight;
                    _facingInitialized = true;
                }
            }

            // --- animation: Activity → IChibiVisual.Bind (Traveling→walk, Idle→idle) ---
            _visual?.Bind(npc.Activity);
        }
    }
}
