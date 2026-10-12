using UnityEngine;

/// <summary>
/// Generates a procedural ocean floor mesh with authentic marine geography:
///   - Shallow Coral Reef Atolls & Plateaus (Depth 10mâ€“30m): sunlit coral gardens & lagoons
///   - Reef Slopes & Drop-Off Walls (Depth 30mâ€“90m): home to sharks and deep fans
///   - Deep Sandy Basins & Canyons (Depth 90mâ€“180m): rolling sand dunes & boulder fields
///
/// Features:
///   - Contrast-expanded fBm noise for true vertical dynamic range
///   - Stepped plateau terracing for wide, walkable/swimmable reef flats
///   - Extended perimeter mesh (35% wider than boundary walls) to hide all edges in fog
///   - Full bilinear interpolation for height and slope normal sampling
/// </summary>
public class OceanFloorMeshGenerator : MonoBehaviour
{
    [Header("Mesh Resolution")]
    [Tooltip("Vertices per axis. 96 is a great balance of smooth detail and performance.")]
    [SerializeField] private int resolution = 96;

    [Header("Texture Tiling")]
    [Tooltip("How many times the seabed texture tiles across the entire zone.")]
    [SerializeField] private float textureTiling = 24f;

    // -----------------------------------------------------------------------
    // Runtime state
    // -----------------------------------------------------------------------

    private float _width, _length;
    private float _baseY;
    private float _frequency;
    private float _amplitude;
    private float _seed;
    private float _biomeFreq;
    private float _biomeSeed;

    private MeshFilter   _meshFilter;
    private MeshRenderer _meshRenderer;
    private MeshCollider _meshCollider;
    private Mesh         _mesh;

    private float[] _heightMap;
    private int     _resX, _resZ;
    private int     _zoneIndex = -1;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Query the ocean floor height at any world (x, z) position.
    /// Uses bilinear interpolation between cached vertex heights.
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        if (_heightMap == null || _resX == 0 || _resZ == 0)
            return _baseY;

        float meshW = _width  * 1.35f;
        float meshL = _length * 1.35f;

        float nx = (worldX + meshW * 0.5f) / meshW;
        float nz = (worldZ + meshL * 0.5f) / meshL;
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        float gx = nx * (_resX - 1);
        float gz = nz * (_resZ - 1);

        int x0 = Mathf.FloorToInt(gx);
        int z0 = Mathf.FloorToInt(gz);
        int x1 = Mathf.Min(x0 + 1, _resX - 1);
        int z1 = Mathf.Min(z0 + 1, _resZ - 1);

        float fx = gx - x0;
        float fz = gz - z0;

        float h00 = _heightMap[z0 * _resX + x0];
        float h10 = _heightMap[z0 * _resX + x1];
        float h01 = _heightMap[z1 * _resX + x0];
        float h11 = _heightMap[z1 * _resX + x1];

