---
title: player-auto-move-system
type: architecture
sources:
  - Marooned/Assets/Scripts/Systems/PlayerAutoMoveSystem.cs
  - Marooned/Assets/Scripts/Systems/PlayerMovementSystem.cs
  - Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs
  - Marooned/Assets/Scripts/Systems/McpMainThreadDispatcher.cs
  - Shared/PlayerSurvivalState.cs
  - Marooned/Assets/Scripts/Core/GameTickDriver.cs
  - Marooned/Assets/Scripts/Core/GameLifetimeScope.cs
  - Marooned/Assets/Tests/Runtime/MoveToLocationPlayModeTests.cs
  - Marooned/Assets/Tests/Runtime/BridgeRoundTripPlayModeTests.cs
related:
  - "[[player-system]]"
  - "[[npc-embodiment-movement]]"
  - "[[mcp-bridge]]"
  - "[[PlayerMovementSystem.cs]]"
  - "[[McpRequestHandlers.cs]]"
  - "[[GameTickDriver.cs]]"
folder: architecture
created: 2026-09-12
tags:
  - architecture
  - marooned
  - lab-c
  - player
  - mcp
---

# Player Auto-Move System (MoveToLocation — เดินจริง ไม่ Teleport)

## ภาพรวม (Gameplay Perspective)

เมื่อ AI VTuber สั่ง `move_to_location` ผู้เล่นต้อง**เดิน**จากจุดปัจจุบันไปยัง
`WorldX/WorldY` ของปลายทางจริง ๆ (เห็นบนจอ ใช้เวลาเท่าระยะทาง/ความเร็ว)
แล้วโซน (CurrentLocationId), biome ฉาก, ชุด chibi NPC และ loot table ค่อยเปลี่ยน
**หลังเดินถึงเท่านั้น** — ไม่ใช่ teleport เปลี่ยนโซนทันทีแบบเดิม

ระหว่างเดินอัตโนมัติ คีย์บอร์ด (WASD) **ถูกล็อก** — กันสองระบบแย่งเขียนตำแหน่ง

## ทำไมต้องแยกระบบ (Single Source of Truth)

`PlayerMovementSystem` อ่านคีย์บอร์ด ส่วน auto-move อ่าน state ล้วนๆ — ถ้าทำอยู่
ไฟล์เดียวกันจะซับซ้อน จึงแยกเป็น `PlayerAutoMoveSystem` และกันการแย่งด้วย flag
`IsAutoMoving`:

- `IsAutoMoving == true` → `PlayerMovementSystem.Tick()` **return ทันที**
  (`PlayerInputService` ยัง Tick ปกติ แค่ค่า `MoveAxis` ไม่ถูกใช้)
- ต่อเฟรมมี "ระบบเดียว" เขียน `PositionX/PositionY` เสมอ

pattern เดียวกับ [[npc-embodiment-movement]]: การเลือกปลายทาง (handler/MCP) แยกจาก
การเดิน (system เดินอย่างเดียว)

## Data Flow

```
[MCP] move_to_location(locationId)  ← AI VTuber ผ่าน McpBridge (TCP 3216)
   ▼
[Handler] MoveToLocationHandler.InvokeAsync (3 เฟส)
   เฟส 1 (main thread): validate (unknown_location / not_connected) →
     player.TargetX/Y = clamp(LocationDef.WorldX/Y, WorldBounds)
     player.FacingRight = TargetX > PositionX
     player.IsAutoMoving = true          ← ยังไม่แตะ CurrentLocationId
   เฟส 2: await WaitUntilAsync(() => !player.IsAutoMoving, MoveTimeoutSeconds)
   เฟส 3 (main thread): ถึงแล้ว → CurrentLocationId = locationId + publish
     PlayerLocationChangedMessage → ChibiSpawnerView/BiomeScatterView เปลี่ยนฉาก
     (timeout → ปลด flag + Activity = Idle + คืน move_timeout)
   ▼
[System] PlayerAutoMoveSystem.Tick(dt)   (plain C# singleton — GameTickDriver เรียก)
   - !IsAutoMoving → return ทันที
   - ระยะ ≤ 0.1f → IsAutoMoving = false, Activity = Idle → return
   - เลื่อน PositionX/Y เข้าหา TargetX/Y ด้วย Speed = 3.5 (normalize +
     กัน overshoot, frame-rate independent — สูตรเดียวกับ NpcMovementSystem)
   - FacingRight ตาม dx (top-down lite: flip เฉพาะซ้าย/ขวา)
   - Activity = Traveling
   ▼
[View] PlayerCharacterView.Update()      (MVP Lite — passive)
   - transform.position = state ตรงทุกเฟรม (ไม่มีอะไรแก้ — ใช้ pipeline เดิม)
```

