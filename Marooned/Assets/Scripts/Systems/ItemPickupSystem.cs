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
    /// Lab B Phase 3 — เก็บไอเท็ม: หาไอเท็มใกล้สุด → เอาออกจากพื้น → เพิ่มเข้า
    /// CardInventorySystem → publish ItemPickedUpMessage (discrete event ครั้งเดียว
    /// ต่อการเก็บ 1 ครั้ง ไม่ยิงทุกเฟรม) — Tick ผ่าน GameTickDriver เดิม
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
                // edge-triggered: กด 1 ครั้ง = พยายามเก็บ 1 ครั้ง
                if (!_input.ConsumeInteractPressed()) return;
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
            _worldItems.RemoveItem(item);

            // discrete event — ยิงครั้งเดียวต่อการเก็บ (PlayerCharacterView ใช้ trigger anim "pick up")
            _pickupPublisher.Publish(new ItemPickedUpMessage { ItemId = item.CardId, LocationId = locationId });
            return true;
        }
    }
}
