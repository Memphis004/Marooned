/goal อ่านโค้ดทั้งหมดในโปรเจค Marooned รวมถึง game_design_doc.md 
แล้วสร้างเอกสาร architecture รวม ลงที่:
marooned-wiki/wiki/sources/architecture/overview.md

เอกสารต้องครอบคลุม:
1. แต่ละ .md มีโครงสร้าง:
   ---
   title: [ชื่อไฟล์เดิม]
   type: architecture
   sources: [relative path]
   folder: [ชื่อโฟลเดอร์]
   lines: [จำนวนบรรทัด]
   created: 2026-09-05
   tags: [[ชื่อโฟลเดอร์], marooned, lab-a]
   ---
   
   # [ชื่อไฟล์]
   **Path:** `[path]` ([N] lines)
   
   ## Source
   ```[language]
   [content ต้นฉบับครบถ้วน]
   ```
   
   language: .cs→csharp, .csv→csv, .md→markdown, .sh→bash, .json→json, .csproj→xml
2. ภาพรวม architecture ทั้งหมด (Composition Root, DI, Message Bus)
3. ระบบทั้งหมดที่มีอยู่ (Systems/) — แต่ละระบบทำหน้าที่อะไร
4. การเชื่อมต่อระหว่าง Unity ↔ McpBridge (TCP, MessagePipe.Interprocess)
5. Shared types และ data flow
6. UI pattern (MVP Lite)
7. Data pipeline (Luban)
8. จุดที่เป็น TODO / stub / ยังไม่เสร็จ

เขียนเป็น Markdown พร้อม [[WikiLinks]] เชื่อมโยงระหว่าง concepts
ใช้อธิบายเป็นภาษาไทย แต่ technical terms ใช้ภาษาอังกฤษ
อ้างอิง file path จริงทุกครั้งที่กล่าวถึงโค้ด

-----------------------------------------------------------------------
/goal อ่านโค้ดใน Systems/, Core/, Data/, UI/ ทั้งหมด 
แล้วสร้างเอกสารแยกไฟล์สำหรับแต่ละ script สำคัญ ลงที่:
marooned-wiki/wiki/sources/code-snippets/

ตั้งชื่อไฟล์ตามชื่อ class เช่น:
- GameLifetimeScope.cs.md
- SurvivalStatSystem.cs.md
- CardInventorySystem.cs.md
- CraftingSystem.cs.md
- ExplorationSystem.cs.md
- NpcDirectorSystem.cs.md
- DeductionSystem.cs.md
- WorldEventSystem.cs.md
- ChibiAnimatedRenderer.cs.md
- McpRequestHandlers.cs.md
- LubanDataService.cs.md
- UIRoot.cs.md

กฎการสร้างไฟล์:
1. ทุกไฟล์ .cs, .md, ในไดเรกทอรี่ → สร้างเป็น .md แยกกัน
2. ตั้งชื่อ: path เดิมเปลี่ยน / เป็น _ ลงท้ายด้วย .md
   ตัวอย่าง: Shared/CardDef.cs → Shared_CardDef.cs.md
   ตัวอย่าง: McpBridge/Program.cs → McpBridge_Program.cs.md
   ตัวอย่าง: LLMWiki/game_design_doc.md → LLMWiki_game_design_doc.md.md
3. แต่ละ .md มีโครงสร้าง:
   ---
   title: [ชื่อไฟล์เดิม]
   type: snippet
   sources: [relative path]
   folder: [ชื่อโฟลเดอร์]
   lines: [จำนวนบรรทัด]
   created: 2026-09-05
   tags: [[ชื่อโฟลเดอร์], marooned, lab-a]
   ---
   
   # [ชื่อไฟล์]
   **Path:** `[path]` ([N] lines)
   
   ## Source
   ```[language]
   [content ต้นฉบับครบถ้วน]
   ```
   
   language: .cs→csharp, .csv→csv, .md→markdown, .sh→bash, .json→json, .csproj→xml
