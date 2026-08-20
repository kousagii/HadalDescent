using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central registry holding references to every SpeciesData asset.
/// BestiaryManager, SpeciesSpawner, and ScannerSystem all read from this.
///
/// Supports multi-depth species distribution:
///   - Primary encounter occurs in the deepest zone.
///   - Once discovered, the species can also appear in shallower zones.
/// </summary>
[CreateAssetMenu(fileName = "SpeciesRegistry", menuName = "HadalDescent/SpeciesRegistry")]
public class SpeciesRegistry : ScriptableObject
{
    [Tooltip("Drag every SpeciesData asset here (mobile species AND stationary corals/sponges/bivalves).")]
    public List<SpeciesData> allSpecies = new List<SpeciesData>();

    /// <summary>
    /// Returns primary species assigned to this zone (for Bestiary tab categorization).
    /// </summary>
    public List<SpeciesData> GetSpeciesForZone(int zoneIndex) =>
        allSpecies.Where(s => s != null && s.zoneIndex == zoneIndex).ToList();

    /// <summary>
    /// Returns all primary species for this zone, PLUS any multi-depth species that have
    /// already been discovered in deeper zones and can now spawn in this shallower zone.
    /// </summary>
    public List<SpeciesData> GetSpeciesAvailableForZone(int zoneIndex)
    {
        var list = new List<SpeciesData>();
        foreach (var s in allSpecies)
        {
            if (s == null) continue;

            if (s.zoneIndex == zoneIndex)
            {
                list.Add(s);
            }
            else if (s.secondaryZoneIndices != null && System.Array.IndexOf(s.secondaryZoneIndices, zoneIndex) >= 0)
            {
                // Multi-depth species: only spawns in shallower zones AFTER being discovered in its primary zone
                bool isDiscovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(s.zoneIndex, s.speciesId);
                if (isDiscovered)
                {
                    list.Add(s);
                }
            }
        }
        return list;
    }

    public List<SpeciesData> GetMobileSpeciesForZone(int zoneIndex) =>
        GetSpeciesAvailableForZone(zoneIndex).Where(s => !s.isStationary).ToList();

    public List<SpeciesData> GetStationaryForZone(int zoneIndex) =>
        GetSpeciesAvailableForZone(zoneIndex).Where(s => s.isStationary).ToList();

    public SpeciesData FindById(string id) =>
        allSpecies.FirstOrDefault(s => s != null && s.speciesId == id);

    public int TotalCount => allSpecies.Count(s => s != null);

#if UNITY_EDITOR
    [ContextMenu("Validate - Check for duplicate IDs")]
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

    [ContextMenu("Log - Species count per zone")]
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