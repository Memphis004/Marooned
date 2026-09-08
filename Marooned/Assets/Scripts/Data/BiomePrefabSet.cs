using System.Collections.Generic;
using UnityEngine;

namespace Marooned.Data
{
    /// <summary>
    /// Lab C Phase 1 (Hybrid BiomeScatter) — ScriptableObject เก็บ Prefab references
    /// ของ biome หนึ่งอย่างเดียว (ลาก Prefab ใส่ใน Inspector) — ไม่เก็บ logic
    /// เพราะข้อมูล "กติกาการ spawn" (propDensity, itemSpawnRate, harvestableNodeIds)
    /// อยู่ใน BiomeDef.csv (Luban) ตามข้อจำกัด: Data Layer = Luban เท่านั้น,
    /// ScriptableObject = ช่องทางเดียวที่อ้าง GameObject/Prefab ของ Unity ได้
    ///
    /// วางไฟล์ asset ที่ Assets/Resources/BiomePrefabSets/&lt;biomeId&gt;.asset
    /// (ชื่อไฟล์ = biomeId เช่น beach/jungle/cave) เพื่อให้ BiomeScatterView โหลด
    /// ผ่าน Resources.Load โดยไม่ต้องลาก reference ใน Inspector
    /// </summary>
    [CreateAssetMenu(fileName = "NewBiomePrefabSet", menuName = "Marooned/BiomePrefabSet")]
    public class BiomePrefabSet : ScriptableObject
    {
        [Header("ต้องตรงกับ BiomeDef.csv (beach/jungle/cave) — และตรงกับชื่อไฟล์ asset")]
        public string biomeId;

        [Header("Decorative Props (เดินผ่านได้ — ไม่มี Collider หรือ IsTrigger)")]
        public List<GameObject> propPrefabs = new();

        [Header("Harvestable Nodes (ต้องใช้ tool — มี Collider)")]
        public List<GameObject> harvestablePrefabs = new();

        [Header("Visual (ไม่บังคับ — พื้นหลัง biome)")]
        public Material groundMaterial;
    }
}
