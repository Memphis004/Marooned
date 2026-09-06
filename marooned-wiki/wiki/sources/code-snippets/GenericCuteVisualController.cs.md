---
title: GenericCuteVisualController
type: snippet
sources: ["[[sources/genericcutevisualcontroller-cs]]"]
related:
  - "[[NpcState]]"
  - "[[ChibiSpawnerView.cs]]"
  - "[[ChibiAnimatedRenderer.cs]]"
  - NpcActivityState
folder: Core/Visual
lines: 91
created: 2026-09-06
tags:
  - Core
  - marooned
  - lab-b
  - chibi
---

# GenericCuteVisualController.cs
**Path:** `Marooned/Assets/Scripts/Core/Visual/GenericCuteVisualController.cs`

## Purpose
Wrapper คุม Animator ของ asset **"Generic Cute 2D - 001 Student 1"** (PSB skeletal +
Unity Animator) — แยกจาก [[ChibiAnimatedRenderer.cs]] (custom frame-swap paperdoll
เดิม) เพื่อพิสูจน์ Lab B flow ก่อน แล้วค่อยรวม/เลือกใช้ทีหลัง

## Asset Facts (จากการตรวจ asset จริง — อ่านไฟล์ .controller/.prefab)
| ประเด็น | ความจริง | ผลต่อการออกแบบ |
| --- | --- | --- |
| Animator Parameter | `Basic.controller` มี `m_AnimatorParameters: []` (ไม่มีเลย) | **ใช้ `Animator.Play("stateName")` แทนการ set พารามิเตอร์ `State` int ตาม spec เดิม** |
| ชื่อ state | มี `idle`, `walk`, `run`, `interact`, `dig`, `pick up`, `blocking`, `roll` ฯลฯ | Map ตรงชื่อ: Idle/Resting→`idle`, Traveling/Gathering→`walk`, Talking→`interact` |
| Prefab Animator | `m_Controller: {fileID: 0}` (ว่างมาจาก asset) | Bootstrap ผูก `Basic.controller` ให้ prefab แล้ว (ผ่าน code ตอน setup) |

## Public API
| Member | คำอธิบาย |
| --- | --- |
| `void Bind(NpcState npc)` | ผูก ground-truth state เข้ากับ visual + apply animation ทันที |

## Key Logic
- เป็น **View แบบ passive ที่สุด** — ไม่ Resolve อะไรเอง, ไม่ Subscribe เอง
  (ChibiSpawnerView เป็นคนเรียก `Bind()` ตอน spawn)
- `ApplyActivity()` รันเฉพาะตอน `Bind()` — ไม่มี Update/polling (เมื่อมี
  `NpcActivityChangedMessage` ในอนาคตจะเปลี่ยนมา subscribe แทน)
- เช็ค `_animator.HasState(0, hash)` ก่อน `Play()` กัน warning ถ้า state หายไปจาก controller
- คุม guard: Animator ไม่มี controller → log warning ชัดเจนแทน throw

## Installation (ตั้งค่าแล้ว)
- Component นี้ถูกเพิ่มลง **prefab** `001 Student 1 Character.prefab` โดยตรง
  (พร้อมกับผูก `Basic.controller` ให้ Animator) — ทุก chibi ที่ spawn จึงมีในตัว

## TODO / Known Issues
- `Gathering` ยัง map เป็น `walk` ชั่วคราว (รอ schedule system + animation เฉพาะ)
- ยังไม่ต่อ `NpcActivityState` แบบ real-time — ตอนนี้ activity เปลี่ยนไม่ได้เพราะ
  `TickBehavior` ของ NpcDirectorSystem ยังเป็น placeholder
- อนาคตอาจต้องรองรับ SpriteLibrary เพื่อสลับชุด/สีตัวละครระหว่าง NPC (asset รองรับ)
