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
}