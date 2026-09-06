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

            // --- Player system (Lab B Phase 3) ---
            // input service (plain C# ticked by GameTickDriver) + movement + pickup
            builder.Register<PlayerInputService>(Lifetime.Singleton).AsSelf();
            builder.Register<PlayerMovementSystem>(Lifetime.Singleton).AsSelf();
            builder.Register<ItemPickupSystem>(Lifetime.Singleton).AsSelf();
            // WorldItemSystem เป็น IInitializable → EntryPoint เพื่อให้ spawn ชุดแรก
            // หลัง container build เสร็จ (subscribe PlayerLocationChangedMessage ภายใน)
            // .AsSelf() เพิ่มการ register ตัวคลาสเอง (RegisterEntryPoint พื้นฐาน
            // register เฉพาะ implemented interfaces ทำให้ Resolve<WorldItemSystem> ไม่ได้)
            builder.RegisterEntryPoint<WorldItemSystem>(Lifetime.Singleton).AsSelf();

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
