#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setup utility to create Habitat Landmark ScriptableObject data assets,
/// composite 3D landmark prefabs for Sunlight Zone and Twilight Zone,
/// register them into HabitatLandmarkRegistry in Resources,
/// and place them at meaningful coordinates on the ocean floor in both scenes.
///
/// Menu -> HadalDescent -> Setup Habitat Landmarks (Sunlight & Twilight)
/// </summary>
[InitializeOnLoad]
public static class HabitatLandmarkSetupUtility
{
    private const string PrefabFolder = "Assets/Prefabs/Environment/Landmarks";
    private const string DataFolder   = "Assets/Scripts/ScriptableObjects/Landmarks";
    private const string RegistryPath = "Assets/Resources/HabitatLandmarkRegistry.asset";
    private const string PrefKey      = "HD_HabitatLandmarks_Created_v1";

    static HabitatLandmarkSetupUtility()
    {
        EditorApplication.delayCall += OnEditorLoaded;
    }

    private static void OnEditorLoaded()
    {
        if (!EditorPrefs.GetBool(PrefKey, false))
        {
            SetupHabitatLandmarks(false);
            EditorPrefs.SetBool(PrefKey, true);
        }
    }

    [MenuItem("HadalDescent/Setup Habitat Landmarks (Sunlight & Twilight)")]
    public static void SetupHabitatLandmarksMenu()
    {
        SetupHabitatLandmarks(true);
    }