4. เวลาสร้าง YAML Frontmatter ให้ใช้ syntax นี้:
- List items ต้องขึ้นบรรทัดใหม่ + indent 2 spaces
- WikiLinks ต้องใส่ quotes: - "[[Link]]"
- ห้ามเขียน related: [[A]], [[B]] ในบรรทัดเดียว   
   
# [ClassName]

## Purpose
อธิบายหน้าที่ของ class นี้ 1-2 ประโยค

## Public API
public methods/properties พร้อมคำอธิบายสั้นๆ

## Dependencies
class นี้ขึ้นอยู่กับอะไร (DI, MessagePipe events, etc.)

## Key Logic
อธิบาย logic สำคัญ (ไม่ต้อง copy โค้ดทั้งหมด แค่สรุป flow)

## TODO / Known Issues
จุดที่ยังไม่เสร็จหรือมีปัญหา

เขียนอธิบายเป็นภาษาไทย technical terms เป็นภาษาอังกฤษ

-----------------------------------------------------------------------
@marooned-wiki/wiki/sources/code-snippets/

ไฟล์ทั้งหมดใน folder นี้มี YAML frontmatter ที่ related: อยู่ในบรรทัดเดียว 
ทำให้ Obsidian แสดง error

ช่วยแก้ทุกไฟล์ใน folder ให้ related: เป็น YAML list ที่ถูกต้องแบบนี้:

related:
  - "[[MVP-Lite]]"
  - "[[CardHandView]]"
  - "[[CardInventorySystem]]"

แก้ทุกไฟล์ใน code-snippets/ เลย ไม่ต้องถามยืนยัน

------------------------
@marooned-wiki/wiki/sources/code-snippets/

ยังแก้ไม่ครบค่ะ! field `tags:` ในหลายไฟล์ยังเขียนผิด syntax อยู่

ตัวอย่างผิด:
tags: [[Systems]],"marooned","lab-a"

ตัวอย่างที่ถูก:
tags:
  - Systems
  - marooned
  - lab-a

กฎ:
- tags ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + indent 2 spaces
- ห้ามใส่ [[ ]] ใน tags (tags ไม่ใช่ WikiLink)
- ห้ามใช้ quotes ผสมกับ [[ ]]
- แต่ละ tag ต้องเป็น plain text เท่านั้น

ช่วยแก้ทุกไฟล์ใน code-snippets/ ที่ tags: ยังผิดอยู่ ให้ถูกต้อง
-----------------------------------------------------------------------
goal อ่าน game_design_doc.md และโค้ดใน Systems/ ทั้งหมด 
แล้วสร้างเอกสาร mechanics แยกตามระบบเกม ลงที่:
marooned-wiki/wiki/sources/mechanics/

สร้างไฟล์เหล่านี้:
- survival-stats.md → ระบบ Survival Stats (หิว, กระหาย, พลังงาน, etc.)
- card-inventory.md → ระบบ Card Inventory และ hand management
- crafting.md → ระบบ Crafting และ recipes
- exploration.md → ระบบ Exploration และ map
- npc-director.md → ระบบ NPC behavior และ daily schedule
- deduction.md → ระบบ Social Deduction (Killer/Innocent, clues, voting)
- world-events.md → ระบบ World Events และ effects
- chibi-avatar.md → ระบบ Chibi sprite-swap avatar

=== กฎ YAML Frontmatter (สำคัญมาก! ต้องทำตามนี้ทุกไฟล์) ===

ตัวอย่าง Frontmatter ที่ถูกต้อง:
---
title: survival-stats
type: mechanics
sources:
  - Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs
  - Shared/PlayerSurvivalState.cs
related:
  - "[[CardInventorySystem]]"
  - "[[WorldEventSystem]]"
  - "[[overview]]"
folder: mechanics
created: 2026-09-05
tags:
  - mechanics
  - survival
  - marooned
  - lab-a
---

