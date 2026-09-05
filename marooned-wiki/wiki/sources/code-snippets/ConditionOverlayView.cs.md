---
title: ConditionOverlayView
type: snippet
sources: [Marooned/Assets/Scripts/UI/Views/ConditionOverlayView.cs]
related:
  - MVP-Lite
  - "[[UIRoot.cs]]"
  - "[[IllnessDef]]"
  - "[[ChibiAnimatedRenderer.cs]]"
  - "[[ChibiAppearance]]"
folder: UI/Views
lines: 10
created: 2026-09-05
tags:
  - UI/Views
  - marooned
  - lab-a
---

# ConditionOverlayView.cs
**Path:** `Marooned/Assets/Scripts/UI/Views/ConditionOverlayView.cs` (10 lines)

## Source
```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Small HUD icons for the player's own active illness/injury cards (ActiveConditionCardIds). NPC-side visuals are handled by ChibiAnimatedRenderer's ConditionOverlays slots directly, not this view.</summary>
    public class ConditionOverlayView : MonoBehaviour
    {
        [SerializeField] private Transform iconContainer;
    }
}
```

# ConditionOverlayView

## Purpose
HUD icon เล็ก ๆ แสดง illness/injury card ที่ผู้เล่นกำลังเป็น (`ActiveConditionCardIds`) —
เฉพาะฝั่งผู้เล่นเท่านั้น ส่วน NPC ใช้ `ConditionOverlays` slots บน [[ChibiAnimatedRenderer.cs]]
โดยตรง ไม่ผ่าน View นี้

## Public API
- `class ConditionOverlayView : MonoBehaviour` — ยังไม่มี public method
- Serialized field: `Transform iconContainer` (จุดวาง icon)
- ไม่มี Presenter (ไม่เหมือน panel อื่น)

## Dependencies
- Data: `PlayerSurvivalState.ActiveConditionCardIds` + `IllnessDef`/`CardDef` (icon/sprite)
- เกี่ยวข้องกับ [[ChibiAppearance]].ConditionOverlays (ฝั่ง NPC)

## Key Logic
ยังไม่มี logic — วางแผน: เมื่อ condition เพิ่ม/หาย เติม/ถอด icon ใน iconContainer

## TODO / Known Issues
- ไม่มี Presenter และไม่มี render logic; ไม่ได้ subscribe
  `ConditionCardAppliedMessage` (message มีอยู่ใน `GameMessages.cs` แล้วแต่ไม่มีใครฟัง)
- ไม่มี icon/sprite asset (โปรเจคยังไม่มี art)
- ไม่มีกลไก heal/cure ทำให้ icon จะไม่หายไปจาก inventory-side logic (ดู
  [[SurvivalStatSystem.cs]] Known Issues)
