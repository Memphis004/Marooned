# Marooned — Card Survival × Social Deduction

เกม 2D sandbox แนว survival ผสม social deduction (คล้าย Among Us) ที่ผู้เล่นเดินสำรวจเกาะแบบ real-time (WASD), เก็บทรัพยากร/คราฟ, ดูแล Hunger/Thirst/Mood/Fatigue — ขณะเดียวกันก็ต้องสืบหาว่า NPC ตัวไหนเป็น "Killer" ก่อนที่ตัวเองจะกลายเป็นเหยื่อถัดไป (หรือจะเลือกเป็น Killer ซะเองก็ได้)

จุดที่ต่างจากเกมทั่วไป: **ทุกกลไกเล่นผ่าน MCP (Model Context Protocol) ได้** —ออกแบบมาให้ AI VTuber/agent เชื่อมต่อแล้วเล่นแทนสตรีมเมอร์ได้จริง ไม่ใช่แค่คนเล่นคีย์บอร์ดเท่านั้น

> 📖 อ่านรายละเอียดสถาปัตยกรรม/design decision ทั้งหมดได้ที่ [`marooned-wiki/`](marooned-wiki/wiki/sources/index.md) — README นี้เป็นแค่ทางเข้าแบบสรุป

---

## 🎮 สถานะปัจจุบัน (อัปเดต 2026-09-12)

โปรเจกผ่านมาแล้ว 3 "Lab" ใหญ่ ตอนนี้เป็น **playable prototype** ที่ไม่ใช่แค่ code scaffold แล้ว:

- ✅ **Lab A — Survival Core**: stat system, card/inventory, crafting, MCP round-trip กับ AI ผ่านได้จริง
- ✅ **Lab B — Walking Sandbox**: เปลี่ยนจาก point-and-click เป็นเดิน WASD จริง, chibi visual (2 backend: GenericCute/Spine), zone-based loot, card hand UI แบบ click-to-use, **Player-as-Killer** (ใช้การ์ด Weapon ฆ่า NPC ได้ ภายใต้กฎ No-Witness เดียวกับ AI killer)
- ✅ **Lab C — Living World**: Biome scatter + harvestable nodes (ต้องใช้ tool ถูกประเภท), NPC embodiment เต็มรูปแบบ (เดินจริง มี Hunger/Fear/Curiosity), NPC ข้ามโซนโดยเดินผ่านจุดเชื่อมจริง (ไม่ teleport), Utility AI แยกตาม role (`InnocentUtilityAI` / `KillerPlanner` แบบ state machine 5 phase), MCP `move_to_location` ให้ผู้เล่นเดินจริงด้วย (ไม่ teleport เหมือนเดิม)

สิ่งที่ **ยังไม่มี**: Meeting Phase ที่หยุดเกมจริงจัง (ตอนนี้ accuse ได้ทุกเมื่อ), Clue system เวอร์ชันเต็ม (ยังไม่มีกลไกเก็บ clue เข้ามือผู้เล่น), scene/prefab/art จริงจังยังเป็น placeholder เป็นส่วนใหญ่

ดูรายละเอียดสถานะ ✅/⚠️/❌ ของแต่ละระบบที่ [Development Roadmap](marooned-wiki/wiki/sources/Marooned%20Development%20Roadmap.md)

---

## 🗺️ Roadmap (ภาพรวม)

| # | ระบบ | สถานะ |
|---|---|---|
| 1 | Hybrid Transition Points (Camera Follow, Zone Transitions, Biome Scatter) | ✅ เสร็จ |
| 2 | Living NPCs (Embodiment, Survival stats, Basic AI Hooks) | ✅ เสร็จ |
| 3 | Debug Overlay (F12 ดู ground truth ของ NPC) | ✅ เสร็จ |
| 4 | Clue System v2 | ⚪ วางแผน |
| 5 | Clue Board Graph View | ⚪ วางแผน |
| 6 | Accuse() → TryEliminate Merge | ⚪ วางแผน |
| 7 | Vision-based No-Witness + WeatherSystem (mechanic only) | ⚪ วางแผน |
| 8 | Day/Night Cycle  | ⚪ วางแผน |
| 9 | Meeting Phase State Machine | ⚪ วางแผน |
| 10 | Alibi System + UI | ⚪ วางแผน |
| 11 | AwaitNextEvent Timeout | ⚪ วางแผน |
| 12 | Task System (Avalon-lite) | ⚪ วางแผน |
| 13 | Polish: Durability & Collision, visual fog-of-war/weather particles | 🔄 แทรกได้อิสระ |

