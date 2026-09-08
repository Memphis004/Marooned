---
title: biome-scatter-editor-setup
type: architecture
sources: []
related:
  - "[[biome-scatter-system]]"
  - "[[GameLifetimeScope.cs]]"
folder: sources/architecture
created: 2026-09-09
tags:
  - lab-c
  - biome
  - editor-setup
  - testing
---

# 🛠️ Biome Scatter — Unity Editor Setup & Test Protocol (Lab C Phase 1)

> อัปเดตล่าสุด: 2026-09-09 — คู่มือติดตั้งใน Editor + หลักฐานการเทส Test A–D
> อ่านสถาปัตยกรรมก่อนใน [[biome-scatter-system]]

## ⚡ ทางลัด: Setup อัตโนมัติ (แนะนำ)

```
Marooned > Lab C > Setup Biome Scatter Test Scene
```

สร้าง prefab placeholders 9 ตัว + BiomePrefabSet 3 ชุด + เพิ่ม BiomeScatterView ใน
scene แล้ว save scene ให้เอง (idempotent — รันซ้ำไม่ทับของจริง; สคริปต์
`Assets/Editor/BiomeScatterTestSetup.cs`)

เทสก็รันจากเมนูเดียวกันได้ (ระหว่าง Play): `Run Test A/C/D`, `Dump Play Logs` —
เขียนไฟล์หลักฐานลง `TestEvidence/lab-c-phase1/` อัตโนมัติ + Test B ใช้
`python Tools/mcp_roundtrip.py` (ดู README ในโฟลเดอร์นั้น)

ขั้นตอน manual ด้านล่างยังใช้ได้ ถ้าอยากใช้ art จริงแทน placeholder:

## 1️⃣ สร้าง BiomePrefabSet (3 ชุด)

1. ใน Project window → สร้าง folder `Assets/Resources/BiomePrefabSets/` (ถ้ายังไม่มี)
2. คลิกขวา → **Create → Marooned → BiomePrefabSet** → ตั้งชื่อ asset ให้ตรง biomeId:
   - `beach.asset` (biomeId = `beach`)
   - `jungle.asset` (biomeId = `jungle`)
   - `cave.asset` (biomeId = `cave`)
3. กรอกใน Inspector:

| Field | ค่า | หมายเหตุ |
|---|---|---|
| `biomeId` | ต้องตรงกับ BiomeDef.csv | `beach` / `jungle` / `cave` |
| `propPrefabs` | ลาก prefab ต้นไม้/หิน/หญ้า/เปลือกหอย | **ไม่ต้องมี Collider** (เดินผ่านได้) |
| `harvestablePrefabs` | ลาก prefab ต้นไม้ตัดได้ / ก้อนหินขุดได้ / พุ่มเบอร์รี่ | **prefab.name ต้องตรงกับ node id** เป๊ะ ๆ: `tree_wood`, `rock_stone`, `bush_berry` |
| `groundMaterial` | ใส่หรือไม่ก็ได้ | ถ้าไม่ใส่ View จะ tint สีพื้นจาก `BiomeDef.backgroundColor` ให้เอง |

> ⚠️ ถ้า prefab.name ไม่ตรง node id — View จะ warn
> `node '{id}' ไม่มี prefab ใน harvestablePrefabs` และข้ามจุดนั้น

Placeholder prefab ชั่วคราว (ยังไม่มี art): สร้าง GameObject → ใส่ SpriteRenderer
(สี่เหลี่ยม/วงกลมสีใดก็ได้) → ลากเป็น prefab → ตั้งชื่อตามตารางข้างบน
harvestable ใส่ Collider2D ได้เผื่ออนาคต แต่ระบบเก็บเกี่ยวใช้ระยะ (radius) ไม่ใช้ Collider

## 2️⃣ ติด BiomeScatterView ใน Scene

1. เปิด `SampleScene` → หา GameObject ลูกของ `GameLifetimeScope`
   (แนะนำสร้าง GameObject เปล่าชื่อ `BiomeScatterLayer` ใต้ scope เดียวกับ `GameManager`)
2. **Add Component → BiomeScatterView**
3. Inspector:
   - `Tint Ground` ✓ (ปิดได้ถ้าไม่อยากให้เปลี่ยนสีพื้นตาม biome)
   - `Ground Alpha` = 0.35 (ความทึบของสีพื้น placeholder)

View จะ resolve ทุกอย่างเองผ่าน `GetComponentInParent<GameLifetimeScope>()` — **ไม่ต้องลาก reference ใด ๆ**

## 3️⃣ ตรวจสอบข้อมูล Luban (ทำให้แล้วใน repo)

- `DataTables/Data/BiomeDef.csv` — มี column `harvestableNodeIds` (sep `;`)
- `DataTables/Data/HarvestableNodeDef.csv` — `tree_wood` / `rock_stone` / `bush_berry`
- `DataTables/Data/CardDef.csv` — มี `tool_axe`, `tool_pickaxe`, `wood_log`, `stone`, `berry`
  (yield ต้องมีใน CardDef ไม่งั้น TryAdd จะ warn ไม่เข้า inventory)
- รัน gen แล้ว: JSON อยู่ครบที่ `Assets/Resources/DataTables/game_tb*.json` (8 ตาราง)

สั่ง gen ใหม่เมื่อแก้ CSV:

```bash
cd DataTables && dotnet ../Tools/Luban/Luban.dll -c ...
```

(ดู command เต็มใน devlog — ต้องการทั้ง `-c` definition และ output paths ตาม luban.conf)

## 4️⃣ เทส (Protocol)

