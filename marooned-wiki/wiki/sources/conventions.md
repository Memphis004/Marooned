---
title: conventions
type: conventions
sources: []
related:
  - "[[overview]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[McpRequestHandlers.cs]]"
folder: sources
created: 2026-09-05
tags:
  - conventions
  - coding-standards
  - marooned
  - lab-a
  - nullable
  - shared-dtos
---

# Coding Conventions — Marooned

สรุป coding conventions จากโค้ดจริงในโปรเจค (สแกน ณ 2026-09-05) — กฎใหม่ควรเขียนให้เข้า
กับแบบแผนที่มีอยู่

## 1. Architecture Patterns
- **DI Pattern:** VContainer + Composition Root เดียวที่ `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs`
  — gameplay systems register เป็น `Lifetime.Singleton` (เช่น
  `builder.Register<SurvivalStatSystem>(Lifetime.Singleton).AsSelf()`), MCP request
  handlers register ด้วย `RegisterAsyncRequestHandler<TReq,TRes,THandler>` ทีละคู่
  (Unity/IL2CPP ไม่มี open-generics auto-registration)
- **Message Bus:** MessagePipe (in-process pub/sub เช่น `SurvivalStatChangedMessage`) +
  MessagePipe.Interprocess (TCP request/response ข้าม process กับ McpBridge)
- **UI Pattern:** MVP Lite — Views อยู่ `UI/Views/` (MonoBehaviour passive), Presenters
  อยู่ `UI/Presenters/` (plain C#)
- **Data Access:** ผ่าน `LubanDataService` (ใน `Systems/GameStateProvider.cs`) เท่านั้น —
  ระบบอื่นฉีดเข้ามาแล้วอ่าน dictionary ของ def ไม่โหลดเอง

## 2. Naming Conventions
- **Classes:** PascalCase — `CardInventorySystem`, `NpcDirectorSystem`, `DeductionSystem`
- **Methods:** PascalCase — `TryCraft()`, `GetObservableNpcsAt()`, `Tick()`
- **Private fields:** `_camelCase` (เช่น `_cardDefs`, `_eliminatedPublisher`) หรือ
  camelCase สำหรับ `[SerializeField]` (เช่น `cardSlotContainer`, `framesPerSecond`)
- **Constants:** PascalCase const ใช้อยู่จริง (`MaxWrongAccusations` ใน `DeductionSystem.cs`)
  — หากเป็น UPPER_SNAKE_CASE ให้ใช้กับ static readonly เท่านั้น
- **Files:** ชื่อไฟล์ตรงกับ class หลัก (`DeductionSystem.cs`); ข้อยกเว้นที่มีอยู่แล้ว:
  `CraftingSystem` อยู่ใน `CardInventorySystem.cs` และ `LubanDataService` อยู่ใน
  `GameStateProvider.cs` — **โค้ดใหม่อย่าทำตามข้อยกเว้นนี้** ให้แยกไฟล์ต่อ class
- **Message classes:** ตาม pattern `<Name>Request`/`<Name>Response` (cross-process) และ
  `<Name>Message` (in-process broadcast)

## 3. Async Pattern
- ใช้ **UniTask** ฝั่ง Unity — `IAsyncRequestHandler` ใน `McpRequestHandlers.cs` คืน
  `UniTask<TRes>` (MessagePipe Unity build แทน ValueTask ด้วย UniTask)
- ฝั่ง McpBridge (.NET 8) ใช้ `Task`/`ValueTask` ตาม SDK — **ห้าม copy ไฟล์ async ข้ามฝั่ง**
- ยังไม่มี convention ต่อท้าย `Async` ในโค้ดปัจจุบัน (method ทั้งหมด sync ภายใน) —
  โค้ดใหม่ที่เป็น async ให้ลงท้าย `Async`
- ไม่ใช้ `async void` ยกเว้น event handlers; ไม่ใช้ coroutines (UniTask แทน)

## 4. System Design Rules
- Gameplay systems เป็น **Singleton** ทั้งหมด (register ใน `GameLifetimeScope.cs`) และ
  **ถือ state ผ่าน `GameStateProvider` เท่านั้น** — ไม่มีใครสร้าง `PlayerSurvivalState` เอง
- Systems เรียกกันตรง ๆ ผ่าน **constructor injection ได้** (เช่น `ExplorationSystem` →
  `CardInventorySystem`) — ใช้ MessagePipe publish เมื่อเป็น "broadcast" ที่หลายผู้ฟัง
  (เช่น stat changed, NPC eliminated); สิ่งที่ต้องเห็นผลทันทีแบบ in-process ให้เรียก
  direct method ตามบทเรียน Lab 13 ของโปรเจคอ้างอิง
- **Information hiding:** สิ่งที่ข้ามเส้น MCP ต้องผ่าน `DeductionSystem` เสมอ — ห้าม
  expose `NpcState` ดิบ (มี `NpcObservableView` สำหรับ view ฝั่งผู้เล่น)

## 5. UI Rules
- Views เป็น **passive MonoBehaviour** — มีแค่ `[SerializeField]` layout hooks
- Presenters เป็น **plain C#** ถือ logic — เรียก View ตรง ๆ (ไม่ผ่าน pub/sub สำหรับ
  in-process UI logic ที่ต้องเห็นผลทันที)
