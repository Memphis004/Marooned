---
title: GameTickDriver
type: snippet
sources: ["[[sources/gametickdriver-cs]]"]
related:
  - "[[SurvivalStatSystem.cs]]"
  - "[[WorldEventSystem.cs]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[RoundInitializer.cs]]"
  - VContainer
folder: Core
lines: 69
created: 2026-09-06
tags:
  - Core
  - marooned
  - lab-b
---

# GameTickDriver.cs
**Path:** `Marooned/Assets/Scripts/Core/GameTickDriver.cs`

## Source
```csharp
using Marooned.Systems;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// ปิดช่องว่างจุดที่ 2: ระบบหลังบ้านไม่มี Driver เรียก Tick() เกมจึงหยุดนิ่ง
    ///
    /// เป็น MonoBehaviour ที่เรียก Tick ของระบบ gameplay ทุกเฟรมใน Update():
    ///   - SurvivalStatSystem.Tick(float deltaSeconds)
    ///   - NpcDirectorSystem.Tick(float deltaSeconds)  (killer AI / กำหนดเวลา NPC)
    ///   - WorldEventSystem.Tick(float deltaSeconds, string currentLocationTag)
    ///
    /// โปรเจกต์ยังไม่มี interface แบบ ITickable เอง จึงเรียกตรงตาม signature จริง
    /// ระบบทั้งหมดถูก Resolve จาก VContainer (ไม่ลากใส่ Inspector)
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (เช่น GameManager)
    /// </summary>
    public class GameTickDriver : MonoBehaviour
    {
        private SurvivalStatSystem _survival;
        private NpcDirectorSystem _npcDirector;
        private WorldEventSystem _worldEvents;
        private GameStateProvider _stateProvider;

        private bool _resolved;

        private void Awake()
        {
            // GameLifetimeScope (parent) build container ใน Awake ซึ่งรันก่อน Awake
            // ของ GameObject ลูก — ลอง resolve เลย ถ้ายังไม่สำเร็จจะลองอีกครั้งใน Start
            TryResolveSystems();
        }

        private void Start()
        {
            if (!_resolved) TryResolveSystems();
        }

        private bool TryResolveSystems()
        {
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[GameTickDriver] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return false;
            }

            _survival = scope.Container.Resolve<SurvivalStatSystem>();
            _npcDirector = scope.Container.Resolve<NpcDirectorSystem>();
            _worldEvents = scope.Container.Resolve<WorldEventSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _resolved = true;
            Debug.Log("[GameTickDriver] Resolve ระบบครบแล้ว — เริ่ม Tick ทุกเฟรม");
            return true;
        }

        private void Update()
        {
            if (!_resolved) return;

            float deltaSeconds = Time.deltaTime;

            // ลำดับ: stat ผู้เล่น -> พฤติกรรม NPC -> world event
            // (ลำดับนี้ยังไม่มีผลเชิงระบบตอนนี้ แต่ fix ไว้ให้ deterministic)
            _survival.Tick(deltaSeconds);
            _npcDirector.Tick(deltaSeconds);

            // WorldEventSystem.Tick ต้องการ location tag ปัจจุบัน — ใช้ id ของ
            // location ผู้เล่นเป็น tag ไปก่อน (mock events ไม่ได้กำหนด RequiredLocationTags)
            _worldEvents.Tick(deltaSeconds, _stateProvider.Player.CurrentLocationId);
        }
    }
}
```

# GameTickDriver

## Purpose
เป็น "หัวใจเต้น" ของเกม — MonoBehaviour เดียวที่เรียก `Tick()` ของระบบ gameplay
ทุกเฟรม แก้ปัญหา "ระบบไม่อัปเดต ทั้งที่ MCP เชื่อมแล้ว" (stat ไม่ drain, NPC ไม่
ขยับ, ไม่มี world event เกิด)

## Public API
| Member | คำอธิบาย |
| --- | --- |
| (ไม่มี) | ทำงานอัตโนมัติผ่าน lifecycle `Awake/Start/Update` — ไม่มี public member |

## Dependencies
- **GameLifetimeScope** (VContainer) — `GetComponentInParent<>()` + `Container.Resolve<T>()`
  ทั้ง 4 ระบบ: `SurvivalStatSystem`, `NpcDirectorSystem`, `WorldEventSystem`,
  `GameStateProvider`
- **ข้อควรระวังของ VContainer**: `Resolve<T>()` เป็น *extension method* ใน
  namespace `VContainer` (`IObjectResolverExtensions`) — ต้อง `using VContainer;`
  ไม่งั้น compile error `CS0308` (Resolve เป็น non-generic บน interface เอง)

## Key Logic
- **Resolve 2 จังหวะ** (`Awake` → `Start`): Awake ของ GameObject ลูก รันหลัง
  parent (ซึ่ง build container) — แต่กัน edge case ด้วยการ retry ใน Start
- **ลำดับการ Tick ใน Update()** (fix ไว้ให้ deterministic):
  1. `SurvivalStatSystem.Tick(dt)` — drain Hunger/Thirst, สะสมวิกฤต, roll illness
  2. `NpcDirectorSystem.Tick(dt)` — พฤติกรรม NPC + killer ลอง eliminate
  3. `WorldEventSystem.Tick(dt, currentLocationTag)` — roll world event
     (ใช้ `Player.CurrentLocationId` เป็น tag ชั่วคราว — mock events ไม่มี
     `RequiredLocationTags` จึง eligible ทุกที่)
- ทุกระบบรับ `Time.deltaTime` เป็นวินาที (fractional-accumulator pattern
  เดียวกับโปรเจกต์อ้างอิง)

## TODO / Known Issues
- เรียก Tick ตรงแบบ hardcode ต่อระบบ — ถ้าจำนวนระบบโต ควรทำ registry แบบ
  `List<ITickable>` (interface ของโปรเจกต์เอง) แล้ว register ใน
  GameLifetimeScope เพื่อไม่ต้องแก้ driver ทุกครั้ง
- ใช้ `Time.deltaTime` ตรง — ยังไม่รองรับ time-scale manipulation (เช่น
  ช่วง meeting ที่ควรหยุดเวลาโลก) — จะต้อง design ตอนทำ meeting flow
- WorldEventSystem ที่ deque แล้วใคร consume ยังเป็น `AwaitNextEventHandler`
  (busy-poll) — ดู [[WorldEventSystem.cs]] Known Issues
