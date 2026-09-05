---
title: overview
type: architecture
sources:
  - Marooned/Assets/Scripts/
  - McpBridge/
  - Shared/
  - DataTables/
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
folder: architecture
lines: 2623
created: 2026-09-05
tags:
  - architecture
  - marooned
---

# overview
**Path:** `marooned-wiki/wiki/sources/architecture/overview.md` (2623 lines — ภาพรวมสถาปัตยกรรมทั้งโปรเจค Marooned — สแกนโค้ดครบทุกไฟล์ ณ วันที่ 2026-09-05)

เอกสารนี้คือ **architecture overview รวม** ของโปรเจค Marooned — เกม single-player 2D sandbox
card-survival ที่มี social deduction ผสมอยู่ และออกแบบให้ AI-VTuber เล่นผ่าน MCP ได้
ปัจจุบันอยู่ช่วง **Lab A (Survival Core)** — เป็น code scaffolding ยังไม่มี scene/prefab/art
(ดู [[Lab-A]] และ [[game_design_doc]])

## Source

ตารางนี้คือ **inventory ของไฟล์ต้นฉบับทั้งหมด** ที่ใช้ประกอบเอกสารนี้ (โค้ดฉบับเต็มของทุกไฟล์ที่เขียนมือ
อยู่ใน [ภาคผนวก](#ภาคผนวกโค้ดต้นฉบับครบถ้วน-per-file-snippets) ท้ายเอกสาร):

| Path | Lines | หน้าที่ |
| --- | --- | --- |
| `marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md` | 219 | GDD — แหล่งความจริงของ design decisions ทั้งหมด |
| `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs` | 90 | Composition Root (VContainer) |
| `Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs` | 73 | Tick stat ผู้เล่น 4 ค่า + roll illness |
| `Marooned/Assets/Scripts/Systems/CardInventorySystem.cs` | 74 | Inventory + CraftingSystem (อยู่ไฟล์เดียวกัน) |
| `Marooned/Assets/Scripts/Systems/ExplorationSystem.cs` | 57 | สำรวจ location, weighted loot แบบ deplete ได้ |
| `Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs` | 109 | เจ้าของ NpcState ทั้งหมด + killer AI + spawn clue |
| `Marooned/Assets/Scripts/Systems/DeductionSystem.cs` | 75 | Information-hiding layer + ตัดสิน accusation |
| `Marooned/Assets/Scripts/Systems/WorldEventSystem.cs` | 47 | Weighted random event queue (Survival/Social) |
| `Marooned/Assets/Scripts/Systems/GameStateProvider.cs` | 158 | Single live state + LubanDataService (mock) |
| `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs` | 202 | 10 request handlers รับจาก MCP Bridge |
| `Marooned/Assets/Scripts/Shared/` (7 ไฟล์) | 464 | Shared types (copy จาก `Shared/` ด้วย sync script) |
| `Marooned/Assets/Scripts/UI/Core/UIRoot.cs` | 43 | Panel registry ของ UI |
| `Marooned/Assets/Scripts/UI/Presenters/` (4 ไฟล์) | 30 | Presenter skeletons (ยังว่าง) |
| `Marooned/Assets/Scripts/UI/Views/` (5 ไฟล์) | 53 | View MonoBehaviours (ยังเป็น skeleton) |
| `Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs` | 127 | Chibi sprite-swap paperdoll renderer |
| `Marooned/Assets/Scripts/Data/Gen/` (13 ไฟล์) | 764 | Luban auto-generated code (namespace `cfg`) |
| `McpBridge/Program.cs` | 198 | .NET 8 MCP server + 10 MCP tools |
| `McpBridge/McpBridge.csproj` | 23 | Bridge project file (net8.0) |
| `Shared/` (7 ไฟล์ — canonical) | 464 | ต้นฉบับ Shared types |
| `DataTables/Data/*.csv` (7 ไฟล์) | 47 | Luban source CSV drafts |
| `DataTables/gen.sh` / `gen.bat` / `luban.conf` | 17+18+15 | Luban code/data generation scripts |
| `DataTables/Defines/event.xml` | — | Luban schema definition |
| `sync-shared.sh` | 17 | Copy `Shared/*.cs` → Unity + McpBridge |
| `Marooned/Assets/Resources/DataTables/*.json` (6 ไฟล์) | — | Luban JSON output (ยังไม่มีใครโหลด) |

---

## 1. ภาพรวมสถาปัตยกรรม (Composition Root, DI, Message Bus)

### 1.1 ภาพรวม 3 กระบวนการ

```
┌─────────────────────────────┐         stdio (JSON-RPC)        ┌──────────────────────────┐
│   AI VTuber (Claude/GPT)    │ ◄──────────────────────────────► │  McpBridge (.NET 8)      │
└─────────────────────────────┘                                  │  MCP Server + Tools      │
                                                                 │  McpBridge/Program.cs    │
                                                                 └────────────┬─────────────┘
                                                          MessagePipe.Interprocess
                                                     Request/Response ผ่าน TCP 127.0.0.1:3216
                                                          (MessagePack serialization)
                                                                     │
                                                                     ▼
                                                                 ┌──────────────────────────┐
                                                                 │  Unity (HostAsServer)    │
                                                                 │  GameLifetimeScope.cs    │
                                                                 │  ├─ Gameplay Systems     │
                                                                 │  ├─ GameStateProvider    │
                                                                 │  ├─ McpRequestHandlers   │
                                                                 │  ├─ UI (MVP Lite)        │
                                                                 │  └─ MessagePipe (in-proc)│
                                                                 └──────────────────────────┘
```

- **AI VTuber ↔ McpBridge**: stdio MCP (Model Context Protocol) — Bridge เป็น MCP server,
  tool ถูก auto-discover จาก assembly ด้วย `[McpServerToolType]` (`McpBridge/Program.cs:30-33`)
- **McpBridge ↔ Unity**: MessagePipe-Interprocess ผ่าน TCP `127.0.0.1:3216` — Unity เป็น
  **server** (`HostAsServer = true`), Bridge เป็น client (`HostAsServer = false`)
- **ภายใน Unity**: MessagePipe in-process pub/sub + request handlers ผ่าน VContainer

### 1.2 Composition Root — `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs`

`GameLifetimeScope` (extends `LifetimeScope` ของ VContainer) คือจุดประกอบร่างทั้งหมด ทำ 4 อย่าง:

1. **Register MessagePipe** — `builder.RegisterMessagePipe()` แล้วผูก
   `GlobalMessagePipe.SetProvider(...)` เพื่อเปิด Diagnostics window (`GameLifetimeScope.cs:41-44`)
2. **เปิด TCP interprocess ฝั่ง server** — `messagePipeBuilder.AddTcpInterprocess(host, port, tcp => tcp.HostAsServer = true)`
   ที่ port `3216` (ตั้งใจแยกจากโปรเจคอ้างอิง Cultivation-Together ที่ใช้ `3215`) (`GameLifetimeScope.cs:37,47-50`)
3. **บังคับสร้าง TcpWorker จริง** — `RegisterBuildCallback` ที่ `container.Resolve<TcpWorker>()`
   เพราะไม่งั้น VContainer จะ register ไว้เฉยๆ ไม่ยอม instantiate (`GameLifetimeScope.cs:52-56`)
4. **Register ทุก dependency แบบ manual** — Unity/IL2CPP ไม่มี open-generics auto-registration
   จึงต้อง `RegisterAsyncRequestHandler<TReq,TRes,THandler>` ทั้ง 10 คู่เอง (`GameLifetimeScope.cs:75-84`)
   ต่างจากฝั่ง McpBridge ที่เป็น plain .NET และได้ `IRemoteRequestHandler<TReq,TRes>` ฟรีหลัง config client

ระบบที่ register เป็น Singleton ทั้งหมด: `SurvivalStatSystem`, `CardInventorySystem`,
`CraftingSystem`, `ExplorationSystem`, `NpcDirectorSystem`, `DeductionSystem`,
`WorldEventSystem`, `LubanDataService`, `GameStateProvider` + `RegisterEntryPoint<UIRoot>()`

> ⚠️ หมายเหตุในโค้ดเอง (`GameLifetimeScope.cs:29-32`): overload ชื่อ/ลำดับพารามิเตอร์ของ
> MessagePipe.Interprocess 1.8.2 ยัง **STILL VERIFY** — ถ้า compiler ฟ้อง ให้เชื่อ compiler
> มากกว่า comment

### 1.3 Message Bus — สองชั้น

| ชั้น | ไลบรารี | ใช้ทำอะไร | ตัวอย่าง message |
| --- | --- | --- | --- |
| In-process (ภายใน Unity) | MessagePipe | Broadcast เหตุการณ์ภายในระหว่าง systems กับ UI | `SurvivalStatChangedMessage`, `ConditionCardAppliedMessage`, `NpcEliminatedMessage` (`Marooned/Assets/Scripts/Shared/GameMessages.cs:147-168`) |
| Inter-process (Unity ↔ Bridge) | MessagePipe.Interprocess (TCP) | Request/Response ข้าง process ด้วย MessagePack | 10 คู่ Request/Response (`Marooned/Assets/Scripts/Shared/GameMessages.cs:11-143`) |

ข้อควรระวังที่บันทึกไว้ใน `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs:8-14`:
build ฝั่ง Unity ของ MessagePipe แทน `ValueTask<T>` ด้วย **`UniTask<T>`** (ต้องมี UniTask package)
ขณะที่ฝั่ง .NET ของ McpBridge ใช้ `ValueTask<T>` — **ห้าม copy ไฟล์ handler ข้ามฝั่ง**

---

## 2. ระบบทั้งหมดใน `Marooned/Assets/Scripts/Systems/`

ทุก system เป็น plain C# class (non-MonoBehaviour) register เป็น Singleton ผ่าน VContainer
และอ่าน/เขียน state กลางผ่าน `GameStateProvider` — ไม่มีใครสร้าง state เอง

### [[SurvivalStatSystem.cs]] — `Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs`
- `Tick(deltaSeconds)` ลด Hunger (0.15/s), Thirst (0.25/s) และเพิ่ม Fatigue (0.10/s) แบบ clamp 0–100
- ถ้า Hunger ≤ 15 นานเกิน 120 วินาที → เพิ่ม condition `illness_malnutrition` (Thirst ≤ 15 นาน 90 วิ → `illness_dehydration`)
- Hunger และ Thirst ถึง 0 พร้อมกัน → `IsAlive = false`
- ทุกครั้งที่ stat เปลี่ยน publish `SurvivalStatChangedMessage` ออก bus
- ใช้ fractional-accumulator pattern ตามโปรเจคอ้างอิง (TickGathering/TickCrafting)

### [[CardInventorySystem.cs]] + [[CraftingSystem.cs]] — `Marooned/Assets/Scripts/Systems/CardInventorySystem.cs`
- `CardInventorySystem`: `TryAdd` (เคารพ `CardDef.StackLimit`), `TryConsume`, `HasAtLeast`
  บน `PlayerSurvivalState.Inventory` (Dictionary `cardId → count`)
- `CraftingSystem` (อยู่ไฟล์เดียวกัน ไฟล์เดียวมี 2 class): `TryCraft(recipeId)` เช็คลำดับ
  `unknown_recipe → wrong_location → missing_tool → missing_ingredients` แล้วหัก input,
  เติม output (`CardInventorySystem.cs:52-72`)

### [[ExplorationSystem.cs]] — `Marooned/Assets/Scripts/Systems/ExplorationSystem.cs`
- เก็บ `LocationRuntimeState.RemainingWeight` ต่อ location — loot node **deplete ได้จริง**
  (สุ่มแบบ weighted แล้วหัก weight ทีละ 1; หมด = explore ได้แต่ว่างเปล่า)
- Side effect: การ explore ตั้ง `_player.CurrentLocationId` ให้เป็น location นั้นด้วย (`ExplorationSystem.cs:52`)

### [[NpcDirectorSystem.cs]] — `Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs`
- **เจ้าของ ground truth ทั้งหมดของ NPC** (รวม `NpcRole.Killer`) — class นี้ห้าม leak ออกนอก Unity โดยตรง
- `SetupRound(npcIds, killerCount)` สุ่มเลือก killer (guideline 1:4–1:8 ตาม Among Us, `NpcDirectorSystem.cs:32-53`)
- `Tick` เรียก `TickBehavior` (**placeholder ว่างเปล่า** — schedule จริงรอ Luban table) และ
  `TryAttemptElimination` สำหรับ Killer: ลด cooldown → เช็คกฎ **"no witness"** (มีตัวอื่นอยู่ใน
  location เดียวกัน **ตัวเดียว** เท่านั้น = เหยื่อ) → โอกาส 15% ต่อ tick → ฆ่า, ตั้ง cooldown 180s,
  spawn clue, publish `NpcEliminatedMessage`
- `SpawnClues` เวอร์ชัน v1 ยัง hardcode `clue_blood_stain` + 30% `clue_scratch_mark`
  (รอย้ายไปใช้ `ClueDef` weighted roll เมื่อ DataTable พร้อม, `NpcDirectorSystem.cs:98-107`)

### [[DeductionSystem.cs]] — `Marooned/Assets/Scripts/Systems/DeductionSystem.cs`
**หัวใจของ information hiding** (ดู [[Information-Hiding]]):
- `GetObservableNpcsAt(locationId)` เป็น "ทางเดียวที่อนุญาต" ในการแปลง `NpcState` (ground truth)
  → `NpcObservableView` (สิ่งที่ผู้สังเกตเห็นได้จริง) — ไม่มี `NpcRole`, ไม่มี `KillCooldownRemaining`,
  กรอง condition card ด้วย `IllnessDef.Visible` (`DeductionSystem.cs:29-53`)
- `Accuse(targetNpcId)`: โหวตถูก → killer ตัวนั้นตาย, ถ้าหมด = **win**; โหวตผิด →
  `WrongAccusations++`, Mood −15, ครบ 3 ครั้ง = **loss** (`MaxWrongAccusations = 3`)

### [[WorldEventSystem.cs]] — `Marooned/Assets/Scripts/Systems/WorldEventSystem.cs`
- Roll event แบบ weighted (~1% ต่อ tick, placeholder) จาก `WorldEventDef` ที่กรองด้วย
  `RequiredLocationTags` — แยกกลุ่ม `"Survival"` / `"Social"` ตาม design doc §3
- Event ที่ได้เข้า `Queue<WorldEventDef> _pending` — ใครก็ดีได้ด้วย `TryDequeue`

### [[LubanDataService.cs|GameStateProvider]] + [[LubanDataService.cs]] — `Marooned/Assets/Scripts/Systems/GameStateProvider.cs`
- `GameStateProvider`: ถือ `PlayerSurvivalState` ตัวเดียวเป็น single live instance
  (แนวคิดเดียวกับ SectStateProvider ของโปรเจคอ้างอิง) — starting state เป็น mock
  (อยู่ `beach`, มี `food_coconut` 1) เพื่อรอรอบ round-trip แรก
- `LubanDataService`: expose dictionary ของทุก def type (`CardDefs`, `LocationDefs`,
  `RecipeDefs`, `ClueDefs`, `IllnessDefs`, `WorldEventDefs`, `ChibiPartDefs`, `ChibiOutfitDefs`)
  แต่ **`LoadAll()` ทั้งหมดเป็น mock data ที่พิมพ์มือ** ลอกมาจาก CSV drafts — ยังไม่ได้อ่าน
  Luban JSON จริง (mark ไว้เป็น `TODO Lab A+1` ที่ `GameStateProvider.cs:46-47`)

### [[McpRequestHandlers.cs]] — `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`
10 handler ที่ implement `IAsyncRequestHandler<TReq,TRes>` (เวอร์ชัน UniTask) ทำหน้าที่
เป็นปลายทาง request ที่เดินทางมาจาก Bridge:

| Handler | เรียกใช้ system | หมายเหตุ |
| --- | --- | --- |
| `ExploreLocationHandler` | ExplorationSystem | `TriggeredEventId` ยังส่งค่าว่าง (`McpRequestHandlers.cs:28`) |
| `CraftCardHandler` | CraftingSystem | — |
| `AwaitNextEventHandler` | WorldEventSystem | **naive poll** — ไม่มี async wait, ไม่เคารพ TimeoutSeconds (`McpRequestHandlers.cs:50-58`) |
| `AccuseNpcHandler` | DeductionSystem | — |
| `GetGameStateHandler` | GameStateProvider | — |
| `GetVisibleNpcsHandler` | DeductionSystem | กรองผ่าน `GetObservableNpcsAt` เสมอ |
| `GetClueBoardHandler` | GameStateProvider | คืน `CollectedClueCardIds` |
| `MoveToLocationHandler` | LubanDataService | ตรวจ `ConnectedLocationIds` ก่อนย้าย |
| `UseCardHandler` | CardInventorySystem | apply `StatEffect` (Hunger/Thirst/Mood/Fatigue) |
| `CallMeetingHandler` | DeductionSystem | Lab A: แค่ snapshot คนที่มองเห็น (`McpRequestHandlers.cs:194-200`) |

---

## 3. การเชื่อมต่อ Unity ↔ McpBridge

### 3.1 ฝั่ง McpBridge — `McpBridge/Program.cs` (+ `McpBridge/McpBridge.csproj`)

- **.NET 8 console app** (`net8.0`), dependencies: `MessagePipe` 1.8.2,
  `MessagePipe.Interprocess` 1.8.2, `ModelContextProtocol` 2.2.0,
  `Microsoft.Extensions.Hosting` 10.0.11 (`McpBridge/McpBridge.csproj:13-18`)
- Logging ทั้งหมดบังคับลง **stderr** (`LogToStandardErrorThreshold = LogLevel.Trace`) เพราะ
  stdout ถูกใช้เป็นช่อง JSON-RPC framing ของ stdio MCP (`Program.cs:13-16`)
- TCP client: `AddTcpInterprocess("127.0.0.1", 3216, tcp => tcp.HostAsServer = false)`
  — **ต้องสตาร์ท Unity Play mode ก่อน** ไม่งั้น connection fail (`Program.cs:18-27`)
- ไม่มี manual registration ฝั่งนี้ — DI auto-resolve `IRemoteRequestHandler<TReq,TRes>`
  ให้ constructor ของ tool class เอง

### 3.2 MCP Tools (10 tools, 3 กลุ่มใน `McpBridge/Program.cs`)

| กลุ่ม | Tool | ทางไป Unity |
| --- | --- | --- |
| `SurvivalQueryTools` (read-only) | `get_game_state` | → `GetGameStateRequest/Response` |
| | `get_visible_npcs` | → `GetVisibleNpcsRequest/Response` (ผ่าน DeductionSystem เท่านั้น) |
| | `get_clue_board` | → `GetClueBoardRequest/Response` |
| `SurvivalActionTools` (mutating) | `explore_location` | → `ExploreLocationRequest/Response` |
| | `craft_card` | → `CraftCardRequest/Response` |
| | `move_to_location` | → `MoveToLocationRequest/Response` |
| | `use_card` | → `UseCardRequest/Response` |
| | `await_next_event` | → `AwaitNextEventRequest/Response` |
| `DeductionTools` | `call_meeting` | → `CallMeetingRequest/Response` |
| | `accuse_npc` | → `AccuseNpcRequest/Response` |

ทุก tool คืนค่าเป็น **ข้อความ human/LLM-readable** (ไม่ใช่ raw JSON) และ comment ใน
`Program.cs:39-43` ระบุชัด: `GetVisibleNpcs`/`GetClueBoard` เห็นแค่สิ่งที่
`DeductionSystem.GetObservableNpcsAt` สร้างให้ — true `NpcRole` **ไม่ข้ามเส้นนี้ by construction**

### 3.3 Sequence ของหนึ่ง request

```
AI: "explore_location('beach')"
  → MCP stdio → McpBridge SurvivalActionTools.ExploreLocation
  → IRemoteRequestHandler<ExploreLocationRequest,...>.InvokeAsync   [TCP 3216, MessagePack]
  → Unity: MessagePipe dispatch → ExploreLocationHandler.InvokeAsync (UniTask)
  → ExplorationSystem.Explore("beach") → weighted roll → TryAdd การ์ด
  → ExploreLocationResponse (MessagePack) → กลับ Bridge → ข้อความสรุป → AI
```

---

## 4. Shared types และ Data Flow

### 4.1 กลไก sync — `sync-shared.sh`

Canonical source คือ `Shared/*.cs` ที่ root เท่านั้น — script จะ copy ไปสองที่คือ
`Marooned/Assets/Scripts/Shared/` (สำหรับ Unity) และ `McpBridge/Shared/` (สำหรับ .NET)
ทุกครั้งที่รัน (โค้ดฉบับเต็มใน [ภาคผนวก](#sync-sharedsh)) — ไฟล์ copy ทั้งเจ็ดตอนนี้
**identical** กับต้นฉบับ (ตรวจแล้ว ณ 2026-09-05)

`McpBridge.csproj` **ไม่มี** `<Compile Include>` เพราะ SDK auto-include อยู่แล้ว
(ใส่ซ้ำจะ build fail `NETSDK1022` — comment ไว้ที่ `McpBridge/McpBridge.csproj:20-23`)

### 4.2 ชนิดข้อมูลหลัก (7 ไฟล์ใน `Shared/`)

| ไฟล์ | Type | บทบาท |
| --- | --- | --- |
| `Shared/CardDef.cs` | `CardDef`, enum `CardCategory` | นิยามการ์ด (StatEffect / ActionPenalty เป็น Dictionary) |
| `Shared/PlayerSurvivalState.cs` | `PlayerSurvivalState` [MessagePackObject] | Stat 4 ค่า + Inventory + ActiveConditionCardIds + CollectedClueCardIds + WrongAccusations + Avatar |
| `Shared/NpcState.cs` | `NpcState` + **`NpcObservableView`**, enums `NpcRole`/`NpcActivityState` | Ground truth (ห้าม serialize ออก) vs safe view |
| `Shared/LocationDef.cs` | `LocationDef`, `RecipeDef` | Map node (LootTable, Capacity, ConnectedLocationIds) + recipe |
| `Shared/ClueDef.cs` | `ClueDef`, `IllnessDef`, `WorldEventDef`, enum `ClueReliability` | เบาะแส / โรค / event |
| `Shared/ChibiAppearance.cs` | `ChibiAppearance` [MessagePackObject], `ChibiPartDef`, `ChibiOutfitDef`, enums `FacingDirection`/`ChibiAnimState` | ข้อมูล chibi paperdoll (slot→partId + ConditionOverlays แยก dict) |
| `Shared/GameMessages.cs` | 10 คู่ Request/Response + 3 broadcast message | สัญญาข้าม process ทั้งหมด |

### 4.3 Data flow และกฎ information hiding ([[Information-Hiding]])

```
Ground truth (Unity-internal only)          Safe view (ข้าม TCP ได้)
─────────────────────────────────           ─────────────────────────
NpcState (Role=Killer, KillCooldown,        NpcObservableView (Id, IsAlive,
  AllConditionCardIds, HiddenAgendaId)   →    Activity, VisibleConditionCardIds,
                                              Avatar)  ← DeductionSystem สร้างเสมอ
PlayerSurvivalState (full)               →   GetGameStateResponse (ส่งได้ทั้งก้อน)
```

กฎจาก GDD §5: `LinkedNpcIdHint` และ `NpcRole.Killer` ต้องไม่หลุดใน MCP response —
โค้ด enforce ด้วยการที่ `NpcDirectorSystem.Npcs` ไม่ถูก expose ให้ handler ใดเลย
(handler เรียก `DeductionSystem` เท่านั้น) — ดู comment ที่ `NpcState.cs:16-20`
และ `McpBridge/Program.cs:39-43`

### 4.4 สอง data model ขนานกัน (จุดที่ต้องเข้าใจ)

- **`cfg.game.CardDef`** (Luban-generated, `Marooned/Assets/Scripts/Data/Gen/game/CardDef.cs`)
  — flat columns ตาม CSV (`hungerDelta`, `explorePenalty`, ...) เป็น `readonly`
- **`Marooned.Shared.CardDef`** (`Shared/CardDef.cs`) — Dictionary-based (`StatEffect`, `ActionPenalty`)
- ตอนนี้ gameplay ใช้ฝั่ง `Marooned.Shared` ผ่าน mock ใน `LubanDataService` — เมื่อ wire
  Luban จริง (Lab A+1) ต้องเขียน mapper แปลง `cfg.game.*` → `Marooned.Shared.*`

---

## 5. UI Pattern — MVP Lite

Pattern จาก GDD §7 (สืบทอด MVP-Lite ของโปรเจคอ้างอิง): **View = MonoBehaviour (passive),
Presenter = plain C# (logic), in-process logic เรียกตรงด้วย direct method call — ไม่ใช้ pub/sub
สำหรับ UI ที่ต้องเห็นผลทันที** (บทเรียน Lab 13 ของโปรเจคอ้างอิง)

### `Marooned/Assets/Scripts/UI/Core/UIRoot.cs`
- enum `UIPanelType` { CardHand, MapExplore, MeetingVote, ClueBoard, ConditionOverlay }
- Registry แบบ `Dictionary<UIPanelType, Type>` ชี้ตรงไป View class (ไม่มี assembly scanning)
- `Initialize()` ตอนนี้ **ยังว่าง** — รอ instantiation ของ panel prefab ตอนมี GameplayScene (Additive Scene plan)

### Panels (ทั้งหมดยังเป็น skeleton — มี serialized field แต่ไม่มี logic)

| View (`Marooned/Assets/Scripts/UI/Views/`) | Presenter (`Marooned/Assets/Scripts/UI/Presenters/`) | หน้าที่ตาม design |
| --- | --- | --- |
| `CardHandView.cs` (13 บรรทัด) | `CardHandPresenter.cs` | มือการ์ด, drag เพื่อ craft/use |
| `MapExploreView.cs` | `MapExplorePresenter.cs` | แผนที่ 2D sandbox, node สำรวจได้/หมด |
| `ClueBoardView.cs` | `ClueBoardPresenter.cs` | Detective board ของ clue ที่เก็บมา |
| `MeetingVoteView.cs` | `MeetingVotePresenter.cs` | หน้าประชุม + ปุ่ม Accuse/Abstain |
| `ConditionOverlayView.cs` | — (ไม่มี presenter) | HUD icon ของ illness/injury ผู้เล่นเอง |

NPC-side คราบเลือด/ผ้าพันแผลไม่ได้ render ผ่าน ConditionOverlayView แต่ผ่าน
`ChibiAppearance.ConditionOverlays` บน [[ChibiAnimatedRenderer.cs]] โดยตรง
(ดู `Marooned/Assets/Scripts/UI/Views/ConditionOverlayView.cs:5-7`)

### [[ChibiAnimatedRenderer.cs]] — `Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs`
- 1 `SpriteRenderer` ต่อ slot (`Parts` + `ConditionOverlays`), sort ด้วย `ChibiPartDef.DrawOrder`
  (layer stack: leg_back(0) → body(10) → ... → accessory(60))
- `Update()` สลับ frame ด้วย key `$"{Facing}_{AnimState}"` (เช่น `Down_Walk`) จาก
  `ChibiPartDef.FramesByAnimKey` — classic sprite-swap ไม่ใช้ bone/skeleton
- โหลด sprite ผ่าน `Resources.Load` พร้อม cache (`ChibiAnimatedRenderer.cs:96-109`)
- ยัง **ไม่มีใครเรียก `Init()`** — รอ Lab B เชื่อมกับ ChibiPartDef table และ movement controller

---

## 6. Data Pipeline — Luban

### 6.1 Pipeline ปัจจุบัน

```
DataTables/Data/*.csv  (7 ตาราง draft, มือเขียนได้)
        │   DataTables/gen.sh (bash) หรือ gen.bat (Windows)
        │   dotnet Tools/Luban/Luban.dll -t client -c cs-simple-json -d json
        │   --conf DataTables/luban.conf
        ├────────────► Marooned/Assets/Scripts/Data/Gen/   (C# code, namespace cfg, 764 บรรทัด)
        └────────────► Marooned/Assets/Resources/DataTables/*.json (6 ไฟล์ data)
```

- `DataTables/luban.conf`: schema จาก `DataTables/Defines/event.xml`, dataDir `Data`,
  target `client`, topModule `cfg`
- `DataTables/Gen/Tables.cs` คือ entry table ที่โหลด 6 ตาราง: `TbCardDef`, `TbLocationDef`,
  `TbRecipeDef`, `TbClueDef`, `TbIllnessDef`, `TbWorldEventDef`
- JSON output อยู่ใน `Assets/Resources/DataTables/` พร้อม `.meta` — แสดงว่า **generate ผ่านแล้วจริง**

### 6.2 ตารางที่มี (DataTables/Data/)

| CSV | เนื้อหา |
| --- | --- |
| `CardDef.csv` (8 บรรทัด) | มะพร้าว, น้ำขวด, ปลาดิบ, ปลาย่าง, illness 2 ชนิด (delta ต่อ stat + explore/craft penalty) |
| `LocationDef.csv` (6) | beach/jungle_edge/deep_jungle/cave_entrance พร้อม loot weight + capacity |
| `RecipeDef.csv` (4) | `recipe_cook_fish` — ปลาดิบ → ปลาย่าง (ต้องมี `tool_campfire`) |
| `ClueDef.csv` (6) | 4 เบาะแส — Strong/Weak/RedHerring, บางชนิด `visibleToBystanders=false` |
| `IllnessDef.csv` (5) | 2 โรค (มองไม่เห็น) + `injury_cut` (มองเห็น, รักษาด้วย bandage) |
| `WorldEventDef.csv` (6) | Survival 2 / Social 2 พร้อม weight |
| `ChibiPartDef.csv` (12) | part ต่อ slot + pivot — frame data จะใช้ JSON sidecar แยก (หมายเหตุในไฟล์) — **ยังไม่ผ่าน Luban** (ไม่มี `TbChibiPartDef` ใน Tables.cs) |

### 6.3 จุดตัด pipeline ที่ยังขาด

`LubanDataService.LoadAll()` ใน `Marooned/Assets/Scripts/Systems/GameStateProvider.cs:48-156`
**ไม่ได้อ่าน** ทั้ง `cfg.Tables` และ JSON — เป็น mock dictionary ที่พิมพ์มือทั้งหมด
(สาเหตุ: ยังไม่ต้องการ dependency ระหว่าง round-trip test แรก — comment ที่ `GameStateProvider.cs:38-43`)
นี่คือ TODO ใหญ่ที่สุดตัวหนึ่งของ Lab A+1

---

## 7. TODO / Stub / ยังไม่เสร็จ (สถานะ Lab A)

รวมจุดที่โค้ดระบุเอง + ที่สำรวจพบ:

### ระบบเกม
1. **ไม่มี game loop ที่เรียก `Tick()`** — `SurvivalStatSystem.Tick`, `NpcDirectorSystem.Tick`,
   `WorldEventSystem.Tick` ยังไม่มี driver (ไม่มี `IInitializable`/MonoBehaviour จับเวลา) —
   เกมจึง "หยุดนิ่ง" ถึงแม้จะเชื่อม MCP ได้
2. `NpcDirectorSystem.TickBehavior` เป็น **placeholder ว่าง** — รอ daily-schedule table
   (`Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs:65-69`)
3. `NpcDirectorSystem.SpawnClues` hardcode clue id — รอ ClueDef-driven weighted roll
   (`NpcDirectorSystem.cs:98-102`); และยังไม่มีการเก็บ clue เข้า `PlayerSurvivalState.CollectedClueCardIds`
4. `AwaitNextEvent` เป็น naive poll ไม่มี async wait และไม่เคารพ `TimeoutSeconds` —
   รอแก้ Lab E (`Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs:50-58`)
5. `ExploreLocationResponse.TriggeredEventId` ยังส่งค่าว่าง (`McpRequestHandlers.cs:28`)
6. `WorldEventSystem.Tick` ใช้ fixed 1% ต่อ tick — รอ per-tag cooldown
7. `CallMeeting` แค่ snapshot คนที่มองเห็น — ไม่มี pause/summon จริง (`McpRequestHandlers.cs:194-197`)
8. `PlayerSurvivalState.Fatigue` เริ่ม 0 และถูก "ลบด้วยค่าลบ" ให้เพิ่มขึ้น
   (`SurvivalStatSystem.cs:35`) — แต่ `UseCardHandler` clamp 0–100 แบบเดียวกับ stat อื่น
   (สม่ำเสมอกันพอ แต่ควรเช็คเงื่อนไข die จาก fatigue ไม่มีในระบบ)
9. ไม่มีการ heal/cure condition card (IllnessDef.CureCardId ยังไม่มีใครใช้)
10. Mock starting state ใน `GameStateProvider` — รอ save/new-game logic

### Data pipeline
11. `LubanDataService.LoadAll()` = mock ทั้งหมด — ต้องแทนด้วย Luban loader จริง
    (`TODO Lab A+1` ที่ `GameStateProvider.cs:46-47`) พร้อม mapper `cfg.game.*` → `Marooned.Shared.*`
12. `ChibiPartDef.csv` ยังไม่อยู่ใน Luban pipeline (ไม่มี TbChibiPartDef); frame data รอ JSON sidecar

### UI / Scene
13. ทุก Presenter ว่างเปล่า (มีแค่ comment อธิบาย), ทุก View ไม่มี render logic
14. `UIRoot.Initialize()` ว่าง — รอ GameplayScene/prefab
15. `ChibiAnimatedRenderer.Init()` ยังไม่มีใครเรียก; `LubanPartLookup` ยังไม่ถูก wire
16. **ไม่มี scene, prefab, หรือ art** ในโปรเจคเลย (ตาม status "code scaffolding only")

### ระบบอื่น
17. `McpBridge.csproj` ใช้ `RootNamespace` คือ `Xianxia.Sect.Bridge` — เศษจากโปรเจคอ้างอิง
    (ไม่กระทบ build เพราะ top-level statements + namespace ชัดเจนในไฟล์ แต่ควรเปลี่ยน)
18. `GameLifetimeScope` API signature ของ MessagePipe.Interprocess **ยังต้อง verify ใน Unity จริง**
19. ไม่มี `await_next_world_event` แบบ async-signal, ไม่มี observe/talk_to_npc tool
    (ตาราง GDD §6 มี แต่ Bridge ยังไม่ทำ)

### ขั้นถัดไปตาม Roadmap (GDD §8)
Lab A (ปัจจุบัน) → Lab B Illness/Injury + Chibi overlay → Lab C NPC skeleton →
Lab D Social Deduction core → Lab E MCP full tool set + AI VTuber playtest →
Lab F Additive Scene

---

## ภาคผนวก: โค้ดต้นฉบับครบถ้วน (per-file snippets)

> ทุกไฟล์ที่ "เขียนมือ" ถูกฝังเนื้อหาต้นฉบับครบถ้วนด้านล่าง — ไฟล์ auto-generated
> (`Marooned/Assets/Scripts/Data/Gen/**` 764 บรรทัด) และไฟล์ package/library
> (`Marooned/Assets/Plugins/MessagePipe.Interprocess/**`) ไม่ฝัง แต่อ้าง path ไว้
> ไฟล์ `Shared/` ฝังจากต้นฉบับ canonical ที่ root (copy ใน Unity/McpBridge เหมือนกันทุกบรรทัด)

### Marooned/Assets/Scripts/Core/GameLifetimeScope.cs (90 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs`

```csharp
using MessagePipe;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.UI.Core;
using VContainer;
using VContainer.Unity;

namespace Marooned.Core
{
    /// <summary>
    /// Composition root. Same shape as the reference project's GameLifetimeScope:
    /// wires MessagePipe (in-process bus) + MessagePipe.Interprocess (TCP, Unity as
    /// host) + gameplay subsystems + the MCP-facing state provider.
    ///
    /// Fixed against MessagePipe.Interprocess's real Unity/VContainer API (the
    /// previous draft used made-up method names). Per the official README's
    /// Unity section:
    ///   var options = builder.RegisterMessagePipe();
    ///   var messagePipeBuilder = builder.ToMessagePipeBuilder();
    ///   var interprocessOptions = messagePipeBuilder.AddTcpInterprocess(host, port, cfg);
    ///   builder.RegisterAsyncRequestHandler&lt;TReq,TRes,THandler&gt;(options); // exposes the handler; TCP dispatch happens because HostAsServer=true
    ///
    /// Unity has no open-generics/auto-registration (IL2CPP), so every
    /// request/handler pair must be registered manually here — unlike McpBridge's
    /// plain .NET side, which gets IRemoteRequestHandler&lt;TReq,TRes&gt; for free
    /// once AddTcpInterprocess is configured as a client (HostAsServer=false).
    ///
    /// STILL VERIFY: exact overload names/order can drift between package
    /// versions. If `dotnet`/Unity reports a different signature, trust the
    /// compiler/IntelliSense over this comment and adjust — this is written
    /// against MessagePipe.Interprocess 1.8.2's public README sample, not a
    /// tested build.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [UnityEngine.SerializeField] private string interprocessHost = "127.0.0.1";
        [UnityEngine.SerializeField] private int interprocessPort = 3216; // different port than reference project's 3215

        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();

            // Enables the MessagePipe Diagnostics window + GlobalMessagePipe helpers.
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

            var messagePipeBuilder = builder.ToMessagePipeBuilder();
            var interprocessOptions = messagePipeBuilder.AddTcpInterprocess(interprocessHost, interprocessPort, tcp =>
            {
                tcp.HostAsServer = true;
            });

            // บังคับให้ VContainer สร้าง TcpWorker จริง ไม่ใช่แค่ register ไว้เฉยๆ
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<MessagePipe.Interprocess.Workers.TcpWorker>();
            });

            // --- Gameplay subsystems ---
            builder.Register<SurvivalStatSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<CardInventorySystem>(Lifetime.Singleton).AsSelf();
            builder.Register<CraftingSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<ExplorationSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<NpcDirectorSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<DeductionSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<WorldEventSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<LubanDataService>(Lifetime.Singleton).AsSelf();

            // --- State provider consumed by MCP query handlers ---
            builder.Register<GameStateProvider>(Lifetime.Singleton).AsSelf();

            // --- MCP-facing request handlers (Bridge -> Unity, request/response) ---
            // Unity is the SERVER (HostAsServer = true above): registering the
            // async handler here is what makes it network-callable, no separate
            // "expose over TCP" call needed on the server side per the README.
            builder.RegisterAsyncRequestHandler<ExploreLocationRequest, ExploreLocationResponse, ExploreLocationHandler>(options);
            builder.RegisterAsyncRequestHandler<CraftCardRequest, CraftCardResponse, CraftCardHandler>(options);
            builder.RegisterAsyncRequestHandler<AwaitNextEventRequest, AwaitNextEventResponse, AwaitNextEventHandler>(options);
            builder.RegisterAsyncRequestHandler<AccuseNpcRequest, AccuseNpcResponse, AccuseNpcHandler>(options);
            builder.RegisterAsyncRequestHandler<GetGameStateRequest, GetGameStateResponse, GetGameStateHandler>(options);
            builder.RegisterAsyncRequestHandler<GetVisibleNpcsRequest, GetVisibleNpcsResponse, GetVisibleNpcsHandler>(options);
            builder.RegisterAsyncRequestHandler<GetClueBoardRequest, GetClueBoardResponse, GetClueBoardHandler>(options);
            builder.RegisterAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse, MoveToLocationHandler>(options);
            builder.RegisterAsyncRequestHandler<UseCardRequest, UseCardResponse, UseCardHandler>(options);
            builder.RegisterAsyncRequestHandler<CallMeetingRequest, CallMeetingResponse, CallMeetingHandler>(options);

            // --- UI root ---
            builder.RegisterEntryPoint<UIRoot>();
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs (73 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/SurvivalStatSystem.cs`

```csharp
using System;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Ticks Hunger/Thirst/Mood/Fatigue over time and rolls for illness when a stat
    /// stays critical too long. Mirrors the reference project's TickGathering /
    /// TickCrafting fractional-accumulator pattern.
    /// </summary>
    public class SurvivalStatSystem
    {
        private readonly IPublisher<SurvivalStatChangedMessage> _statPublisher;
        private readonly IPublisher<ConditionCardAppliedMessage> _conditionPublisher;
        private readonly PlayerSurvivalState _state;

        private float _criticalHungerSeconds;
        private float _criticalThirstSeconds;

        public SurvivalStatSystem(
            IPublisher<SurvivalStatChangedMessage> statPublisher,
            IPublisher<ConditionCardAppliedMessage> conditionPublisher,
            GameStateProvider stateProvider)
        {
            _statPublisher = statPublisher;
            _conditionPublisher = conditionPublisher;
            _state = stateProvider.Player;
        }

        public void Tick(float deltaSeconds)
        {
            ApplyDrain("Hunger", ref _state.Hunger, 0.15f * deltaSeconds);
            ApplyDrain("Thirst", ref _state.Thirst, 0.25f * deltaSeconds);
            ApplyDrain("Fatigue", ref _state.Fatigue, -0.10f * deltaSeconds); // fatigue rises (negative "drain" = increase)

            if (_state.Hunger <= 15f)
            {
                _criticalHungerSeconds += deltaSeconds;
                if (_criticalHungerSeconds > 120f && !_state.ActiveConditionCardIds.Contains("illness_malnutrition"))
                    ApplyCondition("illness_malnutrition");
            }
            else _criticalHungerSeconds = 0f;

            if (_state.Thirst <= 15f)
            {
                _criticalThirstSeconds += deltaSeconds;
                if (_criticalThirstSeconds > 90f && !_state.ActiveConditionCardIds.Contains("illness_dehydration"))
                    ApplyCondition("illness_dehydration");
            }
            else _criticalThirstSeconds = 0f;

            if (_state.Hunger <= 0f && _state.Thirst <= 0f)
                _state.IsAlive = false;
        }

        private void ApplyDrain(string key, ref float value, float amount)
        {
            var before = value;
            value = Math.Clamp(value - amount, 0f, 100f);
            if (!Mathf_Approximately(before, value))
                _statPublisher.Publish(new SurvivalStatChangedMessage { StatKey = key, NewValue = value, Delta = value - before });
        }

        private static bool Mathf_Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

        private void ApplyCondition(string conditionCardId)
        {
            _state.ActiveConditionCardIds.Add(conditionCardId);
            _conditionPublisher.Publish(new ConditionCardAppliedMessage { TargetEntityId = "player", ConditionCardId = conditionCardId });
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/CardInventorySystem.cs (74 บรรทัด — มี CraftingSystem อยู่ด้วย)
**Path:** `Marooned/Assets/Scripts/Systems/CardInventorySystem.cs`

```csharp
using System.Collections.Generic;
using Marooned.Shared;

namespace Marooned.Systems
{
    public class CardInventorySystem
    {
        private readonly PlayerSurvivalState _state;
        private readonly Dictionary<string, CardDef> _cardDefs; // loaded from Luban-generated JSON at boot

        public CardInventorySystem(GameStateProvider stateProvider, LubanDataService dataService)
        {
            _state = stateProvider.Player;
            _cardDefs = dataService.CardDefs;
        }

        public bool TryAdd(string cardId, int count = 1)
        {
            if (!_cardDefs.TryGetValue(cardId, out var def)) return false;
            _state.Inventory.TryGetValue(cardId, out var current);
            var next = current + count;
            if (def.StackLimit > 0) next = System.Math.Min(next, def.StackLimit);
            _state.Inventory[cardId] = next;
            return true;
        }

        public bool TryConsume(string cardId, int count = 1)
        {
            if (!_state.Inventory.TryGetValue(cardId, out var current) || current < count) return false;
            _state.Inventory[cardId] = current - count;
            if (_state.Inventory[cardId] <= 0) _state.Inventory.Remove(cardId);
            return true;
        }

        public bool HasAtLeast(string cardId, int count) =>
            _state.Inventory.TryGetValue(cardId, out var current) && current >= count;
    }

    public class CraftingSystem
    {
        private readonly CardInventorySystem _inventory;
        private readonly PlayerSurvivalState _state;
        private readonly Dictionary<string, RecipeDef> _recipes;

        public CraftingSystem(CardInventorySystem inventory, GameStateProvider stateProvider, LubanDataService dataService)
        {
            _inventory = inventory;
            _state = stateProvider.Player;
            _recipes = dataService.RecipeDefs;
        }

        public (bool success, string failureReason, string outputCardId) TryCraft(string recipeId)
        {
            if (!_recipes.TryGetValue(recipeId, out var recipe))
                return (false, "unknown_recipe", null);

            if (recipe.RequiredLocationIds is { Count: > 0 } && !recipe.RequiredLocationIds.Contains(_state.CurrentLocationId))
                return (false, "wrong_location", null);

            if (!string.IsNullOrEmpty(recipe.RequiredToolCardId) && !_inventory.HasAtLeast(recipe.RequiredToolCardId, 1))
                return (false, "missing_tool", null);

            foreach (var kv in recipe.Inputs)
                if (!_inventory.HasAtLeast(kv.Key, kv.Value))
                    return (false, "missing_ingredients", null);

            foreach (var kv in recipe.Inputs)
                _inventory.TryConsume(kv.Key, kv.Value);

            _inventory.TryAdd(recipe.OutputCardId, recipe.OutputCount);
            return (true, null, recipe.OutputCardId);
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/ExplorationSystem.cs (57 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/ExplorationSystem.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>Tracks how depleted each LocationDef's loot table currently is.</summary>
    public class LocationRuntimeState
    {
        public Dictionary<string, int> RemainingWeight = new();
    }

    public class ExplorationSystem
    {
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly Dictionary<string, LocationRuntimeState> _runtime = new();
        private readonly CardInventorySystem _inventory;
        private readonly PlayerSurvivalState _player;
        private readonly Random _rng = new();

        public ExplorationSystem(LubanDataService dataService, CardInventorySystem inventory, GameStateProvider stateProvider)
        {
            _locations = dataService.LocationDefs;
            _inventory = inventory;
            _player = stateProvider.Player;

            foreach (var loc in _locations.Values)
                _runtime[loc.Id] = new LocationRuntimeState { RemainingWeight = new Dictionary<string, int>(loc.LootTable) };
        }

        public (bool success, List<string> foundCardIds) Explore(string locationId)
        {
            if (!_locations.ContainsKey(locationId)) return (false, new List<string>());

            var runtime = _runtime[locationId];
            var pool = runtime.RemainingWeight.Where(kv => kv.Value > 0).ToList();
            if (pool.Count == 0) return (true, new List<string>()); // node depleted, exploring is still a valid (empty-handed) action

            var totalWeight = pool.Sum(kv => kv.Value);
            var roll = _rng.Next(0, totalWeight);
            string picked = pool[0].Key;
            var cumulative = 0;
            foreach (var kv in pool)
            {
                cumulative += kv.Value;
                if (roll < cumulative) { picked = kv.Key; break; }
            }

            runtime.RemainingWeight[picked] -= 1;
            _inventory.TryAdd(picked, 1);
            _player.CurrentLocationId = locationId;

            return (true, new List<string> { picked });
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs (109 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Owns every NpcState (ground truth, including hidden Role). Moves NPCs between
    /// locations, lets the Killer NPC attempt eliminations when unwitnessed, and
    /// spawns Clue cards on the resulting body / nearby NPCs.
    ///
    /// IMPORTANT: never hand out NpcState directly to MCP query handlers — always go
    /// through DeductionSystem.BuildObservableView (see DeductionVisibilityRules).
    /// </summary>
    public class NpcDirectorSystem
    {
        private readonly Dictionary<string, NpcState> _npcs = new();
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly IPublisher<NpcEliminatedMessage> _eliminatedPublisher;
        private readonly Random _rng = new();

        public IReadOnlyDictionary<string, NpcState> Npcs => _npcs;

        public NpcDirectorSystem(LubanDataService dataService, IPublisher<NpcEliminatedMessage> eliminatedPublisher)
        {
            _locations = dataService.LocationDefs;
            _eliminatedPublisher = eliminatedPublisher;
        }

        /// <summary>
        /// Sets up a round: picks killerCount out of npcIds to be Killer, rest Innocent.
        /// Ratio guidance (confirmed): ~1 killer per 4-8 innocents, Among Us style
        /// (e.g. 5 NPC -> 1 killer, 10 NPC -> 2 killers).
        /// </summary>
        public void SetupRound(IEnumerable<string> npcIds, int killerCount)
        {
            _npcs.Clear();
            var ids = npcIds.ToList();
            var killerIds = ids.OrderBy(_ => _rng.Next()).Take(killerCount).ToHashSet();

            foreach (var id in ids)
            {
                _npcs[id] = new NpcState
                {
                    Id = id,
                    Role = killerIds.Contains(id) ? NpcRole.Killer : NpcRole.Innocent,
                    IsAlive = true,
                    Activity = NpcActivityState.Idle
                };
            }
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcs.Values.Where(n => n.IsAlive))
            {
                TickBehavior(npc, deltaSeconds);
                if (npc.Role == NpcRole.Killer)
                    TryAttemptElimination(npc, deltaSeconds);
            }
        }

        private void TickBehavior(NpcState npc, float deltaSeconds)
        {
            // Placeholder schedule: random idle/gather/rest/travel switching.
            // Replace with a proper daily-schedule table (Luban) in a later lab.
        }

        private void TryAttemptElimination(NpcState killer, float deltaSeconds)
        {
            killer.KillCooldownRemaining = Math.Max(0, killer.KillCooldownRemaining - deltaSeconds);
            if (killer.KillCooldownRemaining > 0) return;

            var sameLocation = _npcs.Values
                .Where(n => n.IsAlive && n.Id != killer.Id && n.CurrentLocationId == killer.CurrentLocationId)
                .ToList();

            // "No witness" rule: only killer + exactly one victim present, nobody else.
            if (sameLocation.Count != 1) return;

            var victim = sameLocation[0];
            if (_rng.NextDouble() > 0.15) return; // small per-tick chance, tune later

            victim.IsAlive = false;
            killer.KillCooldownRemaining = 180f; // seconds

            var clueIds = SpawnClues(killer, victim);
            _eliminatedPublisher.Publish(new NpcEliminatedMessage
            {
                VictimNpcId = victim.Id,
                LocationId = victim.CurrentLocationId,
                SpawnedClueCardIds = clueIds
            });
        }

        private List<string> SpawnClues(NpcState killer, NpcState victim)
        {
            // Simple v1: always drop one visible clue on the victim's location, and a
            // weaker chance of a red herring clue somewhere else. Replace with
            // ClueDef-driven weighted rolls once DataTables/ClueDef.csv is populated.
            var clues = new List<string> { "clue_blood_stain" };
            if (_rng.NextDouble() < 0.3) clues.Add("clue_scratch_mark");
            victim.AllConditionCardIds.AddRange(clues);
            return clues;
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/DeductionSystem.cs (75 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/DeductionSystem.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// The one place allowed to translate ground-truth NpcState into what a player
    /// (or the AI agent controlling the player through MCP) is actually allowed to
    /// know. Also resolves accusations. Max wrong-accusation cap enforces the loss
    /// condition described in the design doc.
    /// </summary>
    public class DeductionSystem
    {
        private const int MaxWrongAccusations = 3;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly PlayerSurvivalState _player;
        private readonly Dictionary<string, IllnessDef> _illnessDefs;

        public DeductionSystem(NpcDirectorSystem npcDirector, GameStateProvider stateProvider, LubanDataService dataService)
        {
            _npcDirector = npcDirector;
            _player = stateProvider.Player;
            _illnessDefs = dataService.IllnessDefs;
        }

        /// <summary>Build the safe, MCP-facing view of every NPC the player can currently see (same location).</summary>
        public List<NpcObservableView> GetObservableNpcsAt(string locationId)
        {
            var result = new List<NpcObservableView>();
            foreach (var npc in _npcDirector.Npcs.Values.Where(n => n.CurrentLocationId == locationId))
            {
                result.Add(new NpcObservableView
                {
                    Id = npc.Id,
                    IsAlive = npc.IsAlive,
                    CurrentLocationId = npc.CurrentLocationId,
                    Activity = npc.Activity,
                    VisibleConditionCardIds = FilterVisible(npc.AllConditionCardIds),
                    Avatar = npc.Avatar
                });
            }
            return result;
        }

        private List<string> FilterVisible(List<string> conditionCardIds)
        {
            // Clues (blood stain, scratch mark) default to visible; illness cards check IllnessDef.Visible.
            return conditionCardIds
                .Where(id => !_illnessDefs.TryGetValue(id, out var def) || def.Visible)
                .ToList();
        }

        public (bool wasCorrect, bool win, bool loss, string resultText) Accuse(string targetNpcId)
        {
            if (!_npcDirector.Npcs.TryGetValue(targetNpcId, out var target))
                return (false, false, false, "unknown_npc");

            var correct = target.Role == NpcRole.Killer;

            if (correct)
            {
                target.IsAlive = false; // removed from play
                var anyKillersLeft = _npcDirector.Npcs.Values.Any(n => n.IsAlive && n.Role == NpcRole.Killer);
                return (true, !anyKillersLeft, false, anyKillersLeft ? "correct_but_more_killers_remain" : "all_killers_caught_win");
            }

            _player.WrongAccusations++;
            _player.Mood = System.Math.Max(0, _player.Mood - 15f);
            var lost = _player.WrongAccusations >= MaxWrongAccusations;
            return (false, false, lost, lost ? "too_many_wrong_accusations_loss" : "wrong_accusation");
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/WorldEventSystem.cs (47 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/WorldEventSystem.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// Weighted random world events, same pattern as the reference project
    /// (auto-pause + Request-Response await, not raw pub/sub, per the Lab 6 fix).
    /// Events are tagged Survival or Social so the design doc's two-group split is
    /// enforced in data, not just convention.
    /// </summary>
    public class WorldEventSystem
    {
        private readonly List<WorldEventDef> _events;
        private readonly Random _rng = new();
        private readonly Queue<WorldEventDef> _pending = new();

        public WorldEventSystem(LubanDataService dataService)
        {
            _events = dataService.WorldEventDefs.Values.ToList();
        }

        public void Tick(float deltaSeconds, string currentLocationTag)
        {
            // Simple fixed-interval roll; replace with per-tag cooldowns later.
            if (_rng.NextDouble() > 0.01) return; // ~1% chance per tick, tune later

            var eligible = _events.Where(e =>
                e.RequiredLocationTags == null || e.RequiredLocationTags.Count == 0 ||
                e.RequiredLocationTags.Contains(currentLocationTag)).ToList();
            if (eligible.Count == 0) return;

            var totalWeight = eligible.Sum(e => e.Weight);
            var roll = _rng.Next(0, totalWeight);
            var cumulative = 0;
            foreach (var e in eligible)
            {
                cumulative += e.Weight;
                if (roll < cumulative) { _pending.Enqueue(e); break; }
            }
        }

        public bool TryDequeue(out WorldEventDef evt) => _pending.TryDequeue(out evt);
    }
}
```

### Marooned/Assets/Scripts/Systems/GameStateProvider.cs (158 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/GameStateProvider.cs`

```csharp
using System.Collections.Generic;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>Single live instance, same idea as the reference project's SectStateProvider (Lab 7) — no re-creating state every query.</summary>
    public class GameStateProvider
    {
        public PlayerSurvivalState Player { get; } = new()
        {
            // Mock starting state so GetGameState has something to show immediately
            // in the first round-trip test. Move this into real save/new-game logic later.
            CurrentLocationId = "beach",
            Inventory = new Dictionary<string, int> { ["food_coconut"] = 1 },
        };
    }

    /// <summary>
    /// Loads Luban-generated JSON (post Excel->JSON->C# pipeline) into plain
    /// dictionaries at boot. LoadAll() below is TEMPORARY MOCK DATA hand-copied
    /// from the DataTables/*.csv drafts, just enough to unblock the first MCP
    /// round-trip test. Replace with real Luban-generated loader calls once the
    /// Excel pipeline (Tools/Luban/gen.bat) is wired up — search this file for
    /// "TODO Lab A+1" when you get there.
    /// </summary>
    public class LubanDataService
    {
        public Dictionary<string, CardDef> CardDefs { get; private set; } = new();
        public Dictionary<string, LocationDef> LocationDefs { get; private set; } = new();
        public Dictionary<string, RecipeDef> RecipeDefs { get; private set; } = new();
        public Dictionary<string, ClueDef> ClueDefs { get; private set; } = new();
        public Dictionary<string, IllnessDef> IllnessDefs { get; private set; } = new();
        public Dictionary<string, WorldEventDef> WorldEventDefs { get; private set; } = new();
        public Dictionary<string, ChibiPartDef> ChibiPartDefs { get; private set; } = new();
        public Dictionary<string, ChibiOutfitDef> ChibiOutfitDefs { get; private set; } = new();

        public LubanDataService()
        {
            // Called from the constructor (not a separate lifecycle hook) so mock
            // data is guaranteed loaded the moment VContainer resolves this
            // singleton — no dependency on IInitializable/EntryPoint ordering
            // while that part of the DI wiring is still being worked out.
            LoadAll();
        }

        // TODO Lab A+1: replace this whole method body with real Luban-generated
        // loader calls, e.g. CardDefs = LubanTables.CardDefTable.ToDictionary(x => x.Id);
        public void LoadAll()
        {
            CardDefs = new Dictionary<string, CardDef>
            {
                ["food_coconut"] = new CardDef
                {
                    Id = "food_coconut", Category = CardCategory.Resource, DisplayName = "มะพร้าว",
                    SpritePath = "Sprites/Cards/food_coconut", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 15f },
                },
                ["water_bottle"] = new CardDef
                {
                    Id = "water_bottle", Category = CardCategory.Resource, DisplayName = "น้ำขวด",
                    SpritePath = "Sprites/Cards/water_bottle", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Thirst"] = 20f },
                },
                ["raw_fish"] = new CardDef
                {
                    Id = "raw_fish", Category = CardCategory.Resource, DisplayName = "ปลาดิบ",
                    SpritePath = "Sprites/Cards/raw_fish", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 10f, ["Mood"] = -5f },
                },
                ["cooked_fish"] = new CardDef
                {
                    Id = "cooked_fish", Category = CardCategory.Craftable, DisplayName = "ปลาย่าง",
                    SpritePath = "Sprites/Cards/cooked_fish", StackLimit = 10,
                    StatEffect = new Dictionary<string, float> { ["Hunger"] = 25f, ["Mood"] = 5f },
                },
                ["illness_malnutrition"] = new CardDef
                {
                    Id = "illness_malnutrition", Category = CardCategory.Illness, DisplayName = "ภาวะขาดสารอาหาร",
                    SpritePath = "Sprites/Cards/illness_malnutrition", StackLimit = 1,
                    ActionPenalty = new Dictionary<string, float> { ["Explore"] = -0.3f, ["Craft"] = -0.2f },
                },
                ["illness_dehydration"] = new CardDef
                {
                    Id = "illness_dehydration", Category = CardCategory.Illness, DisplayName = "ภาวะขาดน้ำ",
                    SpritePath = "Sprites/Cards/illness_dehydration", StackLimit = 1,
                    ActionPenalty = new Dictionary<string, float> { ["Explore"] = -0.4f },
                },
            };

            LocationDefs = new Dictionary<string, LocationDef>
            {
                ["beach"] = new LocationDef
                {
                    Id = "beach", DisplayName = "ชายหาด", WorldX = 0, WorldY = 0,
                    ConnectedLocationIds = new List<string> { "jungle_edge", "cave_entrance" },
                    LootTable = new Dictionary<string, int> { ["food_coconut"] = 5, ["water_bottle"] = 2, ["raw_fish"] = 3 },
                    Capacity = 6,
                },
                ["jungle_edge"] = new LocationDef
                {
                    Id = "jungle_edge", DisplayName = "ชายป่า", WorldX = 10, WorldY = 5,
                    ConnectedLocationIds = new List<string> { "beach", "deep_jungle" },
                    LootTable = new Dictionary<string, int> { ["food_coconut"] = 3 },
                    Capacity = 4,
                },
                ["deep_jungle"] = new LocationDef
                {
                    Id = "deep_jungle", DisplayName = "ป่าลึก", WorldX = 20, WorldY = 10,
                    ConnectedLocationIds = new List<string> { "jungle_edge" },
                    LootTable = new Dictionary<string, int> { ["raw_fish"] = 1 },
                    Capacity = 3,
                },
                ["cave_entrance"] = new LocationDef
                {
                    Id = "cave_entrance", DisplayName = "ปากถ้ำ", WorldX = -5, WorldY = 8,
                    ConnectedLocationIds = new List<string> { "beach" },
                    LootTable = new Dictionary<string, int> { ["water_bottle"] = 4 },
                    Capacity = 3,
                },
            };

            RecipeDefs = new Dictionary<string, RecipeDef>
            {
                ["recipe_cook_fish"] = new RecipeDef
                {
                    Id = "recipe_cook_fish", OutputCardId = "cooked_fish", OutputCount = 1,
                    Inputs = new Dictionary<string, int> { ["raw_fish"] = 1 },
                },
            };

            ClueDefs = new Dictionary<string, ClueDef>
            {
                ["clue_blood_stain"] = new ClueDef
                {
                    Id = "clue_blood_stain", DisplayName = "คราบเลือด", SpritePath = "Sprites/Clues/blood_stain",
                    Reliability = ClueReliability.Strong, VisibleToBystanders = true,
                },
                ["clue_scratch_mark"] = new ClueDef
                {
                    Id = "clue_scratch_mark", DisplayName = "รอยขีดข่วน", SpritePath = "Sprites/Clues/scratch_mark",
                    Reliability = ClueReliability.Weak, VisibleToBystanders = true,
                },
            };

            IllnessDefs = new Dictionary<string, IllnessDef>
            {
                ["illness_malnutrition"] = new IllnessDef { Id = "illness_malnutrition", DisplayName = "ภาวะขาดสารอาหาร", Visible = false, CureCardId = "cooked_fish", SeverityGrowthPerHour = 0.5f },
                ["illness_dehydration"] = new IllnessDef { Id = "illness_dehydration", DisplayName = "ภาวะขาดน้ำ", Visible = false, CureCardId = "water_bottle", SeverityGrowthPerHour = 0.8f },
            };

            WorldEventDefs = new Dictionary<string, WorldEventDef>
            {
                ["event_storm"] = new WorldEventDef { Id = "event_storm", Group = "Survival", Weight = 10, DisplayText = "พายุเข้า ทำให้ Fatigue ลดเร็วขึ้นชั่วคราว" },
                ["event_npc_argument"] = new WorldEventDef { Id = "event_npc_argument", Group = "Social", Weight = 6, DisplayText = "NPC สองคนทะเลาะกัน เผยข้อมูลความสัมพันธ์" },
            };
        }
    }
}
```

### Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs (202 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    // IMPORTANT (Unity-specific): MessagePipe's Unity build replaces every
    // ValueTask<T> in the async interfaces with UniTask<T> (requires the
    // UniTask package, already in the openupm add list in README). So
    // IAsyncRequestHandler<TReq,TRes> here means:
    //   UniTask<TRes> InvokeAsync(TReq request, CancellationToken ct = default)
    // NOT System.Threading.Tasks.ValueTask<TRes> like on the McpBridge (.NET) side.
    // Do not copy this file as-is into McpBridge/ — the .NET side keeps ValueTask.

    public class ExploreLocationHandler : IAsyncRequestHandler<ExploreLocationRequest, ExploreLocationResponse>
    {
        private readonly ExplorationSystem _exploration;
        public ExploreLocationHandler(ExplorationSystem exploration) => _exploration = exploration;

        public UniTask<ExploreLocationResponse> InvokeAsync(ExploreLocationRequest request, CancellationToken cancellationToken = default)
        {
            var (success, found) = _exploration.Explore(request.LocationId);
            return UniTask.FromResult(new ExploreLocationResponse
            {
                Success = success,
                FoundCardIds = found,
                TriggeredEventId = "" // wire up WorldEventSystem roll here in Lab A
            });
        }
    }

    public class CraftCardHandler : IAsyncRequestHandler<CraftCardRequest, CraftCardResponse>
    {
        private readonly CraftingSystem _crafting;
        public CraftCardHandler(CraftingSystem crafting) => _crafting = crafting;

        public UniTask<CraftCardResponse> InvokeAsync(CraftCardRequest request, CancellationToken cancellationToken = default)
        {
            var (success, reason, output) = _crafting.TryCraft(request.RecipeId);
            return UniTask.FromResult(new CraftCardResponse { Success = success, FailureReason = reason, OutputCardId = output });
        }
    }

    public class AwaitNextEventHandler : IAsyncRequestHandler<AwaitNextEventRequest, AwaitNextEventResponse>
    {
        private readonly WorldEventSystem _worldEvents;
        public AwaitNextEventHandler(WorldEventSystem worldEvents) => _worldEvents = worldEvents;

        public UniTask<AwaitNextEventResponse> InvokeAsync(AwaitNextEventRequest request, CancellationToken cancellationToken = default)
        {
            // Lab A: naive poll; Lab E should replace with a proper async wait
            // (signalled from WorldEventSystem.Tick) so this doesn't busy-loop.
            if (_worldEvents.TryDequeue(out var evt))
                return UniTask.FromResult(new AwaitNextEventResponse { TimedOut = false, EventId = evt.Id, Group = evt.Group, DisplayText = evt.DisplayText });

            return UniTask.FromResult(new AwaitNextEventResponse { TimedOut = true });
        }
    }

    public class AccuseNpcHandler : IAsyncRequestHandler<AccuseNpcRequest, AccuseNpcResponse>
    {
        private readonly DeductionSystem _deduction;
        public AccuseNpcHandler(DeductionSystem deduction) => _deduction = deduction;

        public UniTask<AccuseNpcResponse> InvokeAsync(AccuseNpcRequest request, CancellationToken cancellationToken = default)
        {
            var (correct, win, loss, text) = _deduction.Accuse(request.TargetNpcId);
            return UniTask.FromResult(new AccuseNpcResponse { WasCorrect = correct, GameOverWin = win, GameOverLoss = loss, ResultText = text });
        }
    }

    public class GetGameStateHandler : IAsyncRequestHandler<GetGameStateRequest, GetGameStateResponse>
    {
        private readonly GameStateProvider _stateProvider;
        public GetGameStateHandler(GameStateProvider stateProvider) => _stateProvider = stateProvider;

        public UniTask<GetGameStateResponse> InvokeAsync(GetGameStateRequest request, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new GetGameStateResponse { Player = _stateProvider.Player });
        }
    }

    public class GetVisibleNpcsHandler : IAsyncRequestHandler<GetVisibleNpcsRequest, GetVisibleNpcsResponse>
    {
        private readonly DeductionSystem _deduction;
        private readonly GameStateProvider _stateProvider;

        public GetVisibleNpcsHandler(DeductionSystem deduction, GameStateProvider stateProvider)
        {
            _deduction = deduction;
            _stateProvider = stateProvider;
        }

        public UniTask<GetVisibleNpcsResponse> InvokeAsync(GetVisibleNpcsRequest request, CancellationToken cancellationToken = default)
        {
            var npcs = _deduction.GetObservableNpcsAt(_stateProvider.Player.CurrentLocationId);
            return UniTask.FromResult(new GetVisibleNpcsResponse { Npcs = npcs });
        }
    }

    public class GetClueBoardHandler : IAsyncRequestHandler<GetClueBoardRequest, GetClueBoardResponse>
    {
        private readonly GameStateProvider _stateProvider;
        public GetClueBoardHandler(GameStateProvider stateProvider) => _stateProvider = stateProvider;

        public UniTask<GetClueBoardResponse> InvokeAsync(GetClueBoardRequest request, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new GetClueBoardResponse { CollectedClueCardIds = _stateProvider.Player.CollectedClueCardIds });
        }
    }

    public class MoveToLocationHandler : IAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse>
    {
        private readonly GameStateProvider _stateProvider;
        private readonly LubanDataService _data;

        public MoveToLocationHandler(GameStateProvider stateProvider, LubanDataService data)
        {
            _stateProvider = stateProvider;
            _data = data;
        }

        public UniTask<MoveToLocationResponse> InvokeAsync(MoveToLocationRequest request, CancellationToken cancellationToken = default)
        {
            if (!_data.LocationDefs.TryGetValue(request.LocationId, out var targetDef))
                return UniTask.FromResult(new MoveToLocationResponse { Success = false, FailureReason = "unknown_location" });

            var current = _stateProvider.Player.CurrentLocationId;
            if (!string.IsNullOrEmpty(current)
                && _data.LocationDefs.TryGetValue(current, out var currentDef)
                && currentDef.ConnectedLocationIds != null
                && !currentDef.ConnectedLocationIds.Contains(request.LocationId))
            {
                return UniTask.FromResult(new MoveToLocationResponse { Success = false, FailureReason = "not_connected" });
            }

            _stateProvider.Player.CurrentLocationId = request.LocationId;
            return UniTask.FromResult(new MoveToLocationResponse { Success = true });
        }
    }

    public class UseCardHandler : IAsyncRequestHandler<UseCardRequest, UseCardResponse>
    {
        private readonly GameStateProvider _stateProvider;
        private readonly CardInventorySystem _inventory;
        private readonly LubanDataService _data;

        public UseCardHandler(GameStateProvider stateProvider, CardInventorySystem inventory, LubanDataService data)
        {
            _stateProvider = stateProvider;
            _inventory = inventory;
            _data = data;
        }

        public UniTask<UseCardResponse> InvokeAsync(UseCardRequest request, CancellationToken cancellationToken = default)
        {
            if (!_data.CardDefs.TryGetValue(request.CardId, out var def))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "unknown_card" });

            if (!_inventory.TryConsume(request.CardId, 1))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "not_in_inventory" });

            if (def.StatEffect != null)
            {
                var player = _stateProvider.Player;
                foreach (var kv in def.StatEffect)
                {
                    switch (kv.Key)
                    {
                        case "Hunger": player.Hunger = System.Math.Clamp(player.Hunger + kv.Value, 0f, 100f); break;
                        case "Thirst": player.Thirst = System.Math.Clamp(player.Thirst + kv.Value, 0f, 100f); break;
                        case "Mood": player.Mood = System.Math.Clamp(player.Mood + kv.Value, 0f, 100f); break;
                        case "Fatigue": player.Fatigue = System.Math.Clamp(player.Fatigue + kv.Value, 0f, 100f); break;
                    }
                }
            }

            return UniTask.FromResult(new UseCardResponse { Success = true });
        }
    }

    public class CallMeetingHandler : IAsyncRequestHandler<CallMeetingRequest, CallMeetingResponse>
    {
        private readonly DeductionSystem _deduction;
        private readonly GameStateProvider _stateProvider;

        public CallMeetingHandler(DeductionSystem deduction, GameStateProvider stateProvider)
        {
            _deduction = deduction;
            _stateProvider = stateProvider;
        }

        public UniTask<CallMeetingResponse> InvokeAsync(CallMeetingRequest request, CancellationToken cancellationToken = default)
        {
            // Lab A: meeting just snapshots whoever is currently visible; a real
            // "gather everyone" pause/summon step is a later lab.
            var npcs = _deduction.GetObservableNpcsAt(_stateProvider.Player.CurrentLocationId);
            return UniTask.FromResult(new CallMeetingResponse { Success = true, AttendingNpcs = npcs });
        }
    }
}
```

### Shared/ (canonical — sync ไป Unity + McpBridge)

#### Shared/CardDef.cs (34 บรรทัด)
**Path:** `Shared/CardDef.cs` (copy: `Marooned/Assets/Scripts/Shared/CardDef.cs`, `McpBridge/Shared/CardDef.cs`)

```csharp
using System.Collections.Generic;

namespace Marooned.Shared
{
    public enum CardCategory
    {
        Resource,
        Consumable,
        Tool,
        Illness,
        Injury,
        Clue,
        Craftable
    }

    /// <summary>
    /// Static definition of a card, generated from DataTables/CardDef.csv via Luban.
    /// Plain class (no ScriptableObject) so it can be diffed in git and edited as text.
    /// </summary>
    public class CardDef
    {
        public string Id;
        public CardCategory Category;
        public string DisplayName;
        public string SpritePath;
        public int StackLimit;

        /// <summary>Stat key -> delta applied when the card is used (Hunger/Thirst/Mood/Fatigue).</summary>
        public Dictionary<string, float> StatEffect;

        /// <summary>Only relevant for Illness/Injury: how much each action type is penalized while active.</summary>
        public Dictionary<string, float> ActionPenalty;
    }
}
```

#### Shared/PlayerSurvivalState.cs (32 บรรทัด)
**Path:** `Shared/PlayerSurvivalState.cs` (copy: `Marooned/Assets/Scripts/Shared/PlayerSurvivalState.cs`, `McpBridge/Shared/PlayerSurvivalState.cs`)

```csharp
using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    [MessagePackObject]
    public class PlayerSurvivalState
    {
        [Key(0)] public float Hunger = 100f;
        [Key(1)] public float Thirst = 100f;
        [Key(2)] public float Mood = 100f;
        [Key(3)] public float Fatigue = 0f;

        /// <summary>Illness/Injury card ids currently affecting the player.</summary>
        [Key(4)] public List<string> ActiveConditionCardIds = new();

        /// <summary>cardId -> count.</summary>
        [Key(5)] public Dictionary<string, int> Inventory = new();

        [Key(6)] public string CurrentLocationId;

        [Key(7)] public bool IsAlive = true;

        /// <summary>Clue cards the player has personally picked up / observed so far.</summary>
        [Key(8)] public List<string> CollectedClueCardIds = new();

        /// <summary>How many times the player has accused someone wrongly. Hitting the cap = loss.</summary>
        [Key(9)] public int WrongAccusations = 0;

        [Key(10)] public ChibiAppearance Avatar = new();
    }
}
```

#### Shared/NpcState.cs (66 บรรทัด)
**Path:** `Shared/NpcState.cs` (copy: `Marooned/Assets/Scripts/Shared/NpcState.cs`, `McpBridge/Shared/NpcState.cs`)

```csharp
using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    public enum NpcRole
    {
        Innocent,
        Killer,
        Neutral // เอาตัวรอดอย่างเดียว ไม่นับเป็นทั้งฝ่ายดี/ร้าย (เปิดใช้ทีหลังได้)
    }

    public enum NpcActivityState
    {
        Idle,
        Gathering,
        Resting,
        Traveling,
        Talking
    }

    /// <summary>
    /// FULL state, including ground truth (Role, hidden agenda). This class must
    /// only ever be held server-side (Unity SectStateProvider equivalent) and must
    /// NEVER be serialized directly into an MCP query response. Build an
    /// NpcObservableView from it instead — see Systems/DeductionVisibilityRules.cs.
    /// </summary>
    [MessagePackObject]
    public class NpcState
    {
        [Key(0)] public string Id;
        [Key(1)] public NpcRole Role;
        [Key(2)] public bool IsAlive = true;
        [Key(3)] public string CurrentLocationId;
        [Key(4)] public NpcActivityState Activity;

        /// <summary>All condition cards, including ones a bystander could not actually notice yet.</summary>
        [Key(5)] public List<string> AllConditionCardIds = new();

        [Key(6)] public ChibiAppearance Avatar = new();

        /// <summary>Killer-only: cooldown seconds remaining before another Eliminate action is possible.</summary>
        [Key(7)] public float KillCooldownRemaining;

        /// <summary>Killer-only optional objective, e.g. "eliminate 3 before day 5" — for future killer-AI mode.</summary>
        [Key(8)] public string HiddenAgendaId;
    }

    /// <summary>
    /// SAFE view of an NPC for MCP / player-facing queries. Only contains what a
    /// bystander standing in the same location could plausibly observe.
    /// </summary>
    [MessagePackObject]
    public class NpcObservableView
    {
        [Key(0)] public string Id;
        [Key(1)] public bool IsAlive;
        [Key(2)] public string CurrentLocationId;
        [Key(3)] public NpcActivityState Activity;

        /// <summary>Only condition cards flagged Visible=true in IllnessDef/injury def (e.g. visible scratch, not internal fever unless player checks closely).</summary>
        [Key(4)] public List<string> VisibleConditionCardIds = new();

        [Key(5)] public ChibiAppearance Avatar = new();
    }
}
```

#### Shared/LocationDef.cs (36 บรรทัด)
**Path:** `Shared/LocationDef.cs` (copy: `Marooned/Assets/Scripts/Shared/LocationDef.cs`, `McpBridge/Shared/LocationDef.cs`)

```csharp
using System.Collections.Generic;

namespace Marooned.Shared
{
    /// <summary>A single explorable node on the 2D sandbox map.</summary>
    public class LocationDef
    {
        public string Id;
        public string DisplayName;
        public float WorldX;
        public float WorldY;
        public List<string> ConnectedLocationIds;

        /// <summary>cardId -> spawn weight. Nodes deplete over time (see LocationRuntimeState).</summary>
        public Dictionary<string, int> LootTable;

        /// <summary>Max simultaneous NPCs this node can hold (relevant for "no witness" kill checks).</summary>
        public int Capacity = 4;
    }

    public class RecipeDef
    {
        public string Id;
        public string OutputCardId;
        public int OutputCount = 1;

        /// <summary>cardId -> count consumed.</summary>
        public Dictionary<string, int> Inputs;

        /// <summary>Optional: recipe only usable at these location ids. Empty = anywhere.</summary>
        public List<string> RequiredLocationIds;

        /// <summary>Optional tool card required in inventory but not consumed.</summary>
        public string RequiredToolCardId;
    }
}
```

#### Shared/ClueDef.cs (38 บรรทัด)
**Path:** `Shared/ClueDef.cs` (copy: `Marooned/Assets/Scripts/Shared/ClueDef.cs`, `McpBridge/Shared/ClueDef.cs`)

```csharp
using System.Collections.Generic;

namespace Marooned.Shared
{
    public enum ClueReliability
    {
        Strong,
        Weak,
        RedHerring
    }

    public class ClueDef
    {
        public string Id;
        public string DisplayName;   // "คราบเลือด", "รอยขีดข่วน", "รอยเท้าเปื้อนโคลน"
        public string SpritePath;
        public ClueReliability Reliability;
        public bool VisibleToBystanders; // false = ต้องเข้าไป "ตรวจสอบ" (investigate_clue) ถึงจะเห็น
    }

    public class IllnessDef
    {
        public string Id;
        public string DisplayName;
        public bool Visible; // true = NPC อื่นมองเห็น (บาดแผล/ผ้าพันแผล), false = ภายใน (ไข้)
        public string CureCardId;
        public float SeverityGrowthPerHour;
    }

    public class WorldEventDef
    {
        public string Id;
        public string Group; // "Survival" | "Social"
        public int Weight;
        public string DisplayText;
        public List<string> RequiredLocationTags;
    }
}
```

#### Shared/ChibiAppearance.cs (89 บรรทัด)
**Path:** `Shared/ChibiAppearance.cs` (copy: `Marooned/Assets/Scripts/Shared/ChibiAppearance.cs`, `McpBridge/Shared/ChibiAppearance.cs`)

```csharp
using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    public enum FacingDirection
    {
        Down,
        Up,
        Left,
        Right
    }

    public enum ChibiAnimState
    {
        Idle,
        Walk,
        Hurt,
        Sleep
    }

    /// <summary>
    /// Data-only description of a chibi character's look. Same Dictionary-based
    /// philosophy as the reference project's AvatarAppearance (add a slot = edit
    /// JSON, no schema break), but each slot now points to a *part sheet*
    /// (ChibiPartDef) that has per-direction animation frames instead of a single
    /// portrait sprite — because this character walks around a 2D sandbox map
    /// instead of standing still in a dialogue box.
    /// </summary>
    [MessagePackObject]
    public class ChibiAppearance
    {
        // slot -> partId, e.g. "body" -> "body_base_tan", "hair" -> "hair_twin_tail"
        [Key(0)] public Dictionary<string, string> Parts = new();

        // slot -> colorId, reserved for tinting (same as reference project, Method A / Image.color multiply)
        [Key(1)] public Dictionary<string, string> Colors = new();

        // Condition overlays currently applied on top of the base rig, e.g. "bandage_arm",
        // "blood_stain_torso", "scratch_face". These are ALSO slot->partId entries but
        // live in a separate dictionary so gameplay code can clear them independently
        // of outfit changes (healing shouldn't require re-picking the whole outfit).
        [Key(2)] public Dictionary<string, string> ConditionOverlays = new();

        [Key(3)] public FacingDirection Facing = FacingDirection.Down;

        [Key(4)] public ChibiAnimState AnimState = ChibiAnimState.Idle;
    }

    /// <summary>
    /// Static def for one part (one slot's worth of art), generated from
    /// DataTables/ChibiPartDef.csv via Luban. Unlike the reference project's
    /// AvatarPartDef (single spritePath + spritePathBack), each part now carries a
    /// small sprite sheet per direction so ChibiAnimatedRenderer can flip through
    /// frames for walk-cycle animation.
    /// </summary>
    public class ChibiPartDef
    {
        public string Id;
        public string Slot;       // "body","head","hair","arm_left","arm_right","leg_left","leg_right","accessory","overlay"
        public int DrawOrder;     // layering within the rig, same idea as reference project's drawOrder
        public string SexTag;     // "", "Male", "Female" — filtering only, same convention as before

        /// <summary>
        /// direction+animState key (e.g. "Down_Walk", "Left_Idle") -> ordered list of
        /// sprite paths to cycle through. Idle typically has 1-2 frames (breathing),
        /// Walk typically has 4 frames. This is the actual "sprite swap" driving the
        /// movement — no bones/rigging required, just frame swapping per limb slot.
        /// </summary>
        public Dictionary<string, List<string>> FramesByAnimKey = new();

        /// <summary>Local pivot offset (in pixels, base 1024x1024 chibi canvas) so limb
        /// parts attach correctly to the torso when swapped between outfits.</summary>
        public float PivotX;
        public float PivotY;
    }

    /// <summary>Package of parts + pose that can be applied atomically (same intent as
    /// the reference project's OutfitDef, adapted so a full outfit swaps consistent
    /// walk-cycle frame sets across every limb slot at once).</summary>
    public class ChibiOutfitDef
    {
        public string Id;
        public string DisplayName;
        public string SexTag;
        public Dictionary<string, string> Parts; // slot -> partId, must all share compatible frame keys
        public string ThumbPath;
    }
}
```

#### Shared/GameMessages.cs (169 บรรทัด)
**Path:** `Shared/GameMessages.cs` (copy: `Marooned/Assets/Scripts/Shared/GameMessages.cs`, `McpBridge/Shared/GameMessages.cs`)

```csharp
using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    // ---- Cross-process (Bridge <-> Unity) request/response messages ----
    // Pattern: Request-Response over MessagePipe.Interprocess, same convention as
    // the reference project's AwaitWorldEventRequest/Response (avoids the double
    // TCP-listener bug documented in Lab 6 of the reference project).

    [MessagePackObject]
    public class ExploreLocationRequest
    {
        [Key(0)] public string LocationId;
    }

    [MessagePackObject]
    public class ExploreLocationResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public List<string> FoundCardIds = new();
        [Key(2)] public string TriggeredEventId; // may be empty
    }

    [MessagePackObject]
    public class CraftCardRequest
    {
        [Key(0)] public string RecipeId;
    }

    [MessagePackObject]
    public class CraftCardResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason;
        [Key(2)] public string OutputCardId;
    }

    [MessagePackObject]
    public class AwaitNextEventRequest
    {
        [Key(0)] public int TimeoutSeconds = 30;
    }

    [MessagePackObject]
    public class AwaitNextEventResponse
    {
        [Key(0)] public bool TimedOut;
        [Key(1)] public string EventId;
        [Key(2)] public string Group; // "Survival" | "Social"
        [Key(3)] public string DisplayText;
    }

    [MessagePackObject]
    public class AccuseNpcRequest
    {
        [Key(0)] public string TargetNpcId;
    }

    [MessagePackObject]
    public class AccuseNpcResponse
    {
        [Key(0)] public bool WasCorrect;
        [Key(1)] public bool GameOverWin;
        [Key(2)] public bool GameOverLoss;
        [Key(3)] public string ResultText;
    }

    // ---- Added for the full MCP tool table (design doc §6): GetGameState,
    // GetVisibleNpcs, GetClueBoard, MoveToLocation, UseCard, CallMeeting ----

    [MessagePackObject]
    public class GetGameStateRequest
    {
        // no parameters; empty request kept for symmetry with the request/response pattern
    }

    [MessagePackObject]
    public class GetGameStateResponse
    {
        [Key(0)] public PlayerSurvivalState Player;
    }

    [MessagePackObject]
    public class GetVisibleNpcsRequest
    {
        // uses the player's current location server-side; no parameters needed
    }

    [MessagePackObject]
    public class GetVisibleNpcsResponse
    {
        [Key(0)] public List<NpcObservableView> Npcs = new();
    }

    [MessagePackObject]
    public class GetClueBoardRequest
    {
    }

    [MessagePackObject]
    public class GetClueBoardResponse
    {
        [Key(0)] public List<string> CollectedClueCardIds = new();
    }

    [MessagePackObject]
    public class MoveToLocationRequest
    {
        [Key(0)] public string LocationId;
    }

    [MessagePackObject]
    public class MoveToLocationResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason;
    }

    [MessagePackObject]
    public class UseCardRequest
    {
        [Key(0)] public string CardId;
    }

    [MessagePackObject]
    public class UseCardResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason;
    }

    [MessagePackObject]
    public class CallMeetingRequest
    {
    }

    [MessagePackObject]
    public class CallMeetingResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public List<NpcObservableView> AttendingNpcs = new();
    }

    // ---- In-process broadcast messages (published locally inside Unity via MessagePipe) ----

    [MessagePackObject]
    public class SurvivalStatChangedMessage
    {
        [Key(0)] public string StatKey; // Hunger/Thirst/Mood/Fatigue
        [Key(1)] public float NewValue;
        [Key(2)] public float Delta;
    }

    [MessagePackObject]
    public class ConditionCardAppliedMessage
    {
        [Key(0)] public string TargetEntityId; // "player" or npcId
        [Key(1)] public string ConditionCardId;
    }

    [MessagePackObject]
    public class NpcEliminatedMessage
    {
        [Key(0)] public string VictimNpcId;
        [Key(1)] public string LocationId;
        [Key(2)] public List<string> SpawnedClueCardIds = new();
    }
}
```

### Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs (127 บรรทัด)
**Path:** `Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs`

```csharp
using System.Collections.Generic;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Data
{
    /// <summary>
    /// Replaces the reference project's single-image AvatarRenderer (portrait swap
    /// only) with a walking paperdoll: one SpriteRenderer per limb slot, each
    /// flipping through ChibiPartDef.FramesByAnimKey frames as the character walks
    /// around the 2D sandbox. No bones/skeleton needed — this is classic top-down
    /// RPG-style frame swapping per body part, composited by draw order exactly
    /// like the reference project's hair-front/hair-back layering.
    ///
    /// Layer stack (bottom to top), same DrawOrder convention as before:
    ///   leg_back(0) -> body(10) -> leg_front(15) -> arm_back(20) -> head(30)
    ///   -> hair_back(35) -> face_marking(38) -> hair_front(40) -> arm_front(45)
    ///   -> overlay/condition(50) -> accessory(60)
    /// </summary>
    public class ChibiAnimatedRenderer : MonoBehaviour
    {
        [SerializeField] private float framesPerSecond = 6f;

        private readonly Dictionary<string, SpriteRenderer> _slotRenderers = new();
        private readonly Dictionary<string, Sprite[]> _cachedLoadedSprites = new();

        private ChibiAppearance _appearance;
        private LubanPartLookup _partLookup; // wraps LubanDataService.ChibiPartDefs, injected at Init

        private float _frameTimer;
        private int _frameIndex;

        public void Init(ChibiAppearance appearance, LubanPartLookup partLookup)
        {
            _appearance = appearance;
            _partLookup = partLookup;
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            foreach (var kv in _slotRenderers) Destroy(kv.Value.gameObject);
            _slotRenderers.Clear();

            foreach (var (slot, partId) in _appearance.Parts)
                EnsureSlotRenderer(slot, partId);

            // Condition overlays (bandage, blood stain, scratch) layer on top, keyed
            // by their own slot names ("overlay_arm", "overlay_face", ...) so they
            // don't collide with outfit part slots and can be cleared independently.
            foreach (var (slot, partId) in _appearance.ConditionOverlays)
                EnsureSlotRenderer(slot, partId);
        }

        private void EnsureSlotRenderer(string slot, string partId)
        {
            var def = _partLookup.Get(partId);
            if (def == null) return;

            var go = new GameObject($"slot_{slot}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(def.PivotX / 100f, def.PivotY / 100f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = def.DrawOrder;
            _slotRenderers[slot] = sr;
        }

        private void Update()
        {
            if (_appearance == null) return;

            _frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
            }

            var animKey = $"{_appearance.Facing}_{_appearance.AnimState}";

            foreach (var (slot, renderer) in _slotRenderers)
            {
                var partId = _appearance.Parts.TryGetValue(slot, out var pid) ? pid
                    : _appearance.ConditionOverlays.TryGetValue(slot, out var oid) ? oid
                    : null;
                if (partId == null) continue;

                var def = _partLookup.Get(partId);
                if (def == null || !def.FramesByAnimKey.TryGetValue(animKey, out var frames) || frames.Count == 0)
                    continue;

                var sprites = GetOrLoadSprites(partId, animKey, frames);
                renderer.sprite = sprites[_frameIndex % sprites.Length];
            }
        }

        private Sprite[] GetOrLoadSprites(string partId, string animKey, List<string> framePaths)
        {
            var cacheKey = $"{partId}:{animKey}";
            if (_cachedLoadedSprites.TryGetValue(cacheKey, out var cached)) return cached;

            var sprites = new Sprite[framePaths.Count];
            for (var i = 0; i < framePaths.Count; i++)
                sprites[i] = Resources.Load<Sprite>(framePaths[i]);

            _cachedLoadedSprites[cacheKey] = sprites;
            return sprites;
        }

        /// <summary>Call when the entity's movement input changes (from a top-down movement controller).</summary>
        public void SetMotion(FacingDirection facing, bool isMoving)
        {
            _appearance.Facing = facing;
            _appearance.AnimState = isMoving ? ChibiAnimState.Walk : ChibiAnimState.Idle;
        }
    }

    /// <summary>Thin wrapper so the renderer doesn't need to know about VContainer/LubanDataService directly.</summary>
    public class LubanPartLookup
    {
        private readonly Dictionary<string, ChibiPartDef> _defs;
        public LubanPartLookup(Dictionary<string, ChibiPartDef> defs) => _defs = defs;
        public ChibiPartDef Get(string partId) => _defs.TryGetValue(partId, out var d) ? d : null;
    }
}
```

### Marooned/Assets/Scripts/UI/Core/UIRoot.cs (43 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Core/UIRoot.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.UI.Core
{
    public enum UIPanelType
    {
        CardHand,
        MapExplore,
        MeetingVote,
        ClueBoard,
        ConditionOverlay
    }

    /// <summary>
    /// Same explicit enum->Type panel resolution as the reference project (no
    /// assembly scanning). Also re-applies the stretch-anchor fix from the
    /// reference project's Lab 13 (ContentSizeFitter=PreferredSize was clobbering
    /// anchor stretch and causing panels to pile up center-screen).
    /// </summary>
    public class UIRoot : IInitializable
    {
        private readonly Dictionary<UIPanelType, Type> _panelViewTypes = new()
        {
            { UIPanelType.CardHand, typeof(Views.CardHandView) },
            { UIPanelType.MapExplore, typeof(Views.MapExploreView) },
            { UIPanelType.MeetingVote, typeof(Views.MeetingVoteView) },
            { UIPanelType.ClueBoard, typeof(Views.ClueBoardView) },
            { UIPanelType.ConditionOverlay, typeof(Views.ConditionOverlayView) },
        };

        public void Initialize()
        {
            // Lab A: just make sure the Canvas root stretches full-screen and every
            // top-level panel starts at Unconstrained layout, per the reference
            // project's UIRoot.Awake() fix. Actual instantiation of each panel
            // prefab happens once GameplayScene loads (see design doc Additive
            // Scene section).
        }
    }
}
```

