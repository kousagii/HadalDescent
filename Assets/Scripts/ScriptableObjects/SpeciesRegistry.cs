using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central registry holding references to every SpeciesData asset.
/// BestiaryManager, SpeciesSpawner, and ScannerSystem all read from this.
///
/// Create via: Right-click in Project ? Create ? HadalDescent ? SpeciesRegistry
/// Setup: Drag all SpeciesData .asset files into the allSpecies list.
/// </summary>
[CreateAssetMenu(fileName = "SpeciesRegistry", menuName = "HadalDescent/SpeciesRegistry")]
public class SpeciesRegistry : ScriptableObject
{
    [Tooltip("Drag every SpeciesData asset here (mobile species AND stationary corals/sponges/bivalves).")]
    public List<SpeciesData> allSpecies = new List<SpeciesData>();

    public List<SpeciesData> GetSpeciesForZone(int zoneIndex) =>
        allSpecies.Where(s => s != null && s.zoneIndex == zoneIndex).ToList();

    public List<SpeciesData> GetMobileSpeciesForZone(int zoneIndex) =>
        allSpecies.Where(s => s != null && s.zoneIndex == zoneIndex && !s.isStationary).ToList();

    public List<SpeciesData> GetStationaryForZone(int zoneIndex) =>
        allSpecies.Where(s => s != null && s.zoneIndex == zoneIndex && s.isStationary).ToList();

    public SpeciesData FindById(string id) =>
        allSpecies.FirstOrDefault(s => s != null && s.speciesId == id);

    public int TotalCount => allSpecies.Count(s => s != null);

#if UNITY_EDITOR
    [ContextMenu("Validate — Check for duplicate IDs")]
    private void ValidateIds()
    {
        var ids        = allSpecies.Where(s => s != null).Select(s => s.speciesId).ToList();
        int empties    = ids.Count(id => string.IsNullOrEmpty(id));
        var duplicates = ids.GroupBy(x => x).Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                           .Select(g => g.Key).ToList();
        if (empties > 0)
            UnityEngine.Debug.LogError($"[SpeciesRegistry] {empties} entries have an empty speciesId!");
        foreach (var dup in duplicates)
            UnityEngine.Debug.LogError($"[SpeciesRegistry] Duplicate speciesId: '{dup}'");
        if (empties == 0 && duplicates.Count == 0)
            UnityEngine.Debug.Log($"[SpeciesRegistry] All {ids.Count} entries are valid.");
    }

    [ContextMenu("Log — Species count per zone")]
    private void LogZoneCounts()
    {
        for (int z = 0; z < ZoneConfig.ZoneCount; z++)
        {
            int count = allSpecies.Count(s => s != null && s.zoneIndex == z);
            UnityEngine.Debug.Log($"[SpeciesRegistry] Zone {z} ({ZoneConfig.Zones[z].zoneName}): {count} entries");
        }
    }
#endif
}
