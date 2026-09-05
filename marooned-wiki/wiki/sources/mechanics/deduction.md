---
title: deduction
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/DeductionSystem.cs
  - Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs
  - Marooned/Assets/Scripts/Shared/NpcState.cs
  - Marooned/Assets/Scripts/Shared/ClueDef.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[DeductionSystem.cs]]"
  - "[[npc-director]]"
  - "[[MeetingVoteView.cs]]"
  - "[[ClueBoardView.cs]]"
  - "[[Information-Hiding]]"
  - "[[overview]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - deduction
  - marooned
  - lab-a
---

# Social Deduction

## ภาพรวม (Gameplay Perspective)
เมื่อเกิดฆาตกรรม ผู้เล่นจะพบเบาะแส (Clue Card) เช่น คราบเลือด รอยขีดข่วน — บางอันเชื่อถือได้
(Strong) บางอันเป็นกับดัก (RedHerring) ผู้เล่นสะสมเบาะแสไว้บน Clue Board แล้วเรียกประชุม
เพื่อ **กล่าวหา (Accuse)** NPC ที่สงสัย — โหวตถูกตัวครบทุก Killer = ชนะ แต่โหวตผิด 3 ครั้ง
= แพ้ทันที (และ Mood ถูกหักทุกครั้ง) หลักการสำคัญ: ผู้เล่น/AI ไม่มีทางรู้ role จริงนอกจาก
อนุมานจากสิ่งที่สังเกตได้ เหมือน Among Us

## การ Implement (Developer Perspective)
- **Class หลัก:** `DeductionSystem` (`Marooned/Assets/Scripts/Systems/DeductionSystem.cs`) —
  **ชั้นเดียวที่แปลง ground truth → safe view** ([[Information-Hiding]])
- **Public API สำคัญ:**
  - `List<NpcObservableView> GetObservableNpcsAt(string locationId)` — NPC ที่มองเห็นได้ใน
    location เดียวกัน (ไม่มี Role/Cooldown หลุดออก; กรอง condition ด้วย `IllnessDef.Visible`)
  - `(bool wasCorrect, bool win, bool loss, string resultText) Accuse(string targetNpcId)` —
    ถูก: killer ออกจากเกม (หมดทุกตัว = win); ผิด: `WrongAccusations++`, Mood −15,
    ครบ `MaxWrongAccusations = 3` = loss
- **Flow การเก็บเบาะแส:** `NpcDirectorSystem.SpawnClues` ใส่ clue id ใน
  `victim.AllConditionCardIds` → ตัวกรอง visibility กำหนดว่าเห็นได้จากภายนอกหรือต้อง
  investigate → (❌ ยังไม่มีกลไกเก็บเข้า `CollectedClueCardIds`)
- **MCP tools:** `get_visible_npcs` / `get_clue_board` / `call_meeting` / `accuse_npc`
  (handler ทั้งหมดผ่าน DeductionSystem เท่านั้น — ground truth ไม่ข้ามเส้น by construction)
- **Meeting Phase:** `CallMeetingHandler` ยังแค่ snapshot คนที่มองเห็น — ยังไม่มี pause/summon
  จริง (GDD §2.3)

## Data Tables
- `DataTables/Data/ClueDef.csv` — เบาะแส 4 ชนิด: `clue_blood_stain` (Strong),
  `clue_scratch_mark` (Weak), `clue_footprint_mud` (Weak, ต้อง investigate),
  `clue_torn_cloth` (RedHerring)
- `DataTables/Data/IllnessDef.csv` — field `visible` ใช้กรองว่าผู้สังเกตเห็นได้หรือไม่
- ⚠️ ทั้งหมดยังโหลดผ่าน mock ใน [[LubanDataService.cs]]

## ความเชื่อมโยงกับระบบอื่น
- [[npc-director]] — แหล่ง ground truth และเหตุการณ์ฆาตกรรม
- [[MeetingVoteView.cs]] / `MeetingVotePresenter` — UI โหวตกล่าวหา
- [[ClueBoardView.cs]] / `ClueBoardPresenter` — UI กระดานเบาะแส
- [[survival-stats]] — โหวตผิดหัก Mood
- GDD §9 (Open Questions) — Neutral role และระบบ alibi ยังไม่ตัดสินใจ

## สถานะปัจจุบัน
- ✅ Accuse (win/loss) + observable view filtering + โทษโหวตผิด เสร็จแล้ว
- ❌ **`CollectedClueCardIds` ไม่เคยถูกเติม** — ไม่มีกลไกเก็บ clue → `get_clue_board` ว่างเสมอ
- ❌ ไม่มี Meeting Phase state machine — accuse ได้ทุกเมื่อ
- ❌ ไม่มี `investigate_clue` สำหรับเบาะแส `VisibleToBystanders = false`
- ❌ `MaxWrongAccusations = 3` hardcode