- `UIRoot` (`UI/Core/UIRoot.cs`) เป็น IInitializable entry point + panel registry
  (`UIPanelType` enum → Type)
- panel ใหม่ = เพิ่ม enum value + mapping ใน `UIRoot._panelViewTypes`

## 6. Data & DataTables
- Shared types แก้ที่ `Shared/*.cs` (root) เท่านั้น → รัน `./sync-shared.sh` เพื่อ copy
  ไป `Marooned/Assets/Scripts/Shared/` และ `McpBridge/Shared/` — **ห้ามแก้ไฟล์ copy ตรง ๆ**
- DataTables ใช้ Luban pipeline: แก้ `DataTables/Data/*.csv` → รัน `gen.sh` (หรือ `gen.bat`)
  → code ออกที่ `Data/Gen/` + JSON ที่ `Assets/Resources/DataTables/` — **ห้ามแก้ไฟล์
  generated มือ**
- `McpBridge.csproj` ห้ามใส่ `<Compile Include>` — SDK auto-include อยู่แล้ว (ซ้ำจะ
  build fail NETSDK1022)

## 7. MCP Bridge Rules
- Port: **3216** (Unity = server `HostAsServer = true`, McpBridge = client
  `HostAsServer = false`) — ต้องตรงกันทั้งสองฝั่ง
- Start Unity (Play mode) ก่อน → ค่อย run McpBridge
- Tools ต้องมี attribute จริงตาม SDK: `[McpServerToolType]` ที่ class และ
  `[McpServerTool, Description("...")]` ที่ method (ใน `Program.cs`) — ไม่ต้อง register
  มือฝั่ง Bridge
- Logging ฝั่ง Bridge ต้องไป stderr เท่านั้น (stdout คือช่อง JSON-RPC)
- ทุก tool คืนข้อความ human/LLM-readable และห้าม leak ground truth (role Killer) —
  ดู [[mcp-tool-table]]

## 8. Documentation (Wiki) Rules
- เอกสารใหม่มี YAML frontmatter รูปแบบเดียวกัน: `title`, `type`, `sources` (list ขึ้นบรรทัด
  ใหม่), `related` (quoted WikiLinks list), `folder`, `created`, `tags` (plain text list)
- **ห้าม**เขียน `related: [[A]], [[B]]` หรือ `tags: [[x]]` บรรทัดเดียว — Obsidian parse
  ไม่ได้ (บทเรียนจาก devlog 2026-09-05)
- อ้าง path ไฟล์จริงทุกครั้งที่พูดถึงโค้ด

## 9. Nullable Annotation — Shared DTOs (2026-09-13)

