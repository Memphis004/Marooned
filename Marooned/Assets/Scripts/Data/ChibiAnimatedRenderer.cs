using System.Collections.Generic;
using Marooned.Shared;
using UnityEngine;

namespace Marooned.Data
{
    /// <summary>
    /// Replaces the reference project's single-image AvatarRenderer (portrait swap
    /// only) with a walking paperdoll: one SpriteRenderer per limb slot, each
    /// flipping through ChibiPartDef.FramesByAnimKey frames as the character walks
    /// around the 2D sandbox. No bones/skeleton needed — this is classic top-down
    /// RPG-style frame swapping per body part, composited by draw order exactly
    /// like the reference project's hair-front/hair-back layering.
    ///
    /// Layer stack (bottom to top), same DrawOrder convention as before:
    ///   leg_back(0) -> body(10) -> leg_front(15) -> arm_back(20) -> head(30)
    ///   -> hair_back(35) -> face_marking(38) -> hair_front(40) -> arm_front(45)
    ///   -> overlay/condition(50) -> accessory(60)
    /// </summary>
    public class ChibiAnimatedRenderer : MonoBehaviour
    {
        [SerializeField] private float framesPerSecond = 6f;

        private readonly Dictionary<string, SpriteRenderer> _slotRenderers = new();
        private readonly Dictionary<string, Sprite[]> _cachedLoadedSprites = new();

        private ChibiAppearance _appearance;
        private LubanPartLookup _partLookup; // wraps LubanDataService.ChibiPartDefs, injected at Init

        private float _frameTimer;
        private int _frameIndex;

        public void Init(ChibiAppearance appearance, LubanPartLookup partLookup)
        {
            _appearance = appearance;
            _partLookup = partLookup;
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            foreach (var kv in _slotRenderers) Destroy(kv.Value.gameObject);
            _slotRenderers.Clear();

            foreach (var (slot, partId) in _appearance.Parts)
                EnsureSlotRenderer(slot, partId);

            // Condition overlays (bandage, blood stain, scratch) layer on top, keyed
            // by their own slot names ("overlay_arm", "overlay_face", ...) so they
            // don't collide with outfit part slots and can be cleared independently.
            foreach (var (slot, partId) in _appearance.ConditionOverlays)
                EnsureSlotRenderer(slot, partId);
        }

        private void EnsureSlotRenderer(string slot, string partId)
        {
            var def = _partLookup.Get(partId);
            if (def == null) return;

            var go = new GameObject($"slot_{slot}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(def.PivotX / 100f, def.PivotY / 100f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = def.DrawOrder;
            _slotRenderers[slot] = sr;
        }

        private void Update()
        {
            if (_appearance == null) return;

            _frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
            }

            var animKey = $"{_appearance.Facing}_{_appearance.AnimState}";

            foreach (var (slot, renderer) in _slotRenderers)
            {
                var partId = _appearance.Parts.TryGetValue(slot, out var pid) ? pid
                    : _appearance.ConditionOverlays.TryGetValue(slot, out var oid) ? oid
                    : null;
                if (partId == null) continue;

                var def = _partLookup.Get(partId);
                if (def == null || !def.FramesByAnimKey.TryGetValue(animKey, out var frames) || frames.Count == 0)
                    continue;

                var sprites = GetOrLoadSprites(partId, animKey, frames);
                renderer.sprite = sprites[_frameIndex % sprites.Length];
            }
        }

        private Sprite[] GetOrLoadSprites(string partId, string animKey, List<string> framePaths)
        {
            var cacheKey = $"{partId}:{animKey}";
            if (_cachedLoadedSprites.TryGetValue(cacheKey, out var cached)) return cached;

            var sprites = new Sprite[framePaths.Count];
            for (var i = 0; i < framePaths.Count; i++)
                sprites[i] = Resources.Load<Sprite>(framePaths[i]);

            _cachedLoadedSprites[cacheKey] = sprites;
            return sprites;
        }

        /// <summary>Call when the entity's movement input changes (from a top-down movement controller).</summary>
        public void SetMotion(FacingDirection facing, bool isMoving)
        {
            _appearance.Facing = facing;
            _appearance.AnimState = isMoving ? ChibiAnimState.Walk : ChibiAnimState.Idle;
        }
    }

    /// <summary>Thin wrapper so the renderer doesn't need to know about VContainer/LubanDataService directly.</summary>
    public class LubanPartLookup
    {
        private readonly Dictionary<string, ChibiPartDef> _defs;
        public LubanPartLookup(Dictionary<string, ChibiPartDef> defs) => _defs = defs;
        public ChibiPartDef Get(string partId) => _defs.TryGetValue(partId, out var d) ? d : null;
    }
}
