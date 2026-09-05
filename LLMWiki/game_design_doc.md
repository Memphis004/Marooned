# [ชื่อเกมชั่วคราว] "Marooned" — Card Survival x Social Deduction
สรุปการออกแบบเกม + สถาปัตยกรรม (อ้างอิง tech stack จากโปรเจก Cultivation Together)

## 1. แนวคิดเกม

เกมเอาชีวิตรอดแบบ **single-player 2D sandbox + card game** ที่ยืมโครงหลักจาก
*Card Survival: Tropical Island* และ *Cultivation Realm: Card Survival* (สำรวจ →
เก็บการ์ดทรัพยากร → คราฟ → บริหารสเตตัส) แล้วเติมชั้น **social deduction**
แบบ Among Us / มาเฟีย-หมาป่าเข้าไป: ผู้เล่นไม่ได้ติดเกาะคนเดียว แต่มี NPC
ติดอยู่ในสถานที่เดียวกัน และหนึ่งใน NPC เหล่านั้น (หรือมากกว่า) คือ "ฆาตกร"
ที่แอบกำจัด NPC ตัวอื่นทีละคน ผู้เล่นต้องเอาชีวิตรอดจากสิ่งแวดล้อม *และ*
ไล่หาตัวฆาตกรให้เจอ ก่อนที่ตัวเองจะกลายเป็นเป้าหมายถัดไป

**เสาหลักของเกม (3 pillars)**
1. **Survival Resource Loop** — หิว/กระหาย/อารมณ์/เหนื่อยล้า, สำรวจ, คราฟ, เจ็บป่วย/บาดเจ็บเป็นการ์ดติดตัว
2. **Social Deduction Loop** — สังเกตพฤติกรรม NPC, เก็บการ์ดเบาะแส, กล่าวหา/โหวต, หลีกเลี่ยงการตกเป็นเหยื่อ
3. **AI-Playable Layer** — ทุกกลไกต้อง query/ตัดสินใจได้ผ่าน MCP เพื่อให้ AI VTuber เล่นแทนสตรีมเมอร์ได้ (สืบทอดหลักคิดจากโปรเจกเดิม)

---

## 2. Core Gameplay Loop

```
สำรวจพื้นที่ (การ์ด Location) → เจอการ์ดทรัพยากร/เหตุการณ์/NPC
        ↓
เก็บ/คราฟการ์ด → จัดการ Stat (Hunger/Thirst/Mood/Fatigue)
        ↓
พบเบาะแส (Clue Card) หรือเหตุการณ์ฆาตกรรม → สืบสวน
        ↓
Day/Night Cycle จบวัน → ที่ประชุม (Meeting Phase) → กล่าวหา/โหวต หรือเงียบไว้ก่อน
        ↓
วนกลับสำรวจต่อ จนกว่า (ก) หาฆาตกรเจอครบ (ข) ผู้เล่นตาย/ถูกฆ่า (ค) รอดจนครบเงื่อนไขเอาชีวิตรอด
```

### 2.1 Stat Management (เหมือนโปรเจกอ้างอิง)
- `Hunger`, `Thirst`, `Mood`, `Fatigue` — ลดลงตามเวลา/การกระทำ, เติมด้วยการ์ดไอเท็ม
- Stat ตกต่ำ → เพิ่มโอกาสสุ่ม "Illness Card" (เจ็บป่วย) ติดตัวผู้เล่น เช่น ไข้, ท้องเสีย, นอนไม่หลับ
- Illness/Injury Card มีผล debuff ต่อ action (เช่น สำรวจช้าลง, คราฟพลาดง่ายขึ้น) และต้องใช้การ์ดยา/พักผ่อนเพื่อรักษา

### 2.2 Exploration & Crafting
- แผนที่แบ่งเป็น Location Node (2D sandbox, เดินไปมาได้) แต่ละ node มี Loot Table (Luban table) กำหนดว่าเจออะไรได้บ้าง + weight
- Crafting ใช้ Recipe Table (Luban) : input card(s) → output card, มีเงื่อนไข tool/สถานที่/skill
- ทรัพยากรจำกัด (finite nodes ค่อยๆ หมด) เพื่อบังคับให้ต้องแย่งชิงกับ NPC หรือย้ายโซน

