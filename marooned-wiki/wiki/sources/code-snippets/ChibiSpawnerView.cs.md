---
title: ChibiSpawnerView
type: snippet
sources: ["[[sources/chibispawnerview-cs]]"]
related:
  - "[[NpcDirectorSystem.cs]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[GenericCuteVisualController.cs]]"
  - "[[RoundInitializer.cs]]"
  - "[[PlayerSurvivalState]]"
  - MessagePipe
  - "[[NpcLocationChangedMessage]]"
  - "[[PlayerLocationChangedMessage]]"
folder: Core/Visual
lines: 190
created: 2026-09-06
tags:
  - Core
  - marooned
  - lab-b
  - chibi
---

# ChibiSpawnerView.cs
**Path:** `Marooned/Assets/Scripts/Core/Visual/ChibiSpawnerView.cs`

## Source
```csharp
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

        [Header("GenericCute backend — '001 Student 1 Character'")]
        [SerializeField] private GameObject genericCutePrefab;

        [Header("Spine backend (experimental) — เวียนสลับตาม index NPC เพื่อหน้าตาไม่ซ้ำกัน")]
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
        /// เลือก prefab ตาม backend:
        ///  - GenericCute → ใช้ตัวเดียวหมด (พฤติกรรมเดิม Phase 1)
        ///  - Spine → เวียนสลับตามเลขท้าย npc id (npc_01→[0] Elena, npc_02→[1] Derek, ...)
        ///    เพื่อให้หน้าตาไม่ซ้ำกัน; ถ้า array ว่าง fallback กลับ GenericCute
        /// </summary>
        private GameObject PickPrefab(string npcId)
        {
            if (backend == ChibiBackend.Spine && spinePrefabs != null && spinePrefabs.Length > 0)
            {
                int npcNumber = ParseNpcNumber(npcId);
                int index = npcNumber > 0 ? (npcNumber - 1) % spinePrefabs.Length : 0;
                var picked = spinePrefabs[index];
                if (picked != null) return picked;
                Debug.LogWarning($"[ChibiSpawnerView] spinePrefabs[{index}] ว่าง — fallback ไป GenericCute");
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
```
*(source ใน repo มี Debug.Log + guard ครบกว่านี้ — snippet นี้ย่อเพื่ออ่านง่าย)*

# ChibiSpawnerView

## Purpose
Lab B Visual layer — spawn/despawn chibi ของ NPC ที่อยู่ location เดียวกับผู้เล่น
ตอบสนองต่อ event การเปลี่ยน location **แบบ event-driven** (ไม่ polling ใน Update)

## Installation (ตั้งค่าแล้วใน SampleScene)
- GameObject: `GameLifetimeScope/ChibiSystem`
- Components: Transform + ChibiSpawnerView
- `chibiPrefab` = `Assets/Generic Cute 2D - 001 Student 1/Student/001 Student 1/Prefabs/001 Student 1 Character.prefab`

## Architectural Rules (ตามข้อกำหนด Lab B)
| กฎ | การนำไปใช้ |
| --- | --- |
| VContainer Resolve เท่านั้น | `GetComponentInParent<GameLifetimeScope>()` + `Container.Resolve<T>()` ใน `Start()` — ไม่มี `[Inject]`, System ไม่ถูกลากใน Inspector (มีแค่ `chibiPrefab` ซึ่งเป็น asset ล้วนๆ) |
| MessagePipe แทน polling | Subscribe `PlayerLocationChangedMessage` + `NpcLocationChangedMessage` → เรียก `ReconcileChibis()` — ไม่มีการเช็คตำแหน่งใน `Update()` |
| MVP Lite | นี่คือ View แบบ passive — logic ทั้งหมดอยู่ที่ Systems (NpcDirectorSystem/ExplorationSystem เป็นคน publish) |

## Key Logic
- **`ReconcileChibis()`** เป็น single entry point: เทียบ state จริง (`Npcs`) กับ
  `_activeChibis` — spawn ที่ควรมี, despawn ที่ควรหาย (รวม `IsAlive=false`), ลบ
  chibi ค้างของ NPC ที่ถูกลบออกจากระบบ — ทำให้รับได้ทั้ง message ใดๆ โดยไม่ซ้ำซ้อน
- **Initial sync แบบ one-shot** (`UniTask.NextFrame` หนึ่งครั้ง): เพราะ `Start()`
  ของ GameObject ต่างกันรันไม่กำหนดลำดับ — ถ้า RoundInitializer publish message
  ก่อนเรา subscribe จะพลาด จึง sync ครั้งเดียวหลัง Start ทุกตัวเสร็จ (ไม่ใช่ polling)
- **Positioning**: ใช้ `LocationDef.WorldX/WorldY` + offset x ทีละ 2 unit ตามจำนวน
  chibi ที่มีอยู่กันซ้อนกัน (แผนที่ mock ยังไม่มี visual tile)
- Dispose subscription ทุกตัวใน `OnDestroy` กัน leak

## TODO / Known Issues
- ยังไม่มี visual ของตัวผู้เล่นเอง (เฉพาะ NPC chibi)
- ตำแหน่ง chibi ยังไล่ offset ตรงๆ — ควรอ่านจาก spawn point table เมื่อมีแผนที่จริง
- อนาคตถ้ามี `NpcActivityChangedMessage` ให้ forward เข้า `GenericCuteVisualController`
