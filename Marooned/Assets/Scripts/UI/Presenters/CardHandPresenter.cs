namespace Marooned.UI.Presenters
{
    /// <summary>Plain C#, transient. Reads CardInventorySystem, pushes to CardHandView.</summary>
    public class CardHandPresenter
    {
        // Constructor-injects CardInventorySystem + CardHandView reference; subscribes
        // to inventory-changed message and calls view.RenderHand(...). (Lab A stub)

        // ---- Phase 4 Step 7: use_card feedback ----
        // เมื่อ UI flow ใช้การ์ด (เรียก UseCardHandler ผ่าน MessagePipe request/response)
        // ให้ map FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยก่อนแสดงผล
        // ครอบคลุมทุก reason ที่ UseCardHandler/CanEliminate คืนได้จริง:
        //   unknown_card, missing_target, invalid_target_type,
        //   unknown_target, target_already_dead, target_not_same_location,
        //   witnessed, not_in_inventory

        /// <summary>แปลง FailureReason จาก UseCardResponse เป็นข้อความภาษาไทยสำหรับผู้เล่น</summary>
        public string GetLocalizedReason(string reason) => reason switch
        {
            "witnessed" => "มีคนเห็น! ไม่สามารถลงมือได้",
            "target_not_same_location" => "เป้าหมายไม่ได้อยู่ในโซนเดียวกัน",
            "target_already_dead" => "เป้าหมายนี้ไม่อยู่แล้ว",
            "missing_target" => "ต้องระบุเป้าหมายสำหรับไอเท็มนี้",
            "invalid_target_type" => "การ์ดนี้ใช้กับเป้าหมายไม่ได้",
            "not_in_inventory" => "ไม่มีการ์ดนี้ในมือ",
            "unknown_card" => "ไม่รู้จักการ์ดนี้",
            "unknown_target" => "ไม่พบเป้าหมายนี้",
            _ => "ใช้งานไม่ได้"
        };
    }
}
