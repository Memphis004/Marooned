---
title: ClueBoardView
type: snippet
sources: ["[[sources/clueboardview-cs]]"]
related:
  - MVP-Lite
  - "[[ClueBoardPresenter.cs]]"
  - "[[ClueDef]]"
  - "[[UIRoot.cs]]"
folder: UI/Views
lines: 10
created: 2026-09-05
tags:
  - UI/Views
  - marooned
  - lab-a
---

# ClueBoardView.cs
**Path:** `Marooned/Assets/Scripts/UI/Views/ClueBoardView.cs` (10 lines)

## Source
```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Detective-board style layout of collected ClueDef cards, for the player (and AI agent's get_clue_board query) to review.</summary>
    public class ClueBoardView : MonoBehaviour
    {
        [SerializeField] private Transform clueBoardContainer;
    }
}
```

# ClueBoardView

## Purpose
View แบบ detective-board (กระดานปะติดปะต่อเบาะแส) แสดง `ClueDef` ที่ผู้เล่นเก็บไว้ —
ข้อมูลชุดเดียวกับที่ AI query ผ่าน MCP tool `get_clue_board`

## Public API
- `class ClueBoardView : MonoBehaviour` — ยังไม่มี public method
- Serialized field: `Transform clueBoardContainer` (จุดแปะการ์ดเบาะแส)

## Dependencies
- Presenter: [[ClueBoardPresenter.cs]]
- Data: `ClueDef` (Reliability, SpritePath) จาก [[LubanDataService.cs]]
- ข้อมูลเดียวกับ `GetClueBoardResponse.CollectedClueCardIds` ใน
  `Marooned/Assets/Scripts/Shared/GameMessages.cs`

## Key Logic
ยังไม่มี logic — layout แบบ detective board จะจัดการ์ดเบาะแสบน container

## TODO / Known Issues
- ไม่มี render logic จริง; scene/prefab ยังไม่มี
- ข้อมูลต้นทาง (`CollectedClueCardIds`) ยังไม่เคยถูกเติมจาก gameplay
