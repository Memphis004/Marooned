---
title: npc-embodiment-movement
type: architecture
sources:
  - Marooned/Assets/Scripts/Systems/NpcMovementSystem.cs
  - Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs
  - Shared/NpcState.cs
  - Shared/NpcInventory.cs
  - Marooned/Assets/Scripts/Core/GameTickDriver.cs
related:
  - "[[NpcMovementSystem.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[GameTickDriver.cs]]"
  - "[[npc-director]]"
  - "[[chibi-visual-system]]"
folder: architecture
created: 2026-09-10
tags:
  - architecture
  - npc
  - movement
  - lab-c
  - marooned
---

# NPC Embodiment — Data Foundation + Movement (Lab C Phase 2, Step 1-2)

## ภาพรวม (Gameplay Perspective)
NPC ไม่ได้แค่ "มีอยู่" ในโซนแล้วนิ่งเฉย — แต่ละตัวมีตำแหน่งจริงบนโลก (PositionX/Y),
เดินหาอาหาร/พักผ่อนเอง (wander), และทอยข้ามโซนผ่านจุดเชื่อมต่อของแผนที่
ทำให้ "Walking Sandbox" มีชีวิตจริง และเป็นฐานให้ AI ตาม role (InnocentUtilityAI /
KillerPlanner) ใน Step 4 ต่อได้ทันที

## การ Implement (Developer Perspective)

### Data Foundation (Step 1)
- **`NpcState`** เพิ่มฟิลด์ Ground Truth — MessagePack keys **9-15** (0-8 ใช้อยู่แล้ว ห้ามชน):
  | Key | Field | ความหมาย |
  |-----|-------|----------|
  | 9 | `PositionX` | ตำแหน่งจริงบนโลก (ต้อง seed ผ่าน MoveNpc เสมอ) |
  | 10 | `PositionY` | 〃 |
  | 11 | `TargetX` | จุดหมายที่กำลังเดินไป |
  | 12 | `TargetY` | 〃 |
  | 13 | `MovementSpeed` | default 2.0 (ผู้เล่น 3.5) |
  | 14 | `Inventory` | [[NpcInventory]] — อาวุธของ Killer ฯลฯ |
  | 15 | `Survival` | NpcSurvivalState (Hunger/Fear/Curiosity) — ground truth รอ Step 4 |
- **`NpcInventory`** (`Shared/NpcInventory.cs`): `Dictionary<string,int>` +
  `HasItem/AddItem/RemoveItem` — RemoveItem คืน false ถ้าไม่มีพอ (ไม่ mutate)
- **`NpcSurvivalState`** (ใน `Shared/NpcState.cs`): Hunger/Fear/Curiosity 0-100
  — ยังไม่มีใคร tick ค่า (Step 4 เป็นคนใช้)
- ⚠️ **MsgPack003**: analyzer ของ MessagePack บังคับว่า member type ต้องมี
  `[MessagePackObject]` ด้วย จึงใส่ attribute ให้ทั้งสอง class (ไม่ได้แปลว่า expose)

### ⚠️ Position Seeding Rule (กัน warp ไป (0,0))
`NpcDirectorSystem.MoveNpc()` = จุดเดียวที่เปลี่ยน `CurrentLocationId` และเป็นคน
seed พิกัดให้เองทุกครั้ง:
1. `PositionX/Y = LocationDef.WorldX/Y` ของ destination (จุดกึ่งกลางโซนใหม่)
2. `TargetX/Y = PositionX/Y` (ถือว่า "ถึงเป้าแล้ว" ทันที — กันเป้าเก่าจากโซนเดิม
   ลาก NPC เดินข้ามแผนที่หลังย้ายโซน = warp แอบแฝง)

`RoundInitializer.AssignStartingLocations()` วางตัวเริ่มต้นผ่าน `MoveNpc()` เท่านั้น
จึงได้ seeding ถูกต้องตั้งแต่เฟรมแรก

### แจกอาวุธให้ Killer (SetupRound)
- ตรวจการ์ด `Category == Weapon` จาก `LubanDataService.CardDefs` (ข้อมูลจริงจาก
  `DataTables/CardDef.csv` — ตอนนี้มี `knife_basic` SingleTarget+Eliminate อยู่แล้ว)
