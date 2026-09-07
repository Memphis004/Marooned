---
title: 2026-09-07
type: devlog
sources:
  - "[[sources/overview]]"
  - "[[sources/game_design_doc]]"
  - "[[sources/card-system]]"
  - "[[sources/mcprequesthandlers-cs]]"
  - "[[sources/roundinitializer-cs]]"
  - "[[sources/gametickdriver-cs]]"
  - "[[sources/npcdirectorsystem-cs]]"
related:
  - "[[architecture/overview]]"
  - "[[architecture/card-system]]"
  - "[[game-design-doc/game_design_doc]]"
folder: devlog-history
created: 2026-09-07
tags:
  - devlog
  - lab-a
  - lab-b
  - mcp
  - chibi
  - player-as-killer
  - multiplayer-ready
  - design-pivot
---

# Dev Log — 2026-09-07

## 📌 สรุปวัน
วันปิด Lab A (MCP Round Trip สำเร็จ) + เปิด Lab B (Chibi Visual + Player Movement + Zone-based Loot + Architecture Refactor) — เกมเปลี่ยนจาก "Code Scaffolding" เป็น "Playable Prototype" พร้อม Design Pivot ไปเป็น Walking Sandbox

## ✅ สิ่งที่ทำได้วันนี้

### 1. Lab A Closure — ปิดช่องว่าง MCP Round Trip ✅

#### 1.1 ปัญหาเดิม
- NPC ไม่เกิด (NpcDirectorSystem ยังไม่มีใครเรียก `SetupRound`)
- เกมไม่ขยับ (SurvivalStatSystem, NpcDirectorSystem, WorldEventSystem ไม่มีใครเรียก `Tick()`)
- MCP Tools `get_visible_npcs` ตอบว่าง — ไม่มี NPC ให้เห็น

#### 1.2 วิธีแก้
- สร้าง `RoundInitializer.cs` — เรียก `NpcDirectorSystem.SetupRound(npcIds, killerCount)` ตอนเริ่มเกม
  - หา NPC IDs จาก `GameStateProvider` หรือ config
  - กำหนด killerCount ตามสูตร `clamp(round(npcCount / 6), 1, npcCount/4)` (Among Us style)
- สร้าง `GameTickDriver.cs` — เรียก `Tick(deltaSeconds)` ทุกเฟรมสำหรับทุก system
  - `SurvivalStatSystem.Tick()` — ลด Hunger/Thirst/Mood/Fatigue
  - `NpcDirectorSystem.Tick()` — TickBehavior + TryAttemptElimination
  - `WorldEventSystem.Tick()` — Roll event สุ่ม

#### 1.3 ผลลัพธ์
- ทดสอบผ่าน MCP Tools สำเร็จ:
  - `get_visible_npcs` — ตอบกลับ NPC ที่อยู่ location เดียวกัน (พร้อม visibility filter จาก DeductionSystem)
  - `explore_location` — สำรวจ location แล้วได้ card จาก loot table
- เกมเริ่ม "มีชีวิต" — stat ลดลงตามเวลา, NPC ขยับ, event เกิดขึ้น

---

### 2. Lab B Phase 1-2: Chibi Visual System ✅

#### 2.1 Integrate Asset "Generic Cute 2D" (Unity 2D Animation)
- นำเข้า Asset package "Generic Cute 2D" จาก Unity Asset Store
- ใช้เป็น Default Backend สำหรับ Chibi Character rendering
- ตั้งค่า Animation Controller, Sprite Atlas, Cosmetic Layers

#### 2.2 Experimental Backend สำหรับ Spine (Elena/Derek)
- สร้าง Interface `IChibiVisual` เพื่อรองรับการสลับ Renderer ในอนาคต
  ```csharp
  public interface IChibiVisual
  {
      void Init(ChibiAppearance appearance);
      void SetFacing(FacingDirection direction);
      void SetAnimation(ChibiAnimState animState);
      void ApplyConditionOverlay(string conditionCardId, bool show);
      void Teleport(Vector3 position);
      void Cleanup();
  }
  ```
- สร้าง 2 Implementation:
  - `ChibiVisual2D` — ใช้ Unity 2D Animation (Default Backend)
  - `ChibiVisualSpine` — ใช้ Spine (Experimental, รอ art asset)

