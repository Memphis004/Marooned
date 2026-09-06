---
title: WorldEventSystem
type: snippet
sources: ["[[sources/worldeventsystem-cs]]"]
related:
  - "[[WorldEventDef]]"
  - "[[LubanDataService.cs]]"
  - "[[McpRequestHandlers.cs]]"
  - "[[game_design_doc]]"
folder: Systems
lines: 47
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# WorldEventSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/WorldEventSystem.cs` (47 lines)

## Source
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// Weighted random world events, same pattern as the reference project
    /// (auto-pause + Request-Response await, not raw pub/sub, per the Lab 6 fix).
    /// Events are tagged Survival or Social so the design doc's two-group split is
    /// enforced in data, not just convention.
    /// </summary>
    public class WorldEventSystem
    {
        private readonly List<WorldEventDef> _events;
        private readonly Random _rng = new();
        private readonly Queue<WorldEventDef> _pending = new();

        public WorldEventSystem(LubanDataService dataService)
        {
            _events = dataService.WorldEventDefs.Values.ToList();
        }

        public void Tick(float deltaSeconds, string currentLocationTag)
        {
            // Simple fixed-interval roll; replace with per-tag cooldowns later.
            if (_rng.NextDouble() > 0.01) return; // ~1% chance per tick, tune later

            var eligible = _events.Where(e =>
                e.RequiredLocationTags == null || e.RequiredLocationTags.Count == 0 ||
                e.RequiredLocationTags.Contains(currentLocationTag)).ToList();
            if (eligible.Count == 0) return;

            var totalWeight = eligible.Sum(e => e.Weight);
            var roll = _rng.Next(0, totalWeight);
            var cumulative = 0;
            foreach (var e in eligible)
            {
                cumulative += e.Weight;
                if (roll < cumulative) { _pending.Enqueue(e); break; }
            }
        }

        public bool TryDequeue(out WorldEventDef evt) => _pending.TryDequeue(out evt);
    }
}
```

# WorldEventSystem


## Purpose
สุ่ม world event แบบ weighted จาก `WorldEventDef` — แยกกลุ่ม **"Survival"** / **"Social"**
ให้ตรงกับ design doc §3 แล้วจัดคิว event ที่เกิดขึ้นให้ consumer (MCP tool
`await_next_event`) มา deque ไปแสดง

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `void Tick(float deltaSeconds, string currentLocationTag)` | roll event ทุก tick (~1% โอกาส) — กรองด้วย `RequiredLocationTags` แล้วสุ่ม weighted เข้าคิว |
| `bool TryDequeue(out WorldEventDef evt)` | ดึง event ที่รออยู่ตัวแรกออกจากคิว (FIFO) |

## Dependencies
- **LubanDataService** — `WorldEventDefs` (โหลดครั้งเดียวตอน constructor เป็น `List<WorldEventDef>`)
- `WorldEventDef` จาก `Marooned/Assets/Scripts/Shared/ClueDef.cs` (Id, Group, Weight,
  DisplayText, RequiredLocationTags)
- ถูกใช้โดย: `AwaitNextEventHandler` ใน [[McpRequestHandlers.cs]] (MCP tool `await_next_event`)

## Key Logic
- `Tick`: สุ่มก่อนว่า tick นี้จะ roll เลยไหม (`NextDouble() > 0.01` → ข้าม = ~1% ต่อ tick)
  → กรอง event ที่ `RequiredLocationTags` ว่าง (เกิดได้ทุกที่) หรือมี tag ตรงกับ
  `currentLocationTag` → สุ่ม cumulative weight → `Enqueue` เข้า `_pending`
- ผู้บริโภคดึงด้วย `TryDequeue` — pattern Request-Response ตามบทเรียน Lab 6 ของโปรเจค
  อ้างอิง (ไม่ใช้ raw pub/sub ข้าม TCP)
- `currentLocationTag` ตอนนี้ยังไม่มีใครส่งค่าจริง (LocationDef ยังไม่มี field tag)

## TODO / Known Issues
- **~1% ต่อ tick เป็น placeholder** — ไม่มี per-tag cooldown / per-event cooldown
- `AwaitNextEventHandler` ยังไม่ block/รอจริง — เรียกแล้วถ้าคิวว่างคืน TimedOut ทันที
  และไม่เคารพ `TimeoutSeconds` (ดู [[McpRequestHandlers.cs]] Known Issues)
- Social event ยังไม่ผูกกับ NPC state machine (GDD §3 ต้องการให้ social event trigger
  ตามสถานะ NPC เช่น ทะเลาะกัน, พบศพ — ตอนนี้สุ่มอิสระทั้งหมด)
- Event ที่ deque แล้วไม่มีผลต่อ stat/gameplay — แค่ข้อความ DisplayText
- ~~ไม่มีใครเรียก `Tick`~~ **แก้แล้ว 2026-09-06** — [[GameTickDriver.cs]] เรียก
  `Tick(Time.deltaTime, Player.CurrentLocationId)` ทุกเฟรม (ส่ง location id เป็น
  tag ชั่วคราว — ยังไม่มี location-tag mapping จริง)