### Marooned/Assets/Scripts/UI/Presenters/ (4 ไฟล์ skeleton)

#### CardHandPresenter.cs (9 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Presenters/CardHandPresenter.cs`

```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Plain C#, transient. Reads CardInventorySystem, pushes to CardHandView.</summary>
    public class CardHandPresenter
    {
        // Constructor-injects CardInventorySystem + CardHandView reference; subscribes
        // to inventory-changed message and calls view.RenderHand(...).
    }
}
```

#### ClueBoardPresenter.cs (7 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Presenters/ClueBoardPresenter.cs`

```csharp
namespace Marooned.UI.Presenters
{
    public class ClueBoardPresenter
    {
        // Reads PlayerSurvivalState.CollectedClueCardIds, resolves ClueDef, renders cards.
    }
}
```

#### MapExplorePresenter.cs (7 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Presenters/MapExplorePresenter.cs`

```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Calls ExplorationSystem.Explore on node click; direct method call (in-process), not pub/sub.</summary>
    public class MapExplorePresenter
    {
    }
}
```

#### MeetingVotePresenter.cs (7 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Presenters/MeetingVotePresenter.cs`

```csharp
namespace Marooned.UI.Presenters
{
    /// <summary>Calls DeductionSystem.Accuse via DecisionExecutor-style direct method call (Lab 13 lesson from reference project: don't use pub/sub for in-process UI logic that must have an immediate visible effect).</summary>
    public class MeetingVotePresenter
    {
    }
}
```