#### 2.3 Event-driven Spawn/Despawn ผ่าน MessagePipe
- แทนการ Polling ตรวจตำแหน่ง, ใช้ Event-driven:
  - `PlayerLocationChangedMessage` — publish เมื่อ player เคลื่อนย้าย location
  - `NpcLocationChangedMessage` — publish เมื่อ NPC เคลื่อนย้าย location
- ช่วยให้ระบบอื่นๆ (เช่น UI, Spawner) ตอบสนองต่อการเปลี่ยนแปลงตำแหน่งแบบ real-time

**Note: การออกแบบแบบ Interface-driven นี้ช่วยให้อนาคตสลับระหว่าง 2D Animation / Spine ได้โดยไม่ต้อง refactor UI หรือ game logic — เพียง swap implementation ใน DI registration**

---

### 3. Lab B Phase 3: Player System & Zone-based Loot ✅

#### 3.1 Player Movement (WASD)
- เพิ่ม Input System (Unity Input System หรือ 舊 Input Manager)
- WASD ขยับ Chibi Character บน 2D Sandbox
- จำกัดการเคลื่อนที่ในboundของ map

#### 3.2 Item Pickup (2 โหมด)
- **WalkOver Pickup:** เมื่อเดินทับไอเท็มบนพื้น → อัตโนมัติเข้า inventory
- **Interact Pickup:** กดปุ่ม Interact (E/F) เมื่ออยู่ใกล้ไอเท็ม → pickup

#### 3.3 NPC Roster Change
- เปลี่ยน NPC Roster จากทั่วไป เป็น Wizard / College Student สลับกัน
- **Student 1 สงวนให้ Player** — คนอื่นเป็น NPC
- ทำให้เกมมี flavor ของ "มหาวิทยาลัยลอยทะเล" แทนเกาะเปล่าๆ

#### 3.4 Mock Zone-based Loot Table
- สร้าง Mock Loot Table สำหรับทดสอบ (ยังไม่ผ่าน Luban):
  | Zone | ไอเท็ม | น้ำหนัก |
  |------|--------|---------|
  | Beach | Coconut (มะพร้าว) | 5 |
  | Jungle | Vine (เถาวัลย์) | 4 |
  | Cave | Stone (หิน) | 3 |

---

### 4. Lab B Phase 4: Architecture Refactor (Player-as-Killer & Multiplayer-Ready) ✅

#### 4.1 Refactor `CardDef` — เพิ่ม Targeting และ Effect Type
- เพิ่ม `TargetType` enum:
  ```csharp
  public enum CardTargetType
  {
      SelfOnly,    // ใช้กับตัวเอง (Consumable, Medicine)
      SingleTarget, // ใช้กับ NPC/Entity ตัวเดียว (Weapon)
      Any          // ใช้ได้ทั้งตัวเองหรือคนอื่น
  }
  ```
- เพิ่ม `EffectType` enum:
  ```csharp
  public enum CardEffectType
  {
      StatDelta,   // เปลี่ยน Stat (Hunger/Thirst/Mood/Fatigue)
      Eliminate    // Eliminated NPC
  }
  ```
- อัปเดต `CardDef.cs`:
  ```csharp
  public class CardDef
  {
      public string Id;
      public CardCategory Category;
      public CardTargetType TargetType; // เพิ่ม
      public CardEffectType EffectType; // เพิ่ม
      public string DisplayName;
      public string SpritePath;
      public int StackLimit;
      public Dictionary<string, float> StatEffect;
      public string? TargetId; // เฉพาะ Weapon ที่ต้องมี target
  }
  ```

#### 4.2 ปรับ `UseCardRequest/Handler` — เช็คเงื่อนไขก่อนหักการ์ด
- เพิ่ม `TargetId` ใน `UseCardRequest` (nullable — null = Self)
- ใน `UseCardHandler` ทำการตรวจสอบก่อน:
  1. เช็ค targetId ว่ามี NPC อยู่ใน location เดียวกันหรือไม่
  2. เช็คว่า 플레이어มีการ์ดในมือหรือไม่
  3. **เช็คก่อนหักการ์ด** — ถ้า fail เนื่องจากกฎ No Witness, การ์ดไม่เลือน
  4. ถ้าผ่านเงื่อนไข → หักการ์ด + execute effect
- **Safe UX:** ผู้เล่นไม่เสียการ์ดเมื่อใช้ไม่สำเร็จ (เช่น มีพยาน)

