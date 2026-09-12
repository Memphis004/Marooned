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
        [Key(0)] public string LocationId = string.Empty;
    }

    [MessagePackObject]
    public class ExploreLocationResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public List<string> FoundCardIds = new();
        [Key(2)] public string TriggeredEventId = string.Empty; // may be empty
    }

    [MessagePackObject]
    public class CraftCardRequest
    {
        [Key(0)] public string RecipeId = string.Empty;
    }

    [MessagePackObject]
    public class CraftCardResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason = string.Empty;
        [Key(2)] public string OutputCardId = string.Empty;
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
        [Key(1)] public string EventId = string.Empty;
        [Key(2)] public string Group = string.Empty; // "Survival" | "Social"
        [Key(3)] public string DisplayText = string.Empty;
    }

    [MessagePackObject]
    public class AccuseNpcRequest
    {
        [Key(0)] public string TargetNpcId = string.Empty;
    }

    [MessagePackObject]
    public class AccuseNpcResponse
    {
        [Key(0)] public bool WasCorrect;
        [Key(1)] public bool GameOverWin;
        [Key(2)] public bool GameOverLoss;
        [Key(3)] public string ResultText = string.Empty;
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
        [Key(0)] public PlayerSurvivalState Player = new();
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
        [Key(0)] public string LocationId = string.Empty;
    }

    [MessagePackObject]
    public class MoveToLocationResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason = string.Empty;
    }

    [MessagePackObject]
    public class CancelMoveRequest
    {
    }

    [MessagePackObject]
    public class CancelMoveResponse
    {
        /// <summary>true = ยกเลิกการเดินที่กำลังรันอยู่; false = ไม่ได้เดินอยู่แล้ว (not_moving)</summary>
        [Key(0)] public bool Success;

        /// <summary>"" | "not_moving"</summary>
        [Key(1)] public string FailureReason = string.Empty;
    }

    [MessagePackObject]
    public class UseCardRequest
    {
        [Key(0)] public string CardId = string.Empty;

        // Phase 4 (Player-as-Killer): null/empty = Self; npc id = SingleTarget (เช่น weapon)
        [Key(1)] public string TargetId = string.Empty;
    }

    [MessagePackObject]
    public class UseCardResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason = string.Empty; // "missing_target", "witnessed", etc.
        [Key(2)] public string ResultText = string.Empty;    // เติมเฉพาะตอน Eliminate สำเร็จ
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
        [Key(0)] public string StatKey = string.Empty; // Hunger/Thirst/Mood/Fatigue
        [Key(1)] public float NewValue;
        [Key(2)] public float Delta;
    }

    [MessagePackObject]
    public class ConditionCardAppliedMessage
    {
        [Key(0)] public string TargetEntityId = string.Empty; // "player" or npcId
        [Key(1)] public string ConditionCardId = string.Empty;
    }

    [MessagePackObject]
    public class NpcEliminatedMessage
    {
        [Key(0)] public string VictimNpcId = string.Empty;
        [Key(1)] public string LocationId = string.Empty;
        [Key(2)] public List<string> SpawnedClueCardIds = new();
    }

    // ---- Added in Lab B (Chibi Sprite Integration): location-change broadcasts ----
    // In-process only (MessagePipe) — ใช้โดย ChibiSpawnerView เพื่อ spawn/despawn
    // chibi ตาม location ของผู้เล่น/NPC โดยไม่ต้อง polling ใน Update()

    [MessagePackObject]
    public class PlayerLocationChangedMessage
    {
        [Key(0)] public string OldLocationId = string.Empty;
        [Key(1)] public string NewLocationId = string.Empty;
    }

    [MessagePackObject]
    public class NpcLocationChangedMessage
    {
        [Key(0)] public string NpcId = string.Empty;
        [Key(1)] public string OldLocationId = string.Empty; // อาจเป็น null ตอนวาง NPC ครั้งแรกของรอบ
        [Key(2)] public string NewLocationId = string.Empty;
    }

    // ---- Added in Lab B Phase 3 (Player System): item pickup broadcast ----

    [MessagePackObject]
    public class ItemPickedUpMessage
    {
        [Key(0)] public string ItemId = string.Empty;
        [Key(1)] public string LocationId = string.Empty;
    }

    // ---- Added in Lab B Phase 5 (Card Hand UI): inventory change broadcast ----
    // CardInventorySystem publish ทุกครั้งที่ inventory เปลี่ยนจริง → CardHandPresenter
    // subscribe เพื่อ render มือการ์ดแบบ event-driven (แทน polling)

    [MessagePackObject]
    public class CardInventoryChangedMessage
    {
        [Key(0)] public string CardId = string.Empty;
        [Key(1)] public int NewCount; // จำนวนหลังเปลี่ยน (0 = หมด/ถูกลบออกจากมือ)
        [Key(2)] public int Delta;    // +เพิ่ม / -ลด
    }

    // ---- Added in Lab C Phase 1 (Hybrid BiomeScatter): harvestable node broadcast ----
    // In-process only (MessagePipe) — NodeHarvestSystem publish หลัง harvest สำเร็จ
    // 1 ครั้ง (ได้ไอเท็มเข้า inventory แล้ว) — Depleted=true เมื่อ durability หมด
    // (RegrowSeconds > 0 = จะกลับมาให้เก็บใหม่, 0 = หายถาวร)

    [MessagePackObject]
    public class NodeHarvestedMessage
    {
        [Key(0)] public string NodeId = string.Empty;
        [Key(1)] public string ItemId = string.Empty;
        [Key(2)] public int Count;
        [Key(3)] public bool Depleted;
        [Key(4)] public int RegrowSeconds;
    }

    // ---- Added in Lab C Phase 1.5 (MCP harvest_node): request/response ----
    // Bridge → Unity: NodeHarvestSystem ทำงานจริง — toolItemId ว่าง = ให้ระบบเลือก
    // tool ที่เหมาะสมจาก inventory เอง (ต่างจากกด E ที่หมายถึงมือเปล่า)

    [MessagePackObject]
    public class HarvestNodeRequest
    {
        // optional: specific tool card id (e.g. "tool_axe"); null/empty = auto-pick
        // the best matching tool from inventory (or bare hands if none needed)
        [Key(0)] public string ToolItemId = string.Empty;
    }

    [MessagePackObject]
    public class HarvestNodeResponse
    {
        [Key(0)] public bool Success;
        [Key(1)] public string FailureReason = string.Empty; // "no_node_in_range", "wrong_tool", "regrowing"
        [Key(2)] public string NodeId = string.Empty;        // node ที่พยายามเก็บ (เติมเมื่อเจอ node)
        [Key(3)] public string ItemId = string.Empty;        // yield card id (เติมเมื่อ Success)
        [Key(4)] public int Count;            // จำนวน yield (เติมเมื่อ Success)
        [Key(5)] public bool Depleted;        // durability หมดพอดี (เติมเมื่อ Success)
        [Key(6)] public int RegrowSeconds;    // > 0 เมื่อ Depleted และจะงอกใหม่ (เติมเมื่อ Success)
    }
}
