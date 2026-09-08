---
title: Player System (Lab B Phase 3)
type: architecture
related:
  - "[[PlayerInputService.cs]]"
  - "[[PlayerMovementSystem.cs]]"
  - "[[PlayerCharacterView.cs]]"
  - "[[WorldItemSystem.cs]]"
  - "[[ItemPickupSystem.cs]]"
  - "[[GameTickDriver.cs]]"
  - "[[ChibiSpawnerView.cs]]"
  - "[[chibi-visual-system]]"
  - "[[ItemPickedUpMessage]]"
  - "[[PlayerLocationChangedMessage]]"
created: 2026-09-06
tags:
  - architecture
  - marooned
  - lab-b
  - player
---

# Player System (Lab B Phase 3)

## Overview

## Camera Follow
ระบบกล้องตามผู้เล่นด้วยการตั้งค่า Camera.main.transform.position ให้ตรงกับ PlayerCharacterView.transform.position + offset (z = -10) เพื่อให้ผู้เล่นอยู่กลางจอเสมอ

## Zone Triggers
โซนกำหนดด้วย Collider2D (IsTrigger) ที่แนบกับ GameObject โซน แต่เมื่อ PlayerLocationChangedMessage ปล่อยออกจากโซนเดิมและเข้าสโซนใหม่ จะทำการเปลี่ยน loot table ของ WorldItemSystem ตามโซนนั้น และ WorldItemSystem จะคงรายการไอเท็มที่ spawn อยู่ในโซนเดิมจนกว่าจะเปลี่ยนโซนใหม่ (per‑location persistence)

ระบบผู้เล่นแบบ free movement (top-down lite) เดิน 4 ทิศด้วย keyboard แล้วเก็บ
ไอเท็มตามโซน (zone-based loot) เข้า inventory — ครบห่วงโซ่
`input → movement (state) → view (direct read) → pickup → inventory + message`
โดยยังคงกติกาเดิม: VContainer resolve, MessagePipe เฉพาะ discrete event,
View อ่าน state ตรงทุกเฟรม (บทเรียน Lab 13: continuous state เรียกตรง ไม่ pub/sub)

## Data Flow

```
[Input]  Keyboard (Legacy Input)
   │  PlayerInputService.Tick()  ← GameTickDriver เรียกทุกเฟรม (ลำดับแรกสุด)
   │    - WASD/Arrows → MoveAxis (Vector2 raw)
   │    - E/Space → InteractPressed (edge-triggered, ConsumeInteractPressed)
   ▼
[Logic] PlayerMovementSystem.Tick(dt)   (plain C# singleton, no MonoBehaviour)
   │    - เพิ่ม PositionX/Y ใน PlayerSurvivalState (Speed=3.5, clamp bounds ±11/±3.5..5)
   │    - ตั้ง FacingRight ตามแกน x, Activity = Traveling/Idle
   │    - ❌ ไม่ publish message ทุกเฟรม
   ▼
[View]  PlayerCharacterView.Update()    (MonoBehaviour passive, MVP Lite)
   │    - transform.position = state ตรงทุกเฟรม
   │    - _visual.SetFacing(FacingRight) + Bind(Activity)  → idle/walk
   │    - subscribe ItemPickedUpMessage (discrete) → PlayPickup() + anim lock 0.8s
   ▼
[Pickup] ItemPickupSystem.Tick(dt)
   │    - Mode enum { InteractKey (default), WalkOver } — สลับ runtime ได้
   │    - WorldItemSystem.TryGetNearest(pos, 1.6) → CardInventorySystem.TryAdd
   │    - สำเร็จ → RemoveItem (destroy) + publish ItemPickedUpMessage ครั้งเดียว
   ▼
[Items] WorldItemSystem (IInitializable = EntryPoint)
         - mock ZoneLootTable (TODO Lab A+1: ย้ายไปตาราง Luban)
           beach → food_coconut / jungle_edge → mat_vine / cave_entrance → mat_stone
         - subscribe PlayerLocationChangedMessage → clear + respawn ตามโซนใหม่
         - spawn เป็น GameObject (วงกลมเขียว placeholder + TextMesh ชื่อไอเท็ม)
           ตำแหน่งสุ่มรอบ WorldX/Y ของ location (ZoneSpread 2.5)
```