#### 4.3 สร้าง `NpcDirectorSystem.TryEliminate()` — Method กลาง
- สร้าง method กลางสำหรับ eliminate NPC (ทั้ง AI และ Player ใช้ร่วมกัน):
  ```csharp
  public bool TryEliminate(string killerId, string targetNpcId, out string failureReason)
  {
      // 1. เช็ค target อยู่ใน location เดียวกันหรือไม่
      // 2. เช็ค No Witness — มี NPC คนอื่นอยู่ใน location เดียวกันหรือไม่
      // 3. ถ้ามีพยาน → failureReason = "witness_present"
      // 4. ถ้าไม่มีพยาน → execute elimination, cooldown เริ่ม
  }
  ```
- **Both AI Killer และ Player Killer เรียก method เดียวกัน** → กฎ No Witness ถูกบังคับใช้แบบเดียวกัน

#### 4.4 Refactor `GameStateProvider` — Dictionary-based (Multiplayer-ready)
- เปลี่ยนจาก `PlayerSurvivalState` ตัวเดียว เป็น `Dictionary<string, PlayerSurvivalState>`
- รักษา backward compatibility:
  - `GetPlayer()` ยังคงตอบ state ของ player เอง (player_local)
  - เพิ่ม method `GetPlayerState(playerId)` สำหรับ future multiplayer
- ยังคง support single-player ทันที — ไม่จำเป็นต้อง refactor ระบบอื่น

**Note: แม้ปัจจุบันเกมยังเป็น Single-player แต่การออกแบบนี้ทำให้พร้อม expand เป็น Multiplayer ได้ง่ายในอนาคต — ไม่ต้องเปลี่ยนโครงสร้างพื้นฐาน**

---

### 5. Design Pivot — GDD Update ✅

- อ่าน `game_design_doc.md` และ อัปเดตเนื้อหาให้ตรงกับสถานะปัจจุบัน
- เพิ่มหัวข้อ "Design Pivot Log" บอกว่าเกมเปลี่ยนจาก Point-and-Click → Walking Sandbox เมื่อไหร่ และเพราะอะไร
- แก้ไข:
  - Core Gameplay Loop: Point-and-Click → WASD Walking Sandbox
  - Exploration: คลิกเลือก zone → เดินไปเก็บของ
  - Killer Role: มีแค่ NPC → ผู้เล่นเป็น Killer ได้
  - No Witness Rule: AI เท่านั้น → ใช้ร่วมกัน AI + Player
  - Card System: เพิ่ม Weapon category + CardTargetType
  - Architecture: ระบุ Multiplayer-ready
- **อ่านรายละเอียดการเปลี่ยนแปลงได้ที่เกมมี Design Pivot Summary ในส่วน 10**

---

### 6. Patch — WorldItemSystem: กัน Item Respawn เมื่อกลับมาโซนเดิม ✅

#### 6.1 ปัญหา
- จาก Phase 3 (Zone-based Loot): `WorldItemSystem` spawn ไอเท็มใหม่ทุกครั้งที่ player ย้ายเข้าโซน
  (`RespawnForLocation` ถูกเรียกจาก `PlayerLocationChangedMessage` ทุกครั้ง)
- ผลคือ: เดินออกจาก beach แล้วกลับเข้ามา → มะพร้าว spawn ใหม่ทั้งก้อน ทั้งที่เก็บไปแล้ว
  → **farm ไอเท็มได้ไม่จำกัดในรอบเดียว** (ทำลาย economy ของ survival loop)

#### 6.2 วิธีแก้ (แก้แค่ `WorldItemSystem.cs` 3 จุด)
- เพิ่ม field `HashSet<string> _spawnedLocations` — จดว่าโซนไหนเคย spawn แล้วในรอบนี้
- `Initialize()` — เพิ่ม location เริ่มต้นเข้า Set **ก่อน** spawn ชุดแรก (กันกลับมาซ้ำตอนหลัง)
- `RespawnForLocation()` — ใช้ `_spawnedLocations.Add(locationId)` เป็น check+จดพร้อมกัน
  (`Add()` คืน `false` เมื่อ id อยู่แล้ว → log "skip respawn" แล้ว return)
- `ClearItems()` — **ไม่แก้** (ห้ามล้าง Set) — ยืนยันด้วย search ว่า Set ถูกอ้างอิงแค่ 3 จุด:
  ประกาศ field / Initialize / RespawnForLocation