> เวอร์ชันอัตโนมัติ: เมนู `Marooned > Lab C` (A/C/D + dump logs) และ
> `python Tools/mcp_roundtrip.py` (B) — ไฟล์หลักฐานเขียนให้เองที่
> `TestEvidence/lab-c-phase1/` ตารางสถานะอยู่ท้าย README ในโฟลเดอร์นั้น

เริ่มเกม: เปิด Unity → Play → เปิด `dotnet run` ใน `McpBridge/` เพื่อต่อ port 3216 (จำเป็นเฉพาะ Test B)

### ✅ Test A — Play → biome เริ่มต้น spawn

- **ขั้นตอน**: กด Play (ผู้เล่นเริ่มที่ location `beach`)
- **คาดหวัง**:
  - Console: `[LubanDataService] loaded ... 3 biomes, 3 harvestable nodes`
  - Console: `[BiomeScatterSystem] SetBiome 'beach': publish N spawn positions`
  - Console: `[BiomeScatterView] biome 'beach': instantiate N/N objects`
  - Scene: พื้น tint สีครีม `#F4E4C1`, props + nodes กระจายแบบสุ่มไม่ทับกัน (ระยะห่าง ≥ 0.8)
  - ป้าย node: `ต้นไม้ [3/3] (tool_axe)` เป็นต้น
- **หลักฐาน**: ภาพหน้าจอ Game view + Console (`TestEvidence/lab-c-phase1/test-a/`)

### ✅ Test B — เปลี่ยน biome ผ่าน MCP

- **ขั้นตอน**: จาก Play mode รัน MCP `explore_location("cave_entrance")` (หรือ move_to_location)
- **คาดหวัง**:
  - ของเก่า (beach) หายทั้งหมด → ของ cave โผล่มาแทน (พื้นเปลี่ยนเป็น `#1A1A1A`)
  - Console: `PlayerLocationChangedMessage` → `SetBiome 'cave'` → `instantiate N/N`
- **หลักฐาน**: ภาพหน้าจอ ก่อน/หลัง (`TestEvidence/lab-c-phase1/test-b/`)

### ✅ Test C — เก็บเกี่ยวด้วย tool ถูก/ผิด

- **ขั้นตอน**: เดิน (WASD) เข้าใกล้ node ในรัศมี 1.6 หน่วย → กด E
  (ระบบเลือก tool จาก inventory ให้เอง — auto-pick)
- **คาดหวัง**:
  - **ไม่มี tool ในมือ** (เก็บ `tree_wood` โดยไม่มีการ์ด `tool_axe`): Console
    `[NodeHarvestSystem] harvest 'tree_wood' ไม่สำเร็จ: missing_tool`
    — durability คงเดิม, inventory ไม่เพิ่ม
  - **มี tool ถูก** (การ์ด `tool_axe` ในมือ): ได้ `wood_log` ×1 เข้ามือการ์ด,
    ป้ายลดเหลือ `[2/3]`, Console `harvested 'tree_wood' → +1 wood_log (durability 2, depleted=False)`
  - Node `bush_berry` (ไม่ต้องใช้ tool): เก็บมือเปล่าได้ `berry` ×2
- **หลักฐาน**: ภาพหน้าจอ มือการ์ดก่อน/หลัง + Console (`TestEvidence/lab-c-phase1/test-c/`)

### ✅ Test D — durability หมด → regrow

- **ขั้นตอน**: เก็บ node เดิมซ้ำจน durability = 0 (เช่น `bush_berry` 2 ครั้ง — regrow 30s)
- **คาดหวัง**:
  - ครั้งสุดท้าย: Console `depleted=True`, ป้ายเปลี่ยนเป็น `พุ่มเบอร์รี่ (กำลังงอกใหม่…)` สีจาง
  - กด E ระหว่าง regrow: `[NodeHarvestSystem] harvest 'bush_berry' ไม่สำเร็จ: regrowing`
  - หลัง 30 วิ: ป้ายกลับเป็น `[2/2]` สีปกติ → เก็บได้อีก
  - ถ้า regrowTime = 0 (ถาวร): GameObject ถูก Destroy ทันทีที่ durability หมด
- **หลักฐาน**: ภาพหน้าจอ/คลิป Console timeline (`TestEvidence/lab-c-phase1/test-d/`)

## 5️⃣ ปัญหาที่พบบ่อย

| อาการ | สาเหตุ | วิธีแก้ |
|---|---|---|
| `ไม่เจอ BiomePrefabSet '{id}' ใน Resources/BiomePrefabSets` | ยังไม่ได้สร้าง asset หรือชื่อไม่ตรง biomeId | สร้าง asset ชื่อให้ตรง §1 |
| `node '{id}' ไม่มี prefab ใน harvestablePrefabs` | prefab.name ≠ node id | เปลี่ยนชื่อ prefab ให้ตรง |
| `biome '{id}' ไม่มีใน BiomeDefs` | CSV ยังไม่มี row / ยังไม่ได้ gen | เพิ่ม row + รัน Luban gen |
| `yield card '{id}' ไม่มีใน CardDefs` | yieldItemId ไม่อยู่ใน CardDef.csv | เพิ่ม card row + gen ใหม่ |
| View spawn แต่ไม่เห็นอะไร | sortingOrder ชนกับ marker/ไอเท็ม | props ที่ -10, พื้น -100 (ค่าเริ่มถูกต้องแล้ว) |
| กด E แล้วไม่มีอะไรเกิดขึ้น | อยู่นอกรัศมี 1.6 หรือ node กำลัง regrow | เข้าใกล้ขึ้น / รอ regrow (ดู Console) |
