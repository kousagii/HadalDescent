using UnityEngine;

/// <summary>
/// Stores educational data for a habitat landmark survey POI.
/// Create instances via Assets > Create > Hadal Descent > Habitat Landmark Data.
///
/// Used by EnvironmentFactTarget to populate its fields from a data asset,
/// and by BestiaryManager to display surveyed habitats in the Bestiary.
/// </summary>
[CreateAssetMenu(fileName = "NewHabitatLandmark", menuName = "Hadal Descent/Habitat Landmark Data")]
public class HabitatLandmarkData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique ID for save state (e.g. 'landmark_barrier_reef_01').")]
    public string factId = "landmark_habitat_01";

    [Tooltip("Display name (e.g. 'Barrier Reef Formation').")]
    public string habitatName = "Barrier Reef Formation";

    [Tooltip("Category (e.g. 'Biogenic Marine Habitat', 'Geothermal Vent', 'Whale Fall').")]
    public string category = "Biogenic Marine Habitat";

    [Tooltip("Zone index: 0=Sunlight, 1=Twilight, 2=Midnight, 3=Abyss, 4=Hadal")]
    [Range(0, 4)]
    public int zoneIndex = 0;

    [Tooltip("Depth range text (e.g. '10–30 m').")]
    public string depthRangeText = "10–30 m";

    [Header("Educational Content")]
    [TextArea(3, 6)]
    public string habitatDescription = "Description of this marine habitat or geological formation.";

    [TextArea(3, 6)]
    public string ecologicalSignificance = "The ecological role and importance of this formation.";

    [TextArea(3, 6)]
    public string interestingFact = "A fascinating scientific fact about this habitat.";

    [Tooltip("Which ecological biome band this landmark belongs to (e.g. Hard for coral reef, Soft for seagrass).")]
    public BiomeBand targetBiome = BiomeBand.Hard;

    [Tooltip("Authoritative scientific source/citations for this fact card.")]
    public string citationSource = "NOAA Coral Reef Conservation Program / Smithsonian Ocean";

    [Header("Rewards & Visuals")]
    [Tooltip("Research Data Points awarded upon first survey.")]
    public int rdpReward = 30;

    [Tooltip("Photo or illustration of this habitat type.")]
    public Sprite habitatPhoto;
}
