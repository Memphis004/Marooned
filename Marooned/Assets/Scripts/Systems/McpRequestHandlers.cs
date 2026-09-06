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
        private readonly IPublisher<PlayerLocationChangedMessage> _playerLocationPublisher;

        public MoveToLocationHandler(GameStateProvider stateProvider, LubanDataService data,
            IPublisher<PlayerLocationChangedMessage> playerLocationPublisher)
        {
            _stateProvider = stateProvider;
            _data = data;
            _playerLocationPublisher = playerLocationPublisher;
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

            // Lab B: publish เพื่อให้ Visual layer (ChibiSpawnerView) รู้ตัวแทนการ polling
            _playerLocationPublisher.Publish(new PlayerLocationChangedMessage { OldLocationId = current, NewLocationId = request.LocationId });

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