กฎที่ต้องปฏิบัติตามอย่างเคร่งครัด:
1. `sources:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + indent 2 spaces
   ✅ ถูก:
   sources:
     - path/to/file1.cs
     - path/to/file2.cs
   ❌ ผิด: sources: [file1.cs, file2.cs]
   ❌ ผิด: sources: file1.cs, file2.cs

2. `related:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + WikiLink ต้องใส่ quotes
   ✅ ถูก:
   related:
     - "[[SomePage]]"
     - "[[AnotherPage]]"
   ❌ ผิด: related: [[SomePage]], [[AnotherPage]]
   ❌ ผิด: related: - "[[A]]", "[[B]]"

3. `tags:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + เป็น plain text เท่านั้น
   ✅ ถูก:
   tags:
     - mechanics
     - survival
     - marooned
   ❌ ผิด: tags: [[mechanics]],"survival","marooned"
   ❌ ผิด: tags: mechanics, survival, marooned
    ผิด: tags: [mechanics, survival]

4. ใช้ indent 2 spaces เท่านั้น (ห้ามใช้ tab)
5. ห้ามเขียน list item หลายอันในบรรทัดเดียวเด็ดขาด
6. ทุก field ต้องขึ้นบรรทัดใหม่เสมอ

=== เนื้อหาในแต่ละไฟล์ ===

แต่ละไฟล์ต้องมีส่วนเหล่านี้ (หลังจาก Frontmatter):

# [ชื่อระบบ]

## ภาพรวม (Gameplay Perspective)
อธิบายว่า mechanic นี้ทำงานยังไงจากมุมมองผู้เล่น 2-3 ประโยค

## การ Implement (Developer Perspective)
- class หลักที่เกี่ยวข้อง
- flow การทำงาน
- public API สำคัญ

## Data Tables
ระบุ DataTables/ ที่เกี่ยวข้อง (ถ้ามี)

## ความเชื่อมโยงกับระบบอื่น
ใช้ [[WikiLinks]] เชื่อมโยงกับระบบอื่นใน Wiki

## สถานะปัจจุบัน
- ✅ เสร็จแล้ว / ⚠️ stub / ❌ TODO
- ระบุจุดที่ยังต้องทำต่อ

เขียนอธิบายเป็นภาษาไทย technical terms เป็นภาษาอังกฤษ
-----------------------------------------------------------------------
อ่านโค้ดใน McpBridge/ ทั้งหมด 
แล้วสร้างเอกสารลงที่:
marooned-wiki/wiki/sources/architecture/mcp-bridge.md

และสร้าง:
marooned-wiki/wiki/sources/architecture/mcp-tool-table.md

=== กฎ YAML Frontmatter (สำคัญมาก! ต้องทำตามนี้ทุกไฟล์) ===

ตัวอย่าง Frontmatter ที่ถูกต้อง:
---
title: mcp-bridge
type: architecture
sources:
  - McpBridge/Program.cs
  - McpBridge/Tools/SurvivalQueryTools.cs
related:
  - "[[overview]]"
  - "[[game-design-doc]]"
  - "[[GameLifetimeScope]]"
folder: architecture
created: 2026-09-05
tags:
  - architecture
  - mcp
  - marooned
  - lab-a
---

กฎที่ต้องปฏิบัติตามอย่างเคร่งครัด:
1. `sources:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + indent 2 spaces
   ✅ ถูก:
   sources:
     - path/to/file1.cs
     - path/to/file2.cs
   ❌ ผิด: sources: [file1.cs, file2.cs]
    ผิด: sources: file1.cs, file2.cs

