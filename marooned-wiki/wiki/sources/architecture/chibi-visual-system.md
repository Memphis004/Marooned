---
title: Chibi Visual System (Lab B)
type: architecture
related:
  - "[[ChibiSpawnerView.cs]]"
  - "[[GenericCuteVisualController.cs]]"
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
  ├─ NPC มีชีวิต && อยู่ location เดียวกับ player → Instantiate(chibiPrefab)
  │    └─ GenericCuteVisualController.Bind(NpcState) → Animator.Play("idle"/"walk"/"interact")
  └─ NPC ตาย / ย้ายไป location อื่น / ถูกลบ → Destroy
```

## Scene Setup (SampleScene — ตั้งค่าแล้ว ณ 2026-09-06)
- `GameLifetimeScope/GameManager` — RoundInitializer + GameTickDriver (Lab A)
- `GameLifetimeScope/ChibiSystem` — ChibiSpawnerView, `chibiPrefab` =
  `001 Student 1 Character.prefab`
- Prefab `001 Student 1 Character.prefab` — Animator ผูก `Basic.controller` +
  GenericCuteVisualController (แก้ไข prefab ของ asset โดยตรง)

## Message Contracts (เพิ่มใน Shared/GameMessages.cs ตอน Lab B)
- `PlayerLocationChangedMessage { OldLocationId, NewLocationId }` — publish จาก
  `ExplorationSystem.Explore()` และ `MoveToLocationHandler` (จุด mutate
  `Player.CurrentLocationId` ทั้งสองจุดของโปรเจกต์)
- `NpcLocationChangedMessage { NpcId, OldLocationId, NewLocationId }` — publish จาก
  `NpcDirectorSystem.MoveNpc()` ซึ่งเป็น **จุดเดียว** ที่อนุญาตให้แก้
  `NpcState.CurrentLocationId` (RoundInitializer ใช้ตอนวางตำแหน่งเริ่มต้นด้วย)

## Test Evidence (Play Mode 2026-09-06)
```
[1] เริ่มเกม: player @ beach, get_visible_npcs = 5 ตัว, chibi บนจอ = 5
[2] move_to_location(jungle_edge) → chibi บนจอ = 0
[3] move_to_location(beach) → chibi บนจอ = 5
```
+ ภาพจริงจาก Game View: chibi Student 1 ปรากฏ 4–5 ตัวที่ beach (offset กันซ้อน)

## Known Issues / Next Steps
- ยังไม่มี visual ของผู้เล่น + สภาพแวดล้อม (tilemap) — chibi ลอยบนพื้นสีพื้นหลังกล้อง
- `NpcDirectorSystem.TickBehavior` ยังเป็น placeholder → NPC ยังไม่มีการเดินจริง
  (message การย้ายที่เกิดจริงตอนนี้มีจาก RoundInitializer + player movement)
- Asset ใช้ PSB skeletal — ต่างจาก ChibiAnimatedRenderer (frame-swap); การเลือก
  renderer กลางสำหรับ NPC หลายตัว (memory/perf) ยังเป็นโจทย์ Lab ถัดไป
- การเชื่อม McpBridge จริง (TCP) กับ get_visible_npcs ยังต้องทดสอบแยก (Lab B ทดสอบ
  ผ่าน handler in-process เท่ากับ code path เดียวกันข้ามชั้น TCP)
