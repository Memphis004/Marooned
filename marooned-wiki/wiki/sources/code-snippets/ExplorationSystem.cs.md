---
title: ExplorationSystem
type: snippet
sources: ["[[sources/explorationsystem-cs]]"]
related:
  - "[[LocationDef]]"
  - "[[CardInventorySystem.cs]]"
  - "[[PlayerSurvivalState]]"
  - "[[LubanDataService.cs]]"
  - "[[ExplorationSystem.cs|LocationRuntimeState]]"
folder: Systems
lines: 57
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# ExplorationSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/ExplorationSystem.cs` (57 lines)

## Source
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>Tracks how depleted each LocationDef's loot table currently is.</summary>
    public class LocationRuntimeState
    {
        public Dictionary<string, int> RemainingWeight = new();
    }

    public class ExplorationSystem
    {
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly Dictionary<string, LocationRuntimeState> _runtime = new();
        private readonly CardInventorySystem _inventory;
        private readonly PlayerSurvivalState _player;
        private readonly Random _rng = new();

        public ExplorationSystem(LubanDataService dataService, CardInventorySystem inventory, GameStateProvider stateProvider)
        {
            _locations = dataService.LocationDefs;
            _inventory = inventory;
            _player = stateProvider.Player;

            foreach (var loc in _locations.Values)
                _runtime[loc.Id] = new LocationRuntimeState { RemainingWeight = new Dictionary<string, int>(loc.LootTable) };
        }

        public (bool success, List<string> foundCardIds) Explore(string locationId)
        {
            if (!_locations.ContainsKey(locationId)) return (false, new List<string>());

            var runtime = _runtime[locationId];
            var pool = runtime.RemainingWeight.Where(kv => kv.Value > 0).ToList();
            if (pool.Count == 0) return (true, new List<string>()); // node depleted, exploring is still a valid (empty-handed) action

            var totalWeight = pool.Sum(kv => kv.Value);
            var roll = _rng.Next(0, totalWeight);
            string picked = pool[0].Key;
            var cumulative = 0;
            foreach (var kv in pool)
            {
                cumulative += kv.Value;
                if (roll < cumulative) { picked = kv.Key; break; }
            }

            runtime.RemainingWeight[picked] -= 1;
            _inventory.TryAdd(picked, 1);
            _player.CurrentLocationId = locationId;

            return (true, new List<string> { picked });
        }
    }
}
```

# ExplorationSystem


## Purpose
สำรวจ location node แบบสุ่ม weighted จาก `LocationDef.LootTable` — จุดเด่นคือ loot node
**deplete ได้จริง** (ทรัพยากรจำกัด ตามแนวคิด GDD §2.2 ที่บังคับให้แย่งชิงกับ NPC)

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `(bool success, List<string> foundCardIds) Explore(string locationId)` | สำรวจ node — สุ่มการ์ด 1 ใบตาม weight ที่เหลือ, เพิ่มเข้า inventory, และย้ายผู้เล่นไป node นั้น |

## Dependencies
- **LubanDataService** — `LocationDefs`
- **CardInventorySystem** — เพิ่มการ์ดที่สุ่มได้
- **GameStateProvider** — set `Player.CurrentLocationId`
- มี helper class `LocationRuntimeState` (public, ไฟล์เดียวกัน) เก็บ `RemainingWeight` ต่อ location
- เรียกใช้โดย: `ExploreLocationHandler` ใน [[McpRequestHandlers.cs]] + (วางแผนไว้)
  `MapExplorePresenter` สำหรับ UI

## Key Logic
1. Constructor: สำเร็จ runtime copy ของ `LootTable` ทุก location เก็บใน
   `_runtime[locationId].RemainingWeight` (ต้นฉบับ `LocationDef` ไม่ถูกแก้)
2. `Explore(locationId)`:
   - location ไม่มีจริง → `(false, [])`
   - กรองเฉพาะรายการที่ weight > 0; **ถ้าหมดทุกรายการ → `(true, [])`** (node depleted
     แต่การสำรวจยังเป็น action ที่ "สำเร็จ" — ได้มือเปล่า)
   - สุ่มแบบ cumulative weight (`Random.Next(0, totalWeight)` แล้วไล่หาช่วง)
   - หัก weight ของการ์ดที่ได้ลง 1 → `TryAdd` เข้า inventory → set `CurrentLocationId`
3. ต่อหนึ่งครั้ง explore ได้ **สูงสุด 1 ใบ** (ไม่ใช่หลายใบ)

## TODO / Known Issues
- **Side effect ซ่อนอยู่**: การ explore ตั้ง `CurrentLocationId` ให้เป็น node นั้นด้วย
  (`ExplorationSystem.cs:52`) — ยังไม่มีกฎระยะทาง/การเดินทาง (handler `move_to_location`
  ตรวจ connectivity เองแยกต่างหาก) — แปลว่า AI สำรวจ node ไกลได้ทั้งที่ไม่ได้เดินไป
- `TriggeredEventId` ใน response ยังส่งค่าว่าง — ไม่ได้ roll [[WorldEventSystem.cs]] หลังสำรวจ
- ไม่มี `ActionPenalty` จาก illness card (GDD §2.1: สำรวจช้าลงเมื่อป่วย — ยังไม่ implement)
- ไม่ publish event เมื่อสำรวจ (UI ไม่รู้จน query ใหม่)
