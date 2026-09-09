using System.Collections.Generic;
using MessagePack;

namespace Marooned.Shared
{
    /// <summary>
    /// Inventory ของ NPC (ground truth) — ต่างจากผู้เล่นที่ใช้ Dictionary แบนใน
    /// PlayerSurvivalState.Inventory เพราะ NPC ต้องพก "role-enabling item" อย่าง
    /// อาวุธของ Killer ที่ห้ามหลุดออกไปเห็นจากภายนอกเด็ดขาด
    ///
    /// เป็นสมาชิกของ NpcState (ground truth ที่ห้าม serialize ออกนอก Unity ตรงๆ
    /// อยู่แล้ว ดูหมายเหตุบน NpcState) — [MessagePackObject] ใส่ไว้เพื่อผ่าน
    /// MessagePack analyzer (MsgPack003) เท่านั้น ไม่ได้แปลว่าอนุญาตให้ expose
    ///
    /// Information Hiding: class นี้ห้ามโผล่ใน NpcObservableView /
    /// GetObservableNpcsAt / MCP response ใดๆ — อ่านได้เฉพาะระบบภายใน
    /// เช่น KillerPlanner (Step 4: HasWeapon ก่อน Executing)
    /// </summary>
    [MessagePackObject]
    public class NpcInventory
    {
        /// <summary>cardId -> count (cardId ต้องมีจริงใน DataTables/CardDef.csv ผ่าน LubanDataService.CardDefs)</summary>
        private readonly Dictionary<string, int> _items = new();

        /// <summary>จำนวน item ชนิดที่ระบุ (0 = ไม่มี)</summary>
        public int Count(string cardId) => _items.TryGetValue(cardId, out var count) ? count : 0;

        public bool HasItem(string cardId) => Count(cardId) > 0;

        public void AddItem(string cardId, int count = 1)
        {
            if (count <= 0) return;
            _items.TryGetValue(cardId, out var current);
            _items[cardId] = current + count;
        }

        /// <summary>หัก item — return false ถ้าไม่มีพอ (ไม่ mutate อะไรเลยในกรณีนั้น)</summary>
        public bool RemoveItem(string cardId, int count = 1)
        {
            if (count <= 0) return true;
            if (!HasItem(cardId) || Count(cardId) < count) return false;

            var remaining = Count(cardId) - count;
            if (remaining == 0) _items.Remove(cardId);
            else _items[cardId] = remaining;
            return true;
        }
    }
}
