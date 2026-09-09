using Marooned.Shared;

namespace Marooned.Systems.AI
{
    /// <summary>
    /// Lab C Phase 2 Step 4 — บริบทที่ให้ action ใช้ตัดสินใจ/ลงมือ
    /// สร้างครั้งเดียว (Singleton) ต่อระบบ ไม่ต่อ tick ต่อ action
    ///
    /// หมายเหตุ DI (กัน cycle): NpcDirectorSystem อ้างถึง AI ใน constructor และ AI
    /// อ้างกลับถึง NpcDirectorSystem ผ่าน context นี้ — จึงสร้าง context โดยไม่มี
    /// NpcDirector ก่อน (Data + StateProvider เท่านั้น) แล้ว NpcDirectorSystem
    /// Bind(this) เข้ามาใน constructor ของตัวเอง AI อ่าน NpcDirector ตอน Tick
    /// เท่านั้น (หลัง construct จบแล้วเสมอ) จึงไม่มีจังหวะอ่านค่า null
    /// </summary>
    public class UtilityContext
    {
        /// <summary>ตาราง Luban ทั้งหมด (LocationDefs/CardDefs ฯลฯ)</summary>
        public LubanDataService Data { get; }

        /// <summary>เจ้าของ ground truth NPC + MoveNpc/CanEliminate/TryEliminate</summary>
        public NpcDirectorSystem NpcDirector { get; private set; }

        /// <summary>ผู้เล่น (ไว้หา ตำแหน่ง/location ของ player เมื่อ action ต้องใช้)</summary>
        public GameStateProvider StateProvider { get; }

        public UtilityContext(LubanDataService data, GameStateProvider stateProvider)
        {
            Data = data;
            StateProvider = stateProvider;
        }

        /// <summary>เรียกโดย NpcDirectorSystem constructor เท่านั้น (จุดเดียวของเกม)</summary>
        public void Bind(NpcDirectorSystem npcDirector) => NpcDirector = npcDirector;
    }

    /// <summary>
    /// 1 action ของ utility AI — ให้คะแนนความเหมาะสมเทียบ action อื่น แล้วตัวที่ชนะ
    /// จะถูก Execute ทุก tick จนกว่าจะมีการ re-evaluate ครั้งใหม่
    /// implementation ต้องเป็น stateless ต่อ NPC (state ต่อ NPC อยู่ที่ AI ที่เรียก)
    /// </summary>
    public interface IUtilityAction
    {
        /// <summary>ชื่อเพื่อ log/debug เท่านั้น</summary>
        string Id { get; }

        /// <summary>
        /// คะแนนความเหมาะสม (ยิ่งสูงยิ่งน่าทำ) — ต้อง pure (ไม่ mutate อะไร)
        /// คืนค่า &lt;= 0 = ไม่สนใจ action นี้เลย
        /// </summary>
        float Score(NpcState npc, UtilityContext ctx);

        /// <summary>
        /// ลงมือทำ 1 tick — ถูกเรียกซ้ำทุก tick ตราบใดที่ action นี้ยังชนะการเลือก
        /// (ยกเว้น InnocentUtilityAI ที่ re-evaluate ทุก ~1.5 วิ)
        /// คืนชื่อ log ภายใน (หรือ null) เพื่อเขียน phase/decision log
        /// </summary>
        void Execute(NpcState npc, UtilityContext ctx, float deltaSeconds);
    }
}
