---
title: MeetingVotePresenter
type: snippet
sources: ["[[sources/meetingvotepresenter-cs]]"]
related:
  - MVP-Lite
  - "[[MeetingVoteView.cs]]"
  - "[[DeductionSystem.cs]]"
  - "[[UIRoot.cs]]"
folder: UI/Presenters
lines: 7
created: 2026-09-05
tags:
  - UI/Presenters
  - marooned
  - lab-a
---

# MeetingVotePresenter.cs
**Path:** `Marooned/Assets/Scripts/UI/Presenters/MeetingVotePresenter.cs` (7 lines)

## Source
```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Calls DeductionSystem.Accuse via DecisionExecutor-style direct method call (Lab 13 lesson from reference project: don't use pub/sub for in-process UI logic that must have an immediate visible effect).</summary>
    public class MeetingVotePresenter
    {
    }
}
```

# MeetingVotePresenter

## Purpose
Presenter ของหน้าประชุม/โหวต (Meeting Phase) — เรียก [[DeductionSystem.cs]].Accuse เมื่อผู้เล่น
กดกล่าวหา NPC ด้วย direct method call ไม่ใช้ pub/sub เพราะผลโหวตต้องเห็นผลทันที
(บทเรียน Lab 13 จากโปรเจคอ้างอิง) — ตอนนี้ยังเป็น skeleton

## Public API
- `class MeetingVotePresenter` — ยังไม่มี member ใด ๆ

## Dependencies
- วางแผน inject: [[DeductionSystem.cs]] (ตัดสิน accusation), `MeetingVoteView`
- ยังไม่ถูก register ใน VContainer / [[GameLifetimeScope.cs]]

## Key Logic
ยังไม่มี logic — flow ตาม comment: ผู้เล่นเลือก NPC + กด Accuse/Abstain →
`DeductionSystem.Accuse(targetNpcId)` → แสดงผลถูก/ผิด, mood ที่ลด, จำนวน wrong accusation
คงเหลือ หรือ win/lose ทันทีบน View

## TODO / Known Issues
- ทั้ง class ว่างเปล่า
- ระบบ Meeting Phase ยังไม่มี state machine (ผู้เล่น accuse ได้ทุกเมื่อ — ดู
  [[DeductionSystem.cs]] Known Issues)
- ไม่ได้ register ใน DI container