2. `related:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + WikiLink ต้องใส่ quotes
   ✅ ถูก:
   related:
     - "[[SomePage]]"
     - "[[AnotherPage]]"
   ❌ ผิด: related: [[SomePage]], [[AnotherPage]]
   ❌ ผิด: related: - "[[A]]", "[[B]]"

3. `tags:` ต้องเป็น YAML list ขึ้นบรรทัดใหม่ + เป็น plain text เท่านั้น
   ✅ ถูก:
   tags:
     - architecture
     - mcp
   ❌ ผิด: tags: [[architecture]],"mcp"
   ❌ ผิด: tags: architecture, mcp
   ❌ ผิด: tags: [architecture, mcp]

4. ใช้ indent 2 spaces เท่านั้น (ห้ามใช้ tab)
5. ห้ามเขียน list item หลายอันในบรรทัดเดียวเด็ดขาด
6. ทุก field ต้องขึ้นบรรทัดใหม่เสมอ

=== เนื้อหา mcp-bridge.md ===

# MCP Bridge Architecture

## 1. MCP Bridge คืออะไร
- อธิบายหน้าที่และบทบาทของ MCP Bridge ในโปรเจค
- ความสัมพันธ์กับ Unity (TCP client ↔ server)
- ทำไมต้องแยกเป็น .NET console app ต่างหาก

## 2. Tool Surface ทั้งหมด
อธิบายแต่ละ tool class:
- SurvivalQueryTools → query tools มีอะไรบ้าง
- SurvivalActionTools → action tools มีอะไรบ้าง
- DeductionTools → deduction tools มีอะไรบ้าง

## 3. TCP Connection
- Port ที่ใช้ (3216)
- Protocol (MessagePipe.Interprocess)
- ใครเป็น server (Unity) ใครเป็น client (McpBridge)
- วิธี start (Unity ก่อน แล้วค่อย run bridge)

## 4. วิธี Build และ Run
- Commands สำหรับ build/run McpBridge
- Dependencies ที่ต้อง install (dotnet add package ...)
- วิธีตรวจสอบว่าเชื่อมต่อสำเร็จ

## 5. วิธีเพิ่ม MCP Tool ใหม่ (Step-by-Step)
- สร้าง class ใหม่ใน Tools/
- เพิ่ม method + [McpTool] attribute
- Register ใน Program.cs
- เพิ่ม documentation ใน mcp-tool-table.md
- ทดสอบ connection

## 6. สถานะปัจจุบัน
- ตารางสรุป: Tool Name | Status (✅/⚠️/❌) | Notes
- ระบุทุก method ที่เป็น NotImplementedException
- TODO items ที่ต้องทำต่อ

เขียนอธิบายเป็นภาษาไทย technical terms เป็นภาษาอังกฤษ

=== เนื้อหา mcp-tool-table.md ===

# MCP Tool Table

ตาราง MCP tools ทั้งหมด อ้างอิงจาก game_design_doc.md และโค้ดจริงใน McpBridge/

## ตารางหลัก

| Tool Name | Category | Input | Output | Description | Status |
|-----------|----------|-------|--------|-------------|--------|
| GetGameState | SurvivalQuery | - | GameState | ดึงสถานะเกมปัจจุบัน | ✅/⚠️/❌ |
| ... | ... | ... | ... | ... | ... |

(สร้างแถวให้ครบทุก tool ที่พบในโค้ด + game_design_doc.md)

## คำอธิบายแต่ละ Tool
อธิบายสั้นๆ 1-2 ประโยคสำหรับแต่ละ tool ว่าทำอะไร และใช้เมื่อไหร่

## การเรียกใช้จาก AI VTuber
อธิบาย format การเรียก tool จาก MCP client side

เขียนอธิบายเป็นภาษาไทย technical terms เป็นภาษาอังกฤษ
-----------------------------------------------------------------------
@marooned-wiki/wiki/sources/

ช่วยวิเคราะห์ทุก [[WikiLink]] ในเอกสารทั้งหมด 
แล้วแยกเป็น 2 กลุ่ม:

1. **ควรสร้างไฟล์จริง**: WikiLink ที่หมายถึง class, system, concept 
   ที่สำคัญควรมีเอกสารเฉพาะ (เช่น LubanDataService, CardDef)

2. **ควรลบออก**: WikiLink ที่เป็นแค่ general term 
   ที่ไม่จำเป็นต้องมีไฟล์เฉพาะ (เช่น MVP-Lite, transient)

สำหรับกลุ่มที่ 2 ช่วยลบ [[ ]] ออก ให้เป็น plain text แทน
เช่น [[MVP-Lite]] → MVP-Lite

เขียนอธิบายเป็นภาษาไทย technical terms เป็นภาษาอังกฤษ
------------------------