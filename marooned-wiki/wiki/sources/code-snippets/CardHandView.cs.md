---
title: CardHandView
type: snippet
sources:
  - Marooned/Assets/Scripts/UI/Views/CardHandView.cs
related:
  - MVP-Lite
  - "[[CardHandPresenter.cs]]"
  - "[[CardDef]]"
  - "[[UIRoot.cs]]"
folder: UI/Views
lines: 13
created: 2026-09-05
tags:
  - UI/Views
  - marooned
  - lab-a
---

# CardHandView.cs
**Path:** `Marooned/Assets/Scripts/UI/Views/CardHandView.cs` (13 lines)

## Source
```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Displays the player's card hand/inventory; drag onto CraftingSystem or UseCard action.</summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform cardSlotContainer;
        [SerializeField] private GameObject cardSlotPrefab; // pooled, per reference project's grid button pooling lesson

        // Presenter calls RenderHand(cardId -> count) to refresh slots.
    }
}
```

# CardHandView

## Purpose
View (MonoBehaviour แบบ passive ตาม MVP-Lite) แสดงมือการ์ด/inventory ของผู้เล่น —
รองรับการลากการ์ดไปคราฟ ([[CraftingSystem.cs]]) หรือใช้ (UseCard action)

## Public API
- `class CardHandView : MonoBehaviour` — ยังไม่มี public method
- Serialized fields: `Transform cardSlotContainer` (จุด anchor ของ slot การ์ด),
  `GameObject cardSlotPrefab` (prefab slot แบบ pooled — ตามบทเรียน grid button pooling
  ของโปรเจคอ้างอิง)

## Dependencies
- Presenter: [[CardHandPresenter.cs]] (วางแผนเรียก `RenderHand(cardId → count)`)
- Data: `CardDef` (ชื่อ/sprite ต่อ slot) จาก [[LubanDataService.cs]]
- Unity UI (Transform/prefab) — scene/prefab ยังไม่มีในโปรเจค

## Key Logic
ยังไม่มี logic — View เก็บแค่ layout hooks; flow ที่วางแผน: Presenter ส่ง dictionary
cardId→count → View instantiate/pool prefab ต่อ slot → แสดงจำนวน

## TODO / Known Issues
- ไม่มี `RenderHand(...)` จริง (มีแค่ comment บอก contract)
- ไม่มี drag-and-drop interaction กับ craft/use
- cardSlotPrefab และ scene ยังไม่ถูกสร้าง (ไม่มี .unity/.prefab ในโปรเจค)
