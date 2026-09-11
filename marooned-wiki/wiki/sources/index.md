---title: index
type: index
sources: []
related: []
folder: sources
created: 2026-09-05
tags:
  - index
  - marooned
  - lab-a
  - lab-b
  - lab-c

---

# 📚 Marooned Wiki — สารบัญ

> เอกสารทั้งหมดของโปรเจค Marooned (Card Survival × Social Deduction)
อัปเดตล่าสุด: 2026-09-11 (Lab C Phase 2.5A Living NPCs: NpcSurvivalSystem decay
+ motive hooks ผ่าน NpcEliminatedMessage + เปิด FleeToSafeZone/InvestigateNoise
จริง — EditMode 8/8 + PlayMode 2/2 ผ่าน)

## 🎮 Game Design
- [[game_design_doc]] — Game Design Document หลัก (แหล่งความจริงของทุก design decision)

## 🏗️ Architecture
- [[overview]] — ภาพรวม architecture ทั้งหมด (Composition Root, DI, Message Bus, Data Flow)
- [[card-system]] — สถาปัตยกรรม Card System Phase 4 (Player-as-Killer, TryEliminate กลาง,
  Multiplayer-ready GameStateProvider, Safe UX เช็คก่อนหักการ์ด)
- [[mcp-bridge]] — MCP Bridge architecture (build/run, TCP connection, วิธีเพิ่ม tool)
- [[mcp-tool-table]] — ตาราง MCP tools ทั้งหมด พร้อมสถานะ
- [[chibi-visual-system]] — ระบบ spawn/คุม visual ตัวละคร Chibi แบบ event-driven
  (MessagePipe + VContainer + MVP Lite)
- [[player-system]] — ระบบผู้เล่น Lab B Phase 3: เดิน WASD 4 ทิศ + เก็บไอเท็มตามโซน
  (PlayerInputService/Movement/WorldItem/Pickup + zone loot mock table)
- [[biome-scatter-system]] — Lab C Phase 1: Hybrid Biome Scatter (System คำนวณ + publish event,
  View เป็นคน Instantiate) + HarvestableNode tool-gathering (durability/tool/regrow จาก Luban CSV)
- [[biome-scatter-editor-setup]] — คู่มือสร้าง BiomePrefabSet ใน Unity Editor +
  protocol เทส Test A–D พร้อมจุดพบบ่อย
- [[npc-embodiment-movement]] — Lab C Phase 2 Step 1-4 ครบ: NPC embodiment + AI Hook เทส A-F ผ่านหมด
  เป็น Ground Truth (NpcState Key 9-15, NpcInventory, NpcSurvivalState) +
  NpcMovementSystem (เดินอย่างเดียว) + สมอง AI แยก role InnocentUtilityAI/KillerPlanner
  ผ่าน MoveNpc + Position Seeding Rule + DI cycle ปลอดภัย (Bind pattern)
  กัน warp (0,0) + tick order Director ก่อน Movement + NpcCharacterView sync chibi
  กับ Position/Activity ทุกเฟรม (พร้อมผลเทส Test A–D และภาพหลักฐาน)
- [[npc-zone-transitions]] — Hybrid Transition Points (2026-09-11): NPC เดินข้ามโซนผ่าน
  จุดเชื่อมแทน teleport — ZoneConnectionDef.csv (กฎพิกัด 80% จากโซนต้นทาง) +
  LubanDataService.GetTransition + NpcState Key 16-17 (ground truth ห้าม leak) +
  Wander.MoveToZone (single source of truth, fallback teleport) +
  NpcZoneTransitionSystem FSM (WalkingToPoint→Exiting 0.5s→ข้ามโซน→Entering 0.3s)
  + tick order Director→Movement→ZoneTransition + EditMode 6/6 และ PlayMode visual A+B ผ่าน
- [[npc-survival-motives]] — Lab C Phase 2.5A Living NPCs (2026-09-11): NPC "รู้สึก" ถึงโลกจริง —
  NpcSurvivalSystem decay tick (Hunger +2/วิ, Fear −6/วิ ≈17s, Curiosity −3/วิ ≈33s, สเกล 0-100)
  + motive hooks subscribe NpcEliminatedMessage (witness Fear +50, hearer Curiosity +30
  + LastNoiseLocationId Key 3) + เปิด FleeToSafeZoneAction/InvestigateNoiseAction จริง
  (stateless ตามสัญญา IUtilityAction) + Wander.ClampTargetToZone (เฉพาะ TransitionPhase == None
  และเฉพาะ Target) + tick order Survival→Director→Movement→ZoneTransition +
  EditMode 8/8 และ PlayMode 2/2 ผ่าน

## 📄 Code Snippets

### Core
- [[GameLifetimeScope.cs]] — Composition Root: ประกอบร่าง DI, MessagePipe, TCP server ทั้งหมด
- [[GameTickDriver.cs]] — MonoBehaviour เรียก `Tick()` ของระบบ gameplay ทุกเฟรม (game loop)
- [[RoundInitializer.cs]] — เรียก `SetupRound()` อัตโนมัติตอน Scene โหลด ให้มี NPC เกิดในเกม

