#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to construct the complete architectural scene hierarchy for
/// Midnight Zone (Zone 2), Abyss Zone (Zone 3), and Hadal Zone (Zone 4)
/// matching SunlightZone.unity and TwilightZone.unity 1:1.
///
/// Run via: Menu -> HadalDescent -> Setup All Deep Zones (Midnight, Abyss, Hadal)
/// </summary>
public static class DeepZonesSetupUtility
{
    [MenuItem("HadalDescent/Setup Midnight Zone Scene")]
    public static void SetupMidnightZone() => SetupZone(2, "Assets/Scenes/MidnightZone.unity");

    [MenuItem("HadalDescent/Setup Abyss Zone Scene")]
    public static void SetupAbyssZone() => SetupZone(3, "Assets/Scenes/AbyssZone.unity");

    [MenuItem("HadalDescent/Setup Hadal Zone Scene")]
    public static void SetupHadalZone() => SetupZone(4, "Assets/Scenes/HadalZone.unity");

    [MenuItem("HadalDescent/Setup All Deep Zones (Midnight, Abyss, Hadal)")]
    public static void SetupAllDeepZonesMenu()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Setup All Deep Ocean Zones?",
            "This will construct the complete gameplay hierarchy (Player Submarine, Ocean Floor, PCG Terrain, Boundaries, Species Spawner, and Managers) for:\n\n" +
            "• Midnight Zone (Zone 2)\n" +
            "• Abyss Zone (Zone 3)\n" +
            "• Hadal Zone (Zone 4)\n\n" +
            "Proceed?",
            "Yes, Setup All 3 Zones", "Cancel");

        if (!proceed) return;

        SetupZone(2, "Assets/Scenes/MidnightZone.unity", false);
        SetupZone(3, "Assets/Scenes/AbyssZone.unity", false);
        SetupZone(4, "Assets/Scenes/HadalZone.unity", false);

        EditorUtility.DisplayDialog("Setup Complete",
            "Successfully constructed complete scene hierarchies for Midnight, Abyss, and Hadal zones!\n\n" +
            "All scenes have been configured with boundaries, players, procedural terrain, and species spawners and saved to disk.", "OK");
    }

    public static void SetupZone(int zoneIdx, string scenePath, bool showDialog = true)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"[DeepZonesSetup] Could not find scene at '{scenePath}'");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        ZoneDefinition zone = ZoneConfig.Zones[zoneIdx];
        Debug.Log($"[DeepZonesSetup] Constructing '{zone.zoneName}' hierarchy ({zone.playableWidth}x{zone.playableLength}x{zone.playableDepth}m)...");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Setup {zone.zoneName}");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. EnvPropSet for this zone
        EnvPropSet props = LoadZoneProps(zoneIdx);

        // 2. Setup Directional Light
        SetupDirectionalLight(zone, zoneIdx);

        // 3. Setup Global Volume
        SetupGlobalVolume();

        // 4. Setup EventSystem
        SetupEventSystem();

        // 5. Setup Player & Camera hierarchy (removes orphaned cameras)
        GameObject playerGO = SetupPlayerAndCamera(zone);

        // 6. Setup Environment container & OceanFloor
        GameObject oceanFloorGO = SetupEnvironment(zone, props);

        // 7. Setup Boundary container, triggers, spawns & side walls
        var boundaries = SetupBoundaries(zone);

        // 8. Setup Managers container (_PCGManager, ZoneSceneSetup, HazardZoneTrigger, GameManager, MinigameManager, AudioManager)
        SetupManagers(zone, zoneIdx, props, oceanFloorGO, boundaries);

        // 9. Apply Scene Atmosphere
        ZoneManager.ApplyAtmosphere(zone);
        ApplyRenderSettings(zone);

        // Mark scene dirty and save scene to disk
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[DeepZonesSetup] Successfully configured and saved '{zone.zoneName}'!");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Setup Complete",
                $"{zone.zoneName} hierarchy has been fully built & saved!\n\n" +
                $"• Playable Depth: {zone.playableDepth}m (HUD: {zone.displayDepthMin}m - {zone.displayDepthMax}m)\n" +
                $"• Top Boundary: Y = 0m | Bottom Boundary: Y = -{zone.playableDepth}m\n" +
                $"• PCG Terrain & SpeciesSpawner wired to SpeciesRegistry.", "OK");
        }
    }

    private static EnvPropSet LoadZoneProps(int zoneIdx)
    {
        string name = zoneIdx switch
        {
            2 => "MidnightProps",
            3 => "AbyssProps",
            4 => "HadalProps",
            _ => "TwilightProps"
        };
        string path = $"Assets/Scripts/ScriptableObjects/Environment/{name}.asset";
        return AssetDatabase.LoadAssetAtPath<EnvPropSet>(path);
    }

    private static void SetupDirectionalLight(ZoneDefinition zone, int zoneIdx)
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
        lightComp.color = zoneIdx switch
        {
            2 => new Color(0.04f, 0.05f, 0.12f, 1f),
            3 => new Color(0.02f, 0.02f, 0.05f, 1f),
            4 => new Color(0.01f, 0.01f, 0.03f, 1f),
            _ => new Color(0.12f, 0.28f, 0.45f, 1f)
        };
        lightComp.intensity = zoneIdx switch
        {
            2 => 0.15f,
            3 => 0.08f,
            4 => 0.04f,
            _ => 0.30f
        };
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
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
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
        meshRenderer.enabled = false; // Keep submarine body invisible to first-person camera

        // Camera setup - find or create child Main Camera
        Transform existingCamTr = playerGO.transform.Find("Main Camera");
        GameObject camGO = existingCamTr != null ? existingCamTr.gameObject : null;

        if (camGO == null)
        {
            Camera existingSceneCam = Object.FindFirstObjectByType<Camera>();
            if (existingSceneCam != null && existingSceneCam.transform.parent != playerGO.transform)
            {
                camGO = existingSceneCam.gameObject;
                camGO.transform.SetParent(playerGO.transform);
                camGO.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
                camGO.transform.localRotation = Quaternion.identity;
                camGO.name = "Main Camera";
            }
            else
            {
                camGO = EnsureGameObject("Main Camera", playerGO.transform);
                camGO.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
                camGO.transform.localRotation = Quaternion.identity;
            }
        }

        camGO.tag = "MainCamera";

        // Remove any duplicate cameras in scene
        Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in allCams)
        {
            if (c.gameObject != camGO)
            {
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

        // Submarine Headlights & Fog Penetration Controller
        var lightCtrl = EnsureComponent<SubmarineLightController>(camGO);
        if (lightCtrl != null)
        {
            lightCtrl.EnsureLightRigBuilt();
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

    private static GameObject SetupEnvironment(ZoneDefinition zone, EnvPropSet props)
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
        Material seabedMat = (props != null && props.seabedMaterial != null)
            ? props.seabedMaterial
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

    private static void SetupManagers(ZoneDefinition zone, int zoneIdx, EnvPropSet props, GameObject oceanFloorGO, BoundaryReferences boundaries)
    {
        var managersGO = EnsureGameObject("Managers", null);
        managersGO.transform.position = Vector3.zero;

        // 1. _PCGManager
        var pcgGO = EnsureGameObject("_PCGManager", managersGO.transform);
        pcgGO.transform.localPosition = Vector3.zero;
        pcgGO.SetActive(true);

        var terrainGen = EnsureComponent<TerrainGenerator>(pcgGO);
        if (props != null)
        {
            var tSO = new SerializedObject(terrainGen);
            SetProperty(tSO, "envPropSet", props);
            tSO.ApplyModifiedProperties();
            SetPrivateField(terrainGen, "envPropSet", props);
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
        var pcgMR = EnsureComponent<MeshRenderer>(pcgGO);
        if (pcgMR != null) pcgMR.enabled = true;
        EnsureComponent<MeshFilter>(pcgGO);
        EnsureComponent<MeshCollider>(pcgGO);

        // 2. ZoneSceneSetup
        var setupGO = EnsureGameObject("ZoneSceneSetup", managersGO.transform);
        setupGO.transform.localPosition = Vector3.zero;
        var zoneSetup = EnsureComponent<ZoneSceneSetup>(setupGO);
        var zsSO = new SerializedObject(zoneSetup);
        SetProperty(zsSO, "zoneIndex", zoneIdx);
        SetProperty(zsSO, "topBoundary", boundaries.topBoundary);
        SetProperty(zsSO, "bottomBoundary", boundaries.bottomBoundary);
        SetProperty(zsSO, "topSpawnPoint", boundaries.topSpawn);
        SetProperty(zsSO, "bottomSpawnPoint", boundaries.bottomSpawn);
        SetProperty(zsSO, "oceanFloor", oceanFloorGO);
        zsSO.ApplyModifiedProperties();

        SetPrivateField(zoneSetup, "zoneIndex", zoneIdx);
        SetPrivateField(zoneSetup, "topBoundary", boundaries.topBoundary);
        SetPrivateField(zoneSetup, "bottomBoundary", boundaries.bottomBoundary);
        SetPrivateField(zoneSetup, "topSpawnPoint", boundaries.topSpawn);
        SetPrivateField(zoneSetup, "bottomSpawnPoint", boundaries.bottomSpawn);
        SetPrivateField(zoneSetup, "oceanFloor", oceanFloorGO);

        // 3. HazardZoneTrigger
        var hazardGO = EnsureGameObject("HazardZoneTrigger", managersGO.transform);
        hazardGO.transform.localPosition = Vector3.zero;
        var hCol = hazardGO.GetComponent<SphereCollider>();
        if (hCol != null) Object.DestroyImmediate(hCol);

        var hazardTrigger = EnsureComponent<HazardZoneTrigger>(hazardGO);
        var hSO = new SerializedObject(hazardTrigger);
        SetProperty(hSO, "zoneIndex", zoneIdx);
        SetProperty(hSO, "enableSpatialProximity", false);
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
                    if (parent == null && candidate.transform.parent == null) { go = candidate; break; }
                    if (parent != null && candidate.transform.parent == parent) { go = candidate; break; }
                }
            }
        }

        if (go == null) go = new GameObject(name);
        if (parent != null && go.transform.parent != parent) go.transform.SetParent(parent);

        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go == null) return null;
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void EnsureTag(GameObject go, string tag)
    {
        try { go.tag = tag; } catch { }
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
        if (field != null) field.SetValue(target, value);
    }
}
#endif