## MessagePack Contract (Shared/PlayerSurvivalState.cs)

Keys ใหม่ต่อจาก 0-14 เดิม (**ห้ามชน**) — แก้ที่ `Shared/` เท่านั้นแล้วรัน
`./sync-shared.sh` (Unity + McpBridge ได้ไฟล์เดียวกัน):

| Key | Field | ความหมาย |
|-----|-------|----------|
| 15 | `TargetX` | จุดหมายการเดินอัตโนมัติ (ตั้งโดย handler, อ่านโดย auto-move) |
| 16 | `TargetY` | 〃 |
| 17 | `IsAutoMoving` | true = กำลังเดินอัตโนมัติ (movement งดรับคีย์บอร์ด) |

## DI Wiring + Tick Order

- `GameLifetimeScope.Configure()`: `builder.Register<PlayerAutoMoveSystem>(Lifetime.Singleton).AsSelf();`
- `GameTickDriver.Update()` ลำดับเต็ม:
  `input → survival → **auto-move** → movement → pickup → harvest → npcSurvival → npcDirector → npcMovement → npcZoneTransition → worldEvent`
- เหตุผลที่ auto-move อยู่**ก่อน** movement: เฟรมที่ auto-move ปิด flag,
  movement กลับมารับคีย์บอร์ดได้ทันทีในเฟรมเดียวกัน (ไม่มีเฟรมเสีย)

## Redirect / Cancel กลางทาง (เพิ่ม 2026-09-12)

VTuber สั่งเดินใหม่ได้ระหว่างเดิน (redirect) และระบบยกเลิกได้โดยตรง (cancel)
ผ่านกลไก **generation token** บน PlayerAutoMoveSystem:

```csharp
public int MoveGeneration { get; }        // bump ทุกครั้งที่ Begin/Cancel
public AutoMoveEndReason LastEndReason { get; }  // Arrived / Cancelled
public int Begin(float targetX, float targetY)   // เริ่ม/แทนที่การเดิน — คืน gen ของการเดินนี้
public bool Cancel()                             // ยกเลิกทันที (คืน false ถ้าไม่ได้เดิน)
```

- **Begin = แทนที่เสมอ:** handler เรียก `Begin` แทนการ set flag ตรง —
  การเดินใหม่ bump generation → waiter (handler) ของการเดินเก่ารู้ตัวทันที
- **Waiter เทียบ generation:** predicate ของ WaitUntilAsync คือ
  `!IsAutoMoving || MoveGeneration != gen` — ถ้าโดนแทนที่ ออกจากการรอทันที
  (ไม่รอจนเดินครบ)
- **Commit guard:** ก่อนเปลี่ยนโซน handler เช็ค `MoveGeneration == gen`
  อีกครั้ง — ไม่ตรง = คืน `FailureReason = "superseded"` **ห้าม commit โซน**
  (กัน handler เก่าเปลี่ยนโซนทับคำสั่งใหม่)
- **⚠️ Arrival ห้าม bump generation** — เฉพาะ Begin/Cancel เท่านั้น ไม่งั้น
  การเดินถึงปกติจะโดนมองว่า superseded
- **Cancel() = หยุดตรงนั้น** (ไม่เปลี่ยนตำแหน่ง/โซน) + Activity = Idle +
  ปลดล็อกคีย์บอร์ดเฟรมถัดไป