```csharp
private void RespawnForLocation(string locationId)
{
    // เคย spawn โซนนี้ไปแล้วในรอบนี้ → ข้าม (Add() คืน false = มีอยู่ใน Set แล้ว)
    if (!_spawnedLocations.Add(locationId))
    {
        Debug.Log($"[WorldItemSystem] skip respawn @ {locationId} (เคย spawn แล้วในรอบนี้)");
        return;
    }
    ClearItems();
    SpawnForLocation(locationId);
}
```

#### 6.3 พฤติกรรมใหม่

| สถานการณ์ | เดิม | หลัง patch |
|---|---|---|
| เข้า beach ครั้งแรก | spawn 3 มะพร้าว | spawn 3 มะพร้าว (เหมือนเดิม) |
| ออกจาก beach แล้วกลับมา | spawn ใหม่ทั้งหมด ❌ | "skip respawn" — โซนว่าง ✅ |
| เก็บไป 2 ผล แล้วออก-กลับ | spawn ครบ 3 ผล ❌ | หายถาวรทั้งก้อนจนจบรอบ ✅ |
| เข้าโซนใหม่ครั้งแรก (jungle_edge) | spawn | spawn (เหมือนเดิม) |

#### 6.4 Known Trade-off
- โซนที่ "เคยเข้าแล้วออก" โดยยังไม่ได้เก็บ — ไอเท็ม despawn ตอนออก และจะ**ไม่กลับมาอีกจนจบรอบ**
  (ตรง spec "ไม่ respawn ซ้ำในรอบเดียวกัน" แต่อาจรู้สึกว่างผิดคาด)
- ถ้าอนาคตอยากให้ของคงอยู่ข้ามการ revisit → เปลี่ยนจาก despawn-on-leave เป็น persist visual
  (เก็บ GameObject ไว้ ไม่ต้องรอ respawn) — ต่างจาก patch นี้ ต้องตัดสินใจแยก
- Set นี้ reset ตอนเริ่มรอบใหม่ (ระบบเป็น Singleton ต่อ Play session — round reset จริงจัง
  ควรมาพร้อม `ResetRoundState()` ใน Lab ถัดไปถ้ามี restart flow)

---

## ⚠️ ปัญหาที่เจอและวิธีแก้

| ปัญหา | Solution |
|-------|----------|
| NPC ไม่เกิด, เกมไม่ขยับ | สร้าง `RoundInitializer` + `GameTickDriver` เรียก system ทุกเฟรม |
| Spine asset ยังไม่พร้อม | สร้าง Interface `IChibiVisual` รอ swap เมื่อ art ready |
| อยากให้ player เป็น killer ได้ | Refactor `CardDef` + สร้าง `TryEliminate` method กลาง |
| กลัวเปลี่ยน structure แล้วต้อง refactor ทุกที่ | Refactor `GameStateProvider` แบบ backward compatible |
| กลับโซนเดิมแล้วของ spawn ใหม่ (farm ไม่จำกัด) | `HashSet<string> _spawnedLocations` จดโซนที่เคย spawn — `ClearItems()` ไม่ล้าง Set |

---

## 🎯 Next Steps (พรุ่งนี้)

- [ ] ทำ UI Setup Automation สำหรับ `CardHandView` (Canvas, Container, Prefab)
- [ ] ทดสอบ Runtime การเด้งของการ์ดและ Animation เมื่อ Inventory เป็น
- [ ] ตัดสินใจว่าจะให้ไอเท็ม persist ข้ามการ revisit ไหม (ดู Patch 6.4 trade-off)
- [ ] ถ้ามี restart/replay flow → เพิ่ม `ResetRoundState()` ให้ `WorldItemSystem` เคลียร์ `_spawnedLocations`

---

## 📊 สถานะโปรเจค

- **Lab A:** ✅ ปิดแล้ว — MCP Round Trip สำเร็จ, เกมมีการ tick, NPC เกิดขึ้น
- **Lab B Phase 1-2:** ✅ Chibi Visual System พร้อม 2 Backend (2D Animation + Spine experimental)
- **Lab B Phase 3:** ✅ Player Movement + Zone-based Loot + NPC Roster
- **Lab B Phase 4:** ✅ Architecture Refactor + Player-as-Killer + Multiplayer-ready
- **Design Pivot:** ✅ GDD อัปเดตแล้ว

**เกมที่เล่นได้ตอนนี้:** ผู้เล่นเดิน WASD เก็บไอเท็มจากโซน, Craft, ใช้การ์ด (กิน/ดื่ม/Weapon), และ Player-as-Killer ใช้การ์ด Weapon โจมตี NPC ได้ (ต้อง No Witness)
