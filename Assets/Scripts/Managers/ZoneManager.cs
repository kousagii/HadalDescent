using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that owns zone state and handles scene-to-scene transitions.
///
/// Responsibilities:
///   - Tracks which zone is currently loaded (CurrentZoneIndex).
///   - Configures the scene atmosphere (fog, ambient light) on load.
///   - Positions the submarine at the correct spawn point (top or bottom).
///   - Provides LoadZone() to trigger a transition with an optional loading screen.
///
/// Persists across scenes via DontDestroyOnLoad.
/// </summary>
public class ZoneManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    private static ZoneManager _instance;
    public static ZoneManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ZoneManager>();
                if (_instance == null)
                {
                    var go = new GameObject("ZoneManager");
                    _instance = go.AddComponent<ZoneManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    // -----------------------------------------------------------------------
    // Zone state (static so DepthTracker can read without a reference)
    // -----------------------------------------------------------------------

    /// <summary>Index into ZoneConfig.Zones for the currently loaded zone.</summary>
    public static int CurrentZoneIndex { get; private set; } = 0;

    /// <summary>
    /// True when the player arrived from above (entered at zone top).
    /// False when ascending from a deeper zone (entered at zone bottom).
    /// </summary>
    public static bool EnteredFromAbove { get; private set; } = true;

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Scene Names")]
    [Tooltip("Exact scene name for the zone select menu.")]
    [SerializeField] private string zoneSelectSceneName = "ZoneSelect";

    [Tooltip("Exact scene name for the loading screen.")]
    [SerializeField] private string loadingSceneName = "LoadingScreen";

    [Header("References (auto-found in scene)")]
    [SerializeField] private DepthTracker depthTracker;

    // -----------------------------------------------------------------------
    // Spawn points
    // -----------------------------------------------------------------------

    // Each zone scene must have GameObjects tagged "ZoneTopSpawn" and "ZoneBottomSpawn".
    // If not found, defaults are used based on ZoneConfig dimensions.
    private Transform _topSpawn;
    private Transform _bottomSpawn;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Detect which zone scene is *already* active when PersistentManagers
        // loads additively, or when Play is pressed directly inside a zone scene.
        string activeName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < ZoneConfig.ZoneCount; i++)
        {
            if (ZoneConfig.Zones[i].sceneName == activeName)
            {
                CurrentZoneIndex = i;
                Debug.Log($"[ZoneManager] Detected active zone scene '{activeName}' → CurrentZoneIndex = {i} ({ZoneConfig.Zones[i].zoneName})");
                break;
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ignore non-gameplay scenes (menus, loading screen)
        bool isZoneScene = false;
        for (int i = 0; i < ZoneConfig.ZoneCount; i++)
        {
            if (ZoneConfig.Zones[i].sceneName == scene.name)
            {
                isZoneScene = true;
                CurrentZoneIndex = i;
                break;
            }
        }
        if (!isZoneScene) return;

        StartCoroutine(SetupZoneScene());
    }

    // -----------------------------------------------------------------------
    // Zone transition (public API)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Load a zone by index. Call this from ZoneBoundaryTrigger after the player confirms.
    /// </summary>
    /// <param name="targetZoneIndex">0-4</param>
    /// <param name="enteredFromAbove">True if descending into zone, false if ascending.</param>
    public void LoadZone(int targetZoneIndex, bool enteredFromAbove)
    {
        if (!ZoneConfig.IsValidZone(targetZoneIndex))
        {
            Debug.LogWarning($"[ZoneManager] Invalid zone index: {targetZoneIndex}");
            return;
        }

        CurrentZoneIndex  = targetZoneIndex;
        EnteredFromAbove  = enteredFromAbove;
        PlayerPrefs.SetInt("Save_HasPosition", 0);

        string sceneName = ZoneConfig.Zones[targetZoneIndex].sceneName;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Returns to the zone selection menu.</summary>
    public void ReturnToZoneSelect()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainMenuBGM();
        }

        if (ZoneSelectionUI.Instance != null)
        {
            ZoneSelectionUI.Instance.OpenZoneSelection();
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenZoneSelection();
        }
        else if (Application.CanStreamedLevelBeLoaded(zoneSelectSceneName))
        {
            SceneManager.LoadScene(zoneSelectSceneName);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // -----------------------------------------------------------------------
    // Scene setup
    // -----------------------------------------------------------------------

    private IEnumerator SetupZoneScene()
    {
        // Wait one frame so all scene objects have Awake/Start called
        yield return null;

        ZoneDefinition zone = ZoneConfig.Zones[CurrentZoneIndex];

        // 1. Apply atmosphere
        ApplyAtmosphere(zone);

        // 2. Find spawn tags, fall back to computed defaults
        FindSpawnPoints(zone);

        // 3. Configure DepthTracker boundaries
        depthTracker = FindFirstObjectByType<DepthTracker>();
        if (depthTracker != null)
        {
            depthTracker.ZoneTopY    = _topSpawn    != null ? _topSpawn.position.y    : 0f;
            depthTracker.ZoneBottomY = _bottomSpawn != null ? _bottomSpawn.position.y : -zone.playableDepth;
        }

        // 4. Move submarine to correct spawn position (or restore saved position on Continue)
        GameObject sub = GameObject.FindGameObjectWithTag("Player") ?? FindFirstObjectByType<PlayerMovement>()?.gameObject;
        if (sub != null)
        {
            if (PlayerPrefs.GetInt("Save_HasPosition", 0) == 1)
            {
                float x = PlayerPrefs.GetFloat("Save_PosX", 0f);
                float y = PlayerPrefs.GetFloat("Save_PosY", 0f);
                float z = PlayerPrefs.GetFloat("Save_PosZ", 0f);
                float rotY = PlayerPrefs.GetFloat("Save_RotY", 0f);

                sub.transform.position = new Vector3(x, y, z);
                sub.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

                var rb = sub.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = new Vector3(x, y, z);
                    rb.linearVelocity = Vector3.zero;
                }

                var camCtrl = sub.GetComponentInChildren<SubmarineCamera>();
                if (camCtrl != null)
                {
                    camCtrl.SnapYawToBody();
                }

                Debug.Log($"[ZoneManager] Restored saved player position: ({x:F1}, {y:F1}, {z:F1})");
            }
            else
            {
                Transform spawnPoint = EnteredFromAbove ? _topSpawn : _bottomSpawn;
                Vector3 spawnPos;
                Quaternion spawnRot = Quaternion.identity;
                if (spawnPoint != null)
                {
                    // If entered from above, spawn slightly below surface (Depth ~8m)
                    // so the player does not instantly touch the surface boundary on frame 1
                    Vector3 offset = EnteredFromAbove ? new Vector3(0f, -5f, 0f) : new Vector3(0f, 6f, 0f);
                    spawnPos = spawnPoint.position + offset;
                    spawnRot = spawnPoint.rotation;
                }
                else
                {
                    // Fallback: top-centre or bottom-centre of zone
                    float spawnY = EnteredFromAbove ? -6f : -zone.playableDepth + 10f;
                    spawnPos = new Vector3(0f, spawnY, 0f);
                }

                sub.transform.position = spawnPos;
                sub.transform.rotation = spawnRot;
                var subRb = sub.GetComponent<Rigidbody>();
                if (subRb != null)
                {
                    subRb.position = spawnPos;
                    subRb.linearVelocity = Vector3.zero;
                }

                // Snap camera yaw so there's no jarring snap
                var camCtrl = sub.GetComponentInChildren<SubmarineCamera>();
                camCtrl?.SnapYawToBody();
            }
        }

        Debug.Log($"[ZoneManager] Loaded '{zone.zoneName}' | " +
                  $"Playable: {zone.playableWidth}x{zone.playableLength}x{zone.playableDepth}m | " +
                  $"Display: {zone.displayDepthMin}-{zone.displayDepthMax}m");

        // 5. Generate terrain via Perlin Noise (TerrainGenerator must be in the zone scene)
        var terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null)
        {
            terrain.Generate(CurrentZoneIndex);
        }
        else
        {
            Debug.LogWarning("[ZoneManager] No TerrainGenerator found in scene. " +
                             "Add a GameObject with TerrainGenerator to the zone scene.");
        }

        // 6. Spawn species using ecological placement rules
        var spawner = FindFirstObjectByType<SpeciesSpawner>();
        if (spawner != null)
        {
            spawner.SpawnForZone(CurrentZoneIndex);
        }
        else
        {
            Debug.LogWarning("[ZoneManager] No SpeciesSpawner found in scene. " +
                             "Add a GameObject with SpeciesSpawner to the zone scene.");
        }

        // 7. Play zone-specific BGM
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayZoneBGM(CurrentZoneIndex);
        }
    }

    public static void ApplyAtmosphere(ZoneDefinition zone)
    {
        RenderSettings.fog              = true;
        RenderSettings.fogMode          = FogMode.Linear;
        RenderSettings.fogColor         = zone.fogColor;
        RenderSettings.fogStartDistance = zone.fogStartDistance;
        RenderSettings.fogEndDistance   = zone.fogEndDistance;
        RenderSettings.fogDensity       = zone.fogDensity;
        RenderSettings.ambientLight     = zone.ambientLight;
        RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;

        // Apply only to main scene cameras, never offscreen, UI, or preview cameras
        var mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (mainCam != null && mainCam.targetTexture == null)
        {
            mainCam.clearFlags      = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = zone.fogColor;
            mainCam.farClipPlane    = Mathf.Max(zone.fogEndDistance + 25f, 120f);
        }

        var cams = Camera.allCameras;
        if (cams != null)
        {
            foreach (var cam in cams)
            {
                if (cam == null || cam == mainCam) continue;
                // Exclude any camera rendering to a RenderTexture or specialized preview/stage cameras
                if (cam.targetTexture != null) continue;
                string camName = cam.gameObject.name;
                if (camName.Contains("Preview") || camName.Contains("Stage") || camName.Contains("Minigame")) continue;

                if (cam.cameraType == CameraType.Game && !cam.orthographic && cam.CompareTag("MainCamera"))
                {
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    cam.backgroundColor = zone.fogColor;
                    cam.farClipPlane    = Mathf.Max(zone.fogEndDistance + 25f, 120f);
                }
            }
        }
    }

    private void FindSpawnPoints(ZoneDefinition zone)
    {
        _topSpawn    = FindTaggedOrNull("ZoneTopSpawn");
        _bottomSpawn = FindTaggedOrNull("ZoneBottomSpawn");

        // If scene doesn't have tagged spawns, create virtual ones at defaults
        if (_topSpawn == null)
        {
            GameObject go = new GameObject("_TopSpawn_Auto");
            go.transform.position = new Vector3(0f, 0f, 0f);
            _topSpawn = go.transform;
        }

        if (_bottomSpawn == null)
        {
            GameObject go = new GameObject("_BottomSpawn_Auto");
            go.transform.position = new Vector3(0f, -zone.playableDepth + 10f, 0f);
            _bottomSpawn = go.transform;
        }
    }

    private static Transform FindTaggedOrNull(string tag)
    {
        try
        {
            GameObject go = GameObject.FindGameObjectWithTag(tag);
            return go != null ? go.transform : null;
        }
        catch { return null; }
    }

    // -----------------------------------------------------------------------
    // Convenience
    // -----------------------------------------------------------------------

    public ZoneDefinition CurrentZone => ZoneConfig.Zones[CurrentZoneIndex];
}
