---
title: chibi-avatar
type: mechanics
sources:
  - Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs
  - Marooned/Assets/Scripts/Shared/ChibiAppearance.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[ChibiAnimatedRenderer.cs]]"
  - "[[ChibiAppearance]]"
  - "[[LubanDataService.cs]]"
  - "[[survival-stats]]"
  - "[[npc-director]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - avatar
  - marooned
  - lab-a
---

# Chibi Avatar (Sprite-Swap Paperdoll)

## ภาพรวม (Gameplay Perspective)
ตัวละครทุกตัว (ผู้เล่นและ NPC) เป็น chibi paperdoll ที่ประกอบจากชิ้นส่วนแยก — body, head,
hair, แขนข้างซ้าย/ขวา, ขาซ้าย/ขวา, accessory — เดินได้ 4 ทิศด้วยการสลับ sprite frame แบบ
เกม RPG top-down คลาสสิก (ไม่ใช้ bone/skeleton) สถานะของตัวละครแสดงบนตัวได้โดยตรง เช่น
คราบเลือด/ผ้าพันแผล/รอยขีดข่วน เป็น clue ที่ผู้เล่นมองเห็นได้จากภายนอก (GDD §4.1)

## การ Implement (Developer Perspective)
- **Class หลัก:** `ChibiAnimatedRenderer` (MonoBehaviour,
  `Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs`) + `LubanPartLookup` wrapper
- **Data:** `ChibiAppearance` (MessagePack — `Parts`: slot→partId, `Colors`, แยก dict
  `ConditionOverlays`, `Facing`, `AnimState`), `ChibiPartDef` (Slot, DrawOrder, SexTag,
  `FramesByAnimKey`: key เช่น `"Down_Walk"` → list sprite path, PivotX/Y),
  `ChibiOutfitDef` (สลับทั้งชุด atomic)
- **Public API สำคัญ:**
  - `void Init(ChibiAppearance appearance, LubanPartLookup partLookup)` — ผูกข้อมูล + สร้าง
    slot renderers
  - `void SetMotion(FacingDirection facing, bool isMoving)` — เรียกจาก movement controller
- **Flow:** `Init` → สร้าง 1 `SpriteRenderer` ต่อ slot (จาก `Parts` + `ConditionOverlays`)
  โดย sort ด้วย `DrawOrder` (layer stack: leg_back(0) → body(10) → … → accessory(60)) →
  `Update` สร้าง animKey `$"{Facing}_{AnimState}"` → ทุก slot สลับ frame จาก
  `FramesByAnimKey[animKey]` ด้วย cache (`Resources.Load` + dictionary cache)
- หลักการ slot แบบ Dictionary (เพิ่ม slot = แก้ data ไม่ต้องแก้ schema) สืบทอดจาก
  AvatarAppearance ของโปรเจคอ้างอิง

## Data Tables
- `DataTables/Data/ChibiPartDef.csv` — part 11 ชิ้น (body, head, hair 2 แบบ, แขนขา 4,
  overlay 3) พร้อม DrawOrder/Pivot — **frame data วางแผนใช้ JSON sidecar แยกต่อ part**
  (too nested for flat CSV ตามหมายเหตุในไฟล์)
- ⚠️ ตารางนี้**ยังไม่ผ่าน Luban** (ไม่มี `TbChibiPartDef` ใน `Data/Gen/Tables.cs`) และ
  [[LubanDataService.cs]].ChibiPartDefs ยังว่างเปล่า

## ความเชื่อมโยงกับระบบอื่น
- [[npc-director]] / [[DeductionSystem.cs]] — `NpcState.Avatar` / `NpcObservableView.Avatar`
  ส่ง appearance ข้าม MCP ได้ (MessagePack) — AI เห็นตัวตน/คราบเลือดของ NPC
- [[survival-stats]] — condition card (illness/injury) ควร map เป็น ConditionOverlays (Lab B)
- [[LubanDataService.cs]] — แหล่ง ChibiPartDefs ที่ยังต้อง wire
- GDD §4.1 — ConditionOverlays แยกจาก outfit เพื่อให้รักษา/หายโดยไม่ต้องเปลี่ยนชุดทั้งชุด

## สถานะปัจจุบัน
- ✅ Renderer logic (slot rebuild, layer stack, frame swap, sprite cache, SetMotion)
  เสร็จแล้วในระดับโค้ด
- ❌ **ยังไม่มีใครเรียก `Init()`** — ไม่มี spawner/movement controller
- ❌ `FramesByAnimKey` ยังไม่มีข้อมูลจริง (รอ JSON sidecar) — ตอนนี้ renderer วาดอะไรไม่ได้
- ❌ ไม่มี art asset, prefab, scene ในโปรเจค
- ❌ tint จาก `ChibiAppearance.Colors` ยังไม่ implement (reserved)
