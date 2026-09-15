using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters environment prop prefabs (rocks, corals, sponges) on top of the
/// procedural ocean floor mesh in natural geological and ecological clusters
/// (Endless Ocean Luminous style) according to the Hadal Descent Environment Blueprint.
///
/// Features:
///   - Biome-Aware Placement: queries Perlin BiomeBand to cluster geological props
///     in their natural habitats (Limestone in Hard/Reef, Boulders in Rock, etc.).
///   - Multi-Zone Transformation Matrix: supports non-uniform scaling (e.g. flat reef
///     ledges, tall hydrothermal spires, spherical boulders).
///   - Blueprint Material Application: overrides default untextured Blender white materials
///     with authentic marine rock shaders.
///   - Natural seabed anchoring and obstacle tagging for submarine ContextSteering.
/// </summary>
public class EnvPropScatterer : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------

    [Header("Clustering")]
    [Tooltip("Number of distinct rock/coral garden cluster centers to generate.")]
    [SerializeField] private int clusterCount = 40;

    [Tooltip("Radius of each reef/rock cluster (meters).")]
    [SerializeField] private float clusterRadius = 6.5f;

    [Tooltip("Target coral count per reef complex.")]
    [SerializeField] private int propsPerCluster = 16;

    [Header("Placement")]
    [Tooltip("Minimum spacing between individual corals in a reef complex.")]
    [SerializeField] private float minCoralSpacing = 0.9f;

    [Tooltip("Fraction of the prop's height that embeds into the seabed sand (0.0 = on surface, 0.15 = 15% embedded).")]
    [SerializeField] private float naturalSinkFraction = 0.12f;

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private Transform _propParent;
    private int       _terrainLayerId = -1;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Scatter(OceanFloorMeshGenerator meshGen, EnvPropSet propSet,
                        float width, float length, float seed)
    {
        TerrainGenerator terrain = GetComponent<TerrainGenerator>() ?? FindFirstObjectByType<TerrainGenerator>();
        Scatter(meshGen, terrain, propSet, width, length, seed);
    }

    /// <summary>
    /// Scatter props across the zone's ocean floor with biome-aware placement.
    /// </summary>
    public void Scatter(OceanFloorMeshGenerator meshGen, TerrainGenerator terrain,
                        EnvPropSet propSet, float width, float length, float seed)
    {
        if (propSet == null || propSet.props == null || propSet.props.Length == 0)
        {
            Debug.LogWarning("[EnvPropScatterer] No prop set or empty prop list — skipping.");
            return;
        }

        _terrainLayerId = LayerMask.NameToLayer("Terrain");
        EnsurePropParent();
        ClearExistingProps();

        // Seed random state for deterministic placement
        Random.State savedState = Random.state;
        Random.InitState(Mathf.RoundToInt(seed * 7919f));

        PrewarmCoralMaterials(propSet);

        var placedPositions = new List<Vector3>();
        int placedCount = 0;

        // Bound props strictly within 42% of zone width & length so no props spawn beyond the ocean floor edges
        float halfW = width  * 0.42f;
        float halfL = length * 0.42f;

        // Categorize props by biome
        var hardProps = new List<EnvPropSet.PropEntry>();
        var hardFoundationProps = new List<EnvPropSet.PropEntry>();
        var hardCoralProps = new List<EnvPropSet.PropEntry>();
        var rockProps = new List<EnvPropSet.PropEntry>();
        var softMeadowProps = new List<EnvPropSet.PropEntry>();
        var softDuneProps = new List<EnvPropSet.PropEntry>();

        foreach (var p in propSet.props)
        {
            if (p == null || p.prefab == null) continue;
            string n = p.prefab.name.ToLower();
            if (p.targetBiome == BiomeBand.Hard)
            {
                hardProps.Add(p);
                if (n.Contains("cube") || n.Contains("cone") || n.Contains("plateau") || n.Contains("pinnacle") || n.Contains("rock"))
                    hardFoundationProps.Add(p);
                else
                    hardCoralProps.Add(p);
            }
            else if (p.targetBiome == BiomeBand.Rock)
            {
                rockProps.Add(p);
            }
            else if (p.targetBiome == BiomeBand.Soft)
            {
                if (n.Contains("seagrass") || n.Contains("clam") || n.Contains("shell"))
                    softMeadowProps.Add(p);
                else
                    softDuneProps.Add(p);
            }
        }

        if (hardFoundationProps.Count == 0) hardFoundationProps = hardProps;
        if (hardCoralProps.Count == 0) hardCoralProps = hardProps;

        // ── Phase 1: Biome Ecosystem Formations ───────────────────────────
        float zoneAreaRatio = (width * length) / (300f * 300f);
        int totalClusters = Mathf.RoundToInt(clusterCount * zoneAreaRatio * propSet.propDensity);
        totalClusters = Mathf.Clamp(totalClusters, 12, 110);

        int coralColorSequence = 0;

        for (int c = 0; c < totalClusters; c++)
        {
            float cx = Random.Range(-halfW, halfW);
            float cz = Random.Range(-halfL, halfL);
            BiomeBand biome = terrain != null ? terrain.GetBiomeAt(cx, cz) : BiomeBand.Hard;

            if (biome == BiomeBand.Hard)
            {
                // ── REEF COMPLEX (Hard Biome) ─────────────────────────────
                // 1. Center Foundation: Rock / Plateau / Pinnacle
                var foundationEntry = PickRandomFromList(hardFoundationProps);
                if (foundationEntry != null)
                {
                    if (TryPlacePropInstance(meshGen, propSet, foundationEntry, cx, cz, placedPositions, 2.0f, -1))
                        placedCount++;
                }

                // 2. Dense Coral Garden: tightly clustered corals on & around the base
                int reefItems = Random.Range(Mathf.Max(8, propsPerCluster - 4), propsPerCluster + 6);
                float reefRadius = Random.Range(clusterRadius * 0.7f, clusterRadius * 1.15f);
                for (int i = 0; i < reefItems; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * reefRadius;
                    float px = Mathf.Clamp(cx + offset.x, -halfW, halfW);
                    float pz = Mathf.Clamp(cz + offset.y, -halfL, halfL);

                    var coralEntry = PickRandomFromList(hardCoralProps);
                    if (coralEntry == null) coralEntry = PickRandomFromList(hardProps);

                    // Neighboring corals alternate colors across the 6-color palette
                    int colorIdx = (coralColorSequence++) % CoralPalette.Length;
                    // Tight spacing within reef allows corals to crowd and interlock naturally
                    if (TryPlacePropInstance(meshGen, propSet, coralEntry, px, pz, placedPositions, minCoralSpacing, colorIdx))
                        placedCount++;
                }
            }
            else if (biome == BiomeBand.Rock)
            {
                // ── ROCK OUTCROP (Rock Biome) ──────────────────────────────
                int boulderCount = Random.Range(6, 12);
                float outcropRadius = Random.Range(6.0f, 13.0f);
                for (int i = 0; i < boulderCount; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * outcropRadius;
                    float px = Mathf.Clamp(cx + offset.x, -halfW, halfW);
                    float pz = Mathf.Clamp(cz + offset.y, -halfL, halfL);

                    var rockEntry = PickRandomFromList(rockProps);
                    if (rockEntry != null && TryPlacePropInstance(meshGen, propSet, rockEntry, px, pz, placedPositions, 2.2f, -1))
                        placedCount++;
                }
            }
            else // BiomeBand.Soft
            {
                // ── SEAGRASS MEADOW / CLAM BED (Soft Biome) ────────────────
                int softCount = Random.Range(12, 24);
                float meadowRadius = Random.Range(5.0f, 10.0f);
                for (int i = 0; i < softCount; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * meadowRadius;
                    float px = Mathf.Clamp(cx + offset.x, -halfW, halfW);
                    float pz = Mathf.Clamp(cz + offset.y, -halfL, halfL);

                    var meadowEntry = PickRandomFromList(softMeadowProps);
                    if (meadowEntry != null && TryPlacePropInstance(meshGen, propSet, meadowEntry, px, pz, placedPositions, 1.1f, -1))
                        placedCount++;
                }
            }
        }

        // ── Phase 2: Solitary Sand Ripples & Lone Dune Props ───────────────
        // Strictly exclude corals so empty sand dunes remain natural
        int solitaryCount = Mathf.RoundToInt(width * length / 400f * propSet.propDensity);
        for (int s = 0; s < solitaryCount; s++)
        {
            float sx = Random.Range(-halfW, halfW);
            float sz = Random.Range(-halfL, halfL);
            BiomeBand biome = terrain != null ? terrain.GetBiomeAt(sx, sz) : BiomeBand.Soft;

            EnvPropSet.PropEntry solitaryEntry = null;
            if (biome == BiomeBand.Soft)
            {
                solitaryEntry = softDuneProps.Count > 0 ? PickRandomFromList(softDuneProps) : PickRandomFromList(softMeadowProps);
            }
            else if (biome == BiomeBand.Rock && rockProps.Count > 0)
            {
                solitaryEntry = PickRandomFromList(rockProps);
            }

            if (solitaryEntry != null)
            {
                if (TryPlacePropInstance(meshGen, propSet, solitaryEntry, sx, sz, placedPositions, 3.5f, -1))
                    placedCount++;
            }
        }

        // Restore random state
        Random.state = savedState;

        Debug.Log($"[EnvPropScatterer] Placed {placedCount} natural props in {totalClusters} realistic biome formations across {width}x{length}m.");

        // Register all props with distance culling for performance
        if (DistanceCullingManager.Instance != null && _propParent != null)
            DistanceCullingManager.Instance.RegisterParent(_propParent);
    }

    // -----------------------------------------------------------------------
    // Placement helper
    // -----------------------------------------------------------------------

    private bool TryPlacePropInstance(OceanFloorMeshGenerator meshGen, EnvPropSet propSet,
                                      EnvPropSet.PropEntry entry, float px, float pz,
                                      List<Vector3> placedPositions, float minDistance, int coralColorIndex)
    {
        if (entry == null || entry.prefab == null) return false;

        // Spacing check
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float dx = px - placedPositions[i].x;
            float dz = pz - placedPositions[i].z;
            if (dx * dx + dz * dz < minDistance * minDistance)
                return false;
        }

        // Query height & surface normal from procedural mesh
        float floorY = meshGen.SampleHeight(px, pz);
        Vector3 normal = meshGen.SampleNormal(px, pz);

        // Rotation & Scale with globalScaleMultiplier and non-uniform scale matrix
        float mult = propSet != null ? Mathf.Max(0.1f, propSet.globalScaleMultiplier) : 1f;
        float randomScale = Random.Range(entry.minScale, entry.maxScale) * mult;
        Vector3 scaleAxis = entry.scaleMultiplier != Vector3.zero ? entry.scaleMultiplier : Vector3.one;
        Vector3 finalScale = Vector3.Scale(scaleAxis, Vector3.one * randomScale);

        Quaternion rot = Quaternion.identity;
        if (entry.alignToSurface)
        {
            // Slerp towards surface normal based on surfaceTiltStrength
            Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion randomSpin = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float tilt = Mathf.Clamp01(entry.surfaceTiltStrength);
            rot = Quaternion.Slerp(randomSpin, slopeAlign * randomSpin, tilt);
        }
        else
        {
            rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // Apply custom rotation offset if specified (e.g. for models with non-standard export axes)
        if (entry.rotationOffset != Vector3.zero)
        {
            rot = rot * Quaternion.Euler(entry.rotationOffset);
        }

        // Multiply by the prefab's native author-configured rotation so models imported with non-zero
        // Euler rotations (e.g. prop_sun_seagrass_patch +90° X, sand ripples -90° X) maintain their correct upright orientation.
        Quaternion prefabRot = entry.prefab != null ? entry.prefab.transform.rotation : Quaternion.identity;
        Quaternion finalRot = rot * prefabRot;

        // Spawn instance at floorY with finalRot preserving prefab authored orientation
        Vector3 spawnPos = new Vector3(px, floorY, pz);
        GameObject go = Instantiate(entry.prefab, spawnPos, finalRot, _propParent);
        go.transform.localScale = finalScale;

        // Dynamic Coral Color Variation
        if (go.name.IndexOf("coral", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ApplyCoralColorVariation(go, coralColorIndex);
        }
        else
        {
            ApplyPropMaterial(go, entry.materialOverride);
        }

        // Adjust horizontal and vertical position to sit naturally on the seabed
        AdjustContactHeight(go, px, floorY, pz, meshGen);

        // Assign obstacle layer for ContextSteering
        if (entry.isObstacle && _terrainLayerId >= 0)
            SetLayerRecursive(go, _terrainLayerId);

        // Ensure collider exists
        EnsureCollider(go);

        placedPositions.Add(new Vector3(px, floorY, pz));
        return true;
    }

    /// <summary>
    /// Calculates the object's mesh bounds and adjusts position so the base embeds slightly
    /// into the sand rather than floating. Also shifts horizontally if the original 3D model
    /// had an off-center origin/pivot (e.g. coral clusters) so the visible model sits right
    /// at the sampled seabed coordinate.
    /// </summary>
    private void AdjustContactHeight(GameObject go, float targetFloorX, float floorY, float targetFloorZ, OceanFloorMeshGenerator meshGen)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combinedBounds.Encapsulate(renderers[i].bounds);

            // 1. Shift GameObject horizontally if the visual mesh is offset from the transform pivot
            Vector3 horizontalShift = new Vector3(combinedBounds.center.x - targetFloorX, 0f, combinedBounds.center.z - targetFloorZ);
            if (horizontalShift.sqrMagnitude > 0.001f)
            {
                go.transform.position -= horizontalShift;
            }

            // Re-sample true seabed elevation at the object's final horizontal coordinate
            float trueFloorY = meshGen != null ? meshGen.SampleHeight(go.transform.position.x, go.transform.position.z) : floorY;

            // 2. Adjust vertical position so base embeds naturally into seabed
            float bottomY = combinedBounds.min.y;
            float height = combinedBounds.size.y;
            float pivotY = go.transform.position.y;

            float bottomOffset = pivotY - bottomY;
            bool isSeagrass = go.name.IndexOf("seagrass", System.StringComparison.OrdinalIgnoreCase) >= 0;
            // Seagrass embeds slightly at its base without sinking deep or clipping into the floor
            float sink = isSeagrass ? 0.02f : height * naturalSinkFraction;
            float targetY = trueFloorY + bottomOffset - sink;

            // Safety floor clamp: Ensure no prop base penetrates beyond/below the ocean floor
            targetY = Mathf.Max(targetY, trueFloorY - (isSeagrass ? 0.04f : sink));

            Vector3 pos = go.transform.position;
            pos.y = targetY;
            go.transform.position = pos;

            // 3. Ocean surface safety clamp: Ensure no prop breaches above water surface (Y = 0)
            float topOffset = combinedBounds.max.y - pivotY;
            if (targetY + topOffset > -0.5f)
            {
                pos.y = -0.5f - topOffset;
                go.transform.position = pos;
            }
        }
    }

    private void EnsureCollider(GameObject go)
    {
        var colliders = go.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;  // Must be convex to support isTrigger
                mc.isTrigger = true;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
        }
        else
        {
            // Existing colliders from prefab — make them triggers so props
            // don't physically block the submarine. The ocean floor mesh
            // already provides the solid terrain collision surface.
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = true;
            }
        }
    }

    private static T PickRandomFromList<T>(List<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[Random.Range(0, list.Count)];
    }

    private void EnsurePropParent()
    {
        if (_propParent == null)
        {
            var existing = GameObject.Find("_EnvironmentProps");
            if (existing != null)
                _propParent = existing.transform;
            else
            {
                var go = new GameObject("_EnvironmentProps");
                _propParent = go.transform;
            }
        }

        // Center parent at world origin with clean scale
        _propParent.position   = Vector3.zero;
        _propParent.rotation   = Quaternion.identity;
        _propParent.localScale = Vector3.one;
    }

    private void ClearExistingProps()
    {
        if (_propParent == null) return;
        for (int i = _propParent.childCount - 1; i >= 0; i--)
            Destroy(_propParent.GetChild(i).gameObject);
    }

    private static Material _cachedRockMaterial;

    private static readonly Color[] CoralPalette = new Color[]
    {
        new Color(1.00f, 0.45f, 0.08f), // Sunlit Vibrant Orange
        new Color(1.00f, 0.22f, 0.58f), // Electric Coral Pink
        new Color(0.62f, 0.18f, 0.95f), // Royal Violet / Purple
        new Color(1.00f, 0.72f, 0.10f), // Golden Amber
        new Color(0.95f, 0.16f, 0.20f), // Ruby Coral Red
        new Color(0.10f, 0.88f, 0.65f)  // Tropical Seafoam Mint
    };

    private static Material[] _cachedDeviceLabCoralMaterials;
    private static Material[] _cachedKelliRayCoralMaterials;

    private static void PrewarmCoralMaterials(EnvPropSet propSet)
    {
        _cachedDeviceLabCoralMaterials = null;
        _cachedKelliRayCoralMaterials  = null;

        if (propSet == null || propSet.props == null) return;

        foreach (var entry in propSet.props)
        {
            if (entry == null || entry.prefab == null) continue;
            string name = entry.prefab.name;

            if (_cachedDeviceLabCoralMaterials == null && (name.Contains("Orange Coral") || name.Contains("Device Lab")))
            {
                var r = entry.prefab.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterials.Length > 1)
                {
                    Material baseMat = r.sharedMaterials[1];
                    _cachedDeviceLabCoralMaterials = new Material[CoralPalette.Length];
                    for (int i = 0; i < CoralPalette.Length; i++)
                    {
                        Material m = new Material(baseMat);
                        m.name = $"DeviceLabCoral_{i}";
                        m.SetColor("_BaseColor", CoralPalette[i]);
                        m.SetColor("_Color", CoralPalette[i]);
                        m.SetColor("baseColorFactor", CoralPalette[i]);
                        if (m.HasProperty("_Color")) m.color = CoralPalette[i];
                        _cachedDeviceLabCoralMaterials[i] = m;
                    }
                }
            }
            else if (_cachedKelliRayCoralMaterials == null && (name.Contains("Kelli Ray") || name.Contains("coral by Kelli")))
            {
                var r = entry.prefab.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    Material baseMat = r.sharedMaterial;
                    _cachedKelliRayCoralMaterials = new Material[CoralPalette.Length];
                    for (int i = 0; i < CoralPalette.Length; i++)
                    {
                        Material m = new Material(baseMat);
                        m.name = $"KelliRayCoral_{i}";
                        Color tint = Color.Lerp(Color.white, CoralPalette[i], 0.75f);
                        m.SetColor("_BaseColor", tint);
                        m.SetColor("_Color", tint);
                        m.SetColor("baseColorFactor", tint);
                        if (m.HasProperty("_Color")) m.color = tint;
                        _cachedKelliRayCoralMaterials[i] = m;
                    }
                }
            }
        }
    }

    private static void ApplyCoralColorVariation(GameObject go, int colorIndex)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        if (colorIndex < 0) colorIndex = Random.Range(0, CoralPalette.Length);
        colorIndex = colorIndex % CoralPalette.Length;

        string goName = go.name;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (goName.Contains("Orange Coral") || goName.Contains("Device Lab"))
            {
                if (_cachedDeviceLabCoralMaterials != null && colorIndex < _cachedDeviceLabCoralMaterials.Length)
                {
                    var mats = r.sharedMaterials;
                    if (mats.Length > 1)
                    {
                        mats[1] = _cachedDeviceLabCoralMaterials[colorIndex];
                        r.sharedMaterials = mats;
                    }
                    else
                    {
                        r.sharedMaterial = _cachedDeviceLabCoralMaterials[colorIndex];
                    }
                    continue;
                }
            }
            else if (goName.Contains("Kelli Ray") || goName.Contains("coral by Kelli"))
            {
                if (_cachedKelliRayCoralMaterials != null && colorIndex < _cachedKelliRayCoralMaterials.Length)
                {
                    r.sharedMaterial = _cachedKelliRayCoralMaterials[colorIndex];
                    continue;
                }
            }

            // Fallback for any other multi-material or generic coral model
            if (r.sharedMaterials.Length > 1)
            {
                var mats = r.sharedMaterials;
                Material clone = new Material(mats[1]);
                clone.SetColor("_BaseColor", CoralPalette[colorIndex]);
                clone.SetColor("_Color", CoralPalette[colorIndex]);
                clone.SetColor("baseColorFactor", CoralPalette[colorIndex]);
                if (clone.HasProperty("_Color")) clone.color = CoralPalette[colorIndex];
                mats[1] = clone;
                r.sharedMaterials = mats;
            }
            else if (r.sharedMaterial != null)
            {
                Material clone = new Material(r.sharedMaterial);
                Color tint = Color.Lerp(Color.white, CoralPalette[colorIndex], 0.70f);
                clone.SetColor("_BaseColor", tint);
                clone.SetColor("_Color", tint);
                clone.SetColor("baseColorFactor", tint);
                if (clone.HasProperty("_Color")) clone.color = tint;
                r.sharedMaterial = clone;
            }
        }
    }

    private static void ApplyPropMaterial(GameObject go, Material materialOverride)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        // Protect custom modeled props from default rock shader override
        if (go.name.IndexOf("seagrass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            go.name.IndexOf("clam", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            go.name.IndexOf("shell", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // 1. Explicit material recipe override from EnvPropSet
            if (materialOverride != null)
            {
                r.sharedMaterial = materialOverride;
                continue;
            }

            // 2. Catch default / untextured materials from Blender (e.g. "Material", "Material.001", "Default-Material")
            string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
            bool hasTexture = r.sharedMaterial != null && r.sharedMaterial.mainTexture != null;
            bool isColored = r.sharedMaterial != null && (r.sharedMaterial.color.r < 0.75f || r.sharedMaterial.color.g < 0.75f || r.sharedMaterial.color.b < 0.75f);

            bool isUntexturedWhite = !hasTexture && !isColored && (r.sharedMaterial == null
                                  || matName.StartsWith("Default", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.Equals("Lit", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.Equals("Universal Render Pipeline/Lit", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.StartsWith("Material", System.StringComparison.OrdinalIgnoreCase));

            if (isUntexturedWhite)
            {
                if (_cachedRockMaterial == null)
                {
                    _cachedRockMaterial = MaterialUtils.CreateColoredMaterial(
                        new Color(123f / 255f, 140f / 255f, 120f / 255f, 1.0f),
                        0.15f,
                        0.0f
                    );
                    _cachedRockMaterial.name = "ProceduralMarineLimestone";
                }

                if (_cachedRockMaterial != null)
                {
                    r.sharedMaterial = _cachedRockMaterial;
                }
            }
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
