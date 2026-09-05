---
title: exploration
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/ExplorationSystem.cs
  - Marooned/Assets/Scripts/Shared/LocationDef.cs
  - Marooned/Assets/Scripts/Systems/GameStateProvider.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[ExplorationSystem.cs]]"
  - "[[CardInventorySystem.cs]]"
  - "[[WorldEventSystem.cs]]"
  - "[[npc-director]]"
  - "[[MapExploreView.cs]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - exploration
  - marooned
  - lab-a
---

# Exploration

## ภาพรวม (Gameplay Perspective)
เกมเกิดบนเกาะที่แบ่งเป็น location nodes (ชายหาด, ชายป่า, ป่าลึก, ปากถ้ำ) เชื่อมกันเป็น graph —
ผู้เล่นย้ายโซนได้เฉพาะ node ที่เชื่อมถึงกัน แล้ว "สำรวจ" node ปัจจุบันเพื่อสุ่มเก็บการ์ดทรัพยากร
แต่ละ node มี loot จำกัด (deplete ได้จริง) ทำให้ต้องขยับขยายอาณาเขต และแย่งทรัพยากรกับ NPC
ตามแนวคิด GDD §2.2

## การ Implement (Developer Perspective)
- **Class หลัก:** `ExplorationSystem`
  (`Marooned/Assets/Scripts/Systems/ExplorationSystem.cs`) + helper `LocationRuntimeState`
- **State:** `LocationRuntimeState.RemainingWeight` — copy ของ `LocationDef.LootTable`
  ต่อ location (สร้างตอน constructor) — ต้นฉบับ def ไม่ถูกแก้
- **Public API สำคัญ:**
  - `(bool success, List<string> foundCardIds) Explore(string locationId)` — สุ่ม weighted
    1 การ์ด, หัก weight, เพิ่มเข้า inventory, set ตำแหน่งผู้เล่น
- **การย้ายโซน:** `MoveToLocationHandler`
  (`Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`) — ตรวจ `unknown_location` /
  `not_connected` ก่อน set `CurrentLocationId` (การเดินทางเป็น graph-based)
- **Flow:** Explore → กรอง weight > 0 (ถ้าหมดหมด = สำรวจสำเร็จแต่ว่างเปล่า) → สุ่ม cumulative
  weight → `RemainingWeight[picked] -= 1` → `TryAdd` → `CurrentLocationId = locationId`
- MCP: `explore_location` / `move_to_location` tools

## Data Tables
- `DataTables/Data/LocationDef.csv` — 4 locations: beach (capacity 6, loot 3 ชนิด),
  jungle_edge, deep_jungle, cave_entrance พร้อม `connectedLocationIds` แบบ `list#sep=;`
- ⚠️ mock ใน [[LubanDataService.cs]] ตรงกับ CSV แล้ว แต่ยังไม่อ่าน Luban JSON

## ความเชื่อมโยงกับระบบอื่น
- [[CardInventorySystem.cs]] — การ์ดที่สุ่มได้เข้า inventory
- [[WorldEventSystem.cs]] — ควร roll event หลังสำรวจ (`TriggeredEventId` ยังว่าง)
- [[npc-director]] — NPC ใช้ LocationDef เดียวกัน; Capacity ใช้เช็คกฎ "no witness"
- [[MapExploreView.cs]] / `MapExplorePresenter` — UI แผนที่ (ยังเป็น skeleton)

## สถานะปัจจุบัน
- ✅ Weighted loot + depletion + connectivity check เสร็จแล้ว
- ❌ **Side effect ซ่อนเร้น**: การ explore ตั้ง `CurrentLocationId` เอง (`ExplorationSystem.cs:52`)
  — สำรวจ node ไกลได้โดยไม่ต้องเดินไป ต้องตัดสินใจออกแบบให้ชัด
- ❌ `TriggeredEventId` ยังส่งค่าว่าง — ไม่ผูกกับ [[WorldEventSystem.cs]]
- ❌ ActionPenalty (สำรวจช้าลงเมื่อป่วย) ยังไม่ implement
- ❌ ไม่มี "หมดแล้ว regenerate ใหม่" — node หมดถาวร (GDD ยังไม่ได้ตัดสินใจ)
