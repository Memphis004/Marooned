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