### Marooned/Assets/Scripts/UI/Views/ (5 ไฟล์ skeleton)

#### CardHandView.cs (13 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Views/CardHandView.cs`

```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Displays the player's card hand/inventory; drag onto CraftingSystem or UseCard action.</summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform cardSlotContainer;
        [SerializeField] private GameObject cardSlotPrefab; // pooled, per reference project's grid button pooling lesson

        // Presenter calls RenderHand(cardId -> count) to refresh slots.
    }
}
```

#### ClueBoardView.cs (10 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Views/ClueBoardView.cs`

```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Detective-board style layout of collected ClueDef cards, for the player (and AI agent's get_clue_board query) to review.</summary>
    public class ClueBoardView : MonoBehaviour
    {
        [SerializeField] private Transform clueBoardContainer;
    }
}
```

#### ConditionOverlayView.cs (10 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Views/ConditionOverlayView.cs`

```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Small HUD icons for the player's own active illness/injury cards (ActiveConditionCardIds). NPC-side visuals are handled by ChibiAnimatedRenderer's ConditionOverlays slots directly, not this view.</summary>
    public class ConditionOverlayView : MonoBehaviour
    {
        [SerializeField] private Transform iconContainer;
    }
}
```

#### MapExploreView.cs (10 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Views/MapExploreView.cs`

