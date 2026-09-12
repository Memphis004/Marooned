---
title: 2026-09-13-script-execute-roslyn-vtable
type: bug-log
sources: []
related: ["mcp-bridge", "mcp-tool-table", "ui-tmp-migration"]
folder: sources/bug-log
created: 2026-09-13
tags:
  - bug
  - mcp
  - roslyn
  - script-execute
  - nuget
---

# Bug: script-execute — TypeLoadException "invalid vtable method slot 4"

**วันที่:** 2026-09-13 · **อาการ:** เรียก MCP tool `script-execute` แล้วพังทุกครั้งด้วย
`TypeLoadException: Type Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions has
invalid vtable method slot 4 with method none` — plugin compile ผ่าน แต่ execute ไม่ได้เลย

## รากเหตุ (3 ชั้นซ้อนกัน)

1. **Roslyn 2 ชุดใน AppDomain เดียว** — `Assets/Plugins/NuGet/` (Roslyn 4.8 ที่
   DependencyResolver ของ gamedev-mcp ติดตั้ง) ทับซ้อนกับ `com.unity.pipeline` 0.6.0-exp.1
   (Roslyn 3.11) — สองชุดมี assembly identity เดียวกัน (`Microsoft.CodeAnalysis[.CSharp]`)
   → โหลดคู่กันไม่ได้ → vtable error เวลา type จาก CSharp.dll ไปจับกับ Common.dll ผิดเวอร์ชัน
   (`com.unity.ai.assistant` ก็มี Roslyn 6.4/6.5 แต่ rename เป็น `_AIA` ไว้แล้ว ไม่ชน identity
   — ชนก็ตอน script เอ่อร้องขอ type คลุมเครือ CS0433 เท่านั้น)
2. **Legacy migration ค้าง** — `NuGetLegacyMigration` ตอนติดตั้ง (Sep 6) พยายามลบ DLL เก่า
   ที่ถูก Editor lock → ไม่สำเร็จ → เรียก `NuGetPluginConfigurator.DisableImporter` ปิด
   import ไว้ก่อน 8 ไฟล์ (Roslyn CSharp + System.* stack) แล้วรอลบวันหลัง แต่**ไม่มีวันหลัง**
   — ไฟล์ยังอยู่แต่ disabled ใน .meta
3. **แค่รอ recompile ครั้งแรก** — ตอนทำ [[ui-tmp-migration]] วันเดียวกัน มีการถอด
   `com.unity.pipeline` ออกจาก manifest → DependencyResolver เจอ UPM change → รัน NuGet
   restore → recompile ครั้งใหญ่ → CS0234 `'CSharp' does not exist` เพราะ CSharp.dll
   โดนปิดอยู่ แล้ว CS0012 ไล่มาเรื่อยๆ ทีละ dependency ที่โดนปิด

## วิธีแก้ (ครบ 3 จุด)

1. ถอด `com.unity.pipeline` ออกจาก `Marooned/Packages/manifest.json`
   (ยินยอมจากเจ้าของโปรเจค — package experimental ซ้ำซ้อนกับ gamedev-mcp)
2. แก้ `.meta` ของ `Assets/Plugins/NuGet/Microsoft.CodeAnalysis.CSharp.dll` กลับเป็น
   enabled (Any: enabled 1 + Exclude Editor: 0, Editor: enabled 1)
3. เปิดเท่ากันสำหรับ dependency stack ที่โดนปิด: `System.Collections.Immutable`,
   `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`,
   `System.Reflection.Metadata`, `System.Threading.Tasks.Extensions`,
   `System.ComponentModel.Annotations` → refresh จน compile ผ่าน

**หลักฐานสุดท้าย:** body mode คืน `{"typeName":"System.Void","value":"Success"}` และ
full-code mode คืน `"full-code OK"` — Roslyn compile + execute ใช้ได้จริง

## บทเรียน

- **TypeLoadException แบบ vtable = แปลว่ามี assembly duplicate** — อย่าไปไล่แก้โค้ด plugin
  ให้เสียเวลา ให้หาว่าใครแครงโหลด Roslyn/ไลบรารีวิเคราะห์โค้ดบ้าง
- Unity ไม่จัดการ duplicate assembly identity ให้ — package ที่ดีจะ rename dll ของตัวเอง
  (เห็นชัดจาก `_AIA` suffix ของ ai.assistant)
- `DisableImporter` + "deletion retried after next domain reload" เป็นระเบิดเวลา:
  ถ้า reload ไม่เคยกลับมาลบจริง จะได้ DLL ค้าง disabled แล้วระเบิดตอน recompile ครั้งถัดไป
- script-execute **body mode เป็น void Main()** — คืนค่าผ่าน Debug.Log หรือ side effect;
  อยากได้ return value ให้ใช้ full-code mode เขียน `public static object Main()`
- แก้ meta DLL ต้องเข้าใจ 2 ลาย: `Any enabled+Editor enabled` (ธรรมดา) และ
  `Any enabled+Exclude Editor:1+Editor 0` (กรณี Unity มี built-in ทับซ้อน — อย่าเพิ่งเปิดทั้งหมด
  ถ้าไม่จำเป็น)

## ของที่ยังค้าง (ไม่บล็อก แต่ควรรู้)

- ✅ **แก้แล้วตามมา (2026-09-13)**: GUID conflicts ระหว่าง `Assets/NuGet/` (Legacy
  NuGetForUnity install เก่า) กับ UPM package — ลบ `Assets/NuGet/` แล้ว แต่พบว่า UPM
  package `com.github-glitchenzo.nugetforunity` เองก็ compile พังตาม (ตัวมันอ้าง
  NuGetForUnity.PluginAPI ที่เคยโดน legacy folder แย่ง GUID จน Unity cache สถานะ ignored)
  และ MCP plugin ไม่ได้ใช้ API ของมันเลย (มี DependencyResolver ของตัวเอง) → ถอด
  UPM package ออกจาก manifest ด้วย → คอนโซลเคลียร์ ไม่มี GUID conflict เหลือแล้ว
- `Library/PackageCache/com.unity.pipeline@...` ยังเหลือ folder (inert) — จะถูก sweep เอง
- script ที่พิมพ์ type ตรงๆ อย่าง `CSharpCompilationOptions` จะเจอ CS0433 กับ `_AIA` copy —
  script ทั่วไปไม่ใช้ Roslyn type จึงไม่กระทบ
