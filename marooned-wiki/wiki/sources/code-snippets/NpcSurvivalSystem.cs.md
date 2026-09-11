---
title: NpcSurvivalSystem.cs
type: code-snippets
sources:
  - Marooned/Assets/Scripts/Systems/NpcSurvivalSystem.cs
related:
  - "[[npc-survival-motives]]"
  - "[[InnocentUtilityAI.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[survival-stats]]"
  - "[[GameTickDriver.cs]]"
folder: code-snippets
created: 2026-09-11
tags:
  - code-snippets
  - npc
  - survival
  - motive
  - messagepipe
  - lab-c
  - marooned
---

# NpcSurvivalSystem.cs

## หน้าที่
Plain C# singleton (Lab C Phase 2.5A) ที่ทำให้ stats ของ NPC "มีชีวิต" —
2 หน้าที่หลัก: **decay tick ต่อเนื่อง** + **motive event hooks (discrete)**

## API

| Member | หน้าที่ |
|--------|---------|
| `Tick(deltaSeconds)` | decay stats ทุก NPC ที่ยังมีชีวิต + clamp target + ล้าง hint + log สรุประยะ |
| `HungerPerSecond = 2f` | หิว 0→100 ใน ~50 วิ |
| `FearDecayPerSecond = 6f` | Fear 100→0 ใน ~17 วิ |
| `CuriosityDecayPerSecond = 3f` | Curiosity 100→0 ใน ~33 วิ |
| `FearSpikeWitness = 50f` | เห็นศพในโซนเดียวกัน → Fear เพิ่มทันที |
| `CuriositySpikeHearNoise = 30f` | ได้ยินเสียงจาก connected location → Curiosity เพิ่มทันที |
| `CuriosityInvestigateThreshold = 5f` | Curiosity ต่ำกว่านี้ → ล้าง `LastNoiseLocationId` |
| `StatusLogIntervalSeconds = 30f` | log สรุป stats ทั้งหมดเป็นระยะ (Test A) |

## Motive ripple (OnNpcEliminated)

Subscribe `NpcEliminatedMessage` (ผ่าน `ISubscriber<T>` — เก็บ `IDisposable`
ไว้ dispose ตอน container ปิด, pattern เดียวกับ WorldItemSystem):

| กลุ่ม | เงื่อนไข | ผล |
|-------|----------|-----|
| Witness | อยู่โซนเดียวกับเหยื่อ | `Fear += 50` |
| Hearer | connected location | `Curiosity += 30` + จด `LastNoiseLocationId` |

- if / else-if — NPC หนึ่งคนนับกลุ่มเดียว (กันนับซ้ำ)
- ข้ามตัวเอง (`msg.VictimNpcId`) และ NPC ตายแล้ว

## หลักการสำคัญ

- **สเกล 0–100** (NpcSurvivalState เดิม Key 0–2) — ห้าม 0–1, ทุกค่า `Math.Clamp(0..100)`
- **ไม่สร้าง state "finished" พิเศษ** — spike แล้วปล่อย decay พากลับ, AI
  re-evaluate เปลี่ยน action เอง
- แก้เฉพาะ ground truth (`Survival.*`) — ห้ามหลุด NpcObservableView / MCP response
- เรียก `Wander.ClampTargetToZone` ทุกเฟรม (clamp เฉพาะตอน `TransitionPhase == None`)
- Hint threshold (5) ต้องต่ำกว่า spike (30) มาก — ไม่งั้น hint ถูกล้างก่อน AI
  มีโอกาสเลือก InvestigateNoise
- TODO: ย้ายค่าคงที่ไปตาราง Luban (เช่น `SurvivalTuningDef`)

## DI (GameLifetimeScope)

```csharp
builder.Register<NpcSurvivalSystem>(Lifetime.Singleton).AsSelf();
```

inject: `NpcDirectorSystem`, `LubanDataService`, `UtilityContext`,
`ISubscriber<NpcEliminatedMessage>` — tick ก่อน Director เสมอ (ดู [[GameTickDriver.cs]])

## สถานะ
- ✅ เสร็จ + เทสครบ A–E (EditMode 8/8 + PlayMode 2/2) — ดู [[npc-survival-motives]]
