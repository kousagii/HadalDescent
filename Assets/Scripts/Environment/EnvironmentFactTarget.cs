using UnityEngine;

/// <summary>
/// Attach to any environmental prop, landmark, reef formation, vent, or rock outcrop
/// to make it an interactive educational target.
///
/// When the submarine reticle aims at this object within interact range:
///   - Reticle shows "INTERACT"
///   - Pressing INTERACT opens the Environment Fact Card (FactCardUI)
///   - Awards survey RDP on first survey
///   - Automatically adds a SonarTrackable (Environment type) if missing
/// </summary>
[DisallowMultipleComponent]
public class EnvironmentFactTarget : MonoBehaviour
{
    [Header("Data Asset (Optional - overrides manual fields below)")]
    [Tooltip("Assign a HabitatLandmarkData asset to auto-populate all fields. If set, manual fields below are ignored.")]
    [SerializeField] private HabitatLandmarkData dataAsset;

    [Header("Identity & Tracking")]
    [Tooltip("Unique ID for saving survey state (e.g. 'fact_coral_atoll_01').")]
    [SerializeField] private string factId = "fact_habitat_01";

    [Header("Display Information")]
    [Tooltip("Habitat / Formation name (e.g. 'Tropical Coral Reef Matrix').")]
    [SerializeField] private string habitatName = "Tropical Coral Reef Matrix";

    [Tooltip("Category (e.g. 'Biogenic Marine Habitat', 'Geothermal Vent', 'Oceanic Trench').")]
    [SerializeField] private string category = "Biogenic Marine Habitat";

    [Tooltip("Zone index: 0=Sunlight, 1=Twilight, 2=Midnight, 3=Abyss, 4=Hadal")]
    [Range(0, 4)]
    [SerializeField] private int zoneIndex = 0;

    [Tooltip("Depth range text (e.g. '10–30 m').")]
    [SerializeField] private string depthRangeText = "10–30 m";

    [Header("Educational Content")]
    [TextArea(2, 4)]
    [Tooltip("Detailed description of this marine habitat or geological formation.")]
    [SerializeField] private string habitatDescription = "Vibrant calcium carbonate frameworks built over thousands of years by colonial polyps.";

    [TextArea(2, 4)]
    [Tooltip("Ecological significance and role in the marine ecosystem.")]
    [SerializeField] private string ecologicalSignificance = "Supports over 25% of all marine life, providing vital nursery grounds and shoreline wave protection.";

    [TextArea(2, 4)]
    [Tooltip("Fascinating scientific fact displayed on the card.")]
    [SerializeField] private string interestingFact = "Corals live in a symbiotic mutualism with zooxanthellae algae, which provide up to 90% of the coral's energy through photosynthesis.";

    [Header("Rewards & Visuals")]
    [Tooltip("Research Data Points awarded upon first survey.")]
    [SerializeField] private int rdpReward = 30;

    [Tooltip("Optional photo or illustration of this habitat type.")]
    [SerializeField] private Sprite habitatPhoto;

    // -----------------------------------------------------------------------
    // Properties (data asset takes priority when assigned)
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    public string FactId                 => (dataAsset != null && !string.IsNullOrEmpty(dataAsset.factId)) ? dataAsset.factId : (string.IsNullOrEmpty(factId) ? gameObject.name : factId);
    public string HabitatName            => dataAsset != null ? dataAsset.habitatName : habitatName;
    public string Category               => dataAsset != null ? dataAsset.category : category;
    public int    ZoneIndex              => dataAsset != null ? dataAsset.zoneIndex : zoneIndex;
    public string DepthRangeText         => dataAsset != null ? dataAsset.depthRangeText : depthRangeText;
    public string HabitatDescription     => dataAsset != null ? dataAsset.habitatDescription : habitatDescription;
    public string EcologicalSignificance => dataAsset != null ? dataAsset.ecologicalSignificance : ecologicalSignificance;
    public string InterestingFact        => dataAsset != null ? dataAsset.interestingFact : interestingFact;
    public int    RdpReward              => dataAsset != null ? dataAsset.rdpReward : rdpReward;
    public Sprite HabitatPhoto           => dataAsset != null ? dataAsset.habitatPhoto : habitatPhoto;
    public BiomeBand TargetBiome         => dataAsset != null ? dataAsset.targetBiome : _targetBiome;
    public string CitationSource         => dataAsset != null ? dataAsset.citationSource : _citationSource;

    [SerializeField] private BiomeBand _targetBiome = BiomeBand.Hard;
    [SerializeField] private string _citationSource = "NOAA Coral Reef Conservation Program / Smithsonian Ocean Portal";

    public bool IsSurveyed
    {
        get
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.IsDiscovered(FactId)) return true;
                if (FactId == "fact_sun_coral_reef" && GameManager.Instance.IsDiscovered("fact_sun_barrier_reef")) return true;
                if (FactId == "fact_sun_seagrass_bed" && GameManager.Instance.IsDiscovered("fact_sun_kelp_canopy")) return true;
                return false;
            }
            return _isSurveyedLocal;
        }
        set
        {
            _isSurveyedLocal = value;
            if (value && GameManager.Instance != null)
                GameManager.Instance.DiscoverSpecies(zoneIndex, FactId);

            var trackable = GetComponent<SonarTrackable>();
            if (trackable != null) trackable.IsDiscovered = value;
        }
    }
    private bool _isSurveyedLocal = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle & Editor Helpers
    // -----------------------------------------------------------------------

    private void Reset()
    {
        string cleanName = gameObject.name.Replace(" ", "_").Replace("(", "").Replace(")", "").ToLower();
        factId = "fact_" + cleanName;
        habitatName = gameObject.name;
    }

    private void Start()
    {
        // Ensure collider exists so scanner raycast can detect it
        if (GetComponent<Collider>() == null && GetComponentInChildren<Collider>() == null)
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                Bounds b = rend.bounds;
                box.center = transform.InverseTransformPoint(b.center);
                Vector3 size = transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            else
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one * 2f;
            }
        }

        // Register on sonar as Environment landmark
        var trackable = GetComponent<SonarTrackable>();
        if (trackable == null)
        {
            trackable = gameObject.AddComponent<SonarTrackable>();
        }
        trackable.Initialize(SonarTrackable.SonarTargetType.Environment, HabitatName, IsSurveyed);
    }
}