- ไม่ hardcode id — ถ้าไม่มีการ์ด Weapon เลย → log warning แล้วข้าม (Killer ยัง
  setup ได้ แค่ไม่มีอาวุธ; Step 4 KillerPlanner มี phase SeekingWeapon รองรับ)
- คนที่สุ่มได้ Role=Killer จะได้ `Inventory.AddItem(weaponCardId)` 1 ชิ้น

### Movement System (Step 2)
- **`NpcMovementSystem`** (`Marooned/Assets/Scripts/Systems/NpcMovementSystem.cs`)
  — plain C# singleton (VContainer `Lifetime.Singleton`), ไม่ใช่ MonoBehaviour
- `Tick(dt)` ต่อ NPC ที่ยังมีชีวิต:
  1. `MoveTowardTarget` — เลื่อน Position เข้าหา Target ด้วย MovementSpeed
     (normalize + กัน overshoot, frame-rate independent)
  2. ถึงเป้า (≤ 0.1f) → `PickNextTarget()`
- `PickNextTarget()` — **แยกเป็น public method อิสระเพื่อ deprecate ง่ายใน Step 4**:
  - ทอย 20% ข้ามโซน: สุ่ม `ConnectedLocationIds` → เรียก `MoveNpc()` **ทันทีตอนตั้ง
    target** (ไม่ใช่ detect ข้ามขอบระหว่างทาง) → แล้วสุ่ม target ใหม่ในโซนนั้น
  - ไม่งั้น: สุ่มจุดในรัศมี `WanderRadius = 2.5` รอบ `LocationDef.WorldX/Y`
    (สูตร offset เดียวกับ `WorldItemSystem.ZoneSpread` — ไม่เดา bounds ที่ไม่มีจริง)
  - ตั้ง `Activity = Traveling` ขณะมีเป้า (enum ถูกต้อง: ไม่มี Walking)
- Tick order ใน [[GameTickDriver.cs]]: `NpcDirectorSystem.Tick()` (AI ตั้ง target)
  **ก่อน** `NpcMovementSystem.Tick()` (เดิน) ในเฟรมเดียวกัน — ไม่ดีเลย์ 1 เฟรม

### Information Hiding (เคร่งครัด)
`PositionX/Y`, `TargetX/Y`, `Inventory`, `Survival` = Ground Truth — **ห้ามโผล่**ใน
`NpcObservableView` / `GetObservableNpcsAt` / MCP response ใดๆ
(ตรวจแล้ว: [[DeductionSystem.cs]] สร้าง view เฉพาะ Id/IsAlive/Location/Activity/
VisibleConditionCardIds/Avatar เท่านั้น)

## DI Wiring
- `GameLifetimeScope.Configure()`: `builder.Register<NpcMovementSystem>(Lifetime.Singleton).AsSelf()`
- `GameTickDriver` resolve + เรียก Tick ตามลำดับข้างบน

## ความเชื่อมโยงกับระบบอื่น
- [[npc-director]] — MoveNpc/SetupRound อยู่ใน NpcDirectorSystem (เจ้าของ ground truth)
- [[chibi-visual-system]] — NpcLocationChangedMessage เดิมยังใช้ spawn/despawn ข้ามโซน
  (Step 3 จะทำ chibi เดินตาม PositionX/Y ต่อ)
- [[WorldItemSystem]] — แหล่ง pattern ZoneSpread ที่ wander ยึด

## สถานะปัจจุบัน
- ✅ Step 1: NpcState Key 9-15 + NpcInventory + NpcSurvivalState + seeding rule + แจกอาวุธ
- ✅ Step 2: NpcMovementSystem (wander + ข้ามโซน 20% + PickNextTarget แยก method) + tick order
- ❌ Step 3 (รอบถัดไป): chibi เดินตาม Position จริง (NpcCharacterView)
- ❌ Step 4 (รอบถัดไป): InnocentUtilityAI + KillerPlanner — deprecate PickNextTarget
- ❌ NpcSurvivalState ยังไม่มีใคร tick ค่า (รอ Step 4)
- ❌ การเดินยังไม่เช็คชนกับสิ่งกีดขวาง / ขอบเขตโลก (wander clamp ที่รัศมีโซนพอ)
