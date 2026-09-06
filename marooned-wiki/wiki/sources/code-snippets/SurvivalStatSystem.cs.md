---
title: SurvivalStatSystem
type: snippet
sources: ["[[sources/survivalstatsystem-cs]]"]
related:
  - "[[PlayerSurvivalState]]"
  - "[[LubanDataService.cs|GameStateProvider]]"
  - MessagePipe
  - "[[CardDef]]"
  - "[[IllnessDef]]"
folder: Systems
lines: 73
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# SurvivalStatSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs` (73 lines)

## Source
```csharp
using System;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Ticks Hunger/Thirst/Mood/Fatigue over time and rolls for illness when a stat
    /// stays critical too long. Mirrors the reference project's TickGathering /
    /// TickCrafting fractional-accumulator pattern.
    /// </summary>
    public class SurvivalStatSystem
    {
        private readonly IPublisher<SurvivalStatChangedMessage> _statPublisher;
        private readonly IPublisher<ConditionCardAppliedMessage> _conditionPublisher;
        private readonly PlayerSurvivalState _state;

        private float _criticalHungerSeconds;
        private float _criticalThirstSeconds;

        public SurvivalStatSystem(
            IPublisher<SurvivalStatChangedMessage> statPublisher,
            IPublisher<ConditionCardAppliedMessage> conditionPublisher,
            GameStateProvider stateProvider)
        {
            _statPublisher = statPublisher;
            _conditionPublisher = conditionPublisher;
            _state = stateProvider.Player;
        }

        public void Tick(float deltaSeconds)
        {
            ApplyDrain("Hunger", ref _state.Hunger, 0.15f * deltaSeconds);
            ApplyDrain("Thirst", ref _state.Thirst, 0.25f * deltaSeconds);
            ApplyDrain("Fatigue", ref _state.Fatigue, -0.10f * deltaSeconds); // fatigue rises (negative "drain" = increase)

            if (_state.Hunger <= 15f)
            {
                _criticalHungerSeconds += deltaSeconds;
                if (_criticalHungerSeconds > 120f && !_state.ActiveConditionCardIds.Contains("illness_malnutrition"))
                    ApplyCondition("illness_malnutrition");
            }
            else _criticalHungerSeconds = 0f;

            if (_state.Thirst <= 15f)
            {
                _criticalThirstSeconds += deltaSeconds;
                if (_criticalThirstSeconds > 90f && !_state.ActiveConditionCardIds.Contains("illness_dehydration"))
                    ApplyCondition("illness_dehydration");
            }
            else _criticalThirstSeconds = 0f;

            if (_state.Hunger <= 0f && _state.Thirst <= 0f)
                _state.IsAlive = false;
        }

        private void ApplyDrain(string key, ref float value, float amount)
        {
            var before = value;
            value = Math.Clamp(value - amount, 0f, 100f);
            if (!Mathf_Approximately(before, value))
                _statPublisher.Publish(new SurvivalStatChangedMessage { StatKey = key, NewValue = value, Delta = value - before });
        }

        private static bool Mathf_Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

        private void ApplyCondition(string conditionCardId)
        {
            _state.ActiveConditionCardIds.Add(conditionCardId);
            _conditionPublisher.Publish(new ConditionCardAppliedMessage { TargetEntityId = "player", ConditionCardId = conditionCardId });
        }
    }
}
```

# SurvivalStatSystem


## Purpose
Tick สถานะเอาชีวิตรอดของผู้เล่น 4 ค่า (Hunger/Thirst/Mood/Fatigue) ตามเวลา, ใช้ condition card
(โรค) เมื่อ stat ตกวิกฤตินานเกินไป และตัดสินตาย — ใช้ fractional-accumulator pattern
เดียวกับ TickGathering/TickCrafting ของโปรเจคอ้างอิง

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `void Tick(float deltaSeconds)` | เรียกทุกเฟรม/ทุก tick — drain stats, สะสมเวลาวิกฤต, roll illness, เช็คตาย |

(class ไม่มี public property — state ทั้งหมดอยู่ที่ `GameStateProvider.Player` ซึ่งถูก mutate ตรง)

## Dependencies
- **GameStateProvider** (constructor injection) — เข้าถึง `PlayerSurvivalState` ตัวเดียวกลาง
- **MessagePipe** `IPublisher<SurvivalStatChangedMessage>` — broadcast เมื่อ stat เปลี่ยน (ใช้โดย UI ในอนาคต)
- **MessagePipe** `IPublisher<ConditionCardAppliedMessage>` — broadcast เมื่อได้รับ illness card
- Data: ชื่อ condition card (`illness_malnutrition`, `illness_dehydration`) hardcode ในไฟล์

## Key Logic
- **Drain rates ต่อวินาที**: Hunger −0.15, Thirst −0.25, Fatigue **+0.10** (ส่งค่า drain เป็นลบ
  เพื่อให้ fatigue เพิ่มขึ้น) — ทุกค่า clamp 0–100, publish `SurvivalStatChangedMessage`
  เมื่อค่าเปลี่ยนจริง (เทียบด้วย epsilon 0.0001)
- **Critical accumulation**: Hunger ≤ 15 สะสม `_criticalHungerSeconds`; เกิน 120 วินาที
  และยังไม่มี condition นั้น → `ApplyCondition("illness_malnutrition")`
  (Thirst ≤ 15 → 90 วินาที → `illness_dehydration`) — ออกจากวิกฤตแล้วตัวนับ reset เป็น 0
- **Death**: Hunger ≤ 0 **และ** Thirst ≤ 0 พร้อมกัน → `Player.IsAlive = false`
- `ApplyCondition` เพิ่ม card id เข้า `ActiveConditionCardIds` + publish
  `ConditionCardAppliedMessage` (TargetEntityId = `"player"`)

## TODO / Known Issues
- **ไม่มีใครเรียก `Tick()`** — ยังไม่มี game loop/driver (ไม่มี IInitializable/MonoBehaviour
  จับเวลา) ระบบจึงยังไม่ทำงานจริง
- ไม่มีกลไกตายจาก Fatigue สูงสุด หรือผล debuff ของ Mood ต่ำ (แค่ drain/เพิ่มค่า)
- ไม่มีการ heal/cure — `IllnessDef.CureCardId` ยังไม่มีใครอ่านใช้
- ชื่อ illness card hardcode — ควรย้ายไป data-driven จาก Luban table
