---
title: Chibi Visual System (Lab B)
type: architecture
related:
  - "[[ChibiSpawnerView.cs]]"
  - "[[GenericCuteVisualController.cs]]"
  - "[[IChibiVisual.cs]]"
  - "[[SpineVisualController.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[NpcLocationChangedMessage]]"
  - "[[PlayerLocationChangedMessage]]"
created: 2026-09-06
tags:
  - architecture
  - marooned
  - lab-b
  - chibi
---

# Chibi Visual System (Lab B)

## Overview
ระบบ spawn/คุม visual ของตัวละคร Chibi บนจอ โดยอิงหลัก **event-driven**
(MessagePipe) + **VContainer Resolve** + **MVP Lite** — แยก Logic (Systems) ออกจาก
Visual (Views) ชัดเจน

## Flow (ทั้งหมดไม่มี polling)

```
[Logic layer — Systems]
RoundInitializer.Start
  └─ NpcDirectorSystem.SetupRound → MoveNpc(npcId, loc) ──┐ publish
ExplorationSystem.Explore ────────────────────────────────┤ publish
MoveToLocationHandler ────────────────────────────────────┤ publish
                                                          ▼
                    NpcLocationChangedMessage / PlayerLocationChangedMessage
                                                          │ in-process MessagePipe
[Visual layer — Views]                                    ▼
ChibiSpawnerView.ReconcileChibis
  ├─ NPC มีชีวิต && อยู่ location เดียวกับ player → PickPrefab(npcId) → Instantiate
  │    └─ GetComponent<IChibiVisual>().Bind(NpcActivityState)
  │         ├─ GenericCuteVisualController (default) → Animator.Play("idle"/"walk"/"interact")
  │         └─ SpineVisualController (experimental)  → AnimationState.SetAnimation(0, name)
  └─ NPC ตาย / ย้ายไป location อื่น / ถูกลบ → Destroy
```

## Scene Setup (SampleScene — ตั้งค่าแล้ว ณ 2026-09-06)
- `GameLifetimeScope/GameManager` — RoundInitializer + GameTickDriver (Lab A)
- `GameLifetimeScope/ChibiSystem` — ChibiSpawnerView, `backend` = GenericCute
  (default), `genericCutePrefab` = `001 Student 1 Character.prefab`,
  `npcPrefabs[]` = [WizardChibi, CollegeStudentChibi] (Lab B Phase 3 — Student 1
  สงวนให้ Player เท่านั้น NPC ห้ามใช้), `spinePrefabs[]` = [ElenaChibi, DerekChibi]
- `GameLifetimeScope/GameManager/PlayerCharacter` — PlayerCharacterView +
  `visualPrefab` = `001 Student 1 Character.prefab` (ตัวผู้เล่น ไม่ใช่ NPC)
- Prefab `001 Student 1 Character.prefab` — Animator ผูก `Basic.controller` +
  GenericCuteVisualController (แก้ไข prefab ของ asset โดยตรง)
- Prefab variants `Assets/Scripts/Core/Visual/Prefabs/ElenaChibi.prefab` +
  `DerekChibi.prefab` — SkeletonAnimation ของ Elena/Derek + SpineVisualController,
  scale 0.3 (skeleton ~1500 unit ย่อให้พอดีจอ)

## Message Contracts (เพิ่มใน Shared/GameMessages.cs ตอน Lab B)
- `PlayerLocationChangedMessage { OldLocationId, NewLocationId }` — publish จาก
  `ExplorationSystem.Explore()` และ `MoveToLocationHandler` (จุด mutate
  `Player.CurrentLocationId` ทั้งสองจุดของโปรเจกต์)
- `NpcLocationChangedMessage { NpcId, OldLocationId, NewLocationId }` — publish จาก
  `NpcDirectorSystem.MoveNpc()` ซึ่งเป็น **จุดเดียว** ที่อนุญาตให้แก้
  `NpcState.CurrentLocationId` (RoundInitializer ใช้ตอนวางตำแหน่งเริ่มต้นด้วย)

## Test Evidence (Play Mode 2026-09-06)
**Phase 1 (GenericCute):**
```
[1] เริ่มเกม: player @ beach, get_visible_npcs = 5 ตัว, chibi บนจอ = 5
[2] move_to_location(jungle_edge) → chibi บนจอ = 0
[3] move_to_location(beach) → chibi บนจอ = 5
```
+ ภาพจริงจาก Game View: chibi Student 1 ปรากฏ 4–5 ตัวที่ beach (offset กันซ้อน)

**Phase 2 (dual backend — ผ่านทั้ง 3 เคส):**
```
Test A (backend=Spine): spawn 5 → สลับ ElenaChibi/DerekChibi(Clone) ถูกต้อง
  move jungle_edge → 0, move beach → 5 ครบ
Test B (บังคับ state ทั้ง 5 ตัว): current=Idle → Traveling=Walking → Talking=Talking
  (ยืนยันผ่าน AnimationState.GetTrack(0) ทั้ง Elena และ Derek)
Test C (regression, backend=GenericCute): 5 → 0 → 5 หน้าตาเหมือนเดิมทุกตัว
```
+ ภาพจริงทั้งสอง backend (Spine เห็น 2 หน้าตาสลับกัน / GenericCute เหมือนกันหมด)

## Dual-Backend Abstraction (Lab B Phase 2 — 2026-09-06, experimental)

```
ChibiSpawnerView ──(IChibiVisual)──┬── GenericCuteVisualController (Animator)  ← default
                                   └── SpineVisualController (SkeletonAnimation)
```

