---
title: ChibiAnimatedRenderer
type: snippet
sources: [Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs]
related:
  - "[[ChibiAppearance]]"
  - "[[ChibiPartDef]]"
  - "[[LubanDataService.cs]]"
  - ConditionOverlays
folder: Data
lines: 127
created: 2026-09-05
tags:
  - Data
  - marooned
  - lab-a
---

# ChibiAnimatedRenderer.cs
**Path:** `Marooned/Assets/Scripts/Data/ChibiAnimatedRenderer.cs` (127 lines)

## Source
```csharp
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
```

# ChibiAnimatedRenderer

`ChibiAnimatedRenderer` (MonoBehaviour) + `LubanPartLookup` (helper))

## Purpose
Renderer ตัวละคร chibi แบบ **sprite-swap paperdoll** ที่เดินได้จริงบนแผนที่ 2D sandbox —
1 `SpriteRenderer` ต่อ slot (แขน/ขา/หัว/ลำตัว...) สลับ frame ตามทิศทาง+สถานะ animation
แทนที่ portrait-swap เดิมของโปรเจคอ้างอิง (ไม่ใช้ bone/skeleton เลย)

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `void Init(ChibiAppearance appearance, LubanPartLookup partLookup)` | ผูก appearance data + part table แล้วสร้าง slot renderers ทั้งหมด (`RebuildSlots`) |
| `void SetMotion(FacingDirection facing, bool isMoving)` | เรียกจาก movement controller — set ทิศหัน + สลับ Walk/Idle |
| `float framesPerSecond` [SerializeField] | ความเร็ว animation (default 6 fps) |

(`LubanPartLookup.Get(string partId)` — wrapper กัน renderer รู้จัก VContainer/LubanDataService โดยตรง)

## Dependencies
- **ChibiAppearance** (MessagePack data) — `Parts` (slot→partId), `ConditionOverlays`
  (slot→partId แยก dict สำหรับคราบเลือด/ผ้าพันแผล), `Facing`, `AnimState`
- **ChibiPartDef** — `FramesByAnimKey` (key เช่น `"Down_Walk"` → list ของ sprite path),
  `DrawOrder`, `PivotX/PivotY`
- **UnityEngine** — `SpriteRenderer`, `Resources.Load`, `Destroy`
- ในอนาคต: `Init` ควรถูกเรียกโดยระบบ NPC/player spawn โดยส่ง `LubanPartLookup`
  ที่ครอบ `LubanDataService.ChibiPartDefs`

## Key Logic
- **Layer stack** (sort ด้วย `DrawOrder` ตาม comment หัวไฟล์): leg_back(0) → body(10) →
  leg_front(15) → arm_back(20) → head(30) → hair_back(35) → face_marking(38) →
  hair_front(40) → arm_front(45) → overlay/condition(50) → accessory(60)
- **RebuildSlots**: ลบ slot เก่าทั้งหมด → สร้าง `GameObject("slot_{slot}")` ใหม่ต่อ slot
  จาก `Parts` + `ConditionOverlays` — localPosition มาจาก `PivotX/PivotY / 100`
  (canvas ฐาน 1024×1024), sortingOrder = `DrawOrder`
- **Update loop**: สะสม `Time.deltaTime` จนถึงรอบ frame → เพิ่ม `_frameIndex` → สร้าง
  animKey = `$"{Facing}_{AnimState}"` → ทุก slot หา partId ของตัวเอง (จาก `Parts` ก่อน
  แล้ว fallback `ConditionOverlays`) → โหลด/ใช้ cache sprites → set
  `renderer.sprite = sprites[_frameIndex % sprites.Length]`
- **Sprite cache**: key `$"{partId}:{animKey}"` โหลดผ่าน `Resources.Load<Sprite>` ครั้งเดียว
  (sprite paths จึงต้องอยู่ภายใต้ `Assets/Resources/`)

## TODO / Known Issues
- **ยังไม่มีใครเรียก `Init()`** — ไม่มี spawner/player controller ในโปรเจค
- `ChibiPartDef.FramesByAnimKey` ยังไม่มีข้อมูลจริง — `DataTables/Data/ChibiPartDef.csv`
  ใส่แค่ pivot/drawOrder (frame data วางแผนใช้ JSON sidecar แยก) และ
  `LubanDataService.ChibiPartDefs` ยังว่างเปล่า
- `_frameIndex` รัน global ไม่ sync จุดเริ่มต้นระหว่าง slot (slot ทุกชิ้นใช้ index เดียวกัน —
  พอใช้ได้เพราะ frame count ต่อ anim ควรเท่ากันในทุก part ของชุดเดียว)
- ไม่มี tint จาก `ChibiAppearance.Colors` (ยัง reserved)
- ทุกอนิเมชันวนตลอดแม้ Idle (Idle 1-2 frame จะดู "หายใจ" — ตาม design แต่ควรรองรับ
  non-loop สำหรับ Hurt ในอนาคต)
