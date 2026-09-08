using System;
using System.Collections.Generic;
using Marooned.Data;
using MessagePipe;
using UnityEngine;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab C Phase 1 (Hybrid BiomeScatter) — ข้อความ broadcast ใน process (MessagePipe)
    /// เมื่อ biome ของผู้เล่นเปลี่ยน: System คำนวณตำแหน่ง spawn ไว้ให้ครบแล้ว
    /// View (BiomeScatterView) เป็นคน Instantiate/Destroy GameObject เท่านั้น
    /// (System ไม่แตะ GameObject ตามข้อจำกัดของโปรเจกต์)
    /// </summary>
    public class BiomeChangedMessage
    {
        public string BiomeId;
        public List<SpawnPosition> SpawnsToCreate = new();
    }

    /// <summary>ตำแหน่ง spawn 1 จุดที่ System คำนวณให้ View — PrefabId ชี้เข้า BiomePrefabSet</summary>
    public class SpawnPosition
    {
        /// <summary>"prop_{n}" (ชี้ propPrefabs[n]) หรือ node id (ชี้ harvestablePrefabs[n] + HarvestableNodeDefs)</summary>
        public string PrefabId;
        public Vector3 Position;
        public bool IsHarvestable;
        /// <summary>เติมเฉพาะ IsHarvestable — อ้าง id ใน HarvestableNodeDef.csv</summary>
        public string NodeId;
    }

    /// <summary>
    /// Lab C Phase 1 (Hybrid BiomeScatterSystem) — plain C# singleton (VContainer),
    /// เปลี่ยนจากแนว "Ragnarok tile-based" เป็น "Don't Starve/Mad Island modular
    /// scatter": Biome = กติกาการ spawn ของ props + harvestable nodes
    ///
    /// หน้าที่เดียวของ System คือ "คำนวณ" — ไม่มี Instantiate/Destroy ตรงนี้:
    ///   1. SetBiome(biomeId) → อ่าน BiomeDef.csv (propDensity, harvestableNodeIds)
    ///      + BiomePrefabSet (Resources/BiomePrefabSets/ — prefab references เท่านั้น)
    ///   2. สุ่มตำแหน่งภายใน WorldBounds (inject จาก GameLifetimeScope เดิม) แบบ
    ///      rejection sampling กันจุดทับกัน (minDistance)
    ///   3. publish BiomeChangedMessage → BiomeScatterView จัดการ GameObject เอง
    ///
    /// ตัวเลขที่ใช้: จำนวน props ≈ propDensity × PropsPerDensityUnit (default 20)
    /// จำนวน nodes = 1 ต้นแบบต่อ 1 node id ใน harvestableNodeIds (scaled ด้วย
    /// propDensity เช่นกัน ปัดขั้นต่ำ 1 เมื่อมี node ids)
    /// </summary>
    public class BiomeScatterSystem
    {
        /// <summary>จำนวน props = propDensity × ค่านี้ (propDensity 0.3 → 6 จุด)</summary>
        public int PropsPerDensityUnit = 20;

        /// <summary>ระยะห่างขั้นต่ำระหว่างจุด spawn (world unit) — กันของซ้อนกัน</summary>
        public float MinSpawnDistance = 0.8f;

        /// <summary>จำนวนครั้งสูงสุดต่อจุดในการหาตำแหน่งที่ไม่ทับ (rejection sampling)</summary>
        public int MaxPlacementAttempts = 40;

        private readonly LubanDataService _data;
        private readonly WorldBounds _worldBounds;
        private readonly IPublisher<BiomeChangedMessage> _biomePublisher;
        private readonly System.Random _rng = new();

        private string _currentBiomeId;

        public BiomeScatterSystem(LubanDataService data, WorldBounds worldBounds,
            IPublisher<BiomeChangedMessage> biomePublisher)
        {
            _data = data;
            _worldBounds = worldBounds;
            _biomePublisher = biomePublisher;
        }

        /// <summary>id ของ biome ที่ scatter อยู่ตอนนี้ (null ยังไม่เคยเรียก SetBiome)</summary>
        public string CurrentBiomeId => _currentBiomeId;

        /// <summary>
        /// ตั้ง biome ใหม่: คำนวณตำแหน่งทั้งหมดแล้ว publish ให้ View instantiate
        /// (เรียกซ้ำด้วย id เดิม = no-op กัน event pulse)
        /// </summary>
        public void SetBiome(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId) || biomeId == _currentBiomeId) return;

            if (!_data.BiomeDefs.TryGetValue(biomeId, out var biomeDef))
            {
                Debug.LogWarning($"[BiomeScatterSystem] biome '{biomeId}' ไม่มีใน BiomeDefs (BiomeDef.csv) — ข้าม");
                return;
            }

            _currentBiomeId = biomeId;

            var spawns = CalculateSpawnPositions(biomeId, biomeDef);

            _biomePublisher.Publish(new BiomeChangedMessage
            {
                BiomeId = biomeId,
                SpawnsToCreate = spawns,
            });

            Debug.Log($"[BiomeScatterSystem] SetBiome '{biomeId}': publish {spawns.Count} spawn positions " +
                      $"(props + nodes, density {biomeDef.PropDensity})");
        }

        /// <summary>
        /// คำนวณตำแหน่งสุ่มทั้งหมดของ biome — ไม่แตะ GameObject เด็ดขาด
        /// ตรวจความครบของข้อมูลที่ View ต้องใช้ (PrefabSet มีจริง, node id มีใน
        /// HarvestableNodeDefs) ให้เรียบร้อย เพื่อให้ View แค่ "เลือก prefab + instantiate"
        /// </summary>
        private List<SpawnPosition> CalculateSpawnPositions(string biomeId, cfg.game.BiomeDef biomeDef)
        {
            var spawns = new List<SpawnPosition>();
            var prefabSet = TryGetPrefabSet(biomeId);
            if (prefabSet == null)
            {
                Debug.LogWarning($"[BiomeScatterSystem] ไม่เจอ BiomePrefabSet '{biomeId}' ใน Resources/BiomePrefabSets — spawn ไม่ได้");
                return spawns;
            }

            var hasProps = prefabSet.propPrefabs != null && prefabSet.propPrefabs.Count > 0;
            var hasNodes = prefabSet.harvestablePrefabs != null && prefabSet.harvestablePrefabs.Count > 0;

            // ---- decorative props: จำนวนตาม propDensity ----
            if (hasProps)
            {
                int propCount = Mathf.RoundToInt(biomeDef.PropDensity * PropsPerDensityUnit);
                for (int i = 0; i < propCount; i++)
                {
                    if (!TryGetRandomPosition(spawns, out var position)) break;
                    spawns.Add(new SpawnPosition
                    {
                        PrefabId = $"prop_{_rng.Next(prefabSet.propPrefabs.Count)}",
                        Position = position,
                        IsHarvestable = false,
                        NodeId = null,
                    });
                }
            }

            // ---- harvestable nodes: ตาม node ids ใน BiomeDef.harvestableNodeIds ----
            if (hasNodes && biomeDef.HarvestableNodeIds is { Count: > 0 })
            {
                int nodeCount = 0;
                foreach (var nodeId in biomeDef.HarvestableNodeIds)
                {
                    if (_data.HarvestableNodeDefs == null || !_data.HarvestableNodeDefs.ContainsKey(nodeId))
                    {
                        Debug.LogWarning($"[BiomeScatterSystem] node id '{nodeId}' (BiomeDef '{biomeId}') ไม่มีใน HarvestableNodeDefs — ข้าม");
                        continue;
                    }
                    if (!TryGetRandomPosition(spawns, out var position)) break;

                    nodeCount++;
                    spawns.Add(new SpawnPosition
                    {
                        PrefabId = nodeId, // View หา index จาก harvestablePrefabs.FindIndex(p => p.name == id)
                        Position = position,
                        IsHarvestable = true,
                        NodeId = nodeId,
                    });
                }
            }

            return spawns;
        }

        /// <summary>
        /// สุ่มตำแหน่งภายใน WorldBounds (center + half extents เหมือน PlayerMovementSystem)
        /// แบบ rejection sampling — ห้ามอยู่ใกล้จุดที่เลือกไปแล้วน้อยกว่า MinSpawnDistance
        /// </summary>
        private bool TryGetRandomPosition(List<SpawnPosition> existing, out Vector3 position)
        {
            float centerX = (_worldBounds.MinX + _worldBounds.MaxX) / 2f;
            float centerY = (_worldBounds.MinY + _worldBounds.MaxY) / 2f;
            float halfWidth = Mathf.Max(0.5f, (_worldBounds.MaxX - _worldBounds.MinX) / 2f - 0.5f);
            float halfHeight = Mathf.Max(0.5f, (_worldBounds.MaxY - _worldBounds.MinY) / 2f - 0.5f);

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                var candidate = new Vector3(
                    centerX + (float)(_rng.NextDouble() * 2 - 1) * halfWidth,
                    centerY + (float)(_rng.NextDouble() * 2 - 1) * halfHeight,
                    0f);

                bool tooClose = false;
                foreach (var spawn in existing)
                {
                    if ((spawn.Position - candidate).sqrMagnitude < MinSpawnDistance * MinSpawnDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    position = candidate;
                    return true;
                }
            }

            position = default;
            return false; // พื้นที่แน่นเกินไป — ตัดจำนวน spawn ที่เหลือทิ้ง
        }

        /// <summary>โหลด BiomePrefabSet ของ biome (asset ชื่อ = biomeId) ผ่าน Resources</summary>
        private static BiomePrefabSet TryGetPrefabSet(string biomeId)
        {
            return Resources.Load<BiomePrefabSet>($"BiomePrefabSets/{biomeId}");
        }
    }
}
