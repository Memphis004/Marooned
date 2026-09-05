---
title: GameLifetimeScope
type: snippet
sources: [Marooned/Assets/Scripts/Core/GameLifetimeScope.cs]
related:
  - VContainer
  - MessagePipe
  - MessagePipe-Interprocess
  - "[[LubanDataService.cs|GameStateProvider]]"
  - "[[McpRequestHandlers.cs]]"
  - "[[UIRoot.cs]]"
folder: Core
lines: 90
created: 2026-09-05
tags:
  - Core
  - marooned
  - lab-a
---

# GameLifetimeScope.cs
**Path:** `Marooned/Assets/Scripts/Core/GameLifetimeScope.cs` (90 lines)

## Source
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

# GameLifetimeScope


## Purpose
Composition Root ของเกม — จุดเดียวที่ประกอบร่างทั้งหมดด้วย VContainer: ผูก MessagePipe
(in-process bus) + MessagePipe.Interprocess (TCP ฝั่ง server) + gameplay systems ทั้งหมด +
MCP-facing request handlers + UI entry point รูปร่างเดียวกับ `GameLifetimeScope` ของโปรเจคอ้างอิง
Cultivation-Together

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `protected override void Configure(IContainerBuilder builder)` | Override หลักของ `LifetimeScope` — register ทุก dependency ของเกม |
| `[SerializeField] string interprocessHost` | Host ของ TCP interprocess (default `"127.0.0.1"`) — ปรับได้จาก Inspector |
| `[SerializeField] int interprocessPort` | Port ของ TCP interprocess (default `3216` — ตั้งใจแยกจากโปรเจคอ้างอิงที่ใช้ 3215) |

## Dependencies
- **VContainer** — สืบทอด `LifetimeScope`, ใช้ `IContainerBuilder`, `Lifetime`, `RegisterEntryPoint`
- **MessagePipe** — `RegisterMessagePipe()`, `GlobalMessagePipe.SetProvider(...)`
- **MessagePipe.Interprocess** — `ToMessagePipeBuilder().AddTcpInterprocess(...)`, `Workers.TcpWorker`
- Register ให้ systems ทั้งหมด: `SurvivalStatSystem`, `CardInventorySystem`, `CraftingSystem`,
  `ExplorationSystem`, `NpcDirectorSystem`, `DeductionSystem`, `WorldEventSystem`,
  `LubanDataService`, `GameStateProvider` (Singleton ทั้งหมด) และ `UIRoot` เป็น EntryPoint
- อ้าง message/handler types จาก `Marooned.Shared` + `Marooned.Systems`

## Key Logic
`Configure()` ทำ 4 ขั้นตอนตามลำดับ:
1. `builder.RegisterMessagePipe()` แล้ว `RegisterBuildCallback` ผูก `GlobalMessagePipe.SetProvider`
   เพื่อเปิด MessagePipe Diagnostics window
2. `messagePipeBuilder.AddTcpInterprocess(host, port, tcp => tcp.HostAsServer = true)` —
   **Unity เป็น TCP server** (ฝั่ง McpBridge เป็น client `HostAsServer = false`)
3. `RegisterBuildCallback` ที่ `container.Resolve<TcpWorker>()` — **บังคับ VContainer สร้าง
   TcpWorker จริง** ไม่งั้นจะ register ไว้เฉยๆ ไม่ยอม instantiate
4. Register ระบบเกมทั้งหมด + `RegisterAsyncRequestHandler<TReq,TRes,THandler>` ครบ **10 คู่**
   (ExploreLocation, CraftCard, AwaitNextEvent, AccuseNpc, GetGameState, GetVisibleNpcs,
   GetClueBoard, MoveToLocation, UseCard, CallMeeting) — Unity/IL2CPP ไม่มี open-generics
   auto-registration จึงต้อง register manual ทีละคู่ ต่างจากฝั่ง .NET ของ McpBridge
   ที่ได้ `IRemoteRequestHandler` ฟรีหลัง config client

สำคัญ: ฝั่ง server แค่ register async handler ก็ทำให้เรียกผ่าน network ได้เอง
(ไม่ต้องมีคำสั่ง "expose over TCP" แยก) ตาม README ของ MessagePipe.Interprocess 1.8.2

## TODO / Known Issues
- **STILL VERIFY** (comment ในไฟล์เอง บรรทัด 29-32): ชื่อ/ลำดับ overload ของ
  MessagePipe.Interprocess อาจต่างกันตามเวอร์ชัน — โค้ดนี้เขียนตาม README 1.8.2 แต่
  **ยังไม่เคยผ่านการ build จริงใน Unity** — ถ้า compiler ฟ้อง ให้เชื่อ compiler/IntelliSense
- `CraftingSystem` ถูก register แยกจาก `CardInventorySystem` ทั้งที่อยู่ไฟล์เดียวกัน
  (`Marooned/Assets/Scripts/Systems/CardInventorySystem.cs`) — ทำงานได้แต่ควรแยกไฟล์
- ยังไม่มี registration ของ game loop/tick driver (ดู [[SurvivalStatSystem.cs]] Known Issues)
