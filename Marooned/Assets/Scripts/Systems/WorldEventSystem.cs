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
