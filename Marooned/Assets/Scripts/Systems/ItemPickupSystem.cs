using Marooned.Core;
using Marooned.Shared;
using MessagePipe;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>โหมดการเก็บไอเท็ม (สลับได้เพื่อปรับดีไซน์)</summary>
    public enum PickupMode
    {
        WalkOver,   // เดินทับ → เก็บอัตโนมัติ
        InteractKey, // ยืนใกล้ + กด E/Space → เก็บ (default)
    }

    /// <summary>
    /// Lab B Phase 3 — เก็บไอเท็ม: หาไอเท็มใกล้สุด → mark Picked ใน WorldItemSystem
    /// (Lab B Phase 6.1: per-location persistence — item ที่เก็บแล้วไม่ respawn) →
    /// เพิ่มเข้า CardInventorySystem → publish ItemPickedUpMessage (discrete event
    /// ครั้งเดียวต่อการเก็บ 1 ครั้ง ไม่ยิงทุกเฟรม) — Tick ผ่าน GameTickDriver เดิม
    /// </summary>
    public class ItemPickupSystem
    {
        private readonly PlayerInputService _input;
        private readonly GameStateProvider _stateProvider;
        private readonly WorldItemSystem _worldItems;
        private readonly CardInventorySystem _inventory;
        private readonly IPublisher<ItemPickedUpMessage> _pickupPublisher;

        /// <summary>โหมดเก็บเริ่มต้น: InteractKey (E/Space)</summary>
        public PickupMode Mode = PickupMode.InteractKey;

        /// <summary>รัศมีเก็บ (world unit)</summary>
        public float PickupRadius = 1.6f;

        public ItemPickupSystem(PlayerInputService input, GameStateProvider stateProvider,
            WorldItemSystem worldItems, CardInventorySystem inventory,
            IPublisher<ItemPickedUpMessage> pickupPublisher)
        {
            _input = input;
            _stateProvider = stateProvider;
            _worldItems = worldItems;
            _inventory = inventory;
            _pickupPublisher = pickupPublisher;
        }

        public void Tick(float deltaSeconds)
        {
            if (_worldItems.ActiveItems.Count == 0) return;

            var player = _stateProvider.GetPlayer();
            var position = new Vector2(player.PositionX, player.PositionY);

            if (Mode == PickupMode.InteractKey)
            {
                // Peek ก่อน — ถ้าไม่มีการกดปุ่ม ไม่ต้องทำอะไรต่อ
                if (!_input.IsInteractPressed) return;

                // เช็คว่ามีไอเท็มอยู่ในระยะจริงไหม "ก่อน" ตัดสินใจกินปุ่ม —
                // ถ้าไม่มี ปล่อยปุ่มผ่านไปให้ NodeHarvestSystem (Tick ถัดไปในเฟรม
                // เดียวกัน) มีโอกาสได้ใช้ ป้องกัน bug เดิม: WorldItemSystem ยังมี
                // ของเหลืออยู่ในโซน (ActiveItems.Count > 0) แต่ผู้เล่นยืนใกล้ node
                // ไม่ใกล้ไอเท็มบนพื้น → ปุ่ม E ถูกกินทิ้งฟรีไม่ได้ผลอะไรเลย
                if (!_worldItems.TryGetNearest(position, PickupRadius, out _)) return;

                _input.ConsumeInteractPressed(); // ยืนยันว่าจะใช้ปุ่มนี้จริง ค่อยกิน
                if (TryPickupNearest(position))
                    Debug.Log($"[ItemPickupSystem] เก็บ (InteractKey) รอบตัวผู้เล่น @ {player.CurrentLocationId}");
            }
            else // WalkOver
            {
                TryPickupNearest(position);
            }
        }

        private bool TryPickupNearest(Vector2 position)
        {
            if (!_worldItems.TryGetNearest(position, PickupRadius, out var item)) return false;

            // เก็บได้ต่อเมื่อ id มีใน CardDefs (TryAdd ตรวจให้) — กัน id mock หลุดตาราง
            if (!_inventory.TryAdd(item.CardId, 1))
            {
                Debug.LogWarning($"[ItemPickupSystem] card '{item.CardId}' ไม่มีใน CardDefs — เก็บไม่ได้");
                return false;
            }

            var locationId = _stateProvider.GetPlayer().CurrentLocationId;
            Debug.Log($"[ItemPickupSystem] picked up '{item.CardId}' @ {locationId}");
            // Lab B Phase 6.1: mark Picked = true ใน state ของโซน (per-location
            // persistence) — destroy GameObject + ไม่ respawn เมื่อกลับเข้าโซนเดิม
            _worldItems.MarkPicked(item);

            // discrete event — ยิงครั้งเดียวต่อการเก็บ (PlayerCharacterView ใช้ trigger anim "pick up")
            _pickupPublisher.Publish(new ItemPickedUpMessage { ItemId = item.CardId, LocationId = locationId });
            return true;
        }
    }
}
