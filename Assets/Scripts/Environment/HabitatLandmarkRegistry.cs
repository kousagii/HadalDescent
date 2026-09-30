using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of all habitat landmark data assets, organized by zone.
/// Parallels SpeciesRegistry but for environmental/habitat survey POIs.
///
/// Create one via Assets > Create > Hadal Descent > Habitat Landmark Registry.
/// Place it at Resources/HabitatLandmarkRegistry so BestiaryManager can auto-load it.
/// </summary>
[CreateAssetMenu(fileName = "HabitatLandmarkRegistry", menuName = "Hadal Descent/Habitat Landmark Registry")]
public class HabitatLandmarkRegistry : ScriptableObject
{
    [Header("All Habitat Landmarks (ordered by zone)")]
    [Tooltip("Add all HabitatLandmarkData assets here. They will be grouped by zoneIndex.")]
    public List<HabitatLandmarkData> allLandmarks = new List<HabitatLandmarkData>();

    /// <summary>
    /// Returns all landmarks belonging to the given zone index.
    /// </summary>
    public List<HabitatLandmarkData> GetLandmarksForZone(int zoneIndex)
    {
        var result = new List<HabitatLandmarkData>();
        foreach (var lm in allLandmarks)
        {
            if (lm != null && lm.zoneIndex == zoneIndex)
                result.Add(lm);
        }
        return result;
    }

    /// <summary>
    /// Total count of registered habitat landmarks across all zones.
    /// </summary>
    public int TotalCount => allLandmarks != null ? allLandmarks.Count : 0;
}
