using UnityEngine;

/// <summary>
/// Procedurally generates the ocean zone environment using a two-layer Perlin Noise system.
///
/// Layer 1 — Biome Map:
///   Samples Perlin noise on a sparse grid. Each cell value maps to a BiomeBand
///   (OpenWater / Soft / Hard / Rock / Special) using per-zone thresholds from ZoneConfig.
///   This determines WHAT habitat type exists at each location.
///
/// Layer 2 — Height Map:
///   A second independent Perlin pass adds Y displacement to objects within each cell.
///   Sunlight Zone = gentle hills; Hadal Zone = tall jagged spires.
///
/// Terrain objects are prototype primitives (cubes, cylinders) coloured per zone+biome.
/// When Blender models are ready, swap SpawnBiomeCluster() to instantiate real prefabs.
///
/// Setup:
///   Place TerrainGenerator on a GameObject in each zone scene.
///   ZoneManager finds it via FindFirstObjectByType and calls Generate(zoneIndex).
///
/// Layer setup:
///   Create a "Terrain" layer in Edit ? Project Settings ? Tags and Layers.
///   TerrainGenerator assigns structural terrain objects to this layer so ContextSteering
///   can detect them for obstacle avoidance.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Grid")]
    [Tooltip("Perlin samples per axis. 12x12 = 144 biome cells is a good starting point.")]
    [SerializeField] private int gridResolution = 12;

    [Header("Parents")]
    [Tooltip("All terrain primitives are spawned under this transform. Auto-created if null.")]
    [SerializeField] private Transform terrainParent;

    // -----------------------------------------------------------------------
    // Per-zone biome colour palettes  [zoneIndex, biomeIndex]
    // biome order: OpenWater, Soft, Hard, Rock, Special
    // -----------------------------------------------------------------------

    private static readonly UnityEngine.Color[,] BiomeColors =
    {
        { // Zone 0 — Sunlight
            new UnityEngine.Color(0.85f, 0.80f, 0.60f),   // Open  — sandy beige
            new UnityEngine.Color(0.35f, 0.65f, 0.35f),   // Soft  — seagrass green
            new UnityEngine.Color(0.90f, 0.45f, 0.25f),   // Hard  — warm coral orange
            new UnityEngine.Color(0.55f, 0.55f, 0.55f),   // Rock  — grey
            new UnityEngine.Color(0.95f, 0.40f, 0.10f),   // Spec  — vivid orange-red (anemone patch)
        },
        { // Zone 1 — Twilight
            new UnityEngine.Color(0.18f, 0.25f, 0.40f),   // Open  — deep blue-grey
            new UnityEngine.Color(0.15f, 0.35f, 0.50f),   // Soft  — muted teal
            new UnityEngine.Color(0.75f, 0.35f, 0.55f),   // Hard  — bubblegum pink
            new UnityEngine.Color(0.30f, 0.30f, 0.45f),   // Rock  — slate
            new UnityEngine.Color(0.55f, 0.55f, 0.70f),   // Spec  — marine snow grey-blue
        },
        { // Zone 2 — Midnight
            new UnityEngine.Color(0.06f, 0.06f, 0.10f),   // Open  — near-black
            new UnityEngine.Color(0.08f, 0.15f, 0.08f),   // Soft  — dark green (sea pens)
            new UnityEngine.Color(0.04f, 0.04f, 0.14f),   // Hard  — very dark blue (black coral)
            new UnityEngine.Color(0.14f, 0.14f, 0.16f),   // Rock  — charcoal basalt
            new UnityEngine.Color(0.90f, 0.18f, 0.00f),   // Spec  — vent orange glow
        },
        { // Zone 3 — Abyss
            new UnityEngine.Color(0.10f, 0.08f, 0.06f),   // Open  — dark mud brown
            new UnityEngine.Color(0.08f, 0.12f, 0.08f),   // Soft  — cold seep dark green
            new UnityEngine.Color(0.12f, 0.12f, 0.22f),   // Hard  — pale purple sponge field
            new UnityEngine.Color(0.18f, 0.16f, 0.14f),   // Rock  — dark boulder
            new UnityEngine.Color(0.00f, 0.45f, 0.55f),   // Spec  — methane seep teal
        },
        { // Zone 4 — Hadal
            new UnityEngine.Color(0.04f, 0.04f, 0.07f),   // Open  — trench black
            new UnityEngine.Color(0.06f, 0.06f, 0.10f),   // Soft  — debris patch dark
            new UnityEngine.Color(0.08f, 0.08f, 0.18f),   // Hard  — hadal sponge midnight blue
            new UnityEngine.Color(0.10f, 0.10f, 0.13f),   // Rock  — fault block
            new UnityEngine.Color(0.28f, 0.00f, 0.38f),   // Spec  — bioluminescent purple
        },
    };

    // -----------------------------------------------------------------------
    // PCG state (populated by Generate())
    // -----------------------------------------------------------------------

    private int   _zoneIndex;
    private float _zoneW, _zoneL, _zoneDepth;
    private float _seabedY;
    private float _pcgFreq, _pcgAmp, _pcgSeed;
    private float _softT, _hardT, _rockT, _specialT;
    private int   _terrainLayerId = -1;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Y coordinate of the zone seabed (world space). Used by SpeciesSpawner.</summary>
    public float SeabedY => _seabedY;

    /// <summary>
    /// Query the BiomeBand at a world (x, z) position.
    /// Used by SpeciesSpawner to validate candidate spawn positions.
    /// </summary>
    public BiomeBand GetBiomeAt(float worldX, float worldZ)
    {
        float v = UnityEngine.Mathf.PerlinNoise(
            worldX * _pcgFreq + _pcgSeed,
            worldZ * _pcgFreq + _pcgSeed + 50f);
        return ValueToBiome(v);
    }

    // -----------------------------------------------------------------------
    // Entry point — called by ZoneManager after scene setup
    // -----------------------------------------------------------------------

    public void Generate(int zoneIndex)
    {
        _zoneIndex = zoneIndex;
        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            UnityEngine.Debug.LogWarning($"[TerrainGenerator] Invalid zone index {zoneIndex}");
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
                     ? GameManager.Instance.GetOrCreatePcgSeed(zoneIndex)
                     : UnityEngine.Random.Range(0, 99999);
        _softT     = zone.softBiomeThreshold;
        _hardT     = zone.hardBiomeThreshold;
        _rockT     = zone.rockBiomeThreshold;
        _specialT  = zone.specialBiomeThreshold;

        // Cache the "Terrain" layer index (create the layer in Project Settings first)
        _terrainLayerId = UnityEngine.LayerMask.NameToLayer("Terrain");

        EnsureTerrainParent();
        ClearExistingTerrain();
        PlaceTerrainObjects(zone);

        UnityEngine.Debug.Log($"[TerrainGenerator] Zone {zoneIndex} generated " +
                              $"({gridResolution}x{gridResolution} cells, seed={_pcgSeed:0}).");
    }

    // -----------------------------------------------------------------------
    // Generation passes
    // -----------------------------------------------------------------------

    private void PlaceTerrainObjects(ZoneDefinition zone)
    {
        float cellW = _zoneW / gridResolution;
        float cellL = _zoneL / gridResolution;

        for (int xi = 0; xi < gridResolution; xi++)
        {
            for (int zi = 0; zi < gridResolution; zi++)
            {
                float wx = CellToWorld(xi, gridResolution, _zoneW);
                float wz = CellToWorld(zi, gridResolution, _zoneL);

                // Layer 1 — biome band
                float biomeNoise = UnityEngine.Mathf.PerlinNoise(
                    wx * _pcgFreq + _pcgSeed,
                    wz * _pcgFreq + _pcgSeed + 50f);
                BiomeBand biome = ValueToBiome(biomeNoise);

                // Layer 2 — height displacement
                float heightNoise = UnityEngine.Mathf.PerlinNoise(
                    wx * _pcgFreq * 1.7f + _pcgSeed + 200f,
                    wz * _pcgFreq * 1.7f + _pcgSeed + 200f);
                float heightOffset = heightNoise * _pcgAmp;

                SpawnBiomeCluster(biome, wx, wz, _seabedY + heightOffset, cellW, cellL);
            }
        }
    }

    private void SpawnBiomeCluster(BiomeBand biome, float cx, float cz,
                                   float baseY, float cellW, float cellL)
    {
        int z = _zoneIndex;
        UnityEngine.Color floorCol  = BiomeColors[z, (int)biome];
        float hw = cellW * 0.92f;
        float hl = cellL * 0.92f;

        // Always place a floor tile
        SpawnPrimitive(UnityEngine.PrimitiveType.Cube,
            new UnityEngine.Vector3(cx, baseY - 0.5f, cz),
            new UnityEngine.Vector3(hw, 1f, hl),
            floorCol, "Floor", obstacleLayer: false);

        switch (biome)
        {
            case BiomeBand.Soft:
                for (int i = 0; i < UnityEngine.Random.Range(1, 3); i++)
                {
                    float h = UnityEngine.Random.Range(0.4f, 1.5f);
                    SpawnPrimitive(UnityEngine.PrimitiveType.Cylinder,
                        RandomCellPos(cx, cz, baseY + h * 0.5f, cellW * 0.35f, cellL * 0.35f),
                        new UnityEngine.Vector3(0.3f, h, 0.3f),
                        floorCol * 1.3f, "SoftMound", obstacleLayer: false);
                }
                break;

            case BiomeBand.Hard:
                int coralCount = UnityEngine.Random.Range(2, 5);
                for (int i = 0; i < coralCount; i++)
                {
                    float h = UnityEngine.Random.Range(0.8f, 3.5f);
                    float r = UnityEngine.Random.Range(0.2f, 0.7f);
                    SpawnPrimitive(UnityEngine.PrimitiveType.Cylinder,
                        RandomCellPos(cx, cz, baseY + h * 0.5f, cellW * 0.38f, cellL * 0.38f),
                        new UnityEngine.Vector3(r, h, r),
                        floorCol * 1.4f, "CoralStructure", obstacleLayer: true);
                }
                break;

            case BiomeBand.Rock:
                int rockCount = UnityEngine.Random.Range(1, 4);
                for (int i = 0; i < rockCount; i++)
                {
                    float s = UnityEngine.Random.Range(1f, 3f);
                    SpawnPrimitive(UnityEngine.PrimitiveType.Cube,
                        RandomCellPos(cx, cz, baseY + s * 0.5f, cellW * 0.38f, cellL * 0.38f),
                        new UnityEngine.Vector3(
                            s * UnityEngine.Random.Range(0.6f, 1.2f), s,
                            s * UnityEngine.Random.Range(0.6f, 1.2f)),
                        floorCol, "Rock", obstacleLayer: true);
                }
                break;

            case BiomeBand.Special:
                float ventH = UnityEngine.Random.Range(2.5f, 7f);
                SpawnPrimitive(UnityEngine.PrimitiveType.Cylinder,
                    new UnityEngine.Vector3(cx, baseY + ventH * 0.5f, cz),
                    new UnityEngine.Vector3(0.7f, ventH, 0.7f),
                    floorCol, "VentStructure", obstacleLayer: true);
                break;
        }
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

    private static float CellToWorld(int i, int res, float size) =>
        -size * 0.5f + (i + 0.5f) * (size / res);

    private static UnityEngine.Vector3 RandomCellPos(float cx, float cz, float y, float rx, float rz) =>
        new UnityEngine.Vector3(
            cx + UnityEngine.Random.Range(-rx, rx),
            y,
            cz + UnityEngine.Random.Range(-rz, rz));

    private void EnsureTerrainParent()
    {
        if (terrainParent == null)
        {
            var go = new UnityEngine.GameObject("_TerrainObjects");
            terrainParent = go.transform;
        }
    }

    private void ClearExistingTerrain()
    {
        for (int i = terrainParent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(terrainParent.GetChild(i).gameObject);
    }

    private UnityEngine.GameObject SpawnPrimitive(
        UnityEngine.PrimitiveType type,
        UnityEngine.Vector3 position,
        UnityEngine.Vector3 scale,
        UnityEngine.Color color,
        string label,
        bool obstacleLayer)
    {
        var go = UnityEngine.GameObject.CreatePrimitive(type);
        go.name = label;
        go.transform.SetParent(terrainParent, false);
        go.transform.position   = position;
        go.transform.localScale = scale;

        // Apply prototype colour
        var rend = go.GetComponent<UnityEngine.Renderer>();
        if (rend != null)
        {
            rend.material = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"))
            { color = color };
        }

        // Assign to Terrain layer so ContextSteering danger rays can detect it
        if (obstacleLayer && _terrainLayerId >= 0)
            go.layer = _terrainLayerId;

        return go;
    }
}
