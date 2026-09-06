---
title: CardHandPresenter
type: snippet
sources:
  - Marooned/Assets/Scripts/UI/Presenters/CardHandPresenter.cs
related:
  - MVP-Lite
  - "[[CardHandView.cs]]"
  - "[[CardInventorySystem.cs]]"
  - "[[UIRoot.cs]]"
  - "[[card-system]]"
folder: UI/Presenters
lines: 31
created: 2026-09-05
tags:
  - UI/Presenters
  - marooned
  - lab-a
  - phase-4
---

# CardHandPresenter
**Path:** `Marooned/Assets/Scripts/UI/Presenters/CardHandPresenter.cs` (31 lines)

## Source
```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Plain C#, transient. Reads CardInventorySystem, pushes to CardHandView.</summary>
    public class CardHandPresenter
    {
        // Constructor-injects CardInventorySystem + CardHandView reference; subscribes
        // to inventory-changed message and calls view.RenderHand(...). (Lab A stub)

        // ---- Phase 4 Step 7: use_card feedback ----
        // เมื่อ UI flow ใช้การ์ด (เรียก UseCardHandler ผ่าน MessagePipe request/response)
        // ให้ map FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยก่อนแสดงผล
        // ครอบคลุมทุก reason ที่ UseCardHandler/CanEliminate คืนได้จริง:
        //   unknown_card, missing_target, invalid_target_type,
        //   unknown_target, target_already_dead, target_not_same_location,
        //   witnessed, not_in_inventory

        /// <summary>แปลง FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยสำหรับผู้เล่น</summary>
        public string GetLocalizedReason(string reason) => reason switch
        {
            "witnessed" => "มีคนเห็น! ไม่สามารถลงมือได้",
            "target_not_same_location" => "เป้าหมายไม่ได้อยู่ในโซนเดียวกัน",
            "target_already_dead" => "เป้าหมายนี้ไม่อยู่แล้ว",
            "missing_target" => "ต้องระบุเป้าหมายสำหรับไอเท็มนี้",
            "invalid_target_type" => "การ์ดนี้ใช้กับเป้าหมายไม่ได้",
            "not_in_inventory" => "ไม่มีการ์ดนี้ในมือ",
            "unknown_card" => "ไม่รู้จักการ์ดนี้",
            "unknown_target" => "ไม่พบเป้าหมายนี้",
            _ => "ใช้งานไม่ได้"
        };
    }
}
```

# CardHandPresenter

## Purpose
Presenter ของหน้ามือการ์ด (card hand) ตาม MVP-Lite — อ่านข้อมูลจาก [[CardInventorySystem.cs]]
แล้ว push ให้ [[CardHandView.cs]] render (flow หลักยังเป็น skeleton)

**Phase 4 Step 7** เพิ่ม `GetLocalizedReason` — แปลง `FailureReason` จาก `UseCardResponse`
(มาจาก [[UseCardHandler]] ใน [[McpRequestHandlers.cs]]) เป็นข้อความภาษาไทยที่ผู้เล่นอ่านเข้าใจทันที
ครอบคลุมครบทุก reason ที่ server คืนได้จริง รวม reason จาก `NpcDirectorSystem.CanEliminate`
(weapon flow ใหม่ — ดู [[card-system]])

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `string GetLocalizedReason(string reason)` | map FailureReason → ข้อความไทย (`witnessed` → "มีคนเห็น! ไม่สามารถลงมือได้" ฯลฯ); reason ที่ไม่รู้จัก → "ใช้งานไม่ได้" |

- flow หลักยังวางแผน: constructor-inject `CardInventorySystem` + `CardHandView`, subscribe
  inventory-changed message, เรียก `view.RenderHand(...)` (ยังไม่ implement)

## Dependencies
- วางแผน inject: [[CardInventorySystem.cs]] (แหล่งข้อมูล inventory), `CardHandView`
- `GetLocalizedReason` ออกแบบให้เรียกหลังได้ response จาก [[UseCardHandler]]
  (`UseCardResponse.FailureReason`)
- ยังไม่ถูก register ใน VContainer / [[GameLifetimeScope.cs]]

## Key Logic
- switch expression แปลง reason machine-readable เป็นข้อความภาษาไทย — 8 เคส + fallback
- reason มาจาก 2 ชั้น: ตัว handler เอง (`unknown_card`, `missing_target`,
  `invalid_target_type`, `not_in_inventory`) และจาก `CanEliminate` (`unknown_target`,
  `target_already_dead`, `target_not_same_location`, `witnessed`)

## TODO / Known Issues
- flow การใช้การ์ดจาก UI ยังไม่มี — ตอนนี้มีแค่ MCP bridge เรียก [[UseCardHandler]] ได้
  เมื่อสร้าง hand UI แล้ว presenter จะเรียก `GetLocalizedReason(res.FailureReason)` เมื่อ
  `Success = false` และเล่น `PlayActionAnimation("attack"/"use_item")` บน
  `PlayerCharacterView` เมื่อสำเร็จ (ดู [[IChibiVisual.cs]])
- ไม่มี inventory-changed message ให้ subscribe (ต้องเพิ่มใน Shared ก่อน)
- ไม่ได้ register ใน DI container
