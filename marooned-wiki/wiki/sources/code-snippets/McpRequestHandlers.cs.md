---
title: McpRequestHandlers
type: snippet
sources: ["[[sources/mcprequesthandlers-cs]]"]
related:
  - MessagePipe-Interprocess
  - "[[mcp-bridge]]"
  - "[[DeductionSystem.cs]]"
  - "[[ExplorationSystem.cs]]"
  - "[[CraftingSystem.cs]]"
  - "[[WorldEventSystem.cs]]"
  - "[[LubanDataService.cs|GameStateProvider]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[card-system]]"
folder: Systems
lines: 252
created: 2026-09-05
tags:
  - Systems
  - marooned
  - lab-a
  - phase-4
---

# McpRequestHandlers.cs
**Path:** `Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs` (252 lines)

## Source
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

            var current = _stateProvider.GetPlayer().CurrentLocationId;
            if (!string.IsNullOrEmpty(current)
                && _data.LocationDefs.TryGetValue(current, out var currentDef)
                && currentDef.ConnectedLocationIds != null
                && !currentDef.ConnectedLocationIds.Contains(request.LocationId))
            {
                return UniTask.FromResult(new MoveToLocationResponse { Success = false, FailureReason = "not_connected" });
            }

            _stateProvider.GetPlayer().CurrentLocationId = request.LocationId;

            // Lab B: publish เพื่อให้ Visual layer (ChibiSpawnerView) รู้ตัวแทนการ polling
            _playerLocationPublisher.Publish(new PlayerLocationChangedMessage { OldLocationId = current, NewLocationId = request.LocationId });

            return UniTask.FromResult(new MoveToLocationResponse { Success = true });
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

        public UseCardHandler(GameStateProvider stateProvider, CardInventorySystem inventory,
            LubanDataService data, NpcDirectorSystem npcDirector)
        {
            _stateProvider = stateProvider;
            _inventory = inventory;
            _data = data;
            _npcDirector = npcDirector;
        }

        public UniTask<UseCardResponse> InvokeAsync(UseCardRequest request, CancellationToken cancellationToken = default)
        {
            if (!_data.CardDefs.TryGetValue(request.CardId, out var def))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "unknown_card" });

            // Phase 4: ตรวจ targeting requirement ก่อน
            if (def.TargetType == CardTargetType.SingleTarget && string.IsNullOrEmpty(request.TargetId))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "missing_target" });
            if (def.TargetType == CardTargetType.Self && !string.IsNullOrEmpty(request.TargetId))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "invalid_target_type" });

            // Phase 4 (Safe UX): เช็คเงื่อนไข elimination ก่อนหักการ์ด — CanEliminate เป็น
            // dry-run ไม่ mutate state ทำให้การ์ดไม่หายเมื่อลงมือไม่สำเร็จ (witnessed ฯลฯ)
            if (def.EffectType == CardEffectType.Eliminate)
            {
                var player = _stateProvider.GetPlayer();
                var (canEliminate, failReason) = _npcDirector.CanEliminate(
                    GameStateProvider.LocalPlayerId, request.TargetId, player.CurrentLocationId);
                if (!canEliminate)
                    return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = failReason });
            }

            // ผ่านเงื่อนไขทั้งหมดแล้วค่อยหักการ์ด
            if (!_inventory.TryConsume(request.CardId, 1))
                return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = "not_in_inventory" });

            // ดำเนินการจริง
            switch (def.EffectType)
            {
                case CardEffectType.Eliminate:
                {
                    var player = _stateProvider.GetPlayer();
                    var (success, reason) = _npcDirector.TryEliminate(
                        GameStateProvider.LocalPlayerId, request.TargetId, player.CurrentLocationId);
                    if (!success)
                        return UniTask.FromResult(new UseCardResponse { Success = false, FailureReason = reason });
                    return UniTask.FromResult(new UseCardResponse { Success = true, ResultText = $"eliminated_{request.TargetId}" });
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
                    return UniTask.FromResult(new UseCardResponse { Success = true });
                }
            }
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
            var npcs = _deduction.GetObservableNpcsAt(_stateProvider.GetPlayer().CurrentLocationId);
            return UniTask.FromResult(new CallMeetingResponse { Success = true, AttendingNpcs = npcs });
        }
    }
}
```

# McpRequestHandlers


## Purpose
คือ **ปลายทางฝั่ง Unity ของทุก request ที่เดินทางมาจาก McpBridge** — 10 class ที่ implement
`IAsyncRequestHandler<TReq,TRes>` แปลง request จาก AI (ผ่าน TCP) ให้เป็นการเรียก gameplay
system ที่เหมาะสม แล้วคืน response กลับ

## Public API (10 handler classes)
| Class | Request → Response | หน้าที่ / system ที่เรียก |
| --- | --- | --- |
| `ExploreLocationHandler` | `ExploreLocationRequest/Response` | [[ExplorationSystem.cs]].Explore — การ์ดที่พบ |
| `CraftCardHandler` | `CraftCardRequest/Response` | [[CraftingSystem.cs]].TryCraft |
| `AwaitNextEventHandler` | `AwaitNextEventRequest/Response` | [[WorldEventSystem.cs]].TryDequeue |
| `AccuseNpcHandler` | `AccuseNpcRequest/Response` | [[DeductionSystem.cs]].Accuse |
| `GetGameStateHandler` | `GetGameStateRequest/Response` | คืน `PlayerSurvivalState` เต็ม |
| `GetVisibleNpcsHandler` | `GetVisibleNpcsRequest/Response` | [[DeductionSystem.cs]].GetObservableNpcsAt ที่ตำแหน่งผู้เล่น |
| `GetClueBoardHandler` | `GetClueBoardRequest/Response` | คืน `CollectedClueCardIds` |
| `MoveToLocationHandler` | `MoveToLocationRequest/Response` | ตรวจ connectivity จาก `LocationDefs` แล้วย้าย |
| `UseCardHandler` | `UseCardRequest/Response` | ตรวจ targeting → [[NpcDirectorSystem.cs]].CanEliminate/TryEliminate (weapon, Phase 4) หรือ [[CardInventorySystem.cs]].TryConsume + apply `StatEffect` |
| `CallMeetingHandler` | `CallMeetingRequest/Response` | Snapshot NPC ที่มองเห็น (Lab A placeholder) |

ทุก method มี signature: `UniTask<TRes> InvokeAsync(TReq request, CancellationToken ct = default)`

## Dependencies
- **MessagePipe (Unity build)** — ⚠️ สำคัญ (comment หัวไฟล์): ฝั่ง Unity แทน `ValueTask<T>`
  ด้วย **UniTask<T>** (ต้องมี UniTask package) ต่างจากฝั่ง .NET ของ McpBridge —
  **ห้าม copy ไฟล์นี้ไป McpBridge/**
- Gameplay systems: [[DeductionSystem.cs]], [[ExplorationSystem.cs]], [[CraftingSystem.cs]],
  [[WorldEventSystem.cs]], [[CardInventorySystem.cs]]
- **GameStateProvider** + **LubanDataService**
- Message types จาก `Marooned/Assets/Scripts/Shared/GameMessages.cs`
- Register ครบ 10 คู่ที่ [[GameLifetimeScope.cs]] (RegisterAsyncRequestHandler)

## Key Logic
- ทุก handler เป็น **synchronous logic ห่อด้วย `UniTask.FromResult`** — ไม่มี await จริง
- `GetVisibleNpcsHandler` / `CallMeetingHandler`: ใช้ตำแหน่งผู้เล่นเป็นตัวกำหนด (request ไม่มี
  พารามิเตอร์) และกรองผ่าน `DeductionSystem` เสมอ — ground truth ไม่หลุด
- `MoveToLocationHandler`: ปฏิเสธด้วย `unknown_location` ถ้า id ไม่มี และ `not_connected`
  ถ้าไม่อยู่ใน `ConnectedLocationIds` ของ location ปัจจุบัน (การเดินทางเป็น graph-based)
- `UseCardHandler` (Phase 4): **Order of Operations — เช็คก่อนหักการ์ดเสมอ (Safe UX)**:
  1. `unknown_card` — id ไม่มีใน CardDefs
  2. targeting: `SingleTarget` ไม่มี `TargetId` → `missing_target`; `Self` แต่ส่ง `TargetId`
     มา → `invalid_target_type`
  3. ถ้า `EffectType == Eliminate` (weapon): dry-run `NpcDirectorSystem.CanEliminate` —
     ไม่ผ่าน (`witnessed`, `target_not_same_location`, `target_already_dead`, `unknown_target`)
     คืน fail **โดยการ์ดยังไม่หาย**
  4. ผ่านทุกเงื่อนไขค่อย `TryConsume` → ลงมือจริงผ่าน `TryEliminate` (ผล `ResultText =
     eliminated_<npcId>`) หรือวน `StatEffect` dictionary apply ตาม key (Hunger/Thirst/Mood/
     Fatigue) ด้วย clamp 0–100

## TODO / Known Issues
- `AwaitNextEventHandler` เป็น **naive poll** — ไม่มี async wait จริง (ควร signal จาก
  `WorldEventSystem.Tick`) และ**ไม่เคารพ `TimeoutSeconds`** — รอแก้ Lab E
  (`McpRequestHandlers.cs:50-58`)
- `ExploreLocationResponse.TriggeredEventId` ส่งค่าว่างเสมอ (`McpRequestHandlers.cs:28`)
- `CallMeetingHandler` แค่ snapshot คนที่มองเห็น — ไม่มี pause/summon จริง
  (`McpRequestHandlers.cs:194-197`)
- `UseCardHandler`: `CardEffectType.Cure` ยังไม่ implement — ลง path StatDelta เดิม
  (ลบ condition card / รักษา illness ด้วย CureCardId ยังไม่ถูกใช้)
- Handler ยังไม่ validate สถานะเกม (เช่น explore ตอนตายแล้ว, accuse นอก meeting)
