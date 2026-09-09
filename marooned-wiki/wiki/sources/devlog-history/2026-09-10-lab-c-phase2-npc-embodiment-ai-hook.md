# LabC-Phase2: NPC Embodiment (Data Foundation + Movement + Visual Sync + Basic AI Hook)

**Date:** 2026-09-10

## Overview
- NPCs เป็น "ตัวจริง" ใน walking sandbox: มีตำแหน่งบนโลก, Inventory, สถิติภายใน (Hunger/Fear/Curiosity) เป็น Ground Truth
- NPC เดิน wander ในโซน + ข้ามโซนผ่าน `MoveNpc()` เท่านั้น (Single Source of Truth) + Position Seeding Rule กัน warp ไป (0,0)
- Chibi บนจอเดินตาม `NpcState.Position` จริง (per-frame direct read, ทั้ง GenericCute และ Spine backend)
- แทนการฆ่าสุ่ม 15%/tick ด้วยสมองแยกตาม role: `InnocentUtilityAI` (utility scoring) + `KillerPlanner` (state machine 5 phase)
- Runtime tests A–F ผ่านครบใน Play Mode จริง (รวม no-witness rule + no-leak check บน MCP response)

## Details

### Step 1 — Data Foundation (Shared)
- `NpcState` เพิ่มฟิลด์ Key(9)–(15): `PositionX/Y`, `TargetX/Y`, `MovementSpeed=2.0`, `NpcInventory Inventory`, `NpcSurvivalState Survival` (Key 0–8 เดิมไม่ชน)
- `NpcInventory.cs` ใหม่ (Dictionary + HasItem/AddItem/RemoveItem/GetCardIds) — `[MessagePackObject]` ใส่เพื่อผ่าน analyzer (MsgPack003) ไม่ใช่การอนุญาตให้ expose
- `NpcSurvivalState` (Hunger/Fear/Curiosity 0–100) — ground truth สำหรับ AI ห้ามหลุดออกนอกระบบ
- **Position Seeding Rule**: `MoveNpc()` ต้อง set `Position = LocationDef.WorldX/Y` ของปลายทางทุกครั้ง (และ seed `Target = Position` ด้วย — กันเป้าเก่าจากโซนเดิมลาก NPC ข้ามแผนที่)
- `SetupRound()` แจกอาวุธแรกที่ `Category==Weapon` จาก Luban จริง (`knife_basic` มีอยู่แล้วใน CardDef.csv + generated JSON — ไม่ต้อง regen)

