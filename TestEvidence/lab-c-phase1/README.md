# Test Evidence — Lab C Phase 1 (Biome Scatter + Tool-Gathering + MCP)

> วิธีรันและหลักฐานของ Tests A–D — รายละเอียดระบบอยู่ใน
> `marooned-wiki/wiki/sources/architecture/biome-scatter-system.md`

## ครั้งแรก: Setup (หนึ่งครั้ง — Editor automation)

Unity Editor เปิดโปรเจกต์นี้อยู่ → เมนู:

```
Marooned > Lab C > Setup Biome Scatter Test Scene
```

ทำให้ทั้งหมด (idempotent — รันซ้ำได้ ไม่ทับของจริง):

1. สร้าง placeholder prefabs 9 ตัว + `BiomePrefabSet` 3 ชุด
   (`beach`/`jungle`/`cave`) ที่ `Assets/Resources/BiomePrefabSets/`
2. เพิ่ม GameObject `BiomeScatterLayer` (ติด `BiomeScatterView`) ใต้
   `GameLifetimeScope` แล้ว **บันทึก scene ให้เอง**
3. รอให้ Unity compile เสร็จ (ไม่มี error ใน Console)

> Script: `Marooned/Assets/Editor/BiomeScatterTestSetup.cs`

## รันเทส (ทุก session)

### เตรียม

1. กด **Play** ใน Unity — Console ควรขึ้น
   `[LubanDataService] loaded ... 3 biomes, 3 harvestable nodes`
2. ทุกเทสเขียนไฟล์หลักฐานลงโฟลเดอร์นี้อัตโนมัติ

### Test A — Initial spawn (Unity menu)

```
Marooned > Lab C > Run Test A — Initial Spawn
```

ตรวจ: props+nodes กระจายใน scene, พื้น tint สี beach, ป้าย node แสดง durability/tool
→ ไฟล์ `test-a-initial-spawn.txt` (มี RESULT: PASS/FAIL ท้ายไฟล์)

### Test B — MCP round-trip (CLI, ระหว่าง Play mode ค้างไว้)

```bash
cd McpBridge && dotnet build && cd ..
python Tools/mcp_roundtrip.py          # ลบไฟล์เก่า รันใหม่ทั้งชุด
# หรือแค่เช็คการเชื่อมต่อ: python Tools/mcp_roundtrip.py --smoke
```

ลำดับที่ driver ยิง (ผลจริงเขียน `mcp-roundtrip.jsonl` ทีละ step):

| Step | Tool call | คาดหวัง |
|---|---|---|
| 1 | `GetGameState` | ตอบกลับได้ (bridge ↔ Unity ต่อกัน) |
| 2 | `HarvestNode` (auto tool) | ได้ไอเท็มถ้ามี node ในรัศมี/missing_tool ถ้าไม่มี tool |
| 3 | `MoveToLocation cave_entrance` | success → biome cave โผล่แทน (ดู Game view) |
| 4 | `HarvestNode` | ของ cave (`rock_stone` เป็นไปได้) |
| 5 | `MoveToLocation deep_jungle` (ข้าม jungle_edge) | **fail** `not_connected` |
| 6–7 | `MoveToLocation jungle_edge` → `deep_jungle` | success ทั้งคู่ |
| 8 | `MoveToLocation atlantis` | **fail** `unknown_location` |
| 9 | `GetGameState` (final) | location = `deep_jungle` |

### Test C — Harvest with auto tool (Unity menu)

```
Marooned/Lab C/Run Test C — Harvest With Auto Tool
```

เทเลพอตผู้เล่นไปข้าง node ใกล้สุด → เรียก `TryHarvestNearest()` (path เดียวกับกด E)
→ เทียบ inventory ก่อน/หลัง → ไฟล์ `test-c-harvest.txt`
(คาดหวัง: ได้ไอเท็มเข้ามือ — `tool_axe`/`tool_pickaxe` ถูกเลือกเองถ้า node ต้องใช้)

### Test D — Deplete until regrow (Unity menu)

```
Marooned/Lab C/Run Test D — Deplete Until Regrow
```

เก็บ `bush_berry` ต้นเดียวซ้ำจน durability หมด → ตรวจว่าเข้าสู่ regrow
(ป้ายจาง + `(กำลังงอกใหม่…)` ใน Game view, เก็บซ้ำได้ `regrowing`)
→ ไฟล์ `test-d-regrow.txt`

### เก็บ log ทั้ง session

```
Marooned/Lab C/Dump Play Logs
```

เขียน log ทั้งหมดที่ recorder เก็บไว้ → `play-logs.txt`

## สถานะ

| Test | ไฟล์หลักฐาน | สถานะ |
|---|---|---|
| A — initial spawn | `test-a-initial-spawn.txt` | ⏳ รอรัน (Setup + Play + เมนู A) |
| B — MCP round-trip | `mcp-roundtrip.jsonl` | ⏳ รอรัน (`python Tools/mcp_roundtrip.py`) |
| C — harvest auto tool | `test-c-harvest.txt` | ⏳ รอรัน (เมนู C) |
| D — deplete → regrow | `test-d-regrow.txt` | ⏳ รอรัน (เมนู D) |

> อัปเดตตารางนี้เป็น ✅ พร้อมวันที่ เมื่อรันแล้ว — log ดิบทั้ง session อยู่ใน `play-logs.txt`