- **Timeout path เดิมยังปลด flag** — `move_timeout` คืนเมื่อ 30 วิ ไม่มีอะไรจบการเดินเลย

### ทางเข้าถึงจาก MCP: tool `cancel_move` (เพิ่ม 2026-09-12)

- **Shared contract:** `CancelMoveRequest` / `CancelMoveResponse` ใน GameMessages.cs
  (Response: Success, WasMoving, Message, PositionX/Y, CurrentLocationId)
- **Unity handler:** `CancelMoveHandler` — เรียก `PlayerAutoMoveSystem.Cancel()`
  ผ่าน dispatcher, publish อะไรก็ตามที่ต้อง publish, คืน Success ต่อเมื่อ cancel สำเร็จ
- **Bridge:** tool `cancel_move` ใน SurvivalActionTools (Program.cs) —
  tools/list คืนชื่อ `cancel_move` พิสูจน์แล้วผ่าน stdio client จริง
- **เหตุผล:** redirect (สั่ง move ใหม่) เหมาะกับ "เปลี่ยนปลายทาง" แต่ "หยุดซะ"
  ควรเป็นคำสั่งของตัวเอง — ไม่งั้น VTuber ต้องปลอมๆ สั่งไปที่ตำแหน่งปัจจุบัน

## WaitUntilAsync (McpMainThreadDispatcher)

```csharp
public async UniTask<bool> WaitUntilAsync(Func<bool> predicate, float timeout = 30f)
```

- poll ทุกเฟรมผ่าน `UniTask.Yield(PlayerLoopTiming.Update)` — continuation กลับมา
  main thread เอง จึงอ่าน state ปลอดภัย แม้ caller อยู่บน TCP background thread
- จับเวลาด้วย `Stopwatch` (wall-clock) **ไม่ใช่ `Time.deltaTime`** เพราะ caller
  ฝั่ง TCP thread ห้ามแตะ `UnityEngine.Time`
- คืน `bool` — handler ใช้แยก "ถึงจริง" กับ "หมดเวลา" (`move_timeout` ปลด flag
  ไม่ให้ผู้เล่นติดล็อกคีย์บอร์ดตลอดไป)

## MCP Response Timing (สิ่งที่ VTuber เห็น)

- `move_to_location` **ตอบหลังเดินถึงเท่านั้น** — เวลา response ≈ ระยะทาง / 3.5
  (เช่น beach→jungle_edge ≈ 11.09 unit → ~3.2 วิ)
- ของเดิม (teleport) ตอบทันที — Python proxy เคยหน่วง `travel_time()` เอง
  ทำซ้ำซ้อน; ตอนนี้เกมเดินจริง จึงไม่ต้องหน่วงซ้ำฝั่ง proxy
- `MoveTimeoutSeconds = 30` (public — test ปรับได้)

## Test Evidence (PlayMode 2026-09-12 — Test Runner จริง)

