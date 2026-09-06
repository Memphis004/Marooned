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
lines: 137
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
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    public class ChibiSpawnerView : MonoBehaviour
    {
        [Header("Prefab ล้วนๆ — ลาก '001 Student 1 Character' ใส่ตรงนี้")]
        [SerializeField] private GameObject chibiPrefab;

        private NpcDirectorSystem _npcDirector;
        private GameStateProvider _stateProvider;
        private LubanDataService _data;

        private readonly Dictionary<string, GameObject> _activeChibis = new();
        private readonly List<IDisposable> _subscriptions = new();
        private bool _resolved;

        private void Start()
        {
            // VContainer: container ถูก build ใน LifetimeScope.Awake จึง resolve ใน Start
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null) { /* error + disable */ return; }

            _npcDirector = scope.Container.Resolve<NpcDirectorSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _data = scope.Container.Resolve<LubanDataService>();
            _resolved = true;

            // MessagePipe: subscribe event การเปลี่ยน location (in-process bus)
            var playerLocationSubscriber = scope.Container.Resolve<ISubscriber<PlayerLocationChangedMessage>>();
            var npcLocationSubscriber = scope.Container.Resolve<ISubscriber<NpcLocationChangedMessage>>();
            _subscriptions.Add(playerLocationSubscriber.Subscribe(_ => ReconcileChibis()));
            _subscriptions.Add(npcLocationSubscriber.Subscribe(_ => ReconcileChibis()));

            // one-shot initial sync หลัง 1 เฟรม (รอ Start ของ RoundInitializer) — ไม่ใช่ polling
            SyncInitialOnceAsync().Forget();
        }

        private async UniTaskVoid SyncInitialOnceAsync()
        {
            await UniTask.NextFrame();
            if (this != null && _resolved) ReconcileChibis();
        }

        private void ReconcileChibis()
        {
            var playerLocation = _stateProvider.Player.CurrentLocationId;
            foreach (var npc in _npcDirector.Npcs.Values)
            {
                bool shouldBeVisible = npc.IsAlive && npc.CurrentLocationId == playerLocation;
                if (shouldBeVisible && !_activeChibis.ContainsKey(npc.Id)) SpawnChibi(npc);
                else if (!shouldBeVisible && _activeChibis.ContainsKey(npc.Id)) DespawnChibi(npc.Id);
            }
            // เก็บ chibi ค้างของ NPC ที่ถูกลบ (ย่อ — ดู full source ใน repo)
        }

        private void SpawnChibi(NpcState npc)
        {
            var chibi = Instantiate(chibiPrefab, transform);
            var visual = chibi.GetComponent<GenericCuteVisualController>();
            if (visual != null) visual.Bind(npc);
            // วางตำแหน่งตาม LocationDef.WorldX/WorldY + offset กันซ้อน
        }

        private void DespawnChibi(string npcId)
        {
            if (_activeChibis.TryGetValue(npcId, out var chibi)) { Destroy(chibi); _activeChibis.Remove(npcId); }
        }

        private void OnDestroy()
        {
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
