---
title: mcp-bridge
type: architecture
sources:
  - McpBridge/Program.cs
  - McpBridge/McpBridge.csproj
  - McpBridge/Shared/GameMessages.cs
  - Marooned/Assets/Scripts/Core/GameLifetimeScope.cs
  - Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs
  - marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md
related:
  - "[[overview]]"
  - "[[game_design_doc]]"
  - "[[GameLifetimeScope.cs]]"
  - "[[McpRequestHandlers.cs]]"
  - "[[mcp-tool-table]]"
  - MessagePipe-Interprocess
folder: architecture
created: 2026-09-05
tags:
  - architecture
  - mcp
  - marooned
  - lab-a
---

# MCP Bridge Architecture

## 1. MCP Bridge คืออะไร

**MCP Bridge** (`McpBridge/Program.cs`) คือ .NET 8 console app ที่ทำหน้าที่เป็น **ล่าม
สองภาษา** ของโปรเจค: ด้านหนึ่งคุยกับ AI VTuber ผ่าน **MCP (Model Context Protocol) แบบ
stdio** อีกด้านคุยกับ Unity ผ่าน **MessagePipe.Interprocess แบบ TCP** — มันแปลงคำสั่ง
ระดับ "เกม" ของ AI (เช่น `explore_location('beach')`) ให้เป็น request ที่ Unity เข้าใจ
และแปลงผลลัพธ์กลับเป็นข้อความที่ LLM อ่านและตัดสินใจต่อได้

- **ความสัมพันธ์กับ Unity:** Unity เป็น **TCP server** (`HostAsServer = true` ใน
  `GameLifetimeScope.cs`) ส่วน Bridge เป็น **TCP client** (`HostAsServer = false` ใน
  `Program.cs:24-27`) — Bridge ไม่ถือ game state เองเลย ทุกคำถามต้อง round-trip ไป Unity
- **ทำไมต้องแยก .NET console app:** MCP SDK (`ModelContextProtocol` 2.2.0) เป็น pure .NET
  ที่รัน stdio server ได้ตรง ๆ แต่ Unity ฝัง stdio process ไม่ได้ — แยก process ทำให้
  AI client (เช่น Claude Desktop) spawn Bridge ได้ง่าย และ Unity ไม่ต้องรู้จัก MCP เลย
  (รู้จักแค่ MessagePipe.Interprocess ที่ใช้อยู่แล้ว) — สืบทอดแนวทางจากโปรเจคอ้างอิง
  Cultivation-Together
- **ข้อควรระวังสำคัญ** (`Program.cs:13-16`): stdout ถูกใช้เป็นช่อง JSON-RPC framing ของ
  stdio MCP จึงต้องบังคับ log ทั้งหมดลง **stderr** (`LogToStandardErrorThreshold =
  LogLevel.Trace`) — ไม่งั้น MCP client parse stream ไม่ได้

## 2. Tool Surface ทั้งหมด

Tool ทั้งหมดอยู่ใน `McpBridge/Program.cs` จัดกลุ่มเป็น 3 class (ติด
`[McpServerToolType]` เพื่อให้ SDK auto-discover จาก assembly — **ไม่ต้อง register
มือ**):

- **`SurvivalQueryTools`** (read-only) — `GetGameState`, `GetVisibleNpcs`, `GetClueBoard`
  ใช้ `IRemoteRequestHandler<TReq,TRes>` inject ผ่าน constructor (plain .NET ได้
  auto-registration ฟรีต่างจากฝั่ง Unity) ทุก tool คืนค่าเป็นข้อความ human/LLM-readable
- **`SurvivalActionTools`** (mutating) — `ExploreLocation`, `CraftCard`, `MoveToLocation`,
  `UseCard`, `AwaitNextEvent` แต่ละตัว round-trip ไป Unity handler ก่อนตอบ
- **`DeductionTools`** — `CallMeeting`, `AccuseNpc` รวมถึง logic win/lose ของเกม
  (`GameOverWin`/`GameOverLoss` ใน response)

กฎความปลอดภัยข้อมูล (comment ใน `Program.cs:39-43`): query tools เห็นเฉพาะสิ่งที่
`DeductionSystem.GetObservableNpcsAt` สร้างให้ฝั่ง Unity — true `NpcRole` ไม่ข้ามเส้นนี้
by construction

รายละเอียด tool ทุกตัวดู [[mcp-tool-table]]

## 3. TCP Connection

- **Port:** `3216` (hardcode ใน `Program.cs:24` — ต้องตรงกับ `interprocessPort` ใน
  `GameLifetimeScope.cs` ซึ่ง default เท่ากัน; ตั้งใจแยกจากโปรเจคอ้างอิงที่ใช้ 3215)
- **Protocol:** MessagePipe-Interprocess 1.8.2 — Request/Response serialization ด้วย
  MessagePack (`[MessagePackObject]` / `[Key(n)]` ใน `McpBridge/Shared/GameMessages.cs`)
- **Server/Client:** Unity = server (host), Bridge = client — Bridge เปิด connection
  ตอนสตาร์ท process
- **ลำดับการ start (สำคัญ):**
  1. เปิด Unity แล้วกด **Play mode** (Unity ต้อง resolve `TcpWorker` ก่อน —
     `GameLifetimeScope.cs` มี `RegisterBuildCallback` บังคับสร้างจริง)
  2. ค่อยรัน Bridge — ถ้า Bridge สตาร์ทก่อนจะ **connect fail** (comment ใน
     `Program.cs:18-22`)

## 4. วิธี Build และ Run

