using System.Collections.Generic;

namespace Marooned.Shared
{
    public enum CardCategory
    {
        Resource,
        Consumable,
        Tool,
        Illness,
        Injury,
        Clue,
        Craftable
    }

    /// <summary>
    /// Static definition of a card, generated from DataTables/CardDef.csv via Luban.
    /// Plain class (no ScriptableObject) so it can be diffed in git and edited as text.
    /// </summary>
    public class CardDef
    {
        public string Id;
        public CardCategory Category;
        public string DisplayName;
        public string SpritePath;
        public int StackLimit;

        /// <summary>Stat key -> delta applied when the card is used (Hunger/Thirst/Mood/Fatigue).</summary>
        public Dictionary<string, float> StatEffect;

        /// <summary>Only relevant for Illness/Injury: how much each action type is penalized while active.</summary>
        public Dictionary<string, float> ActionPenalty;
    }
}
