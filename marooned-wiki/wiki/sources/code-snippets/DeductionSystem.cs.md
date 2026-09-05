---
title: DeductionSystem
type: snippet
sources: [Marooned/Assets/Scripts/Systems/DeductionSystem.cs]
related:
  - "[[NpcDirectorSystem.cs]]"
  - "[[NpcState]]"
  - "[[NpcObservableView]]"
  - "[[IllnessDef]]"
  - "[[Information-Hiding]]"
  - "[[McpRequestHandlers.cs]]"
folder: Systems
lines: 75
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# DeductionSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/DeductionSystem.cs` (75 lines)

## Source
```csharp
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// The one place allowed to translate ground-truth NpcState into what a player
    /// (or the AI agent controlling the player through MCP) is actually allowed to
    /// know. Also resolves accusations. Max wrong-accusation cap enforces the loss
    /// condition described in the design doc.
    /// </summary>
    public class DeductionSystem
    {
        private const int MaxWrongAccusations = 3;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly PlayerSurvivalState _player;
        private readonly Dictionary<string, IllnessDef> _illnessDefs;

        public DeductionSystem(NpcDirectorSystem npcDirector, GameStateProvider stateProvider, LubanDataService dataService)
        {
            _npcDirector = npcDirector;
            _player = stateProvider.Player;
            _illnessDefs = dataService.IllnessDefs;
        }

        /// <summary>Build the safe, MCP-facing view of every NPC the player can currently see (same location).</summary>
        public List<NpcObservableView> GetObservableNpcsAt(string locationId)
        {
            var result = new List<NpcObservableView>();
            foreach (var npc in _npcDirector.Npcs.Values.Where(n => n.CurrentLocationId == locationId))
            {
                result.Add(new NpcObservableView
                {
                    Id = npc.Id,
                    IsAlive = npc.IsAlive,
                    CurrentLocationId = npc.CurrentLocationId,
                    Activity = npc.Activity,
                    VisibleConditionCardIds = FilterVisible(npc.AllConditionCardIds),
                    Avatar = npc.Avatar
                });
            }
            return result;
        }

        private List<string> FilterVisible(List<string> conditionCardIds)
        {
            // Clues (blood stain, scratch mark) default to visible; illness cards check IllnessDef.Visible.
            return conditionCardIds
                .Where(id => !_illnessDefs.TryGetValue(id, out var def) || def.Visible)
                .ToList();
        }

        public (bool wasCorrect, bool win, bool loss, string resultText) Accuse(string targetNpcId)
        {
            if (!_npcDirector.Npcs.TryGetValue(targetNpcId, out var target))
                return (false, false, false, "unknown_npc");

            var correct = target.Role == NpcRole.Killer;

            if (correct)
            {
                target.IsAlive = false; // removed from play
                var anyKillersLeft = _npcDirector.Npcs.Values.Any(n => n.IsAlive && n.Role == NpcRole.Killer);
                return (true, !anyKillersLeft, false, anyKillersLeft ? "correct_but_more_killers_remain" : "all_killers_caught_win");
            }

            _player.WrongAccusations++;
            _player.Mood = System.Math.Max(0, _player.Mood - 15f);
            var lost = _player.WrongAccusations >= MaxWrongAccusations;
            return (false, false, lost, lost ? "too_many_wrong_accusations_loss" : "wrong_accusation");
        }
    }
}
```

# DeductionSystem


## Purpose
**ชั้นเดียวที่ได้รับอนุญาต** ในการแปลง ground-truth `NpcState` เป็นสิ่งที่ผู้เล่น (หรือ AI ที่
ควบคุมผู้เล่นผ่าน MCP) มีสิทธิ์รู้ — หัวใจของ [[Information-Hiding]] แบบ Among Us —
พร้อมทั้งตัดสินการกล่าวหา (accusation) และ enforce เงื่อนไขแพ้

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `List<NpcObservableView> GetObservableNpcsAt(string locationId)` | สร้าง safe view ของ NPC ทุกตัวใน location เดียวกับผู้เล่น — **ไม่มี** `NpcRole`, cooldown หรือ hidden agenda หลุดออกมา |
| `(bool wasCorrect, bool win, bool loss, string resultText) Accuse(string targetNpcId)` | ตัดสินการกล่าวหา — ถูก = killer ตัวนั้นออกจากเกม; ผิด = โทษ mood + นับ wrong accusation |

## Dependencies
- **NpcDirectorSystem** — แหล่ง ground truth (`Npcs` dictionary)
- **GameStateProvider** — อ่าน/เขียน `PlayerSurvivalState` (Mood, WrongAccusations)
- **LubanDataService** — `IllnessDefs` (ใช้ flag `Visible` ตอนกรอง condition card)
- ถูกเรียกโดย: `AccuseNpcHandler`, `GetVisibleNpcsHandler`, `CallMeetingHandler`
  ใน [[McpRequestHandlers.cs]] — handler ทุกตัวที่ต้อง "เห็น NPC" ต้องผ่าน class นี้

## Key Logic
- **การกรอง visibility** (`FilterVisible`): จาก `AllConditionCardIds` ของ NPC —
  clue (เช่น คราบเลือด) แสดงเป็น default; illness/injury card เช็ค `IllnessDef.Visible`
  (ไม่มี def ในตาราง = ถือว่ามองเห็นได้ — `!_illnessDefs.TryGetValue(...)` → ผ่าน)
- **Accuse ฝั่งถูก**: `target.Role == NpcRole.Killer` → `IsAlive = false` → เช็คว่า
  killer เหลือไหม → `(win = true)` เมื่อหมด (resultText: `all_killers_caught_win` /
  `correct_but_more_killers_remain`)
- **Accuse ฝั่งผิด**: `WrongAccusations++`, `Mood = max(0, Mood - 15)` → ครบ
  `MaxWrongAccusations = 3` → `loss = true` (resultText: `too_many_wrong_accusations_loss`)
  — enforce lose condition ตาม GDD §2.4

## TODO / Known Issues
- `MaxWrongAccusations = 3` hardcode — ควรย้ายไป balance table
- "ผู้เล่นเก็บ clue" ยังไม่เชื่อม — `PlayerSurvivalState.CollectedClueCardIds` ไม่เคยถูกเติม
  จากการพบศพ/ตรวจสอบ (ทำให้ `get_clue_board` ตอนนี้ว่างเสมอ)
- ไม่มี "ตรวจสอบศพให้ละเอียดขึ้น" (`investigate_clue`) ที่ `ClueDef.VisibleToBystanders = false`
  ควรใช้
- Accuse ทำงานได้โดยไม่ต้องอยู่ใน Meeting Phase — ยังไม่มี state machine ของ meeting
