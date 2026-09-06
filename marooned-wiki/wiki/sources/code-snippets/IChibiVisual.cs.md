---
title: IChibiVisual
type: snippet
sources: ["[[sources/ichibivisual-cs]]"]
related:
  - "[[ChibiSpawnerView.cs]]"
  - "[[GenericCuteVisualController.cs]]"
  - "[[SpineVisualController.cs]]"
  - "[[NpcActivityState]]"
folder: Core/Visual
lines: 17
created: 2026-09-06
tags:
  - Core
  - marooned
  - lab-b
  - chibi
---

# IChibiVisual.cs
**Path:** `Marooned/Assets/Scripts/Core/Visual/IChibiVisual.cs`

Interface กลางของ visual layer (Lab B Phase 2) — `ChibiSpawnerView` พึง interface
นี้เท่านั้น ไม่รู้ว่า backend ข้างใต้เป็น Animator (GenericCute) หรือ Spine
ดูสถาปัตยกรรมรวมที่ [[chibi-visual-system]]

## Source
```csharp
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Core.Visual
{
    /// <summary>
    /// Interface กลางของ visual layer — ChibiSpawnerView ไม่ต้องรู้ว่า
    /// backend ข้างใต้เป็น Animator / Spine / paperdoll ในอนาคต
    /// </summary>
    public interface IChibiVisual
    {
        void Bind(NpcActivityState state);
        void SetFacing(bool facingRight);
        Transform Transform { get; }
    }
}
```
