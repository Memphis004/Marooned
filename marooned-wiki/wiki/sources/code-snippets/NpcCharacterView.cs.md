---
title: NpcCharacterView.cs
type: code-snippets
sources:
  - Marooned/Assets/Scripts/Core/Visual/NpcCharacterView.cs
related:
  - "[[ChibiSpawnerView.cs]]"
  - "[[IChibiVisual.cs]]"
  - "[[PlayerCharacterView.cs]]"
  - "[[npc-embodiment-movement]]"
folder: code-snippets
created: 2026-09-10
tags:
  - code-snippets
  - visual
  - npc
  - lab-c
  - marooned
---

# NpcCharacterView.cs

## หน้าที่
View ต่อ 1 chibi NPC (MonoBehaviour) — sync ตำแหน่ง/facing/animation กับ
`NpcState` (ground truth) ทุกเฟรม ทำให้ chibi บนจอเดินตาม PositionX/Y จริง

## หลักการสำคัญ
- **MVP Lite (บทเรียน Lab 13)**: การเดินเป็น continuous per-frame state → อ่าน
  state ตรงแบบ direct method call ใน `Update()` **ห้ามใช้ MessagePipe ต่อเฟรม**
  (MessagePipe ใช้เฉพาะ discrete event ข้ามโซนที่ ChibiSpawnerView flow เดิมจัดการ)
- **Passive View**: ไม่ resolve container เอง — รับ dependency ผ่าน `Init()` แบบ
  direct call (ต่างจาก PlayerCharacterView ที่ resolve เองใน Start เพราะอยู่ใน
  scene ตั้งแต่ต้น ส่วน view นี้ถูก spawn ทีหลัง)
- **Information Hiding**: อ่านเฉพาะ Position/Activity/IsAlive (ตาเห็นอยู่แล้ว)
  ห้าม render/expose Inventory/Fear/Curiosity

## Wiring (ChibiSpawnerView.SpawnChibi)
```
Instantiate(prefab)
  → GetComponent<NpcCharacterView>() ?? AddComponent<NpcCharacterView>()
  → characterView.Init(npc.Id, _npcDirector)     // direct call
  → chibi.transform.position = (npc.PositionX, npc.PositionY, 0)  // กันเฟรมแรกโผล่ origin
```

## Update() flow
1. `Npcs.TryGetValue(_npcId, out npc)` — view ที่ NPC โดนลบจะหยุดเอง
2. `transform.position = (PositionX, PositionY, 0)` — position sync
3. Facing: flip จากเครื่องหมาย `dx = TargetX - PositionX` (threshold 0.02)
   → `IChibiVisual.SetFacing()` (GenericCute = localScale.x, Spine = Skeleton.ScaleX)
4. `IChibiVisual.Bind(npc.Activity)` — Traveling→walk, Idle→idle (ทั้งสอง backend)

## สถานะ
- ✅ เสร็จ + เทสแล้วใน Play Mode (Test A–D ผ่าน ดู [[npc-embodiment-movement]])
- ภาพหลักฐาน: `TestEvidence/step3_testA_genericcute_walk.png`,
  `TestEvidence/step3_testC_spine_walk.png`
