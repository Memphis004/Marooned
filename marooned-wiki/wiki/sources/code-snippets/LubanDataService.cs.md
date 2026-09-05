---
title: LubanDataService
type: snippet
sources: [Marooned/Assets/Scripts/Systems/GameStateProvider.cs]
related:
  - Luban
  - "[[LubanDataService.cs|GameStateProvider]]"
  - "[[CardDef]]"
  - "[[LocationDef]]"
  - "[[RecipeDef]]"
  - "[[ClueDef]]"
  - "[[IllnessDef]]"
  - "[[WorldEventDef]]"
  - "[[ChibiPartDef]]"
folder: Systems
lines: 158
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# LubanDataService.cs
**Path:** `Marooned/Assets/Scripts/Systems/GameStateProvider.cs` (158 lines)

## Source
```csharp
using System.Collections.Generic;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>Single live instance, same idea as the reference project's SectStateProvider (Lab 7) — no re-creating state every query.</summary>
    public class GameStateProvider
    {
        public PlayerSurvivalState Player { get; } = new()
        {
            // Mock starting state so GetGameState has something to show immediately
            // in the first round-trip test. Move this into real save/new-game logic later.
            CurrentLocationId = "beach",
            Inventory = new Dictionary<string, int> { ["food_coconut"] = 1 },
        };
    }

    /// <summary>
    /// Loads Luban-generated JSON (post Excel->JSON->C# pipeline) into plain
    /// dictionaries at boot. LoadAll() below is TEMPORARY MOCK DATA hand-copied
    /// from the DataTables/*.csv drafts, just enough to unblock the first MCP
    /// round-trip test. Replace with real Luban-generated loader calls once the
    /// Excel pipeline (Tools/Luban/gen.bat) is wired up — search this file for
    /// "TODO Lab A+1" when you get there.
    /// </summary>
    public class LubanDataService
    {
        public Dictionary<string, CardDef> CardDefs { get; private set; } = new();
        public Dictionary<string, LocationDef> LocationDefs { get; private set; } = new();
        public Dictionary<string, RecipeDef> RecipeDefs { get; private set; } = new();
        public Dictionary<string, ClueDef> ClueDefs { get; private set; } = new();
        public Dictionary<string, IllnessDef> IllnessDefs { get; private set; } = new();
        public Dictionary<string, WorldEventDef> WorldEventDefs { get; private set; } = new();
        public Dictionary<string, ChibiPartDef> ChibiPartDefs { get; private set; } = new();
        public Dictionary<string, ChibiOutfitDef> ChibiOutfitDefs { get; private set; } = new();

        public LubanDataService()
        {
            // Called from the constructor (not a separate lifecycle hook) so mock
            // data is guaranteed loaded the moment VContainer resolves this
            // singleton — no dependency on IInitializable/EntryPoint ordering
            // while that part of the DI wiring is still being worked out.
            LoadAll();
        }

        // TODO Lab A+1: replace this whole method body with real Luban-generated
        // loader calls, e.g. CardDefs = LubanTables.CardDefTable.ToDictionary(x => x.Id);
        public void LoadAll()
        {
            CardDefs = new Dictionary<string, CardDef>
            {
                ["food_coconut"] = new CardDef
                {
                    Id = "food_coconut", Category = CardCategory.Resource, DisplayName = "มะพร้าว",
                    SpritePath = "Sprites/Cards/food_coconut", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 15f },
                },
                ["water_bottle"] = new CardDef
                {
                    Id = "water_bottle", Category = CardCategory.Resource, DisplayName = "น้ำขวด",
                    SpritePath = "Sprites/Cards/water_bottle", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Thirst"] = 20f },
                },
                ["raw_fish"] = new CardDef
                {
                    Id = "raw_fish", Category = CardCategory.Resource, DisplayName = "ปลาดิบ",
                    SpritePath = "Sprites/Cards/raw_fish", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 10f, ["Mood"] = -5f },
                },
                ["cooked_fish"] = new CardDef
                {
                    Id = "cooked_fish", Category = CardCategory.Craftable, DisplayName = "ปลาย่าง",
                    SpritePath = "Sprites/Cards/cooked_fish", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 25f, ["Mood"] = 5f },
                },
                ["illness_malnutrition"] = new CardDef
                {
                    Id = "illness_malnutrition", Category = CardCategory.Illness, DisplayName = "ภาวะขาดสารอาหาร",
                    SpritePath = "Sprites/Cards/illness_malnutrition", StackLimit = 1,
                    ActionPenalty = new Dictionary<string, float> { ["Explore"] = -0.3f, ["Craft"] = -0.2f },
                },
                ["illness_dehydration"] = new CardDef
                {
                    Id = "illness_dehydration", Category = CardCategory.Illness, DisplayName = "ภาวะขาดน้ำ",
                    SpritePath = "Sprites/Cards/illness_dehydration", StackLimit = 1,
                    ActionPenalty = new Dictionary<string, float> { ["Explore"] = -0.4f },
                },
            };

            LocationDefs = new Dictionary<string, LocationDef>
            {
                ["beach"] = new LocationDef
                {
                    Id = "beach", DisplayName = "ชายหาด", WorldX = 0, WorldY = 0,
                    ConnectedLocationIds = new List<string> { "jungle_edge", "cave_entrance" },
                    LootTable = new Dictionary<string, int> { ["food_coconut"] = 5, ["water_bottle"] = 2, ["raw_fish"] = 3 },
                    Capacity = 6,
                },
                ["jungle_edge"] = new LocationDef
                {
                    Id = "jungle_edge", DisplayName = "ชายป่า", WorldX = 10, WorldY = 5,
                    ConnectedLocationIds = new List<string> { "beach", "deep_jungle" },
                    LootTable = new Dictionary<string, int> { ["food_coconut"] = 3 },
                    Capacity = 4,
                },
                ["deep_jungle"] = new LocationDef
                {
                    Id = "deep_jungle", DisplayName = "ป่าลึก", WorldX = 20, WorldY = 10,
                    ConnectedLocationIds = new List<string> { "jungle_edge" },
                    LootTable = new Dictionary<string, int> { ["raw_fish"] = 1 },
                    Capacity = 3,
                },
                ["cave_entrance"] = new LocationDef
                {
                    Id = "cave_entrance", DisplayName = "ปากถ้ำ", WorldX = -5, WorldY = 8,
                    ConnectedLocationIds = new List<string> { "beach" },
                    LootTable = new Dictionary<string, int> { ["water_bottle"] = 4 },
                    Capacity = 3,
                },
            };

            RecipeDefs = new Dictionary<string, RecipeDef>
            {
                ["recipe_cook_fish"] = new RecipeDef
                {
                    Id = "recipe_cook_fish", OutputCardId = "cooked_fish", OutputCount = 1,
                    Inputs = new Dictionary<string, int> { ["raw_fish"] = 1 },
                },
            };

            ClueDefs = new Dictionary<string, ClueDef>
            {
                ["clue_blood_stain"] = new ClueDef
                {
                    Id = "clue_blood_stain", DisplayName = "คราบเลือด", SpritePath = "Sprites/Clues/blood_stain",
                    Reliability = ClueReliability.Strong, VisibleToBystanders = true,
                },
                ["clue_scratch_mark"] = new ClueDef
                {
                    Id = "clue_scratch_mark", DisplayName = "รอยขีดข่วน", SpritePath = "Sprites/Clues/scratch_mark",
                    Reliability = ClueReliability.Weak, VisibleToBystanders = true,
                },
            };

            IllnessDefs = new Dictionary<string, IllnessDef>
            {
                ["illness_malnutrition"] = new IllnessDef { Id = "illness_malnutrition", DisplayName = "ภาวะขาดสารอาหาร", Visible = false, CureCardId = "cooked_fish", SeverityGrowthPerHour = 0.5f },
                ["illness_dehydration"] = new IllnessDef { Id = "illness_dehydration", DisplayName = "ภาวะขาดน้ำ", Visible = false, CureCardId = "water_bottle", SeverityGrowthPerHour = 0.8f },
            };

            WorldEventDefs = new Dictionary<string, WorldEventDef>
            {
                ["event_storm"] = new WorldEventDef { Id = "event_storm", Group = "Survival", Weight = 10, DisplayText = "พายุเข้า ทำให้ Fatigue ลดเร็วขึ้นชั่วคราว" },
                ["event_npc_argument"] = new WorldEventDef { Id = "event_npc_argument", Group = "Social", Weight = 6, DisplayText = "NPC สองคนทะเลาะกัน เผยข้อมูลความสัมพันธ์" },
            };
        }
    }
}
```

