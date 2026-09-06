---
title: Card System Architecture (Phase 4 — Player-as-Killer + Multiplayer-Ready)
type: architecture
sources:
  - Shared/CardDef.cs
  - Shared/GameMessages.cs
  - Marooned/Assets/Scripts/Systems/GameStateProvider.cs
  - Marooned/Assets/Scripts/Systems/NpcDirectorSystem.cs
  - Marooned/Assets/Scripts/Systems/McpRequestHandlers.cs
  - McpBridge/Program.cs
related:
  - "[[overview]]"
  - "[[mcp-tool-table]]"
  - "[[npc-director]]"
  - "[[card-inventory]]"
  - "[[NpcDirectorSystem.cs]]"
  - "[[McpRequestHandlers.cs]]"
  - "[[LubanDataService.cs]]"
  - "[[IChibiVisual.cs]]"
folder: architecture
created: 2026-09-07
tags:
  - architecture
  - marooned
  - phase-4
  - card-system
---

# Card System Architecture (Phase 4)

Phase 4 ปรับสถาปัตยกรรม Card System ให้รองรับ 3 เป้าหมาย:

1. **Player-as-Killer** — player ใช้การ์ด Weapon (เช่น `knife_basic`) ฆ่า NPC ได้
   ผ่านกติกาเดียวกับ AI killer (same-location + no-witness)
2. **Multiplayer-ready** — `GameStateProvider` เก็บ player แบบ Dictionary ต่อ id
   (แม้ตอนนี้จะมี player เดียว)
3. **Backward compatible** — ค่า default ทั้งหมดออกแบบให้การ์ดเดิม/โค้ดเดิมทำงานเหมือนเดิม

## Design Decisions

### 1. Player-as-Killer — TryEliminate เป็น method กลาง
กติกาการฆ่า (same-location, no-witness, spawn clue, publish `NpcEliminatedMessage`)
อยู่ที่ **`NpcDirectorSystem.TryEliminate` จุดเดียว** ทั้ง AI killer (Tick) และ player
(weapon card ผ่าน `UseCardHandler`) เรียกผ่าน path เดียวกัน:

```
AI killer (Tick)  ──┐
                    ├──► NpcDirectorSystem.TryEliminate ──► NpcEliminatedMessage
Player (weapon card)┘         ▲ CanEliminate (dry-run)      (visual layer ไม่รู้ต้นตอ)
```

- **CanEliminate (dry-run)** — เช็ค 4 เงื่อนไขโดยไม่ mutate state:
  `unknown_target` / `target_already_dead` / `target_not_same_location` / `witnessed`
- **TryEliminate** — เรียก `CanEliminate` ก่อน แล้วค่อย `IsAlive = false` → `SpawnClues`
  → publish `NpcEliminatedMessage`
- แยกเป็น 2 method เพื่อ **Safe UX** (ข้อถัดไป)

### 2. Safe UX — เช็คก่อนหักการ์ด (Order of Operations)
`UseCardHandler` ตรวจทุกเงื่อนไข**ก่อน** `TryConsume` เสมอ ป้องกันการ์ดหายฟรีเมื่อลงมือไม่สำเร็จ:

```
unknown_card → missing_target / invalid_target_type → CanEliminate (dry-run)
→ [ผ่านทั้งหมดค่อย] TryConsume → Eliminate หรือ StatDelta
```

ผลยืนยันจาก Play Mode test (Phase 4 Step 9): ใช้ `knife_basic` กับ NPC ที่มี witness →
`FailureReason = "witnessed"`, NPC ยังมีชีวิต, **การ์ดยังอยู่ครบ**

### 3. Multiplayer-ready GameStateProvider
เปลี่ยนจาก property `Player` เดี่ยว เป็น `Dictionary<string, PlayerSurvivalState>`:

| API | คำอธิบาย |
| --- | --- |
| `GetPlayer(playerId = LocalPlayerId)` | API เดิม — ไม่ใส่ param ได้ผลลัพธ์เดิม (`"player_local"`) |
| `GetOrCreatePlayer(playerId)` | ได้ player ตาม id โดยสร้างใหม่ถ้ายังไม่มี (ไว้เผื่อ multiplayer) |
| `AllPlayers` | player ทุกตัว (read-only) |
| `const string LocalPlayerId = "player_local"` | id ผู้เล่นหลัก — handler ต่างๆ อ้างอิงค่านี้ ไม่ hardcode |

ทุก call site เดิมที่ใช้ `_stateProvider.Player` ถูกย้ายเป็น `_stateProvider.GetPlayer()`
(13 ไฟล์ — ยืนยันด้วย `search_code` ว่าไม่เหลือแล้ว; `res.Player` ใน McpBridge คือ
property ของ response message ไม่เกี่ยวกับ provider)

### 4. CardDef — TargetType + EffectType (default = พฤติกรรมเดิม)

```csharp
public enum CardTargetType { None, Self, SingleTarget }
public enum CardEffectType { StatDelta, Eliminate, Cure /* TODO */ }

public class CardDef
{
    public CardTargetType TargetType = CardTargetType.Self;    // เดิม = Self
    public CardEffectType EffectType = CardEffectType.StatDelta; // เดิม = StatDelta
}
```

