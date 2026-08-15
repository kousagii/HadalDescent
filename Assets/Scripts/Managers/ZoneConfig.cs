using UnityEngine;

/// <summary>
/// Single source of truth for every ocean zone's:
///   - Display depth range (what the HUD shows the player)
///   - Playable scene dimensions (horizontal X/Z and vertical Y in Unity units)
///   - Scene name for loading
///   - Visual identity (fog colour, ambient light)
///
/// 1 Unity unit = 1 metre in this project.
/// Horizontal sizes match the design doc. Vertical depth is scaled so that
/// the sub takes a meaningful amount of time to cross each zone top-to-bottom.
/// </summary>
[System.Serializable]
public class ZoneDefinition
{
    [Header("Identity")]
    public string zoneName;       // "Sunlight Zone"
    public string sceneName;      // exact scene asset name in Build Settings

    [Header("Display Depth (metres shown on HUD)")]
    public float displayDepthMin; // depth shown at zone entrance
    public float displayDepthMax; // depth shown at zone exit

    [Header("Playable Dimensions (Unity units = metres)")]
    public float playableWidth;   // X extent  (half on each side = width/2)
    public float playableLength;  // Z extent  (half on each side = length/2)
    public float playableDepth;   // Y extent  (top Y = 0, bottom Y = -playableDepth)

    [Header("Atmosphere")]
    public Color fogColor;
    public float fogDensity;
    public Color ambientLight;

    [Header("Gameplay")]
    public int    requiredHullTier;   // minimum hull upgrade to enter
    public int    totalSpeciesCount;  // how many species live in this zone
    [Range(0f,1f)]
    public float  unlockThreshold = 0.5f; // fraction of species needed to unlock next zone

    [Header("PCG — Perlin Noise Terrain")]
    [Tooltip("Perlin sampling frequency. Low = large smooth features; high = small jagged features.")]
    public float pcgFrequency   = 0.05f;
    [Tooltip("Maximum Y displacement of terrain objects from the seabed baseline.")]
    public float pcgAmplitude   = 8f;

    // Biome band Perlin thresholds — value below softBiomeThreshold = OpenWater, etc.
    [Range(0f, 1f)] public float softBiomeThreshold    = 0.30f;
    [Range(0f, 1f)] public float hardBiomeThreshold    = 0.52f;
    [Range(0f, 1f)] public float rockBiomeThreshold    = 0.75f;
    [Range(0f, 1f)] public float specialBiomeThreshold = 0.92f;
}

/// <summary>
/// Static registry — access zone data anywhere via ZoneConfig.Zones[index].
/// </summary>
public static class ZoneConfig
{
    /// <summary>
    /// Zone index: 0 = Sunlight, 1 = Twilight, 2 = Midnight, 3 = Abyss, 4 = Hadal.
    /// </summary>
    public static readonly ZoneDefinition[] Zones = new ZoneDefinition[]
    {
        // ── Zone 0: Sunlight ───────────────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Sunlight Zone",
            sceneName        = "SunlightZone",

            displayDepthMin  = 0f,
            displayDepthMax  = 200f,

            playableWidth    = 300f,
            playableLength   = 300f,
            playableDepth    = 150f,   // vertical travel = 150 Unity units

            fogColor         = new Color(0.40f, 0.75f, 0.90f, 1f),   // bright sea-blue
            fogDensity       = 0.005f,
            ambientLight     = new Color(0.90f, 0.95f, 1.00f, 1f),

            requiredHullTier = 1,
            totalSpeciesCount = 10,
            unlockThreshold  = 0.5f,

            // PCG — gentle sandy reef terrain
            pcgFrequency          = 0.040f,
            pcgAmplitude          = 5f,
            softBiomeThreshold    = 0.28f,
            hardBiomeThreshold    = 0.50f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.92f,
        },

        // ── Zone 1: Twilight ───────────────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Twilight Zone",
            sceneName        = "TwilightZone",

            displayDepthMin  = 200f,
            displayDepthMax  = 1000f,

            playableWidth    = 350f,
            playableLength   = 350f,
            playableDepth    = 175f,

            fogColor         = new Color(0.08f, 0.18f, 0.32f, 1f),   // dark teal
            fogDensity       = 0.010f,
            ambientLight     = new Color(0.20f, 0.28f, 0.45f, 1f),

            requiredHullTier = 2,
            totalSpeciesCount = 10,
            unlockThreshold  = 0.5f,

            // PCG — continental shelf, moderate features
            pcgFrequency          = 0.050f,
            pcgAmplitude          = 10f,
            softBiomeThreshold    = 0.30f,
            hardBiomeThreshold    = 0.54f,
            rockBiomeThreshold    = 0.76f,
            specialBiomeThreshold = 0.93f,
        },

        // ── Zone 2: Midnight ──────────────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Midnight Zone",
            sceneName        = "MidnightZone",

            displayDepthMin  = 1000f,
            displayDepthMax  = 4000f,

            playableWidth    = 450f,
            playableLength   = 450f,
            playableDepth    = 200f,

            fogColor         = new Color(0.02f, 0.03f, 0.07f, 1f),   // near black
            fogDensity       = 0.020f,
            ambientLight     = new Color(0.04f, 0.05f, 0.10f, 1f),

            requiredHullTier = 3,
            totalSpeciesCount = 10,
            unlockThreshold  = 0.5f,

            // PCG — basalt fields, vent structures
            pcgFrequency          = 0.060f,
            pcgAmplitude          = 16f,
            softBiomeThreshold    = 0.25f,
            hardBiomeThreshold    = 0.50f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.90f,
        },

        // ── Zone 3: Abyss ─────────────────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Abyss Zone",
            sceneName        = "AbyssZone",

            displayDepthMin  = 4000f,
            displayDepthMax  = 6000f,

            playableWidth    = 450f,
            playableLength   = 450f,
            playableDepth    = 200f,

            fogColor         = new Color(0.05f, 0.02f, 0.10f, 1f),   // dark purple
            fogDensity       = 0.030f,
            ambientLight     = new Color(0.03f, 0.02f, 0.06f, 1f),

            requiredHullTier = 4,
            totalSpeciesCount = 10,
            unlockThreshold  = 0.5f,

            // PCG — sparse abyssal mud plain with cold seeps
            pcgFrequency          = 0.065f,
            pcgAmplitude          = 20f,
            softBiomeThreshold    = 0.22f,
            hardBiomeThreshold    = 0.46f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.88f,
        },

        // ── Zone 4: Hadal ─────────────────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Hadal Zone",
            sceneName        = "HadalZone",

            displayDepthMin  = 6000f,
            displayDepthMax  = 8000f,

            playableWidth    = 500f,
            playableLength   = 500f,
            playableDepth    = 200f,

            fogColor         = new Color(0.00f, 0.00f, 0.00f, 1f),   // pitch black
            fogDensity       = 0.040f,
            ambientLight     = new Color(0.01f, 0.01f, 0.02f, 1f),

            requiredHullTier = 5,
            totalSpeciesCount = 10,
            unlockThreshold  = 0.5f,

            // PCG — jagged hadal trench walls and spires
            pcgFrequency          = 0.080f,
            pcgAmplitude          = 28f,
            softBiomeThreshold    = 0.20f,
            hardBiomeThreshold    = 0.44f,
            rockBiomeThreshold    = 0.70f,
            specialBiomeThreshold = 0.86f,
        },
    };

    public const int ZoneCount = 5;

    /// <summary>Returns true if <paramref name="index"/> is a valid zone index.</summary>
    public static bool IsValidZone(int index) => index >= 0 && index < ZoneCount;
}