# LubanDataService

บรรทัด 26-158 เป็นของ class นี้ (บรรทัด 7-16 คือ [[LubanDataService.cs|GameStateProvider]] อีก class หนึ่งในไฟล์เดียวกัน)

## Purpose
ที่เดียวสำหรับอ่าน static definition tables (การ์ด/สถานที่/recipe/เบาะแส/โรค/event/chibi part)
แบบ typed dictionary — ตามแผนควรโหลดจาก Luban-generated JSON แต่ตอนนี้**ทั้งหมดเป็น
mock data ที่พิมพ์มือ**ลอกจาก CSV drafts

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `Dictionary<string, CardDef> CardDefs` | นิยามการ์ดทั้งหมด (StatEffect, ActionPenalty, StackLimit) |
| `Dictionary<string, LocationDef> LocationDefs` | Map node + LootTable + connectivity + Capacity |
| `Dictionary<string, RecipeDef> RecipeDefs` | สูตรคราฟ |
| `Dictionary<string, ClueDef> ClueDefs` | เบาะแส (Reliability, VisibleToBystanders) |
| `Dictionary<string, IllnessDef> IllnessDefs` | โรค/บาดแผล (Visible, CureCardId) |
| `Dictionary<string, WorldEventDef> WorldEventDefs` | event กลุ่ม Survival/Social |
| `Dictionary<string, ChibiPartDef> ChibiPartDefs` | ส่วนประกอบ chibi (ตอนนี้**ว่างเปล่า**) |
| `Dictionary<string, ChibiOutfitDef> ChibiOutfitDefs` | ชุด chibi (ตอนนี้**ว่างเปล่า**) |
| `void LoadAll()` | เติม mock data ทั้งหมด (ถูกเรียกจาก constructor) |

