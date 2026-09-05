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
folder: UI/Presenters
lines: 9
created: 2026-09-05
tags:
  - UI/Presenters
  - marooned
  - lab-a
---

# CardHandPresenter
**Path:** `Marooned/Assets/Scripts/UI/Presenters/CardHandPresenter.cs` (9 lines)

## Source
```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Plain C#, transient. Reads CardInventorySystem, pushes to CardHandView.</summary>
    public class CardHandPresenter
    {
        // Constructor-injects CardInventorySystem + CardHandView reference; subscribes
        // to inventory-changed message and calls view.RenderHand(...).
    }
}
```

# CardHandPresenter

## Purpose
Presenter ของหน้ามือการ์ด (card hand) ตาม MVP-Lite — อ่านข้อมูลจาก [[CardInventorySystem.cs]]
แล้ว push ให้ [[CardHandView.cs]] render (ตอนนี้ยังเป็น skeleton ไม่มี logic)

## Public API
- `class CardHandPresenter` — ยังไม่มี method/property public ใด ๆ (class ว่าง มีแค่ comment
  อธิบายแผน: constructor-inject `CardInventorySystem` + `CardHandView`, subscribe
  inventory-changed message, เรียก `view.RenderHand(...)`)

## Dependencies
- วางแผน inject: [[CardInventorySystem.cs]] (แหล่งข้อมูล inventory), `CardHandView`
- วางแผน subscribe: "inventory-changed message" (ยังไม่มี message type นี้ใน
  `Marooned/Assets/Scripts/Shared/GameMessages.cs`)
- ยังไม่ถูก register ใน VContainer / [[GameLifetimeScope.cs]]

## Key Logic
ยังไม่มี logic — เป็น comment บอก flow ที่จะทำ: inventory เปลี่ยน → Presenter อ่าน
`PlayerSurvivalState.Inventory` → resolve ชื่อ/จำนวน → `RenderHand(cardId → count)` ที่ View

## TODO / Known Issues
- ทั้ง class ว่างเปล่า — ต้อง implement constructor injection, subscription และ render call
- ไม่มี inventory-changed message ให้ subscribe (ต้องเพิ่มใน Shared ก่อน)
- ไม่ได้ register ใน DI container