Build:
```bash
cd McpBridge
dotnet build
```

Run (ต้องเปิด Unity Play mode ก่อน):
```bash
cd McpBridge
dotnet run
```

**Dependencies** (ใน `McpBridge/McpBridge.csproj` — pin ไว้แล้ว):
- `MessagePipe` 1.8.2 + `MessagePipe.Interprocess` 1.8.2 (TCP transport)
- `ModelContextProtocol` 2.2.0 (MCP server SDK)
- `Microsoft.Extensions.Hosting` 10.0.11 (host builder)

**วิธีตรวจสอบว่าเชื่อมต่อสำเร็จ:**
- Bridge สตาร์ทโดยไม่ throw TCP connection error
- เรียก tool ง่ายสุดผ่าน MCP client เช่น `GetGameState` — ถ้าได้ข้อความ
  `Location: beach | Alive: True | ...` (mock starting state) แปลว่า round-trip
  Unity สำเร็จ (นี่คือ milestone "first round trip" ของ Lab A)
- หาก connection ไม่ผ่าน: เช็คว่า Unity กำลัง Play อยู่, port ตรงกันทั้งสองฝั่ง
  (3216) และ `HostAsServer` ตั้งถูกฝั่ง

## 5. วิธีเพิ่ม MCP Tool ใหม่ (Step-by-Step)

> หมายเหตุ: ปัจจุบัน tool ทั้งหมดอยู่ใน `Program.cs` — เมื่อ tool เยอะขึ้นควรย้ายแต่ละ
> tool class ไปไฟล์แยกใน `McpBridge/Tools/` (SDK auto-discover ทั้ง assembly อยู่แล้ว
> การแยกไฟล์ไม่ต้อง register เพิ่ม)

1. **เพิ่ม Request/Response messages** ใน `Shared/GameMessages.cs` (canonical) —
   `[MessagePackObject]` + `[Key(n)]` ครบทุก field → รัน `./sync-shared.sh`
2. **สร้าง tool class** (แนะนำไฟล์ใหม่ใน `McpBridge/Tools/` เช่น
   `Tools/MyNewTools.cs`) — ติด `[McpServerToolType]` ที่ class, `[McpServerTool,
   Description("...")]` ที่ method แล้ว inject
   `IRemoteRequestHandler<MyRequest, MyResponse>` ใน constructor
3. **เพิ่ม handler ฝั่ง Unity** ใน `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs`
   — implement `IAsyncRequestHandler<TReq,TRes>` (เวอร์ชัน **UniTask** ไม่ใช่ ValueTask!)
4. **Register ใน `GameLifetimeScope.cs`** — เพิ่มบรรทัด
   `builder.RegisterAsyncRequestHandler<TReq,TRes,THandler>(options);`
   (ฝั่ง Bridge **ไม่ต้อง** register — SDK auto-discover)
5. **อัปเดตเอกสาร** ใน [[mcp-tool-table]] (ตารางหลัก + คำอธิบาย)
6. **ทดสอบ** — `dotnet build` ฝั่ง Bridge, เข้า Unity Play mode, เรียก tool ใหม่ผ่าน
   MCP client แล้วตรวจ response; ถ้า compiler ฝั่ง Unity ฟ้อง signature ให้เชื่อ compiler
   ก่อน comment ใน `GameLifetimeScope.cs` (ยังอยู่ในสถานะ STILL VERIFY)

## 6. สถานะปัจจุบัน

**ไม่มี method ใดเป็น `NotImplementedException` แล้ว** (สถานะ "McpBridge tool methods are
NotImplementedException" ใน `AGENTS.md` ล้าสมัยแล้ว — โค้ดปัจจุบัน implement ครบ 10 tools)

| Tool | Status | Notes |
| --- | --- | --- |
| GetGameState | ✅ | คืน mock starting state (beach + coconut) |
| GetVisibleNpcs | ✅ | ผ่าน DeductionSystem — ปลอดภัยข้อมูล |
| GetClueBoard | ⚠️ | โค้ดถูก แต่ `CollectedClueCardIds` ไม่เคยถูกเติม → ตอบว่างเสมอ |
| ExploreLocation | ⚠️ | ทำงานได้; `TriggeredEventId` ยังส่งค่าว่าง |
| CraftCard | ⚠️ | ทำงานได้; recipe มีแค่ 1 สูตร (mock data) |
| MoveToLocation | ✅ | ตรวจ connectivity ครบ |
| UseCard | ✅ | apply StatEffect ครบ 4 stat |
| AwaitNextEvent | ⚠️ | naive poll — ไม่ block รอจริง, ไม่เคารพ `TimeoutSeconds` |
| CallMeeting | ⚠️ | แค่ snapshot คนที่มองเห็น — ไม่มี Meeting Phase จริง |
| AccuseNpc | ✅ | win/lose logic ครบ (ผ่าน DeductionSystem) |

**TODO:**
- ❌ Tools ตาม GDD §6 ที่ยังไม่มี: `talk_to_npc`, `observe_npc`, `report_body`
  (ปัจจุบันใช้ `CallMeeting` แทนชั่วคราว), `get_hand`/`get_inventory` แยกจาก
  `GetGameState`
- ❌ แยก tool classes ออกจาก `Program.cs` ไป `McpBridge/Tools/`
- ❌ เปลี่ยน `RootNamespace` ใน `McpBridge.csproj` จาก `Xianxia.Sect.Bridge`
  (เศษโปรเจคอ้างอิง)
- ❌ port/host ควรอ่านจาก config/args แทน hardcode 3216