### 2.3 NPC & Social Deduction
- NPC มี Role: `Innocent`, `Killer`, อาจมี `Neutral` (เอาตัวรอดอย่างเดียว ไม่ช่วยใคร, เปิดใช้ทีหลังได้)
- **สัดส่วน Killer:Innocent = ยึดตาม Among Us (~1:4 ถึง 1:8)** เช่น NPC 5 ตัว → 1 killer, NPC 10 ตัว → 2 killer — คำนวณจาก `killerCount = clamp(round(npcCount / 6), 1, npcCount/4)` เป็นค่าเริ่มต้น ปรับ balance ได้ภายหลังจาก playtest
- NPC แต่ละตัวมี **Schedule/Behavior State** (กำลังหาน้ำ, นอนพัก, สำรวจโซน X) ที่ผู้เล่น query ได้บางส่วน (ไม่ full information — ต้องสังเกต/ตามดูจริง คล้าย Among Us "vent/task" pattern)
- Killer NPC มี hidden action `Sabotage`/`Eliminate` ที่ทำได้เมื่อ (ก) อยู่กับเหยื่อสองต่อสองไม่มีพยาน (ข) ผ่าน cooldown
- เมื่อมีการฆาตกรรม → เกิด **Clue Card** สุ่มติดที่ศพ/NPC ใกล้เคียง เช่น "คราบเลือด", "รอยขีดข่วน", "รอยเท้าเปื้อนโคลน" — การ์ดเหล่านี้เป็นข้อมูลให้ผู้เล่น (และ AI) ใช้ในการนิรนัยหาตัวคนร้าย
- Meeting Phase: ผู้เล่นเลือก "กล่าวหา" NPC หรือ "งดออกเสียง" — ถ้ากล่าวหาผิด เสีย trust/mood ทั้งกลุ่ม, ถ้าปล่อยฆาตกรไว้นานเกินไป จำนวน NPC ที่รอดลดลงเรื่อยๆ

### 2.4 Win / Lose Conditions
- **Win:** จับฆาตกรได้ครบ (โหวตถูกตัว) หรือเอาชีวิตรอดจนครบจำนวนวันที่กำหนด (survival timer) โดยไม่ตาย
- **Lose:** ผู้เล่นตายจาก stat ติดลบ/เจ็บป่วยรุนแรง/ถูกฆาตกรรม, หรือกล่าวหาผิดจนหมด "trust token" (โหวตผิดครบจำนวนครั้ง)

---

## 3. Event System

สืบทอด `WorldEventSystem` เดิม (weighted random pool ผ่าน Luban) แต่แยก event
เป็น 2 กลุ่มเพื่อไม่ให้ปนกัน:

| กลุ่ม Event | ตัวอย่าง | Trigger |
| --- | --- | --- |
| **Survival Event** | พายุเข้า (เพิ่ม fatigue drain), พบสัตว์ป่า, อาหารเป็นพิษ | Tick-based random, ขึ้นกับ location/เวลา |
| **Social Event** | NPC ทะเลาะกัน (เผย relationship), พบศพ, NPC ขอความช่วยเหลือ (อาจเป็นกับดัก), ขอแลกไอเท็ม | ขึ้นกับ NPC state machine + role |

Event ทั้งสองกลุ่มต้อง expose ผ่าน MCP เป็น queryable event queue เดียวกัน
(เหมือน `await_next_world_event` เดิม) เพื่อให้ AI Agent เห็นภาพรวมและตัดสินใจ
ตอบสนองได้โดยไม่ต้อง "สัญชาตญาณมนุษย์"

---

## 4. Tech Stack — ใช้ต่อจาก Cultivation Together

ตามที่ระบุไว้ ให้ scope เฉพาะส่วนที่จำเป็นให้ AI VTuber เล่นได้ ไม่ทำต่อถึง
Twitch extension/multiplayer voting