```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>2D sandbox map: clickable LocationDef nodes, shows depleted/available state.</summary>
    public class MapExploreView : MonoBehaviour
    {
        [SerializeField] private Transform nodeContainer;
    }
}
```

#### MeetingVoteView.cs (10 บรรทัด)
**Path:** `Marooned/Assets/Scripts/UI/Views/MeetingVoteView.cs`

```csharp
using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Meeting phase: list of living NPCs (observable view only) + Accuse/Abstain buttons.</summary>
    public class MeetingVoteView : MonoBehaviour
    {
        [SerializeField] private Transform npcListContainer;
    }
}
```

### McpBridge/

#### McpBridge/Program.cs (198 บรรทัด)
**Path:** `McpBridge/Program.cs`

```csharp
using Marooned.Shared;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// Stdio MCP uses stdout for JSON-RPC framing, so ALL logs must go to stderr,
// or the MCP client will fail to parse the stream.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// --- MessagePipe + TCP interprocess client ---
// Unity is the server (HostAsServer = true, see GameLifetimeScope.cs) and must
// already be running (Play mode) before this process starts, or the TCP
// connection will fail. Host/port must match GameLifetimeScope's
// interprocessHost/interprocessPort (127.0.0.1:3216 by default).
builder.Services.AddMessagePipe()
    .AddTcpInterprocess("127.0.0.1", 3216, tcp =>
    {
        tcp.HostAsServer = false;
    });

// --- MCP server over stdio, tools auto-discovered from this assembly ---
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

namespace Marooned.McpBridge
{
    /// <summary>
    /// Read-only-ish query tools. GetVisibleNpcs / GetClueBoard only ever return
    /// what DeductionSystem.GetObservableNpcsAt builds server-side -- the true
    /// NpcRole never crosses this boundary, by construction (see design doc §5).
    /// </summary>
    [McpServerToolType]
    public class SurvivalQueryTools
    {
        private readonly IRemoteRequestHandler<GetGameStateRequest, GetGameStateResponse> _getGameState;
        private readonly IRemoteRequestHandler<GetVisibleNpcsRequest, GetVisibleNpcsResponse> _getVisibleNpcs;
        private readonly IRemoteRequestHandler<GetClueBoardRequest, GetClueBoardResponse> _getClueBoard;

        public SurvivalQueryTools(
            IRemoteRequestHandler<GetGameStateRequest, GetGameStateResponse> getGameState,
            IRemoteRequestHandler<GetVisibleNpcsRequest, GetVisibleNpcsResponse> getVisibleNpcs,
            IRemoteRequestHandler<GetClueBoardRequest, GetClueBoardResponse> getClueBoard)
        {
            _getGameState = getGameState;
            _getVisibleNpcs = getVisibleNpcs;
            _getClueBoard = getClueBoard;
        }

        [McpServerTool, Description("Get the player's current survival stats, inventory, location and active conditions.")]
        public async Task<string> GetGameState()
        {
            var res = await _getGameState.InvokeAsync(new GetGameStateRequest());
            var p = res.Player;
            var inv = string.Join(", ", p.Inventory.Count == 0
                ? new[] { "(empty)" }
                : p.Inventory.Select(kv => $"{kv.Key} x{kv.Value}"));
            var conditions = p.ActiveConditionCardIds.Count == 0 ? "none" : string.Join(", ", p.ActiveConditionCardIds);
            return $"Location: {p.CurrentLocationId} | Alive: {p.IsAlive}\n" +
                   $"Hunger: {p.Hunger:0} | Thirst: {p.Thirst:0} | Mood: {p.Mood:0} | Fatigue: {p.Fatigue:0}\n" +
                   $"Inventory: {inv}\n" +
                   $"Conditions: {conditions}\n" +
                   $"Wrong accusations so far: {p.WrongAccusations}";
        }

        [McpServerTool, Description("Get NPCs visible at the player's current location. Only shows what a bystander could actually observe -- never the true killer/innocent role.")]
        public async Task<string> GetVisibleNpcs()
        {
            var res = await _getVisibleNpcs.InvokeAsync(new GetVisibleNpcsRequest());
            if (res.Npcs.Count == 0) return "No NPCs visible here.";
            return string.Join("\n", res.Npcs.Select(n =>
            {
                var conditions = n.VisibleConditionCardIds.Count == 0 ? "none visible" : string.Join(", ", n.VisibleConditionCardIds);
                return $"{n.Id} | alive={n.IsAlive} | activity={n.Activity} | visible conditions: {conditions}";
            }));
        }

        [McpServerTool, Description("Get all clue cards the player has personally collected so far, for deduction.")]
        public async Task<string> GetClueBoard()
        {
            var res = await _getClueBoard.InvokeAsync(new GetClueBoardRequest());
            return res.CollectedClueCardIds.Count == 0
                ? "No clues collected yet."
                : string.Join(", ", res.CollectedClueCardIds);
        }
    }

    /// <summary>Mutating tools -- each round-trips through Unity's request/response handlers.</summary>
    [McpServerToolType]
    public class SurvivalActionTools
    {
        private readonly IRemoteRequestHandler<ExploreLocationRequest, ExploreLocationResponse> _explore;
        private readonly IRemoteRequestHandler<CraftCardRequest, CraftCardResponse> _craft;
        private readonly IRemoteRequestHandler<MoveToLocationRequest, MoveToLocationResponse> _move;
        private readonly IRemoteRequestHandler<UseCardRequest, UseCardResponse> _useCard;
        private readonly IRemoteRequestHandler<AwaitNextEventRequest, AwaitNextEventResponse> _awaitNextEvent;

        public SurvivalActionTools(
            IRemoteRequestHandler<ExploreLocationRequest, ExploreLocationResponse> explore,
            IRemoteRequestHandler<CraftCardRequest, CraftCardResponse> craft,
            IRemoteRequestHandler<MoveToLocationRequest, MoveToLocationResponse> move,
            IRemoteRequestHandler<UseCardRequest, UseCardResponse> useCard,
            IRemoteRequestHandler<AwaitNextEventRequest, AwaitNextEventResponse> awaitNextEvent)
        {
            _explore = explore;
            _craft = craft;
            _move = move;
            _useCard = useCard;
            _awaitNextEvent = awaitNextEvent;
        }

        [McpServerTool, Description("Explore the player's current location. Returns any cards found; empty if the node is temporarily depleted.")]
        public async Task<string> ExploreLocation([Description("Location id to explore, e.g. 'beach'")] string locationId)
        {
            var res = await _explore.InvokeAsync(new ExploreLocationRequest { LocationId = locationId });
            if (!res.Success) return $"Could not explore '{locationId}'.";
            return res.FoundCardIds.Count == 0
                ? $"Explored '{locationId}' but found nothing this time."
                : $"Found: {string.Join(", ", res.FoundCardIds)}";
        }

        [McpServerTool, Description("Craft a recipe by id using cards currently in inventory.")]
        public async Task<string> CraftCard([Description("Recipe id, e.g. 'recipe_cook_fish'")] string recipeId)
        {
            var res = await _craft.InvokeAsync(new CraftCardRequest { RecipeId = recipeId });
            return res.Success ? $"Crafted: {res.OutputCardId}" : $"Craft failed: {res.FailureReason}";
        }

        [McpServerTool, Description("Move the player to a connected location id.")]
        public async Task<string> MoveToLocation([Description("Destination location id")] string locationId)
        {
            var res = await _move.InvokeAsync(new MoveToLocationRequest { LocationId = locationId });
            return res.Success ? $"Moved to {locationId}." : $"Move failed: {res.FailureReason}";
        }

        [McpServerTool, Description("Consume/use a card from inventory (food, water, medicine, etc).")]
        public async Task<string> UseCard([Description("Card id to use")] string cardId)
        {
            var res = await _useCard.InvokeAsync(new UseCardRequest { CardId = cardId });
            return res.Success ? $"Used {cardId}." : $"Could not use {cardId}: {res.FailureReason}";
        }

        [McpServerTool, Description("Block until the next world event fires (Survival or Social group), or time out.")]
        public async Task<string> AwaitNextEvent([Description("Seconds to wait before timing out")] int timeoutSeconds = 30)
        {
            var res = await _awaitNextEvent.InvokeAsync(new AwaitNextEventRequest { TimeoutSeconds = timeoutSeconds });
            return res.TimedOut ? "No event occurred within the timeout." : $"[{res.Group}] {res.DisplayText} (id={res.EventId})";
        }
    }

    /// <summary>Social-deduction specific tools.</summary>
    [McpServerToolType]
    public class DeductionTools
    {
        private readonly IRemoteRequestHandler<CallMeetingRequest, CallMeetingResponse> _callMeeting;
        private readonly IRemoteRequestHandler<AccuseNpcRequest, AccuseNpcResponse> _accuse;

        public DeductionTools(
            IRemoteRequestHandler<CallMeetingRequest, CallMeetingResponse> callMeeting,
            IRemoteRequestHandler<AccuseNpcRequest, AccuseNpcResponse> accuse)
        {
            _callMeeting = callMeeting;
            _accuse = accuse;
        }

        [McpServerTool, Description("Report a body / call an emergency meeting when a murder is discovered.")]
        public async Task<string> CallMeeting()
        {
            var res = await _callMeeting.InvokeAsync(new CallMeetingRequest());
            if (!res.Success) return "Could not call a meeting right now.";
            return res.AttendingNpcs.Count == 0
                ? "Meeting called, but no NPCs are present."
                : $"Meeting called. Attending: {string.Join(", ", res.AttendingNpcs.Select(n => n.Id))}";
        }

        [McpServerTool, Description("Accuse an NPC of being the killer during a meeting. Wrong accusations cost mood and count toward a loss condition.")]
        public async Task<string> AccuseNpc([Description("NPC id to accuse")] string targetNpcId)
        {
            var res = await _accuse.InvokeAsync(new AccuseNpcRequest { TargetNpcId = targetNpcId });
            if (res.GameOverWin) return $"Correct! All killers caught. YOU WIN. ({res.ResultText})";
            if (res.GameOverLoss) return $"Too many wrong accusations. YOU LOSE. ({res.ResultText})";
            return res.WasCorrect
                ? $"Correct -- one killer down, but more may remain. ({res.ResultText})"
                : $"Wrong accusation. ({res.ResultText})";
        }
    }
}
```

