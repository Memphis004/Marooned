---
title: survival-stats
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs
  - Marooned/Assets/Scripts/Shared/PlayerSurvivalState.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[SurvivalStatSystem.cs]]"
  - "[[CardInventorySystem.cs]]"
  - "[[WorldEventSystem.cs]]"
  - "[[LubanDataService.cs]]"
  - "[[overview]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - survival
  - marooned
  - lab-a
---

# Survival Stats

## ภาพรวม (Gameplay Perspective)
ผู้เล่นต้องคอยดูแล stat 4 ค่า ได้แก่ **Hunger** (หิว), **Thirst** (กระหาย), **Mood** (อารมณ์)
และ **Fatigue** (ความเหนื่อยล้า) ซึ่งลดลง/เพิ่มขึ้นตลอดเวลาตามการกระทำ ถ้าปล่อยให้ Hunger หรือ
Thirst ต่ำกว่า 15 นานเกินไป ผู้เล่นจะเริ่มป่วยเป็น Illness Card ติดตัว และถ้า Hunger กับ Thirst
ถึง 0 พร้อมกันผู้เล่นจะตาย การเอาชีวิตรอดคือแรงกดดันหลักที่บังคับให้ผู้เล่นต้องออกสำรวจและ
คราฟอาหาร/น้ำอยู่เสมอ

## การ Implement (Developer Perspective)
- **Class หลัก:** `SurvivalStatSystem` (`Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs`)
  — plain C# class, register เป็น Singleton ใน VContainer ผ่าน `GameLifetimeScope`
- **State:** `PlayerSurvivalState` (`Marooned/Assets/Scripts/Shared/PlayerSurvivalState.cs`) —
  MessagePack class ที่ [[LubanDataService.cs|GameStateProvider]] ถือ instance เดียวกลาง
- **Public API สำคัญ:**
  - `void Tick(float deltaSeconds)` — drain stats + สะสมเวลาวิกฤต + เช็คตาย
- **Flow:** `Tick` → drain ตามอัตราต่อวินาที (Hunger −0.15, Thirst −0.25, Fatigue +0.10,
  ทุกค่า clamp 0–100) → publish `SurvivalStatChangedMessage` ผ่าน MessagePipe เมื่อค่าเปลี่ยน
  → ถ้า stat วิกฤตสะสมเกินเวลา (Hunger ≤ 15 นาน 120s / Thirst ≤ 15 นาน 90s) เพิ่ม condition
  card (`illness_malnutrition` / `illness_dehydration`) + publish `ConditionCardAppliedMessage`
  → Hunger ≤ 0 และ Thirst ≤ 0 พร้อมกัน → `IsAlive = false`
- ใช้ fractional-accumulator pattern เดียวกับ TickGathering/TickCrafting ของโปรเจคอ้างอิง

## Data Tables
- `DataTables/Data/CardDef.csv` — `StatEffect` (hungerDelta/thirstDelta/moodDelta/fatigueDelta)
  คือค่าที่ `UseCardHandler` ใช้เติม stat เมื่อใช้การ์ด
- `DataTables/Data/IllnessDef.csv` — นิยามโรค (`illness_malnutrition`, `illness_dehydration`,
  `injury_cut`), CureCardId และ SeverityGrowthPerHour (ยังไม่ถูกใช้ใน logic)
- ⚠️ ทั้งหมดยังโหลดผ่าน mock ใน [[LubanDataService.cs]] ไม่ได้อ่าน Luban JSON จริง

## ความเชื่อมโยงกับระบบอื่น
- [[CardInventorySystem.cs]] — ใช้การ์ดอาหาร/น้ำเติม stat ผ่าน `UseCardHandler`
- [[WorldEventSystem.cs]] — Survival Event (เช่น พายุ) ควรเร่ง drain ตาม GDD §3
- [[DeductionSystem.cs]] — โหวตผิดหัก Mood −15
- [[chibi-avatar]] — condition card ควรแสดงเป็น overlay บนตัวละคร (Lab B)

## สถานะปัจจุบัน
- ⚠️ Logic drain/illness/death เสร็จแล้วในระดับ Lab A
- ❌ **ไม่มี game loop เรียก `Tick()`** — ระบบยังไม่ทำงานจริงจนกว่าจะมี tick driver
- ❌ ไม่มีกลไก heal/cure (`IllnessDef.CureCardId` ยังไม่มีใครใช้)
- ❌ ไม่มีผล debuff ของ Mood ต่ำ หรือการตายจาก Fatigue
- ❌ ชื่อ illness card hardcode — ควรย้ายไป data-driven