| ส่วน | ใช้ต่อจากโปรเจกเดิม | ปรับสำหรับเกมใหม่ |
| --- | --- | --- |
| Game engine | Unity (C#) | เปลี่ยนจาก UI แบบ sect-management เป็น 2D sandbox + card UI |
| DI | VContainer | เหมือนเดิม, register subsystem ใหม่ (SurvivalSystem, DeductionSystem, CardSystem) |
| Messaging | MessagePipe (in-process) | เหมือนเดิม |
| IPC | MessagePipe.Interprocess (TCP) | เหมือนเดิม, Unity host / Bridge client |
| MCP Bridge | .NET 8 Console (`ModelContextProtocol` SDK) | เพิ่ม tool set ใหม่ (ดูหัวข้อ 6) แทนที่ sect tools เดิม |
| Serialization | MessagePack | เหมือนเดิม |
| DataTable | **Luban** | ตารางใหม่: `CardDef`, `LocationDef`, `RecipeDef`, `NpcRoleDef`, `ClueDef`, `IllnessDef`, `WorldEventDef` |
| UI Framework | **Xianxia.UI.MVP Lite** (เปลี่ยนชื่อ namespace ได้ตามต้องการ) | View/Presenter/Service pattern เดิม, เพิ่ม panel ใหม่: `CardHandView`, `MapExploreView`, `MeetingVoteView`, `ClueBoardView` |
| Avatar | **เปลี่ยนใหม่ทั้งหมด** — ดูหัวข้อ 4.1 | ไม่ใช้ portrait-swap เดิมของโปรเจกอ้างอิง เปลี่ยนเป็น chibi sprite-swap paperdoll ที่เดิน/ขยับแขนขาได้จริงบน 2D sandbox |
| Additive Scene | (ตามแผนเดิมที่ยังเป็น draft) | เหมาะมากสำหรับเกมนี้: `CoreScene` (HUD/inventory persistent) + `GameplayScene` (island/location ที่กำลังสำรวจ) |

**สิ่งที่ตัดออกจาก scope เดิมโดยตั้งใจ** (ตามที่ระบุ): ไม่ทำ Twitch
extension, ไม่ทำระบบโหวตผู้ชม, ไม่ทำ multiplayer — ทุกอย่างจบที่ single
player + MCP ให้ AI VTuber เรียกเล่นได้เท่านั้น

### 4.1 Avatar System — Chibi Sprite-Swap Paperdoll (เปลี่ยนจากโปรเจกอ้างอิง)

โปรเจกอ้างอิงทำ `AvatarAppearance` เป็น **portrait-swap** เท่านั้น (โชว์หน้าตา/
ครึ่งตัวนิ่งๆ ในกล่องสนทนา) ซึ่งไม่พอสำหรับเกมนี้ที่ตัวละครต้องเดินไปมาบน
แผนที่ 2D sandbox จริง จึงออกแบบใหม่เป็น **ChibiAppearance**:

- ยังคง **หลักการ Dictionary-based เดิม** (`Parts: slot→partId`, เพิ่ม slot ใหม่แก้แค่ JSON) แต่แต่ละ slot ตอนนี้คือ**แขนขาแยกชิ้น** ไม่ใช่ภาพนิ่งภาพเดียว: `body, head, hair, arm_left, arm_right, leg_left, leg_right, accessory, overlay`
- แต่ละ part (`ChibiPartDef`) มี **sprite sheet ต่อทิศทาง+อนิเมชัน** (`FramesByAnimKey`: เช่น `"Down_Walk" → [เฟรม1, เฟรม2, เฟรม3, เฟรม4]`) แล้ว renderer สลับเฟรมตามจังหวะเดิน — เป็น **sprite-swap แบบคลาสสิก** (เหมือนเกม RPG top-down ทั่วไป) ไม่ใช่ skeletal/bone rig จึงทำง่ายกว่าและ art วาดแยกชิ้นได้ตรงไปตรงมา
- Layer stack ยึด draw order เดิมแต่ขยายรองรับแขนขา: `leg_back(0) → body(10) → leg_front(15) → arm_back(20) → head(30) → hair_back(35) → face_marking(38) → hair_front(40) → arm_front(45) → overlay/condition(50) → accessory(60)`
- **Condition overlay แยก dictionary ต่างหาก** (`ConditionOverlays`) จาก outfit `Parts` เพื่อให้คราบเลือด/ผ้าพันแผล/รอยขีดข่วน ติด-หลุดได้อิสระโดยไม่ต้องแก้ outfit ทั้งชุด
- `ChibiOutfitDef` แทน `OutfitDef` เดิม: เปลี่ยนทั้งชุด (ทุก slot) พร้อมกันแบบ atomic เหมือนเดิม แต่ต้องเช็คว่าทุก part มี `FramesByAnimKey` ที่ตรงกันครบ (เดินได้ทุกทิศ) ก่อน apply
- ผู้เล่นและ NPC ทุกตัวใช้ระบบเดียวกันหมด (NPC ก็เดินได้/มีคราบเลือดติดตัวเป็น clue ได้เหมือนที่ออกแบบไว้ในหัวข้อ 2.3)

---

## 5. Data Model (ร่าง, สไตล์เดียวกับ Shared/ เดิม)

```csharp
// Shared/CardDef.cs (Luban-generated, plain class)
public class CardDef
{
    public string Id;
    public CardCategory Category; // Resource, Consumable, Tool, Illness, Clue, Craftable
    public string DisplayName;
    public string SpritePath;
    public int StackLimit;
    public Dictionary<string, float> StatEffect; // e.g. {"Hunger": +20}
}

// Shared/PlayerSurvivalState.cs
[MessagePackObject]
public class PlayerSurvivalState
{
    [Key(0)] public float Hunger;
    [Key(1)] public float Thirst;
    [Key(2)] public float Mood;
    [Key(3)] public float Fatigue;
    [Key(4)] public List<string> ActiveConditionCardIds; // illness/injury ติดตัว
    [Key(5)] public Dictionary<string, int> Inventory;   // cardId -> count
}

// Shared/NpcState.cs
[MessagePackObject]
public class NpcState
{
    [Key(0)] public string Id;
    [Key(1)] public NpcRole Role;              // Innocent / Killer / Neutral
    [Key(2)] public bool IsAlive;
    [Key(3)] public string CurrentLocationId;
    [Key(4)] public List<string> VisibleConditionCardIds; // บาดแผล/เบาะแสที่ "เห็นได้" เท่านั้น
    [Key(5)] public ChibiAppearance Avatar;    // ดูหัวข้อ 4.1 — chibi sprite-swap, ไม่ใช่ portrait เดิม
}

// Shared/ClueDef.cs
public class ClueDef
{
    public string Id;
    public string DisplayName;       // "คราบเลือด", "รอยขีดข่วน"
    public ClueReliability Reliability; // Strong / Weak / RedHerring
    public string LinkedNpcIdHint;   // ใช้ฝั่ง server logic เท่านั้น ไม่ส่งตรงให้ client/AI
}
```

**หลักการสำคัญ:** `LinkedNpcIdHint` และ ground-truth ของ `NpcRole.Killer`
ต้อง **ไม่** หลุดออกไปใน MCP response ใดๆ ที่ AI query ได้ตรงๆ (ไม่งั้น AI
จะโกงรู้ตัวคนร้ายทันที) — ต้องมี query layer แยกระหว่าง "GM/debug view"
(เห็นหมด) กับ "Player/Agent view" (เห็นเฉพาะสิ่งที่สังเกตได้จริงในเกม)
เหมือนกับ Among Us ที่ผู้เล่นไม่เห็น role คนอื่น

---

## 6. MCP Tool Design (สำหรับให้ AI VTuber เล่น)

| Tool | ประเภท | หน้าที่ |
| --- | --- | --- |
| `get_game_state` | Query | คืน player stat, inventory, location, เวลาปัจจุบัน, NPC ที่มองเห็นได้ในโซนเดียวกัน (พร้อม visible condition cards เท่านั้น) |
| `get_hand` / `get_inventory` | Query | รายการการ์ดที่ถืออยู่ |
| `explore_location` | Action (Request-Response) | เดินสำรวจ node → คืนการ์ดที่พบ/เหตุการณ์ที่เจอ |
| `craft_card` | Action | ใช้ recipe คราฟไอเท็ม, คืนผลสำเร็จ/ล้มเหลว |
| `use_card` | Action | กินอาหาร/ดื่มน้ำ/ใช้ยา ฯลฯ |
| `move_to_location` | Action | ย้ายโซน |
| `talk_to_npc` / `observe_npc` | Action/Query | สังเกตพฤติกรรม NPC เพื่อเก็บข้อมูลเชิงสังคม |
| `await_next_event` | Request-Response (คงแบบเดิมจาก Lab 6) | รอ event ถัดไป (survival หรือ social) แบบไม่ block TCP |
| `report_body` / `call_meeting` | Action | เริ่ม Meeting Phase หลังพบศพ/สงสัย |
| `accuse_npc` | Action | โหวตกล่าวหาใน Meeting Phase |
| `get_clue_board` | Query | รวมเบาะแสทั้งหมดที่ "เก็บสะสมมาแล้ว" ให้ AI ใช้นิรนัย |

ทุก tool ต้องออกแบบให้ตอบด้วยข้อมูลที่ **AI ตัดสินใจได้จากสิ่งที่ query ได้จริง**
(หลักคิดกลางเดิมของโปรเจก) — ห้าม leak ground truth เช่น NPC role ที่แท้จริง
ผ่าน tool ไหนเลยนอกจาก debug/GM-only tool ที่แยก namespace ชัดเจน

---

## 7. UI.MVP Lite — Panel ใหม่ที่ต้องเพิ่ม

ใช้ pattern เดิม (View = MonoBehaviour, Presenter = Plain C#, Direct Method
Call สำหรับ in-process logic ตาม lesson จาก Lab 13):

- `CardHandView/Presenter` — แสดงมือการ์ด, drag เพื่อ craft/ใช้
- `MapExploreView/Presenter` — 2D sandbox map, node ที่สำรวจได้/หมดแล้ว
- `ClueBoardView/Presenter` — กระดานปะติดปะต่อเบาะแส (คล้าย detective board)
- `MeetingVoteView/Presenter` — หน้าประชุม/โหวตกล่าวหา
- `ConditionOverlayView` — แสดง illness/injury card เป็น overlay บน portrait (ใช้ `AvatarRenderer` ตัวเดิม, เพิ่ม layer สำหรับคราบเลือด/ผ้าพันแผล)

---

## 8. Roadmap ที่แนะนำ (ต่อยอดจากรูปแบบ "Lab Round" เดิม)

1. **Lab A — Survival Core:** Stat system, Card/Inventory data model, Luban table สำหรับ CardDef/LocationDef/RecipeDef, MCP tool อ่าน/คราฟพื้นฐาน
2. **Lab B — Illness/Injury Layer:** ConditionCard system + ChibiAppearance overlay integration + ChibiAnimatedRenderer เดิน 4 ทิศพร้อม overlay
3. **Lab C — NPC Skeleton:** NpcState, schedule/behavior stub, visibility rule (สิ่งที่ query เห็น vs ไม่เห็น)
4. **Lab D — Social Deduction Core:** Killer sabotage logic, Clue generation, Meeting/Vote flow
5. **Lab E — MCP Full Tool Set + AI VTuber Playtest:** ต่อ Bridge ให้ AI เล่นครบ loop ได้จริงตั้งแต่สำรวจจนโหวตจับฆาตกร
6. **Lab F (ถ้ามีเวลา) — Additive Scene migration** ตามแผน CoreScene/GameplayScene ที่ค้างไว้ในโปรเจกเดิม

---

## 9. คำถามเปิด (Open Questions)

- ~~จำนวน NPC ต่อรอบ และสัดส่วน Killer:Innocent~~ → **ตัดสินใจแล้ว: 1:4 ถึง 1:8 ตาม Among Us** (ดูหัวข้อ 2.3)
- ควรมี "Neutral role" (เอาตัวรอดอย่างเดียว ไม่ใช่ทั้งฝ่ายดี/ร้าย) หรือไม่ — เพิ่มความซับซ้อนแต่เพิ่ม replayability
- Killer NPC ควรมี "hidden agenda card" ของตัวเอง (เช่นต้องฆ่าให้ครบ N คนก่อนวันที่ X) เพื่อให้ AI มี objective ที่ query ได้เหมือนฝั่งผู้เล่นหรือไม่ — ต้องเผื่อไว้ถ้าอนาคตอยากให้ AI สลับมาเล่นเป็นฆาตกรได้ด้วย
- ระดับ information ที่ Meeting Phase เปิดเผย (ใครอยู่กับใครตอนไหน) ควรมาจาก log ที่ผู้เล่นสังเกตเองระหว่างเกม หรือมีระบบ "alibi" กลาง ๆ ให้ query