## DI Registration (GameLifetimeScope)
```csharp
builder.Register<PlayerInputService>(Lifetime.Singleton).AsSelf();
builder.Register<PlayerMovementSystem>(Lifetime.Singleton).AsSelf();
builder.Register<ItemPickupSystem>(Lifetime.Singleton).AsSelf();
builder.RegisterEntryPoint<WorldItemSystem>(Lifetime.Singleton).AsSelf(); // IInitializable
```
Tick order ใน `GameTickDriver.Update()`: input → survival → movement → pickup →
npcDirector → worldEvent (input ต้องมาก่อนเพื่อให้เฟรมเดียวกันอ่านค่าล่าสุด)

## Scene Wiring (SampleScene)
- `GameLifetimeScope/GameManager/PlayerCharacter` — PlayerCharacterView,
  `visualPrefab` = `001 Student 1 Character.prefab` (Student 1 สงวนให้ player
  เท่านั้น — spawner ห้าม spawn ให้ NPC อีก)
- NPC rotation ใน `ChibiSpawnerView.npcPrefabs[]` = [WizardChibi,
  CollegeStudentChibi] เวียนตามเลขท้าย npc id (npc_01→[0], npc_02→[1], ...)
  ดูรายละเอียด state-name mapping ต่อ prefab ใน [[chibi-visual-system]]

## Shared State (PlayerSurvivalState — Key 11–14)
`PositionX`, `PositionY`, `FacingRight`, `Activity` — เพิ่มใน `Shared/` แล้วรัน
`./sync-shared.sh` (Unity + McpBridge ได้ไฟล์เดียวกัน) — `McpBridge` เลยอ่าน
ตำแหน่ง/activity ผู้เล่นผ่าน get_game_state ได้ทันที

## Design Decisions
- **Top-down lite:** ขึ้น/ลงใช้ anim walk เดิม (ไม่มี 4-dir sprite) — flip เฉพาะซ้าย/ขวา
- **Pickup default = InteractKey** (ตั้งใจให้เกมช้าแบบ Card Survival) — WalkOver ทำไว้เผื่อดีไซน์
- **ไม่ยิง message ทุกเฟรม:** การเดินเป็น continuous state → View อ่านตรง;
  `ItemPickedUpMessage` เป็น discrete event จึงผ่าน MessagePipe
- **item id ต้องมีใน CardDefs:** `TryAdd` เช็คทุกครั้ง — id mock หลุดตาราง = เก็บไม่ได้ (log warning)

## ⚠️ Design Direction Note (รอแก้ GDD)
เกมขยับทิศไปทาง **walking sandbox** (Don't Starve-like: เดินอิสระ + เก็บทรัพยากร
รายชิ้นตามโซน) จากเดิมที่ผู้เล่นนั่งนิ่ง explore ด้วยการ์ด — zone-based loot table
คือก้าวแรกของทิศทางนี้ **GDD revision = TODO** (ยังไม่แก้ GDD ณ วันที่เอกสารนี้)

## Test Evidence (Play Mode 2026-09-06 — LabB3PlayModeSelfTest mode=1)
```
[A] WASD: pos (0.00,0.00) → (-3.40,-0.14) activity=Traveling
    (คีย์จริงผ่าน OS keybd_event: D 2.5s → A 2s → S 1.5s ในหน้าต่างเทส 30s)
[B] InteractKey (E): items 3→2, food_coconut 1→2  ✅
[C] WalkOver (สลับ Mode): items 2→1  ✅
[D1] NPC rotation: WizardChibi/CollegeStudentChibi สลับครบ 5 ตัว  ✅
[D2] move_to_location(jungle_edge): NPC 0, items=3 = mat_vine  ✅
[D3] move_to_location(beach): NPC 5, items=3 (respawn food_coconut)  ✅
[D4] get_game_state inventory: {food_coconut=3}  ✅
```
+ Screenshot: `TestEvidence/LabB_Phase3/` (testA_beach_playmode,
  testA_walked_right, testB_after_E_taps) + ไฟล์ผล `LabB3_result_mode1.txt`

## Known Issues / Next Steps
- **item id mock** (`mat_vine`, `mat_stone`) — TODO Lab A+1: เพิ่มใน
  DataTables/CardDef.csv จริง + zone loot table ย้ายไป Luban (ZoneLootTable)
- ยังไม่มี tilemap/สภาพแวดล้อม — ผู้เล่นกับไอเท็มลอยบนพื้นหลังกล้อง
- การแปลงไอเท็มเป็นการ์ดในมือ (card hand UI) ยังไม่ทำ — ไอเท็มเข้า inventory state อย่างเดียว
- PlayerInputService ใช้ Legacy Input — ถ้าย้ายไป Input System ต้อง rewrite Tick()
