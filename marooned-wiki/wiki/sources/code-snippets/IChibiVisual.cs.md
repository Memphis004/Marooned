---
title: IChibiVisual
type: snippet
sources: ["[[sources/ichibivisual-cs]]"]
related:
  - "[[ChibiSpawnerView.cs]]"
  - "[[GenericCuteVisualController.cs]]"
  - "[[SpineVisualController.cs]]"
  - "[[NpcActivityState]]"
  - "[[card-system]]"
folder: Core/Visual
lines: 25
created: 2026-09-06
tags:
  - Core
  - marooned
  - lab-b
  - chibi
  - phase-4
---

# IChibiVisual.cs
**Path:** `Marooned/Assets/Scripts/Core/Visual/IChibiVisual.cs` (25 lines)

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

        /// <summary>Lab B Phase 3: เล่น animation "เก็บของ" แบบ one-shot (ถ้า asset มี state นั้น)</summary>
        void PlayPickup();

        /// <summary>
        /// Phase 4 Step 8: เล่น one-shot action ตามชื่อ (เช่น "attack", "use_item")
        /// backend แต่ละตัว map ชื่อ/ข้ามเงียบๆ ถ้าไม่มี state นั้นใน asset จริง
        /// </summary>
        void PlayAction(string actionName);
    }
}
```

## API
| Member | คำอธิบาย |
| --- | --- |
| `Bind(NpcActivityState)` | apply animation ตาม activity (loop — Idle/Walking/Talking) |
| `SetFacing(bool)` | หันซ้าย/ขวา (Animator: flip localScale.x, Spine: Skeleton.ScaleX) |
| `Transform` | transform ของ visual |
| `PlayPickup()` | one-shot "เก็บของ" (Lab B Phase 3 — Student 1 เท่านั้นที่มี state นี้) |
| `PlayAction(string)` | **Phase 4 Step 8** — one-shot action: `"use_item"` → `interact` (GenericCute), `"attack"` → `"Slash"` (Spine); ชื่ออื่นส่งตรงถ้า asset มีจริง, ไม่มี → ข้ามเงียบๆ |

## Consumers
- [[ChibiSpawnerView.cs]] — spawn/despawn + Bind activity ของ NPC
- `PlayerCharacterView` — คุม player visual; เรียก `PlayPickup` เมื่อได้ `ItemPickedUpMessage`
  และ expose `PlayActionAnimation(actionName)` (Phase 4 Step 8) ให้ presenter เรียกหลังใช้
  การ์ดสำเร็จ — ห่อ `_animLockUntil` กัน Update ทับ one-shot anim
