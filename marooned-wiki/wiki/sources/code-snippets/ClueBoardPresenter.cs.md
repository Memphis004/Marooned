---
title: ClueBoardPresenter
type: snippet
sources: ["[[sources/clueboardpresenter-cs]]"]
related:
  - MVP-Lite
  - "[[ClueBoardView.cs]]"
  - "[[ClueDef]]"
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

# ClueBoardPresenter.cs
**Path:** `Marooned/Assets/Scripts/UI/Presenters/ClueBoardPresenter.cs` (7 lines)

## Source
```csharp
namespace Marooned.UI.Presenters
{
    public class ClueBoardPresenter
    {
        // Reads PlayerSurvivalState.CollectedClueCardIds, resolves ClueDef, renders cards.
    }
}
```

# ClueBoardPresenter

## Purpose
Presenter ของกระดานเบาะแส (detective board) — อ่าน clue ที่ผู้เล่นเก็บสะสมแล้ว render ลง
[[ClueBoardView.cs]] ให้ผู้เล่น/AI ใช้นิรนัยหาฆาตกร (ตอนนี้ยังเป็น skeleton)

## Public API
- `class ClueBoardPresenter` — ยังไม่มี member ใด ๆ (มีแค่ comment: อ่าน
  `PlayerSurvivalState.CollectedClueCardIds`, resolve `ClueDef`, render cards)

## Dependencies
- วางแผนใช้: `PlayerSurvivalState.CollectedClueCardIds` (ผ่าน [[LubanDataService.cs|GameStateProvider]]),
  `ClueDef` จาก [[LubanDataService.cs]]
- ยังไม่ถูก register ใน VContainer / [[GameLifetimeScope.cs]]

## Key Logic
ยังไม่มี logic — flow ตาม comment: รายการ clue id ที่เก็บแล้ว → lookup `ClueDef` เพื่อเอา
ชื่อ/sprite/reliability → สั่ง View แปะการ์ดลงกระดาน

## TODO / Known Issues
- ทั้ง class ว่างเปล่า
- `CollectedClueCardIds` ตอนนี้ไม่เคยถูกเติมจาก gameplay (ดู [[DeductionSystem.cs]] Known
  Issues) — เมื่อ Presenter เสร็จ กระดานจะว่างเสมอจนกว่าจะมีระบบเก็บ clue
- ไม่ได้ register ใน DI container
