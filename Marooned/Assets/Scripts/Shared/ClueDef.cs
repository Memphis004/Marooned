using System.Collections.Generic;

namespace Marooned.Shared
{
    public enum ClueReliability
    {
        Strong,
        Weak,
        RedHerring
    }

    public class ClueDef
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;   // "คราบเลือด", "รอยขีดข่วน", "รอยเท้าเปื้อนโคลน"
        public string SpritePath = string.Empty;
        public ClueReliability Reliability;
        public bool VisibleToBystanders; // false = ต้องเข้าไป "ตรวจสอบ" (investigate_clue) ถึงจะเห็น
    }

    public class IllnessDef
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public bool Visible; // true = NPC อื่นมองเห็น (บาดแผล/ผ้าพันแผล), false = ภายใน (ไข้)
        public string CureCardId = string.Empty;
        public float SeverityGrowthPerHour;
    }

    public class WorldEventDef
    {
        public string Id = string.Empty;
        public string Group = string.Empty; // "Survival" | "Social"
        public int Weight;
        public string DisplayText = string.Empty;
        public List<string> RequiredLocationTags = new();
    }
}
