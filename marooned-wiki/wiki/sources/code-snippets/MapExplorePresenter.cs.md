---
title: MapExplorePresenter
type: snippet
sources: [Marooned/Assets/Scripts/UI/Presenters/MapExplorePresenter.cs]
related:
  - MVP-Lite
  - "[[MapExploreView.cs]]"
  - "[[ExplorationSystem.cs]]"
  - "[[UIRoot.cs]]"
folder: UI/Presenters
lines: 7
created: 2026-09-05
tags:
  - UI/Presenters
  - marooned
  - lab-a
---

# MapExplorePresenter.cs
**Path:** `Marooned/Assets/Scripts/UI/Presenters/MapExplorePresenter.cs` (7 lines)

## Source
```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Calls ExplorationSystem.Explore on node click; direct method call (in-process), not pub/sub.</summary>
    public class MapExplorePresenter
    {
    }
}
```

# MapExplorePresenter

## Purpose
Presenter ของแผนที่ 2D sandbox — เมื่อผู้เล่นคลิก location node จะเรียก
[[ExplorationSystem.cs]].Explore ด้วย **direct method call** (in-process) ไม่ใช้ pub/sub
ตามบทเรียน Lab 13 ของโปรเจคอ้างอิง (ตอนนี้ยังเป็น skeleton)

## Public API
- `class MapExplorePresenter` — ยังไม่มี member ใด ๆ

## Dependencies
- วางแผน inject: [[ExplorationSystem.cs]], `MapExploreView`
- ยังไม่ถูก register ใน VContainer / [[GameLifetimeScope.cs]]

## Key Logic
ยังไม่มี logic — flow ตาม comment: node click → `ExplorationSystem.Explore(locationId)` →
อัปเดตสถานะ node (สำรวจได้/หมด) บน View ทันที

## TODO / Known Issues
- ทั้ง class ว่างเปล่า (ไม่มี comment ภายใน body ด้วย — คำอธิบายอยู่บน XML doc)
- ไม่ได้ register ใน DI container; UI scene/prefab ยังไม่มีในโปรเจค
