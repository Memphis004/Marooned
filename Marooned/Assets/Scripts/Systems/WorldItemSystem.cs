using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab B Phase 3 — spawn ไอเท็มบนพื้นตามโซน (location) ของผู้เล่น
    /// เป็น plain C# singleton ที่ implement IInitializable (register เป็น
    /// EntryPoint ใน GameLifetimeScope) — subscribe PlayerLocationChangedMessage
    /// แทน polling และ spawn ชุดแรกตอน Initialize
    ///
    /// ตารางไอเท็มต่อโซนยังเป็น mock — TODO Lab A+1: ย้ายไปตาราง Luban
    /// (ZoneLootTable) เมื่อเปิด pipeline; item id ต้องมีใน CardDefs
    /// (CardInventorySystem.TryAdd ตรวจ id กับ CardDefs ทุกครั้ง)
    /// </summary>
    public class WorldItemSystem : IInitializable, IDisposable
    {
        /// <summary>ไอเท็ม 1 ชิ้นที่วางอยู่บนพื้น</summary>
        public class WorldItem
        {
            public string ItemId;
            public string CardId;
            public GameObject GameObject;
        }

        // TODO Lab A+1: mock zone loot table — ย้ายไป Luban table ทีหลัง
        private static readonly Dictionary<string, string[]> ZoneLootTable = new()
        {
            ["beach"] = new[] { "food_coconut" },
            ["jungle_edge"] = new[] { "mat_vine" },
            ["cave_entrance"] = new[] { "mat_stone" },
        };

        private readonly GameStateProvider _stateProvider;
        private readonly LubanDataService _data;
        private readonly ISubscriber<PlayerLocationChangedMessage> _playerLocationSubscriber;
        private readonly System.Random _rng = new();

        /// <summary>จำนวนไอเท็มต่อโซน (ปรับได้)</summary>
        public int ItemsPerZone = 3;

        /// <summary>รัศมีกระจายไอเท็มรอบจุดกึ่งกลางโซน (world unit)</summary>
        public float ZoneSpread = 2.5f;

        private readonly List<WorldItem> _activeItems = new();
        private IDisposable _subscription;
        private Transform _itemLayer;
        private Sprite _itemSprite;
        private Font _labelFont;

        public IReadOnlyList<WorldItem> ActiveItems => _activeItems;

        public WorldItemSystem(GameStateProvider stateProvider, LubanDataService dataService,
            ISubscriber<PlayerLocationChangedMessage> playerLocationSubscriber)
        {
            _stateProvider = stateProvider;
            _data = dataService;
            _playerLocationSubscriber = playerLocationSubscriber;
        }

        public void Initialize()
        {
            // host transform ของไอเท็มทั้งหมด (สร้างเอง ไม่ผูกกับ scene object)
            var host = new GameObject("WorldItemLayer");
            _itemLayer = host.transform;
            _itemSprite = CreateCircleSprite();
            _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // spawn ชุดแรกตาม location เริ่มต้น + subscribe การย้ายโซน
            SpawnForLocation(_stateProvider.Player.CurrentLocationId);
            _subscription = _playerLocationSubscriber.Subscribe(msg => RespawnForLocation(msg.NewLocationId));
        }

        public void Dispose() => _subscription?.Dispose();

        private void RespawnForLocation(string locationId)
        {
            ClearItems();
            SpawnForLocation(locationId);
        }

        private void SpawnForLocation(string locationId)
        {
            if (!_data.LocationDefs.TryGetValue(locationId, out var locationDef)) return;
            if (!ZoneLootTable.TryGetValue(locationId, out var lootIds) || lootIds.Length == 0) return;

            for (int i = 0; i < ItemsPerZone; i++)
            {
                // เวียนสลับ card id ในโซน (ตอนนี้โซนละ 1 ชนิด) + สุ่มตำแหน่งใน bounds
                var cardId = lootIds[i % lootIds.Length];
                var displayName = _data.CardDefs.TryGetValue(cardId, out var cardDef)
                    ? cardDef.DisplayName : cardId;

                var go = new GameObject($"item_{cardId}_{i + 1}");
                go.transform.SetParent(_itemLayer);

                var basePos = new Vector2(locationDef.WorldX, locationDef.WorldY);
                var offset = new Vector2(
                    (float)(_rng.NextDouble() * 2 - 1) * ZoneSpread,
                    (float)(_rng.NextDouble() * 2 - 1) * (ZoneSpread * 0.6f));
                go.transform.position = basePos + offset;

                // placeholder visual: วงกลมเขียว + ป้ายชื่อไอเท็ม
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _itemSprite;
                renderer.sortingOrder = 1;

                var labelGo = new GameObject("label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                var label = labelGo.AddComponent<TextMesh>();
                label.text = displayName;
                label.fontSize = 24;
                label.characterSize = 0.12f;
                label.anchor = TextAnchor.MiddleCenter;
                label.font = _labelFont;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = _labelFont.material;

                _activeItems.Add(new WorldItem
                {
                    ItemId = go.name,
                    CardId = cardId,
                    GameObject = go,
                });
            }

            Debug.Log($"[WorldItemSystem] spawn {_activeItems.Count} items @ {locationId}");
        }

        private void ClearItems()
        {
            foreach (var item in _activeItems)
                if (item.GameObject != null) UnityEngine.Object.Destroy(item.GameObject);
            _activeItems.Clear();
        }

        /// <summary>
        /// หาไอเท็มใกล้ผู้เล่นที่สุดภายในรัศมี — เรียกโดย ItemPickupSystem
        /// (direct method call ไม่ใช่ message เพราะเป็น continuous query)
        /// </summary>
        public bool TryGetNearest(Vector2 position, float radius, out WorldItem item)
        {
            item = null;
            float bestSqr = radius * radius;
            foreach (var candidate in _activeItems)
            {
                if (candidate.GameObject == null) continue;
                var sqr = ((Vector2)candidate.GameObject.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; item = candidate; }
            }
            return item != null;
        }

        /// <summary>เอาไอเท็มออกจากพื้น (หลังเก็บ) — destroy GameObject และถอดจาก registry</summary>
        public void RemoveItem(WorldItem item)
        {
            if (item.GameObject != null) UnityEngine.Object.Destroy(item.GameObject);
            _activeItems.Remove(item);
        }

        /// <summary>วงกลมสีเขียว placeholder สำหรับไอเท็ม (สร้าง texture ใน code)</summary>
        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var inside = new Vector2(x - center, y - center).sqrMagnitude <= center * center;
                    tex.SetPixel(x, y, inside ? new Color(0.35f, 0.85f, 0.45f) : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