### Systems
- [[SurvivalStatSystem.cs]] — drain stat 4 ค่า + roll illness เมื่อ stat วิกฤต
- [[CardInventorySystem.cs]] — เพิ่ม/หัก/เช็คการ์ดใน inventory (พร้อม CraftingSystem ในไฟล์เดียว)
- [[CraftingSystem.cs]] — ตรวจเงื่อนไข recipe แล้วคราฟการ์ด
- [[ExplorationSystem.cs]] — สำรวจ location แบบ weighted loot ที่ deplete ได้
- [[NpcDirectorSystem.cs]] — เจ้าของ ground truth NPC + killer AI + spawn clue
  (Phase 4: `CanEliminate`/`TryEliminate` method กลางที่ player ใช้ร่วมกับ AI)
- [[Wander.cs]] — helper การเดินร่วมของ AI (single source of truth):
  `IsTransitioning` gate + `MoveToZone` (เริ่ม transition ผ่านจุดเชื่อม / fallback
  teleport) + `SetRandomTargetInZone`
- [[NpcZoneTransitionSystem.cs]] — FSM ข้ามโซนแบบเดินผ่านจุดเชื่อม
  (WalkingToPoint→Exiting→Entering→None) plain C# timer dictionary ไม่มี coroutine
  (2026-09-11)
- [[NpcSurvivalSystem.cs]] — decay tick stats NPC (สเกล 0-100) + motive hooks
  NpcEliminatedMessage (witness/hearer) + clamp target + hint lifecycle
  LastNoiseLocationId + status log ทุก 30 วิ (2026-09-11)
- [[InnocentUtilityAI.cs]] — สมองฝั่ง Innocent: utility scoring re-evaluate ทุก ~1.5 วิ
  — IdleWander/SeekFood/FleeToSafeZone (Fear)/InvestigateNoise (Curiosity×ไม่กลัว)
  stateless ตามสัญญา IUtilityAction, การเดินผ่าน Wander helper เท่านั้น (2026-09-11)
- [[DeductionSystem.cs]] — information-hiding layer + ตัดสิน accusation
- [[WorldEventSystem.cs]] — weighted random event queue (Survival/Social)
- [[McpRequestHandlers.cs]] — ปลายทาง request จาก MCP Bridge 10 ตัว
- [[LubanDataService.cs]] — GameStateProvider + LubanDataService (mock data รอ Luban จริง)

### UI
- [[UIRoot.cs]] — panel registry ของ UI (enum → Type mapping)
- [[CardHandPresenter.cs]] — Presenter ของมือการ์ด: render event-driven + click-to-use
  (`UseCardFromSlot`, target selection สำหรับ weapon ผ่าน ChibiSpawnerView) +
  `GetLocalizedReason` แปลง failure reason เป็นไทย (Phase 4–5)
- [[CardSlotUI.cs]] — View slot การ์ดเดี่ยว: rendering category color, label stack count, pop animation,
  และ IPointerClickHandler ส่ง event Clicked(cardId) ให้Presenter (MVP Lite —คลิกซ้ายบน slot ที่มีการ์ดเท่านั้น)
- [[ClueBoardPresenter.cs]] — Presenter ของกระดานเบาะแส (skeleton)
- [[MapExplorePresenter.cs]] — Presenter ของแผนที่สำรวจ (skeleton)
- [[MeetingVotePresenter.cs]] — Presenter ของหน้าโหวตกล่าวหา (skeleton)
- [[CardHandView.cs]] — View แสดงมือการ์ด (skeleton)
- [[ClueBoardView.cs]] — View กระดานเบาะแสแบบ detective board (skeleton)
- [[ConditionOverlayView.cs]] — HUD icon illness/injury ของผู้เล่น (skeleton)
- [[MapExploreView.cs]] — View แผนที่ 2D sandbox (skeleton)
- [[MeetingVoteView.cs]] — View หน้าประชุม + ปุ่ม Accuse/Abstain (skeleton)

### Visual Layer (Chibi)
- [[IChibiVisual.cs]] — interface กลางของ visual layer (backend-agnostic: Animator/Spine/paperdoll,
  Phase 4 เพิ่ม `PlayAction` one-shot)
- [[ChibiSpawnerView.cs]] — spawn/despawn chibi ของ NPC ตาม location แบบ event-driven
- [[NpcCharacterView.cs]] — Step 3: sync ตำแหน่ง/facing/animation ของ chibi NPC
  กับ NpcState ทุกเฟรม (direct read ไม่ใช้ MessagePipe ต่อเฟรม) + จับ phase
  เปลี่ยนของ transition เล่น `PlayAction("zone_exit"/"zone_enter")` one-shot
- [[GenericCuteVisualController.cs]] — คุม Animator ของ asset "Generic Cute 2D Student" (PSB skeletal)
- [[SpineVisualController.cs]] — Spine backend คุม SkeletonAnimation (Elena/Derek ใช้ร่วมกัน)

