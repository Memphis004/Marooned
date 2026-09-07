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
        private readonly McpMainThreadDispatcher _mainThread;

        public ExploreLocationHandler(ExplorationSystem exploration, McpMainThreadDispatcher mainThread)
        {
            _exploration = exploration;
            _mainThread = mainThread;
        }

        public async UniTask<ExploreLocationResponse> InvokeAsync(ExploreLocationRequest request, CancellationToken cancellationToken = default)
        {
            // Test C fix: handler ถูก invoke บน TCP background thread — body ต้อง
            // รันบน main thread เพราะ publish chain (inventory → UI re-render,
            // location → chibi spawn) แตะ Unity API
            return await _mainThread.EnqueueAsync(() =>
            {
                var (success, found) = _exploration.Explore(request.LocationId);
                return new ExploreLocationResponse
                {
                    Success = success,
                    FoundCardIds = found,
                    TriggeredEventId = "" // wire up WorldEventSystem roll here in Lab A
                };
            });
        }
    }

    public class CraftCardHandler : IAsyncRequestHandler<CraftCardRequest, CraftCardResponse>
    {
        private readonly CraftingSystem _crafting;
        private readonly McpMainThreadDispatcher _mainThread;

        public CraftCardHandler(CraftingSystem crafting, McpMainThreadDispatcher mainThread)
        {
            _crafting = crafting;
            _mainThread = mainThread;
        }

        public async UniTask<CraftCardResponse> InvokeAsync(CraftCardRequest request, CancellationToken cancellationToken = default)
        {
            return await _mainThread.EnqueueAsync(() =>
            {
                var (success, reason, output) = _crafting.TryCraft(request.RecipeId);
                return new CraftCardResponse { Success = success, FailureReason = reason, OutputCardId = output };
            });
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
        private readonly McpMainThreadDispatcher _mainThread;

        public AccuseNpcHandler(DeductionSystem deduction, McpMainThreadDispatcher mainThread)
        {
            _deduction = deduction;
            _mainThread = mainThread;
        }

        public async UniTask<AccuseNpcResponse> InvokeAsync(AccuseNpcRequest request, CancellationToken cancellationToken = default)
        {
            return await _mainThread.EnqueueAsync(() =>
            {
                var (correct, win, loss, text) = _deduction.Accuse(request.TargetNpcId);
                return new AccuseNpcResponse { WasCorrect = correct, GameOverWin = win, GameOverLoss = loss, ResultText = text };
            });
        }
    }

    public class GetGameStateHandler : IAsyncRequestHandler<GetGameStateRequest, GetGameStateResponse>
    {
        private readonly GameStateProvider _stateProvider;
        public GetGameStateHandler(GameStateProvider stateProvider) => _stateProvider = stateProvider;

        public UniTask<GetGameStateResponse> InvokeAsync(GetGameStateRequest request, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new GetGameStateResponse { Player = _stateProvider.GetPlayer() });
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
            var npcs = _deduction.GetObservableNpcsAt(_stateProvider.GetPlayer().CurrentLocationId);
            return UniTask.FromResult(new GetVisibleNpcsResponse { Npcs = npcs });
        }
    }

    public class GetClueBoardHandler : IAsyncRequestHandler<GetClueBoardRequest, GetClueBoardResponse>
    {
        private readonly GameStateProvider _stateProvider;
        public GetClueBoardHandler(GameStateProvider stateProvider) => _stateProvider = stateProvider;

        public UniTask<GetClueBoardResponse> InvokeAsync(GetClueBoardRequest request, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new GetClueBoardResponse { CollectedClueCardIds = _stateProvider.GetPlayer().CollectedClueCardIds });
        }
    }

    public class MoveToLocationHandler : IAsyncRequestHandler<MoveToLocationRequest, MoveToLocationResponse>
    {
        private readonly GameStateProvider _stateProvider;
        private readonly LubanDataService _data;
        private readonly IPublisher<PlayerLocationChangedMessage> _playerLocationPublisher;
        private readonly McpMainThreadDispatcher _mainThread;

        public MoveToLocationHandler(GameStateProvider stateProvider, LubanDataService data,
            IPublisher<PlayerLocationChangedMessage> playerLocationPublisher, McpMainThreadDispatcher mainThread)
        {
            _stateProvider = stateProvider;
            _data = data;
            _playerLocationPublisher = playerLocationPublisher;
            _mainThread = mainThread;
        }

        public async UniTask<MoveToLocationResponse> InvokeAsync(MoveToLocationRequest request, CancellationToken cancellationToken = default)
        {
            // publish chain แตะ Unity API (ChibiSpawnerView spawn/despawn chibi)
            return await _mainThread.EnqueueAsync(() =>
            {
                if (!_data.LocationDefs.TryGetValue(request.LocationId, out var targetDef))
                    return new MoveToLocationResponse { Success = false, FailureReason = "unknown_location" };

                var current = _stateProvider.GetPlayer().CurrentLocationId;
                if (!string.IsNullOrEmpty(current)
                    && _data.LocationDefs.TryGetValue(current, out var currentDef)
                    && currentDef.ConnectedLocationIds != null
                    && !currentDef.ConnectedLocationIds.Contains(request.LocationId))
                {
                    return new MoveToLocationResponse { Success = false, FailureReason = "not_connected" };
                }

                _stateProvider.GetPlayer().CurrentLocationId = request.LocationId;

                // Lab B: publish เพื่อให้ Visual layer (ChibiSpawnerView) รู้ตัวแทนการ polling
                _playerLocationPublisher.Publish(new PlayerLocationChangedMessage { OldLocationId = current, NewLocationId = request.LocationId });

                return new MoveToLocationResponse { Success = true };
            });
        }
    }

    /// <summary>
    /// Phase 4 (Player-as-Killer): รองรับ weapon card ผ่าน NpcDirectorSystem.TryEliminate
    /// Order of Operations: ตรวจ card → ตรวจ targeting → เช็คเงื่อนไข (dry-run) → ค่อยหักการ์ด
    /// → ดำเนินการจริง — เช็คก่อนหักเสมอเพื่อกันการ์ดหายฟรี (Safe UX)
    /// </summary>
    public class UseCardHandler : IAsyncRequestHandler<UseCardRequest, UseCardResponse>
    {
        private readonly GameStateProvider _stateProvider;
        private readonly CardInventorySystem _inventory;
        private readonly LubanDataService _data;
        private readonly NpcDirectorSystem _npcDirector; // ใหม่ Phase 4 — inject ผ่าน constructor
        private readonly McpMainThreadDispatcher _mainThread;

        public UseCardHandler(GameStateProvider stateProvider, CardInventorySystem inventory,
            LubanDataService data, NpcDirectorSystem npcDirector, McpMainThreadDispatcher mainThread)
        {
            _stateProvider = stateProvider;
            _inventory = inventory;
            _data = data;
            _npcDirector = npcDirector;
            _mainThread = mainThread;
        }

        public async UniTask<UseCardResponse> InvokeAsync(UseCardRequest request, CancellationToken cancellationToken = default)
        {
            // publish chain (inventory → UI, eliminate → chibi despawn) แตะ Unity API
            return await _mainThread.EnqueueAsync(() =>
            {
                if (!_data.CardDefs.TryGetValue(request.CardId, out var def))
                    return new UseCardResponse { Success = false, FailureReason = "unknown_card" };

                // Phase 4: ตรวจ targeting requirement ก่อน
                if (def.TargetType == CardTargetType.SingleTarget && string.IsNullOrEmpty(request.TargetId))
                    return new UseCardResponse { Success = false, FailureReason = "missing_target" };
                if (def.TargetType == CardTargetType.Self && !string.IsNullOrEmpty(request.TargetId))
                    return new UseCardResponse { Success = false, FailureReason = "invalid_target_type" };

                // Phase 4 (Safe UX): เช็คเงื่อนไข elimination ก่อนหักการ์ด — CanEliminate เป็น
                // dry-run ไม่ mutate state ทำให้การ์ดไม่หายเมื่อลงมือไม่สำเร็จ (witnessed ฯลฯ)
                if (def.EffectType == CardEffectType.Eliminate)
                {
                    var player = _stateProvider.GetPlayer();
                    var (canEliminate, failReason) = _npcDirector.CanEliminate(
                        GameStateProvider.LocalPlayerId, request.TargetId, player.CurrentLocationId);
                    if (!canEliminate)
                        return new UseCardResponse { Success = false, FailureReason = failReason };
                }

                // ผ่านเงื่อนไขทั้งหมดแล้วค่อยหักการ์ด
                if (!_inventory.TryConsume(request.CardId, 1))
                    return new UseCardResponse { Success = false, FailureReason = "not_in_inventory" };

                // ดำเนินการจริง
                switch (def.EffectType)
                {
                    case CardEffectType.Eliminate:
                    {
                        var player = _stateProvider.GetPlayer();
                        var (success, reason) = _npcDirector.TryEliminate(
                            GameStateProvider.LocalPlayerId, request.TargetId, player.CurrentLocationId);
                        if (!success)
                            return new UseCardResponse { Success = false, FailureReason = reason };
                        return new UseCardResponse { Success = true, ResultText = $"eliminated_{request.TargetId}" };
                    }

                    case CardEffectType.StatDelta:
                    default:
                    {
                        if (def.StatEffect != null)
                        {
                            var player = _stateProvider.GetPlayer();
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
                        return new UseCardResponse { Success = true };
                    }
                }
            });
        }
    }

    public class CallMeetingHandler : IAsyncRequestHandler<CallMeetingRequest, CallMeetingResponse>
    {
        private readonly DeductionSystem _deduction;
        private readonly GameStateProvider _stateProvider;
        private readonly McpMainThreadDispatcher _mainThread;

        public CallMeetingHandler(DeductionSystem deduction, GameStateProvider stateProvider,
            McpMainThreadDispatcher mainThread)
        {
            _deduction = deduction;
            _stateProvider = stateProvider;
            _mainThread = mainThread;
        }

        public async UniTask<CallMeetingResponse> InvokeAsync(CallMeetingRequest request, CancellationToken cancellationToken = default)
        {
            // Lab A: meeting just snapshots whoever is currently visible; a real
            // "gather everyone" pause/summon step is a later lab.
            // (อ่าน state เปล่าๆ — แต่กัน race กับ main thread ที่ mutate dict ด้วย
            // เพราะ handler ถูก invoke บน TCP thread เหมือนกัน)
            return await _mainThread.EnqueueAsync(() =>
            {
                var npcs = _deduction.GetObservableNpcsAt(_stateProvider.GetPlayer().CurrentLocationId);
                return new CallMeetingResponse { Success = true, AttendingNpcs = npcs };
            });
        }
    }
}
