---
title: npc-survival-motives
type: architecture
sources:
  - Marooned/Assets/Scripts/Systems/NpcSurvivalSystem.cs
  - Marooned/Assets/Scripts/Systems/AI/InnocentUtilityAI.cs
  - Marooned/Assets/Scripts/Systems/AI/Wander.cs
  - Marooned/Assets/Scripts/Core/GameTickDriver.cs
  - Marooned/Assets/Scripts/Core/GameLifetimeScope.cs
  - Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs
  - Shared/NpcState.cs
  - Shared/GameMessages.cs
related:
  - "[[npc-zone-transitions]]"
  - "[[npc-embodiment-movement]]"
  - "[[survival-stats]]"
  - "[[npc-director]]"
  - "[[NpcSurvivalSystem.cs]]"
  - "[[InnocentUtilityAI.cs]]"
  - "[[Wander.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[GameTickDriver.cs]]"
folder: architecture
created: 2026-09-11
tags:
  - architecture
  - npc
  - ai
  - survival
  - utility-ai
  - messagepipe
  - lab-c
  - marooned
---

# NPC Survival Motives — Living NPCs (Lab C Phase 2.5A)

> ทำให้ NPC "รู้สึก" ถึงโลกจริง: stats ไม่นิ่งอีกต่อไป — Hunger ไต่ขึ้นช้า ๆ,
> Fear/Curiosity พุ่งจากเหตุการณ์ฆ่าแล้ว decay กลับ, และ [[InnocentUtilityAI]]
> เปลี่ยน action ตามคะแนน motive โดยไม่ต้องมี state machine พิเศษ

## ภาพรวม

ก่อนหน้านี้ NPC เดินได้ ([[npc-embodiment-movement]]) และข้ามโซนแบบเดินจริงได้
([[npc-zone-transitions]]) แต่ `NpcSurvivalState` นิ่งสนิท — Hunger/Fear/Curiosity
ไม่เคยเปลี่ยน, `FleeToSafeZoneAction`/`InvestigateNoiseAction` เป็น stub score 0
(dead code) ระบบนี้ปิดช่องนั้นด้วยกลไก 2 ชั้น:

1. **Decay/Drain tick (ต่อเนื่อง)** — `NpcSurvivalSystem.Tick` ปรับ stats ทุกเฟรม
2. **Motive event hooks (discrete)** — subscribe `NpcEliminatedMessage` ผ่าน
   MessagePipe เพิ่ม spike ทันทีเมื่อเกิดเหตุฆ่า

แนวคิดหลัก: **ห้ามสร้าง state "finished"/state พิเศษใหม่** — spike แล้วปล่อยให้
decay พากลับ baseline ธรรมชาติของ utility AI ([[InnocentUtilityAI.cs]] re-evaluate
ทุก ~1.5 วิ) จะเปลี่ยน action เองเมื่อคะแนนเปลี่ยน

## ข้อมูล: NpcSurvivalState (Shared/NpcState.cs)

`NpcSurvivalState` เป็น class แยกฝังใน `NpcState` (`[Key(15)] Survival`) —
**คนละ numbering space** กับ NpcState หลัก (ซึ่งถึง Key 17 แล้วจาก transition):

| Key | Field | สเกล | ความหมาย |
|-----|-------|------|----------|
| 0 | `Hunger` | 0–100 | ยิ่งสูงยิ่งหิว (normalize ง่ายตอน score) |
| 1 | `Fear` | 0–100 | เห็นศพ/เหตุน่ากลัว → เพิ่ม |
| 2 | `Curiosity` | 0–100 | ได้ยินเสียง/เจอเบาะแส → เพิ่ม |
| 3 | `LastNoiseLocationId` | string/null | โซนจุดเสียงล่าสุด — hint ให้ `InvestigateNoiseAction` |

⚠️ **สเกล 0–100 เท่านั้น** (ตามที่มีอยู่แล้ว) — action ต่าง ๆ normalize เป็น 0–1
ตอน score เอง (เช่น `Fear / 100f`) ห้ามเก็บเป็น 0–1

⚠️ **Information Hiding**: ทั้ง class เป็น ground truth — ห้ามหลุดเข้า
`NpcObservableView` / `GetObservableNpcsAt` / MCP response เด็ดขาด
(เทส E พิสูจน์ด้วย reflection ว่า observableView มี 6 members เท่าเดิม)

