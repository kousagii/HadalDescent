using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that stores all persistent player state.
/// Attach to a "GameManager" GameObject in your first scene and it will
/// survive across all scene loads.
///
/// For the prototype, data is kept in memory only. Hook up SaveManager later.
/// </summary>
public class GameManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    public static GameManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Player stats
    // -----------------------------------------------------------------------

    [Header("Currency")]
    public int RDP = 0;

    [Header("Submarine Upgrades")]
    [Range(1, 5)] public int HullTier    = 1;
    [Range(1, 5)] public int EngineTier  = 1;
    [Range(1, 5)] public int SonarTier   = 1;
    [Range(1, 5)] public int ScannerTier = 1;
    [Range(1, 5)] public int LightTier   = 1;

    [Header("Collectibles")]
    public int ClamShells = 0;

    // -----------------------------------------------------------------------
    // Species discovery — tracks by zone then by species ID
    // -----------------------------------------------------------------------

    // Key: zoneIndex (0-4), Value: set of discovered species IDs in that zone
    private readonly Dictionary<int, HashSet<string>> _discoveredByZone
        = new Dictionary<int, HashSet<string>>();

    // -----------------------------------------------------------------------
    // PCG seeds — one per zone, generated on first visit and then fixed
    // -----------------------------------------------------------------------

    // Key: zoneIndex, Value: integer seed passed to TerrainGenerator
    private readonly Dictionary<int, int> _zonePcgSeeds = new Dictionary<int, int>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------------------------------
    // RDP
    // -----------------------------------------------------------------------

    /// <summary>Add RDP and notify the HUD.</summary>
    public void AddRDP(int amount)
    {
        RDP += Mathf.Max(0, amount);
        UIManager.Instance?.RefreshHUD();
        Debug.Log($"[GameManager] +{amount} RDP → Total: {RDP}");
    }

    /// <summary>
    /// Spend RDP. Returns true if successful, false if insufficient funds.
    /// </summary>
    public bool SpendRDP(int amount)
    {
        if (RDP < amount) return false;
        RDP -= amount;
        UIManager.Instance?.RefreshHUD();
        return true;
    }

    // -----------------------------------------------------------------------
    // Species discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mark a species as discovered in a zone.
    /// Returns true if this is a new discovery (was not already recorded).
    /// </summary>
    public bool DiscoverSpecies(int zoneIndex, string speciesId)
    {
        if (!_discoveredByZone.ContainsKey(zoneIndex))
            _discoveredByZone[zoneIndex] = new HashSet<string>();

        bool isNew = _discoveredByZone[zoneIndex].Add(speciesId);
        if (isNew)
            Debug.Log($"[GameManager] New species discovered! Zone {zoneIndex} | ID: {speciesId}");
        return isNew;
    }

    /// <summary>Returns true if the species has already been discovered.</summary>
    public bool IsDiscovered(int zoneIndex, string speciesId)
    {
        return _discoveredByZone.TryGetValue(zoneIndex, out var set) && set.Contains(speciesId);
    }

    /// <summary>How many distinct species have been discovered in this zone.</summary>
    public int GetDiscoveredCountInZone(int zoneIndex)
    {
        return _discoveredByZone.TryGetValue(zoneIndex, out var set) ? set.Count : 0;
    }

    /// <summary>
    /// Returns 0.0–1.0 progress for zone unlock (discovered / totalSpecies).
    /// </summary>
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

    /// <summary>
    /// Returns the Perlin seed for <paramref name="zoneIndex"/>.
    /// Creates and stores a random seed the first time a zone is visited so
    /// re-entering the zone generates the exact same terrain layout.
    /// </summary>
    public int GetOrCreatePcgSeed(int zoneIndex)
    {
        if (!_zonePcgSeeds.ContainsKey(zoneIndex))
            _zonePcgSeeds[zoneIndex] = Random.Range(0, 99999);
        return _zonePcgSeeds[zoneIndex];
    }

    // -----------------------------------------------------------------------
    // Submarine upgrades
    // -----------------------------------------------------------------------

    public bool UpgradeHull()
    {
        if (HullTier >= 5) return false;
        HullTier++;
        Debug.Log($"[GameManager] Hull upgraded → Tier {HullTier}");
        return true;
    }

    public bool UpgradeEngine()
    {
        if (EngineTier >= 5) return false;
        EngineTier++;
        return true;
    }

    public bool UpgradeSonar()
    {
        if (SonarTier >= 5) return false;
        SonarTier++;
        return true;
    }

    public bool UpgradeScanner()
    {
        if (ScannerTier >= 5) return false;
        ScannerTier++;
        return true;
    }

    public bool UpgradeLight()
    {
        if (LightTier >= 5) return false;
        LightTier++;
        return true;
    }

    // -----------------------------------------------------------------------
    // Clam shells
    // -----------------------------------------------------------------------

    public void CollectClamShell()
    {
        ClamShells++;
        Debug.Log($"[GameManager] Clam shell collected! Total: {ClamShells}");
    }
}
