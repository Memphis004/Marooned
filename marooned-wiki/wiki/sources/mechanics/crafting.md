---
title: crafting
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/CardInventorySystem.cs
  - Marooned/Assets/Scripts/Shared/LocationDef.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[CraftingSystem.cs]]"
  - "[[CardInventorySystem.cs]]"
  - "[[ExplorationSystem.cs]]"
  - "[[survival-stats]]"
  - "[[CardHandView.cs]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - crafting
  - marooned
  - lab-a
---

# Crafting

## ภาพรวม (Gameplay Perspective)
ผู้เล่นรวมการ์ดวัตถุดิบตามสูตร (Recipe) เพื่อได้การ์ดที่ดีกว่า — เช่น ปลาดิบ 1 ใบ → ปลาย่าง
1 ใบ ที่เติม Hunger ได้มากกว่าและเพิ่ม Mood บางสูตรต้องคราฟที่สถานที่เฉพาะหรือมี tool card
ในมือ (เช่น tool_campfire) การคราฟเป็นทางเดียวที่จะเปลี่ยนทรัพยากรดิบให้เป็นของใช้ที่มีค่า

## การ Implement (Developer Perspective)
- **Class หลัก:** `CraftingSystem`
  (`Marooned/Assets/Scripts/Systems/CardInventorySystem.cs` บรรทัด 39-73 — อยู่ไฟล์เดียวกับ
  CardInventorySystem แต่ register แยกใน VContainer)
- **Public API สำคัญ:**
  - `(bool success, string failureReason, string outputCardId) TryCraft(string recipeId)` —
    failureReason เป็น machine-readable เหมาะกับ AI agent
- **Flow:** `TryCraft` ตรวจลำดับ: `unknown_recipe` → `wrong_location`
  (`RecipeDef.RequiredLocationIds` ถ้าระบุ) → `missing_tool` (`RequiredToolCardId` ต้องมีใน
  inventory แต่**ไม่ถูกหัก**) → `missing_ingredients` (ตรวจ `Inputs` ทุกรายการ) → หักวัตถุดิบ
  → `TryAdd(outputCardId, outputCount)`
- MCP: `craft_card` tool → `CraftCardHandler` (`Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`)
- Recipe model: `RecipeDef` (`Marooned/Assets/Scripts/Shared/LocationDef.cs`) — OutputCardId,
  OutputCount, Inputs (cardId → count), RequiredLocationIds, RequiredToolCardId

## Data Tables
- `DataTables/Data/RecipeDef.csv` — สูตรเดียวตอนนี้: `recipe_cook_fish` (raw_fish ×1 →
  cooked_fish ×1, required tool `tool_campfire`)
- `DataTables/Data/CardDef.csv` — นิยามผลลัพธ์ (cooked_fish: Hunger +25, Mood +5)
- ⚠️ mock ใน [[LubanDataService.cs]] ยังไม่ใส่ required tool → ต่างจาก CSV

## ความเชื่อมโยงกับระบบอื่น
- [[CardInventorySystem.cs]] — แหล่งวัตถุดิบและปลายทาง output
- [[ExplorationSystem.cs]] — ต้องสำรวจเพื่อเก็บวัตถุดิบก่อน
- [[survival-stats]] — อาหารปรุงสุกเติม stat ได้ดีกว่าของดิบ
- GDD §2.1 — illness ควรทำให้ "คราฟพลาดง่ายขึ้น" (ยังไม่ implement)

## สถานะปัจจุบัน
- ✅ TryCraft ตรวจครบทั้ง 4 เงื่อนไข + หัก/เติม inventory ถูกต้อง
- ❌ คราฟสำเร็จไม่มี event publish — UI ไม่รู้ทันที
- ❌ ActionPenalty (คราฟพลาดง่ายเมื่อป่วย) ยังไม่ implement
- ❌ ตาราง recipe จริงมีแค่สูตรเดียว — ต้องเพิ่ม content
- ❌ `tool_campfire` ยังไม่มีใน CardDef (mock) — สูตรจาก CSV คราฟไม่ได้ในทางปฏิบัติ
