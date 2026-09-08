using System.Collections.Generic;
using Marooned.Core;
using Marooned.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Lab C Phase 1 — one-click scene setup สำหรับ Biome Scatter + Tool-Gathering
    /// (Editor menu: Marooned > Lab C > Setup Biome Scatter Test Scene)
    ///
    /// ทำ 3 อย่างให้ครบก่อนกด Play:
    ///   1. สร้าง placeholder prefabs + 3 BiomePrefabSet assets (beach/jungle/cave)
    ///      ที่ Assets/Resources/BiomePrefabSets/ — สร้างแล้ว = ข้าม (ไม่ทับของจริง)
    ///   2. เพิ่ม GameObject "BiomeScatterLayer" (ติด BiomeScatterView) ใต้ลูกของ
    ///      GameLifetimeScope ใน scene ปัจจุบัน — ถ้ามีอยู่แล้วแต่ยังไม่มี component
    ///      จะ AddComponent ให้ (ไม่ duplicate GameObject)
    ///   3. เลือก scene ให้แล้วบันทึกเอง (Ctrl+S ไม่ต้องกด)
    ///
    /// รันครั้งเดียวก่อน Play — รันซ้ำได้ปลอดภัย (idempotent)
    /// </summary>
    public static class BiomeScatterTestSetup
    {
        private const string PrefabSetsFolder = "Assets/Resources/BiomePrefabSets";
        private const string PrefabsFolder = "Assets/Resources/BiomePrefabSets/Prefabs";

        // ---- placeholder spec: (ชื่อ, สี hex, ขนาด, node id หรือ null) ----
        // node id ต้องตรง HarvestableNodeDef.csv เป๊ะ (View หา prefab ด้วยชื่อ)
        private static readonly (string name, string color, float size, string nodeId)[] Specs =
        {
            ("prop_palm",     "#E8D8A0", 0.6f, null),        // ต้นปาล์มตกแต่ง
            ("prop_shell",    "#F2EFE6", 0.3f, null),        // เปลือกหอย
            ("prop_grass",    "#7FB069", 0.45f, null),       // หญ้า
            ("prop_fern",     "#4A7C59", 0.5f, null),        // เฟิร์น
            ("prop_mushroom", "#B5651D", 0.25f, null),       // เห็ด
            ("prop_stalagmite","#6E6E6E", 0.4f, null),      // หินย้อย
            ("tree_wood",     "#2D5016", 0.7f, "tree_wood"), // ต้นไม้ตัดได้ (tool_axe)
            ("rock_stone",    "#555555", 0.5f, "rock_stone"),// ก้อนหินขุดได้ (tool_pickaxe)
            ("bush_berry",    "#8B0000", 0.45f, "bush_berry"),// พุ่มเบอร์รี่ (มือเปล่า)
        };

        [MenuItem("Marooned/Lab C/Setup Biome Scatter Test Scene", priority = 0)]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[BiomeScatterTestSetup] ต้องรันตอน Edit mode (ออกจาก Play ก่อน) — กัน save scene ตอน play");
                return;
            }

            EnsureFolder(PrefabSetsFolder);
            EnsureFolder(PrefabsFolder);

            // ---- 1) placeholder prefabs + BiomePrefabSet assets ----
            var setIds = new[] { "beach", "jungle", "cave" };
            foreach (var setId in setIds)
            {
                var setPath = $"{PrefabSetsFolder}/{setId}.asset";
                var existingSet = AssetDatabase.LoadAssetAtPath<BiomePrefabSet>(setPath);
                if (existingSet != null)
                {
                    Debug.Log($"[BiomeScatterTestSetup] BiomePrefabSet '{setId}' มีอยู่แล้ว — ข้าม (ไม่ทับของจริง)");
                    continue;
                }

                var set = ScriptableObject.CreateInstance<BiomePrefabSet>();
                set.biomeId = setId;
                set.propPrefabs = new List<GameObject>();
                set.harvestablePrefabs = new List<GameObject>();

                foreach (var (name, color, size, nodeId) in Specs)
                {
                    var prefabPath = $"{PrefabsFolder}/{name}.prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        // SaveAsPrefabAsset สร้าง asset ที่ path ให้แล้ว (ห้าม CreateAsset ซ้ำ)
                        prefab = CreatePlaceholderPrefab(prefabPath, name, color, size, nodeId != null);
                    }

                    if (nodeId == null) set.propPrefabs.Add(prefab);
                    else set.harvestablePrefabs.Add(prefab);
                }

                AssetDatabase.CreateAsset(set, setPath);
                Debug.Log($"[BiomeScatterTestSetup] สร้าง {setPath} (props={set.propPrefabs.Count}, harvestables={set.harvestablePrefabs.Count})");
            }

            // ---- 2) BiomeScatterLayer + BiomeScatterView ใน scene ----
            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[BiomeScatterTestSetup] หา GameLifetimeScope ไม่เจอใน scene — เปิด scene เกมหลักก่อนแล้วรันเมนูนี้อีกครั้ง");
                return;
            }

            var host = GameObject.Find("BiomeScatterLayer");
            if (host == null)
            {
                host = new GameObject("BiomeScatterLayer");
                Undo.RegisterCreatedObjectUndo(host, "Create BiomeScatterLayer");
            }

            // ต้องเป็นลูก (หลาน) ของ scope เพื่อให้ GetComponentInParent<GameLifetimeScope> เจอ
            if (!host.transform.IsChildOf(scope.transform)) host.transform.SetParent(scope.transform, false);

            var view = host.GetComponent<BiomeScatterView>();
            if (view == null)
            {
                view = Undo.AddComponent<BiomeScatterView>(host);
                Debug.Log("[BiomeScatterTestSetup] เพิ่ม BiomeScatterView บน BiomeScatterLayer แล้ว");
            }
            else
            {
                Debug.Log("[BiomeScatterView] มีอยู่แล้ว — ข้าม");
            }

            // ---- 3) mark dirty + save scene ----
            var scene = host.scene;
            if (!scene.IsValid()) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BiomeScatterTestSetup] ✅ Setup เสร็จ — scene saved กด Play ได้เลย (รันซ้ำ idempotent)");
        }

        /// <summary>placeholder = SpriteRenderer สี่เหลี่ยมสี + ordering ตามชนิด (props -10 / nodes -9)</summary>
        private static GameObject CreatePlaceholderPrefab(string path, string name, string colorHex, float size, bool isHarvestable)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            if (ColorUtility.TryParseHtmlString(colorHex, out var c)) tex.SetPixel(0, 0, c);
            else tex.SetPixel(0, 0, Color.magenta);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = $"{name}_sprite";
            sr.sprite = sprite;
            sr.sortingOrder = isHarvestable ? -9 : -10;

            go.transform.localScale = new Vector3(size, size, 1f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0]; // "Assets"
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
