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
    // -----------------------------------------------------------------------

    public static ZoneManager Instance { get; private set; }

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

        string sceneName = ZoneConfig.Zones[targetZoneIndex].sceneName;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Returns to the zone selection menu.</summary>
    public void ReturnToZoneSelect()
    {
        SceneManager.LoadScene(zoneSelectSceneName);
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

        // 4. Move submarine to correct spawn position
        GameObject sub = GameObject.FindGameObjectWithTag("Player");
        if (sub != null)
        {
            Transform spawnPoint = EnteredFromAbove ? _topSpawn : _bottomSpawn;
            if (spawnPoint != null)
            {
                sub.transform.position = spawnPoint.position;
                sub.transform.rotation = spawnPoint.rotation;
            }
            else
            {
                // Fallback: top-centre or bottom-centre of zone
                float spawnY = EnteredFromAbove ? 0f : -zone.playableDepth + 10f;
                sub.transform.position = new Vector3(0f, spawnY, 0f);
            }

            // Snap camera yaw so there's no jarring snap
            var camCtrl = sub.GetComponentInChildren<SubmarineCamera>();
            camCtrl?.SnapYawToBody();
        }

        Debug.Log($"[ZoneManager] Loaded '{zone.zoneName}' | " +
                  $"Playable: {zone.playableWidth}x{zone.playableLength}x{zone.playableDepth}m | " +
                  $"Display: {zone.displayDepthMin}–{zone.displayDepthMax}m");

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
    }

    private void ApplyAtmosphere(ZoneDefinition zone)
    {
        RenderSettings.fog             = true;
        RenderSettings.fogMode         = FogMode.Exponential;
        RenderSettings.fogColor        = zone.fogColor;
        RenderSettings.fogDensity      = zone.fogDensity;
        RenderSettings.ambientLight    = zone.ambientLight;
        RenderSettings.ambientMode     = UnityEngine.Rendering.AmbientMode.Flat;
        Camera.main.backgroundColor    = zone.fogColor;
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
