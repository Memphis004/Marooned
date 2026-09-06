using System.IO;
using Marooned.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Marooned.EditorTools
{
    /// <summary>
    /// [ชั่วคราว เฉพาะเทส Lab B Phase 3] State machine 3 เฟส (SessionState "LabB3Run2Phase"):
    ///   0) setup: variants WizardChibi/CollegeStudentChibi + npcPrefabs + PlayerCharacter
    ///      → ตั้ง backend=Spine (ยืนยัน Phase 2 ที่ค้าง) → play mode 0
    ///   1) restore backend=GenericCute → play mode 1 (Phase 3 tests A–D)
    ///   2) เสร็จ
    /// log → LabB3_phase_log.txt
    /// </summary>
    [InitializeOnLoad]
    public static class LabB3Bootstrap
    {
        private const string Student1Path =
            "Assets/Generic Cute 2D - 001 Student 1/Student/001 Student 1/Prefabs/001 Student 1 Character.prefab";
        private const string WizardSrcPath = "Assets/Wizard - 2D Character/Wizard.prefab";
        private const string CollegeSrcPath = "Assets/CollegeStudent/CollegeStudent.prefab";
        private const string ElenaVariantPath = "Assets/Scripts/Core/Visual/Prefabs/ElenaChibi.prefab";
        private const string DerekVariantPath = "Assets/Scripts/Core/Visual/Prefabs/DerekChibi.prefab";
        private const string VariantsFolder = "Assets/Scripts/Core/Visual/Prefabs";

        static LabB3Bootstrap()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (EditorApplication.isPlaying) return;
            var phase = SessionState.GetInt("LabB3Run2Phase", 0);
            if (phase >= 3) return;

            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                var spawner = GameObject.Find("GameLifetimeScope/ChibiSystem")?.GetComponent<ChibiSpawnerView>();
                if (spawner == null) { Log("ERROR: หา ChibiSystem ไม่เจอ"); SessionState.SetInt("LabB3Run2Phase", 3); return; }

                if (phase == 0)
                {
                    // --- variants ของ NPC ตระกูล GenericCute (per-character state names) ---
                    if (!AssetDatabase.IsValidFolder(VariantsFolder))
                        AssetDatabase.CreateFolder("Assets/Scripts/Core/Visual", "Prefabs");
                    var wizard = CreateGenericVariant(WizardSrcPath, VariantsFolder + "/WizardChibi.prefab");
                    var college = CreateGenericVariant(CollegeSrcPath, VariantsFolder + "/CollegeStudentChibi.prefab");

                    // --- spawner: npcPrefabs rotation + คง spinePrefabs ของ Phase 2 ---
                    var so = new SerializedObject(spawner);
                    so.FindProperty("backend").enumValueIndex = (int)ChibiBackend.Spine; // เฟสแรกยืนยัน Spine ก่อน
                    so.FindProperty("genericCutePrefab").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(Student1Path);
                    var npcs = so.FindProperty("npcPrefabs");
                    npcs.arraySize = 2;
                    npcs.GetArrayElementAtIndex(0).objectReferenceValue = wizard;
                    npcs.GetArrayElementAtIndex(1).objectReferenceValue = college;
                    var spines = so.FindProperty("spinePrefabs");
                    if (spines.arraySize < 2)
                    {
                        spines.arraySize = 2;
                        spines.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ElenaVariantPath);
                        spines.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DerekVariantPath);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // --- PlayerCharacter ใต้ GameManager ---
                    var playerGo = GameObject.Find("GameLifetimeScope/GameManager/PlayerCharacter");
                    if (playerGo == null)
                    {
                        var manager = GameObject.Find("GameLifetimeScope/GameManager");
                        playerGo = new GameObject("PlayerCharacter");
                        playerGo.transform.SetParent(manager.transform, false);
                        var view = playerGo.AddComponent<PlayerCharacterView>();
                        var vso = new SerializedObject(view);
                        vso.FindProperty("visualPrefab").objectReferenceValue =
                            AssetDatabase.LoadAssetAtPath<GameObject>(Student1Path);
                        vso.ApplyModifiedPropertiesWithoutUndo();
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Log("Phase 0: variants + npcPrefabs + PlayerCharacter saved, backend=Spine (Phase 2 evidence)");

                    AttachTestAndPlay(0);
                }
                else if (phase == 1)
                {
                    // --- restore default backend (spec: ห้ามเปลี่ยนพฤติกรรมเดิม) ---
                    var so = new SerializedObject(spawner);
                    so.FindProperty("backend").enumValueIndex = (int)ChibiBackend.GenericCute;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Log("Phase 1: backend=GenericCute restored → Phase 3 tests A–D");

                    AttachTestAndPlay(1);
                }
            }
            catch (System.Exception ex)
            {
                Log("BOOTSTRAP EXCEPTION: " + ex);
                SessionState.SetInt("LabB3Run2Phase", 3);
            }
        }

        private static GameObject CreateGenericVariant(string sourcePath, string variantPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (existing != null) return existing;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
            var visual = instance.AddComponent<GenericCuteVisualController>();
            // state names จริงของ Wizard Demo / AnimationDemo controller (inspect จาก .controller):
            // มี Idle/Run/Attack ฯลฯ — ไม่มี walk/interact/pick up แบบ Student 1
            var vso = new SerializedObject(visual);
            vso.FindProperty("idleAnim").stringValue = "Idle";
            vso.FindProperty("walkAnim").stringValue = "Run";
            vso.FindProperty("interactAnim").stringValue = "";   // ไม่มี state interact
            vso.FindProperty("pickupAnim").stringValue = "";     // NPC ไม่ต้องเก็บของ
            vso.ApplyModifiedPropertiesWithoutUndo();

            var variant = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, variantPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
            Log($"สร้าง variant: {variantPath}");
            return variant;
        }

        private static void AttachTestAndPlay(int mode)
        {
            var sys = GameObject.Find("GameLifetimeScope/ChibiSystem");
            var test = sys.GetComponent<LabB3PlayModeSelfTest>();
            if (test == null) test = sys.AddComponent<LabB3PlayModeSelfTest>();
            var tso = new SerializedObject(test);
            tso.FindProperty("mode").intValue = mode;
            tso.ApplyModifiedPropertiesWithoutUndo();

            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = true;
                Log($"เริ่ม Play Mode (mode={mode})");
            };
        }

        private static void Log(string message)
        {
            File.AppendAllText(Path.Combine(Application.dataPath, "..", "LabB3_phase_log.txt"),
                $"{System.DateTime.Now:HH:mm:ss} — {message}\n");
        }
    }
}