#### McpBridge/McpBridge.csproj (23 บรรทัด)
**Path:** `McpBridge/McpBridge.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Xianxia.Sect.Bridge</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Pinned via `dotnet add package` - resolved to real published versions. -->
    <PackageReference Include="MessagePipe" Version="1.8.2" />
    <PackageReference Include="MessagePipe.Interprocess" Version="1.8.2" />
    <PackageReference Include="ModelContextProtocol" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
  </ItemGroup>

  <!-- No explicit <Compile Include> block - the SDK already auto-includes
       every .cs file under this directory (including Shared/), adding one
       here double-counts them and fails the build with NETSDK1022. -->

</Project>
```

### Scripts และ Config ของ data pipeline

#### sync-shared.sh (17 บรรทัด)
**Path:** `sync-shared.sh`

```bash
#!/usr/bin/env bash
# Copies Shared/*.cs into Marooned and McpBridge. Edit files ONLY in the
# top-level Shared/ folder — the copies below get overwritten every run.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$ROOT_DIR/Shared"

UNITY_DEST="$ROOT_DIR/Marooned/Assets/Scripts/Shared"
BRIDGE_DEST="$ROOT_DIR/McpBridge/Shared"

mkdir -p "$UNITY_DEST" "$BRIDGE_DEST"

cp "$SRC"/*.cs "$UNITY_DEST"/
cp "$SRC"/*.cs "$BRIDGE_DEST"/

echo "Synced $(ls "$SRC"/*.cs | wc -l | tr -d ' ') file(s) from Shared/ -> Marooned/ and McpBridge/"
```

