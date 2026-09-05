using System.Collections.Generic;

namespace Marooned.Shared
{
    /// <summary>A single explorable node on the 2D sandbox map.</summary>
    public class LocationDef
    {
        public string Id;
        public string DisplayName;
        public float WorldX;
        public float WorldY;
        public List<string> ConnectedLocationIds;

        /// <summary>cardId -> spawn weight. Nodes deplete over time (see LocationRuntimeState).</summary>
        public Dictionary<string, int> LootTable;

        /// <summary>Max simultaneous NPCs this node can hold (relevant for "no witness" kill checks).</summary>
        public int Capacity = 4;
    }

    public class RecipeDef
    {
        public string Id;
        public string OutputCardId;
        public int OutputCount = 1;

        /// <summary>cardId -> count consumed.</summary>
        public Dictionary<string, int> Inputs;

        /// <summary>Optional: recipe only usable at these location ids. Empty = anywhere.</summary>
        public List<string> RequiredLocationIds;

        /// <summary>Optional tool card required in inventory but not consumed.</summary>
        public string RequiredToolCardId;
    }
}