- **IChibiVisual** (`Marooned.Core.Visual`) — interface กลาง `Bind(NpcActivityState)` /
  `SetFacing(bool)` / `Transform` / `PlayPickup()` (Lab B Phase 3) /
  `PlayAction(string)` (Phase 4 — one-shot action เช่น "attack", "use_item"; spawner
  พึง interface เท่านั้น ไม่รู้จัก backend concrete (`GetComponent<IChibiVisual>()` หลัง Instantiate)
- **ChibiBackend enum** — { GenericCute, Spine } SerializeField บน ChibiSpawnerView;
  default = GenericCute (ห้ามเปลี่ยนพฤติกรรมเดิม)
- **Character rotation** — backend Spine วน prefab ตามเลขท้าย npc id:
  `npc_01→spinePrefabs[0], npc_02→[1], npc_03→[0], ...` (modulo) เพื่อหน้าตาไม่ซ้ำกัน;
  array ว่าง → fallback กลับ GenericCute
- **Controller ตัวเดียวทั้ง Elena/Derek** — logic เหมือนกัน, ต่างแค่ SkeletonDataAsset
  ที่มากับ prefab ต้นทาง (variant)

### การ map NpcActivityState → animation (ของจริงจาก asset ทั้งสองฝั่ง)

**Lab B Phase 3 update (2026-09-06):** state names ในตระกูล GenericCute **ไม่แชร์กัน** —
Student 1 (`Basic.controller`) = idle/walk/interact/pick up (ตัวพิมพ์เล็ก) ส่วน
Wizard (`Wizard Demo.controller`) และ CollegeStudent (`AnimationDemo.controller`)
= Idle/Run/Attack/Hurt/Die/Jump/KickBoard (ไม่มี interact / pick up) จึงย้ายชื่อ
state เป็น SerializeField ต่อ prefab ใน `GenericCuteVisualController` —
wrapper `WizardChibi.prefab` / `CollegeStudentChibi.prefab` ตั้ง idleAnim=Idle,
walkAnim=Run, interactAnim/pickupAnim ว่าง (`PlayIfAvailable` ข้าม state ที่ไม่มีให้เอง)
| NpcActivityState | GenericCute (Animator state) | Spine (Elena=Derek, 29 ชื่อเหมือนกัน) |
|---|---|---|
| Idle | `idle` | `Idle` |
| Resting | `idle` | `Idle` |
| Traveling | `walk` | `Walking` |
| Gathering | `walk` | `Walking` (ยังไม่มี animation เฉพาะ รอ schedule system) |
| Talking | `interact` | `Talking` |

หมายเหตุ: enum จริงของโปรเจกต์ไม่มี Walking/Dead — การตายคือ `IsAlive=false`
(despawn ที่ spawner) ไม่มี auto-play "Die"

### สิ่งที่ต้องระวังกับ Spine-Unity 4.3 (split component)
- `AnimationState.GetCurrent(track)` ถูกเปลี่ยนชื่อเป็น **`GetTrack(track)`**
- flip ด้วย `Skeleton.ScaleX` (ไม่แตะ Transform.localScale กันพัง mesh bounds)
- SpineVisualController มี fallback: ถ้าชื่อ animation ไม่มีจริงใน SkeletonData
  → log warning + เล่น "Idle" แทน (กัน SetAnimation โดนชื่อเดา)
- Awake log รายชื่อ animation ทั้งหมด 1 ครั้งต่อ instance เผื่อ asset อนาคต
  ต่างชื่อจาก Elena/Derek

### สถานะ: experimental
Spine backend เป็นฐานทดลอง runtime เผื่อย้ายไปใช้ Spine ในอนาคตเท่านั้น —
default ของเกมยังเป็น GenericCute และการตัดสินใจเปลี่ยน backend production
ต้องผ่านการอัปเดต GDD ก่อน

## Known Issues / Next Steps
- ~~ยังไม่มี visual ของผู้เล่น~~ (แก้แล้วใน Lab B Phase 3 — PlayerCharacterView +
  PlayerMovementSystem เดินด้วย WASD ได้ ดู [[player-system]]) แต่ยังไม่มี
  สภาพแวดล้อม (tilemap) — chibi ยังลอยบนพื้นสีพื้นหลังกล้อง
- `NpcDirectorSystem.TickBehavior` ยังเป็น placeholder → NPC ยังไม่มีการเดินจริง
  (message การย้ายที่เกิดจริงตอนนี้มีจาก RoundInitializer + player movement)
- Asset ใช้ PSB skeletal (GenericCute) / Spine skeletal (Elena, Derek) — ต่างจาก
  ChibiAnimatedRenderer (frame-swap); การเลือก renderer กลางสำหรับ NPC หลายตัว
  (memory/perf) ยังเป็นโจทย์ Lab ถัดไป
- การเชื่อม McpBridge จริง (TCP) กับ get_visible_npcs ยังต้องทดสอบแยก (Lab B ทดสอบ
  ผ่าน handler in-process เท่ากับ code path เดียวกันข้ามชั้น TCP)
- Spine backend ยัง experimental — ยังไม่ผูก roster จริง (npc_01..05 ที่เวียนสลับ
  Elena/Derek เป็น mock จนกว่าจะมี Luban NpcDef table)
- ~~Facing ยังไม่มีใครเรียก `SetFacing`~~ (แก้แล้วใน Lab B Phase 3 —
  PlayerMovementSystem ตั้ง `FacingRight` ใน state แล้ว PlayerCharacterView เรียก
  `SetFacing` ทุกเฟรม; ฝั่ง NPC ยังไม่มีใครเรียก)
