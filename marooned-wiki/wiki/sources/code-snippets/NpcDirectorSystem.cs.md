---
title: NpcDirectorSystem
type: snippet
sources: ["[[sources/npcdirectorsystem-cs]]"]
related:
  - "[[NpcState]]"
  - "[[DeductionSystem.cs]]"
  - "[[LocationDef]]"
  - MessagePipe
  - NpcEliminatedMessage
  - "[[Information-Hiding]]"
folder: Systems
lines: 109
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# NpcDirectorSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs` (109 lines)

## Source
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Owns every NpcState (ground truth, including hidden Role). Moves NPCs between
    /// locations, lets the Killer NPC attempt eliminations when unwitnessed, and
    /// spawns Clue cards on the resulting body / nearby NPCs.
    ///
    /// IMPORTANT: never hand out NpcState directly to MCP query handlers — always go
    /// through DeductionSystem.BuildObservableView (see DeductionVisibilityRules).
    /// </summary>
    public class NpcDirectorSystem
    {
        private readonly Dictionary<string, NpcState> _npcs = new();
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly IPublisher<NpcEliminatedMessage> _eliminatedPublisher;
        private readonly Random _rng = new();

        public IReadOnlyDictionary<string, NpcState> Npcs => _npcs;

        public NpcDirectorSystem(LubanDataService dataService, IPublisher<NpcEliminatedMessage> eliminatedPublisher)
        {
            _locations = dataService.LocationDefs;
            _eliminatedPublisher = eliminatedPublisher;
        }

        /// <summary>
        /// Sets up a round: picks killerCount out of npcIds to be Killer, rest Innocent.
        /// Ratio guidance (confirmed): ~1 killer per 4-8 innocents, Among Us style
        /// (e.g. 5 NPC -> 1 killer, 10 NPC -> 2 killers).
        /// </summary>
        public void SetupRound(IEnumerable<string> npcIds, int killerCount)
        {
            _npcs.Clear();
            var ids = npcIds.ToList();
            var killerIds = ids.OrderBy(_ => _rng.Next()).Take(killerCount).ToHashSet();

            foreach (var id in ids)
            {
                _npcs[id] = new NpcState
                {
                    Id = id,
                    Role = killerIds.Contains(id) ? NpcRole.Killer : NpcRole.Innocent,
                    IsAlive = true,
                    Activity = NpcActivityState.Idle
                };
            }
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcs.Values.Where(n => n.IsAlive))
            {
                TickBehavior(npc, deltaSeconds);
                if (npc.Role == NpcRole.Killer)
                    TryAttemptElimination(npc, deltaSeconds);
            }
        }

        private void TickBehavior(NpcState npc, float deltaSeconds)
        {
            // Placeholder schedule: random idle/gather/rest/travel switching.
            // Replace with a proper daily-schedule table (Luban) in a later lab.
        }

        private void TryAttemptElimination(NpcState killer, float deltaSeconds)
        {
            killer.KillCooldownRemaining = Math.Max(0, killer.KillCooldownRemaining - deltaSeconds);
            if (killer.KillCooldownRemaining > 0) return;

            var sameLocation = _npcs.Values
                .Where(n => n.IsAlive && n.Id != killer.Id && n.CurrentLocationId == killer.CurrentLocationId)
                .ToList();

            // "No witness" rule: only killer + exactly one victim present, nobody else.
            if (sameLocation.Count != 1) return;

            var victim = sameLocation[0];
            if (_rng.NextDouble() > 0.15) return; // small per-tick chance, tune later

            victim.IsAlive = false;
            killer.KillCooldownRemaining = 180f; // seconds

            var clueIds = SpawnClues(killer, victim);
            _eliminatedPublisher.Publish(new NpcEliminatedMessage
            {
                VictimNpcId = victim.Id,
                LocationId = victim.CurrentLocationId,
                SpawnedClueCardIds = clueIds
            });
        }

        private List<string> SpawnClues(NpcState killer, NpcState victim)
        {
            // Simple v1: always drop one visible clue on the victim's location, and a
            // weaker chance of a red herring clue somewhere else. Replace with
            // ClueDef-driven weighted rolls once DataTables/ClueDef.csv is populated.
            var clues = new List<string> { "clue_blood_stain" };
            if (_rng.NextDouble() < 0.3) clues.Add("clue_scratch_mark");
            victim.AllConditionCardIds.AddRange(clues);
            return clues;
        }
    }
}
```

# NpcDirectorSystem


## Purpose
**เจ้าของ ground truth ของ NPC ทุกตัว** (รวม `NpcRole.Killer` ที่ซ่อนจากผู้เล่น/AI) — จัดรอบเกม
(setup killer), ขยับ NPC, ให้ Killer พยายามฆ่าเมื่อไม่มีพยาน และ spawn clue บนเหยื่อ
class นี้ห้าม expose `NpcState` ดิบออกนอก Unity โดยตรง — ต้องผ่าน [[DeductionSystem.cs]] เสมอ

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `IReadOnlyDictionary<string, NpcState> Npcs { get; }` | Ground truth ทั้งหมด — **internal use เท่านั้น** (ห้ามส่งให้ MCP query) |
| `void SetupRound(IEnumerable<string> npcIds, int killerCount)` | ตั้งรอบใหม่ — สุ่มเลือก killerCount ตัวเป็น Killer ที่เหลือ Innocent, ทุกตัว IsAlive + Idle |
| `void Tick(float deltaSeconds)` | เรียกทุก tick — อัปเดต behavior + ให้ Killer ลองฆ่า |

## Dependencies
- **LubanDataService** — `LocationDefs` (inject แล้วแต่ยังไม่ได้ใช้ใน logic จริง — รอ schedule system)
- **MessagePipe** `IPublisher<NpcEliminatedMessage>` — broadcast เมื่อมีการฆาตกรรม
- `NpcState` / `NpcRole` / `NpcActivityState` จาก `Marooned/Assets/Scripts/Shared/NpcState.cs`
- ถูกอ่านโดย [[DeductionSystem.cs]] เท่านั้น (ผู้เดียวที่แปลงเป็น safe view)

## Key Logic
- **SetupRound**: shuffle ids แบบ `OrderBy(_ => _rng.Next()).Take(killerCount)` → HashSet ของ
  killer — ratio guideline ยึด Among Us ~1:4–1:8 (เช่น 5 NPC → 1 killer, 10 NPC → 2)
- **Tick**: loop NPC ที่ยังมีชีวิต → `TickBehavior` (ยังว่างเปล่า) → ถ้าเป็น Killer เรียก
  `TryAttemptElimination`
- **กฎ "no witness"** ใน `TryAttemptElimination`:
  1. ลด `KillCooldownRemaining` — ถ้า > 0 ยังฆ่าไม่ได้
  2. หาผู้มีชีวิตใน location เดียวกับ Killer (ไม่รวมตัวเอง) — ต้องเหลือ **ตัวเดียวพอดี**
     (= เหยื่อ, ไม่มีพยาน) ไม่งั้นข้าม
  3. สุ่ม 15% ต่อ tick ให้ฆ่าสำเร็จ (`_rng.NextDouble() > 0.15` → ยกเลิก)
  4. สำเร็จ: เหยื่อ `IsAlive = false`, ตั้ง cooldown **180 วินาที**, `SpawnClues`, publish
     `NpcEliminatedMessage { VictimNpcId, LocationId, SpawnedClueCardIds }`
- **SpawnClues (v1)**: hardcode `clue_blood_stain` เสมอ + 30% `clue_scratch_mark` —
  ใส่เป็น clue card ใน `victim.AllConditionCardIds` (ควรเปลี่ยนเป็น ClueDef weighted roll)

## TODO / Known Issues
- `TickBehavior` เป็น **placeholder ว่างเปล่า** (`NpcDirectorSystem.cs:65-69`) — NPC ไม่เดิน
  ไม่เปลี่ยน activity; รอ daily-schedule table จาก Luban
- `SpawnClues` hardcode id — รอ `DataTables/Data/ClueDef.csv` ถูกใช้จริง; และยังไม่มี
  red-herring clue ทั้งที่ comment บอกว่าจะทำ
- เหยื่อที่ตายแล้ว `CurrentLocationId` ค้างเดิม = "ศพนิ่ง" ตรวจสอบได้จาก
  `GetVisibleNpcs` (alive=false) — ยังไม่มี report_body flow ที่แท้จริง
- ~~ไม่มีใครเรียก `SetupRound`/`Tick`~~ **แก้แล้ว 2026-09-06** — [[RoundInitializer.cs]]
  เรียก `SetupRound` ตอน Scene โหลด (วาง NPC 5 ตัว, 1 killer, กำหนด `CurrentLocationId`
  เริ่มต้นให้ด้วยเพราะ `SetupRound()` เองยังไม่ทำ) และ [[GameTickDriver.cs]] เรียก
  `Tick(Time.deltaTime)` ทุกเฟรม — ยืนยันผ่าน Play Mode test: `get_visible_npcs`
  คืน 3 ตัวที่ beach
- killerCount ยังไม่มี formula auto-calc ตาม GDD §2.3 (`clamp(round(n/6),1,n/4)`)
