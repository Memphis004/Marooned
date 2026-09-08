using System;
using System.Collections.Generic;
using Marooned.Shared;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab B Phase 3 → Phase 6.1 — spawn ไอเท็มบนพื้นตามโซน (location) ของผู้เล่น
    /// แบบ Per-Location Loot Persistence: แต่ละโซนมี state ของตัวเอง
    /// (Dictionary&lt;string, LocationLoot&gt;) — สุ่ม loot จาก mock table ครั้งเดียวตอน
    /// เข้าโซนครั้งแรก แล้วจด CardId/Position/Picked ไว้ตลอดทั้งรอบ:
    ///   • ออกจากโซน   → Destroy เฉพาะ GameObject (state คงเดิม, GameObject = null)
    ///   • กลับเข้าโซน → Instantiate ใหม่เฉพาะ item ที่ Picked == false ที่ตำแหน่งเดิม
    ///   • เก็บของ     → ItemPickupSystem เรียก MarkPicked() เพื่อ set Picked = true
    /// แก้บั๊กเดิม (Phase 6.1): RespawnForLocation() เคย return ก่อน ClearItems()
    /// เมื่อโซนอยู่ใน _spawnedLocations แล้ว ทำให้ไอเท็มโซนเก่าค้างจอและไอเท็มของ
    /// โซนที่กลับเข้าไม่โผล่เลย
    ///
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
        /// <summary>state ถาวรของไอเท็ม 1 ชิ้นในโซน — รอดจากการออก/กลับเข้าโซน (GameObject = null คืออยู่นอกจอ)</summary>
        public class ItemState
        {
            public string CardId;
            public Vector2 Position;
            public bool Picked;
            public GameObject GameObject;
        }

        /// <summary>state loot ทั้งโซน — สุ่มครั้งเดียวตอนเข้าโซนครั้งแรก แล้วคงอยู่ตลอดรอบ</summary>
        public class LocationLoot
        {
            public string LocationId;
            public List<ItemState> Items = new();
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

        /// <summary>state ต่อโซน (key = locationId) — สุ่ม loot ครั้งเดียวตอนเข้าครั้งแรก</summary>
        private readonly Dictionary<string, LocationLoot> _locationLoots = new();

        /// <summary>ไอเท็มที่กำลังแสดงบนจอ (ของโซนปัจจุบันเท่านั้น — GameObject != null เสมอ)</summary>
        private readonly List<ItemState> _activeItems = new();

        private string _activeLocationId;
        private IDisposable _subscription;
        private Transform _itemLayer;
        private Sprite _itemSprite;
        private Font _labelFont;

        /// <summary>ไอเท็มที่กำลังแสดงบนจอ (ใช้โดย ItemPickupSystem)</summary>
        public IReadOnlyList<ItemState> ActiveItems => _activeItems;

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
            _activeLocationId = _stateProvider.GetPlayer().CurrentLocationId;
            EnterLocation(_activeLocationId);
            _subscription = _playerLocationSubscriber.Subscribe(OnPlayerLocationChanged);
        }

        public void Dispose() => _subscription?.Dispose();

        /// <summary>ย้ายโซน: เก็บ GameObject ของโซนเดิม (state คงเดิม) แล้วเข้าโซนใหม่</summary>
        private void OnPlayerLocationChanged(PlayerLocationChangedMessage msg)
        {
            if (msg.NewLocationId == _activeLocationId) return; // กัน event ซ้ำ (อยู่โซนเดิม)
            LeaveLocation();
            _activeLocationId = msg.NewLocationId;
            EnterLocation(_activeLocationId);
        }

        /// <summary>ออกจากโซนปัจจุบัน: Destroy เฉพาะ GameObject — Picked/Position คงเดิมใน state</summary>
        private void LeaveLocation()
        {
            foreach (var item in _activeItems)
            {
                if (item.GameObject == null) continue;
                UnityEngine.Object.Destroy(item.GameObject);
                item.GameObject = null;
            }
            _activeItems.Clear();
        }

        /// <summary>
        /// เข้าโซน: ครั้งแรก → สุ่ม loot + ตำแหน่งแล้วจด state ถาวร;
        /// ทุกครั้ง → Instantiate ใหม่เฉพาะ item ที่ยังไม่ถูกเก็บ (Picked == false) ที่ตำแหน่งเดิม
        /// </summary>
        private void EnterLocation(string locationId)
        {
            if (!_data.LocationDefs.TryGetValue(locationId, out var locationDef)) return;

            if (!_locationLoots.TryGetValue(locationId, out var loot))
            {
                loot = RollLoot(locationId, locationDef);
                _locationLoots[locationId] = loot;
            }

            int spawned = 0;
            int picked = 0;
            for (int i = 0; i < loot.Items.Count; i++)
            {
                var item = loot.Items[i];
                if (item.Picked) { picked++; continue; }      // เก็บไปแล้ว — ไม่ respawn
                if (item.GameObject != null) continue;        // กัน spawn ซ้ำ
                InstantiateWorldItem(item, i);
                _activeItems.Add(item);
                spawned++;
            }

            Debug.Log($"[WorldItemSystem] enter {locationId}: spawn {spawned} item (state รวม {loot.Items.Count}, เก็บไปแล้ว {picked})");
        }

        /// <summary>สุ่ม loot ของโซนครั้งเดียว (ครั้งแรกที่เขย) — จด state ไว้ใน _locationLoots</summary>
        private LocationLoot RollLoot(string locationId, LocationDef locationDef)
        {
            var loot = new LocationLoot { LocationId = locationId };
            if (!ZoneLootTable.TryGetValue(locationId, out var lootIds) || lootIds.Length == 0)
                return loot; // โซนไม่มี loot table — โล่ง

            var basePos = new Vector2(locationDef.WorldX, locationDef.WorldY);
            for (int i = 0; i < ItemsPerZone; i++)
            {
                // เวียนสลับ card id ในโซน (ตอนนี้โซนละ 1 ชนิด) + สุ่มตำแหน่งใน bounds
                var cardId = lootIds[i % lootIds.Length];
                var offset = new Vector2(
                    (float)(_rng.NextDouble() * 2 - 1) * ZoneSpread,
                    (float)(_rng.NextDouble() * 2 - 1) * (ZoneSpread * 0.6f));
                loot.Items.Add(new ItemState
                {
                    CardId = cardId,
                    Position = basePos + offset,
                    Picked = false,
                    GameObject = null,
                });
            }
            return loot;
        }

        /// <summary>สร้าง GameObject ของไอเท็ม 1 ชิ้นบนจอ (placeholder: วงกลมเขียว + ป้ายชื่อ)</summary>
        private void InstantiateWorldItem(ItemState item, int index)
        {
            var displayName = _data.CardDefs.TryGetValue(item.CardId, out var cardDef)
                ? cardDef.DisplayName : item.CardId;

            var go = new GameObject($"item_{item.CardId}_{index + 1}");
            go.transform.SetParent(_itemLayer);
            go.transform.position = item.Position; // ตำแหน่งเดิมจาก state

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

            item.GameObject = go;
        }

        /// <summary>
        /// หาไอเท็มใกล้ผู้เล่นที่สุดภายในรัศมี — เรียกโดย ItemPickupSystem
        /// (direct method call ไม่ใช่ message เพราะเป็น continuous query)
        /// </summary>
        public bool TryGetNearest(Vector2 position, float radius, out ItemState item)
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

        /// <summary>
        /// mark ว่าเก็บไอเท็มแล้ว (เรียกโดย ItemPickupSystem หลัง TryAdd สำเร็จ) —
        /// set Picked = true ถาวรใน state ของโซน + destroy GameObject ออกจากจอ
        /// (item ที่ Picked แล้วจะไม่ respawn เมื่อกลับเข้าโซนเดิม)
        /// </summary>
        public void MarkPicked(ItemState item)
        {
            item.Picked = true;
            if (item.GameObject != null)
            {
                UnityEngine.Object.Destroy(item.GameObject);
                item.GameObject = null;
            }
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