รายละเอียดเต็ม + กฎการอัปเดต roadmap → [Marooned Development Roadmap.md](marooned-wiki/wiki/sources/Marooned%20Development%20Roadmap.md)

---

## 🏗️ สถาปัตยกรรม (สรุปสั้น)

```
AI VTuber ⇄ (stdio/MCP) ⇄ McpBridge (.NET 8) ⇄ (TCP :3216, MessagePipe.Interprocess) ⇄ Unity (VContainer)
```

- **Unity**: ตัวเกมจริง — Systems ทั้งหมดเป็น plain C# (VContainer DI), state กลางอยู่ที่ `GameStateProvider`/`NpcDirectorSystem`
- **McpBridge**: .NET 8 console app แปลคำสั่ง MCP ↔ TCP request/response ไปหา Unity
- **Data**: Luban pipeline (`DataTables/*.csv` → generate → JSON + C#) สำหรับการ์ด, โซน, ไบโอม, recipe ฯลฯ
- **Information Hiding**: `DeductionSystem` เป็นจุดเดียวที่แปลง ground truth (NPC role จริง) → สิ่งที่ผู้เล่น/AI เห็นได้จริง — role ของ Killer ไม่มีทางหลุดผ่าน MCP response ไหนเลย

อ่านสถาปัตยกรรมเต็ม → [`architecture/overview.md`](marooned-wiki/wiki/sources/architecture/overview.md)

---

## 🚀 เริ่มต้นใช้งาน

1. เปิดโปรเจกด้วย Unity แล้วกด **Play** (ต้องรันก่อนเสมอ — Unity เป็น TCP server)
2. เปิด terminal แยกแล้ว `cd McpBridge && dotnet run` เพื่อสตาร์ท MCP bridge (ต้องรันหลัง Unity Play เท่านั้น)
3. ต่อ MCP client (เช่น Claude Desktop) เข้ากับ `McpBridge` ผ่าน stdio
4. ลองเรียก tool `get_game_state` เพื่อเช็คว่า round-trip ผ่าน

รายละเอียด/troubleshooting → [`architecture/mcp-bridge.md`](marooned-wiki/wiki/sources/architecture/mcp-bridge.md)

### แก้ไข Shared types
ห้ามแก้ไฟล์ copy ใน `Marooned/Assets/Scripts/Shared/` หรือ `McpBridge/Shared/` ตรงๆ — แก้ที่ `Shared/` (root) แล้วรัน `./sync-shared.sh`

### แก้ไข DataTables
แก้ `DataTables/Data/*.csv` แล้วรัน `DataTables/gen.sh` (หรือ `gen.bat` บน Windows)

---

## 📚 เอกสารเพิ่มเติม

ทุกอย่างอยู่ใน [`marooned-wiki/wiki/sources/`](marooned-wiki/wiki/sources/index.md) — จุดเริ่มต้นที่ดีที่สุดคือ [index.md](marooned-wiki/wiki/sources/index.md) ซึ่งมีลิงก์ไปยัง:

- [Game Design Document](marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md) — แหล่งความจริงของทุก design decision
- `architecture/` — overview, mcp-bridge, card-system, biome-scatter-system, npc-embodiment-movement, npc-zone-transitions, npc-survival-motives, player-auto-move-system ฯลฯ
- `mechanics/` — survival-stats, card-inventory, crafting, exploration, npc-director, deduction, world-events, chibi-avatar
- `code-snippets/` — เอกสารรายไฟล์ของโค้ดสำคัญทุกไฟล์
- `bug-log/` — บันทึก bug ที่เจอ + root cause + วิธีแก้ + บทเรียน
- `devlog-history/` — dev log รายวัน

---

## 🧩 Tech Stack

Unity (C#) · VContainer (DI) · MessagePipe + MessagePipe.Interprocess (message bus/TCP) · MessagePack (serialization) · Luban (data tables) · .NET 8 + `ModelContextProtocol` SDK (MCP Bridge)

---

*Last updated: 2026-09-12*
