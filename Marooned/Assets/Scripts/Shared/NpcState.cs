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

        // ---- Lab C Phase 2 (NPC Embodiment): walking sandbox ground truth ----
        // ตำแหน่งจริงบนโลก — MoveNpc() ต้อง seed ด้วย LocationDef.WorldX/Y ของ
        // destination เสมอ (Position Seeding Rule — กัน NPC warp ไป (0,0))
        // ขณะเดิน NpcMovementSystem เป็นผู้อัปเดตต่อเนื่อง
        [Key(9)] public float PositionX;
        [Key(10)] public float PositionY;

        /// <summary>จุดหมายปลายทางที่กำลังเดินไป (ตั้งโดย AI/movement — อ่านโดย NpcMovementSystem)</summary>
        [Key(11)] public float TargetX;
        [Key(12)] public float TargetY;

        /// <summary>ความเร็วเดิน (world unit/วินาที) — default 2.0 ช้ากว่าผู้เล่น (3.5)</summary>
        [Key(13)] public float MovementSpeed = 2.0f;

        /// <summary>
        /// Ground truth inventory (เช่น อาวุธของ Killer) — ห้ามหลุดเข้า
        /// NpcObservableView / GetObservableNpcsAt เด็ดขาด
        /// </summary>
        [Key(14)] public NpcInventory Inventory = new();

        /// <summary>
        /// Stats ภายในของ NPC (Hunger/Fear/Curiosity) — ground truth สำหรับ AI
        /// ใน Step 4 (InnocentUtilityAI/KillerPlanner) ห้าม expose ออกนอกระบบ
        /// แยกเป็น class ของตัวเองเพื่อให้ Step 4 เติมเมธอดเชิงพฤติกรรมได้โดยไม่แตะ NpcState
        /// </summary>
        [Key(15)] public NpcSurvivalState Survival = new();

        // ---- Hybrid Transition Points (Part 1 — foundation): ground truth fields
        //      สำหรับการเดินข้ามโซนผ่านจุดเชื่อม (ZoneConnectionDef) — Part 2
        //      (NpcZoneTransitionSystem) จะเริ่มใช้จริง ตอนนี้ยังไม่มีใครเขียน/อ่าน
        //      นอกจาก default เพื่อไม่ให้พฤติกรรม NPC เปลี่ยน
        //      Information Hiding: ทั้งสอง field เป็น ground truth — ห้ามหลุดเข้า
        //      NpcObservableView / GetObservableNpcsAt / MCP response เด็ดขาด
        //      (player เห็นได้แค่ "NPC เดินออกนอกจอ" จาก position/animation เท่านั้น)

        /// <summary>Ground truth — phase ปัจจุบันของการข้ามโซน ห้ามหลุดเข้า NpcObservableView</summary>
        [Key(16)] public NpcTransitionPhase TransitionPhase = NpcTransitionPhase.None;

        /// <summary>Ground truth — โซนปลายทางที่กำลังจะไป (empty ถ้าไม่ได้อยู่ระหว่าง transition) ห้ามหลุดเข้า NpcObservableView</summary>
        [Key(17)] public string PendingTransitionTargetZoneId;
    }

    /// <summary>
    /// Phase ของการข้ามโซนแบบเดินผ่านจุดเชื่อม (Hybrid Transition Points)
    ///  • None           = เดินอิสระปกติภายในโซนเดียว
    ///  • WalkingToPoint = กำลังเดินเข้าหาจุดเชื่อม (transition point) ของโซนต้นทาง
    ///  • Exiting        = ถึงจุดเชื่อมแล้ว กำลังเล่น exit animation (เดินออกนอกจอ)
    ///  • Entering       = ข้ามโซนมาแล้ว กำลังเล่น enter animation (เดินเข้าจากจุดเชื่อมของโซนปลายทาง)
    /// </summary>
    public enum NpcTransitionPhase
    {
        None,
        WalkingToPoint,
        Exiting,
        Entering
    }

    /// <summary>
    /// สถานะอยู่รอดภายในของ NPC — Ground Truth สำหรับ AI (Step 4)
    ///  • Hunger   = ความหิว 0-100 (ยิ่งสูงยิ่งหิวมาก — normalize ง่ายตอน score)
    ///  • Fear     = ความกลัว 0-100 (เห็นศพ/พฤติกรรมน่าสงสัย → เพิ่ม)
    ///  • Curiosity = ความสงสัยใคร่รู้ 0-100 (เจอเบาะแส/เสียงแปลก → เพิ่ม)
    /// Information Hiding: ห้ามโผล่ใน NpcObservableView / GetObservableNpcsAt /
    /// MCP response ใดๆ — มีไว้ให้ระบบ AI ภายในอ่าน/แก้เท่านั้น
    /// </summary>
    [MessagePackObject]
    public class NpcSurvivalState
    {
        [Key(0)] public float Hunger;
        [Key(1)] public float Fear;
        [Key(2)] public float Curiosity;
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
