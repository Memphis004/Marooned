---
title: world-events
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/WorldEventSystem.cs
  - Marooned/Assets/Scripts/Shared/ClueDef.cs
  - Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[WorldEventSystem.cs]]"
  - "[[survival-stats]]"
  - "[[npc-director]]"
  - "[[ExplorationSystem.cs]]"
  - "[[overview]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - events
  - marooned
  - lab-a
---

# World Events

## ภาพรวม (Gameplay Perspective)
เหตุการณ์สุ่มเกิดขึ้นบนเกาะแบ่งเป็น 2 กลุ่ม: **Survival Event** (พายุเข้า, เจอสัตว์ป่า —
กระทบ stat หรือทำให้บาดเจ็บ) และ **Social Event** (NPC ทะเลาะกันเผยความสัมพันธ์, NPC ขอ
ความช่วยเหลือที่อาจเป็นกับดัก — ให้ข้อมูลเชิงสังคม) ผู้เล่น/AI รอรับ event ผ่านคำสั่ง
"รอ event ถัดไป" โดยไม่ต้องเดาเอง — ออกแบบตาม GDD §3 ให้ AI ตอบสนองได้โดยไม่ต้องมี
สัญชาตญาณมนุษย์

## การ Implement (Developer Perspective)
- **Class หลัก:** `WorldEventSystem` (`Marooned/Assets/Scripts/Systems/WorldEventSystem.cs`)
- **Model:** `WorldEventDef` (`Marooned/Assets/Scripts/Shared/ClueDef.cs`) — Id, Group
  ("Survival"/"Social"), Weight, DisplayText, RequiredLocationTags
- **Public API สำคัญ:**
  - `void Tick(float deltaSeconds, string currentLocationTag)` — roll ~1% ต่อ tick, กรองด้วย
    location tag, สุ่ม cumulative weight → Enqueue
  - `bool TryDequeue(out WorldEventDef evt)` — ดึง event จากคิว (FIFO)
- **MCP:** `await_next_event` tool → `AwaitNextEventHandler`
  (`Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`) — ตอบ `[{Group}] {DisplayText}
  (id=...)` หรือ "No event occurred within the timeout"
- Pattern: Request-Response await (ไม่ใช้ raw pub/sub ข้าม TCP) ตามบทเรียน Lab 6 ของโปรเจค
  อ้างอิง

## Data Tables
- `DataTables/Data/WorldEventDef.csv` — 4 events: `event_storm` (Survival, w=10),
  `event_wild_animal` (Survival, w=8, tag "jungle"), `event_npc_argument` (Social, w=6),
  `event_npc_help_request` (Social, w=5)
- ⚠️ mock ใน [[LubanDataService.cs]] ยังไม่ใส่ RequiredLocationTags (ต่างจาก CSV)

## ความเชื่อมโยงกับระบบอื่น
- [[survival-stats]] — Survival event ควรกระทบ drain rate (เช่น พายุเร่ง Fatigue)
- [[npc-director]] — Social event ควร trigger จาก NPC state machine (ทะเลาะ/พบศพ)
- [[ExplorationSystem.cs]] — event กรองตาม location tag; ควรผูก `TriggeredEventId`
- [[chibi-avatar]] — บาง event (บาดเจ็บ) ควรแสดง overlay

## สถานะปัจจุบัน
- ✅ Weighted roll + location tag filter + event queue เสร็จแล้ว
- ❌ **ไม่มีใครเรียก `Tick`** — ยังไม่มี game loop
- ❌ `AwaitNextEventHandler` เป็น naive poll — ไม่ block รอจริง, ไม่เคารพ `TimeoutSeconds`
- ❌ Event ที่ deque แล้วไม่มีผลต่อ gameplay ใด ๆ — แค่ข้อความ
- ❌ 1% ต่อ tick เป็น placeholder — รอ per-tag cooldown
- ❌ Social event ยังสุ่มอิสระ ไม่ผูกกับ NPC state (GDD §3 กำหนดไว้)
