---
title: card-inventory
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/CardInventorySystem.cs
  - Marooned/Assets/Scripts/Shared/CardDef.cs
  - Marooned/Assets/Scripts/Shared/PlayerSurvivalState.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[CardInventorySystem.cs]]"
  - "[[CraftingSystem.cs]]"
  - "[[ExplorationSystem.cs]]"
  - "[[survival-stats]]"
  - "[[CardHandView.cs]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - cards
  - marooned
  - lab-a
---

# Card Inventory

## ภาพรวม (Gameplay Perspective)
ทรัพยากร อาหาร เครื่องมือ โรค และเบาะแสทั้งหมดในเกมเป็น **การ์ด** — ผู้เล่นเก็บการ์ดเข้า
inventory จากการสำรวจ คราฟ หรือรับเหตุการณ์ แต่ละการ์ดมี `StackLimit` จำกัดจำนวนต่อกอง
ผู้เล่นเปิดดูมือการ์ดผ่าน CardHand panel แล้วเลือก "ใช้" (กิน/ดื่ม/ทายา) หรือลากไปคราฟ

## การ Implement (Developer Perspective)
- **Class หลัก:** `CardInventorySystem`
  (`Marooned/Assets/Scripts/Systems/CardInventorySystem.cs` บรรทัด 6-37)
- **State:** `PlayerSurvivalState.Inventory` — `Dictionary<string, int>` (cardId → count)
  serialize ได้ด้วย MessagePack
- **Public API สำคัญ:**
  - `bool TryAdd(string cardId, int count = 1)` — เพิ่มการ์ด (ตรวจ def มีจริง + clamp ด้วย
    `CardDef.StackLimit` เมื่อ > 0)
  - `bool TryConsume(string cardId, int count = 1)` — หักการ์ด (ลบ key เมื่อหมด)
  - `bool HasAtLeast(string cardId, int count)` — เช็คโดยไม่แก้ state
- **Flow:** ระบบอื่นเรียก TryAdd/TryConsume ผ่าน DI — inventory ไม่ publish event เอง
  (UI อ่านค่าใหม่ตอน query)
- การใช้การ์ด: `UseCardHandler` ใน `McpRequestHandlers.cs` หัก 1 ใบแล้ว apply
  `CardDef.StatEffect` (Hunger/Thirst/Mood/Fatigue) ด้วย clamp 0–100
- นิยามการ์ด: `CardDef` (`Marooned/Assets/Scripts/Shared/CardDef.cs`) — Category
  (Resource/Consumable/Tool/Illness/Injury/Clue/Craftable), StatEffect, ActionPenalty

## Data Tables
- `DataTables/Data/CardDef.csv` — การ์ด 6 ชนิดใน draft (มะพร้าว, น้ำขวด, ปลาดิบ, ปลาย่าง,
  โรค 2 ชนิด) พร้อม stat delta + explore/craft penalty
- ⚠️ [[LubanDataService.cs]] ยังใช้ mock ที่พิมพ์มือ — ยังไม่อ่าน Luban JSON

## ความเชื่อมโยงกับระบบอื่น
- [[ExplorationSystem.cs]] — แหล่งได้การ์ดหลัก (loot)
- [[CraftingSystem.cs]] — หัก input/เติม output ผ่าน inventory นี้
- [[survival-stats]] — ใช้การ์ดเพื่อเติม stat
- [[CardHandView.cs]] / `CardHandPresenter` — UI แสดงมือการ์ด (ยังเป็น skeleton)

## สถานะปัจจุบัน
- ✅ TryAdd/TryConsume/HasAtLeast + StackLimit + UseCard effect เสร็จแล้ว
- ❌ ไม่มี inventory-changed message ให้ UI subscribe (ต้องเพิ่มใน `GameMessages.cs`)
- ❌ ไม่มี hand limit / card slot UI (มือการ์ดยังไม่จำกัดจำนวน — GDD ยังไม่ได้กำหนด)
- ❌ ActionPenalty ของ illness card ยังไม่มีผลกับ action ใด
