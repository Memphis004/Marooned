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

### Movement System (Step 2 + Step 3 patch)
- **`NpcMovementSystem`** (`Marooned/Assets/Scripts/Systems/NpcMovementSystem.cs`)
  — plain C# singleton (VContainer `Lifetime.Singleton`), ไม่ใช่ MonoBehaviour
- `Tick(dt)` ต่อ NPC ที่ยังมีชีวิต:
  1. `MoveTowardTarget` — เลื่อน Position เข้าหา Target ด้วย MovementSpeed
     (normalize + กัน overshoot, frame-rate independent)
  2. ถึงเป้า (≤ 0.1f) → **Activity = Idle + สุ่มพัก 0.8–2.5 วิ** (เพิ่มใน Step 3
     ตาม contract "Idle เมื่อถึงเป้า" — ทำให้จอเห็น idle ชัดเจน) → หมดเวลาพัก
     ค่อย `PickNextTarget()`
- **Step 4 note**: idle-pause นี้อยู่ใน Tick loop เดียวกับ re-target — ตอน
  deprecate PickNextTarget ให้ย้ายการจัดการ Idle/พักไป AI planner ด้วย
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

### Visual Sync (Step 3)
- **`NpcCharacterView`** (`Marooned/Assets/Scripts/Core/Visual/NpcCharacterView.cs`)
  — MonoBehaviour 1 ตัวต่อ chibi, ติดตั้งโดย ChibiSpawnerView.SpawnChibi ผ่าน
  `Init(npcId, npcDirector)` แบบ direct call (View แบบ passive — ไม่ resolve container)
- `Update()`: อ่าน NpcState ตรง → `transform.position = (PositionX, PositionY, 0)` +
  flip จากเครื่องหมาย dx (threshold 0.02) + `IChibiVisual.Bind(Activity)`
  — per-frame = direct read (บทเรียน Lab 13) ไม่ใช้ MessagePipe ต่อเฟรม
- ตำแหน่งเฟรมแรก: SpawnChibi ตั้ง `chibi.transform.position` จาก PositionX/Y ทันที
  (offset ไล่ตัวเดิมถูกถอด — ตำแหน่งจริงมาจาก state แล้ว)
- `IChibiVisual` ทั้งสอง backend ยืนยันแล้วว่า Traveling → walk:
  GenericCute → "walk" (SerializeField ต่อ prefab), Spine → "Walking" (Elena/Derek)
- Spine flip ผ่าน `Skeleton.ScaleX`, GenericCute flip ผ่าน `localScale.x` — view ไม่ต้องรู้
- Property `NpcId` (read-only) เพิ่มบน view เพื่อการจับคู่/ตรวจสอบในเทส

## การเทส (Play Mode จริง 2026-09-10)
- A ✅: syncErr=(0.00,0.00) ทุก snapshot, anim=walk/idle ตาม Traveling/Idle, face สลับ L/R
- B ✅: ข้ามโซน spawn/despawn บาลานซ์ (5/5) ผ่าน message flow เดิม ไม่มี transform แย่ง
- C ✅: backend=Spine (runtime toggle) — anim=Walking บน rig Spine + syncErr 0
- D ✅: ไม่มี exception จาก game code, ผู้เล่น/กล้อง/pickup ไม่ถูกแตะ, port 3216 listening
- ภาพหลักฐาน: `TestEvidence/step3_testA_genericcute_walk.png`, `TestEvidence/step3_testC_spine_walk.png`

## Step 4: Basic AI Hook (2026-09-10)
- **สมองแยกตาม role** ใน `NpcDirectorSystem.TickBehavior`:
  `Role==Killer → KillerPlanner.Tick` ไม่งั้น → `InnocentUtilityAI.Tick`
- **4.1 deprecate re-target-on-arrival**: `NpcMovementSystem` เหลือหน้าที่เดินเข้าหา
  Target ที่มีอยู่ + ตั้ง Activity ตามสถานะ (Traveling/Idle) เท่านั้น — `PickNextTarget`
  ถูกลบ การเลือก target ทั้งหมดอยู่ที่ AI (กันสองระบบแย่ง TargetX/Y)
- **4.2 namespace `Marooned.Systems.AI`**:
  - `IUtilityAction { Id, Score(npc, ctx), Execute(npc, ctx, dt) }` (Score = pure)
  - `UtilityContext { Data, NpcDirector, StateProvider }` — Singleton สร้างครั้งเดียว
  - `Wander` (static helper): `HasPendingTarget / SetRandomTargetInZone /
    MoveToRandomConnectedZone / MoveToZone / FindZonesWithCardCategory` — ตั้ง target
    ผ่าน MoveNpc เท่านั้น (Single Source of Truth) caller เช็ค HasPendingTarget ก่อน
    ตั้งเป้าใหม่ (กันแย่ง)
  - `InnocentUtilityAI`: re-evaluate ทุก ~1.5 วิต่อตัว → execute ทุก tick จนรอบถัดไป
    (IdleWander = baseline 0.1, SeekFood = Hunger>55 → เดินไปโซน loot มี food แล้วกิน
    จาก inventory, InvestigateNoise/FleeToSafeZone = stub score 0)
  - `KillerPlanner` (state machine ต่อ killer): Patrolling → SeekingWeapon →
    SeekingOpportunity → Executing → BuildingAlibi (timeout 30 วิ → Patrolling)
    - cooldown นับถอยใน planner ทุก tick ไม่ใช่เฉพาะตอนพยายามฆ่า (semantics ย้ายครบ)
    - ฆ่าผ่าน `TryEliminate/CanEliminate` เท่านั้น (เช็คซ้ำ dry-run ก่อนลงมือ)
      HasWeapon (Category==Weapon ใน inventory) เป็นเงื่อนไขก่อน Executing —
      economy-agnostic ตาม design decision, อาวุธไม่ถูกหักตอนฆ่า (TODO durability)