## ชั้นที่ 1 — Decay Tick (NpcSurvivalSystem)

Plain C# singleton (`Lifetime.Singleton`) implement `IDisposable` — pattern
เดียวกับ `WorldItemSystem` (subscribe ใน constructor เก็บ `IDisposable` ไว้
dispose ตอน container ปิด)

อัตรา decay (ค่าคงที่ public — TODO ย้ายไปตาราง Luban เช่น `SurvivalTuningDef`):

| Stat | อัตรา/วินาที | ความหมาย |
|------|-------------|----------|
| Hunger | `+2` | หิวจาก 0→100 ใน ~50 วิ |
| Fear | `−6` | เต็ม 100 หมดใน ~17 วิ |
| Curiosity | `−3` | เต็ม 100 หมดใน ~33 วิ |

ทุกค่า clamp `0..100` (ใช้ `Math.Clamp` — ไม่ให้เกินขอบสองฝั่ง)

### Hint lifecycle (LastNoiseLocationId)

- ตั้งค่าตอน "ได้ยินเสียง" (ดูชั้นที่ 2)
- **ล้างเป็น null เมื่อ Curiosity decay ต่ำกว่า `CuriosityInvestigateThreshold = 5`**
  → ให้เวลาสำรวจ ~8 วิหลัง spike (30 → 5 ที่ −3/วิ)
- ⚠️ บทเรียนการออกแบบ: threshold ต้อง**ต่ำกว่า spike มาก** — ถ้าตั้งเท่ากับ
  spike (30) hint จะถูกล้างใน tick แรกก่อน AI มีโอกาสเลือก InvestigateNoise เลย

### Log สรุป (Test A)

log สถิติทุก NPC ทุก 30 วิเกม (`StatusLogIntervalSeconds`) — ไม่ log ทุกเฟรม:
`[NpcSurvival] npc_01 @ beach: Hunger=12 Fear=0 Curiosity=0 noise=beach`

## ชั้นที่ 2 — Motive Event Hooks (NpcEliminatedMessage)

Subscribe `ISubscriber<NpcEliminatedMessage>` (MessagePipe in-process) —
`NpcEliminatedMessage` มี `VictimNpcId` + `LocationId`:

| กลุ่ม NPC | เงื่อนไข | ผล |
|-----------|----------|-----|
| **Witness** ("เห็นศพ") | `CurrentLocationId == msg.LocationId` | `Fear += 50` |
| **Hearer** ("ได้ยินเสียง") | location เป็น connected ของจุดเหตุการณ์ | `Curiosity += 30` + จด `LastNoiseLocationId = msg.LocationId` |

- เงื่อนไขเป็น **if / else-if** — NPC หนึ่งคนนับอยู่กลุ่มเดียว (กันนับซ้ำ)
- ข้าม NPC ตัวเอง (`npc.Id == msg.VictimNpcId`) และ NPC ตายแล้ว
- log ตัวอย่าง motive spike:
  ```
  [NpcSurvival] npc_02 WITNESS body in beach: Fear 0 -> 50
  [NpcSurvival] npc_03 heard noise from beach: Curiosity 0 -> 30 (wants to investigate)
  [NpcSurvival] elimination ripple — victim npc_01 @ beach: 1 witness(es), 1 hearer(s)
  ```

### ข้อจำกัดจริงของ witness path

ผ่าน flow จริงของ `CanEliminate` (no-witness rule ของ [[npc-director]])
**witness ในโซนเดียวกับเหยื่อตอนฆ่าเป็นไปไม่ได้** — path witness (Fear +50)
จึงเกิดได้จากสถานการณ์อื่นเช่น NPC เดินเข้าไปเจอศพภายหลัง (อนาคต) รอบนี้
เทส EditMode ยิง message ตรงเข้า bus เพื่อคุม path นี้แบบ deterministic
ส่วนเทส PlayMode ใช้ flow จริงจึงครอบคลุมแค่ hearer path

## ชั้นที่ 3 — Utility Actions เปิดใช้งาน (InnocentUtilityAI)

สอง stub เดิม (score 0) เปิดใช้จริง — ทั้งคู่ **stateless** ตามสัญญา
`IUtilityAction.Execute` (คืน void — "ไม่มี finished") และเดินเฉพาะผ่าน
[[Wander.cs]] helper เท่านั้น (ห้ามเขียน movement/teleport เอง):

