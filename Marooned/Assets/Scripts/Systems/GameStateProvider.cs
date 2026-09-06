using System.Collections.Generic;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>Single live instance, same idea as the reference project's SectStateProvider (Lab 7) — no re-creating state every query.</summary>
    public class GameStateProvider
    {
        /// <summary>id ของผู้เล่นหลัก (single-player) — ทุก call site เดิมชี้ตัวนี้ผ่าน GetPlayer()</summary>
        public const string LocalPlayerId = "player_local";

        // Phase 4 (Multiplayer-ready): เก็บ player แบบ Dictionary แต่ API หน้าตาเดิม —
        // ตัวละครทั้งหมดในเกมปัจจุบันยังใช้ตัวเดียว (player_local) เหมือนเดิมทุกอย่าง
        private readonly Dictionary<string, PlayerSurvivalState> _players = new()
        {
            // Mock starting state so GetGameState has something to show immediately
            // in the first round-trip test. Move this into real save/new-game logic later.
            [LocalPlayerId] = new PlayerSurvivalState
            {
                CurrentLocationId = "beach",
                Inventory = new Dictionary<string, int> { ["food_coconut"] = 1 },
            },
        };

        /// <summary>API เดิม — เรียก GetPlayer() ไม่ใส่ param ได้ผลลัพธ์เดิม (ตัวละครผู้เล่นหลัก)</summary>
        public PlayerSurvivalState GetPlayer(string playerId = LocalPlayerId) => _players[playerId];

        /// <summary>Phase 4 (multiplayer-ready): ได้ player ตาม id โดยสร้างใหม่ให้ถ้ายังไม่มี</summary>
        public PlayerSurvivalState GetOrCreatePlayer(string playerId)
        {
            if (!_players.TryGetValue(playerId, out var state))
            {
                state = new PlayerSurvivalState();
                _players[playerId] = state;
            }
            return state;
        }

        /// <summary>Phase 4 (multiplayer-ready): player ทุกตัวในระบบ (อ่านอย่างเดียว)</summary>
        public IReadOnlyDictionary<string, PlayerSurvivalState> AllPlayers => _players;
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
                // TODO Lab A+1: การ์ดวัสดุ mock สำหรับ WorldItemSystem (Lab B Phase 3) —
                // เพิ่มเข้า DataTables/CardDef.csv จริงเมื่อเปิด pipeline Luban
                ["mat_vine"] = new CardDef
                {
                    Id = "mat_vine", Category = CardCategory.Resource, DisplayName = "เถาวัลย์",
                    SpritePath = "Sprites/Cards/mat_vine", StackLimit = 10,
                },
                ["mat_stone"] = new CardDef
                {
                    Id = "mat_stone", Category = CardCategory.Resource, DisplayName = "หิน",
                    SpritePath = "Sprites/Cards/mat_stone", StackLimit = 10,
                },
                // Phase 4 (Player-as-Killer): weapon card ตัวแรก — ใช้กับ NPC target
                // (CanEliminate ตรวจ same-location + no-witness ก่อน การ์ดจึงไม่หายฟรี)
                ["knife_basic"] = new CardDef
                {
                    Id = "knife_basic", Category = CardCategory.Weapon, DisplayName = "มีด",
                    SpritePath = "Sprites/Cards/knife_basic", StackLimit = 1,
                    TargetType = CardTargetType.SingleTarget,
                    EffectType = CardEffectType.Eliminate,
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
