using UnityEngine;

/// <summary>
/// Defines a single bestiary entry — works for mobile species (fish, sharks, jellyfish)
/// AND stationary scannable objects (corals, sponges, bivalves).
///
/// Create via: Right-click in Project → Create → HadalDescent → SpeciesData
///
/// Placement rules:
///   preferredBiome + minDepthFraction/maxDepthFraction drive where SpeciesSpawner puts this entry.
///   isStationary = true  → ScanTarget component attached (no AI).
///   isStationary = false → SpeciesAI + ContextSteering attached.
///
/// Placeholder workflow:
///   Leave modelPrefab null while Blender models are being made.
///   Set placeholderShape + placeholderColor so the entry is visible in-game.
///   Once the model is ready, drag the prefab into modelPrefab — placeholder is then ignored.
/// </summary>
[CreateAssetMenu(fileName = "New Species", menuName = "HadalDescent/SpeciesData")]
public class SpeciesData : ScriptableObject
{
    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    [Header("Identity")]
    [Tooltip("Unique key used by GameManager to track discovery. MUST be unique across all entries.")]
    public string speciesId;
    public string commonName;
    public string scientificName;

    // -----------------------------------------------------------------------
    // Classification
    // -----------------------------------------------------------------------

    [Header("Classification")]
    public TaxonomicClass taxonomicClass;

    [Tooltip("Zone index: 0=Sunlight, 1=Twilight, 2=Midnight, 3=Abyss, 4=Hadal")]
    [Range(0, 4)] public int zoneIndex;

    // -----------------------------------------------------------------------
    // Bestiary content
    // -----------------------------------------------------------------------

    [Header("Bestiary Info")]
    [Tooltip("Short habitat description — shown even when undiscovered.")]
    public string habitat;

    [TextArea(2, 4)]
    [Tooltip("Physical traits, size, appearance, and notable adaptations.")]
    public string characteristics;

    [TextArea(2, 4)]
    [Tooltip("Role this species plays in its ecosystem (predator, filter feeder, symbiont, etc.).")]
    public string ecologicalRole;

    [TextArea(2, 5)]
    public string description;

    [TextArea(2, 4)]
    public string interestingFact;

    // -----------------------------------------------------------------------
    // Gameplay
    // -----------------------------------------------------------------------

    [Header("Gameplay")]
    [Tooltip("RDP awarded on first successful scan.")]
    public int rdpReward = 80;

    [Tooltip("True for corals, sponges, bivalves — no AI, just collider + ScanTarget.")]
    public bool isStationary;

    [Tooltip("If true, approaching with the scanner before lock-on causes this species to flee.")]
    public bool isShy;

    // -----------------------------------------------------------------------
    // Ecological placement (used by SpeciesSpawner)
    // -----------------------------------------------------------------------

    [Header("Ecological Placement")]
    [Tooltip("Which Perlin biome band this species prefers.")]
    public BiomeBand preferredBiome = BiomeBand.OpenWater;

    [Range(0f, 1f)]
    [Tooltip("0 = zone top (entrance), 1 = zone floor. Start of preferred depth range.")]
    public float minDepthFraction = 0f;

    [Range(0f, 1f)]
    [Tooltip("End of preferred depth range.")]
    public float maxDepthFraction = 1f;

    [Tooltip("How many individuals of this species to spawn per zone load.")]
    [Range(1, 10)] public int instanceCount = 3;

    // -----------------------------------------------------------------------
    // AI parameters (ignored when isStationary = true)
    // -----------------------------------------------------------------------

    [Header("AI Parameters (mobile species only)")]
    public float wanderRadius    = 15f;
    public float moveSpeed       = 2f;
    public float fleeSpeed       = 5f;
    [Tooltip("Distance at which a shy species detects the submarine and starts fleeing.")]
    public float fleeRange       = 10f;
    [Tooltip("Distance from spawn before a fleeing creature turns back.")]
    public float returnThreshold = 40f;

    // -----------------------------------------------------------------------
    // Placeholder visuals (used when modelPrefab is null)
    // -----------------------------------------------------------------------

    [Header("Placeholder Visuals (until 3D model is ready)")]
    public PlaceholderShape placeholderShape = PlaceholderShape.Sphere;
    public Color            placeholderColor  = Color.cyan;
    public Vector3          placeholderScale  = Vector3.one;

    // -----------------------------------------------------------------------
    // Final assets (assign when Blender models are ready)
    // -----------------------------------------------------------------------

    [Header("Final Assets (leave null for prototype)")]
    [Tooltip("3D model prefab. When assigned, placeholder visuals are ignored.")]
    public GameObject modelPrefab;
    [Tooltip("Actual photograph of the species — shown in the Bestiary detail card.")]
    public Sprite     photo;
    [Tooltip("Dark silhouette sprite shown for undiscovered entries.")]
    public Sprite     silhouette;
    [Tooltip("Full colour illustration shown in completed bestiary entries.")]
    public Sprite     fullImage;
}

// ==========================================================================
// Enums (shared across the Species system — kept here so SpeciesData.cs
// is the single file to import when referencing any species enum)
// ==========================================================================

/// <summary>Taxonomic class — drives ecological placement rules in SpeciesSpawner.</summary>
public enum TaxonomicClass
{
    Actinopterygii,   // Bony fish (clownfish, tuna, lanternfish …)
    Chondrichthyes,   // Sharks, rays
    Mammalia,         // Dolphins, whales, dugongs
    Reptilia,         // Sea turtles, sea kraits
    Cephalopoda,      // Squid, octopus, nautilus
    Malacostraca,     // Crabs, lobsters, isopods, amphipods
    Echinoidea,       // Sea urchins, heart urchins
    Asteroidea,       // Sea stars / starfish
    Scyphozoa,        // True jellyfish
    Hydrozoa,         // Siphonophores, bluebottles
    Gastropoda,       // Sea hare, glassy nautilus
    Bivalvia,         // Clams, mussels                 [Stationary]
    Anthozoa,         // Corals, anemones, sea pens     [Stationary + Scannable]
    Demospongiae,     // Sponges                        [Stationary + Scannable]
}

/// <summary>Primitive shape used when modelPrefab is null.</summary>
public enum PlaceholderShape { Sphere, Capsule, Cube, Cylinder }

/// <summary>
/// Perlin biome band. Thresholds are defined per-zone in ZoneConfig.
/// SpeciesSpawner queries TerrainGenerator.GetBiomeAt() to match a candidate
/// position's biome against the species' preferredBiome before spawning.
/// </summary>
public enum BiomeBand
{
    OpenWater,   // Perlin 0.0 – softThreshold       (pelagic, sandy bottom, mud plain)
    Soft,        // softThreshold – hardThreshold     (seagrass, sediment, organic debris)
    Hard,        // hardThreshold – rockThreshold     (coral reef, sponge garden, basalt field)
    Rock,        // rockThreshold – specialThreshold  (cliffs, boulders, trench walls)
    Special,     // specialThreshold – 1.0            (hydrothermal vents, cold seeps, whale falls)
}
