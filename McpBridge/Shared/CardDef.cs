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
        Craftable,
        Weapon // Phase 4: แยกจาก Tool ชัดเจน — การ์ดที่ใช้กับ target อื่น (เช่นฆ่า NPC)
    }

    /// <summary>Phase 4: การ์ดนี้ต้องการ target แบบไหน (ค่าเริ่มต้น Self = พฤติกรรมเดิม)</summary>
    public enum CardTargetType
    {
        None,         // ไม่ต้องมี target
        Self,         // ใช้กับตัวเอง (ค่าเริ่มต้น — backward compatible)
        SingleTarget, // ต้องระบุ TargetId ตอนใช้ (มีด, ของขวัญ)
    }

    /// <summary>Phase 4: ผลของการ์ดเมื่อถูกใช้ (ค่าเริ่มต้น StatDelta = พฤติกรรมเดิม)</summary>
    public enum CardEffectType
    {
        StatDelta,  // ค่าเริ่มต้น — ใช้ StatEffect dictionary เหมือนเดิม
        Eliminate,  // Weapon: เรียก NpcDirectorSystem.TryEliminate(TargetId)
        Cure,       // TODO Phase 4+: ลบ condition card — ยังไม่ implement
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

        // ---- Phase 4 (Player-as-Killer): targeting + effect ----
        /// <summary>ค่าเริ่มต้น Self = การ์ดเดิมทุกใบทำงานเหมือนเดิม (backward compatible)</summary>
        public CardTargetType TargetType = CardTargetType.Self;

        /// <summary>ค่าเริ่มต้น StatDelta = ใช้ StatEffect dictionary เหมือนเดิม (backward compatible)</summary>
        public CardEffectType EffectType = CardEffectType.StatDelta;

        /// <summary>Stat key -> delta applied when the card is used (Hunger/Thirst/Mood/Fatigue).</summary>
        public Dictionary<string, float> StatEffect;

        /// <summary>Only relevant for Illness/Injury: how much each action type is penalized while active.</summary>
        public Dictionary<string, float> ActionPenalty;
    }
}