```
Test A+B (MoveToLocation_WalksContinuously_AndArrives):
  start (0.08,0.04) → target (10,5) straight=11.09
  13 samples @0.25s: Traveling + auto=True + facingRight=True ทุกเฟรมระหว่างเดิน
  traveled=10.99 (≥60% ของ straight — teleport จะได้ ~0)
  ถึงแล้ว: PositionX/Y = WorldX/Y ±0.1, Activity=Idle, IsAutoMoving=false

Test C (KeyboardInput_Blocked_DuringAutoMove):
  InjectTestInput(ขวาเต็มที่) 1 วิ ระหว่างเดิน → ขยับ 3.52 (cap 4.7 = speed เดินอัตโนมัติ)
  → คีย์บอร์ดไม่แย่ง control

Test D (ใน A+B): CurrentLocationId เปลี่ยนที่ "ถึง" + publish จริง
  → ChibiSpawnerView/BiomeScatterView reconcile ตาม (map: jungle_edge→jungle)

Test E (MoveValidation_RejectsUnknown_AndNotConnected):
  atlantis → unknown_location, beach→deep_jungle → not_connected
  (ปฏิเสธโดยไม่เริ่มเดิน)

Redirect (RedirectMidWalk_OldRequestSuperseded_NewArrives):
  fire A→jungle_edge, เดิน 0.5 วิ แล้ว fire B→cave_entrance กลางทาง
  → A คืน superseded (ไม่ commit โซน), B เดินถึงจริง + commit cave_entrance
  (จุดหมาย clamp ด้วย WorldBounds — cave WorldY=8 เกิน maxY=5 → ถึง (−5,5))

Cancel (Cancel_StopsWalk_AndUnlocksKeyboard):
  Begin(10,5) → เดิน 0.5 วิ → Cancel() หยุดตรง (1.65,0.83) ไม่ขยับต่อ
  → InjectTestInput ขวา 0.5 วิ ขยับ +1.76 (speed 3.5) = คีย์บอร์ดกลับมาทันที

สภาพแวดล้อมเทส: UnityTearDown คืน world state (cancel + seed beach กึ่งกลาง +
RefreshNow) — กัน pollution ไปเทส NPC ที่รันต่อใน play session เดียวกัน

End-to-end (BridgeRoundTripPlayModeTests — spawn McpBridge.exe จริงเป็น MCP client):
  initialize → tools/list (get_game_state, move_to_location) →
  move_to_location(jungle_edge) ตอบใน 3.2s ≈ 11.09/3.5
  state หลัง call: location=jungle_edge, pos=(9.91,4.96), activity=Idle
  → MessagePack contract Key 15-17 ตรงกันทั้งสองฝั่ง TCP

E2E cancel_move ผ่าน bridge จริง (stdio client ยิงคู่ขนาน move+cancel):
  tools/list มี `cancel_move` ✓ → cancel ตอน idle คืน `not_moving` ✓
  → move_to_location(jungle_edge) + cancel กลางทาง (1.2s) →
  move ที่โดน cancel คืน `superseded` ✓ และ move เต็มหลังจากนั้นตอบ ~3.2s ✓
```

+ หลักฐาน: `TestEvidence/movetolocation-fix/` (txt รายเทส)
+ ชุดเต็ม: PlayMode 10/10 + EditMode 18/18 ผ่าน

## Design Decisions / จุดที่ต่างจาก spec ตั้งต้น

- **`WaitUntilAsync` คืน `UniTask<bool>` + ใช้ Stopwatch** (spec ให้ void + deltaTime):
  caller อยู่บน TCP thread ห้ามแตะ `UnityEngine.Time` และ handler ต้องรู้ว่า
  timeout เพื่อปลด flag แทนการค้างตลอดไป
- **Validate ก่อนเริ่มเดิน** (unknown_location/not_connected) — สืบทอดสัญญา MCP เดิม
- **Timeout tunable** ผ่าน `MoveHandler.MoveTimeoutSeconds` เพื่อเทส failure path
- **บทเรียนเทส:** ห้าม block Unity main thread ระหว่างรอ bridge (threadpool ผ่าน
  `UniTask.Run` เท่านั้น) — main thread ค้าง = GameTickDriver หยุด = เดินไม่จบ;
  และ bridge process ค้างเก่า (zombie) ที่ถือ session TCP 3216 ต้อง kill ก่อนรันซ้ำ

## Known Issues / Next Steps

- การเดินเป็นเส้นตรงตรงเข้าเป้า — ไม่มี pathfinding รอบสิ่งกีดขวาง (biome props
  ยังไม่มี collider บังทาง — ถ้าเพิ่ม obstacle ต้องพิจารณา NavMesh/waypoint)
- ~~ยกเลิก auto-move~~ ✅ ทำแล้ว (2026-09-12): `Cancel()` + redirect ผ่าน
  generation token + MCP tool `cancel_move` — จุดเดียวที่อนุญาตคือ
  PlayerAutoMoveSystem (ไม่ set flag ตรง)