### Data
- [[ChibiAnimatedRenderer.cs]] — chibi sprite-swap paperdoll renderer เดิน 4 ทิศ

### Luban Generated (`Data/Gen/` — ห้ามแก้มือ)
- [[Data_Gen_Tables.cs]] — root table manager ของทั้ง 6 ตาราง
- [[Data_Gen_game_CardDef.cs]] — bean: การ์ด
- [[Data_Gen_game_ClueDef.cs]] — bean: เบาะแส
- [[Data_Gen_game_IllnessDef.cs]] — bean: โรค/บาดแผล
- [[Data_Gen_game_LocationDef.cs]] — bean: location node
- [[Data_Gen_game_RecipeDef.cs]] — bean: สูตรคราฟ
- [[Data_Gen_game_WorldEventDef.cs]] — bean: world event
- [[Data_Gen_game_TbCardDef.cs]] — table wrapper: การ์ด
- [[Data_Gen_game_TbClueDef.cs]] — table wrapper: เบาะแส
- [[Data_Gen_game_TbIllnessDef.cs]] — table wrapper: โรค
- [[Data_Gen_game_TbLocationDef.cs]] — table wrapper: location
- [[Data_Gen_game_TbRecipeDef.cs]] — table wrapper: recipe
- [[Data_Gen_game_TbWorldEventDef.cs]] — table wrapper: world event

## ⚙️ Game Mechanics
- [[survival-stats]] — ระบบ Survival Stats (Hunger/Thirst/Mood/Fatigue + illness)
- [[card-inventory]] — ระบบ Card Inventory และ hand management
- [[crafting]] — ระบบ Crafting และ recipes
- [[exploration]] — ระบบ Exploration และ map
- [[npc-director]] — ระบบ NPC Director (behavior, killer, no-witness rule)
- [[deduction]] — ระบบ Social Deduction (clues, accusation, win/lose)
- [[world-events]] — ระบบ World Events (Survival/Social groups)
- [[chibi-avatar]] — ระบบ Chibi sprite-swap avatar

## 📋 Conventions & Logs
- [[conventions]] — Coding conventions จากโค้ดจริง
- [[2026-09-05]] — Dev Log วันแรก
- [[2026-09-07-lab-a-closure-b-phases-architecture-refactor]] — Dev Log: ปิด Lab A (MCP round trip) + Lab B Phase 1-4 (Chibi, Player, Player-as-Killer, refactor) + patch WorldItemSystem กัน item respawn ซ้ำ
- [[2026-09-09-lab-c-phase1-hybrid-biome-scatter-node-harvest-zone-transition]] — Dev Log: Lab C Phase 1 – Hybrid Biome Scatter + Node Harvest + Zone Transition
- [[2026-09-10-lab-c-phase2-npc-embodiment-ai-hook]] — Dev Log: Lab C Phase 2 – NPC Embodiment (data foundation, movement, visual sync, AI Hook InnocentUtilityAI/KillerPlanner) + runtime tests A–F ผ่านครบ + 2 bugs ที่เจอ/แก้
- [[2026-09-11-devlog]] — Dev Log: Hybrid Transition Points (Visual/ Spatial Fix) • Living NPCs (Survival Tick + Motive Events + Stub Actions) • Debug Overlay
- [[npc-embodiment-movement]] — (architecture) Lab C Phase 2 Step 1-4 – NPC Embodiment + Basic AI Hook: data/movement/visual sync + InnocentUtilityAI/KillerPlanner (2026-09-10)

## 🗂️ โครงสร้าง Wiki

```
marooned-wiki/
├── game-design-doc/          # GDD (ไฟล์ game_design_doc.md)
└── wiki/
    ├── sources/
    │   ├── index.md              # สารบัญนี้
    │   ├── conventions.md        # coding conventions
    │   ├── architecture/         # overview, mcp-bridge, mcp-tool-table, chibi-visual-system
    │   ├── code-snippets/        # snippet รายไฟล์ (40 ไฟล์ — Systems/Core/Data/UI/Visual)
    │   ├── mechanics/            # เอกสารระบบเกม 8 ระบบ
    │   ├── game-design-doc/      # GDD (canonical location)
    │   ├── bug-log/              # (ว่าง — ไว้บันทึก bug)
    │   └── devlog-history/       # dev log รายวัน
    ├── concepts/                 # auto-generated concept pages (Karpathy plugin)
    └── entities/                 # auto-generated entity pages (Karpathy plugin)
```

- โครงสร้างยึดแบบ **Karpathy LLM Wiki** — `sources/` เก็บเอกสารที่เขียน/สร้างจากโค้ดจริง
  `concepts/` กับ `entities/` เป็นของ plugin generate เอง
- ทุกเอกสารใน `sources/` มี YAML frontmatter รูปแบบเดียวกัน (ดู [[conventions]])
- เชื่อมโยงเนื้อหาด้วย `[[...]]` WikiLinks เสมอ เพื่อให้ Obsidian graph และ local LLM
  (Smart Connections) ค้นหาความสัมพันธ์ได้
