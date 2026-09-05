using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    [MessagePackObject]
    public class PlayerSurvivalState
    {
        [Key(0)] public float Hunger = 100f;
        [Key(1)] public float Thirst = 100f;
        [Key(2)] public float Mood = 100f;
        [Key(3)] public float Fatigue = 0f;

        /// <summary>Illness/Injury card ids currently affecting the player.</summary>
        [Key(4)] public List<string> ActiveConditionCardIds = new();

        /// <summary>cardId -> count.</summary>
        [Key(5)] public Dictionary<string, int> Inventory = new();

        [Key(6)] public string CurrentLocationId;

        [Key(7)] public bool IsAlive = true;

        /// <summary>Clue cards the player has personally picked up / observed so far.</summary>
        [Key(8)] public List<string> CollectedClueCardIds = new();

        /// <summary>How many times the player has accused someone wrongly. Hitting the cap = loss.</summary>
        [Key(9)] public int WrongAccusations = 0;

        [Key(10)] public ChibiAppearance Avatar = new();
    }
}
