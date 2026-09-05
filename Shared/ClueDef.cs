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
        public string Id;
        public string DisplayName;   // "คราบเลือด", "รอยขีดข่วน", "รอยเท้าเปื้อนโคลน"
        public string SpritePath;
        public ClueReliability Reliability;
        public bool VisibleToBystanders; // false = ต้องเข้าไป "ตรวจสอบ" (investigate_clue) ถึงจะเห็น
    }

    public class IllnessDef
    {
        public string Id;
        public string DisplayName;
        public bool Visible; // true = NPC อื่นมองเห็น (บาดแผล/ผ้าพันแผล), false = ภายใน (ไข้)
        public string CureCardId;
        public float SeverityGrowthPerHour;
    }

    public class WorldEventDef
    {
        public string Id;
        public string Group; // "Survival" | "Social"
        public int Weight;
        public string DisplayText;
        public List<string> RequiredLocationTags;
    }
}
