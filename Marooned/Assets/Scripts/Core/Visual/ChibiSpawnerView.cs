using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Marooned.Core.Visual;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// Lab B Phase 5 (click-to-use): เพิ่มโหมดเลือกเป้าหมาย — SetTargetSelectionMode(true)
    /// แล้วคลิก chibi → NpcClicked(npcId), คลิกที่ว่าง → WorldClicked (ยกเลิก)
    /// (NPC chibi ไม่มี Canvas UI จึงใช้ Physics2DRaycaster ตรงๆ แทน IPointerClickHandler)
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
        // LubanDataService ถูกถอดออก (Step 3): ตำแหน่ง chibi มาจาก NpcState.PositionX/Y
        // ผ่าน NpcCharacterView แล้ว ไม่ต้องอ่าน LocationDefs ใน spawner อีก

        // Key: npcId, Value: chibi GameObject ที่ spawn อยู่บนจอ
        private readonly Dictionary<string, GameObject> _activeChibis = new();
        private readonly List<IDisposable> _subscriptions = new();
        private bool _resolved;

        // ---- Lab B Phase 5: target selection (click NPC to use weapon card) ----
        private bool _targetSelectionMode;
        private Camera _mainCamera;
        private Collider2D[] _hitBuffer = new Collider2D[8];

        /// <summary>อ่านค่า backend ปัจจุบัน (ให้ test script ใช้ยืนยัน config)</summary>
        public ChibiBackend Backend => backend;

        /// <summary>คลิกโดน NPC chibi (ในโหมดเลือกเป้าหมาย) — Presenter subscribe</summary>
        public event Action<string> NpcClicked;

        /// <summary>คลิกที่ว่าง (ไม่โดน NPC ใด) — ใช้ยกเลิกการเลือกเป้าหมาย</summary>
        public event Action WorldClicked;

        /// <summary>สลับ backend ตอน runtime (ทดลอง backend ใน Play Mode โดยไม่แก้ scene)</summary>
        public void SetBackend(ChibiBackend newBackend) => backend = newBackend;

        /// <summary>บังคับ reconcile ทันที (เช่นหลังสลับ backend — ปกติ reconcile เกิดจาก message เท่านั้น)</summary>
        public void RefreshNow() => ReconcileChibis();

        /// <summary>
        /// เปิด/ปิดโหมดเลือกเป้าหมาย: highlight chibi ทั้งหมด + เปิดรับ click NPC
        /// (เรียกโดย CardHandPresenter เมื่อเลือกการ์ด SingleTarget เช่น weapon)
        /// </summary>
        public void SetTargetSelectionMode(bool enabled)
        {
            _targetSelectionMode = enabled;
            foreach (var chibi in _activeChibis.Values)
                if (chibi != null) Highlight(chibi, enabled);
        }

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

        private void Update()
        {
            if (!_targetSelectionMode) return; // ปกติไม่ทำอะไรเลย — zero cost เมื่อไม่ได้เลือกเป้าหมาย

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                // คลิกบน UI (เช่น การ์ดในมือ) ไม่นับเป็น world click — ปล่อยให้ EventSystem จัดการ
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                var mouseWorld = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;

                var count = Physics2D.OverlapPointNonAlloc(mouseWorld, _hitBuffer);
                for (int i = 0; i < count; i++)
                {
                    var npcId = FindNpcIdFor(_hitBuffer[i]);
                    if (npcId == null) continue;
                    NpcClicked?.Invoke(npcId);
                    return;
                }
                WorldClicked?.Invoke(); // คลิกที่ว่าง → ยกเลิก
            }
        }

        /// <summary>ไต่ขึ้นหา chibi root ที่จับคู่กับ npcId ไว้ (collider อยู่ที่ child sprite ก็ได้)</summary>
        private string FindNpcIdFor(Collider2D hit)
        {
            var t = hit != null ? hit.transform : null;
            while (t != null)
            {
                foreach (var kv in _activeChibis)
                    if (kv.Value == t.gameObject) return kv.Key;
                t = t.parent;
            }
            return null;
        }

        private static void Highlight(GameObject chibi, bool on)
        {
            // ขั้นต่ำ: ปั่นสี sprite ให้ต่างจากปกติ (ไม่ต้องมี shader พิเศษ)
            foreach (var renderer in chibi.GetComponentsInChildren<Renderer>())
                renderer.material.color = on ? new Color(1f, 0.8f, 0.5f) : Color.white;
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

            // Lab C Phase 2 Step 3: ติดตั้ง NpcCharacterView (1 ตัวต่อ chibi) ผ่าน
            // direct call Init — มันจะ sync transform.position กับ NpcState.PositionX/Y
            // ทุกเฟรมต่อจากนี้ (MVP Lite: per-frame = direct read ไม่ใช่ MessagePipe)
            var characterView = chibi.GetComponent<NpcCharacterView>();
            if (characterView == null) characterView = chibi.AddComponent<NpcCharacterView>();
            characterView.Init(npc.Id, _npcDirector);

            // ⚠️ เฟรมแรกต้องไม่โผล่ที่ origin: ตั้งตำแหน่งจาก PositionX/Y จริงทันที
            // (Update ของ NpcCharacterView จะคงตำแหน่งนี้ต่อ — ไม่มี offset แย่งกัน)
            // หมายเหตุ: offset ไล่ตัวเดิมถูกถอด — ตำแหน่งจริงต่อ NPC มาจาก PositionX/Y
            chibi.transform.position = new Vector3(npc.PositionX, npc.PositionY, 0f);

            EnsureClickable(chibi);

            if (_targetSelectionMode) Highlight(chibi, true); // spawn ระหว่างโหมดเลือกเป้าหมาย → ไฮไลต์ทันที

            _activeChibis[npc.Id] = chibi;
            Debug.Log($"[ChibiSpawnerView] Spawn chibi {npc.Id} ({chibi.name}) @ {npc.CurrentLocationId} pos=({npc.PositionX:F1},{npc.PositionY:F1}) (active={_activeChibis.Count})");
        }

        /// <summary>
        /// การ์ด weapon คลิกเป้าหมายผ่าน Physics2D raycast จึงต้องมี Collider2D
        /// บน chibi — ถ้า prefab ไม่มี (ส่วนใหญ่ไม่มี) ใส่ trigger กลมคลุมตัวแบบง่าย
        /// </summary>
        private static void EnsureClickable(GameObject chibi)
        {
            if (chibi.GetComponentInChildren<Collider2D>() != null) return;

            var bounds = CalculateLocalBounds(chibi);
            var colliderGo = new GameObject("ClickCollider");
            colliderGo.transform.SetParent(chibi.transform, false);
            colliderGo.transform.localPosition = bounds.center;
            var circle = colliderGo.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.1f;
        }

        private static Bounds CalculateLocalBounds(GameObject root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            bool any = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                // bounds เป็น world-space → แปลงกลับเป็น local ของ root
                var local = root.transform.InverseTransformPoint(renderer.bounds.center);
                var ext = root.transform.InverseTransformVector(renderer.bounds.extents);
                if (!any) { bounds = new Bounds(local, ext * 2f); any = true; }
                else bounds.Encapsulate(new Bounds(local, ext * 2f));
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one);
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
