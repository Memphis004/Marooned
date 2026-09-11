---
title: npc-zone-transitions
type: architecture
sources:
  - DataTables/Data/ZoneConnectionDef.csv
  - DataTables/Defines/schema.xml
  - Marooned/Assets/Scripts/Systems/NpcZoneTransitionSystem.cs
  - Marooned/Assets/Scripts/Systems/AI/Wander.cs
  - Marooned/Assets/Scripts/Core/GameTickDriver.cs
  - Marooned/Assets/Scripts/Core/Visual/NpcCharacterView.cs
  - Marooned/Assets/Scripts/Systems/GameStateProvider.cs
  - Shared/NpcState.cs
related:
  - "[[npc-embodiment-movement]]"
  - "[[NpcZoneTransitionSystem.cs]]"
  - "[[Wander.cs]]"
  - "[[GameTickDriver.cs]]"
  - "[[NpcCharacterView.cs]]"
  - "[[chibi-visual-system]]"
  - "[[npc-director]]"
folder: architecture
created: 2026-09-11
tags:
  - architecture
  - npc
  - zone-transition
  - lab-c
  - marooned
---

# NPC Zone Transitions — Hybrid Transition Points (Lab C Phase 2, Part 1+2)

## ภาพรวม (Gameplay Perspective)

ก่อนหน้านี้ NPC "ข้ามโซน" ด้วยการ teleport (`MoveNpc` ทันที — หายจากจอแล้วโผล่
กลางโซนใหม่) ทำให้ตาเห็นว่า NPC หายตัวลึกลับ ระบบ **Hybrid Transition Points**
ทำให้ NPC **เดินผ่านจุดเชื่อม (transition point) ของแผนที่จริง**:

```
เดินอิสระในโซน ──AI สั่งข้ามโซน──▶ เดินเข้าหาจุดเชื่อม ──ถึงจุดเชื่อม──▶
      ▲                                                    เดินออกนอกจอ 0.5 วิ
      │                                                            │
      └──กลับสู่การเดินปกติ◀──เดินเข้าจากจุดเชื่อมฝั่งใหม่ 0.3 วิ◀──ข้ามโซน
```

