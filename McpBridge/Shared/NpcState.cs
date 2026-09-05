using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    public enum NpcRole
    {
        Innocent,
        Killer,
        Neutral // เอาตัวรอดอย่างเดียว ไม่นับเป็นทั้งฝ่ายดี/ร้าย (เปิดใช้ทีหลังได้)
    }

    public enum NpcActivityState
    {
        Idle,
        Gathering,
        Resting,
        Traveling,
        Talking
    }

    /// <summary>
    /// FULL state, including ground truth (Role, hidden agenda). This class must
    /// only ever be held server-side (Unity SectStateProvider equivalent) and must
    /// NEVER be serialized directly into an MCP query response. Build an
    /// NpcObservableView from it instead — see Systems/DeductionVisibilityRules.cs.
    /// </summary>
    [MessagePackObject]
    public class NpcState
    {
        [Key(0)] public string Id;
        [Key(1)] public NpcRole Role;
        [Key(2)] public bool IsAlive = true;
        [Key(3)] public string CurrentLocationId;
        [Key(4)] public NpcActivityState Activity;

        /// <summary>All condition cards, including ones a bystander could not actually notice yet.</summary>
        [Key(5)] public List<string> AllConditionCardIds = new();

        [Key(6)] public ChibiAppearance Avatar = new();

        /// <summary>Killer-only: cooldown seconds remaining before another Eliminate action is possible.</summary>
        [Key(7)] public float KillCooldownRemaining;

        /// <summary>Killer-only optional objective, e.g. "eliminate 3 before day 5" — for future killer-AI mode.</summary>
        [Key(8)] public string HiddenAgendaId;
    }

    /// <summary>
    /// SAFE view of an NPC for MCP / player-facing queries. Only contains what a
    /// bystander standing in the same location could plausibly observe.
    /// </summary>
    [MessagePackObject]
    public class NpcObservableView
    {
        [Key(0)] public string Id;
        [Key(1)] public bool IsAlive;
        [Key(2)] public string CurrentLocationId;
        [Key(3)] public NpcActivityState Activity;

        /// <summary>Only condition cards flagged Visible=true in IllnessDef/injury def (e.g. visible scratch, not internal fever unless player checks closely).</summary>
        [Key(4)] public List<string> VisibleConditionCardIds = new();

        [Key(5)] public ChibiAppearance Avatar = new();
    }
}
