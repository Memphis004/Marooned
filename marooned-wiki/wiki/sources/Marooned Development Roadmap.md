
เอกสารนี้บันทึกแผนการพัฒนาเกม Marooned เป็นระยะ (Phases) เพื่อรักษาทิศทางของสถาปัตยกรรมและป้องกันการทำงานที่ซ้อนทับกัน (Scope Creep)

> **หลักการสำคัญ:** ทำระบบที่ "พึ่งพากันเอง" (Self-contained loops) ให้เสร็จก่อน แล้วค่อยขยายมิติของเกม

---

## 🗺️ Master Roadmap

| # | ฟีเจอร์ / ระบบ | สถานะ | คำอธิบายสั้นๆ และ Dependency |
|---|---|---|---|
| **1** | **Hybrid Transition Points** | 🟢 กำลังทำ | Camera Follow + Zone Transition Triggers + BiomeScatterSystem (Don't Starve style) |
| **2** | **Living NPCs (Embodiment)** | 🟡 รอทำ | NpcSurvivalState (Hunger/Fear), NpcInventory, NpcMovementSystem (Traveling/Idle), Basic AI Hooks (InnocentUtilityAI + KillerPlanner) |
| **3** | **Debug Overlay** | ⚪ วางแผน | UI แสดงค่า Ground Truth (Hunger, Fear, Target) ของ NPC เพื่อใช้ Debug AI Decision โดยไม่ละเมิด Information Hiding ในเกมจริง |
| **4** | **Clue System v2** | ⚪ วางแผน | Instance model + Generation pipeline (ศพ = interactable node yield clue) + graph data + incidental hooks |
| **5** | **Clue Board Graph View** | ⚪ วางแผน | UI แบบ Node-Graph (Thought Cloud reframe) + MCP tool `get_clue_graph` สำหรับให้ AI infer ความสัมพันธ์ของเบาะแส |
| **6** | **Accuse() → TryEliminate Merge** | ⚪ วางแผน | รวมเส้นทางกำจัด NPC ให้ใช้ pipeline เดียวกับ #4 (รองรับการ spawn clue จากกรณีที่ accuse ผิด/ถูก) |
| **7** | **Vision-based No-Witness + WeatherSystem (mechanic only)** | ⚪ วางแผน | ปรับปรุงกฏ No-Witness เพิ่มระบบ Vision-based และ WeatherSystem เข้ามา |
| **8** | **Day/Night Cycle** | ⚪ วางแผน | ระบบเวลากลางวัน กลางคืน |
| **9** | **Meeting Phase State Machine** | ⚪ วางแผน | Core deduction loop: หยุดเกมชั่วคราว, นำเสนอเบาะแส, โหวต |
| **10** | **Alibi System + UI** | ⚪ วางแผน | บันทึก SightingLog (ใครเห็นใคร ที่ไหน เมื่อไหร่) + UI สำหรับ Meeting/Accusation |
| **11** | **AwaitNextEvent Timeout** | ⚪ วางแผน | จัดการ TimeoutSeconds เพื่อให้ AI VTuber ไม่ hang รอ event ตอนจบ Meeting Phase |
| **12**| **Task System (Avalon-lite)** | ⚪ วางแผน | ใช้ Meeting state machine (#7) pause โลก + Clue pipeline (#4) ส่งผล sabotage เป็น behavioral clue (ทำหลัง #7-8 เสร็จ) |
| **13**| **Polish: Durability & Collision, Visual fog-of-war/weather particles**| 🔄 แทรกได้อิสระ | Killer weapon durability, Movement collision/world bounds check (ทำแทรกเมื่อระบบหลักนิ่ง) |

---

## 📌 กฎการอัปเดต Roadmap

1. **ห้ามข้ามขั้น (Skip) โดยไม่มีเหตุผล:** ระบบที่อยู่ลำดับหลังมักพึ่งพา Data Model หรือ State Machine ของลำดับก่อนหน้า (เช่น #10 ต้องรอ #7)
2. **อัปเดตสถานะ:** เปลี่ยน 🟡 เป็น 🟢 เมื่อเริ่มทำ และเพิ่ม ✅ เมื่อทำเสร็จพร้อมเทสใน Devlog
3. **Link to Devlog:** เมื่อทำแต่ละข้อเสร็จ ให้ใส่ลิงก์ไปยัง Devlog ของวันนั้นในตาราง (ถ้าทำได้)

---
*Last Updated: 2026-09-10*
