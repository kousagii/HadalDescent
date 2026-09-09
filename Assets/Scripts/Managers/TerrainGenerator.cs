using UnityEngine;

/// <summary>
/// Procedurally generates the ocean zone environment.
///
/// Orchestrates four phases:
///   1. OceanFloorMeshGenerator - creates the deformable seabed mesh (fBm noise & reef plateaus)
///   2. EnvPropScatterer        - scatters rock/coral/sponge prefabs on the mesh
///   3. SpeciesSpawner          - spawns marine life onto the generated seabed & reef structures
///   4. DebrisSpawner           - scatters 3D marine debris clusters for environmental cleanup
///
/// Setup:
///   Place TerrainGenerator, OceanFloorMeshGenerator, and EnvPropScatterer
///   on the same GameObject in each zone scene. Assign the EnvPropSet asset.
///   ZoneManager calls Generate(zoneIndex) after scene load.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Environment Props")]
    [Tooltip("Per-zone prop configuration (seabed material + prop prefab list). " +
             "Create via: Create -> HadalDescent -> EnvPropSet.")]
    [SerializeField] private EnvPropSet envPropSet;

    // -----------------------------------------------------------------------
    // Sub-system references (on same GameObject)
    // -----------------------------------------------------------------------

    private OceanFloorMeshGenerator _meshGen;
    private EnvPropScatterer        _propScatterer;

    // -----------------------------------------------------------------------
    // PCG state
    // -----------------------------------------------------------------------

    private int   _zoneIndex;
    private float _zoneW, _zoneL, _zoneDepth;
    private float _seabedY;
    private float _pcgFreq, _pcgAmp, _pcgSeed;
    private float _softT, _hardT, _rockT, _specialT;
    private bool  _hasGenerated = false;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Returns true if the terrain mesh and props have finished generating.</summary>
    public bool HasGenerated => _hasGenerated;

    /// <summary>
    /// Query the ocean floor height at a world (x, z) position.
    /// Returns the Y coordinate of the mesh surface.
    /// Falls back to flat seabedY if mesh hasn't been generated yet.
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        if (_meshGen != null)
            return _meshGen.SampleHeight(worldX, worldZ);
        return _seabedY;
    }

    /// <summary>
    /// Query the surface normal at a world (x, z) position.
    /// Returns Vector3.up if mesh hasn't been generated yet.
    /// </summary>
    public Vector3 SampleNormal(float worldX, float worldZ)
    {
        if (_meshGen != null)
            return _meshGen.SampleNormal(worldX, worldZ);
        return Vector3.up;
    }

    /// <summary>Y coordinate of the zone seabed baseline (world space). Used as fallback.</summary>
    public float SeabedY => _seabedY;

    /// <summary>Random seed used to generate the PCG terrain mesh and biomes.</summary>
    public float PcgSeed => _pcgSeed;

    /// <summary>
    /// Query the BiomeBand at a world (x, z) position.
    /// Used by SpeciesSpawner to validate candidate spawn positions.
    /// </summary>
    public BiomeBand GetBiomeAt(float worldX, float worldZ)
    {
        float v = Mathf.PerlinNoise(
            worldX * _pcgFreq + _pcgSeed,
            worldZ * _pcgFreq + _pcgSeed + 50f);
        return ValueToBiome(v);
    }

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (!_hasGenerated) Generate(0);
    }

    public void Generate(int zoneIndex)
    {
        _hasGenerated = true;
        _zoneIndex = zoneIndex;
        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            Debug.LogWarning($"[TerrainGenerator] Invalid zone index {zoneIndex}");
            return;
        }

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];
        _zoneW     = zone.playableWidth;
        _zoneL     = zone.playableLength;
        _zoneDepth = zone.playableDepth;
        _seabedY   = -_zoneDepth;
        _pcgFreq   = zone.pcgFrequency;
        _pcgAmp    = zone.pcgAmplitude;
        _pcgSeed   = GameManager.Instance != null
                     ? GameManager.Instance.GetOrCreateZoneSeed(zoneIndex)
                     : Random.Range(0, 99999);
        _softT     = zone.softBiomeThreshold;
        _hardT     = zone.hardBiomeThreshold;
        _rockT     = zone.rockBiomeThreshold;
        _specialT  = zone.specialBiomeThreshold;

        // Ensure sub-systems exist on this GameObject
        EnsureSubSystems();

        // --- Phase 0: Apply ocean atmosphere & linear fog ---
        ZoneManager.ApplyAtmosphere(zone);

        // --- Phase 1: Generate ocean floor mesh ---
        Material seabedMat = envPropSet != null ? envPropSet.seabedMaterial : null;
        _meshGen.Generate(
            _zoneW, _zoneL, _seabedY,
            _pcgFreq, _pcgAmp, _pcgSeed,
            _pcgFreq, _pcgSeed,   // biome freq + seed for height biasing
            seabedMat
        );

        // --- Prepare distance culling (must exist before spawners register objects) ---
        var culler = GetComponent<DistanceCullingManager>()
                     ?? gameObject.AddComponent<DistanceCullingManager>();
        culler.ClearAll();

        // --- Phase 2: Scatter environment props on the mesh ---
        _propScatterer.Scatter(_meshGen, this, envPropSet, _zoneW, _zoneL, _pcgSeed);

        // --- Phase 3: Spawn species onto the generated seabed ---
        var spawner = FindFirstObjectByType<SpeciesSpawner>();
        if (spawner != null)
        {
            spawner.SpawnForZone(zoneIndex);
        }

        // --- Phase 4: Spawn marine debris clusters ---
        var debrisSpawner = FindFirstObjectByType<DebrisSpawner>();
        if (debrisSpawner == null)
        {
            debrisSpawner = gameObject.AddComponent<DebrisSpawner>();
        }
        debrisSpawner.SpawnDebrisForZone(zoneIndex);

        Debug.Log($"[TerrainGenerator] Zone {zoneIndex} fully generated " +
                  $"(mesh + props + species + debris, seed={_pcgSeed:0}).");

        // --- Phase 5: Finalize distance culling thresholds ---
        culler.RefreshThresholds();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private BiomeBand ValueToBiome(float v)
    {
        if (v < _softT)    return BiomeBand.OpenWater;
        if (v < _hardT)    return BiomeBand.Soft;
        if (v < _rockT)    return BiomeBand.Hard;
        if (v < _specialT) return BiomeBand.Rock;
        return BiomeBand.Special;
    }

    private void EnsureSubSystems()
    {
        _meshGen       = GetComponent<OceanFloorMeshGenerator>()
                         ?? gameObject.AddComponent<OceanFloorMeshGenerator>();
        _propScatterer = GetComponent<EnvPropScatterer>()
                         ?? gameObject.AddComponent<EnvPropScatterer>();
    }
}
