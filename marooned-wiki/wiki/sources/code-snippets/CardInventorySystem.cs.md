---
title: CardInventorySystem
type: snippet
sources: [Marooned/Assets/Scripts/Systems/CardInventorySystem.cs]
related:
  - "[[CraftingSystem.cs]]"
  - "[[CardDef]]"
  - "[[PlayerSurvivalState]]"
  - "[[LubanDataService.cs|GameStateProvider]]"
  - "[[LubanDataService.cs]]"
folder: Systems
lines: 74
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
---

# CardInventorySystem.cs
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
            _state = stateProvider.Player;
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
            _state = stateProvider.Player;
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

# CardInventorySystem

`CardInventorySystem` + [[CraftingSystem.cs]] ดูเอกสารแยกของ CraftingSystem)

## Purpose
จัดการ inventory การ์ดของผู้เล่น (เพิ่ม/หัก/เช็คจำนวน) บน `PlayerSurvivalState.Inventory`
โดยเคารพ `CardDef.StackLimit` — เป็นผู้ถือความจริงเรื่อง "ผู้เล่นมีการ์ดอะไรกี่ใบ"

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `bool TryAdd(string cardId, int count = 1)` | เพิ่มการ์ดเข้า inventory — คืน false ถ้า cardId ไม่มีใน `CardDefs`; จำกัดด้วย `StackLimit` (ค่า > 0 เท่านั้น) |
| `bool TryConsume(string cardId, int count = 1)` | หักการ์ด — คืน false ถ้ามีไม่พอ; ลบ key ทิ้งเมื่อจำนวนถึง 0 |
| `bool HasAtLeast(string cardId, int count)` | เช็คว่ามีการ์ดอย่างน้อย count ใบ (ไม่แก้ state) |

## Dependencies
- **GameStateProvider** — อ่าน/เขียน `PlayerSurvivalState.Inventory`
- **LubanDataService** — ใช้ `CardDefs` dictionary ตรวจว่า card id มีจริง + อ่าน `StackLimit`
- เรียกใช้โดย: [[CraftingSystem.cs]], [[ExplorationSystem.cs]], `UseCardHandler` ใน [[McpRequestHandlers.cs]]

## Key Logic
- Inventory เป็น `Dictionary<string, int>` (cardId → count) เก็บใน `PlayerSurvivalState`
  ซึ่ง serialize ได้ด้วย MessagePack — ระบบนี้ mutate ตรง ไม่มี event publish
- `TryAdd`: ตรวจ def ก่อนเสมอ (การ์ดที่ไม่มีนิยามเพิ่มไม่ได้) → `next = current + count` →
  clamp ด้วย `StackLimit` เมื่อ `StackLimit > 0`
- `TryConsume`: ตรวจก่อนว่ามีพอ → หัก → ถ้าเหลือ 0 ลบ key ออกจาก dict (ไม่เก็บ count 0 ไว้)

## TODO / Known Issues
- ไม่มี event notification เมื่อ inventory เปลี่ยน (Presenter ของ `CardHandView` วางแผน
  subscribe "inventory-changed message" ตาม comment ใน
  `Marooned/Assets/Scripts/UI/Presenters/CardHandPresenter.cs` — ยังไม่มี message นี้)
- การ์ดชนิด Illness/Injury ใช้ TryAdd ได้ปกติ ไม่มีการแยก validate ตาม `CardCategory`
- `_cardDefs` ที่ใช้ตอนนี้มาจาก mock ของ [[LubanDataService.cs]] (ยังไม่ได้โหลด Luban จริง)
