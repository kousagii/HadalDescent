using UnityEngine;

/// <summary>
/// Single source of truth for every ocean zone's:
///   - Display depth range (what the HUD shows the player)
///   - Playable scene dimensions (horizontal X/Z and vertical Y in Unity units)
///   - Atmospheric ocean fog (Linear fog start/end distances and color)
///   - Scene name for loading
///   - Visual identity (fog colour, ambient light)
///
/// 1 Unity unit = 1 metre in this project.
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
    public float fogStartDistance = 25f;
    public float fogEndDistance   = 140f;
    public float fogDensity       = 0.008f;
    public Color ambientLight;

    [Header("Gameplay")]
    public int    requiredHullTier;   // minimum hull upgrade to enter
    public int    totalSpeciesCount;  // how many species live in this zone
    [Range(0f,1f)]
    public float  unlockThreshold = 0.5f; // fraction of species needed to unlock next zone

    [Header("PCG — Perlin Noise Terrain")]
    [Tooltip("Perlin sampling frequency. Low = large smooth rolling features.")]
    public float pcgFrequency   = 0.004f;
    [Tooltip("Maximum Y displacement of terrain from the seabed baseline.")]
    public float pcgAmplitude   = 175f;

    // Biome band Perlin thresholds — value below softBiomeThreshold = OpenWater, etc.
    [Range(0f, 1f)] public float softBiomeThreshold    = 0.28f;
    [Range(0f, 1f)] public float hardBiomeThreshold    = 0.50f;
    [Range(0f, 1f)] public float rockBiomeThreshold    = 0.74f;
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
        // ── Zone 0: Sunlight (0–200m) ──────────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Sunlight Zone",
            sceneName        = "SunlightZone",

            displayDepthMin  = 0f,
            displayDepthMax  = 200f,

            playableWidth    = 600f,   // 600x600m vast exploratory ocean
            playableLength   = 600f,
            playableDepth    = 200f,   // 0m surface down to -200m

            fogColor         = new Color(0.18f, 0.52f, 0.72f, 1f),   // rich tropical sea blue
            fogStartDistance = 25f,
            fogEndDistance   = 140f,
            fogDensity       = 0.008f,
            ambientLight     = new Color(0.25f, 0.40f, 0.52f, 1f),

            requiredHullTier = 1,
            totalSpeciesCount = 15,
            unlockThreshold  = 0.5f,

            // PCG — broad rolling dunes & shallow coral reef atolls (reach 15m-25m depth)
            pcgFrequency          = 0.0035f, // wide features (~280m wavelength)
            pcgAmplitude          = 175f,    // base at -200m, highest reef plateaus at -25m
            softBiomeThreshold    = 0.28f,
            hardBiomeThreshold    = 0.50f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.92f,
        },

        // ── Zone 1: Twilight (200–1,000m) ──────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Twilight Zone",
            sceneName        = "TwilightZone",

            displayDepthMin  = 200f,
            displayDepthMax  = 1000f,

            playableWidth    = 700f,
            playableLength   = 700f,
            playableDepth    = 250f,

            fogColor         = new Color(0.04f, 0.12f, 0.22f, 1f),   // deep mesopelagic teal
            fogStartDistance = 15f,
            fogEndDistance   = 110f,
            fogDensity       = 0.012f,
            ambientLight     = new Color(0.15f, 0.22f, 0.38f, 1f),

            requiredHullTier = 2,
            totalSpeciesCount = 17,
            unlockThreshold  = 0.5f,

            // PCG — continental slope, large step ledges
            pcgFrequency          = 0.0045f,
            pcgAmplitude          = 190f,
            softBiomeThreshold    = 0.30f,
            hardBiomeThreshold    = 0.54f,
            rockBiomeThreshold    = 0.76f,
            specialBiomeThreshold = 0.93f,
        },

        // ── Zone 2: Midnight (1,000–4,000m) ────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Midnight Zone",
            sceneName        = "MidnightZone",

            displayDepthMin  = 1000f,
            displayDepthMax  = 4000f,

            playableWidth    = 800f,
            playableLength   = 800f,
            playableDepth    = 300f,

            fogColor         = new Color(0.012f, 0.016f, 0.035f, 1f), // near-black bathyal water
            fogStartDistance = 10f,
            fogEndDistance   = 85f,
            fogDensity       = 0.018f,
            ambientLight     = new Color(0.03f, 0.04f, 0.08f, 1f),

            requiredHullTier = 3,
            totalSpeciesCount = 19,
            unlockThreshold  = 0.5f,

            // PCG — basalt plains & hydrothermal vent mounds
            pcgFrequency          = 0.0055f,
            pcgAmplitude          = 220f,
            softBiomeThreshold    = 0.25f,
            hardBiomeThreshold    = 0.50f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.90f,
        },

        // ── Zone 3: Abyss (4,000–6,000m) ───────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Abyss Zone",
            sceneName        = "AbyssZone",

            displayDepthMin  = 4000f,
            displayDepthMax  = 6000f,

            playableWidth    = 850f,
            playableLength   = 850f,
            playableDepth    = 300f,

            fogColor         = new Color(0.008f, 0.006f, 0.018f, 1f), // abyssal pitch
            fogStartDistance = 8f,
            fogEndDistance   = 75f,
            fogDensity       = 0.022f,
            ambientLight     = new Color(0.02f, 0.015f, 0.04f, 1f),

            requiredHullTier = 4,
            totalSpeciesCount = 15,
            unlockThreshold  = 0.5f,

            // PCG — vast mud plains & cold seep hills
            pcgFrequency          = 0.005f,
            pcgAmplitude          = 210f,
            softBiomeThreshold    = 0.22f,
            hardBiomeThreshold    = 0.46f,
            rockBiomeThreshold    = 0.74f,
            specialBiomeThreshold = 0.88f,
        },

        // ── Zone 4: Hadal (6,000–11,000m+) ─────────────────────────────────
        new ZoneDefinition
        {
            zoneName         = "Hadal Zone",
            sceneName        = "HadalZone",

            displayDepthMin  = 6000f,
            displayDepthMax  = 11000f,

            playableWidth    = 900f,
            playableLength   = 900f,
            playableDepth    = 300f,

            fogColor         = new Color(0.002f, 0.002f, 0.005f, 1f), // absolute darkness
            fogStartDistance = 6f,
            fogEndDistance   = 65f,
            fogDensity       = 0.028f,
            ambientLight     = new Color(0.01f, 0.01f, 0.015f, 1f),

            requiredHullTier = 5,
            totalSpeciesCount = 21,
            unlockThreshold  = 0.5f,

            // PCG — deep trench walls & fault terraces
            pcgFrequency          = 0.0065f,
            pcgAmplitude          = 230f,
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
