using System.Collections.Generic;

namespace Marooned.Shared
{
    /// <summary>A single explorable node on the 2D sandbox map.</summary>
    public class LocationDef
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public float WorldX;
        public float WorldY;
        public List<string> ConnectedLocationIds = new();

        /// <summary>cardId -> spawn weight. Nodes deplete over time (see LocationRuntimeState).</summary>
        public Dictionary<string, int> LootTable = new();

        /// <summary>Max simultaneous NPCs this node can hold (relevant for "no witness" kill checks).</summary>
        public int Capacity = 4;
    }

    public class RecipeDef
    {
        public string Id = string.Empty;
        public string OutputCardId = string.Empty;
        public int OutputCount = 1;

        /// <summary>cardId -> count consumed.</summary>
        public Dictionary<string, int> Inputs = new();

        /// <summary>Optional: recipe only usable at these location ids. Empty = anywhere.</summary>
        public List<string> RequiredLocationIds = new();

        /// <summary>Optional tool card required in inventory but not consumed.</summary>
        public string RequiredToolCardId = string.Empty;
    }
}