### FleeToSafeZoneAction — หนีเมื่อกลัว

```csharp
Score(npc, ctx)    => npc.Survival.Fear / 100f;   // normalize 0-1 — Fear > 10 ชนะ IdleWander (0.1)
Execute(npc,ctx,dt) {
    if (Wander.HasPendingTarget(npc)) return;      // กำลังเดินอยู่ — ไม่แย่ง target
    Wander.MoveToRandomConnectedZone(npc, ctx, _rng); // "ปลอดภัย" = ออกจากโซนเห็นศพ
}
```

ไม่มี state "หนีสำเร็จ" — ปล่อยให้ Fear decay (−6/วิ) ต่ำกว่า IdleWander แล้ว
รอบ re-evaluate ถัดไป (~1.5 วิ) สมองเปลี่ยน action เอง

### InvestigateNoiseAction — สำรวจจุดเสียง

```csharp
Score(npc, ctx) {
    if (npc.Survival.LastNoiseLocationId == null) return 0f;  // ไม่มีจุดเสียง — ไม่สน
    return (npc.Survival.Curiosity / 100f) * (1f - npc.Survival.Fear / 100f);
    // Curiosity=80, Fear=30 → 0.8 × 0.7 = 0.56 — กลัวมาก (= เห็นศพเอง) จะไม่ไปสำรวจ
}
Execute(npc,ctx,dt) {
    if (Wander.HasPendingTarget(npc)) return;
    Wander.MoveToZone(npc, ctx, npc.Survival.LastNoiseLocationId, _rng, requireConnection: true);
    // ถึงโซนแล้ว → MoveToZone สุ่มจุดในโซนให้ "เดินดูรอบ ๆ" ต่อเอง
}
```

ห้ามเพิ่ม timer "หยุดดู N วิ" — ขัดกับกฎ stateless ของ `IUtilityAction`;
ให้ `NpcMovementSystem` idle ธรรมชาติเมื่อถึงจุด แล้ว decay พาไป action อื่น

## กัน NPC หลุดโซน — Wander.ClampTargetToZone (Test D)

`NpcSurvivalSystem.Tick` เรียก `Wander.ClampTargetToZone(npc, ctx)` ทุก NPC
ทุกเฟรม — clamp `TargetX/Y` ให้อยู่ในรัศมี wander รอบ `LocationDef.WorldX/Y`
(`radiusX=2.5`, `radiusY=1.5`)

⚠️ **สองกฎเหล็กของ clamp (ขัดกับ prompt เดิมที่ให้ clamp ทั้ง Position และ
Target — เบี่ยงอย่างมีเหตุผลและระบุไว้ชัดใน code comment):**

1. **Clamp เฉพาะเมื่อ `TransitionPhase == None`** — ระหว่างข้ามโซน target =
   จุดเชื่อมซึ่ง**ตั้งใจ**ให้อยู่นอกรัศมี wander อยู่แล้ว (beach กลาง (0,0)
   radius ~2.5 แต่จุดเชื่อม x=8) — clamp ตอนนั้น FSM ของ [[npc-zone-transitions]]
   พังทันที
2. **Clamp เฉพาะ Target ไม่แตะ Position** — หลังข้ามโซนเสร็จ (phase กลับ None)
   position = จุด arrival ฝั่งโซนใหม่ซึ่ง "ตั้งใจ" ให้อยู่ขอบโซน — clamp position
   ตรงนั้น = **teleport ต่อหน้าผู้เล่น** ขัดกับเป้าหมายทั้งระบบ transition
   (position หลุดโซนได้ทางเดียว = จุด arrival ซึ่งเจตนาไว้แล้ว — AI จะตั้งเป้า
   ใหม่ในโซนพาเดินเข้าเอง)

คืน `true` ถ้ามีการแก้ target (ใช้ตรวจในเทส) — ไม่ mutate อะไรถ้าไม่จำเป็น

## Tick Order (GameTickDriver)

```
PlayerInput → SurvivalStat(ผู้เล่น) → PlayerMovement → ItemPickup → NodeHarvest
    → NpcSurvivalSystem   ← motive decay/clamp/hint ก่อน AI เสมอ
    → NpcDirectorSystem   ← AI ตั้ง target
    → NpcMovementSystem   ← เดิน
    → NpcZoneTransitionSystem ← FSM ข้ามโซน
```