#### DataTables/gen.sh (17 บรรทัด)
**Path:** `DataTables/gen.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail
WORKSPACE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GEN_CLIENT="$WORKSPACE/Tools/Luban/Luban.dll"
CONF_ROOT="$WORKSPACE/DataTables"

dotnet "$GEN_CLIENT" \
    -t client \
    -c cs-simple-json \
    -d json \
    --conf "$CONF_ROOT/luban.conf" \
    -x outputCodeDir="$WORKSPACE/Marooned/Assets/Scripts/Data/Gen" \
    -x outputDataDir="$WORKSPACE/Marooned/Assets/Resources/DataTables"

echo ""
echo "Done. Regenerated code into Marooned/Assets/Scripts/Data/Gen"
echo "and data into Marooned/Assets/Resources/DataTables."
```

#### DataTables/gen.bat (18 บรรทัด)
**Path:** `DataTables/gen.bat` (เวอร์ชัน Windows ของ gen.sh)

```bat
@echo off
setlocal

set WORKSPACE=%~dp0..
set GEN_CLIENT=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=%~dp0

:: ใส่ "" ครอบ path ที่มีโอกาสมีช่องว่างค่ะ
dotnet "%GEN_CLIENT%" ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x outputCodeDir="%WORKSPACE%\Marooned\Assets\Scripts\Data\Gen" ^
    -x outputDataDir="%WORKSPACE%\Marooned\Assets\Resources\DataTables"

echo.
echo Done. Regenerated code into Marooned/Assets/Scripts/Data/Gen
echo and data into Marooned/Assets/Resources/DataTables.
pause
```

