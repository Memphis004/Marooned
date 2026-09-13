using Marooned.Systems;
using cfg.game;
using TMPro;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab C Phase 1 (Tool-Gathering Ready) — component ของ harvestable node 1 ต้น
    /// (ติดให้โดย BiomeScatterView ตอน instantiate prefab ที่มาจาก
    /// BiomePrefabSet.harvestablePrefabs) — ถือ state การเก็บเกี่ยวของ "ต้นนี้":
    /// durability คงเหลือ / requiredTool / yield / regrow (ค่ามาจาก
    /// HarvestableNodeDef.csv ผ่าน LubanDataService)
    ///
    /// แยกชัดจาก decorative props (เดินผ่านได้ ไม่มี component นี้) ตาม design
    /// "Tool-Gathering Ready" — การ harvest จริงทำโดย NodeHarvestSystem (plain C#
    /// เรียก TryHarvest ผ่าน direct method call ตามบทเรียน Lab 13: ของที่ต้องเห็นผล
    /// ทันทีใน process เดียวกันไม่ publish ข้าม message bus)
    ///
    /// MonoBehaviour นี้เป็น "state holder" เท่านั้น — ไม่ publish เอง ไม่รู้จัก
    /// VContainer (ตัวอ้าง NodeHarvestSystem ส่งเข้ามาตอน Init)
    /// </summary>
    public class HarvestableNodeComponent : MonoBehaviour
    {
        /// <summary>id ใน HarvestableNodeDef.csv (เช่น tree_wood)</summary>
        public string NodeId { get; private set; }

        /// <summary>durability คงเหลือของต้นนี้ (หมด = depleted)</summary>
        public int RemainingDurability { get; private set; }

        /// <summary>ตอนนี้ node กลับมาพร้อมเก็บใหม่หรือยัง (regrow)</summary>
        public bool IsRegrowing { get; private set; }

        private HarvestableNodeDef _def;
        private NodeHarvestSystem _harvestSystem;
        private float _regrowTimer;
        private TextMeshPro _label;

        /// <summary>เรียกโดย BiomeScatterView หลัง AddComponent — อ่านค่าจาก def ตาราง</summary>
        public void Init(string nodeId, HarvestableNodeDef nodeDef, NodeHarvestSystem harvestSystem)
        {
            NodeId = nodeId;
            _def = nodeDef;
            _harvestSystem = harvestSystem;
            RemainingDurability = nodeDef.Durability;
            IsRegrowing = false;
            _regrowTimer = 0f;

            CreateLabel();
        }

        /// <summary>
        /// พยายามเก็บเกี่ยวด้วย tool ที่ระบุ — เช็ค requiredToolCardId กับ toolItemId
        /// (ค่าว่าง = เก็บมือเปล่าได้) แล้วค่อยลด durability
        /// คืน (true, reason ว่าง) เมื่อสำเร็จ — inventory/publish เป็นหน้าที่ของ
        /// NodeHarvestSystem (component ไม่แตะข้อมูลส่วนกลาง)
        /// </summary>
        public bool TryHarvest(string toolItemId, out string failureReason)
        {
            failureReason = null;

            if (IsRegrowing)
            {
                failureReason = "regrowing";
                return false;
            }
            if (_def == null)
            {
                failureReason = "unknown_node";
                return false;
            }

            // requiredToolCardId ว่าง = ใช้มือเปล่าได้ (ดู comment ใน HarvestableNodeDef.csv)
            var requiredTool = _def.RequiredToolCardId;
            if (!string.IsNullOrEmpty(requiredTool) && requiredTool != toolItemId)
            {
                failureReason = "wrong_tool";
                return false;
            }

            RemainingDurability--;
            if (RemainingDurability <= 0)
            {
                RemainingDurability = 0;
                StartRegrowIfAny();
            }
            return true;
        }

        /// <summary>node หมด durability → เริ่มนับ regrow (regrowTime = 0 = หายถาวร)</summary>
        private void StartRegrowIfAny()
        {
            if (_def.RegrowTime <= 0)
            {
                // หายถาวร — Destroy ทันที (View อนุญาตให้ component ตัดสินใจของ GameObject ตัวเอง)
                Destroy(gameObject);
                return;
            }

            IsRegrowing = true;
            _regrowTimer = _def.RegrowTime;
            SetLabelDimmed(true);
        }

        private void Update()
        {
            if (!IsRegrowing) return;

            _regrowTimer -= Time.deltaTime;
            if (_regrowTimer <= 0f)
            {
                IsRegrowing = false;
                RemainingDurability = _def.Durability;
                SetLabelDimmed(false);
            }
        }

        /// <summary>ป้ายชื่อ + สถานะ durability เหนือต้น (placeholder visual เหมือนไอเท็ม WorldItemSystem)</summary>
        private void CreateLabel()
        {
            if (_def == null) return;

            var labelGo = new GameObject("label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            _label = labelGo.AddComponent<TextMeshPro>();
            _label.font = WorldItemSystem.LoadLabelFont();
            _label.fontSize = 6;
            _label.alignment = TextAlignmentOptions.Center;
            RefreshLabelText();
        }

        private void RefreshLabelText()
        {
            if (_label == null || _def == null) return;
            var toolHint = string.IsNullOrEmpty(_def.RequiredToolCardId) ? "มือเปล่า" : _def.RequiredToolCardId;
            _label.text = $"{_def.DisplayName} [{RemainingDurability}/{_def.Durability}] ({toolHint})";
        }

        private void SetLabelDimmed(bool dimmed)
        {
            if (_label == null) return;
            var color = _label.color;
            color.a = dimmed ? 0.35f : 1f;
            _label.color = color;
            if (!dimmed) RefreshLabelText();
            else _label.text = $"{_def.DisplayName} (กำลังงอกใหม่…)";
        }
    }
}
