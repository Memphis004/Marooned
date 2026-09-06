using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B — Visual layer สำหรับ Chibi (MVP Lite: View แบบ passive)
    /// คอย spawn/despawn chibi ของ NPC ที่อยู่ location เดียวกับผู้เล่น
    ///
    /// Architectural rules:
    ///  - VContainer: Resolve ทุก dependency จาก GetComponentInParent&lt;GameLifetimeScope&gt;()
    ///    ใน Start() — ห้าม [Inject]/ลาก System ใส่ Inspector
    ///  - MessagePipe: subscribe PlayerLocationChangedMessage / NpcLocationChangedMessage
    ///    — ห้าม polling ตำแหน่งใน Update()
    ///  - ส่วนเดียวที่ใช้ SerializeField คือ chibiPrefab (asset ล้วนๆ ไม่ใช่ System)
    ///
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (เช่น ChibiSystem)
    /// </summary>
    public class ChibiSpawnerView : MonoBehaviour
    {
        [Header("Prefab ล้วนๆ — ลาก '001 Student 1 Character' ใส่ตรงนี้")]
        [SerializeField] private GameObject chibiPrefab;

        private NpcDirectorSystem _npcDirector;
        private GameStateProvider _stateProvider;
        private LubanDataService _data;

        // Key: npcId, Value: chibi GameObject ที่ spawn อยู่บนจอ
        private readonly Dictionary<string, GameObject> _activeChibis = new();
        private readonly List<IDisposable> _subscriptions = new();
        private bool _resolved;

        private void Start()
        {
            // VContainer: container ถูก build ใน LifetimeScope.Awake จึง resolve ใน Start
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[ChibiSpawnerView] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return;
            }

            _npcDirector = scope.Container.Resolve<NpcDirectorSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _data = scope.Container.Resolve<LubanDataService>();
            _resolved = true;

            // MessagePipe: subscribe event การเปลี่ยน location (in-process bus)
            // — เก็บ IDisposable ไว้ dispose ตอน OnDestroy กัน leak
            var playerLocationSubscriber = scope.Container.Resolve<ISubscriber<PlayerLocationChangedMessage>>();
            var npcLocationSubscriber = scope.Container.Resolve<ISubscriber<NpcLocationChangedMessage>>();
            _subscriptions.Add(playerLocationSubscriber.Subscribe(_ => ReconcileChibis()));
            _subscriptions.Add(npcLocationSubscriber.Subscribe(_ => ReconcileChibis()));

            // Start() ของ GameObject อื่น (RoundInitializer บน GameManager) อาจยังไม่รัน
            // จึงเลื่อน sync ครั้งแรกไป 1 เฟรม หลัง Start ทุกตัวเสร็จ — เป็น one-shot sync
            // ไม่ใช่ polling (จากนี้ spawn/despawn ตอบสนองต่อ message เท่านั้น)
            SyncInitialOnceAsync().Forget();
        }

        private async UniTaskVoid SyncInitialOnceAsync()
        {
            await UniTask.NextFrame();
            if (this != null && _resolved) ReconcileChibis();
        }

        /// <summary>
        /// จัด chibi ให้ตรงกับ state จริง: NPC ที่ยังมีชีวิตและอยู่ location เดียวกับ
        /// ผู้เล่น → ต้องมี chibi; NPC ที่ตาย/ย้ายไป location อื่น → despawn
        /// เรียกเฉพาะเมื่อได้รับ message เท่านั้น
        /// </summary>
        private void ReconcileChibis()
        {
            if (!_resolved) return;

            var playerLocation = _stateProvider.Player.CurrentLocationId;

            foreach (var npc in _npcDirector.Npcs.Values)
            {
                bool shouldBeVisible = npc.IsAlive && npc.CurrentLocationId == playerLocation;
                if (shouldBeVisible && !_activeChibis.ContainsKey(npc.Id))
                    SpawnChibi(npc);
                else if (!shouldBeVisible && _activeChibis.ContainsKey(npc.Id))
                    DespawnChibi(npc.Id);
            }

            // เก็บ chibi ค้างของ NPC ที่ถูกลบออกจากระบบแล้ว (เผื่อรอบใหม่)
            var staleIds = new List<string>();
            foreach (var npcId in _activeChibis.Keys)
                if (!_npcDirector.Npcs.ContainsKey(npcId)) staleIds.Add(npcId);
            foreach (var npcId in staleIds) DespawnChibi(npcId);
        }

        private void SpawnChibi(NpcState npc)
        {
            if (chibiPrefab == null)
            {
                Debug.LogWarning("[ChibiSpawnerView] chibiPrefab ยังไม่ถูก assign ใน Inspector — ข้ามการ spawn");
                return;
            }

            var chibi = Instantiate(chibiPrefab, transform);

            // ผูก NpcState ให้ visual controller map เป็น animation state
            var visual = chibi.GetComponent<GenericCuteVisualController>();
            if (visual != null) visual.Bind(npc);

            // วางตำแหน่งตาม WorldX/WorldY ของ location + ไล่ offset กัน chibi ซ้อนกัน
            var position = Vector3.zero;
            if (_data.LocationDefs.TryGetValue(npc.CurrentLocationId, out var locationDef))
                position = new Vector3(locationDef.WorldX, locationDef.WorldY, 0f);
            position += new Vector3(_activeChibis.Count * 2f, 0f, 0f);
            chibi.transform.localPosition = position;

            _activeChibis[npc.Id] = chibi;
            Debug.Log($"[ChibiSpawnerView] Spawn chibi {npc.Id} @ {npc.CurrentLocationId} (active={_activeChibis.Count})");
        }

        private void DespawnChibi(string npcId)
        {
            if (_activeChibis.TryGetValue(npcId, out var chibi))
            {
                Destroy(chibi);
                _activeChibis.Remove(npcId);
                Debug.Log($"[ChibiSpawnerView] Despawn chibi {npcId} (active={_activeChibis.Count})");
            }
        }

        private void OnDestroy()
        {
            // MessagePipe: ยกเลิก subscription ทุกตัวกัน memory leak
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
