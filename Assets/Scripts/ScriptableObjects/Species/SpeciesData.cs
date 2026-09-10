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
    [Tooltip("Short habitat description — shown on cards and detail view.")]
    public string habitat;

    [Tooltip("Formatted depth string displayed on HUD & Bestiary (e.g. '0–15 m', '1,960–4,700 m').")]
    public string depthRangeText;

    [Tooltip("Exploration clue shown on the undiscovered Bestiary card.")]
    [TextArea(2, 4)]
    public string explorationHint;

    [Tooltip("Shallower zones where this species may also spawn AFTER being discovered in its primary deepest zone.")]
    public int[] secondaryZoneIndices;

    [TextArea(2, 4)]
    [Tooltip("Physical traits, size, appearance, and notable adaptations.")]
    public string characteristics;

    [TextArea(2, 4)]
    [Tooltip("Role this species plays in its ecosystem (predator, filter feeder, symbiont, etc.).")]
    public string ecologicalRole;

    [TextArea(2, 4)]
    [Tooltip("Interesting biological fact displayed with gold star.")]
    public string interestingFact;

    // -----------------------------------------------------------------------
    // Gameplay
    // -----------------------------------------------------------------------

    [Header("Gameplay")]
    [Tooltip("RDP awarded on first successful scan.")]
    public int rdpReward = 80;

    [Tooltip("Minigame difficulty override. 0 = Auto (uses zone index). 1=Easy, 2=Medium, 3=Hard, 4=Very Hard, 5=Extreme.")]
    [Range(0, 5)] public int scanDifficulty = 0;

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
    // Visual Assets
    // -----------------------------------------------------------------------

    [Header("3D Model Prefab & Visuals")]
    [Tooltip("3D model prefab rendered on the card thumbnail.")]
    public GameObject modelPrefab;

    [Tooltip("Model facing yaw offset in degrees (e.g. 180 if 3D model was exported facing backward in Blender).")]
    public float modelYawOffset = 0f;

    [Tooltip("Scale multiplier for the 3D model in preview thumbnails.")]
    public float previewScaleMultiplier = 1.0f;

    [Header("Placeholder Visuals (until 3D model is ready)")]
    public PlaceholderShape placeholderShape = PlaceholderShape.Sphere;
    public Color            placeholderColor  = Color.cyan;
    public Vector3          placeholderScale  = Vector3.one;

    [Header("Real Biological Photos (Shown in Detail Modal)")]
    [Tooltip("Actual biological/real-life photograph of the species — shown in the Detail Modal.")]
    public Sprite photo;

    [Tooltip("Full colour illustration or secondary photo.")]
    public Sprite fullImage;

    [Tooltip("Dark silhouette sprite (optional 2D fallback).")]
    public Sprite silhouette;
}

// ==========================================================================
// Enums
// ==========================================================================

public enum TaxonomicClass
{
    Actinopterygii,   // Bony fish (clownfish, tuna, lanternfish …)
    Chondrichthyes,   // Sharks, rays
    Mammalia,         // Dolphins, whales, dugongs
    Reptilia,         // Sea turtles, sea kraits
    Cephalopoda,      // Squid, octopus, navigation
    Malacostraca,     // Crabs, lobsters, isopods, amphipods
    Echinoidea,       // Sea urchins, heart urchins
    Asteroidea,       // Sea stars / starfish
    Scyphozoa,        // True jellyfish
    Hydrozoa,         // Siphonophores, bluebottles
    Gastropoda,       // Sea hare, glassy nautilus
    Bivalvia,         // Clams, mussels                 [Stationary]
    Anthozoa,         // Corals, anemones, sea pens     [Stationary + Scannable]
    Demospongiae,     // Sponges                        [Stationary + Scannable]
    Holothuroidea,    // Sea cucumbers                  [Stationary / Benthic]
    Crinoidea,        // Sea lilies, feather stars      [Stationary]
    Pycnogonida       // Sea spiders                    [Benthic]
}

public enum PlaceholderShape { Sphere, Capsule, Cube, Cylinder }

public enum BiomeBand
{
    OpenWater,   // Perlin 0.0 – softThreshold       (pelagic, sandy bottom, mud plain)
    Soft,        // softThreshold – hardThreshold     (seagrass, sediment, organic debris)
    Hard,        // hardThreshold – rockThreshold     (coral reef, sponge garden, basalt field)
    Rock,        // rockThreshold – specialThreshold  (cliffs, boulders, trench walls)
    Special,     // specialThreshold – 1.0            (hydrothermal vents, cold seeps, whale falls)
}