- การ์ดเดิมทุกใบไม่ได้ตั้งค่า field ใหม่ → default `Self` + `StatDelta` = ทำงานเหมือนเดิมทุกอย่าง
- `knife_basic` = Weapon ใบแรก: `Category=Weapon`, `TargetType=SingleTarget`,
  `EffectType=Eliminate`, `StackLimit=1` (mock ใน `LubanDataService.LoadAll()` —
  ยังไม่เข้า DataTables/CardDef.csv)
- `CardEffectType.Cure` (ลบ condition card) ยังไม่ implement — ลง path StatDelta เดิม

### 5. Wire format — MessagePack keys ท้าย class
`UseCardRequest` เพิ่ม `[Key(1)] TargetId`, `UseCardResponse` เพิ่ม `[Key(2)] ResultText` —
การ append key ท้ายเดิม compatible กับ message รูปแบบเก่า (ฝั่งเก่าอ่าน field ใหม่ข้าม)

## ผังการเรียกใช้ (use_card กับ weapon)

```
MCP AI ──► Bridge UseCard(cardId, targetId)          [McpBridge/Program.cs — Step 6]
              │ UseCardRequest { CardId, TargetId }  [Shared/GameMessages.cs]
              ▼ TCP :3216
        UseCardHandler.InvokeAsync                    [McpRequestHandlers.cs]
              │ 1. CardDefs.TryGetValue → unknown_card
              │ 2. SingleTarget ไม่มี target → missing_target / invalid_target_type
              │ 3. Eliminate → NpcDirectorSystem.CanEliminate (dry-run)
              │ 4. TryConsume (การ์ดหายตรงนี้เท่านั้น)
              │ 5. TryEliminate → ResultText = "eliminated_<npcId>"
              ▼
        NpcDirectorSystem: IsAlive=false → SpawnClues → NpcEliminatedMessage
              ▼ (in-process MessagePipe)
        Visual layer / future listeners
```

## UI Feedback (Phase 4 Step 7–8 — hook พร้อม, flow รอสร้าง)

- **`CardHandPresenter.GetLocalizedReason(reason)`** — map `FailureReason` → ข้อความไทย
  ครบทุก reason ที่ server คืนได้จริง (`witnessed` → "มีคนเห็น! ไม่สามารถลงมือได้" ฯลฯ)
- **`PlayerCharacterView.PlayActionAnimation(actionName)`** — เล่น one-shot anim
  ("attack"/"use_item") ผ่าน `IChibiVisual.PlayAction` + anim-lock 0.8s
  (backend mapping: GenericCute `"use_item"`→`interact`, Spine `"attack"`→`"Slash"`)
- ⚠️ ทั้งสอง hook ยัง**ไม่มี call site** — hand UI flow เป็น Lab A stub; เมื่อสร้าง flow
  ให้ presenter เรียก `GetLocalizedReason` เมื่อ fail และ `PlayActionAnimation` เมื่อสำเร็จ
  (direct method call ตาม MVP Lite)

## MCP Tool (Step 6)

`use_card` ใน `McpBridge/Program.cs` รับ `targetId` (optional):

```
UseCard(cardId: "knife_basic", targetId: "npc_03")
→ "Used knife_basic: eliminated_npc_03"
UseCard(cardId: "knife_basic")              // ลืม target
→ "Could not use knife_basic: missing_target"
UseCard(cardId: "food_coconut")
→ "Used food_coconut."
```

## Runtime Test ผล (Step 9 — ผ่านทั้งหมด 2026-09-07)

| Test | ผล |
| --- | --- |
| A: `food_coconut` (StatDelta) | Hunger 97→100, การ์ดหาย ✅ |
| B: `knife_basic` กับ NPC อยู่ลำพัง | eliminate สำเร็จ + clue spawn + การ์ดหาย ✅ |
| C: `knife_basic` กับ NPC ที่มี witness | `"witnessed"`, การ์ดไม่หาย ✅ |
| D: `knife_basic` ไม่ระบุ target | `"missing_target"`, การ์ดไม่หาย ✅ |
| E: regression (move_to_location, roster 5/1 killer) | ผ่าน ✅ |

## TODO / Known Issues

- `CardEffectType.Cure` ยังไม่ implement
- `DeductionSystem.Accuse()` ยัง set `IsAlive = false` ตรง (ไม่ผ่าน `TryEliminate` —
  ไม่ spawn clue) — พิจารณาย้ายเข้า path กลาง
- `knife_basic` ยังเป็น mock ใน `LubanDataService` — เพิ่มใน `DataTables/CardDef.csv`
  เมื่อเปิด Luban pipeline
- hand UI flow ยังไม่มี — hook (`GetLocalizedReason`, `PlayActionAnimation`) รอ call site
- `PlayerSurvivalState` ยังไม่มี field ระบุว่า player เป็น killer หรือไม่ (ตอนนี้ player
  kill ได้เสมอผ่าน weapon card — ถ้าอนาคตต้องจำกัด ให้เพิ่ม role check ใน UseCardHandler)