ผู้เล่นเห็น NPC เดินไปที่ขอบแผนที่ ออกนอกจอ แล้วเดินเข้ามาจากขอบโซนปลายทาง —
ตัดสินใจ social deduction ได้จากสิ่งที่ตาเห็นจริง ("npc_02 เดินออกทางป่าฝั่ง
deep jungle เมื่อกี้")

## ข้อจำกัดสถาปัตยกรรม (เคร่งครัด — ระบบนี้ยึดทั้งหมด)

1. **`Wander.cs` เป็น single source of truth** — ทั้ง `InnocentUtilityAI` และ
   `KillerPlanner` เรียก helper เดียวกัน (`IsTransitioning` / `MoveToZone`)
2. **Plain C# ไม่มี Coroutine** — `NpcZoneTransitionSystem` นับถอยเวลา anim ด้วย
   `Dictionary<string, float>` ต่อ NPC
3. **System ห้ามแตะ GameObject/View** — เขียนเฉพาะ ground truth fields
   (`TransitionPhase` / `PendingTransitionTargetZoneId` / Position / Target)
   แล้ว `NpcCharacterView` ตรวจจับ phase เปลี่ยนเอง
4. **Reuse `IChibiVisual.PlayAction(string)`** — ไม่เพิ่ม interface method ใหม่
   (`"zone_exit"` / `"zone_enter"`; backend ที่ไม่มี state นี้จะข้ามเงียบ ๆ)
5. **`MoveNpc()` ยังเป็นจุดเดียวที่แก้ `CurrentLocationId`** — ระบบข้ามโซนเรียก
   `MoveNpc` แล้วใช้ escape hatch: override `PositionX/Y` หลังเรียก
   (Position Seeding Rule เดิม ดู [[npc-embodiment-movement]])
6. **Tick order ตายตัว** — Director → Movement → ZoneTransition (ดูด้านล่าง)

## Task 1 — Data Layer: `ZoneConnectionDef`

### CSV (`DataTables/Data/ZoneConnectionDef.csv`)

ตาราง Luban แบน 3 header (`##var` / `##type` / `##`) — 1 แถว = ทิศเดียวของ
จุดเชื่อม จึงต้องประกาศ **คู่สวนกันเสมอ**:

| id | from → to | transition point |
|----|-----------|------------------|
| `beach_to_jungle_edge` | beach → jungle_edge | (8, 4) |
| `jungle_edge_to_beach` | jungle_edge → beach | (2, 1) |
| `jungle_edge_to_deep_jungle` | jungle_edge → deep_jungle | (18, 9) |
| `deep_jungle_to_jungle_edge` | deep_jungle → jungle_edge | (12, 6) |
| `beach_to_cave_entrance` | beach → cave_entrance | (−4, 6.4) |
| `cave_entrance_to_beach` | cave_entrance → beach | (−1, 1.6) |

### ⚠️ Coordinate Convention — "80% ทางจากตัวโซนต้นทางไปหาโซนปลายทาง"

พิกัด `transitionX/Y` ใน CSV **ไม่ใช่ค่าสุ่ม** — derive จาก `WorldX/WorldY` จริง
ใน `LocationDef.csv` ด้วยกฎเดียว:

> จุดเชื่อม = จุดที่อยู่ **80% ของระยะ** จาก "จุดกึ่งกลางโซนต้นทาง" ไปหา
> "จุดกึ่งกลางโซนปลายทาง"

```
ตัวอย่าง beach (0,0) → jungle_edge (10,5):
  point = (0 + 0.8·(10−0), 0 + 0.8·(5−0)) = (8, 4)
```

**ทำไมต้องกฎนี้:** ถ้าคำนวณสองทิศด้วยกฎเดียวกัน จุดเชื่อมของทั้งสองแถว
(A→B และ B→A) จะ **ทับกันเป็นจุดเดียวบนขอบแชร์กัน** — ระบบทั้งโซนรับรู้ตรงกันว่า
"ช่องว่างระหว่างโซน" คือจุดเดียวกัน เช่น beach↔jungle_edge: 8·(1−0.8)=2 จากฝั่ง
jungle_edge ก็ได้ (2,1) พอดี เดินออกจาก (8,4) แล้วเดินเข้าที่ (2,1) = ผ่าน
"ช่องเดียวกัน" จากสองฝั่ง

- ปรับค่าใน CSV ได้เสรี (เช่น cave_entrance เยื้อง ๆ จึงเป็น (−4, 6.4)) — แค่รักษา
  จุดสองฝั่งให้สมเหตุสมผลกับภูมิประเทศ
- แก้ CSV แล้วรัน `bash DataTables/gen.sh` เสมอ (generate `cfg.game.ZoneConnectionDef`
  + `TbZoneConnectionDef` + JSON ที่ `Assets/Resources/DataTables/`)
- **ห้ามสร้าง mirror class ใน `Shared/`** — ใช้ `cfg.game.ZoneConnectionDef` ที่
  Luban generate ตรง ๆ

### Service access (`LubanDataService` ใน `GameStateProvider.cs`)

```csharp
// index คู่โซน (from,to) → def — สร้างครั้งเดียวตอน LoadAll()
// คู่ซ้ำใน CSV = config error ที่โยนตอน boot (จับตั้งแต่เปิดเกม)
_zoneConnectionsByPair = tables.TbZoneConnectionDef.DataList
    .ToDictionary(c => (c.FromLocationId, c.ToLocationId));

// API เดียวที่ระบบอื่นเรียก — null ถ้าคู่นี้ไม่มีจุดเชื่อมใน CSV
public cfg.game.ZoneConnectionDef GetTransition(string from, string to)
```

## Task 2 — NpcState: Ground Truth Fields (Key 16-17)

ต่อจาก keys 9-15 ของ [[npc-embodiment-movement]]:

| Key | Field | ความหมาย |
|-----|-------|----------|
| 16 | `TransitionPhase` | enum `NpcTransitionPhase` — phase ปัจจุบันของ FSM (default `None`) |
| 17 | `PendingTransitionTargetZoneId` | โซนปลายทางระหว่าง transition (null เมื่อว่าง) |

```csharp
public enum NpcTransitionPhase { None, WalkingToPoint, Exiting, Entering }
```

**Information Hiding:** ทั้งสอง field เป็น ground truth — **ห้ามหลุดเข้า
`NpcObservableView` /`GetObservableNpcsAt` / MCP response เด็ดขาด** (GDD §6.3) ผู้เล่นรู้ได้แค่ "เห็น NPC เดินออกนอกจอ" จาก position/animation เท่านั้น ไม่รู้ว่า
ระบบภายในกำลัง transition อยู่ การ์ด `GetObservableNpcsAt` project field-by-field
จึงกัน leak โดยโครงสร้างอยู่แล้ว (ยืนยันด้วย EditMode test แบบ reflection)

## Task 3 — Wander.cs: จุดตั้งต้นของ transition

```csharp
// gate เดียวของทุก AI — เช็คก่อนตั้ง target ใหม่ทุกครั้ง
public static bool IsTransitioning(NpcState npc) =>
    npc.TransitionPhase != NpcTransitionPhase.None;
```

`MoveToZone(npc, ctx, destinationId, rng, requireConnection = true)` ลำดับตัดสินใจ:

1. `IsTransitioning` → false (ไม่แย่ง target กลาง transition)
2. โซนเดียวกัน → `SetRandomTargetInZone` (wander ในโซน รัศมี 2.5)
3. `requireConnection` → เช็คว่า destination อยู่ใน `ConnectedLocationIds` จริง
4. **`GetTransition(from,to)` เจอจุดเชื่อม** → ตั้ง target ที่ transition point +
   `Activity=Traveling` + `TransitionPhase=WalkingToPoint` +
   `PendingTransitionTargetZoneId=destination` — จบที่นี่ ไม่ข้ามโซนเอง
5. **ไม่เจอ (คู่ไม่อยู่ใน CSV)** → fallback teleport ผ่าน `MoveNpc` แบบเดิม
   (คู่ที่ยังไม่ประกาศจุดเชื่อมไม่ error — แค่ไม่มี animation)

`MoveToRandomConnectedZone` delegate เข้า `MoveToZone` ทั้งหมด

## Task 4 — NpcZoneTransitionSystem: FSM

Plain C# singleton (VContainer `Lifetime.Singleton`), tick โดย `GameTickDriver`:

```
None ──(Wander.MoveToZone ตั้ง target=จุดเชื่อม)──▶ WalkingToPoint
WalkingToPoint ──(Movement เดินถึง ≤ ArrivalThreshold 0.1)──▶ Exiting (0.5 วิ)
Exiting ──(timer หมด)──▶ [MoveNpc + override ตำแหน่ง = arrival point] ──▶ Entering (0.3 วิ)
Entering ──(timer หมด)──▶ None
```

- **Timer = `Dictionary<string, float>`** ต่อ NPC (ข้อจำกัด #2) — ถ้า entry หาย
  ระหว่างทางจะผ่านเฟสทันที (กันค้างตลอดกาล)
- **`CompleteExit`** (หัวใจของการข้ามโซน):
  ```csharp
  _npcDirector.MoveNpc(npc.Id, targetZone);        // จุดเดียวที่แก้ CurrentLocationId
  var arrival = _data.GetTransition(targetZone, previousZone);
  if (arrival != null) {                            // escape hatch หลัง MoveNpc
      npc.PositionX = arrival.TransitionX;          // วางที่จุดเชื่อมฝั่งปลายทาง
      npc.TargetX = npc.PositionX;                  // Target=Position → ยืนนิ่งระหว่าง enter
  }
  ```
  `MoveNpc` seed กลางโซน แล้วเรา override เป็นจุดเชื่อมฝั่งใหม่ — chibi จึง
  **เดินเข้าจากขอบ ไม่โผล่กลางโซน** (ยัง publish `NpcLocationChangedMessage` ตาม
  event flow เดิมของ [[ChibiSpawnerView.cs]] — despawn/spawn ทำงานเอง)
- **ตายกลาง transition** → เคลียร์ phase/pending/timer ทันที (ไม่ข้ามโซน ไม่ค้าง)

## Tick Order (ตายตัว — ใน `GameTickDriver.Update`)

```
... playerInput → survival → playerMovement → pickup → nodeHarvest
→ NpcDirectorSystem.Tick    (AI ตั้ง target — รวมถึงเริ่ม transition)
→ NpcMovementSystem.Tick    (เดิน Position เข้าหา Target)
→ NpcZoneTransitionSystem.Tick   ⚠️ หลัง Movement เสมอ
→ WorldEventSystem.Tick
```

เหตุผล: Movement ต้องเดินก่อน ระบบ transition ถึงจะเห็นว่า "ถึงจุดเชื่อมแล้ว"
ในเฟรมเดียวกัน — ถ้าสลับ การข้ามโซนจะดีเลย์ 1 เฟรมทุกครั้ง

## View Layer (`NpcCharacterView`)

ทุกเฟรม view sync จาก ground truth (direct read — ไม่ใช้ MessagePipe ต่อเฟรม):

- จับ **phase เปลี่ยน** (`_lastPhase`): `Exiting` → `PlayAction("zone_exit")`,
  `Entering` → `PlayAction("zone_enter")` (ครั้งเดียวต่อการเปลี่ยน)
- ระหว่าง `Exiting`/`Entering` → **ข้าม facing + `Bind(activity)`** (ตัวละครกำลัง
  เล่น one-shot anim ออก/เข้าจอ ไม่ควร flip ทิศหรือเปลี่ยน anim ทับ)
- การ spawn/despawn ข้ามโซนเป็นหน้าที่ของ `ChibiSpawnerView` ตาม
  `NpcLocationChangedMessage` เดิม — view ตัวนี้ไม่ยุ่ง

## การเทส (หลักฐานทั้งหมดผ่านครบ)

### EditMode (6/6 ผ่าน — deterministic, ไม่ต้องกด Play)
ตระกูล test: `Marooned/Assets/Tests/Editor/NpcZoneTransitionEditModeTests.cs`
สร้าง VContainer container จริง + Luban จริง + drive FSM ตรง:

| Test | สิ่งที่ยืนยัน |
|------|----------------|
| FullCycle | เดินถึงจุดเชื่อม → exit 0.5 วิ → ข้าม → ลง exactly ที่ arrival point (2,1) → enter 0.3 วิ → None, `MoveNpc` publish ครั้งเดียว |
| AiGuard | InnocentUtilityAI + KillerPlanner ตั้ง target/ติ๊ก cooldown ไม่ได้ระหว่าง transition |
| FallbackTeleport | คู่ที่ไม่มีใน CSV (beach→deep_jungle) teleport ได้ ไม่ error, phase คง `None` |
| DeathMidTransition | ตายกลาง transition → phase/pending/timer เคลียร์ ไม่ crash |
| NoLeak + DataIntegrity | reflection พิสูจน์ `NpcObservableView` ไม่มี field transition + ทุก adjacent pair มีแถวสองทิศครบ |

### PlayMode (ผ่าน — visual evidence ใน scene จริง)
`Marooned/Assets/Tests/Runtime/NpcZoneTransitionPlayModeTests.cs` (asmdef
`Marooned.Tests.Runtime` — UTF เข้า Play เอง) โหลด `SampleScene` จริง, บังคับ
transition ผ่าน `Wander.MoveToZone` (path เดียวกับ AI), sample ทุกเฟรม + ถ่าย 3
ภาพ หลักฐานอยู่ที่ `TestEvidence/lab-c-phase2-zone-transition/`:

```
[Test A] state walked: True / chibi moved on screen: True
[Test B] Exiting t=4.71s → crossed t=5.22s → back to None t=5.52s
[Test B] arrival point distance: 0.000 (บังคับ ≤ 0.05)
[Test B] chibi despawned after cross: True / AI resumed: True
RESULT: PASS  (npc_02 beach → jungle_edge, 6.24 วิ)
```

⚠️ บทเรียนโครงสร้าง assembly: UTF ค้นเจอ PlayMode test เฉพาะใน **runtime
assembly** — ต้องมี `Marooned.Game.asmdef` (game code) +
`Marooned.Tests.Runtime.asmdef` (อ้าง `Marooned.Game` ได้ และตั้ง
`autoReferenced: false` กัน circular reference) asmdef อ้าง `Assembly-CSharp`
ตรง ๆ **ไม่ได้** (Unity ทิ้ง reference เงียบ ๆ)

## ไฟล์ที่เกี่ยวข้องทั้งหมด

| ไฟล์ | หน้าที่ |
|------|---------|
| `DataTables/Data/ZoneConnectionDef.csv` | จุดเชื่อม 6 แถว (3 คู่สวน) |
| `DataTables/Defines/schema.xml` | bean + table declaration ของ ZoneConnectionDef |
| `Marooned/Assets/Scripts/Systems/GameStateProvider.cs` | `LubanDataService.GetTransition(from,to)` + pair index |
| `Shared/NpcState.cs` | Key 16-17 + enum `NpcTransitionPhase` (canonical — รัน `./sync-shared.sh` ทุกครั้งที่แก้) |
| `Marooned/Assets/Scripts/Systems/AI/Wander.cs` | `IsTransitioning` / `MoveToZone` / fallback teleport |
| `Marooned/Assets/Scripts/Systems/NpcZoneTransitionSystem.cs` | FSM ข้ามโซน (plain C#) |
| `Marooned/Assets/Scripts/Systems/AI/InnocentUtilityAI.cs` + `KillerPlanner.cs` | gate ด้วย `IsTransitioning` ที่ต้น `Tick()` |
| `Marooned/Assets/Scripts/Core/GameTickDriver.cs` | tick order Director → Movement → ZoneTransition |
| `Marooned/Assets/Scripts/Core/Visual/NpcCharacterView.cs` | จับ phase เปลี่ยน → `PlayAction("zone_exit"/"zone_enter")` |
| `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs` | register `NpcZoneTransitionSystem` singleton |
