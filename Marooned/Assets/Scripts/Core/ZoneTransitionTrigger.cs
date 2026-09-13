using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B Phase 6 — Trigger เปลี่ยนโซนแบบเดินเท้า (Walking Sandbox): เมื่อ Player
    /// เดินเข้าพื้นที่ของ trigger นี้ ให้ตั้ง PlayerSurvivalState.CurrentLocationId =
    /// targetLocationId แล้ว publish PlayerLocationChangedMessage — จุด set + publish
    /// เดียวกับที่ explore_location (ExplorationSystem) และ move_to_location
    /// (McpRequestHandlers) ใช้ ดังนั้น subscriber เดิม (ChibiSpawnerView →
    /// ReconcileChibis, WorldItemSystem → respawn loot) ทำงานเองทันทีโดยไม่ต้องแก้อะไร
    ///
    /// ติดตั้งบน GameObject ขอบเขตโซน: ต้องอยู่ใต้ GameLifetimeScope (เช่น
    /// GameLifetimeScope/Zones/Trigger_BeachToJungle) เพื่อให้ resolve ผ่าน
    /// GetComponentInParent ได้ + BoxCollider2D (Is Trigger ✓)
    ///
    /// Lab B Phase 6.1 — Trigger Markers: Start() สร้าง placeholder visual ให้เห็น
    /// ขอบเขต trigger ใน Game View ด้วย (sprite โปร่งใส alpha ~0.25 คลุม BoxCollider2D
    /// + text label \"→ {DisplayName ของ targetLocationId}\" ลอยขอบบน) และ
    /// OnDrawGizmos() วาด wire cube สีเขียวใน Scene View ตอนไม่ Play — ปรับสีต่อ
    /// trigger ได้ผ่าน markerColor (เช่น โซนถ้ำสีเทา, ป่าสีเขียว)
    ///
    /// หมายเหตุ: ยังไม่เช็ค ConnectedLocationIds — การเดินเท้าข้ามเส้นแบ่งโซน
    /// ผ่านได้อิสระตามเลย์เอาต์แผนที่รวม (ต่างจาก MCP move_to_location ที่ตรวจ
    /// graph) ถ้าอนาคตต้องกัน \"กระโดดโซน\" ให้เช็คกับ LocationDef.ConnectedLocationIds
    /// ตรงนี้
    /// </summary>
    public class ZoneTransitionTrigger : MonoBehaviour
    {
        [Header("id โซนปลายทาง — ต้องตรงกับ LocationDefs (เช่น jungle_edge)")]
        [SerializeField] private string targetLocationId;

        [Header("tag ของ GameObject ผู้เล่น")]
        [SerializeField] private string playerTag = "Player";

        [Header("สีของ placeholder marker (เช่น ถ้ำสีเทา, ป่าสีเขียว)")]
        [SerializeField] private Color markerColor = new Color(0.2f, 0.9f, 0.3f, 0.25f); // เขียวโปร่งใส default

        private GameStateProvider _stateProvider;
        private IPublisher<PlayerLocationChangedMessage> _playerLocationPublisher;

        private void Start()
        {
            // VContainer: container build ใน LifetimeScope.Awake → resolve ใน Start
            // (pattern เดียวกับ RoundInitializer/PlayerCharacterView)
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[ZoneTransitionTrigger] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return;
            }

            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _playerLocationPublisher = scope.Container.Resolve<IPublisher<PlayerLocationChangedMessage>>();

            // กันพิมพ์ id ผิดเงียบๆ — เตือนตั้งแต่ Start ถ้า id ไม่มีในตาราง location
            if (!string.IsNullOrEmpty(targetLocationId)
                && !scope.Container.Resolve<LubanDataService>().LocationDefs.ContainsKey(targetLocationId))
            {
                Debug.LogError($"[ZoneTransitionTrigger] targetLocationId \"{targetLocationId}\" ไม่มีใน LocationDefs — เช็คสะกดให้ตรงกับ LubanDataService");
            }

            CreateMarkerVisual();
        }

        /// <summary>
        /// Lab B Phase 6.1 — สร้าง placeholder visual ของขอบเขต trigger เป็นลูกของ
        /// ตัวมันเอง: sprite โปร่งใส (alpha ของ markerColor) คลุม BoxCollider2D +
        /// Text label \"→ {DisplayName}\" ลอยขอบบน (สร้าง texture ใน code ไม่พึ่ง asset)
        /// </summary>
        private void CreateMarkerVisual()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null)
            {
                Debug.LogWarning($"[ZoneTransitionTrigger] {name} ไม่มี BoxCollider2D — ข้ามการสร้าง marker visual", this);
                return;
            }

            var size = box.size;
            var center = (Vector2)box.offset;

            // --- พื้นทึบโปร่งใส (alpha ~0.25 จาก markerColor) ---
            var surfaceGo = new GameObject("ZoneMarker_Surface");
            surfaceGo.transform.SetParent(transform, false);
            surfaceGo.transform.localPosition = center;
            var surface = surfaceGo.AddComponent<SpriteRenderer>();
            surface.sprite = CreateSolidSprite(markerColor);
            surface.color = markerColor;
            surface.sortingOrder = 0; // ใต้ไอเท็ม (sortingOrder 1)

            // scale: sprite เป็น 1x1 world unit — scale ให้เท่ากับขนาด collider
            surfaceGo.transform.localScale = new Vector3(size.x, size.y, 1f);

            // --- label \"→ {DisplayName}\" ลอยขอบบน ---
            var displayName = ResolveTargetDisplayName();
            var labelGo = new GameObject("ZoneMarker_Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(center.x, center.y + size.y * 0.5f + 0.4f, 0f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.text = $"→ {displayName}";
            label.font = WorldItemSystem.LoadLabelFont();
            label.fontSize = 6;
            label.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>ชื่อโซนปลายทางจาก LocationDefs.DisplayName — fallback เป็น targetLocationId</summary>
        private string ResolveTargetDisplayName()
        {
            if (string.IsNullOrEmpty(targetLocationId)) return "?";
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null) return targetLocationId;
            var data = scope.Container.Resolve<LubanDataService>();
            return data.LocationDefs.TryGetValue(targetLocationId, out var def) && !string.IsNullOrEmpty(def.DisplayName)
                ? def.DisplayName
                : targetLocationId;
        }

        /// <summary>sprite 1x1 สีทึบ สำหรับ marker visual (สร้าง texture ใน code)</summary>
        private static Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white); // สีจริงมาจาก SpriteRenderer.color — texture ขาวไว้ tint ได้
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Lab B Phase 6.1 — วาด wire cube สีเขียวรอบ trigger ใน Scene View (ตอนไม่
        /// Play เท่านั้น) ให้เห็นขอบเขตโซนตอนจัด scene
        /// </summary>
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(markerColor.r, markerColor.g, markerColor.b, 0.9f); // ทึบขึ้นกว่า marker ในเกม
            var worldCenter = transform.TransformPoint(box.offset);
            var worldSize = Vector3.Scale(box.size, transform.lossyScale);
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (_stateProvider == null) return;

            if (string.IsNullOrEmpty(targetLocationId))
            {
                Debug.LogWarning($"[ZoneTransitionTrigger] {name} ยังไม่ได้ตั้ง targetLocationId — ข้าม");
                return;
            }

            var player = _stateProvider.GetPlayer();
            if (player.CurrentLocationId == targetLocationId) return; // อยู่โซนนี้อยู่แล้ว

            // set ตรง + publish เอง (pattern เดียวกับ MoveToLocationHandler) —
            // อย่าเรียก NpcDirectorSystem.MoveNpc เพราะนั่นสำหรับ NPC
            var oldLocationId = player.CurrentLocationId;
            player.CurrentLocationId = targetLocationId;

            _playerLocationPublisher.Publish(new PlayerLocationChangedMessage
            {
                OldLocationId = oldLocationId,
                NewLocationId = targetLocationId,
            });

            Debug.Log($"[ZoneTransitionTrigger] Player เปลี่ยนโซน: {oldLocationId} → {targetLocationId}");
        }
    }
}