เหตุผล: motive spike/decay ต้องพร้อม**ก่อน** AI re-evaluate ในเฟรมเดียวกัน —
ไม่งั้น AI ตัดสินใจด้วยค่าเก่า 1 เฟรม (สำคัญกับเทสที่ยิง message แล้วเช็ค
action ทันที)

DI: `builder.Register<NpcSurvivalSystem>(Lifetime.Singleton).AsSelf();`
ใน `GameLifetimeScope.Configure()` — dependency ที่ inject: `NpcDirectorSystem`,
`LubanDataService`, `UtilityContext`, `ISubscriber<NpcEliminatedMessage>`

## Information Hiding

- stats + `LastNoiseLocationId` = ground truth ของ AI — ห้ามหลุด
  `NpcObservableView` / MCP response (เดิมของ Survival อยู่แล้ว ระบบนี้รักษาต่อ)
- เทส E พิสูจน์ด้วย reflection: `NpcObservableView` ยังมี 6 members
  (`Id, IsAlive, CurrentLocationId, Activity, VisibleConditionCardIds, Avatar`)
  — ไม่มี member ใดชื่อ Hunger/Fear/Curiosity/Survival/LastNoise

## ไฟล์ที่เกี่ยวข้อง

| ไฟล์ | บทบาท |
|------|--------|
| `Shared/NpcState.cs` | `NpcSurvivalState` (Key 0–3) + sync ไป Unity/McpBridge ด้วย `./sync-shared.sh` |
| `Systems/NpcSurvivalSystem.cs` | decay tick + motive hooks + clamp + status log |
| `Systems/AI/InnocentUtilityAI.cs` | `FleeToSafeZoneAction` + `InvestigateNoiseAction` (score/execute จริง) |
| `Systems/AI/Wander.cs` | `ClampTargetToZone` (transition-gated, target-only) |
| `Core/GameTickDriver.cs` | tick order Survival → Director → Movement → ZoneTransition |
| `Core/GameLifetimeScope.cs` | DI registration (Singleton + IDisposable) |
| `Tests/Editor/NpcSurvivalEditModeTests.cs` | เทส A–E (8 test) — deterministic |
| `Tests/Runtime/NpcSurvivalPlayModeTests.cs` | เทส in-scene (decay จริง + kill ripple) |

## ผลเทส (2026-09-11)

**EditMode 8/8 ผ่าน** (สร้าง VContainer container จริง + Luban จริง + tick order
เดียวกับ GameTickDriver):

| เทส | ครอบคลุม | ผล |
|-----|----------|-----|
| A: decay rates + clamps + dead skip | tick 0-100 ถูกอัตรา, clamp 0/100, NPC ตายไม่ tick | ✅ |
| B: witness Fear 0→50 → FleeToSafeZone ชนะ | event-history assertion (flake-proof) | ✅ |
| C: hearer Curiosity +30 + hint → InvestigateNoise ชนะ → ถึง beach ≤20s | ผ่าน FSM ข้ามโซนจริง | ✅ |
| D: target นอกโซนถูก clamp; transition target ไม่ถูกแตะ | (50,−50) → (2.5,−1.5) | ✅ |
| E: observableView ไม่ leak stats | reflection | ✅ |
| Bonus: hint lifecycle ล้างหลัง decay | threshold 5 | ✅ |
| Regression: zone-transition suite 6/6 | tick order ใหม่ไม่พังของเดิม | ✅ |

**PlayMode in-scene 2/2 ผ่าน** (TestEvidence/lab-c-phase2-5-survival/):
Hunger ไต่ตามอัตราจริงใน scene; kill ripple ผ่าน MessagePipe จริง → hearer
ได้ Curiosity +30 + hint → **เดินข้ามโซนจริง beach→jungle_edge→beach ผ่าน FSM
ใน 7.2 วิ** ไปสำรวจจุดเสียง (screenshot หลักฐาน)

## สิ่งที่ยังไม่ได้ทำ (อนาคต)

- ย้ายค่าคงที่ decay/spike ไปตาราง Luban (`SurvivalTuningDef`)
- Witness path จากเหตุการณ์จริง (NPC เดินเจอศพภายหลัง — ตอนนี้มีเทสครอบแต่
  gameplay ยังไม่ trigger เอง)
- Fear จากพฤติกรรมผู้เล่นน่าสงสัย (เช่น เห็น player ถืออาวุธใกล้ศพ)
