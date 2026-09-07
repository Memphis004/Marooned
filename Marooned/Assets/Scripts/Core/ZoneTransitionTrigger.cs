using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
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
    /// หมายเหตุ: ยังไม่เช็ค ConnectedLocationIds — การเดินเท้าข้ามเส้นแบ่งโซน
    /// ผ่านได้อิสระตามเลย์เอาต์แผนที่รวม (ต่างจาก MCP move_to_location ที่ตรวจ
    /// graph) ถ้าอนาคตต้องกัน "กระโดดโซน" ให้เช็คกับ LocationDef.ConnectedLocationIds
    /// ตรงนี้
    /// </summary>
    public class ZoneTransitionTrigger : MonoBehaviour
    {
        [Header("id โซนปลายทาง — ต้องตรงกับ LocationDefs (เช่น jungle_edge)")]
        [SerializeField] private string targetLocationId;

        [Header("tag ของ GameObject ผู้เล่น")]
        [SerializeField] private string playerTag = "Player";

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
