using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Marooned.Core.Visual;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>เลือก visual backend ของ chibi (Phase 2: เพิ่ม Spine เป็น experimental)</summary>
    public enum ChibiBackend
    {
        GenericCute, // default — 001 Student 1 (Unity Animator)
        Spine,       // experimental — Elena/Derek (Spine-Unity SkeletonAnimation)
    }

    /// <summary>
    /// Lab B — Visual layer สำหรับ Chibi (MVP Lite: View แบบ passive)
    /// คอย spawn/despawn chibi ของ NPC ที่อยู่ location เดียวกับผู้เล่น
    ///
    /// Architectural rules:
    ///  - VContainer: Resolve ทุก dependency จาก GetComponentInParent&lt;GameLifetimeScope&gt;()
    ///    ใน Start() — ห้าม [Inject]/ลาก System ใส่ Inspector
    ///  - MessagePipe: subscribe PlayerLocationChangedMessage / NpcLocationChangedMessage
    ///    — ห้าม polling ตำแหน่งใน Update()
    ///  - SerializeField รับได้เฉพาะ asset ล้วนๆ (prefab) + backend toggle
    ///  - Phase 2: พึง IChibiVisual แทน concrete controller และสลับ prefab ตาม
    ///    character rotation (npc_01→spinePrefabs[0], npc_02→[1], ...) เมื่อใช้ Spine
    ///
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (เช่น ChibiSystem)
    /// </summary>
    public class ChibiSpawnerView : MonoBehaviour
    {
        [Header("Backend (default: GenericCute)")]
        [SerializeField] private ChibiBackend backend = ChibiBackend.GenericCute;

        [Header("GenericCute fallback — '001 Student 1' (สงวนให้ Player; NPC ใช้เมื่อ npcPrefabs ว่าง)")]
        [SerializeField] private GameObject genericCutePrefab;

        [Header("NPC prefabs ตระกูล GenericCute — เวียนสลับตาม index NPC (WizardChibi, CollegeStudentChibi...)")]
        [SerializeField] private GameObject[] npcPrefabs;

        [Header("Spine backend (experimental) — เวียนสลับตาม index NPC (ElenaChibi, DerekChibi...)")]
        [SerializeField] private GameObject[] spinePrefabs;

        private NpcDirectorSystem _npcDirector;
        private GameStateProvider _stateProvider;
        private LubanDataService _data;

        // Key: npcId, Value: chibi GameObject ที่ spawn อยู่บนจอ
        private readonly Dictionary<string, GameObject> _activeChibis = new();
        private readonly List<IDisposable> _subscriptions = new();
        private bool _resolved;

        /// <summary>อ่านค่า backend ปัจจุบัน (ให้ test script ใช้ยืนยัน config)</summary>
        public ChibiBackend Backend => backend;

        /// <summary>สลับ backend ตอน runtime (ทดลอง backend ใน Play Mode โดยไม่แก้ scene)</summary>
        public void SetBackend(ChibiBackend newBackend) => backend = newBackend;

        /// <summary>บังคับ reconcile ทันที (เช่นหลังสลับ backend — ปกติ reconcile เกิดจาก message เท่านั้น)</summary>
        public void RefreshNow() => ReconcileChibis();

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
            // จึงเลื่อน sync ครั้งแรกไป 1 เฟรม — one-shot sync ไม่ใช่ polling
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

            var playerLocation = _stateProvider.GetPlayer().CurrentLocationId;

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

        /// <summary>
        /// เลือก prefab ตาม backend (เวียนสลับตามเลขท้าย npc id — npc_01→[0], npc_02→[1], ...):
        ///  - GenericCute → npcPrefabs (Wizard/CollegeStudent; Student 1 สงวนให้ Player)
        ///  - Spine (experimental) → spinePrefabs (Elena/Derek)
        /// ถ้า array ของ backend ว่าง → fallback genericCutePrefab
        /// </summary>
        private GameObject PickPrefab(string npcId)
        {
            var rotationPool = backend == ChibiBackend.Spine ? spinePrefabs : npcPrefabs;
            if (rotationPool != null && rotationPool.Length > 0)
            {
                int npcNumber = ParseNpcNumber(npcId);
                int index = npcNumber > 0 ? (npcNumber - 1) % rotationPool.Length : 0;
                var picked = rotationPool[index];
                if (picked != null) return picked;
                Debug.LogWarning($"[ChibiSpawnerView] rotationPool[{index}] ว่าง — fallback ไป genericCutePrefab");
            }
            return genericCutePrefab;
        }

        private static int ParseNpcNumber(string npcId)
        {
            // npc_03 → 3
            var underscore = npcId != null ? npcId.LastIndexOf('_') : -1;
            return underscore >= 0 && int.TryParse(npcId.Substring(underscore + 1), out var n) ? n : 0;
        }

        private void SpawnChibi(NpcState npc)
        {
            var prefab = PickPrefab(npc.Id);
            if (prefab == null)
            {
                Debug.LogWarning("[ChibiSpawnerView] prefab ยังไม่ถูก assign ใน Inspector — ข้ามการ spawn");
                return;
            }

            var chibi = Instantiate(prefab, transform);

            // ผูก NpcActivityState เข้ากับ visual ผ่าน interface กลาง
            // (spawner ไม่รู้จัก backend ข้างใต้ — Animator หรือ Spine)
            var visual = chibi.GetComponent<IChibiVisual>();
            if (visual != null) visual.Bind(npc.Activity);
            else Debug.LogWarning($"[ChibiSpawnerView] '{chibi.name}' ไม่มี component ที่ implement IChibiVisual");

            // วางตำแหน่งตาม WorldX/WorldY ของ location + ไล่ offset กัน chibi ซ้อนกัน
            var position = Vector3.zero;
            if (_data.LocationDefs.TryGetValue(npc.CurrentLocationId, out var locationDef))
                position = new Vector3(locationDef.WorldX, locationDef.WorldY, 0f);
            position += new Vector3(_activeChibis.Count * 2f, 0f, 0f);
            chibi.transform.localPosition = position;

            _activeChibis[npc.Id] = chibi;
            Debug.Log($"[ChibiSpawnerView] Spawn chibi {npc.Id} ({chibi.name}) @ {npc.CurrentLocationId} (active={_activeChibis.Count})");
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