- **4.3 cleanup**: `TryAttemptElimination` (สุ่ม 15%/tick) + call site ถูกลบ
  (grep ยืนยันไม่มี caller หลงเหลือ)
- **⚠️ DI cycle ที่ต้องเลี่ยง**: NpcDirectorSystem → AI → UtilityContext →
  NpcDirectorSystem จะ cycle ถ้า context resolve director ตอน build — ทางแก้:
  `UtilityContext(data, stateProvider)` ไม่มี director แล้ว `NpcDirectorSystem`
  **Bind(this)** เข้า context ใน constructor ของตัวเอง (จุดเดียวของเกม) AI อ่าน
  `ctx.NpcDirector` ตอน Tick เท่านั้น (หลัง construct จบเสมอ)
- DI: `UtilityContext / InnocentUtilityAI / KillerPlanner` เป็น Lifetime.Singleton
  ใน GameLifetimeScope.Configure() — NpcDirectorSystem รับสมองผ่าน constructor
  (ห้าม new เอง)
- `NpcInventory.GetCardIds()` เพิ่ม (อ่านอย่างเดียว) สำหรับ HasWeapon — ห้ามใช้สร้าง
  MCP response (ground truth ไม่หลุด)
- **4.4 runtime tests (Play Mode จริง 2026-09-10) — ผ่านทั้งหมด**:
  - A ✅ hunger=90 → `[InnocentAI] chose SeekFood (score=0.90)` ทันทีหลัง re-eval,
    action คงอยู่ข้าม tick (กัน jitter ได้จริง)
  - B ✅ killer+เหยื่อ อยู่โซนเดียวกัน (พยานถูกย้ายออก + **pin target กันหลุดโซน**
    ระหว่างกรอบเวลาเทส) → `eliminated npc_02` + `clue_blood_stain` + chibi despawn
    ผ่าน message flow, cooldown reset ~180 (เหลือ 178 หลัง 2 วิ), phase → BuildingAlibi
  - C ✅ มีพยาน (pinned ในโซนเดียวกัน) → `abort (CanEliminate: witnessed)` →
    **ไม่มีใครตาย** ตลอดกรอบเวลา 2 วิ — กฎ no-witness ไม่ถูก bypass
  - D ✅ ผู้เล่นแทงผ่าน UseCardHandler (flow เดียวกับ AI):
    มีพยาน → `Success=False FailureReason=witnessed` (การ์ดไม่ถูกหัก — Safe UX),
    ไม่มีพยาน → `Success=True ResultText=eliminated_npc_01`
  - E ✅ การเดินลื่น (ตัวอย่าง ~1.4 world unit / 2 วิ ≈ MovementSpeed 2.0),
    ไม่มี teleport/แย่ง target หลัง deprecate PickNextTarget
  - F ✅ diff response shape: `get_game_state`/`get_visible_npcs` ไม่มี
    plan/phase/cooldown/inventory/Position leak แม้แต่ field เดียว
    (MessagePack JSON ตรง wire shape — killer ถือ knife_basic แต่ visible ไม่เห็น)
- **Bug ที่เจอตอนเทส (แก้แล้วใน KillerPlanner)**:
  1. BuildingAlibi เลือกทางหนีใหม่ทุกเฟรม (MoveNpc seed Target=Position →
     HasPendingTarget=false ทันที) = teleport storm spawn/despawn รัวๆ — แก้:
     เลือกทางหนี **ครั้งเดียวตอน Transition เข้า phase** ระหว่างรอเดินวนในโซนเดิม
  2. abort (witnessed) → หาเหยื่อใหม่ทันทีทุกเฟรม = log spam 2 บรรทัด/เฟรม — แก้:
     เพิ่ม `OpportunityRetrySeconds` (2 วิ) พักก่อนหาโอกาสใหม่
- **บทเรียนเทส**: witness ที่ปล่อยให้เดินอิสระจะทอยข้ามโซนหลุดจากโซนฆ่าตลอดเวลา
  (zone move = instant ผ่าน MoveNpc) ทำให้ kill จริงเป็น no-witness ถูกกฎหมด —
  เทส forced scenario ต้อง **pin target ของพยานในโซน** ก่อนเปิดกรอบเวลา

## สถานะปัจจุบัน
- ✅ Step 1: NpcState Key 9-15 + NpcInventory + NpcSurvivalState + seeding rule + แจกอาวุธ
- ✅ Step 2: NpcMovementSystem (wander + ข้ามโซน 20% + PickNextTarget แยก method) + tick order
- ✅ Step 3: NpcCharacterView sync position/facing/anim + SpawnChibi wiring + idle pause
- ✅ Step 4 (4.1-4.3): สมอง AI แยก role (InnocentUtilityAI + KillerPlanner) —
  deprecate PickNextTarget แล้ว, idle-pause ถูกถอดไปกับ re-target logic เดิม
  (AI ตั้ง target ใหม่ได้ทันที — ถ้าต้องการ idle ค่อยทำเป็น action ของ utility AI)
- ✅ Step 4.4: runtime tests A-F ผ่านครบ (2026-09-10) — รายละเอียดด้านบน
- ❌ การเดินยังไม่เช็คชนกับสิ่งกีดขวาง / ขอบเขตโลก (wander clamp ที่รัศมีโซนพอ)
- ⚠️ พบว่า scene ถูกแก้ภายนอก: npcPrefabs[0]/[1] ชี้ prefab เดียวกับ fallback
  (001 Student 1) ทำให้ NPC ทุกตัวหน้าตาเหมือน fallback — ไม่ใช่ regression จาก Step 3