ทุก field ชนิด reference type ใน `Shared/*.cs` (canonical — sync ด้วย `./sync-shared.sh`
ตาม Section 6) ต้องระบุสถานะ null ให้ชัดเจน — **ห้ามปล่อย field เปล่าทั้งที่ type ไม่ nullable**
เพราะจะเด้ง warning CS8618 ฝั่ง McpBridge (`McpBridge.csproj` เปิด `<Nullable>enable</Nullable>`)
และ warning จะโผล่ทีเดียวเป็นชุดใหญ่ตอน clean build (กรณีจริง 2026-09-13: 79 จุดใน 7 ไฟล์ —
incremental build ปกติปิดบังไว้)

### กฎตัดสินใจ (decision tree)
1. **Field ต้องมีค่าเสมอ** → ใส่ default initializer ตอนประกาศ (สไตล์เดิมของไฟล์อยู่แล้ว):
   - `string` → `public string Id = string.Empty;`
   - collection → `public List<string> X = new();` / `public Dictionary<string, int> Y = new();`
   - object DTO → `public ChibiAppearance Avatar = new();`
2. **"ไม่มีค่า" เป็น semantic จริงของ field (null = none)** → ประกาศ `string?` ด้วย scoped
   guard ต่อ field เพื่อให้ถูกต้องทั้ง compile context ที่เปิดและไม่เปิด nullable:
   ```csharp
   #nullable enable
   [Key(3)] public string? LastNoiseLocationId;   // null = ยังไม่มีเสียงให้สืบ
   #nullable restore
   ```
   ใช้เฉพาะ field ที่มีโค้ด **เช็ค `== null` จริง** หรือ **เคลียร์ด้วยการ assign null**
   (เทสก็ `Assert.IsNull`) — ตัวอย่างที่มีอยู่จริง: `NpcSurvivalState.LastNoiseLocationId`
   (Key 3) และ `NpcState.PendingTransitionTargetZoneId` (Key 17) — **อย่า**แปลง field กลุ่มนี้
   เป็น `string.Empty` เพราะเปลี่ยนพฤติกรรม AI/เทส
3. **value type** (int/float/bool/enum) ไม่ต้องทำอะไร — CS8618 ไม่แตะ

### ข้อห้าม
- **ห้ามใช้ `required` modifier** — ทำ call site ที่ `new NpcState()` / object initializer
  แบบไม่ครบ field พังทั้งชุด ทั้งฝั่ง Unity และ Bridge
- **ห้ามเปิด `#nullable enable` ทั้งไฟล์** ใน Shared/ — scoped guard ต่อ field ทำให้ diff สั้น
  และปลอดภัยกับทั้งสองฝั่งที่ nullable context ไม่ตรงกัน
- **ห้าม assign `null` ลง field ที่ไม่มี `?`** — ฝั่ง Bridge จะเด้ง CS8625; optional parameter
  ของ MCP tool ให้ประกาศ `string? x = null` แล้ว coalesce ตอนสร้าง DTO:
  `new UseCardRequest { TargetId = targetId ?? string.Empty }` (contract "null/empty = self"
  คงเดิมเพราะ handler เช็ค `IsNullOrEmpty` อยู่แล้ว)

### MessagePack หมายเหตุ
- Initializer **ไม่เปลี่ยน wire format** (Key-based serialization) — default มีผลแค่ตอน
  `new` โดยตรง; ตอน deserialize ค่าจาก payload ทับเสมอ และ key ที่ payload เก่าไม่มีจะได้
  default (`""` / empty collection) แทน null — ถือเป็นจุดดีด้าน backward compat
- **อย่าเลื่อนเลข Key** ตอนเปลี่ยน field เป็น `string?` — Key เดิมต้องคงไว้เป๊ะ

### วิธีตรวจ
- Incremental build **ปิดบัง** CS8618 ที่ emit ไปแล้ว — ต้อง clean build เท่านั้น:
  ```bash
  ./sync-shared.sh
  cd McpBridge && rm -rf obj bin && dotnet build   # ต้องได้ 0 Warning(s) 0 Error(s)
  ```
- แก้ Shared แล้วต้อง verify ฝั่ง Unity ด้วย (assets-refresh ใน Editor ต้องไม่มี error)
