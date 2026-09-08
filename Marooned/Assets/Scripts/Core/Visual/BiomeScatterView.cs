using System;
using System.Collections.Generic;
using Marooned.Data;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using VContainer;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab C Phase 1 (Hybrid BiomeScatter) — View layer ของ BiomeScatterSystem:
    /// เป็น MonoBehaviour คนเดียวที่ Instantiate/Destroy GameObject (System ห้ามแตะ)
    ///
    /// หน้าที่:
    ///   1. โหลด BiomePrefabSet ทั้งหมดจาก Resources/BiomePrefabSets/ (ชื่อ asset = biomeId)
    ///   2. Subscribe BiomeChangedMessage → เคลียร์ของเก่า → instantiate ตาม
    ///      SpawnsToCreate (prop_{n} → propPrefabs[n], node id → harvestablePrefabs ที่
    ///      prefab.name ตรงกับ id) → ติด HarvestableNodeComponent ให้ node
    ///   3. Subscribe PlayerLocationChangedMessage → map locationId → biomeId
    ///      (LocationDef ยังไม่มี column biome — mapping ตรงนี้ชั่วคราว ดู TODO)
    ///   4. เปลี่ยนสีพื้นตาม BiomeDef.backgroundColor (หรือ groundMaterial ของ
    ///      BiomePrefabSet ถ้าลากใส่)
    ///
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (resolve ผ่าน
    /// GetComponentInParent ตาม convention — ไม่ลาก reference ใน Inspector)
    /// </summary>
    public class BiomeScatterView : MonoBehaviour
    {
        [Header("ปิดได้ถ้าไม่อยากให้ tint พื้นตาม BiomeDef.backgroundColor")]
        [SerializeField] private bool tintGround = true;

        [Header("ความทึบของสีพื้น (placeholder ground)")]
        [SerializeField, Range(0f, 1f)] private float groundAlpha = 0.35f;

        private BiomeScatterSystem _scatterSystem;
        private LubanDataService _data;
        private GameStateProvider _stateProvider;
        private NodeHarvestSystem _nodeHarvest;
        private WorldBounds _worldBounds;

        private readonly Dictionary<string, BiomePrefabSet> _prefabSets = new();
        private readonly List<GameObject> _activeObjects = new();
        private readonly HashSet<string> _warnedUnknownBiomes = new();

        private Transform _objectLayer;
        private GameObject _ground;
        private Sprite _groundSprite;
        private Font _labelFont;
        private IDisposable _biomeSubscription;
        private IDisposable _locationSubscription;

        private void Start()
        {
            // VContainer build container ใน LifetimeScope.Awake → resolve ใน Start
            // (pattern เดียวกับ RoundInitializer/ZoneTransitionTrigger)
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[BiomeScatterView] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return;
            }

            _scatterSystem = scope.Container.Resolve<BiomeScatterSystem>();
            _data = scope.Container.Resolve<LubanDataService>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _nodeHarvest = scope.Container.Resolve<NodeHarvestSystem>();
            _worldBounds = scope.Container.Resolve<WorldBounds>();

            // host transform ของ object ทั้งหมด (สร้างเอง ไม่ผูกกับ scene object)
            var host = new GameObject("BiomeScatterLayer");
            host.transform.SetParent(transform, false);
            _objectLayer = host.transform;
            _groundSprite = CreateWhiteSprite();
            _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            LoadPrefabSets();

            // subscribe ก่อนยิง SetBiome แรก — message ต้องมีคนฟัง
            _biomeSubscription = scope.Container.Resolve<ISubscriber<BiomeChangedMessage>>().Subscribe(OnBiomeChanged);
            _locationSubscription = scope.Container.Resolve<ISubscriber<PlayerLocationChangedMessage>>().Subscribe(OnPlayerLocationChanged);

            // spawn biome เริ่มต้นตาม location ของผู้เล่น
            var playerLocation = _stateProvider.GetPlayer().CurrentLocationId;
            _scatterSystem.SetBiome(MapLocationToBiome(playerLocation));
        }

        private void OnDestroy()
        {
            _biomeSubscription?.Dispose();
            _locationSubscription?.Dispose();
            ClearAll();
        }

        // ---- location → biome mapping (ชั่วคราว) ----
        // TODO Lab C Phase 2: เพิ่ม column biomeId ใน LocationDef.csv แล้วอ่านจาก
        // LubanDataService แทน switch ที่ hardcode ตรงนี้

        /// <summary>map locationId → biomeId — id ไม่รู้จัก = null (SetBiome ข้ามให้)</summary>
        private string MapLocationToBiome(string locationId)
        {
            switch (locationId)
            {
                case "beach": return "beach";
                case "jungle_edge":
                case "deep_jungle": return "jungle";
                case "cave_entrance": return "cave";
            }
            // ถ้าตั้งชื่อ location ให้ตรงกับ biome id เลย ก็ใช้ตรงนั้นได้
            if (!string.IsNullOrEmpty(locationId) && _data.BiomeDefs.ContainsKey(locationId))
                return locationId;

            if (_warnedUnknownBiomes.Add(locationId))
                Debug.LogWarning($"[BiomeScatterView] ไม่รู้จัก location '{locationId}' — ไม่มี biome ที่ map ได้ (เพิ่ม case ใน MapLocationToBiome)");
            return null;
        }

        private void OnPlayerLocationChanged(PlayerLocationChangedMessage msg)
            => _scatterSystem.SetBiome(MapLocationToBiome(msg.NewLocationId));

        // ---- BiomeChangedMessage → GameObject ----

        private void OnBiomeChanged(BiomeChangedMessage msg)
        {
            ClearAll();
            ApplyGround(msg.BiomeId);

            if (!_prefabSets.TryGetValue(msg.BiomeId, out var prefabSet))
            {
                Debug.LogWarning($"[BiomeScatterView] ไม่มี BiomePrefabSet '{msg.BiomeId}' (สร้าง asset ที่ Resources/BiomePrefabSets/{msg.BiomeId}.asset) — spawn ข้าม");
                return;
            }

            int spawned = 0;
            foreach (var spawn in msg.SpawnsToCreate)
            {
                var prefab = GetPrefab(prefabSet, spawn);
                if (prefab == null) continue;

                var go = Instantiate(prefab, spawn.Position, Quaternion.identity, _objectLayer);
                go.name = spawn.IsHarvestable ? $"node_{spawn.NodeId}_{spawned + 1}" : $"prop_{msg.BiomeId}_{spawned + 1}";
                _activeObjects.Add(go);

                // HarvestableNode → ติด component ให้ระบบเก็บเกี่ยวจับต้องได้
                if (spawn.IsHarvestable)
                    AttachHarvestableNode(go, spawn.NodeId);

                spawned++;
            }

            Debug.Log($"[BiomeScatterView] biome '{msg.BiomeId}': instantiate {spawned}/{msg.SpawnsToCreate.Count} objects");
        }

        /// <summary>map SpawnPosition → prefab: prop_{n} = propPrefabs[n], node id = prefab.name ตรงกัน</summary>
        private static GameObject GetPrefab(BiomePrefabSet prefabSet, SpawnPosition spawn)
        {
            if (!spawn.IsHarvestable)
            {
                if (spawn.PrefabId != null && spawn.PrefabId.StartsWith("prop_")
                    && int.TryParse(spawn.PrefabId.Substring(5), out var index)
                    && prefabSet.propPrefabs != null && index >= 0 && index < prefabSet.propPrefabs.Count)
                    return prefabSet.propPrefabs[index];
                return null;
            }

            var harvestables = prefabSet.harvestablePrefabs;
            if (harvestables == null) return null;
            var i = harvestables.FindIndex(p => p != null && p.name == spawn.PrefabId);
            if (i < 0)
                Debug.LogWarning($"[BiomeScatterView] node '{spawn.PrefabId}' ไม่มี prefab ใน harvestablePrefabs ของ '{prefabSet.biomeId}' (prefab.name ต้องตรงกับ node id)");
            return i < 0 ? null : harvestables[i];
        }

        private void AttachHarvestableNode(GameObject go, string nodeId)
        {
            if (!_data.HarvestableNodeDefs.TryGetValue(nodeId, out var nodeDef))
            {
                Debug.LogWarning($"[BiomeScatterView] node id '{nodeId}' ไม่มีใน HarvestableNodeDefs — ไม่ติด HarvestableNodeComponent");
                return;
            }

            var component = go.AddComponent<HarvestableNodeComponent>();
            component.Init(nodeId, nodeDef, _nodeHarvest);
        }

        // ---- lifecycle helpers ----

        /// <summary>เคลียร์ของเก่าทั้งหมด (Destroy เฉพาะ GameObject — state อยู่กับ System)</summary>
        private void ClearAll()
        {
            foreach (var go in _activeObjects)
            {
                if (go == null) continue; // node ที่หมด durability แล้ว Destroy ไปก่อนหน้า
                Destroy(go);
            }
            _activeObjects.Clear();
        }

        /// <summary>โหลด BiomePrefabSet ทั้งหมดจาก Resources/BiomePrefabSets/</summary>
        private void LoadPrefabSets()
        {
            _prefabSets.Clear();
            foreach (var set in Resources.LoadAll<BiomePrefabSet>("BiomePrefabSets"))
            {
                if (string.IsNullOrEmpty(set.biomeId))
                {
                    Debug.LogWarning($"[BiomeScatterView] BiomePrefabSet '{set.name}' ยังไม่ได้ตั้ง biomeId — ข้าม");
                    continue;
                }
                _prefabSets[set.biomeId] = set;
            }
            Debug.Log($"[BiomeScatterView] โหลด BiomePrefabSet {_prefabSets.Count} ชุด: {string.Join(", ", _prefabSets.Keys)}");
        }

        /// <summary>เปลี่ยนสีพื้นตาม BiomeDef.backgroundColor (hex) — ถ้าไม่ tint ก็ข้าม</summary>
        private void ApplyGround(string biomeId)
        {
            if (_ground != null) Destroy(_ground);
            _ground = null;
            if (!tintGround) return;

            var color = Color.white;
            if (_data.BiomeDefs.TryGetValue(biomeId, out var biomeDef)
                && !string.IsNullOrEmpty(biomeDef.BackgroundColor))
            {
                if (!ColorUtility.TryParseHtmlString(biomeDef.BackgroundColor, out var parsed))
                    Debug.LogWarning($"[BiomeScatterView] backgroundColor '{biomeDef.BackgroundColor}' parse ไม่ได้ — ใช้สีขาว");
                else
                    color = parsed;
            }

            var width = Mathf.Max(1f, _worldBounds.MaxX - _worldBounds.MinX + 4f);
            var height = Mathf.Max(1f, _worldBounds.MaxY - _worldBounds.MinY + 4f);
            var center = new Vector3((_worldBounds.MinX + _worldBounds.MaxX) / 2f, (_worldBounds.MinY + _worldBounds.MaxY) / 2f, 0f);

            _ground = new GameObject($"BiomeGround_{biomeId}");
            _ground.transform.SetParent(_objectLayer, false);
            _ground.transform.position = center;
            _ground.transform.localScale = new Vector3(width, height, 1f);

            var renderer = _ground.AddComponent<SpriteRenderer>();
            renderer.sprite = _groundSprite; // ขาว 1x1 — tint ด้วย color
            color.a = groundAlpha;
            renderer.color = color;
            renderer.sortingOrder = -100; // ใต้ marker (0), ไอเท็ม (1) และ props (-10)
        }

        /// <summary>sprite ขาว 1x1 สำหรับ tint พื้น (สร้าง texture ใน code ไม่พึ่ง asset)</summary>
        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}

