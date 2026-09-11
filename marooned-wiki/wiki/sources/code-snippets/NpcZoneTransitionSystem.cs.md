---
title: NpcZoneTransitionSystem.cs
type: code-snippets
sources:
  - Marooned/Assets/Scripts/Systems/NpcZoneTransitionSystem.cs
related:
  - "[[npc-zone-transitions]]"
  - "[[Wander.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[NpcCharacterView.cs]]"
  - "[[GameTickDriver.cs]]"
folder: code-snippets
created: 2026-09-11
tags:
  - code-snippets
  - npc
  - zone-transition
  - lab-c
  - marooned
---

# NpcZoneTransitionSystem.cs

## หน้าที่
FSM ข้ามโซนแบบเดินผ่านจุดเชื่อม (Hybrid Transition Points — Lab C Phase 2 Part 2)
plain C# singleton ที่ tick หลัง `NpcMovementSystem` เสมอ — เฝ้าดู
`NpcState.TransitionPhase` ของทุก NPC แล้วขับเคลื่อน
`WalkingToPoint → Exiting → (ข้ามโซน) → Entering → None`

## หลักการสำคัญ
- **Plain C# ไม่มี Coroutine** — นับถอยเวลา anim ด้วย `Dictionary<string, float>`
  ต่อ NPC (`_timers`); entry หาย = ผ่านเฟสทันที (กันค้างตลอดกาล)
- **ห้ามแตะ GameObject/View** — เขียนเฉพาะ ground truth fields
  (`TransitionPhase`/`PendingTransitionTargetZoneId`/Position/Target)
  `NpcCharacterView` ตรวจจับ phase เปลี่ยนเองแล้วเรียก `PlayAction` เอง
- **`MoveNpc()` จุดเดียวที่แก้ `CurrentLocationId`** — เรียกแล้ว override
  `PositionX/Y` เป็น arrival point (จุดเชื่อมฝั่งปลายทางจากแถว reverse) ทำให้
  chibi เดินเข้าจากขอบ ไม่โผล่กลางโซน (Position Seeding Rule escape hatch)

## พารามิเตอร์
| Field | ค่า default | ความหมาย |
|-------|-------------|----------|
| `ExitAnimSeconds` | 0.5 | ระยะเวลา anim เดินออกนอกจอที่จุดเชื่อม |
| `EnterAnimSeconds` | 0.3 | ระยะเวลา anim เดินเข้าจากจุดเชื่อมโซนใหม่ |
| `ArrivalThreshold` | 0.1 | ระยะถือว่าถึงจุดเชื่อมแล้ว (world unit) |

## Tick() flow ต่อ NPC
1. `!IsAlive` + กำลัง transition → เคลียร์ phase/pending/timer (ตายกลางทางสะอาด)
2. `WalkingToPoint` → ถึง target (≤ threshold) → `Exiting` + เริ่ม timer
3. `Exiting` → timer หมด → `CompleteExit`: `MoveNpc` + override arrival point +
   `Entering` + เคลียร์ pending
4. `Entering` → timer หมด → `None` (AI เริ่มตั้ง target ใหม่ได้)

## สถานะ
- ✅ เสร็จ + เทสครบ: EditMode 6/6 + PlayMode visual A+B ผ่าน
  (ดู [[npc-zone-transitions]])
- หลักฐาน: `TestEvidence/lab-c-phase2-zone-transition/`
