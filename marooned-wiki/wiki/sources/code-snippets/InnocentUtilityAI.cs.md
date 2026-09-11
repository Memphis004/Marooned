---
title: InnocentUtilityAI.cs
type: code-snippets
sources:
  - Marooned/Assets/Scripts/Systems/AI/InnocentUtilityAI.cs
related:
  - "[[npc-survival-motives]]"
  - "[[npc-embodiment-movement]]"
  - "[[npc-zone-transitions]]"
  - "[[Wander.cs]]"
  - "[[NpcDirectorSystem.cs]]"
folder: code-snippets
created: 2026-09-11
tags:
  - code-snippets
  - npc
  - ai
  - utility-ai
  - motive
  - lab-c
  - marooned
---

# InnocentUtilityAI.cs

## หน้าที่
สมองฝั่ง Innocent — utility scoring แบบ **stateless ต่อ action**:
re-evaluate ทุก ~1.5 วิต่อตัว (`ReEvaluateInterval`) กัน jitter การเปลี่ยนใจ
แล้ว execute action ที่ชนะทุก tick จนถึงรอบ re-evaluate ถัดไป

## Action ทั้ง 4

| Action | Score | Execute |
|--------|-------|---------|
| `IdleWanderAction` | `0.1` (baseline ต่ำสุด) | สุ่มจุดในโซน + ทอยข้ามโซน 20% |
| `SeekFoodAction` | Hunger > 55 → `Hunger/100` | มี food card → กินเลย / ไม่มี → เดินไปโซนที่มี (ใกล้สุด) |
| `FleeToSafeZoneAction` | `Fear/100` | `MoveToRandomConnectedZone` — หนีโซนที่เห็นศพ |
| `InvestigateNoiseAction` | `(Curiosity/100) × (1−Fear/100)` เมื่อมี hint | `MoveToZone(LastNoiseLocationId)` — สำรวจจุดเสียง |

สองตัวหลังเป็น stub score 0 มาก่อน — เปิดใช้จริงใน Phase 2.5A
([[npc-survival-motives]]) เมื่อ `NpcSurvivalSystem` เริ่ม spike Fear/Curiosity

## กลไก Tick

1. **Gate transition** — `Wander.IsTransitioning(npc)` → return ทันที
   (ห้าม re-evaluate/ตั้ง target ระหว่างข้ามโซน)
2. ล้าง state ของ NPC ที่ไม่มีแล้ว (SetupRound ใหม่) กัน dict โต
3. นับถอย re-evaluate timer → หมดเวลา: ให้ทุก action ให้คะแนน เลือกสูงสุด
   (เก็บ `_currentChoice` + log `[InnocentAI] npc chose X (score=Y)`)
4. Execute action ที่ชนะทุก tick

## หลักการสำคัญ

- `IUtilityAction.Execute` คืน **void** — ไม่มี state "finished": action เดิน
  เฉพาะเมื่อ `!Wander.HasPendingTarget(npc)` (กันแย่ง target) แล้วปล่อยให้
  NpcMovementSystem เดิน + decay ของ motive พาให้เปลี่ยน action เอง
- ห้ามเพิ่ม timer "หยุดดู N วิ" — ขัดกับกฎ stateless ของ action
- การเดินทุกอย่างผ่าน [[Wander.cs]] เท่านั้น (MoveToZone /
  MoveToRandomConnectedZone / SetRandomTargetInZone)
- **Information Hiding**: อ่าน `NpcState.Survival/Inventory` (ground truth) ได้
  เพราะรันใน Unity — แต่ห้ามผลลัพธ์ใด ๆ หลุด MCP response

## สถานะ
- ✅ เสร็จ + เทสครบ (AiGuard gate: [[npc-zone-transitions]], motive actions:
  [[npc-survival-motives]] — EditMode 8/8 + PlayMode 2/2 ผ่าน)
