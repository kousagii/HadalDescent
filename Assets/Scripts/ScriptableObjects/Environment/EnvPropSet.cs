using UnityEngine;

/// <summary>
/// Per-zone environment prop configuration.
/// Create one asset per zone via: Create → HadalDescent → EnvPropSet.
///
/// Holds the seabed material and a list of weighted prop prefabs
/// that EnvPropScatterer uses to populate the ocean floor according to
/// the Hadal Descent Environment Blueprint.
/// </summary>
[CreateAssetMenu(fileName = "NewEnvPropSet", menuName = "HadalDescent/EnvPropSet")]
public class EnvPropSet : ScriptableObject
{
    [Header("Seabed")]
    [Tooltip("Material applied to the generated ocean floor mesh.")]
    public Material seabedMaterial;

    [Header("Props")]
    [Tooltip("How many props to attempt per 100 m². 0.5 = sparse, 1.5 = lush/dense.")]
    [Range(0.1f, 5f)]
    public float propDensity = 1.2f;

    [Tooltip("Global scale multiplier for all props. Multiplies minScale and maxScale (e.g. 4.0 makes small FBX models look like massive ocean boulders and lush coral reefs).")]
    [Range(0.5f, 20f)]
    public float globalScaleMultiplier = 4.0f;

    [Tooltip("List of prefabs that get scattered on the mesh surface.")]
    public PropEntry[] props;

    /// <summary>
    /// One entry in the prop list.
    /// </summary>
    [System.Serializable]
    public class PropEntry
    {
        [Tooltip("Prefab to instantiate (must be a prefab, not raw FBX).")]
        public GameObject prefab;

        [Tooltip("Which ecological biome band this prop belongs to.")]
        public BiomeBand targetBiome = BiomeBand.Hard;

        [Tooltip("Relative spawn weight. Higher = more common.")]
        [Range(0.1f, 10f)]
        public float weight = 1f;

        [Tooltip("Base uniform scale range applied on spawn (will be multiplied by globalScaleMultiplier).")]
        public float minScale = 1.0f;
        public float maxScale = 2.5f;

        [Tooltip("Non-uniform axis scale formula from the Environment Blueprint (e.g. (3.0, 0.4, 3.0) for flat reef ledges, (1.0, 1.2, 1.0) for spires).")]
        public Vector3 scaleMultiplier = Vector3.one;

        [Tooltip("Specific material recipe override for this prop (e.g. Sun-bleached limestone, Granite, Obsidian).")]
        public Material materialOverride;

        [Tooltip("If true, prop rotates to match the slope of the mesh surface.")]
        public bool alignToSurface = true;

        [Tooltip("Degree to which this prop conforms to the surface normal (0 = upright, 1 = full slope tilt).")]
        [Range(0f, 1f)]
        public float surfaceTiltStrength = 0.75f;

        [Tooltip("If true, this prop is tagged as obstacle for ContextSteering avoidance.")]
        public bool isObstacle = true;
    }
}