    public static void SetupHabitatLandmarks(bool interactive = false)
    {
        Debug.Log("[HabitatLandmarkSetup] Setting up Habitat Landmarks for Sunlight & Twilight Zones...");

        EnsureFolders();

        // 1. Create ScriptableObject Data Assets
        var landmarks = CreateDataAssets();

        // 2. Register into Resources/HabitatLandmarkRegistry
        UpdateRegistry(landmarks);

        // 3. Create Landmark Prefabs
        var prefabs = CreatePrefabs(landmarks);

        // 4. Place into scenes
        PlaceLandmarksInScenes(prefabs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[HabitatLandmarkSetup] Completed setup of Habitat Landmarks!");
        if (interactive)
        {
            EditorUtility.DisplayDialog(
                "Habitat Landmarks Setup Complete",
                "Created and configured:\n\n" +
                "1. Data Assets in " + DataFolder + ":\n" +
                "   • Barrier Reef Crest (Sunlight, 10–45 m)\n" +
                "   • Giant Kelp Forest Spire (Sunlight, 20–60 m)\n" +
                "   • Whale Fall Oasis (Twilight, 400–850 m)\n" +
                "   • Cold-Water Lophelia Mound (Twilight, 300–700 m)\n\n" +
                "2. Resources/HabitatLandmarkRegistry.asset (Auto-loaded by Bestiary)\n\n" +
                "3. Composite 3D Prefabs in " + PrefabFolder + "\n\n" +
                "4. Placed Landmark POIs in SunlightZone.unity & TwilightZone.unity\n" +
                "   (Yellow blips on Sonar, hand interaction triggers FactCardUI)",
                "OK"
            );
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Environment/Landmarks"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Environment"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Environment");
            AssetDatabase.CreateFolder("Assets/Prefabs/Environment", "Landmarks");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scripts/ScriptableObjects/Landmarks"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/Scripts", "ScriptableObjects");
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects", "Landmarks");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static List<HabitatLandmarkData> CreateDataAssets()
    {
        var list = new List<HabitatLandmarkData>();

        Sprite coralPhoto = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/Real-life Images/Sunlight Zone/fan_coral.jpg");
        if (coralPhoto == null)
            coralPhoto = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/Real-life Images/Sunlight Zone/anthozoa_bubble_tip_sea_anemone.jpg");

        // ── 0. Sunlight Zone: Coral Reef Matrix ──
        list.Add(CreateOrUpdateData(
            "Landmark_Sun_BarrierReef",
            "fact_sun_coral_reef",
            "Coral Reef Matrix",
            "Biogenic Aragonite Ecosystem",
            0,
            "10–50 m",
            "An expansive three-dimensional calcium carbonate fortress constructed over thousands of years by hermatypic stony corals (Scleractinia) and crustose coralline algae in sunlit waters.",
            "Known as the \"rainforests of the sea,\" coral reefs support over 25% of all marine species while covering under 0.1% of the seafloor. They serve as vital natural breakwaters, attenuating up to 97% of coastal wave energy.",
            "Corals are animal colonies living in mutualism with photosynthetic microscopic algae (zooxanthellae). The algae produce up to 90% of the polyp's energy and provide corals their vibrant living coloration.",
            75,
            coralPhoto,
            BiomeBand.Hard,
            "NOAA Coral Reef Conservation Program / Smithsonian Ocean Portal"
        ));

        // ── 0. Sunlight Zone: Seagrass Bed ──
        list.Add(CreateOrUpdateData(
            "Landmark_Sun_KelpCanopy",
            "fact_sun_seagrass_bed",
            "Seagrass Bed",
            "Marine Angiosperm Meadow",
            0,
            "5–40 m",
            "Vast submerged meadows formed by marine angiosperms (true flowering plants) anchored into soft sands and silts in clear, sunlit coastal waters.",
            "Premier \"blue carbon\" sinks that bury organic carbon up to 35 times faster than tropical rainforests. Their dense rhizome roots stabilize loose seabed sediments and nurture juvenile fish, sea turtles, and dugongs.",
            "Unlike algae or seaweeds, seagrasses have true roots, veins, underwater flowers, and fruit. They pollinate completely submerged using specialized waterborne pollen grains carried by gentle ocean currents!",
            75,
            coralPhoto,
            BiomeBand.Soft,
            "United Nations Environment Programme (UNEP) / Smithsonian Marine Station"
        ));

        // ── 1. Twilight Zone: Whale Fall Oasis ──
        list.Add(CreateOrUpdateData(
            "Landmark_Twi_WhaleFall",
            "fact_twi_whale_fall",
            "Whale Fall Oasis",
            "Chemosynthetic Succession POI",
            1,
            "400–850 m",
            "The massive carcass of a great cetacean settled upon the dark bathyal seafloor, creating a localized biological bonanza.",
            "Fuels four distinct ecological succession stages spanning up to a century: mobile scavengers, enrichment opportunists, sulfophilic chemosynthesis, and reef bones.",
            "A single 40-ton whale carcass deposits the equivalent organic carbon of 2,000 years of ordinary marine snow to that seabed patch.",
            100,
            null,
            BiomeBand.Soft,
            "NOAA Ocean Exploration / Monterey Bay Aquarium Research Institute (MBARI)"
        ));

        // ── 1. Twilight Zone: Cold-Water Lophelia Mound ──
        list.Add(CreateOrUpdateData(
            "Landmark_Twi_ColdCoralMound",
            "fact_twi_cold_coral",
            "Cold-Water Lophelia Mound",
            "Deep-Sea Carbonate Bioherm",
            1,
            "300–700 m",
            "An ancient carbonate bioherm built entirely in perpetual darkness by Lophelia pertusa and Madrepora corals in chilly 4°C deep currents.",
            "Serves as deep-ocean biodiversity hotspots and breeding refuges for deep-water rockfish, squat lobsters, and fragile glass sponge gardens.",
            "Unlike shallow tropical corals, cold-water corals lack symbiotic photosynthetic algae (zooxanthellae) and filter 100% of sustenance from ocean currents.",
            100,
            null,
            BiomeBand.Hard,
            "NOAA Ocean Exploration / ATLAS Deep-Sea Project"
        ));

        return list;
    }

    private static HabitatLandmarkData CreateOrUpdateData(
        string fileName,
        string factId,
        string habitatName,
        string category,
        int zoneIndex,
        string depthRangeText,
        string description,
        string significance,
        string interestingFact,
        int rdpReward,
        Sprite photo,
        BiomeBand targetBiome = BiomeBand.Hard,
        string citationSource = "NOAA / Smithsonian Ocean")
    {
        string path = $"{DataFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<HabitatLandmarkData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<HabitatLandmarkData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.factId                 = factId;
        asset.habitatName            = habitatName;
        asset.category               = category;
        asset.zoneIndex              = zoneIndex;
        asset.depthRangeText         = depthRangeText;
        asset.habitatDescription     = description;
        asset.ecologicalSignificance = significance;
        asset.interestingFact        = interestingFact;
        asset.rdpReward              = rdpReward;
        asset.targetBiome            = targetBiome;
        asset.citationSource         = citationSource;
        if (photo != null) asset.habitatPhoto = photo;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void UpdateRegistry(List<HabitatLandmarkData> landmarks)
    {
        var registry = AssetDatabase.LoadAssetAtPath<HabitatLandmarkRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<HabitatLandmarkRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        registry.allLandmarks.Clear();
        registry.allLandmarks.AddRange(landmarks);
        EditorUtility.SetDirty(registry);
    }

    private static Dictionary<string, GameObject> CreatePrefabs(List<HabitatLandmarkData> dataList)
    {
        var prefabs = new Dictionary<string, GameObject>();

        // Find reference models
        GameObject conePinnacle1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cone_001.prefab");
        GameObject conePinnacle2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cone_002.prefab");
        GameObject cubePlateau   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cube_001.prefab");
        GameObject cubePillar    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cube_004.prefab");
        GameObject icoBoulder3   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Icosphere_003.prefab");
        GameObject bisectSlab    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/bisect.prefab");

        GameObject moundCoral    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Sunlight Zone/coral by Kelli Ray - 7Cs3rTEcpcD.prefab");
        GameObject branchCoral   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Sunlight Zone/Orange Coral by Device Lab - 3HEc6LvqCJd.prefab");
        GameObject coralFan      = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Sunlight Zone/prop_sun_coral_fan.prefab");
        GameObject seagrass      = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Sunlight Zone/prop_sun_seagrass_patch.prefab");

        // 1. Landmark_CoralReef
        HabitatLandmarkData reefData = dataList.Find(d => d.factId == "fact_sun_coral_reef" || d.factId == "fact_sun_barrier_reef");
        prefabs["BarrierReef"] = BuildCompositePrefab(
            "Landmark_CoralReef",
            reefData,
            new Vector3(14f, 8f, 14f),
            root =>
            {
                if (cubePlateau != null)
                {
                    var baseRock = Object.Instantiate(cubePlateau, root.transform);
                    baseRock.name = "RockBase";
                    baseRock.transform.localPosition = Vector3.zero;
                    baseRock.transform.localScale = new Vector3(8f, 4f, 8f);
                }
                if (moundCoral != null)
                {
                    var c1 = Object.Instantiate(moundCoral, root.transform);
                    c1.name = "BrainCoral_Cluster";
                    c1.transform.localPosition = new Vector3(2.5f, 2.2f, 1.8f);
                    c1.transform.localScale = new Vector3(3f, 3f, 3f);
                }
                if (branchCoral != null)
                {
                    var c2 = Object.Instantiate(branchCoral, root.transform);
                    c2.name = "OrangeBranchingCoral";
                    c2.transform.localPosition = new Vector3(-2.8f, 2.0f, -1.5f);
                    c2.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
                }
                if (coralFan != null)
                {
                    var c3 = Object.Instantiate(coralFan, root.transform);
                    c3.name = "MajesticGorgonianFan";
                    c3.transform.localPosition = new Vector3(0.5f, 2.8f, 0f);
                    c3.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    c3.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
                }
            }
        );

        // 2. Landmark_SeaGrassBed
        HabitatLandmarkData kelpData = dataList.Find(d => d.factId == "fact_sun_seagrass_bed" || d.factId == "fact_sun_kelp_canopy");
        prefabs["SeaGrassBed"] = BuildCompositePrefab(
            "Landmark_SeaGrassBed",
            kelpData,
            new Vector3(10f, 20f, 10f),
            root =>
            {
                if (conePinnacle2 != null)
                {
                    var spire = Object.Instantiate(conePinnacle2, root.transform);
                    spire.name = "PinnacleSpire";
                    spire.transform.localPosition = Vector3.zero;
                    spire.transform.localScale = new Vector3(5f, 14f, 5f);
                }
                if (seagrass != null)
                {
                    var k1 = Object.Instantiate(seagrass, root.transform);
                    k1.name = "KelpCanopy_High";
                    k1.transform.localPosition = new Vector3(0f, 12f, 0f);
                    k1.transform.localScale = new Vector3(3.5f, 4.0f, 3.5f);

                    var k2 = Object.Instantiate(seagrass, root.transform);
                    k2.name = "KelpCanopy_Mid";
                    k2.transform.localPosition = new Vector3(1.2f, 6.5f, 0.8f);
                    k2.transform.localScale = new Vector3(2.8f, 3.0f, 2.8f);
                }
            }
        );

        // 3. Landmark_WhaleFall
        HabitatLandmarkData whaleData = dataList.Find(d => d.factId == "fact_twi_whale_fall");
        prefabs["WhaleFall"] = BuildCompositePrefab(
            "Landmark_WhaleFall",
            whaleData,
            new Vector3(16f, 6f, 16f),
            root =>
            {
                if (bisectSlab != null)
                {
                    var spine = Object.Instantiate(bisectSlab, root.transform);
                    spine.name = "SkeletalSpine";
                    spine.transform.localPosition = Vector3.zero;
                    spine.transform.localScale = new Vector3(12f, 2.5f, 4.5f);
                }
                if (conePinnacle1 != null)
                {
                    var ribL = Object.Instantiate(conePinnacle1, root.transform);
                    ribL.name = "SkeletalRib_Left";
                    ribL.transform.localPosition = new Vector3(2.5f, 1.2f, 3.0f);
                    ribL.transform.localRotation = Quaternion.Euler(30f, 0f, 15f);
                    ribL.transform.localScale = new Vector3(1.2f, 4.5f, 1.2f);

                    var ribR = Object.Instantiate(conePinnacle1, root.transform);
                    ribR.name = "SkeletalRib_Right";
                    ribR.transform.localPosition = new Vector3(-1.5f, 1.2f, -3.0f);
                    ribR.transform.localRotation = Quaternion.Euler(-30f, 0f, -15f);
                    ribR.transform.localScale = new Vector3(1.2f, 4.5f, 1.2f);
                }
                if (icoBoulder3 != null)
                {
                    var skull = Object.Instantiate(icoBoulder3, root.transform);
                    skull.name = "CranialMound";
                    skull.transform.localPosition = new Vector3(6.5f, 1.0f, 0f);
                    skull.transform.localScale = new Vector3(4.5f, 3.0f, 4.0f);
                }
            }
        );

        // 4. Landmark_ColdWaterCoralMound
        HabitatLandmarkData coldData = dataList.Find(d => d.factId == "fact_twi_cold_coral");
        prefabs["ColdCoralMound"] = BuildCompositePrefab(
            "Landmark_ColdWaterCoralMound",
            coldData,
            new Vector3(14f, 10f, 14f),
            root =>
            {
                if (cubePillar != null)
                {
                    var mound = Object.Instantiate(cubePillar, root.transform);
                    mound.name = "CarbonateBioherm";
                    mound.transform.localPosition = Vector3.zero;
                    mound.transform.localScale = new Vector3(8f, 5.5f, 8f);
                }
                if (conePinnacle1 != null)
                {
                    var p1 = Object.Instantiate(conePinnacle1, root.transform);
                    p1.name = "LopheliaSpire_1";
                    p1.transform.localPosition = new Vector3(-2f, 3.5f, 2f);
                    p1.transform.localScale = new Vector3(2.5f, 5f, 2.5f);

                    var p2 = Object.Instantiate(conePinnacle1, root.transform);
                    p2.name = "LopheliaSpire_2";
                    p2.transform.localPosition = new Vector3(2.5f, 3.0f, -1.8f);
                    p2.transform.localScale = new Vector3(2.0f, 4f, 2.0f);
                }
            }
        );

        return prefabs;
    }

    private static GameObject BuildCompositePrefab(
        string prefabName,
        HabitatLandmarkData data,
        Vector3 colliderSize,
        System.Action<GameObject> assembleChildren)
    {
        string path = $"{PrefabFolder}/{prefabName}.prefab";

        var root = new GameObject(prefabName);
        var boxCol = root.AddComponent<BoxCollider>();
        boxCol.size = colliderSize;
        boxCol.center = new Vector3(0f, colliderSize.y * 0.45f, 0f);
        boxCol.isTrigger = true;

        var trackable = root.AddComponent<SonarTrackable>();
        trackable.Initialize(SonarTrackable.SonarTargetType.Environment, data != null ? data.habitatName : prefabName, false);

        var factTarget = root.AddComponent<EnvironmentFactTarget>();
        var so = new SerializedObject(factTarget);
        so.FindProperty("dataAsset").objectReferenceValue = data;
        if (data != null)
        {
            so.FindProperty("factId").stringValue                    = data.factId;
            so.FindProperty("habitatName").stringValue               = data.habitatName;
            so.FindProperty("category").stringValue                  = data.category;
            so.FindProperty("zoneIndex").intValue                    = data.zoneIndex;
            so.FindProperty("depthRangeText").stringValue            = data.depthRangeText;
            so.FindProperty("habitatDescription").stringValue        = data.habitatDescription;
            so.FindProperty("ecologicalSignificance").stringValue    = data.ecologicalSignificance;
            so.FindProperty("interestingFact").stringValue           = data.interestingFact;
            so.FindProperty("rdpReward").intValue                    = data.rdpReward;
            so.FindProperty("habitatPhoto").objectReferenceValue     = data.habitatPhoto;
        }
        so.ApplyModifiedProperties();

        assembleChildren?.Invoke(root);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[HabitatLandmarkSetup] Saved landmark prefab: {path}");
        return savedPrefab;
    }

    private static void PlaceLandmarksInScenes(Dictionary<string, GameObject> prefabs)
    {
        string currentScenePath = SceneManager.GetActiveScene().path;

        // ── Sunlight Zone ──
        string sunlightPath = "Assets/Scenes/SunlightZone.unity";
        if (File.Exists(sunlightPath))
        {
            Scene sunScene = EditorSceneManager.OpenScene(sunlightPath, OpenSceneMode.Single);
            Transform envRoot = GameObject.Find("Environment")?.transform;
            if (envRoot != null)
            {
                Transform lmRoot = envRoot.Find("Landmarks");
                if (lmRoot == null)
                {
                    var go = new GameObject("Landmarks");
                    go.transform.SetParent(envRoot, false);
                    lmRoot = go.transform;
                }

                if (prefabs.TryGetValue("BarrierReef", out GameObject reefPrefab) && reefPrefab != null)
                {
                    if (lmRoot.Find(reefPrefab.name) == null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(reefPrefab, lmRoot);
                        inst.transform.position = new Vector3(115f, -148f, 125f);
                        inst.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                    }
                }

                if (prefabs.TryGetValue("KelpSpire", out GameObject kelpPrefab) && kelpPrefab != null)
                {
                    if (lmRoot.Find(kelpPrefab.name) == null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(kelpPrefab, lmRoot);
                        inst.transform.position = new Vector3(-135f, -148f, -110f);
                        inst.transform.rotation = Quaternion.Euler(0f, 110f, 0f);
                    }
                }

                EditorSceneManager.MarkSceneDirty(sunScene);
                EditorSceneManager.SaveScene(sunScene);
                Debug.Log("[HabitatLandmarkSetup] Placed landmarks into SunlightZone.unity");
            }
        }

        // ── Twilight Zone ──
        string twilightPath = "Assets/Scenes/TwilightZone.unity";
        if (File.Exists(twilightPath))
        {
            Scene twiScene = EditorSceneManager.OpenScene(twilightPath, OpenSceneMode.Single);
            Transform envRoot = GameObject.Find("Environment")?.transform;
            if (envRoot != null)
            {
                Transform lmRoot = envRoot.Find("Landmarks");
                if (lmRoot == null)
                {
                    var go = new GameObject("Landmarks");
                    go.transform.SetParent(envRoot, false);
                    lmRoot = go.transform;
                }

                if (prefabs.TryGetValue("WhaleFall", out GameObject whalePrefab) && whalePrefab != null)
                {
                    if (lmRoot.Find(whalePrefab.name) == null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(whalePrefab, lmRoot);
                        inst.transform.position = new Vector3(140f, -248f, 115f);
                        inst.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
                    }
                }

                if (prefabs.TryGetValue("ColdCoralMound", out GameObject coldPrefab) && coldPrefab != null)
                {
                    if (lmRoot.Find(coldPrefab.name) == null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(coldPrefab, lmRoot);
                        inst.transform.position = new Vector3(-145f, -246f, -125f);
                        inst.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
                    }
                }

                EditorSceneManager.MarkSceneDirty(twiScene);
                EditorSceneManager.SaveScene(twiScene);
                Debug.Log("[HabitatLandmarkSetup] Placed landmarks into TwilightZone.unity");
            }
        }

        // Return to originally opened scene if any
        if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
        {
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }
    }
}
#endif
