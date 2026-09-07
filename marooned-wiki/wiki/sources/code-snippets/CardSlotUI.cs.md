---
title: CardSlotUI.cs
type: code-snippet
sources: []
related: [CardHandView.cs, CardHandPresenter.cs, UI, MVP_Lite]
folder: sources/code-snippets
created: 2026-09-07
tags:
  - code-snippet
  - ui
  - mvp-lite
  - card-system
  - click-to-use
---

# CardSlotUI.cs

**ตำแหน่งไฟล์จริง:** `Marooned/Assets/Scripts/UI/Views/CardSlotUI.cs`

สแนปบิตนี้คือตรรกะหน้าจอของ "slot การ์ด 1 ใบ" ในมือผู้เล่น (Card Hand) — จัดการ rendering การ์ดที่ slot นั้น, ไอคอน/สี category/ป้ายชื่อ, การ์ดใหม่เด้ง (pop), และ **IPointerClickHandler** สำหรับคลิกการ์ด.

---

## บทบาทในระบบ

- อยู่ใต้ **CardHandView** → ระบบสร้าง slot มาแล้ว inject `Image/icon/label` ให้
- **Presenter** (CardHandPresenter) เป็นคนเรียก `SetCard(...)` บน slot และ subscribe `Clicked`
- **View นี้ passive** — คลิกส่ง event `Clicked(cardId)` ให้ presenter ตัดสินใจเองว่าจะ use/hover/interact ยังไง (Pattern: MVP Lite)

---

## IPointerClickHandler — จุดเน้นของหน้านี้

```csharp
public class CardSlotUI : MonoBehaviour, IPointerClickHandler
{
    public event Action<string> Clicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (string.IsNullOrEmpty(CardId)) return; // pooled slot ว่าง — ไม่ตอบสนอง
        Clicked?.Invoke(CardId);
    }
}
```

### พฤติกรรม

- **ฟัง mouse click** ผ่าน `IPointerClickHandler` (ต้องแน่ใจว่า **EventSystem** + **GraphicRaycaster** มีอยู่หน้าจอ — ปกติ派生จาก `UIRoot` หรือ Canvas ที่มี raycaster)
- **คัดกรอง:**
  - รับเฉพาะ **ซ้าย click** (`Left`button) — click ขวา/อื่นๆเฉย
  - ถ้า slot ว่าง (`CardId == null/empty`, คือ pooled slot ที่ยังไม่ได้ assign การ์ด) — **ไม่ยิง event** (ป้องกัน click ว่าบนพื้นที่ว่างเปล่า)
- **ส่งต่อ** `Clicked?.Invoke(CardId)` — ไม่ตัดสินใจใน view เอง ให้ presenter จัดการ (use card, show hover info, whatever)

### ทำไมไม่ใช้ UnityEvent / Button

- slot ไม่ได้เป็น `Button` component — ลด overhead และ control logic เองได้เหมือนกัน
- ใช้ `IPointerClickHandler` โดยตรงแทน — ตรวจได้ว่าเครื่องมือ pointer จริงๆ hit การ์ดหรือไม่ (ผ่าน UI raycast ของ EventSystem)

---

## ส่วนอื่นๆของไฟล์

### SetCard

```csharp
public void SetCard(string cardId, string displayName, CardCategory category, int count)
{
    CardId = cardId;
    Count = count;
    background.color = ColorFor(category);
    label.text = count > 1 ? $"{displayName} x{count}" : displayName;
}
```

- ตั้งค่าการ์ดที่ slot: **cardId** (ใช้ใน event), **displayName** ป้ายชื่อ, **category** กำหนดสีพื้น, **count** ไอคอน stack
- ไอคอน sprite ยังว่าง — รอ art pipeline (Lab A ยังไม่มี art)

### ColorFor (static)

แมป `CardCategory` → สีพื้น background เพื่อแยกประเภทการ์ดโดยไม่ต้องดู icon:

- Resource → เขียว
- Craftable → ส้ม
- Consumable → เหลืองอ่อน
- Tool → น้ำเงินอ่อน
- Weapon → เทา
- Clue → ม่วง
- Illness/Injury → แดง

### PlayPop + Update

- `PlayPop()` set `_animT = 0` → Update scale up 1.0 → 1.25 → 1.0 ใน 0.18s (ease-out)
- เด้งครั้งเดียวตอนการ์ดใหม่เข้า หรือ count เพิ่ม
- **ไม่ใช้ coroutine/animation clip** — ทำเองใน Update สำหรับ effect สั้นๆ แบบนี้ (เล็กเกินจะคุ้มเสียกับ animator)

---

## สถานะปัจจุบัน (Lab A → Lab B)

- IPointerClickHandler **เขียนไว้แล้วในโค้ด** — พร้อมใช้เมื่อ CardHandPresenter subscribe
- แต่ยังไม่มี scene/prefab จริงที่ต่อ UI ไว้ → **ยังไม่เห็นผลบนเกม**
- `SetCard` ยัง fake icon เพราะ art pipeline ยังไม่พร้อม

---

## เชื่อมโยง

- [[CardHandView.cs]] — owner ของ slot นี้, สร้าง/จัดการ pool
- [[CardHandPresenter.cs]] — subscribe `Clicked` แล้วเรียก `UseCardFromSlot(cardId)`
- [[card-inventory]], [[card-system]] — mechanics ของการ์ด