        float h0 = Mathf.Lerp(h00, h10, fx);
        float h1 = Mathf.Lerp(h01, h11, fx);
        float sampled = Mathf.Lerp(h0, h1, fz);
        if (IsSunlightZone())
            sampled = Mathf.Min(sampled, -10.0f);
        return sampled;
    }

    /// <summary>
    /// Query the surface normal at a world (x, z) position.
    /// Used by EnvPropScatterer to align props to the slope.
    /// </summary>
    public Vector3 SampleNormal(float worldX, float worldZ)
    {
        float delta = _width / _resX;
        float hL = SampleHeight(worldX - delta, worldZ);
        float hR = SampleHeight(worldX + delta, worldZ);
        float hD = SampleHeight(worldX, worldZ - delta);
        float hU = SampleHeight(worldX, worldZ + delta);

        Vector3 tangentX = new Vector3(delta * 2f, hR - hL, 0f);
        Vector3 tangentZ = new Vector3(0f, hU - hD, delta * 2f);
        return Vector3.Cross(tangentZ, tangentX).normalized;
    }

    public bool IsSunlightZone()
    {
        return (_zoneIndex == 0) ||
               (ZoneManager.CurrentZoneIndex == 0) ||
               (_baseY <= -195f && _baseY >= -205f) ||
               (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Sunlight"));
    }

    /// <summary>
    /// Generate the ocean floor mesh for a zone.
    /// </summary>
    public void Generate(float width, float length, float baseY,
                         float frequency, float amplitude, float seed,
                         float biomeFreq, float biomeSeed,
                         Material seabedMaterial,
                         int zoneIndex = -1)
    {
        _zoneIndex = zoneIndex;
        _width     = width;
        _length    = length;
        _baseY     = baseY;
        _frequency = frequency;
        _amplitude = amplitude;
        _seed      = seed;
        _biomeFreq = biomeFreq;
        _biomeSeed = biomeSeed;
        _resX      = resolution;
        _resZ      = resolution;

        // CRITICAL: Ensure the GameObject transform is unparented and centered at world origin (0, 0, 0)
        transform.SetParent(null);
        transform.position   = Vector3.zero;
        transform.rotation   = Quaternion.identity;
        transform.localScale = Vector3.one;

        EnsureComponents();
        BuildMesh();

        if (seabedMaterial != null)
            _meshRenderer.material = seabedMaterial;
        else
            _meshRenderer.material = MaterialUtils.CreateColoredMaterial(new Color(0.12f, 0.22f, 0.28f, 1f));

        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;

        Debug.Log($"[OceanFloor] Generated {_resX}x{_resZ} mesh across {_width * 1.35f:0}x{_length * 1.35f:0}m, " +
                  $"amplitude={_amplitude}m, baseY={_baseY}m");
    }

    // -----------------------------------------------------------------------
    // Mesh construction
    // -----------------------------------------------------------------------

    private void BuildMesh()
    {
        int vertCount = _resX * _resZ;
        var vertices  = new Vector3[vertCount];
        var uvs       = new Vector2[vertCount];
        _heightMap    = new float[vertCount];

        // Extend mesh 35% beyond playable zone so the perimeter edge is always hidden behind ocean fog
        float meshW = _width  * 1.35f;
        float meshL = _length * 1.35f;

        for (int z = 0; z < _resZ; z++)
        {
            for (int x = 0; x < _resX; x++)
            {
                float nx = (float)x / (_resX - 1);
                float nz = (float)z / (_resZ - 1);

                // Spans from -meshW/2 to +meshW/2, centered at (0, 0)
                float worldX = -meshW * 0.5f + nx * meshW;
                float worldZ = -meshL * 0.5f + nz * meshL;

                float y = ComputeHeight(worldX, worldZ);
                if (IsSunlightZone())
                    y = Mathf.Min(y, -10.0f);

                int idx = z * _resX + x;
                vertices[idx]   = new Vector3(worldX, y, worldZ);
                uvs[idx]        = new Vector2(nx * textureTiling * 1.35f, nz * textureTiling * 1.35f);
                _heightMap[idx] = y;
            }
        }

        // Triangle indices
        int quadCount = (_resX - 1) * (_resZ - 1);
        var triangles = new int[quadCount * 6];
        int ti = 0;
        for (int z = 0; z < _resZ - 1; z++)
        {
            for (int x = 0; x < _resX - 1; x++)
            {
                int bl = z * _resX + x;
                int br = bl + 1;
                int tl = bl + _resX;
                int tr = tl + 1;

                triangles[ti++] = bl;
                triangles[ti++] = tl;
                triangles[ti++] = br;
                triangles[ti++] = br;
                triangles[ti++] = tl;
                triangles[ti++] = tr;
            }
        }

        if (_mesh == null)
            _mesh = new Mesh();
        else
            _mesh.Clear();

        _mesh.name = "OceanFloor";
        if (vertCount > 65535)
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        _mesh.vertices  = vertices;
        _mesh.uv        = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _meshFilter.mesh = _mesh;
    }

    /// <summary>
    /// Compute height at a world position.
    /// Synthesizes authentic marine geomorphology with 4 ecological depth tiers:
    ///   - Tier 1: Shallow Sunlit Reef Platform (Depth 8m-18m) for Cone Snails, Clownfish & Anemones
    ///   - Tier 2: Mid-Reef Shelf & Terraces (Depth 18m-50m) for Lobsters, Rays, Giant Clams
    ///   - Tier 3: Drop-Off Wall & Outer Reef Slope (Depth 50m-100m) for Sharks, Dolphins, Squids
    ///   - Tier 4: Deep Basin Floor & Rolling Dunes (Depth 100m-190m)
    /// </summary>
    private float ComputeHeight(float wx, float wz)
    {
        // ── Domain Warping (Breaks artificial grid alignment; forms natural curved atolls) ──
        float warpX = (SmoothNoise(wx, wz, _frequency * 0.45f, _seed, 2100f) - 0.5f) * 45f;
        float warpZ = (SmoothNoise(wx, wz, _frequency * 0.45f, _seed, 2600f) - 0.5f) * 45f;
        float sampleX = wx + warpX;
        float sampleZ = wz + warpZ;

        // ── Layer 1: Broad Macro Basin & Reef Banks (Wavelength ~250m) ────────
        float macro1 = SmoothNoise(sampleX, sampleZ, _frequency * 0.6f, _seed, 0f);
        float macro2 = SmoothNoise(sampleX, sampleZ, _frequency * 1.0f, _seed, 350f);
        float macro  = macro1 * 0.65f + macro2 * 0.35f;

        // ── Layer 2: Reef Mounds & Terraces (Wavelength ~80m) ──────────────────
        float mound1 = SmoothNoise(sampleX, sampleZ, _frequency * 2.2f, _seed, 700f);
        float mound2 = SmoothNoise(sampleX, sampleZ, _frequency * 3.4f, _seed, 1050f);
        float mound  = mound1 * 0.6f + mound2 * 0.4f;

        // Combine macro and mounds
        float rawShape = macro * 0.68f + mound * 0.32f;

        // ── Biome Influence (Coral / Hard reef areas strongly elevated) ───────
        float biomeVal = Mathf.PerlinNoise(
            sampleX * _biomeFreq + _biomeSeed,
            sampleZ * _biomeFreq + _biomeSeed + 50f);
        float biomeElevation = Mathf.Lerp(-0.08f, 0.22f, biomeVal);
        rawShape += biomeElevation;

        // ── Contrast Expansion ────────────────────────────────────────────────
        float contrast = Mathf.InverseLerp(0.22f, 0.78f, rawShape);
        contrast = Mathf.Clamp01(contrast);

        // ── 4-Tier Ecological Depth Bathymetry Shaping ────────────────────────
        float plateauShape;
        if (contrast > 0.62f)
        {
            // Tier 1: Shallow Sunlit Reef Platform (Depth 10m to 18m, Y = -18m to -10m)
            // Crests at exactly 10m depth (Y = -10m); optimal for Geography Cone Snails (6-17m), Clownfish (1-15m) & Sea Anemones (1-15m)
            float t = Mathf.InverseLerp(0.62f, 1.0f, contrast);
            plateauShape = Mathf.Lerp(0.948f, 1.0f, SmoothStepQuintic(t));
        }
        else if (contrast > 0.44f)
        {
            // Tier 2: Mid-Reef Shelf & Terraces (Depth 18m to 50m, Y = -50m to -18m)
            // Home to Ornate Spiny Lobsters (1-50m), Blue-spotted Rays (0-20m), Giant Clams, Fan Corals
            float t = Mathf.InverseLerp(0.44f, 0.62f, contrast);
            plateauShape = Mathf.Lerp(0.781f, 0.948f, Mathf.SmoothStep(0f, 1f, t));
        }
        else if (contrast > 0.26f)
        {
            // Tier 3: Drop-Off Wall & Outer Reef Slope (Depth 50m to 100m, Y = -100m to -50m)
            // Patrol routes for Blacktip Reef Sharks (0-75m), Dolphins (1-50m), Squids (0-100m)
            float t = Mathf.InverseLerp(0.26f, 0.44f, contrast);
            plateauShape = Mathf.Lerp(0.521f, 0.781f, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            // Tier 4: Deep Basin Floor & Rolling Dunes (Depth 100m to 190m, Y = -190m to -100m)
            float t = Mathf.InverseLerp(0f, 0.26f, contrast);
            plateauShape = Mathf.Lerp(0.052f, 0.521f, Mathf.SmoothStep(0f, 1f, t));
        }

        // ── Layer 3: Organic Sediment Micro-Relief (Gentle Sand Dunes & Ripples) ─
        float ripple = (SmoothNoise(sampleX, sampleZ, _frequency * 6.5f, _seed, 1400f) - 0.5f) * 0.016f;
        plateauShape = Mathf.Clamp01(plateauShape + ripple);

        // ── Edge Falloff: Smoothly blends perimeter to baseline ───────────────
        float edgeFade = EdgeFalloff(wx, wz);
        plateauShape *= edgeFade;

        // Map normalized [0..1] to world Y
        // Guaranteed ceiling: highest seabed point reaches at most 10m depth (Y <= -10.0m) in Sunlight Zone
        bool isSunlight = IsSunlightZone();
        float maxAllowedSeabedY = isSunlight ? -10.0f : (_baseY + _amplitude);
        float computedY = _baseY + plateauShape * _amplitude;
        if (isSunlight)
            computedY = Mathf.Min(computedY, -10.0f);
        return Mathf.Min(computedY, maxAllowedSeabedY);
    }

    private static float SmoothStepQuintic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Smooth Perlin noise using quintic smootherstep.
    /// </summary>
    private float SmoothNoise(float wx, float wz, float freq, float seed, float offset)
    {
        float px = wx * freq + seed + offset;
        float pz = wz * freq + seed + offset + 50f;
        float raw = Mathf.PerlinNoise(px, pz);
        return raw * raw * raw * (raw * (raw * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Edge falloff for the extended mesh.
    /// </summary>
    private float EdgeFalloff(float wx, float wz)
    {
        float halfW = _width  * 1.35f * 0.5f;
        float halfL = _length * 1.35f * 0.5f;
        float margin = Mathf.Min(halfW, halfL) * 0.20f;

        float distX = halfW - Mathf.Abs(wx);
        float distZ = halfL - Mathf.Abs(wz);

        float fx = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(distX / margin));
        float fz = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(distZ / margin));

        return fx * fz;
    }

    // -----------------------------------------------------------------------
    // Component management
    // -----------------------------------------------------------------------

    private void EnsureComponents()
    {
        _meshFilter   = GetComponent<MeshFilter>()   ?? gameObject.AddComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        _meshCollider = GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();

        _meshRenderer.enabled           = true;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows    = true;

        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0)
            gameObject.layer = terrainLayer;
    }
}
