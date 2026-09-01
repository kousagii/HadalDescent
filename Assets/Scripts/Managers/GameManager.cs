using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that stores all persistent player state.
/// Survives across all scene loads.
/// </summary>
public class GameManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton (with auto-fallback creation)
    // -----------------------------------------------------------------------

    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Player stats
    // -----------------------------------------------------------------------

    [Header("Currency")]
    public int RDP = 0;

    [Header("Submarine Upgrades")]
    [Range(1, 5)] public int HullTier      = 1;
    [Range(1, 5)] public int EngineTier    = 1;
    [Range(1, 5)] public int SonarTier     = 1;
    [Range(1, 5)] public int ScannerTier   = 1;
    [Range(1, 5)] public int LightTier     = 1;
    [Range(1, 5)] public int UtilitiesTier = 1;

    [Header("Collectibles")]
    public int ClamShells = 0;

    // -----------------------------------------------------------------------
    // Species discovery
    // -----------------------------------------------------------------------

    // Global set of all discovered species IDs
    private readonly HashSet<string> _allDiscoveredSpecies = new HashSet<string>();

    // Key: zoneIndex (0-4), Value: set of discovered species IDs in that zone
    private readonly Dictionary<int, HashSet<string>> _discoveredByZone
        = new Dictionary<int, HashSet<string>>();

    // -----------------------------------------------------------------------
    // PCG seeds
    // -----------------------------------------------------------------------

    private readonly Dictionary<int, int> _zonePcgSeeds = new Dictionary<int, int>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------------------------------
    // Currency management
    // -----------------------------------------------------------------------

    public void AddRDP(int amount)
    {
        RDP += amount;
        UIManager.Instance?.RefreshHUD();
        Debug.Log($"[GameManager] +{amount} RDP (total: {RDP})");
    }

    public bool TrySpendRDP(int amount)
    {
        if (RDP < amount) return false;
        RDP -= amount;
        UIManager.Instance?.RefreshHUD();
        return true;
    }

    /// <summary>
    /// Deducts RDP with strict non-negative clamping (cannot drop below 0).
    /// Used for hazard minigame penalties.
    /// </summary>
    public void DeductRDP(int amount)
    {
        RDP = Mathf.Max(0, RDP - amount);
        UIManager.Instance?.RefreshHUD();
        Debug.Log($"[GameManager] -{amount} RDP (total: {RDP})");
    }

    // -----------------------------------------------------------------------
    // Species discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mark a species as discovered.
    /// Returns true if this is a new discovery.
    /// </summary>
    public bool DiscoverSpecies(int zoneIndex, string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return false;

        bool isGloballyNew = _allDiscoveredSpecies.Add(speciesId);

        if (!_discoveredByZone.ContainsKey(zoneIndex))
            _discoveredByZone[zoneIndex] = new HashSet<string>();

        bool isZoneNew = _discoveredByZone[zoneIndex].Add(speciesId);

        if (isGloballyNew)
            Debug.Log($"[GameManager] New species cataloged in Bestiary! ID: {speciesId} (Zone {zoneIndex})");

        return isGloballyNew || isZoneNew;
    }

    /// <summary>Returns true if the species has already been discovered anywhere or in this zone.</summary>
    public bool IsDiscovered(int zoneIndex, string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return false;
        if (_allDiscoveredSpecies.Contains(speciesId)) return true;
        return _discoveredByZone.TryGetValue(zoneIndex, out var set) && set.Contains(speciesId);
    }

    /// <summary>Returns true if the species has been discovered anywhere.</summary>
    public bool IsDiscovered(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return false;
        return _allDiscoveredSpecies.Contains(speciesId);
    }

    /// <summary>How many distinct species have been discovered in this zone.</summary>
    public int GetDiscoveredCountInZone(int zoneIndex)
    {
        return _discoveredByZone.TryGetValue(zoneIndex, out var set) ? set.Count : 0;
    }

    /// <summary>Returns 0.0–1.0 progress for zone unlock (discovered / totalSpecies).</summary>
    public float GetZoneDiscoveryProgress(int zoneIndex)
    {
        int total = ZoneConfig.IsValidZone(zoneIndex)
            ? ZoneConfig.Zones[zoneIndex].totalSpeciesCount
            : 0;
        if (total <= 0) return 0f;
        return (float)GetDiscoveredCountInZone(zoneIndex) / total;
    }

    // -----------------------------------------------------------------------
    // PCG seed management
    // -----------------------------------------------------------------------

    public int GetOrCreatePcgSeed(int zoneIndex)
    {
        if (!_zonePcgSeeds.TryGetValue(zoneIndex, out int seed))
        {
            seed = Random.Range(100000, 999999);
            _zonePcgSeeds[zoneIndex] = seed;
        }
        return seed;
    }

    public int GetOrCreateZoneSeed(int zoneIndex) => GetOrCreatePcgSeed(zoneIndex);

    // -----------------------------------------------------------------------
    // Save & Load System
    // -----------------------------------------------------------------------

    public void SaveGame()
    {
        PlayerPrefs.SetInt("Save_RDP", RDP);
        PlayerPrefs.SetInt("Save_HullTier", HullTier);
        PlayerPrefs.SetInt("Save_EngineTier", EngineTier);
        PlayerPrefs.SetInt("Save_SonarTier", SonarTier);
        PlayerPrefs.SetInt("Save_ScannerTier", ScannerTier);
        PlayerPrefs.SetInt("Save_LightTier", LightTier);
        PlayerPrefs.SetInt("Save_UtilitiesTier", UtilitiesTier);
        PlayerPrefs.SetInt("Save_ClamShells", ClamShells);
        PlayerPrefs.SetInt("Save_CurrentZone", ZoneManager.CurrentZoneIndex);

        // Save Discovered Species
        string speciesData = string.Join(";", _allDiscoveredSpecies);
        PlayerPrefs.SetString("Save_DiscoveredSpecies", speciesData);

        // Save Player Position & Rotation
        var player = FindFirstObjectByType<PlayerMovement>();
        GameObject playerGO = player != null ? player.gameObject : GameObject.FindGameObjectWithTag("Player");

        if (playerGO != null)
        {
            Vector3 pos = playerGO.transform.position;
            float rotY = playerGO.transform.eulerAngles.y;
            PlayerPrefs.SetFloat("Save_PosX", pos.x);
            PlayerPrefs.SetFloat("Save_PosY", pos.y);
            PlayerPrefs.SetFloat("Save_PosZ", pos.z);
            PlayerPrefs.SetFloat("Save_RotY", rotY);
            PlayerPrefs.SetInt("Save_HasPosition", 1);
            Debug.Log($"[GameManager] Saved player position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}), rotY: {rotY:F1}");
        }
        else
        {
            Debug.LogWarning("[GameManager] Could not find player GameObject or PlayerMovement to save position!");
        }

        PlayerPrefs.Save();
        Debug.Log("[GameManager] Game Saved Successfully!");
    }

    public bool LoadGame()
    {
        if (!HasSaveData()) return false;

        RDP           = PlayerPrefs.GetInt("Save_RDP", 0);
        HullTier      = PlayerPrefs.GetInt("Save_HullTier", 1);
        EngineTier    = PlayerPrefs.GetInt("Save_EngineTier", 1);
        SonarTier     = PlayerPrefs.GetInt("Save_SonarTier", 1);
        ScannerTier   = PlayerPrefs.GetInt("Save_ScannerTier", 1);
        LightTier     = PlayerPrefs.GetInt("Save_LightTier", 1);
        UtilitiesTier = PlayerPrefs.GetInt("Save_UtilitiesTier", 1);
        ClamShells    = PlayerPrefs.GetInt("Save_ClamShells", 0);

        string speciesData = PlayerPrefs.GetString("Save_DiscoveredSpecies", "");
        _allDiscoveredSpecies.Clear();
        _discoveredByZone.Clear();

        if (!string.IsNullOrEmpty(speciesData))
        {
            string[] ids = speciesData.Split(';');
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _allDiscoveredSpecies.Add(id);
                }
            }
        }

        UIManager.Instance?.RefreshHUD();
        Debug.Log("[GameManager] Game Loaded Successfully!");
        return true;
    }

    /// <summary>
    /// Call after the zone scene has loaded to teleport the player to the saved position.
    /// </summary>
    public void ApplySavedPosition()
    {
        if (PlayerPrefs.GetInt("Save_HasPosition", 0) != 1) return;

        var player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("[GameManager] ApplySavedPosition: PlayerMovement not found.");
            return;
        }

        float x = PlayerPrefs.GetFloat("Save_PosX", 0f);
        float y = PlayerPrefs.GetFloat("Save_PosY", 0f);
        float z = PlayerPrefs.GetFloat("Save_PosZ", 0f);
        float rotY = PlayerPrefs.GetFloat("Save_RotY", 0f);

        player.transform.position = new Vector3(x, y, z);
        player.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        Debug.Log($"[GameManager] Restored position: ({x}, {y}, {z}), rotY: {rotY}");
    }

    public static bool HasSaveData()
    {
        return PlayerPrefs.HasKey("Save_RDP");
    }

    public static void DeleteSaveData()
    {
        PlayerPrefs.DeleteKey("Save_RDP");
        PlayerPrefs.DeleteKey("Save_HullTier");
        PlayerPrefs.DeleteKey("Save_EngineTier");
        PlayerPrefs.DeleteKey("Save_SonarTier");
        PlayerPrefs.DeleteKey("Save_ScannerTier");
        PlayerPrefs.DeleteKey("Save_LightTier");
        PlayerPrefs.DeleteKey("Save_UtilitiesTier");
        PlayerPrefs.DeleteKey("Save_ClamShells");
        PlayerPrefs.DeleteKey("Save_CurrentZone");
        PlayerPrefs.DeleteKey("Save_DiscoveredSpecies");
        PlayerPrefs.DeleteKey("Save_PosX");
        PlayerPrefs.DeleteKey("Save_PosY");
        PlayerPrefs.DeleteKey("Save_PosZ");
        PlayerPrefs.DeleteKey("Save_RotY");
        PlayerPrefs.DeleteKey("Save_HasPosition");
        PlayerPrefs.Save();
        Debug.Log("[GameManager] Saved game deleted.");
    }

    public void ResetState()
    {
        RDP           = 0;
        HullTier      = 1;
        EngineTier    = 1;
        SonarTier     = 1;
        ScannerTier   = 1;
        LightTier     = 1;
        UtilitiesTier = 1;
        ClamShells    = 0;
        _allDiscoveredSpecies.Clear();
        _discoveredByZone.Clear();
        UIManager.Instance?.RefreshHUD();
    }
}