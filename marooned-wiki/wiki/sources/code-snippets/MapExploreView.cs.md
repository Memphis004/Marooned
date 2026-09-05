---
title: MapExploreView
type: snippet
sources: [Marooned/Assets/Scripts/UI/Views/MapExploreView.cs]
related:
  - MVP-Lite
  - "[[MapExplorePresenter.cs]]"
  - "[[LocationDef]]"
  - "[[UIRoot.cs]]"
folder: UI/Views
lines: 10
created: 2026-09-05
tags:
  - UI/Views
  - marooned
  - lab-a
---

# MapExploreView.cs
**Path:** `Marooned/Assets/Scripts/UI/Views/MapExploreView.cs` (10 lines)

## Source
```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>2D sandbox map: clickable LocationDef nodes, shows depleted/available state.</summary>
    public class MapExploreView : MonoBehaviour
    {
        [SerializeField] private Transform nodeContainer;
    }
}
```

# MapExploreView

## Purpose
View ของแผนที่ 2D sandbox — แสดง location node ที่คลิกได้ พร้อมสถานะว่า node ยังมีของ
หรือหมดแล้ว (depleted) จาก `LocationDef` + `LocationRuntimeState`

## Public API
- `class MapExploreView : MonoBehaviour` — ยังไม่มี public method
- Serialized field: `Transform nodeContainer` (จุดวาง node)

## Dependencies
- Presenter: [[MapExplorePresenter.cs]]
- Data: `LocationDef` (WorldX/WorldY, DisplayName) จาก [[LubanDataService.cs]] + runtime
  depletion state จาก [[ExplorationSystem.cs]]

## Key Logic
ยังไม่มี logic — วางแผน: instantiate node ต่อ location → วางตามพิกัด → สี/ไอคอนตามสถานะ
loot คงเหลือ → click ส่งไปที่ Presenter

## TODO / Known Issues
- ไม่มี render/click logic; scene/prefab ยังไม่มี
- ไม่มีกลไกอ่าน `LocationRuntimeState.RemainingWeight` ที่ตอนนี้เป็น internal ของ
  [[ExplorationSystem.cs]] (ต้อง expose ถ้า View จะแสดงสถานะ depleted)
