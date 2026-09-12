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
        public string Id = string.Empty;
        public string Slot = string.Empty;       // "body","head","hair","arm_left","arm_right","leg_left","leg_right","accessory","overlay"
        public int DrawOrder;     // layering within the rig, same idea as reference project's drawOrder
        public string SexTag = string.Empty;     // "", "Male", "Female" — filtering only, same convention as before

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
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string SexTag = string.Empty;
        public Dictionary<string, string> Parts = new(); // slot -> partId, must all share compatible frame keys
        public string ThumbPath = string.Empty;
    }
}
