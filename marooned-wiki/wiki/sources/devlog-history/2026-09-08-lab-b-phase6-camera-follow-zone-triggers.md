---
title: 2026-09-08
type: devlog
sources:
  - "[[sources/architecture/player-system]]"
folder: devlog-history
created: 2026-09-08
tags:
  - devlog
  - lab-b
  - camera
  - zone-triggers
---

# Dev Log — 2026-09-08

## 📌 สรุปวัน
เพิ่มระบบกล้องตามผู้เล่น (Camera Follow) และโซน Trigger (Zone Triggers) ให้ Player System Lab B Phase 6

## ✅ สิ่งที่ทำได้วันนี้

### 1. Camera Follow
- ใช้ `Camera.main.transform.position` ติดตาม `PlayerCharacterView.transform.position` + offset (z = -10)
- ปรับให้กล้องคงอยู่กลางจอเสมอ ไม่กระตุ้นการสั่นของ physics
- เพิ่มสคริปต์ `CameraFollow.cs` ลงใน `MainCamera` และลงทะเบียนใน DI (`GameLifetimeScope`)

### 2. Zone Triggers
- สร้าง Collider2D (IsTrigger) บน GameObject โซนแต่ละอัน
- เมื่อ Player ส่ง `PlayerLocationChangedMessage` ไปยังโซนใหม่ จะเปลี่ยน loot table ของ `WorldItemSystem` ตามโซน
- เพิ่ม `ZoneTriggerSystem` ที่ Subscribe `PlayerLocationChangedMessage` และจัดการเปลี่ยน `WorldItemSystem.CurrentLootTable`

### 3. Integration Tests
- สร้าง Unit Test `CameraFollowTests.cs` ตรวจสอบว่ากล้องตามผู้เล่นอย่างถูกต้องหลังการเคลื่อนที่
- สร้าง Integration Test `ZoneTriggerSystemTests.cs` ตรวจสอบการเปลี่ยน loot เมื่อเข้าโซนต่าง ๆ

## ⚙️ Next Steps
- ปรับ UI ให้แสดงโซนปัจจุบัน (Mini‑Map หรือ UI Indicator)
- ปรับให้ Camera Follow รองรับหลายผู้เล่นในโหมด Multiplayer‑ready
