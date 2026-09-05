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
