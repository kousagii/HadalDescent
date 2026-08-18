using UnityEngine;

/// <summary>
/// Generates a procedural ocean floor mesh using layered, smoothed noise.
///
/// Design aesthetic (Endless Ocean Luminous style):
///   - Broad sandy valleys and gentle rolling dunes
///   - Elevated coral reef plateaus with smooth, swimmable slopes
///   - Zero needle spikes or chaotic sharp cliffs
///   - Full coverage centered at world origin (0, 0, 0)
///
/// Exposes SampleHeight(x,z) and SampleNormal(x,z) for prop scattering
/// and species spawning.
///
/// Usage: Called by TerrainGenerator.Generate().
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

        float nx = (worldX + _width  * 0.5f) / _width;
        float nz = (worldZ + _length * 0.5f) / _length;
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
        return Mathf.Lerp(h0, h1, fz);
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

    /// <summary>
    /// Generate the ocean floor mesh for a zone.
    /// </summary>
    public void Generate(float width, float length, float baseY,
                         float frequency, float amplitude, float seed,
                         float biomeFreq, float biomeSeed,
                         Material seabedMaterial)
    {
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

        // CRITICAL: Ensure the GameObject transform is centered at world origin (0, 0, 0)
        transform.position   = Vector3.zero;
        transform.rotation   = Quaternion.identity;
        transform.localScale = Vector3.one;

        EnsureComponents();
        BuildMesh();

        if (seabedMaterial != null)
            _meshRenderer.material = seabedMaterial;
        else
            _meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;

        Debug.Log($"[OceanFloor] Generated {_resX}x{_resZ} mesh across {_width}x{_length}m, " +
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
    /// Compute height at a world position using multi-layered smooth natural synthesis.
    /// </summary>
    private float ComputeHeight(float wx, float wz)
    {
        // ── Layer 1: Broad Macro Basin & Reef Banks (Wavelength ~150-250m) ────
        float macro1 = SmoothNoise(wx, wz, _frequency * 0.6f, _seed, 0f);
        float macro2 = SmoothNoise(wx, wz, _frequency * 1.0f, _seed, 350f);
        float macro = macro1 * 0.65f + macro2 * 0.35f;

        // ── Layer 2: Reef Mounds & Terraces (Wavelength ~50-80m) ──────────────
        float mound1 = SmoothNoise(wx, wz, _frequency * 2.2f, _seed, 700f);
        float mound2 = SmoothNoise(wx, wz, _frequency * 3.4f, _seed, 1050f);
        float mound = mound1 * 0.6f + mound2 * 0.4f;

        // Combine macro and mounds
        float shape = macro * 0.70f + mound * 0.30f;

        // ── Biome Influence (Coral/Hard areas gently elevated) ────────────────
        float biomeVal = Mathf.PerlinNoise(
            wx * _biomeFreq + _biomeSeed,
            wz * _biomeFreq + _biomeSeed + 50f);
        float biomeElevation = Mathf.Lerp(-0.06f, 0.12f, biomeVal);
        shape = Mathf.Clamp01(shape + biomeElevation);

        // ── Smooth Plateau / Terrace Curve (Creates wide reef flats) ──────────
        // Soft S-curve keeps valleys wide & flat, slopes gentle, and tops rounded
        shape = Mathf.SmoothStep(0f, 1f, shape);

        // ── Layer 3: Subtle Sand Dune Ripples (Micro-scale, low amp) ─────────
        // Only contributes 3-4% height so it never creates sharp spikes
        float ripple = (SmoothNoise(wx, wz, _frequency * 7f, _seed, 1400f) - 0.5f) * 0.04f;
        shape = Mathf.Clamp01(shape + ripple);

        // ── Edge Falloff: Blends seamlessly to baseline at boundaries ────────
        float edgeFade = EdgeFalloff(wx, wz);
        shape *= edgeFade;

        // ── Map normalized [0..1] to world Y coordinates ─────────────────────
        return _baseY + shape * _amplitude;
    }

    /// <summary>
    /// Smooth Perlin noise using quintic curve (Ken Perlin's smootherstep) to remove harsh gradients.
    /// </summary>
    private float SmoothNoise(float wx, float wz, float freq, float seed, float offset)
    {
        float px = wx * freq + seed + offset;
        float pz = wz * freq + seed + offset + 50f;
        float raw = Mathf.PerlinNoise(px, pz);
        // Smootherstep: 6t^5 - 15t^4 + 10t^3
        return raw * raw * raw * (raw * (raw * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Smooth edge falloff so the terrain naturally levels out near the zone perimeter.
    /// </summary>
    private float EdgeFalloff(float wx, float wz)
    {
        float halfW = _width * 0.5f;
        float halfL = _length * 0.5f;
        float margin = Mathf.Min(halfW, halfL) * 0.18f;

        float distX = halfW - Mathf.Abs(wx);
        float distZ = halfL - Mathf.Abs(wz);

        float fx = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(distX / margin));
        float fz = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(distZ / margin));

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

        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows    = true;
    }
}
