---
title: ui-tmp-migration
type: architecture
sources: []
related: ["CardSlotUI.cs", "CardHandView.cs", "WorldItemSystem.cs"]
folder: sources/architecture
created: 2026-09-13
tags:
  - ui
  - textmeshpro
  - font
  - thai
  - lab-c
---

# UI TMP Migration — Legacy Text/TextMesh → TextMeshPro (THSarabunPSK)

> 2026-09-13 — ย้ายระบบข้อความทั้งหมดจาก Legacy `Text`/`TextMesh` ไป `TextMeshPro`
> เพื่อรองรับฟอนต์ไทย **THSarabunPSK** (ก่อนหน้านี้ built-in font ไม่มี glyph ไทย → ช่อง □□□)

## สถาปัตยกรรมใหม่

```
Resources/Fonts/THSarabunPSK SDF.asset   (TMP_FontAsset, AtlasPopulationMode = Dynamic)
        │  Resources.Load<TMP_FontAsset>("Fonts/THSarabunPSK SDF")
        ▼
WorldItemSystem.LoadLabelFont()   ← จุดโหลดกลางเดียว (null check + Debug.LogError)
        │
        ├── WorldItemSystem            → TextMeshPro     (world-space, fontSize 4, Center)
        ├── HarvestableNodeComponent   → TextMeshPro     (world-space, fontSize 4, Center)
        ├── ZoneTransitionTrigger      → TextMeshPro     (world-space, fontSize 4, Center)
        ├── CardSlotUI                 → TMP_Text field  (UI, จาก CardSlot.prefab)
        ├── CardHandView.EnsureFeedbackText → TextMeshProUGUI (auto-create, fontSize 22)
        └── ZoneHudView.EnsureZoneText      → TextMeshProUGUI (auto-create, fontSize 28)
```

## กติกาสำคัญ (conventions ใหม่)

- ใช้ `TMP_Text` เป็น type ของ field UI (รองรับทั้ง TextMeshProUGUI/TextMeshPro)
- World-Space label: `TextMeshPro` + **fontSize 4** (เล็กกว่า TextMesh มาก) + `TextAlignmentOptions.Center`
- UI text: `TextMeshProUGUI` + fontSize 22–28 + `TextAlignmentOptions.Center`
- ห้ามใช้ `label.anchor` / `label.characterSize` / ตั้ง `sharedMaterial` เอง — TMP ดึง material จาก font asset เอง
- Fallback: ถ้า `LoadLabelFont()` คืน null (หา asset ไม่เจอ) → คง default font ของ TMP ไว้ ไม่ set, ไม่ crash + LogError ทันที
- **asmdef**: `Marooned.Game.asmdef` ต้อง reference `Unity.TextMeshPro` (ถูกเพิ่มใน migration นี้)

## Prefab / Scene ที่แก้

| ไฟล์ | การแก้ |
|---|---|
| `Assets/Prefabs/UI/CardSlot.prefab` | ลบ `UnityEngine.UI.Text` → เพิ่ม `TextMeshProUGUI` (fontSize 14, Center, THSarabunPSK SDF) + re-link `CardSlotUI.label` |
| `Assets/Scenes/SampleScene.unity` | Canvas Scaler: ScaleWithScreenSize 1920×1080, MatchWidthOrHeight **0.5** (แก้จาก 0.501) |

## วิธีย้ายที่ใช้จริง (MCP, ไม่ใช้ Editor มือ)

`script-execute` (Roslyn) ใช้ไม่ได้ในเครื่องนี้ (TypeLoadException vtable — conflict ของ
Microsoft.CodeAnalysis assembly) จึงใช้ชุด MCP tools แทน:

1. `assets-prefab-open` (CardSlot.prefab) → `gameobject-find` หา Label
2. `gameobject-component-add` `TMPro.TextMeshProUGUI` (⚠️ ต้องทำ **หลัง** ลบ Text — GameObject เดียวมี Graphic ได้ตัวเดียว)
3. `gameobject-component-modify` + `pathPatches` (fontSize/font/alignment/text)
4. re-link `CardSlotUI.label` ด้วย `pathPatches` path `label`
5. `assets-prefab-close {save:true}` → เซฟ prefab ลงดิสก์
6. Canvas Scaler: `gameobject-component-modify` บน Canvas → `scene-save`

**บทเรียน**: patch ตอน Play mode ไม่ persist — ต้อง verify ค่าหลังเข้าสู่ Edit mode
แล้วแก้ซ้ำ + `scene-save` อีกครั้ง

## ผลเทส (จาก Play session วันที่ 2026-09-13)

- Console: `[WorldItemSystem] enter beach: spawn 3 item` — **ไม่มี** error หา font asset → Resources.Load ผ่าน ✅
- Console: `[CardHandPresenter] ใช้การ์ดสำเร็จ: food_coconut` → feedback path ทำงานกับ TMP แล้ว ✅
- ไม่มี compile error หลังเพิ่ม asmdef reference ✅
- ⚠️ ยังไม่มี screenshot หลักฐาน visual ราย case (A–F) — ต้องเปิด Play แล้วกดเทสด้วยมือ
  (`screenshot-isolated` ใช้กับ Canvas ตรงๆ ไม่ได้ เพราะ Canvas ไม่มี Renderer ให้คำนวณ bounds)

## ข้อแตกต่างจาก spec

1. spec ให้ทำ "fallback เป็น Legacy Font" เมื่อ font ไม่โหลด → ใช้ default font ของ TMP แทน (TMP_Text ไม่ accept `UnityEngine.Font`; ผลเท่ากันคือไม่ crash)
2. เพิ่ม `Unity.TextMeshPro` ใน `Marooned.Game.asmdef` (spec ไม่ได้พูดถึง แต่จำเป็น)
3. ZoneHudView ตั้ง fontSize 28 ตาม spec (ล่าสุดก่อนหน้าเป็น 26)

## ไฟล์ที่เกี่ยวข้อง

- [[WorldItemSystem.cs]] — `LoadLabelFont()` จุดโหลดกลาง + label ไอเท็มบนพื้น
- [[CardSlotUI.cs]] / [[CardHandView.cs]] — UI มือการ์ด
- `Assets/Scripts/Core/ZoneTransitionTrigger.cs` — label ทางเข้าโซน
- `Assets/Scripts/Core/Visual/HarvestableNodeComponent.cs` — durability label
- `Assets/Scripts/UI/Views/ZoneHudView.cs` — HUD ชื่อโซน
