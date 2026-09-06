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

        [McpServerTool, Description("Consume/use a card from inventory (food, water, medicine, etc). For weapon cards (e.g. knife_basic) target_id is required -- the target NPC must be in the same location and there must be no other NPC witnessing, or the attempt fails and the card is not consumed.")]
        public async Task<string> UseCard(
            [Description("Card id to use")] string cardId,
            [Description("ID of the target NPC (required for weapon cards, e.g. 'npc_03'); omit for self-use cards")] string targetId = null)
        {
            var res = await _useCard.InvokeAsync(new UseCardRequest { CardId = cardId, TargetId = targetId });
            if (!res.Success) return $"Could not use {cardId}: {res.FailureReason}";
            return string.IsNullOrEmpty(res.ResultText) ? $"Used {cardId}." : $"Used {cardId}: {res.ResultText}";
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
