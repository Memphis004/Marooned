---
title: bug-2026-09-12-movetolocation-teleport
type: bug-log
sources:
  - Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs
  - Marooned/Assets/Scripts/Systems/PlayerAutoMoveSystem.cs
  - Marooned/Assets/Scripts/Systems/PlayerMovementSystem.cs
  - Shared/PlayerSurvivalState.cs
related:
  - "[[player-auto-move-system]]"
  - "[[player-system]]"
  - "[[npc-embodiment-movement]]"
  - "[[mcp-bridge]]"
folder: bug-log
created: 2026-09-12
tags:
  - bug-log
  - marooned
  - mcp
  - player
  - movement
---

# 🐛 Bug: MoveToLocation Teleport (2026-09-12)

> **สรุปสั้น:** AI VTuber สั่ง `move_to_location` → ตัวละคร teleport ทันที
> (โซนเปลี่ยน แต่ PositionX/Y ไม่ขยับ) — response กลับทันทีด้วย ทำให้
> จังหวะการเล่าเรื่องของ VTuber ผิดจากสิ่งที่เกิดบนจอ

## อาการ (Symptoms)

- สั่ง `move_to_location("jungle_edge")` → `CurrentLocationId` เปลี่ยนทันที,
  biome/chibi เปลี่ยนฉากทันที แต่ sprite ผู้เล่น**ยืนอยู่ที่เดิม**
- `get_game_state` หลัง call: location ใหม่แล้ว แต่ `PositionX/PositionY`
  ยังเป็นพิกัดโซนเก่า → state ขัดแย้งกันเอง
- Response กลับใน < 0.5 วิ สำหรับระยะที่ควรเดินหลายวินาที

## รากเหตุ (Root Cause)

`MoveToLocationHandler.InvokeAsync` (`Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`)
แค่ set `CurrentLocationId` + publish `PlayerLocationChangedMessage` —
**ไม่มีใครขยับ `PositionX/PositionY` เลย**:

```
handler: CurrentLocationId = request.LocationId   ← โซนเปลี่ยนทันที
publish: PlayerLocationChangedMessage             ← ฉากเปลี่ยนทันที
(จบ — ไม่มีคำสั่งเดิน)

PlayerMovementSystem.Tick: ขยับเฉพาะเมื่อ input.MoveAxis ≠ 0 (คีย์บอร์ด)
→ ผู้เล่นไม่กด = PositionX/Y นิ่งตลอด
```

สาเหตุเชิงโครงสร้าง: NPC มีระบบเดินเข้าหาเป้า (`NpcMovementSystem` + `NpcState
TargetX/Y`) แต่ฝั่งผู้เล่น**ไม่มีระบบ auto-move เทียบเท่า** — `PlayerSurvivalState`
ไม่มี field เป้าหมาย และไม่มีระบบไหนถูกออกแบบให้เดินแทนผู้เล่นจากคำสั่ง MCP

> ⚠️ ทางผ่านกลางที่เคยลอง (session ก่อนหน้า): set `CurrentTargetX/Y` ใน handler
> แล้วให้ `PlayerMovementSystem` lerp เองตอนคีย์บอร์ดว่าง — **ยังผิดอยู่** เพราะ
> handler set `CurrentLocationId` ทันที (ฉากเปลี่ยนก่อนถึง) และผสมสองแหล่งที่มา
> ของการควบคุมไว้ใน movement system เดียว

## วิธีแก้ (Fix — 2026-09-12)

แยก "การตั้งเป้า" (handler) ออกจาก "การเดิน" (system) แล้วให้ handler **รอจนถึงจริง**
ค่อยเปลี่ยนโซน — รายละเอียดเต็มใน [[player-auto-move-system]]:

1. **`Shared/PlayerSurvivalState.cs`** — เพิ่ม field `TargetX/TargetY/IsAutoMoving`
   (MessagePack Key 15-17, แก้ที่ canonical `Shared/` แล้วรัน `./sync-shared.sh`)
2. **`PlayerAutoMoveSystem.cs` (ใหม่)** — plain C# singleton เดิน
   `PositionX/Y → TargetX/Y` (Speed 3.5, ถึง ≤ 0.1f ปิด flag เอง + Activity = Idle)
3. **`PlayerMovementSystem.cs`** — guard ต้น Tick: `if (_player.IsAutoMoving) return;`
   (คีย์บอร์ดแย่ง control ไม่ได้ระหว่างเดินอัตโนมัติ)
4. **`MoveToLocationHandler`** — 3 เฟส: ตั้งเป้า+flag (ไม่แตะโซน) → `await
   WaitUntilAsync(() => !player.IsAutoMoving)` → ถึงแล้วค่อย set
   `CurrentLocationId` + publish `PlayerLocationChangedMessage`
   (timeout 30 วิ → ปลด flag + คืน `move_timeout`)
5. **`McpMainThreadDispatcher.WaitUntilAsync`** — poll ผ่าน PlayerLoop ให้ handler
   ที่อยู่บน TCP thread รอได้ปลอดภัย (Stopwatch ไม่แตะ `UnityEngine.Time`)
6. **Wiring** — register ใน `GameLifetimeScope` + GameTickDriver เรียก Tick
   **ก่อน** `PlayerMovementSystem` (เฟรมที่ปิด flag คีย์บอร์ดกลับมาทันที)

## ผลลัพธ์ / หลักฐาน (PlayMode 2026-09-12)

- เดินจริง: traveled 10.99 / straight 11.09, 13 samples @0.25s
  (`Traveling` + `auto=True` ระหว่างทาง, `Idle` เมื่อถึง)
- คีย์บอร์ดช่วงเดินอัตโนมัติ: อัดขวา 1 วิ → ขยับ 3.52 (cap speed 3.5) = ไม่แย่ง
- โซน/ฉากเปลี่ยน**หลังถึง** + bridge round trip จริง: `move_to_location`
  ตอบใน 3.2s ≈ 11.09/3.5
- ชุดเต็ม: PlayMode 10/10 + EditMode 18/18 ผ่าน — ไฟล์หลักฐาน
  `TestEvidence/movetolocation-fix/`

## บทเรียน (Lessons)

- **สัญญาของ discrete event (`PlayerLocationChangedMessage`) คือ "เกิดแล้ว"** —
  ยิงก่อนเดินถึง = ทั้ง visual layer และ MCP response เห็นโลกที่ยังไม่จริง
- **ระบบเดินผู้เล่นกับ NPC ต้องมีเงากัน** — พอ NPC มี auto-move ผู้เล่นก็ต้องมี
  ตอนรับคำสั่งจาก AI (สมมาตรของ architecture ควรเช็คตอนออกแบบใหม่)
- **อย่าผสมสองแหล่งควบคุมในระบบเดียว** — แยกระบบ + flag lock เขียนตำแหน่ง
  ให้มีผู้เขียนเดียวต่อเฟรม (Single Source of Truth)
- **Response timing คือหน้าที่ของเกม ไม่ใช่ proxy** — เกมรู้จบจังหวะเองจาก
  `IsAutoMoving` proxy หน่วงเอง (`travel_time`) จึงซ้ำซ้อน/คลาดจังหวะ
- **เทสที่ block main thread = ทดสอบไม่ได้** — รอ bridge ต้องใช้ threadpool
  ไม่งั้น GameTickDriver หยุดและระบบเดินไม่ทำงาน (เจอตอนเทสจริง)
