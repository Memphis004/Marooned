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
  - "[[card-system]]"
folder: Systems
lines: 176
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
  - phase-4
---

# NpcDirectorSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs` (176 lines)

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
    /// Phase 4 (Player-as-Killer): TryEliminate/CanEliminate เป็น method กลางที่ทั้ง
    /// AI killer (Tick) และ player (weapon card ผ่าน UseCardHandler) ใช้ร่วมกัน
    ///
    /// IMPORTANT: never hand out NpcState directly to MCP query handlers — always go
    /// through DeductionSystem.BuildObservableView (see DeductionVisibilityRules).
    /// </summary>
    public class NpcDirectorSystem
    {
        private readonly Dictionary<string, NpcState> _npcs = new();
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly IPublisher<NpcEliminatedMessage> _eliminatedPublisher;
        private readonly IPublisher<NpcLocationChangedMessage> _npcLocationPublisher;
        private readonly Random _rng = new();

        public IReadOnlyDictionary<string, NpcState> Npcs => _npcs;

        public NpcDirectorSystem(LubanDataService dataService, IPublisher<NpcEliminatedMessage> eliminatedPublisher,
            IPublisher<NpcLocationChangedMessage> npcLocationPublisher)
        {
            _locations = dataService.LocationDefs;
            _eliminatedPublisher = eliminatedPublisher;
            _npcLocationPublisher = npcLocationPublisher;
        }

        /// <summary>
        /// ย้าย NPC ไป location ใหม่ + Publish NpcLocationChangedMessage (Lab B)
        /// ให้ Visual layer (เช่น ChibiSpawnerView) subscribe แทนการ polling
        /// เป็นจุดเดียวที่อนุญาตให้ mutate CurrentLocationId — ระบบอื่นต้องเรียก method นี้
        /// </summary>
        public void MoveNpc(string npcId, string newLocationId)
        {
            if (!_npcs.TryGetValue(npcId, out var npc)) return;

            var oldLocationId = npc.CurrentLocationId;
            if (oldLocationId == newLocationId) return;

            npc.CurrentLocationId = newLocationId;
            _npcLocationPublisher.Publish(new NpcLocationChangedMessage
            {
                NpcId = npcId,
                OldLocationId = oldLocationId,
                NewLocationId = newLocationId
            });
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

        /// <summary>
        /// Phase 4 (Player-as-Killer): เช็คเงื่อนไขการ eliminate โดยไม่ mutate state ใดๆ (dry-run)
        /// — UseCardHandler เรียกก่อนหักการ์ด เพื่อกันการ์ดหายฟรีเมื่อลงมือไม่สำเร็จ (Safe UX)
        /// </summary>
        public (bool success, string reason) CanEliminate(string killerEntityId, string victimNpcId, string killerLocationId)
        {
            if (!_npcs.TryGetValue(victimNpcId, out var victim))
                return (false, "unknown_target");
            if (!victim.IsAlive)
                return (false, "target_already_dead");
            if (victim.CurrentLocationId != killerLocationId)
                return (false, "target_not_same_location");

            // กฎ "no witness" — นับ NPC อื่นที่มีชีวิตใน location เดียวกัน (ยกเว้น killer + victim)
            var witnessCount = _npcs.Values.Count(n =>
                n.IsAlive && n.Id != victimNpcId && n.Id != killerEntityId
                && n.CurrentLocationId == killerLocationId);
            if (witnessCount > 0)
                return (false, "witnessed");

            return (true, null);
        }

        /// <summary>
        /// Method กลางสำหรับ eliminate (ทั้ง AI killer และ player ผ่าน weapon card):
        /// ตรวจเงื่อนไขผ่าน CanEliminate ก่อน แล้วค่อย mutate state + spawn clues + publish
        /// NpcEliminatedMessage (event เดียวกันสำหรับทุก killer — visual layer ไม่ต้องรู้ต้นตอ)
        /// </summary>
        public (bool success, string reason) TryEliminate(string killerEntityId, string victimNpcId, string killerLocationId)
        {
            var (can, reason) = CanEliminate(killerEntityId, victimNpcId, killerLocationId);
            if (!can) return (false, reason);

            var victim = _npcs[victimNpcId]; // CanEliminate รับประกัน key มีอยู่แล้ว
            victim.IsAlive = false;

            var clueIds = SpawnClues(killerEntityId, victim);
            _eliminatedPublisher.Publish(new NpcEliminatedMessage
            {
                VictimNpcId = victim.Id,
                LocationId = victim.CurrentLocationId,
                SpawnedClueCardIds = clueIds
            });
            return (true, null);
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

            // Phase 4: ลงมือผ่าน TryEliminate กลาง (กฎ witness/clue/publish อยู่ที่เดียวกับ player)
            var (success, _) = TryEliminate(killer.Id, victim.Id, killer.CurrentLocationId);
            if (success)
                killer.KillCooldownRemaining = 180f; // seconds
        }

        private List<string> SpawnClues(string killerEntityId, NpcState victim)
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
(setup killer), ขยับ NPC, และเป็น**เจ้าของกติกาการ eliminate แบบรวมศูนย์** (Phase 4):
ทั้ง AI killer และ player (ผ่าน weapon card) ใช้ `CanEliminate`/`TryEliminate` ตัวเดียวกัน
class นี้ห้าม expose `NpcState` ดิบออกนอก Unity โดยตรง — ต้องผ่าน [[DeductionSystem.cs]] เสมอ

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `IReadOnlyDictionary<string, NpcState> Npcs { get; }` | Ground truth ทั้งหมด — **internal use เท่านั้น** (ห้ามส่งให้ MCP query) |
| `void SetupRound(IEnumerable<string> npcIds, int killerCount)` | ตั้งรอบใหม่ — สุ่มเลือก killerCount ตัวเป็น Killer ที่เหลือ Innocent, ทุกตัว IsAlive + Idle |
| `void MoveNpc(string npcId, string newLocationId)` | ย้าย NPC + publish `NpcLocationChangedMessage` (Lab B) — จุดเดียวที่ mutate `CurrentLocationId` ได้ |
| `void Tick(float deltaSeconds)` | เรียกทุก tick — อัปเดต behavior + ให้ Killer ลองฆ่า |
| `(bool, string) CanEliminate(killerEntityId, victimNpcId, killerLocationId)` | **Phase 4** — dry-run เช็คเงื่อนไข (ไม่ mutate): target มีอยู่/ยังมีชีวิต/อยู่โซนเดียวกัน/ไม่มี witness. คืน reason: `unknown_target`, `target_already_dead`, `target_not_same_location`, `witnessed` |
| `(bool, string) TryEliminate(killerEntityId, victimNpcId, killerLocationId)` | **Phase 4** — method กลางลงมือจริง: เรียก `CanEliminate` ก่อน → `IsAlive=false` → `SpawnClues` → publish `NpcEliminatedMessage` |

## Dependencies
- **LubanDataService** — `LocationDefs` (inject แล้วแต่ยังไม่ได้ใช้ใน logic จริง — รอ schedule system)
- **MessagePipe** `IPublisher<NpcEliminatedMessage>` — broadcast เมื่อมีการฆาตกรรม (ทั้งจาก AI และ player)
- **MessagePipe** `IPublisher<NpcLocationChangedMessage>` — broadcast การย้ายโซน (Lab B, ให้ ChibiSpawnerView)
- `NpcState` / `NpcRole` / `NpcActivityState` จาก `Marooned/Assets/Scripts/Shared/NpcState.cs`
- ถูกอ่านโดย [[DeductionSystem.cs]] (safe view) และ [[McpRequestHandlers.cs]] ([[UseCardHandler]] เรียก `CanEliminate`/`TryEliminate` — Phase 4)

## Key Logic
- **SetupRound**: shuffle ids แบบ `OrderBy(_ => _rng.Next()).Take(killerCount)` → HashSet ของ
  killer — ratio guideline ยึด Among Us ~1:4–1:8 (เช่น 5 NPC → 1 killer, 10 NPC → 2)
- **Tick**: loop NPC ที่ยังมีชีวิต → `TickBehavior` (ยังว่างเปล่า) → ถ้าเป็น Killer เรียก
  `TryAttemptElimination`
- **TryEliminate (Phase 4 — กลาง)**: จุดเดียวที่ตัดสินว่า "ฆ่าสำเร็จ" ได้ไหม — ทั้ง AI killer
  และ player (weapon card ผ่าน [[UseCardHandler]]) ผ่าน path เดียวกัน ทำให้กฎ witness,
  การ spawn clue และ event `NpcEliminatedMessage` สอดคล้องกันเสมอ (visual layer เช่น
  ChibiSpawnerView ไม่ต้องรู้ว่าใครเป็น killer — แค่เห็น event)
- **CanEliminate (Phase 4 — dry-run)**: เช็ค 4 เงื่อนไข**โดยไม่ mutate state** — แยกออกจาก
  `TryEliminate` เพื่อให้ UseCardHandler ตรวจ**ก่อนหักการ์ด** (Safe UX — การ์ดไม่หายฟรีเมื่อ
  มี witness หรือ target ไม่อยู่โซนเดียวกัน)
- **กฎ "no witness"**: นับ NPC ที่มีชีวิตอยู่ location เดียวกัน (ยกเว้น killer + victim) —
  ถ้า > 0 คือมีพยาน ฆ่าไม่ได้
- **TryAttemptElimination (AI path)**: ยังเป็นของ AI killer เท่านั้น — ลด cooldown → หา
  เหยื่อที่อยู่ลำพังกับ killer → สุ่ม 15% ต่อ tick → ลงมือผ่าน `TryEliminate` กลาง →
  สำเร็จตั้ง cooldown **180 วินาที**
- **SpawnClues (v1)**: hardcode `clue_blood_stain` เสมอ + 30% `clue_scratch_mark` —
  ใส่เป็น clue card ใน `victim.AllConditionCardIds` (ควรเปลี่ยนเป็น ClueDef weighted roll)

## TODO / Known Issues
- `TickBehavior` เป็น **placeholder ว่างเปล่า** — NPC ไม่เดิน
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
- [[DeductionSystem.cs]] `Accuse()` ยัง set `IsAlive = false` ตรง (ไม่ผ่าน TryEliminate) —
  ยังไม่ spawn clue; พิจารณารวมเข้า path กลางในอนาคต