#### DataTables/luban.conf (15 บรรทัด)
**Path:** `DataTables/luban.conf`

```json
{
    "groups":
    [
        {"names":["c"], "default":true}
    ],
    "schemaFiles":
    [
        {"fileName":"Defines/event.xml", "type":""}
    ],
    "dataDir": "Data",
    "targets":
    [
        {"name":"client", "manager":"Tables", "groups":["c"], "topModule":"cfg"}
    ]
}
```

### DataTables/Data/*.csv (Luban source drafts)

#### DataTables/Data/CardDef.csv (8 บรรทัด)
**Path:** `DataTables/Data/CardDef.csv`

```csv
##var,id,category,displayName,spritePath,stackLimit,hungerDelta,thirstDelta,moodDelta,fatigueDelta,explorePenalty,craftPenalty
##type,string,string,string,string,int,float,float,float,float,float,float
##,card id,category,display name,sprite path,stack limit,hunger effect,thirst effect,mood effect,fatigue effect,explore penalty,craft penalty
,food_coconut,Resource,มะพร้าว,Sprites/Cards/food_coconut,10,15,0,0,0,0,0
,water_bottle,Resource,น้ำขวด,Sprites/Cards/water_bottle,10,0,20,0,0,0,0
,raw_fish,Resource,ปลาดิบ,Sprites/Cards/raw_fish,10,10,0,-5,0,0,0
,cooked_fish,Craftable,ปลาย่าง,Sprites/Cards/cooked_fish,10,25,0,5,0,0,0
,illness_malnutrition,Illness,ภาวะขาดสารอาหาร,Sprites/Cards/illness_malnutrition,1,0,0,0,0,-0.3,-0.2
,illness_dehydration,Illness,ภาวะขาดน้ำ,Sprites/Cards/illness_dehydration,1,0,0,0,0,-0.4,0
```

