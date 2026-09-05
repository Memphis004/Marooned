---
title: UIRoot
type: snippet
sources: [Marooned/Assets/Scripts/UI/Core/UIRoot.cs]
related:
  - MVP-Lite
  - VContainer
  - "[[CardHandView.cs]]"
  - "[[MapExploreView.cs]]"
  - "[[MeetingVoteView.cs]]"
  - "[[ClueBoardView.cs]]"
  - "[[ConditionOverlayView.cs]]"
folder: UI/Core
lines: 43
created: 2026-09-05
tags:
  - UI/Core
  - marooned
  - lab-a
---

# UIRoot.cs
**Path:** `Marooned/Assets/Scripts/UI/Core/UIRoot.cs` (43 lines)

## Source
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.UI.Core
{
    public enum UIPanelType
    {
        CardHand,
        MapExplore,
        MeetingVote,
        ClueBoard,
        ConditionOverlay
    }

    /// <summary>
    /// Same explicit enum->Type panel resolution as the reference project (no
    /// assembly scanning). Also re-applies the stretch-anchor fix from the
    /// reference project's Lab 13 (ContentSizeFitter=PreferredSize was clobbering
    /// anchor stretch and causing panels to pile up center-screen).
    /// </summary>
    public class UIRoot : IInitializable
    {
        private readonly Dictionary<UIPanelType, Type> _panelViewTypes = new()
        {
            { UIPanelType.CardHand, typeof(Views.CardHandView) },
            { UIPanelType.MapExplore, typeof(Views.MapExploreView) },
            { UIPanelType.MeetingVote, typeof(Views.MeetingVoteView) },
            { UIPanelType.ClueBoard, typeof(Views.ClueBoardView) },
            { UIPanelType.ConditionOverlay, typeof(Views.ConditionOverlayView) },
        };

        public void Initialize()
        {
            // Lab A: just make sure the Canvas root stretches full-screen and every
            // top-level panel starts at Unconstrained layout, per the reference
            // project's UIRoot.Awake() fix. Actual instantiation of each panel
            // prefab happens once GameplayScene loads (see design doc Additive
            // Scene section).
        }
    }
}
```

# UIRoot


## Purpose
Root ของระบบ UI — เก็บ registry แบบ enum→Type ของ panel ทั้งหมด 5 แผง (ไม่ใช้ assembly
scanning) และทำหน้าที่ IInitializable entry point ที่ลงทะเบียนผ่าน VContainer

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `enum UIPanelType` | `CardHand`, `MapExplore`, `MeetingVote`, `ClueBoard`, `ConditionOverlay` |
| `Dictionary<UIPanelType, Type> _panelViewTypes` (private) | ผูก enum ตรงเข้า View class — จุด resolve เดียวตอนจะ instantiate panel |
| `void Initialize()` | IInitializable — ตอนนี้ยังว่างเปล่า (ดู TODO) |

## Dependencies
- **VContainer.Unity** — implement `IInitializable`, register ผ่าน
  `builder.RegisterEntryPoint<UIRoot>()` ที่ [[GameLifetimeScope.cs]]
- View classes ทั้ง 5 ใน `Marooned/Assets/Scripts/UI/Views/`
- ออกแบบตาม MVP-Lite ของโปรเจคอ้างอิง (View = MonoBehaviour passive)

## Key Logic
- Registry เป็น dictionary literal ที่ชี้ตรง `typeof(Views.CardHandView)` ฯลฯ — explicit
  ไม่มี magic/reflection (ปลอดภัยกับ IL2CPP)
- Comment บันทึกบทเรียนจากโปรเจคอ้างอิง Lab 13: Canvas ต้อง stretch เต็มจอและ panel
  top-level ต้องเริ่มที่ Unconstrained layout (เดิม `ContentSizeFitter = PreferredSize`
  ทำลาย anchor stretch จน panel กองกลางจอ)
- `Initialize()` ตอนนี้ไม่มีคำสั่งใด — instantiation ของ panel prefab วางแผนไว้ให้เกิดตอน
  GameplayScene โหลด (Additive Scene plan: CoreScene = HUD persistent,
  GameplayScene = island/location)

## TODO / Known Issues
- `Initialize()` ว่างเปล่า — ไม่มีการ instantiate panel ใด ๆ (โปรเจคยังไม่มี Canvas/scene/prefab)
- ไม่มีกลไก `OpenPanel(UIPanelType)` / panel switching / stack — มีแค่ registry
- Presenter ทั้ง 4 ยังไม่ถูก register ใน VContainer และไม่มีใคร wire เข้ากับ View
  (ดู `Marooned/Assets/Scripts/UI/Presenters/*.cs` — skeleton ทั้งหมด)
- Comment ในไฟล์พูดถึง "UIRoot.Awake()" ของโปรเจคอ้างอิง แต่ class นี้เป็น plain
  IInitializable ไม่ใช่ MonoBehaviour — ต้องระวังตอน port fix เรื่อง anchor