### Step 2 — Movement
- `NpcMovementSystem` (plain C# singleton): ตอนแรกทำ wander+ข้ามโซน 20% เอง (PickNextTarget แยก method ไว้ปิดง่าย)
- Tick order ใน `GameTickDriver`: `NpcDirectorSystem.Tick()` (AI ตั้ง target) **ก่อน** `NpcMovementSystem.Tick()` (เดิน) ในเฟรมเดียวกัน — ไม่ดีเลย์ 1 เฟรม

### Step 3 — Visual Sync
- `NpcCharacterView` (MonoBehaviour 1 ตัวต่อ chibi): `Init(npcId, npcDirector)` จาก `ChibiSpawnerView.SpawnChibi` แบบ direct call → `Update()` อ่าน NpcState ตรง: position/flip จากเครื่องหมาย dx/`IChibiVisual.Bind(Activity)`
- ตำแหน่งเฟรมแรก set จาก PositionX/Y ทันที (ไม่โผล่ origin) — ถอด offset ไล่ตัวเดิม
- `Traveling → walk` ยืนยันแล้วทั้ง GenericCute ("walk") และ Spine ("Walking", flip ผ่าน `Skeleton.ScaleX`)
- ทดสอบจริง: syncErr=(0.00,0.00) ทุก snapshot, ข้ามโซน spawn/despawn บาลานซ์ผ่าน `NpcLocationChangedMessage`

### Step 4 — Basic AI Hook
- **4.1 Deprecate re-target-on-arrival**: `NpcMovementSystem` เหลือหน้าที่เดินเข้าหา Target ที่มีอยู่ + ตั้ง Activity (Traveling/Idle) — การเลือก target ทั้งหมดย้ายไป AI (กันสองระบบแย่ง `TargetX/Y`)
- **4.2** namespace `Marooned.Systems.AI`:
  - `IUtilityAction { Id, Score(pure), Execute }` + `UtilityContext { Data, NpcDirector, StateProvider }`
  - `Wander` (static helper): ตั้ง target ในโซน / ข้ามโซนผ่าน `MoveNpc()` / หาโซนจาก loot category — caller เช็ค `HasPendingTarget` ก่อนตั้งเป้าใหม่ (กันแย่ง)
  - `InnocentUtilityAI`: re-evaluate ทุก ~1.5 วิต่อตัว → execute ทุก tick (IdleWander baseline, SeekFood = Hunger>55, InvestigateNoise/FleeToSafeZone stub score 0)
  - `KillerPlanner`: Patrolling → SeekingWeapon → SeekingOpportunity → Executing → BuildingAlibi; cooldown นับถอยใน planner ทุก tick; ฆ่าผ่าน `TryEliminate/CanEliminate` เท่านั้น (HasWeapon เป็นเงื่อนไขฝั่ง caller ตาม design decision — economy-agnostic, อาวุธไม่หักตอนฆ่า)
  - **DI cycle ปลอดภัย**: `UtilityContext` ไม่ resolve NpcDirector ตอน build — `NpcDirectorSystem` **Bind(this)** ใน constructor ของตัวเอง (จุดเดียว), AI อ่าน `ctx.NpcDirector` ตอน Tick เท่านั้น
- **4.3 Cleanup**: `TryAttemptElimination` + call site ถูกลบ (grep ยืนยัน), cooldown semantics ย้ายเข้า planner ครบ

### Step 4.4 — Runtime Tests (Play Mode จริง, probe แบบ freeze+pin window)
| Test | ผล | หลักฐาน |
|---|---|---|
| A: innocent decision | ✅ | hunger=90 → `chose SeekFood (score=0.90)` → กินของ Hunger ลดจริง |
| B: kill flow | ✅ | เหยื่อ alive=False + `clue_blood_stain`, chibi despawn, cooldown รีเซ็ต ~180, phase → BuildingAlibi |
| C: witness rule | ✅ | พยาน (pinned) อยู่ในโซน → `abort (CanEliminate: witnessed)` — ไม่มีใครตาย |
| D: player knife | ✅ | ผ่าน UseCardHandler flow เดียวกัน: มีพยาน → `FailureReason=witnessed` (การ์ดไม่หาย), ไม่มีพยาน → `eliminated_npc_01` |
| E: movement smooth | ✅ | ~MovementSpeed 2.0, ไม่มี teleport/แย่ง target หลัง deprecate |
| F: no leak | ✅ | get_game_state/get_visible_npcs ไม่มี phase/cooldown/inventory/Position แม้แต่ field เดียว |

### Bugs ที่เจอตอนเทส (แก้แล้ว)
1. **BuildingAlibi teleport storm** — เลือกทางหนีใหม่ทุกเฟรม (`MoveNpc` seed Target=Position → `HasPendingTarget` false ทันที) = ย้ายโซนรัวๆ จน chibi spawn/despawn เป็นพายุ → **แก้: เลือกทางหนีครั้งเดียวตอน Transition เข้า phase**
2. **Witnessed-abort log spam** — abort แล้วหาเหยื่อใหม่ทันทีทุกเฟรม (2 บรรทัด/เฟรม) → **แก้: เพิ่ม `OpportunityRetrySeconds` (2 วิ) พักก่อนหาโอกาสใหม่**
3. **บทเรียนเทส**: zone move = instant พยานที่ปล่อยเดินอิสระหลุดโซนฆ่าในไม่กี่วิ (kill จริงกลายเป็น no-witness ถูกกฎ) — forced scenario ต้อง **pin target พยานในโซน** ก่อนเปิดกรอบเวลา
4. **Gotcha**: compile สคริปต์ใหม่ระหว่าง Play Mode = domain reload กลางเกม เด้งออกนอก Play + เช็ต container พัง — แก้/เพิ่มสคริปต์ต้องทำก่อนเข้า Play เสมอ

## Next Steps
- เปิดใช้ stub actions: `FleeToSafeZoneAction` (Fear จากการเห็นศพ) + `InvestigateNoiseAction` (Curiosity)
- NPC Survival stats ยังไม่มีใคร tick ค่าเอง (Hunger ไม่เพิ่มตามเวลา — ต้องมี survival tick ฝั่ง NPC)
- Killer durability (อาวุธควรถูกหัก/เสื่อมหลังฆ่า — TODO เดิมของ design decision)
- Visual ยังไม่เช็คสิ่งกีดขวาง/ขอบเขตโลก (wander clamp ที่รัศมีโซนพอ)
- Scene: `npcPrefabs[0]/[1]` โดนแก้ภายนอกชี้ fallback prefab เดียวกันหมด (NPC หน้าตาเหมือนกันหมด) — ตั้งค่าใหม่ใน Editor ได้
