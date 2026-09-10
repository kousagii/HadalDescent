#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to automatically construct the complete scene hierarchy for
/// Level 2 (Twilight Zone) matching the exact architectural setup of SunlightZone.unity.
///
/// Run via: Menu -> HadalDescent -> Setup Twilight Zone Scene
/// </summary>
public static class TwilightZoneSetupUtility
{
    private const int ZoneIdx = 1; // Twilight Zone

    [MenuItem("HadalDescent/Setup Twilight Zone Scene")]
    public static void SetupTwilightZoneSceneMenu()
    {
        SetupTwilightZoneScene(true);
    }

    public static void SetupTwilightZoneScene(bool interactive = false)
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name != "TwilightZone")
        {
            if (interactive)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Switch Scene?",
                    $"Current scene is '{currentScene.name}'. Open 'TwilightZone.unity' and set it up?",
                    "Yes, Open & Setup", "Cancel");

                if (!proceed) return;
            }

            string scenePath = "Assets/Scenes/TwilightZone.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                if (interactive) EditorUtility.DisplayDialog("Error", $"Could not find scene at '{scenePath}'", "OK");
                Debug.LogError($"[TwilightZoneSetup] Could not find scene at '{scenePath}'");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        ZoneDefinition zone = ZoneConfig.Zones[ZoneIdx];
        Debug.Log($"[TwilightZoneSetup] Setting up '{zone.zoneName}' hierarchy ({zone.playableWidth}x{zone.playableLength}x{zone.playableDepth}m)...");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Twilight Zone Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Ensure EnvPropSet for Twilight Zone exists
        EnvPropSet twilightProps = EnsureTwilightProps();

        // 2. Setup Directional Light
        SetupDirectionalLight(zone);

        // 3. Setup Global Volume
        SetupGlobalVolume();

        // 4. Setup EventSystem
        SetupEventSystem();

        // 5. Setup Player & Camera hierarchy (removes any orphaned cameras)
        GameObject playerGO = SetupPlayerAndCamera(zone);

        // 6. Setup Environment container & OceanFloor
        GameObject oceanFloorGO = SetupEnvironment(zone, twilightProps);

        // 7. Setup Boundary container, triggers, spawns & side walls
        var boundaries = SetupBoundaries(zone);

        // 8. Setup Managers container (_PCGManager, ZoneSceneSetup, HazardZoneTrigger, GameManager, MinigameManager, AudioManager)
        SetupManagers(zone, twilightProps, oceanFloorGO, boundaries);

        // 9. Apply Scene Atmosphere
        ZoneManager.ApplyAtmosphere(zone);
        ApplyRenderSettings(zone);

        // Mark scene dirty and save scene to disk
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[TwilightZoneSetup] Successfully constructed complete Twilight Zone scene hierarchy matching Sunlight Zone 1:1!");
        if (interactive)
        {
            EditorUtility.DisplayDialog("Setup Complete",
                "Twilight Zone (Level 2) hierarchy has been fully built & saved!\n\n" +
                "Configured Objects:\n" +
                "• Directional Light (Dim mesopelagic cyan-blue)\n" +
                "• Global Volume (SampleSceneProfile)\n" +
                "• EventSystem (InputSystem UI)\n" +
                "• Player (Movement, DepthTracker, Scanner, Camera)\n" +
                "• Environment / OceanFloor (700x700m Seabed + Colliders)\n" +
                "• Boundary (TopBoundary, BottomBoundary, Spawns & 4 Walls)\n" +
                "• Managers (_PCGManager, ZoneSceneSetup, HazardZoneTrigger, Game/Minigame/AudioManager)\n\n" +
                "All references are wired and the scene has been saved.", "OK");
        }
    }

    private static void SetupDirectionalLight(ZoneDefinition zone)
    {
        Light lightComp = Object.FindFirstObjectByType<Light>();
        GameObject lightGO;
        if (lightComp == null)
        {
            lightGO = EnsureGameObject("Directional Light", null);
            lightComp = EnsureComponent<Light>(lightGO);
        }
        else
        {
            lightGO = lightComp.gameObject;
        }

        lightComp.type = LightType.Directional;
        // Deep mesopelagic dim oceanic cyan-blue
        lightComp.color = new Color(0.12f, 0.28f, 0.45f, 1f);
        lightComp.intensity = 0.30f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void SetupGlobalVolume()
    {
        Volume vol = Object.FindFirstObjectByType<Volume>();
        if (vol == null)
        {
            var volGO = EnsureGameObject("Global Volume", null);
            vol = EnsureComponent<Volume>(volGO);
        }
        vol.isGlobal = true;
        vol.weight = 1.0f;

        string profilePath = "Assets/Settings/SampleSceneProfile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile != null)
        {
            vol.sharedProfile = profile;
        }
    }

    private static void SetupEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            var esGO = EnsureGameObject("EventSystem", null);
            EnsureComponent<UnityEngine.EventSystems.EventSystem>(esGO);
            EnsureComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(esGO);
        }
    }

    private static GameObject SetupPlayerAndCamera(ZoneDefinition zone)
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            playerGO = GameObject.Find("Player");
        }
        if (playerGO == null)
        {
            playerGO = new GameObject("Player");
        }

        playerGO.name = "Player";
        playerGO.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) playerGO.layer = playerLayer;
        playerGO.transform.position = new Vector3(0f, -10f, 0f);

        // Core Physics
        var rb = EnsureComponent<Rigidbody>(playerGO);
        rb.useGravity = false;
        rb.linearDamping = 2.0f;
        rb.angularDamping = 10.0f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var col = EnsureComponent<CapsuleCollider>(playerGO);
        col.radius = 0.5f;
        col.height = 2.0f;
        col.direction = 1; // Y-axis
        col.center = Vector3.zero;

        // Visuals (Capsule hull)
        var meshFilter = EnsureComponent<MeshFilter>(playerGO);
        if (meshFilter.sharedMesh == null)
        {
            GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshFilter.sharedMesh = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tempCapsule);
        }

        var meshRenderer = EnsureComponent<MeshRenderer>(playerGO);
        Material playerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat");
        if (playerMat != null)
        {
            meshRenderer.sharedMaterial = playerMat;
        }

        // Camera setup - find or create child Main Camera
        Transform existingCamTr = playerGO.transform.Find("Main Camera");
        GameObject camGO = existingCamTr != null ? existingCamTr.gameObject : null;

        if (camGO == null)
        {
            // Check if there is an existing Camera in the scene to adopt
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCams)
            {
                if (c.gameObject.name == "Main Camera")
                {
                    camGO = c.gameObject;
                    break;
                }
            }
            if (camGO == null && Camera.main != null)
            {
                camGO = Camera.main.gameObject;
            }
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
            }
        }

        camGO.name = "Main Camera";
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(playerGO.transform);
        camGO.transform.localPosition = Vector3.zero;
        camGO.transform.localRotation = Quaternion.identity;

        // Clean up any extra orphan root cameras in scene
        Camera[] sceneCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in sceneCams)
        {
            if (c.gameObject != camGO)
            {
                Debug.Log($"[TwilightZoneSetup] Removing duplicate/orphan camera '{c.gameObject.name}'");
                Object.DestroyImmediate(c.gameObject);
            }
        }

        Camera cam = EnsureComponent<Camera>(camGO);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = zone.fogColor;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = Mathf.Max(zone.fogEndDistance + 25f, 135f);
        cam.fieldOfView = 60f;

        EnsureComponent<AudioListener>(camGO);
        var subCam = EnsureComponent<SubmarineCamera>(camGO);
        var subCamSO = new SerializedObject(subCam);
        SetProperty(subCamSO, "yawSensitivity", 0.15f);
        SetProperty(subCamSO, "pitchSensitivity", 0.12f);
        SetProperty(subCamSO, "minPitch", -50f);
        SetProperty(subCamSO, "maxPitch", 50f);
        SetProperty(subCamSO, "smoothSpeed", 20f);
        subCamSO.ApplyModifiedProperties();

        // URP Camera data
        var urpCam = EnsureComponent<UniversalAdditionalCameraData>(camGO);
        if (urpCam != null)
        {
            urpCam.renderShadows = true;
            urpCam.renderPostProcessing = true;
        }

        // Player Scripts
        var pm = EnsureComponent<PlayerMovement>(playerGO);
        var pmSO = new SerializedObject(pm);
        SetProperty(pmSO, "moveSpeed", 15f);
        SetProperty(pmSO, "bodyTurnSpeed", 3f);
        SetProperty(pmSO, "submarineCamera", subCam);

        string inputActionPath = "Assets/InputSystem_Actions.inputactions";
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionPath);
        if (inputActions != null)
        {
            SetProperty(pmSO, "inputActionsAsset", inputActions);
        }
        pmSO.ApplyModifiedProperties();

        var dt = EnsureComponent<DepthTracker>(playerGO);
        dt.ZoneTopY = 0f;
        dt.ZoneBottomY = -zone.playableDepth;

        var scanner = EnsureComponent<ScannerSystem>(playerGO);
        var scanSO = new SerializedObject(scanner);
        SetProperty(scanSO, "baseRange", 18f);
        SetProperty(scanSO, "interactRange", 7f);
        SetProperty(scanSO, "detectionRange", 100f);
        SetProperty(scanSO, "reticleBeamRadius", 0.5f);
        SetProperty(scanSO, "viewportAimThreshold", 0.12f);
        SetProperty(scanSO, "scanCamera", cam);
        scanSO.ApplyModifiedProperties();

        var audioSource = EnsureComponent<AudioSource>(playerGO);
        audioSource.playOnAwake = false;

        return playerGO;
    }

    private static GameObject SetupEnvironment(ZoneDefinition zone, EnvPropSet twilightProps)
    {
        var envGO = EnsureGameObject("Environment", null);
        envGO.transform.position = Vector3.zero;

        // OceanFloor
        var floorGO = EnsureGameObject("OceanFloor", envGO.transform);
        floorGO.transform.localPosition = new Vector3(0f, -zone.playableDepth, 0f);
        floorGO.transform.localScale = new Vector3(100f, 1f, 100f);

        var meshFilter = EnsureComponent<MeshFilter>(floorGO);
        if (meshFilter.sharedMesh == null)
        {
            GameObject tempPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            meshFilter.sharedMesh = tempPlane.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tempPlane);
        }

        var meshRenderer = EnsureComponent<MeshRenderer>(floorGO);
        Material seabedMat = (twilightProps != null && twilightProps.seabedMaterial != null)
            ? twilightProps.seabedMaterial
            : AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OceanFloor.mat");
        if (seabedMat != null)
        {
            meshRenderer.sharedMaterial = seabedMat;
        }

        var meshCol = EnsureComponent<MeshCollider>(floorGO);
        meshCol.sharedMesh = meshFilter.sharedMesh;

        var boxCol = EnsureComponent<BoxCollider>(floorGO);
        boxCol.center = Vector3.zero;
        boxCol.size = new Vector3(zone.playableWidth, 2f, zone.playableLength);

        return floorGO;
    }

    public struct BoundaryReferences
    {
        public GameObject topBoundary;
        public GameObject bottomBoundary;
        public GameObject topSpawn;
        public GameObject bottomSpawn;
    }

    private static BoundaryReferences SetupBoundaries(ZoneDefinition zone)
    {
        var boundaryGO = EnsureGameObject("Boundary", null);
        boundaryGO.transform.position = Vector3.zero;

        float W = zone.playableWidth;
        float L = zone.playableLength;
        float D = zone.playableDepth;
        float thickness = 5f;

        // 1. Top Boundary (Y = 10, above surface)
        var topB = EnsureGameObject("TopBoundary", boundaryGO.transform);
        topB.transform.localPosition = new Vector3(0f, 10f, 0f);
        var topCol = EnsureComponent<BoxCollider>(topB);
        topCol.isTrigger = true;
        topCol.size = new Vector3(W, thickness, L);
        topCol.center = Vector3.zero;
        var topTrigger = EnsureComponent<ZoneBoundaryTrigger>(topB);
        var topSO = new SerializedObject(topTrigger);
        SetProperty(topSO, "isBottomBoundary", false);
        topSO.ApplyModifiedProperties();
        SetPrivateField(topTrigger, "isBottomBoundary", false);

        // 2. Bottom Boundary (Y = -D - thickness/2)
        var botB = EnsureGameObject("BottomBoundary", boundaryGO.transform);
        botB.transform.localPosition = new Vector3(0f, -D - thickness * 0.5f, 0f);
        var botCol = EnsureComponent<BoxCollider>(botB);
        botCol.isTrigger = true;
        botCol.size = new Vector3(W, thickness, L);
        botCol.center = Vector3.zero;
        var botTrigger = EnsureComponent<ZoneBoundaryTrigger>(botB);
        var botSO = new SerializedObject(botTrigger);
        SetProperty(botSO, "isBottomBoundary", true);
        botSO.ApplyModifiedProperties();
        SetPrivateField(botTrigger, "isBottomBoundary", true);

        // 3. Top Spawn Point (Y = -10)
        var topSpawn = EnsureGameObject("ZoneTopSpawn", boundaryGO.transform);
        topSpawn.transform.localPosition = new Vector3(0f, -10f, 0f);
        EnsureTag(topSpawn, "ZoneTopSpawn");

        // 4. Bottom Spawn Point (Y = -D + 15)
        var botSpawn = EnsureGameObject("ZoneBottomSpawn", boundaryGO.transform);
        botSpawn.transform.localPosition = new Vector3(0f, -D + 15f, 0f);
        EnsureTag(botSpawn, "ZoneBottomSpawn");

        // 5. Perimeter Side Walls
        int terrainLayer = LayerMask.NameToLayer("Terrain") >= 0 ? LayerMask.NameToLayer("Terrain") : 0;
        CreateSideWall("Wall_North", boundaryGO.transform, new Vector3(0f, -D * 0.5f, L * 0.5f), new Vector3(W, D + 40f, thickness), terrainLayer);
        CreateSideWall("Wall_South", boundaryGO.transform, new Vector3(0f, -D * 0.5f, -L * 0.5f), new Vector3(W, D + 40f, thickness), terrainLayer);
        CreateSideWall("Wall_East", boundaryGO.transform, new Vector3(W * 0.5f, -D * 0.5f, 0f), new Vector3(thickness, D + 40f, L), terrainLayer);
        CreateSideWall("Wall_West", boundaryGO.transform, new Vector3(-W * 0.5f, -D * 0.5f, 0f), new Vector3(thickness, D + 40f, L), terrainLayer);

        return new BoundaryReferences
        {
            topBoundary = topB,
            bottomBoundary = botB,
            topSpawn = topSpawn,
            bottomSpawn = botSpawn
        };
    }

    private static void CreateSideWall(string name, Transform parent, Vector3 pos, Vector3 size, int layer)
    {
        var wall = EnsureGameObject(name, parent);
        wall.transform.localPosition = pos;
        wall.layer = layer;
        var col = EnsureComponent<BoxCollider>(wall);
        col.isTrigger = false;
        col.size = size;
        col.center = Vector3.zero;
    }

    private static void SetupManagers(ZoneDefinition zone, EnvPropSet twilightProps, GameObject oceanFloorGO, BoundaryReferences boundaries)
    {
        var managersGO = EnsureGameObject("Managers", null);
        managersGO.transform.position = Vector3.zero;

        // 1. _PCGManager
        var pcgGO = EnsureGameObject("_PCGManager", managersGO.transform);
        pcgGO.transform.localPosition = Vector3.zero;

        var terrainGen = EnsureComponent<TerrainGenerator>(pcgGO);
        if (twilightProps != null)
        {
            var tSO = new SerializedObject(terrainGen);
            SetProperty(tSO, "envPropSet", twilightProps);
            tSO.ApplyModifiedProperties();
            SetPrivateField(terrainGen, "envPropSet", twilightProps);
        }

        var speciesSpawner = EnsureComponent<SpeciesSpawner>(pcgGO);
        var sSO = new SerializedObject(speciesSpawner);
        string registryPath = "Assets/Resources/SpeciesRegistry.asset";
        SpeciesRegistry registry = AssetDatabase.LoadAssetAtPath<SpeciesRegistry>(registryPath);
        if (registry != null)
        {
            SetProperty(sSO, "registry", registry);
            SetPrivateField(speciesSpawner, "registry", registry);
        }
        SetProperty(sSO, "terrain", terrainGen);
        SetPrivateField(speciesSpawner, "terrain", terrainGen);
        SetProperty(sSO, "maxAttempts", 30);
        SetProperty(sSO, "minSeparation", 8f);
        SetProperty(sSO, "overlapRadius", 1.5f);
        SetProperty(sSO, "maxStationarySlope", 18f);

        int creatureMask = LayerMask.GetMask("Creature");
        if (creatureMask == 0) creatureMask = 64;
        var overlapProp = sSO.FindProperty("overlapMask");
        if (overlapProp != null) overlapProp.intValue = creatureMask;
        sSO.ApplyModifiedProperties();

        EnsureComponent<OceanFloorMeshGenerator>(pcgGO);
        EnsureComponent<EnvPropScatterer>(pcgGO);
        EnsureComponent<DistanceCullingManager>(pcgGO);
        EnsureComponent<DebrisSpawner>(pcgGO);
        EnsureComponent<MeshRenderer>(pcgGO);
        EnsureComponent<MeshFilter>(pcgGO);
        EnsureComponent<MeshCollider>(pcgGO);

        // 2. ZoneSceneSetup
        var setupGO = EnsureGameObject("ZoneSceneSetup", managersGO.transform);
        setupGO.transform.localPosition = Vector3.zero;
        var zoneSetup = EnsureComponent<ZoneSceneSetup>(setupGO);
        var zsSO = new SerializedObject(zoneSetup);
        SetProperty(zsSO, "zoneIndex", ZoneIdx);
        SetProperty(zsSO, "topBoundary", boundaries.topBoundary);
        SetProperty(zsSO, "bottomBoundary", boundaries.bottomBoundary);
        SetProperty(zsSO, "topSpawnPoint", boundaries.topSpawn);
        SetProperty(zsSO, "bottomSpawnPoint", boundaries.bottomSpawn);
        SetProperty(zsSO, "oceanFloor", oceanFloorGO);
        zsSO.ApplyModifiedProperties();

        SetPrivateField(zoneSetup, "zoneIndex", ZoneIdx);
        SetPrivateField(zoneSetup, "topBoundary", boundaries.topBoundary);
        SetPrivateField(zoneSetup, "bottomBoundary", boundaries.bottomBoundary);
        SetPrivateField(zoneSetup, "topSpawnPoint", boundaries.topSpawn);
        SetPrivateField(zoneSetup, "bottomSpawnPoint", boundaries.bottomSpawn);
        SetPrivateField(zoneSetup, "oceanFloor", oceanFloorGO);

        // 3. HazardZoneTrigger
        var hazardGO = EnsureGameObject("HazardZoneTrigger", managersGO.transform);
        hazardGO.transform.localPosition = Vector3.zero;
        var hCol = EnsureComponent<SphereCollider>(hazardGO);
        hCol.isTrigger = false;
        hCol.radius = 0.5f;
        hCol.enabled = false;

        var hazardTrigger = EnsureComponent<HazardZoneTrigger>(hazardGO);
        var hSO = new SerializedObject(hazardTrigger);
        SetProperty(hSO, "zoneIndex", ZoneIdx);
        SetProperty(hSO, "triggerRadius", 18f);
        SetProperty(hSO, "triggerCooldown", 180f);
        SetProperty(hSO, "enablePeriodicSurge", true);
        hSO.ApplyModifiedProperties();

        // 4. GameManager
        var gmGO = EnsureGameObject("GameManager", managersGO.transform);
        gmGO.transform.localPosition = Vector3.zero;
        EnsureComponent<GameManager>(gmGO);

        // 5. MinigameManager
        var mmGO = EnsureGameObject("MinigameManager", managersGO.transform);
        mmGO.transform.localPosition = Vector3.zero;
        EnsureComponent<MinigameManager>(mmGO);

        // 6. AudioManager Prefab
        Transform audioTr = managersGO.transform.Find("AudioManager");
        if (audioTr == null)
        {
            string audioPrefabPath = "Assets/Prefabs/AudioManager.prefab";
            GameObject audioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(audioPrefabPath);
            if (audioPrefab != null)
            {
                var audioInstance = PrefabUtility.InstantiatePrefab(audioPrefab, managersGO.transform) as GameObject;
                if (audioInstance != null)
                {
                    audioInstance.name = "AudioManager";
                    audioInstance.transform.localPosition = Vector3.zero;
                }
            }
        }
    }

    private static void ApplyRenderSettings(ZoneDefinition zone)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = zone.fogColor;
        RenderSettings.fogStartDistance = zone.fogStartDistance;
        RenderSettings.fogEndDistance = zone.fogEndDistance;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = zone.ambientLight;
    }

    private static EnvPropSet EnsureTwilightProps()
    {
        string path = "Assets/Scripts/ScriptableObjects/Environment/TwilightProps.asset";
        var propSet = AssetDatabase.LoadAssetAtPath<EnvPropSet>(path);
        if (propSet != null) return propSet;

        string sunlightPath = "Assets/Scripts/ScriptableObjects/Environment/SunlightProps.asset";
        var sunlightProps = AssetDatabase.LoadAssetAtPath<EnvPropSet>(sunlightPath);
        if (sunlightProps != null)
        {
            propSet = Object.Instantiate(sunlightProps);
            propSet.name = "TwilightProps";
            propSet.propDensity = 2f;
            propSet.globalScaleMultiplier = 8f;
            AssetDatabase.CreateAsset(propSet, path);
            AssetDatabase.SaveAssets();
            return propSet;
        }

        return null;
    }

    // ── Safe Helpers ────────────────────────────────────────────────────────

    private static GameObject EnsureGameObject(string name, Transform parent)
    {
        GameObject go = null;
        if (parent != null)
        {
            Transform child = parent.Find(name);
            if (child != null) go = child.gameObject;
        }

        if (go == null)
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in all)
            {
                if (candidate.name == name)
                {
                    if (parent == null && candidate.transform.parent == null)
                    {
                        go = candidate;
                        break;
                    }
                    if (parent != null && candidate.transform.parent == parent)
                    {
                        go = candidate;
                        break;
                    }
                }
            }
        }

        if (go == null)
        {
            go = new GameObject(name);
        }

        if (parent != null && go.transform.parent != parent)
        {
            go.transform.SetParent(parent);
        }

        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go == null) return null;
        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = go.AddComponent<T>();
        }
        return comp;
    }

    private static void EnsureTag(GameObject go, string tag)
    {
        try { go.tag = tag; }
        catch { /* Tag might not be created in project settings yet */ }
    }

    private static void SetProperty(SerializedObject so, string propertyName, object value)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null) return;

        if (value is int intVal) prop.intValue = intVal;
        else if (value is float floatVal) prop.floatValue = floatVal;
        else if (value is bool boolVal) prop.boolValue = boolVal;
        else if (value is string strVal) prop.stringValue = strVal;
        else if (value is Object objVal) prop.objectReferenceValue = objVal;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
#endif
