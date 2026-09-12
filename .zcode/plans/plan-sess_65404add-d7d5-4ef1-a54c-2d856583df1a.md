แก้เพื่อทำให้ "การเลื่อนจัตถ์ → เลื่อนจริงจริงไม่เพิ่งเพิ่ง teleport อักษร" ตามสถาปัตยกรรมที่มีอยู่ (PlayerMovementSystem = continuous coordinate, ไม่ใช่ node-like NPC)

**ชุดโค้ดรากเหตุ (ยืนยันว่าเป็นจริง):**
- `MoveToLocationHandler.InvokeAsync` (`McpRequestHandlers.cs:197-220`): ตั้งแค่ `CurrentLocationId` (โซนอักษร) + publish `PlayerLocationChangedMessage` — **ไม่แตะ `PositionX/PositionY` เลย**
- `PlayerMovementSystem.Tick` (`PlayerMovementSystem.cs:63`): ทำจาก `input.MoveAxis` ที่ handler ไม่เคยป้อน → **ไม่ขับ**
- `PlayerCharacterView.Update` (`PlayerCharacterView.cs:80`): สแนปsprite จากพิกัดเก่าที่ไม่เปลี่ยน → ท่านจุดเดิม แต่โซนเปลี่ยน

**การแก้จริง (4 หัว):**
1. `PlayerSurvivalState` (หรือ GameStateProvider) — เพิ่ม `CurrentTargetX/CurrentTargetY` + อธิบายล็อคเลื่อนที่เหมาะสม (ใช้กลไก lerp ที่มีอยู่ของ NPC ไม่ต้องสร้างใหม่ — สำหรับเลื่อนจาก MCP move, การเลื่อนเป็นจังหวะเดิมเพราะ VTuber set destination แล้ว)
2. `PlayerMovementSystem.Tick` — เพิ่มการลุ้ย: เมื่อ `input.MoveAxis` = 0 แต่มี `CurrentTargetX/Y` set → เคลื่อนสู่ `TargetX/TargetY` ด้วย `Speed=3.5`, `ArrivalThreshold=0.1`, ตั้ง `Activity: Traveling→Idle` (ถอดท่าสำนวนจาก `NpcMovementSystem.MoveTowardTarget`, line 36-69)
3. `MoveToLocationHandler` — เมื่อ `TryGetValue` สำเร็จ (`targetDef` ไม่ว่าง): แต่ง `PositionX=WorldX, PositionY=WorldY` (หรือ `TargetX/Y`) ก่อน publish message เดิม — ปฏิรูปการเชื่อมกับชั้นอื่น
4. รักษาส่งต่อบ้างอีกที่ `PlayerLocationChangedMessage` (chibi/UI) เป็นเดิม

ไม่แตะ proxy Python / ไม่แตะ `LocationDef` schema — ขอบเขตแค่ตัวเลื่อน; proxy ถือหน่วงเวลาที่เหมาะสมอยู่แล้ว