---
title: npc-director
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs
  - Marooned/Assets/Scripts/Shared/NpcState.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[NpcDirectorSystem.cs]]"
  - "[[DeductionSystem.cs]]"
  - "[[ExplorationSystem.cs]]"
  - "[[WorldEventSystem.cs]]"
  - "[[chibi-avatar]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - npc
  - marooned
  - lab-a
---

# NPC Director

## ภาพรวม (Gameplay Perspective)
ผู้เล่นไม่ได้ติดเกาะคนเดียว — มี NPC หลายคนที่มีชีวิตประจำวัน (หาของ, พัก, เดินทาง) และ
**หนึ่งในนั้นคือ Killer** ที่จะฆ่า NPC ตัวอื่นเมื่ออยู่ตัวต่อตัวโดยไม่มีพยาน ผู้เล่นสังเกต
พฤติกรรม NPC ได้เฉพาะสิ่งที่มองเห็นได้จริง (activity ตอนอยู่โซนเดียวกัน, บาดแผลที่เห็นภายนอก)
แต่ไม่มีทางรู้ role จริง — สัดส่วน Killer ยึด Among Us ~1:4 ถึง 1:8

## การ Implement (Developer Perspective)
- **Class หลัก:** `NpcDirectorSystem`
  (`Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs`) — **เจ้าของ ground truth ทั้งหมด**
- **State:** `NpcState` (`Marooned/Assets/Scripts/Shared/NpcState.cs`) — Role, IsAlive,
  CurrentLocationId, Activity, AllConditionCardIds, KillCooldownRemaining, HiddenAgendaId —
  **ห้าม serialize ออกนอก Unity ตรง ๆ** ต้องแปลงผ่าน [[DeductionSystem.cs]] เป็น
  `NpcObservableView` เสมอ
- **Public API สำคัญ:**
  - `IReadOnlyDictionary<string, NpcState> Npcs` — internal use เท่านั้น
  - `void SetupRound(IEnumerable<string> npcIds, int killerCount)` — สุ่มเลือก killer
  - `void Tick(float deltaSeconds)` — behavior + killer พยายามฆ่า
- **Flow การฆ่า (`TryAttemptElimination`):** ลด KillCooldown → หาผู้มีชีวิตใน location เดียวกัน
  ต้องเหลือ **ตัวเดียวพอดี** (กฎ "no witness") → สุ่ม 15% ต่อ tick → ฆ่าสำเร็จ: เหยื่อตาย,
  cooldown 180s, spawn clue, publish `NpcEliminatedMessage`
- Ratio guideline (GDD §2.3): `killerCount = clamp(round(npcCount / 6), 1, npcCount/4)`

## Data Tables
- `DataTables/Data/LocationDef.csv` — Capacity ของแต่ละ node (เกี่ยวกับการเช็คพยาน)
- ❌ **ยังไม่มีตาราง NPC schedule** — `TickBehavior` เป็น placeholder ว่าง รอ daily-schedule
  table จาก Luban (GDD §2.3 วางแผน Schedule/Behavior State)
- ❌ ยังไม่มี NpcRoleDef table (GDD §4 ระบุไว้ในรายการตาราง)

## ความเชื่อมโยงกับระบบอื่น
- [[DeductionSystem.cs]] — ผู้เดียวที่อ่าน NpcState ได้ แล้วสร้าง safe view
- [[ExplorationSystem.cs]] — ใช้ LocationDef เดียวกัน, NPC แย่ง loot กับผู้เล่น
- [[WorldEventSystem.cs]] — Social event (ทะเลาะกัน, พบศพ) ควร trigger จาก NPC state
- [[chibi-avatar]] — NPC ทุกตัวใช้ ChibiAppearance + ConditionOverlays เหมือนผู้เล่น

## สถานะปัจจุบัน
- ✅ SetupRound + กฎ no-witness + kill cooldown + spawn clue v1 เสร็จแล้ว
- ❌ **`TickBehavior` ว่างเปล่า** — NPC ไม่ขยับ ไม่เปลี่ยน activity จริง
- ❌ **ไม่มีใครเรียก `SetupRound`/`Tick`** — ยังไม่มี game loop และ flow เริ่มรอบ
- ❌ SpawnClues hardcode id — รอ ClueDef-driven roll; ยังไม่มี red herring
- ❌ killerCount ยังไม่มี formula auto-calc