#### DataTables/Data/ChibiPartDef.csv (12 บรรทัด)
**Path:** `DataTables/Data/ChibiPartDef.csv` (หมายเหตุ: ยังไม่ผ่าน Luban — frame data จะใช้ JSON sidecar แยก)

```csv
Id,Slot,DrawOrder,SexTag,PivotX,PivotY,Note
body_base_tan,body,10,,0,0,"FramesByAnimKey filled in via a separate JSON sidecar per part (path list per Direction_AnimState key) -- too nested for a flat CSV row, see README note on ChibiPartDef frame data"
head_base_round,head,30,,0,40,
hair_twin_tail,hair,40,Female,0,45,
hair_topknot,hair,40,Male,0,45,
arm_left_base,arm_left,20,,-18,20,
arm_right_base,arm_right,45,,18,20,
leg_left_base,leg_left,0,,-8,-30,
leg_right_base,leg_right,15,,8,-30,
overlay_bandage_arm,overlay,50,,-18,20,condition overlay
overlay_blood_stain_torso,overlay,50,,0,10,condition overlay
overlay_scratch_face,overlay,55,,0,42,condition overlay
```

#### DataTables/Data/ClueDef.csv (6 บรรทัด)
**Path:** `DataTables/Data/ClueDef.csv`

```csv
##var,id,displayName,spritePath,reliability,visibleToBystanders
##type,string,string,string,string,bool
##,clue id,display name,sprite path,reliability,visible to bystanders
,clue_blood_stain,คราบเลือด,Sprites/Clues/blood_stain,Strong,true
,clue_scratch_mark,รอยขีดข่วน,Sprites/Clues/scratch_mark,Weak,true
,clue_footprint_mud,รอยเท้าเปื้อนโคลน,Sprites/Clues/footprint_mud,Weak,false
,clue_torn_cloth,ผ้าขาดติดกิ่งไม้,Sprites/Clues/torn_cloth,RedHerring,true
```

#### DataTables/Data/IllnessDef.csv (5 บรรทัด)
**Path:** `DataTables/Data/IllnessDef.csv`

```csv
##var,id,displayName,visible,cureCardId,severityGrowthPerHour
##type,string,string,bool,string,float
##,illness id,display name,visible,cure card,severity growth per hour
,illness_malnutrition,ภาวะขาดสารอาหาร,false,cooked_fish,0.5
,illness_dehydration,ภาวะขาดน้ำ,false,water_bottle,0.8
,injury_cut,บาดแผล,true,bandage,0.2
```

#### DataTables/Data/LocationDef.csv (6 บรรทัด)
**Path:** `DataTables/Data/LocationDef.csv`

```csv
##var,id,displayName,worldX,worldY,connectedLocationIds,lootCard1,lootWeight1,lootCard2,lootWeight2,lootCard3,lootWeight3,capacity
##type,string,string,float,float,"(list#sep=;),string",string,int,string,int,string,int,int
##,location id,display name,world x,world y,connected ids,loot1,weight1,loot2,weight2,loot3,weight3,capacity
,beach,ชายหาด,0,0,jungle_edge;cave_entrance,food_coconut,5,water_bottle,2,raw_fish,3,6
,jungle_edge,ชายป่า,10,5,beach;deep_jungle,food_coconut,3,,,,,4
,deep_jungle,ป่าลึก,20,10,jungle_edge,raw_fish,1,,,,,3
,cave_entrance,ปากถ้ำ,-5,8,beach,water_bottle,4,,,,,3
```

#### DataTables/Data/RecipeDef.csv (4 บรรทัด)
**Path:** `DataTables/Data/RecipeDef.csv`

```csv
##var,id,outputCardId,outputCount,inputCard1,inputCount1,inputCard2,inputCount2,requiredLocationId,requiredToolCardId,
##type,string,string,int,string,int,string,int,string,string,
##,recipe id,output,output count,input1,count1,input2,count2,required location,required tool,
,recipe_cook_fish,cooked_fish,1,raw_fish,1,,,,,tool_campfire
```

#### DataTables/Data/WorldEventDef.csv (6 บรรทัด)
**Path:** `DataTables/Data/WorldEventDef.csv`

```csv
##var,id,group,weight,displayText,requiredLocationTag
##type,string,string,int,string,string
##,event id,group,weight,display text,required location tag
,event_storm,Survival,10,พายุเข้า ทำให้ Fatigue ลดเร็วขึ้นชั่วคราว,
,event_wild_animal,Survival,8,เจอสัตว์ป่า อาจได้ทรัพยากรหรือบาดเจ็บ,jungle
,event_npc_argument,Social,6,NPC สองคนทะเลาะกัน เผยข้อมูลความสัมพันธ์,
,event_npc_help_request,Social,5,NPC ขอความช่วยเหลือ (อาจเป็นกับดัก),
```

---

## อ้างอิงภายนอก

- [[game_design_doc]] — `marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md` (แหล่งความจริงของทุก design decision)
- `AGENTS.md` (root) — กฎการทำงานกับ repo นี้ (wiki-first workflow, port 3216, sync-shared)
- [[Lab-A]] — สถานะปัจจุบัน; แผนถัดไป Lab B–F ตาม GDD §8
