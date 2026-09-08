---
title: biome-scatter-system
type: architecture
sources: []
related:
  - "[[player-system]]"
  - "[[LubanDataService.cs]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[exploration]]"
folder: sources/architecture
created: 2026-09-09
tags:
  - lab-c
  - biome
  - scatter
  - tool-gathering
  - luban
---

# 🌍 Biome Scatter System (Lab C Phase 1 — Hybrid Scatter)

> อัปเดตล่าสุด: 2026-09-09 — สถาปัตยกรรมใหม่ที่เปลี่ยนโปรเจกต์จากแนว
> "Ragnarok Online (tile-based)" เป็น "Don't Starve/Mad Island (modular scatter)"

## 🎯 Design Philosophy

- **Hybrid Approach**: Prefab Scatter + Random Offset (ไม่ใช่ procedural gen เต็มรูปแบบ)
- **Biome = Spawn Rules**: แต่ละ biome มีชุด prefab + loot table + harvestable nodes ของตัวเอง
- **Tool-Gathering Ready**: แยก `HarvestableNode` (ต้องใช้ tool) ออกจาก decorative prop (เดินผ่านได้) ชัดเจน

## 🏛️ Architecture Constraints (เคร่งครัดตาม conventions)

| Layer | ไฟล์ | หน้าที่ | สิ่งที่ **ห้าม** |
|---|---|---|---|
| Data | `DataTables/Data/BiomeDef.csv`, `HarvestableNodeDef.csv` → Luban gen | ค่าทุกอย่างมาจาก CSV | ห้าม hardcode ใน C# |
| System | `Systems/BiomeScatterSystem.cs`, `Systems/NodeHarvestSystem.cs` (plain C#, VContainer) | คำนวณ + publish events | **ห้าม Instantiate/Destroy GameObject** |
| View | `Core/Visual/BiomeScatterView.cs` (MonoBehaviour) | Subscribe events → Instantiate/Destroy | ห้ามคำนวณ spawn logic เอง |
| SO | `Data/BiomePrefabSet.cs` (ScriptableObject) | เก็บ prefab references เท่านั้น (ลากใส่ใน Inspector) | ห้ามใส่ logic |

การสื่อสารระหว่าง System ↔ View ใช้ **MessagePipe in-process** เท่านั้น

## 📐 Data Flow

```
PlayerLocationChangedMessage (จาก Exploration/MoveToLocation)
        │
        ▼
BiomeScatterView.OnPlayerLocationChanged ── mapLocationToBiome() ─┐
        │                                                          │
        ▼                                                          ▼
BiomeScatterSystem.SetBiome(biomeId)
        │  1. อ่าน BiomeDefs (LubanDataService) → propDensity, harvestableNodeIds
        │  2. Resources.Load<BiomePrefabSet>("BiomePrefabSets/{biomeId}")
        │  3. สุ่มตำแหน่งใน WorldBounds (rejection sampling, MinSpawnDistance)
        ▼
publish BiomeChangedMessage { BiomeId, SpawnsToCreate[] }
        │
        ▼
BiomeScatterView.OnBiomeChanged
        │  1. ClearAll() — Destroy ของเก่าทั้งหมด
        │  2. ApplyGround() — tint สีพื้นตาม BiomeDef.backgroundColor
        │  3. Instantiate ตาม SpawnsToCreate (prop_{n} → propPrefabs[n],
        │     node id → harvestablePrefabs ที่ prefab.name ตรงกัน)
        │  4. node → AddComponent<HarvestableNodeComponent>().Init(nodeId, def, NodeHarvestSystem)
        ▼
ผู้เล่นกด E ใกล้ node (GameTickDriver → NodeHarvestSystem.Tick)
        │  1. FindNearestNode ในรัศมี HarvestRadius (1.6)
        │  2. component.TryHarvest(toolItemId) → เช็ค requiredToolCardId, ลด durability
        │  3. _inventory.TryAdd(yieldItemId, yieldCount)
        ▼
publish NodeHarvestedMessage { NodeId, ItemId, Count, Depleted, RegrowSeconds }
        │
        ▼
durability หมด → component.StartRegrowIfAny()
   ├─ regrowTime > 0 → IsRegrowing (จับเวลาใน Update → กลับมาเก็บได้ใหม่)
   └─ regrowTime = 0 → Destroy(gameObject) (หายถาวร)
```

## 📊 ตาราง Luban

### BiomeDef.csv

| field | type | ความหมาย |
|---|---|---|
| `id` | string | biome id (เช่น `beach`, `jungle`, `cave`) |
| `displayName` | string | ชื่อแสดงผล |
| `propDensity` | float | ความหนาแน่น props (0–1) → จำนวนจุด = density × 20 (`PropsPerDensityUnit`) |
| `itemSpawnRate` | float | โอกาสไอเท็ม (ยังไม่ใช้ใน Phase 1) |
| `backgroundColor` | string | hex สีพื้น (View tint) |
| `fogDensity` | float | ความหนา fog (ยังไม่ใช้ใน Phase 1) |
| `harvestableNodeIds` | list,string (sep `;`) | node ids ที่ spawn ใน biome นี้ (1 จุดต่อ 1 id) |

### HarvestableNodeDef.csv

| field | type | ความหมาย |
|---|---|---|
| `id` | string | node id (เช่น `tree_wood`, `rock_stone`, `bush_berry`) |
| `displayName` | string | ชื่อบนป้าย placeholder |
| `requiredToolCardId` | string | ว่าง = เก็บมือเปล่าได้, ไม่งั้นต้องถือการ์ด tool นี้ |
| `yieldItemId` | string | card id ที่ได้ (ต้องมีใน CardDef.csv) |
| `yieldCount` | int | จำนวนที่ได้ต่อการเก็บ 1 ครั้ง |
| `durability` | int | เก็บได้กี่ครั้งก่อนหมด |
| `regrowTime` | int | วินาทีรองอกใหม่ (0 = หายถาวร) |

## 🧩 ScriptableObject — BiomePrefabSet

เก็บที่ `Assets/Resources/BiomePrefabSets/{biomeId}.asset` (ชื่อ asset ต้องตรง biomeId)

- `biomeId` — ต้องตรงกับ BiomeDef.csv
- `propPrefabs` — ต้นไม้/หิน/หญ้าตกแต่ง (ไม่มี Collider หรือ IsTrigger)
- `harvestablePrefabs` — ต้นไม้ตัดได้/ก้อนหินขุดได้ (**prefab.name ต้องตรงกับ node id** — View หา prefab ด้วยชื่อ)
- `groundMaterial` — (เสริม) material พื้นแทน tint

## 🔌 DI Registration (GameLifetimeScope)

```csharp
builder.Register<BiomeScatterSystem>(Lifetime.Singleton).AsSelf();
builder.Register<NodeHarvestSystem>(Lifetime.Singleton).AsSelf();
```

- `WorldBounds` ใช้ instance เดียวกับ PlayerMovementSystem (RegisterInstance เดิม)
- `NodeHarvestSystem` tick โดย `GameTickDriver` **หลัง** `ItemPickupSystem` (กด E ครั้งเดียวเก็บไอเท็มพื้นก่อน แล้วจึงโดน node)
- `BiomeScatterView` ติดบน GameObject ลูกของ GameLifetimeScope (resolve ผ่าน `GetComponentInParent` ตาม convention — ไม่ลาก reference)

## 🗺️ Location → Biome Mapping (ชั่วคราว)

`LocationDef.csv` ยังไม่มี column `biomeId` — `BiomeScatterView.MapLocationToBiome()`
switch แบบ hardcode ชั่วคราว (`beach→beach`, `jungle_edge|deep_jungle→jungle`,
`cave_entrance→cave`, และ id ตรงกันตรง ๆ)

> **TODO Lab C Phase 2**: เพิ่ม column `biomeId` ใน LocationDef.csv แล้วอ่านจาก
> LubanDataService แทน switch

## ✅ สถานะการเทส (Test A–D)

ดูวิธี setup และขั้นตอนเทสทั้งหมดใน [[biome-scatter-editor-setup]]

| Test | สถานะ | หมายเหตุ |
|---|---|---|
| A: Play → biome เริ่มต้น spawn props + nodes | ⏳ รอ setup scene (ต้องสร้าง BiomePrefabSet + prefab ก่อน) | |
| B: `explore_location("cave")` → ของเก่าหาย ของ cave โผล่ | ⏳ รอ setup scene | publish chain ครบแล้ว |
| C: เดินเข้าใกล้ node + กด E ด้วย tool ถูก/ผิด | ⏳ รอ setup scene | wrong_tool → ไม่ได้ของ ไม่หัก durability |
| D: node หมด durability → regrow ตาม regrowTime | ⏳ รอ setup scene | regrowTime = 0 → หายถาวร |

## ⚠️ ข้อจำกัดที่รู้อยู่ (Known Gaps)

1. **Node state หายตอนเปลี่ยน biome** — durability/regrow เป็น state ของ GameObject
   (component); `ClearAll()` Destroy ทิ้งหมด → เข้า biome เดิมใหม่ = node เต็มทุกต้น
   (ยอมรับได้ใน Phase 1 — อนาคตย้าย state ไป System)
2. **`FindObjectsByType` ทุกเฟรมเมื่อกด E** — จำนวน node น้อย ยังไหว; node เยอะค่อยทำ registry
3. **ของตกแต่งยังไม่มี collision** — props เป็น visual ล้วนตาม design (เดินผ่านได้)
4. **prop ใช้ index ไม่ใช่ id** — `SpawnPosition.PrefabId = "prop_{n}"` ชี้ index ใน list
   (harvestable ใช้ id จริง); ถ้าเปลี่ยนลำดับ list ระหว่าง runtime จะเพี้ยน — ยอมรับได้
