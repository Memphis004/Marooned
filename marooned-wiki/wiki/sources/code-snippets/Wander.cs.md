---
title: Wander.cs
type: code-snippets
sources:
  - Marooned/Assets/Scripts/Systems/AI/Wander.cs
related:
  - "[[npc-zone-transitions]]"
  - "[[NpcZoneTransitionSystem.cs]]"
  - "[[npc-embodiment-movement]]"
  - "[[npc-director]]"
  - "[[NpcDirectorSystem.cs]]"
folder: code-snippets
created: 2026-09-11
tags:
  - code-snippets
  - npc
  - ai
  - zone-transition
  - lab-c
  - marooned
---

# Wander.cs

## หน้าที่
Static helper การเดินร่วมของ AI ทุก role — **single source of truth** ที่ทั้ง
`InnocentUtilityAI` และ `KillerPlanner` เรียกผ่าน helper เดียวกัน (ห้ามตั้ง
target/ข้ามโซนเองนอก helper นี้)

## API

| Method | หน้าที่ |
|--------|---------|
| `IsTransitioning(npc)` | gate เดียวของทุก AI — true ระหว่างข้ามโซน ห้ามตั้ง target ใหม่ |
| `HasPendingTarget(npc)` | กำลังเดินไปเป้าที่ยังไม่ถึงอยู่หรือไม่ |
| `SetRandomTargetInZone(npc, ctx, rng, radius=2.5)` | สุ่ม target รอบกึ่งกลางโซนปัจจุบัน (ZoneSpread pattern เดียวกับ WorldItemSystem) |
| `MoveToZone(npc, ctx, dest, rng, requireConnection=true)` | เดินไปโซนที่ระบุ — เริ่ม transition หรือ fallback teleport |
| `MoveToRandomConnectedZone(npc, ctx, rng)` | สุ่ม connected zone แล้ว delegate เข้า `MoveToZone` |
| `FindZonesWithCardCategory(ctx, category)` | หาโซนที่ loot table มีการ์ด category นั้น (ใช้โดย SeekingWeapon) |

## MoveToZone ลำดับตัดสินใจ (หัวใจของ Hybrid Transition Points)

1. `IsTransitioning` → **false** (ไม่แย่ง target กลาง transition)
2. dest == โซนปัจจุบัน → `SetRandomTargetInZone`
3. `requireConnection` → dest ต้องอยู่ใน `ConnectedLocationIds` จริง
4. `GetTransition(from,to)` **เจอ** จุดเชื่อม → ตั้ง target ที่ transition point +
   `TransitionPhase=WalkingToPoint` + `PendingTransitionTargetZoneId` — แล้วปล่อยให้
   [[NpcZoneTransitionSystem]] ขับเคลื่อนต่อ (helper นี้ไม่ข้ามโซนเอง)
5. **ไม่เจอ** (คู่ไม่อยู่ใน ZoneConnectionDef.csv) → fallback teleport ผ่าน
   `MoveNpc` แบบเดิม — ไม่ error

## หลักการสำคัญ
- Static class ไม่มี state — `Random` แยกต่อผู้เรียก (AI แต่ละตัวมีของตัวเอง)
- ไม่ mutate อะไรเลยเมื่อคืน false (ตัดสินใจซ้ำได้ปลอดภัย)
- จุด wander สุ่มรอบ `LocationDef.WorldX/Y` — ไม่เดา bounds ของโซนที่ไม่มีจริง

## สถานะ
- ✅ เสร็จ + เทสครบ (AiGuard test ยืนยัน gate ทำงาน — ดู [[npc-zone-transitions]])
