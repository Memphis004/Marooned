---
title: CraftingSystem
type: snippet
sources: ["[[sources/cardinventorysystem-cs]]"]
related:
  - "[[CardInventorySystem.cs]]"
  - "[[RecipeDef]]"
  - "[[PlayerSurvivalState]]"
  - "[[LubanDataService.cs]]"
  - "[[McpRequestHandlers.cs]]"
folder: Systems
lines: 74
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# CraftingSystem.cs
**Path:** `Marooned/Assets/Scripts/Systems/CardInventorySystem.cs` (74 lines)

## Source
```csharp
using System.Collections.Generic;
using Marooned.Shared;

namespace Marooned.Systems
{
    public class CardInventorySystem
    {
        private readonly PlayerSurvivalState _state;
        private readonly Dictionary<string, CardDef> _cardDefs; // loaded from Luban-generated JSON at boot

        public CardInventorySystem(GameStateProvider stateProvider, LubanDataService dataService)
        {
            _state = stateProvider.GetPlayer();
            _cardDefs = dataService.CardDefs;
        }

        public bool TryAdd(string cardId, int count = 1)
        {
            if (!_cardDefs.TryGetValue(cardId, out var def)) return false;
            _state.Inventory.TryGetValue(cardId, out var current);
            var next = current + count;
            if (def.StackLimit > 0) next = System.Math.Min(next, def.StackLimit);
            _state.Inventory[cardId] = next;
            return true;
        }

        public bool TryConsume(string cardId, int count = 1)
        {
            if (!_state.Inventory.TryGetValue(cardId, out var current) || current < count) return false;
            _state.Inventory[cardId] = current - count;
            if (_state.Inventory[cardId] <= 0) _state.Inventory.Remove(cardId);
            return true;
        }

        public bool HasAtLeast(string cardId, int count) =>
            _state.Inventory.TryGetValue(cardId, out var current) && current >= count;
    }

    public class CraftingSystem
    {
        private readonly CardInventorySystem _inventory;
        private readonly PlayerSurvivalState _state;
        private readonly Dictionary<string, RecipeDef> _recipes;

        public CraftingSystem(CardInventorySystem inventory, GameStateProvider stateProvider, LubanDataService dataService)
        {
            _inventory = inventory;
            _state = stateProvider.GetPlayer();
            _recipes = dataService.RecipeDefs;
        }

        public (bool success, string failureReason, string outputCardId) TryCraft(string recipeId)
        {
            if (!_recipes.TryGetValue(recipeId, out var recipe))
                return (false, "unknown_recipe", null);

            if (recipe.RequiredLocationIds is { Count: > 0 } && !recipe.RequiredLocationIds.Contains(_state.CurrentLocationId))
                return (false, "wrong_location", null);

            if (!string.IsNullOrEmpty(recipe.RequiredToolCardId) && !_inventory.HasAtLeast(recipe.RequiredToolCardId, 1))
                return (false, "missing_tool", null);

            foreach (var kv in recipe.Inputs)
                if (!_inventory.HasAtLeast(kv.Key, kv.Value))
                    return (false, "missing_ingredients", null);

            foreach (var kv in recipe.Inputs)
                _inventory.TryConsume(kv.Key, kv.Value);

            _inventory.TryAdd(recipe.OutputCardId, recipe.OutputCount);
            return (true, null, recipe.OutputCardId);
        }
    }
}
```

# CraftingSystem

บรรทัด 39-73 เป็นของ class นี้ (ไฟล์เดียวกับ [[CardInventorySystem.cs]], register แยกกันใน VContainer)

## Purpose
คราฟการ์ดจาก `RecipeDef` — ตรวจเงื่อนไข (recipe มีจริง, สถานที่, tool, วัตถุดิบ) แล้วหัก
input ออกจาก inventory พร้อมเติม output เข้า inventory

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `(bool success, string failureReason, string outputCardId) TryCraft(string recipeId)` | คราฟตาม recipe id — คืนผลสำเร็จ/เหตุผลที่ล้มเหลว (machine-readable)/การ์ดที่ได้ |

## Dependencies
- **CardInventorySystem** — ใช้ `HasAtLeast`/`TryConsume`/`TryAdd`
- **GameStateProvider** — อ่าน `CurrentLocationId` เพื่อเช็คเงื่อนไขสถานที่
- **LubanDataService** — `RecipeDefs` dictionary
- เรียกใช้โดย: `CraftCardHandler` ใน [[McpRequestHandlers.cs]] (MCP tool `craft_card`)

## Key Logic
ลำดับตรวจใน `TryCraft` (คืน failureReason แรกที่เจอ — machine-readable เหมาะกับ AI agent):
1. `unknown_recipe` — recipeId ไม่มีใน `RecipeDefs`
2. `wrong_location` — recipe ระบุ `RequiredLocationIds` และผู้เล่นไม่ได้อยู่ location ใดในนั้น
   (list ว่าง = คราฟที่ไหนก็ได้)
3. `missing_tool` — `RequiredToolCardId` ระบุไว้แต่ไม่มีใน inventory (**tool ไม่ถูกหัก** —
   แค่ต้องมี)
4. `missing_ingredients` — วัตถุดิบใน `Inputs` (cardId → count) ไม่ครบ
5. ผ่านครบ → หักวัตถุดิบทุกรายการ → `TryAdd(outputCardId, outputCount)` → success

## TODO / Known Issues
- คราฟสำเร็จไม่มี event/message publish — UI จะไม่รู้จนกว่าจะ query ใหม่
- `OutputCardId` คืน `null` ในกรณีล้มเหลว (tuple, ไม่ใช่ nullable annotation)
- ไม่มี `ActionPenalty` จาก illness card มาลดโอกาสสำเร็จ (GDD §2.1 ระบุว่า illness ควร
  "คราฟพลาดง่ายขึ้น" — ยังไม่ implement)
- ตาราง recipe จริงมีแค่ `recipe_cook_fish` ใน mock ของ [[LubanDataService.cs]]
  (CSV มี `tool_campfire` เป็น required tool แต่ mock ไม่ได้ใส่)
