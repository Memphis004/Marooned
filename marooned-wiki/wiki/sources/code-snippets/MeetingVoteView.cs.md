---
title: MeetingVoteView
type: snippet
sources: [Marooned/Assets/Scripts/UI/Views/MeetingVoteView.cs]
related:
  - MVP-Lite
  - "[[MeetingVotePresenter.cs]]"
  - "[[NpcObservableView]]"
  - "[[UIRoot.cs]]"
folder: UI/Views
lines: 10
created: 2026-09-05
tags:
  - UI/Views
  - marooned
  - lab-a
---

# MeetingVoteView.cs
**Path:** `Marooned/Assets/Scripts/UI/Views/MeetingVoteView.cs` (10 lines)

## Source
```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Meeting phase: list of living NPCs (observable view only) + Accuse/Abstain buttons.</summary>
    public class MeetingVoteView : MonoBehaviour
    {
        [SerializeField] private Transform npcListContainer;
    }
}
```

# MeetingVoteView

## Purpose
View ของ Meeting Phase — แสดงรายชื่อ NPC ที่ยังมีชีวิต (เฉพาะ `NpcObservableView` เท่านั้น
— ห้ามใช้ `NpcState`) พร้อมปุ่ม Accuse / Abstain

## Public API
- `class MeetingVoteView : MonoBehaviour` — ยังไม่มี public method
- Serialized field: `Transform npcListContainer` (จุดวางรายชื่อ NPC)

## Dependencies
- Presenter: [[MeetingVotePresenter.cs]]
- Data: `NpcObservableView` จาก [[DeductionSystem.cs]].GetObservableNpcsAt —
  **information-hiding rule**: View ต้องแสดงข้อมูลระดับ observable เท่านั้น (ไม่มี role จริง)

## Key Logic
ยังไม่มี logic — วางแผน: แสดง list ของ NPC (id, alive, activity, visible conditions) +
ปุ่มสองปุ่ม; กด Accuse → Presenter → `DeductionSystem.Accuse`

## TODO / Known Issues
- ไม่มี render/button logic; scene/prefab ยังไม่มี
- ไม่มี state machine ของ Meeting Phase ควบคุมว่าเมื่อไร View นี้เปิด/ปิด
- ต้องระวังไม่ให้ implement ในอนาคตดึง `NpcState` ตรง (ground truth) — ต้องผ่าน
  `NpcObservableView` เสมอ