## Dependencies
- Model classes จาก `Marooned/Assets/Scripts/Shared/` (CardDef, LocationDef, RecipeDef,
  ClueDef, IllnessDef, WorldEventDef, ChibiPartDef, ChibiOutfitDef)
- **ไม่พึ่ง Luban runtime เลยในปัจจุบัน** — ต่างจากไฟล์ generated
  `Marooned/Assets/Scripts/Data/Gen/` (namespace `cfg`) ที่ยังไม่มีใครเรียกใช้
- ถูก inject ให้ระบบเกือบทุกตัว: CardInventorySystem, CraftingSystem, ExplorationSystem,
  NpcDirectorSystem, DeductionSystem, WorldEventSystem, McpRequestHandlers

## Key Logic
- โหลด mock ใน **constructor** (ไม่ใช่ IInitializable) เพื่อ guarantee ว่าข้อมูลพร้อมทันทีที่
  VContainer resolve singleton — ไม่ต้องพึ่งลำดับ lifecycle ที่ยัง wire ไม่เสร็จ
- ข้อมูล mock ลอกจาก `DataTables/Data/*.csv`:
  - การ์ด 6 ชนิด: food_coconut, water_bottle, raw_fish, cooked_fish, illness_malnutrition,
    illness_dehydration
  - location 4 จุด: beach → jungle_edge → deep_jungle / cave_entrance (graph)
  - recipe 1 สูตร: `recipe_cook_fish` (raw_fish → cooked_fish — ไม่ใส่ required tool
    ทั้งที่ CSV มี `tool_campfire`)
  - clue 2 (Strong/Weak), illness 2, event 2 (Survival/Social อย่างละ 1)

## TODO / Known Issues
- **TODO Lab A+1 ในไฟล์เอง** (`GameStateProvider.cs:46-47`): แทน body ของ `LoadAll()`
  ด้วยการโหลดจาก Luban-generated tables เช่น
  `CardDefs = LubanTables.CardDefTable.ToDictionary(x => x.Id)`
- JSON output จริง generate แล้วอยู่ที่ `Marooned/Assets/Resources/DataTables/*.json`
  (6 ไฟล์) แต่**ยังไม่มีใครอ่าน**; ต้องเขียน mapper `cfg.game.*` (flat columns เช่น
  `hungerDelta`) → `Marooned.Shared.*` (Dictionary `StatEffect`)
- `ChibiPartDefs` / `ChibiOutfitDefs` ว่าง — และ `ChibiPartDef.csv` ยังไม่อยู่ใน Luban
  pipeline (ไม่มี `TbChibiPartDef` ใน `Data/Gen/Tables.cs`)
- mock ไม่ได้ใส่ `tool_campfire` ทำให้ recipe จาก CSV คราฟไม่ได้เมื่อเทียบกับ data จริง
- ตาราง `RequiredLocationTags` ของ event ยังไม่มีข้อมูล mock (ต่างจาก CSV ที่มี tag "jungle")
