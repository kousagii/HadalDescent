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
    [SerializeField] private int clusterCount = 18;

    [Tooltip("Radius of each cluster (meters). Props gather around the cluster center.")]
    [SerializeField] private float clusterRadius = 12f;

    [Tooltip("How many props per cluster.")]
    [SerializeField] private int propsPerCluster = 6;

    [Header("Placement")]
    [Tooltip("Minimum distance between any two props to prevent clipping.")]
    [SerializeField] private float minPropSpacing = 2.5f;

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

        var placedPositions = new List<Vector3>();
        int placedCount = 0;

        float halfW = width  * 0.45f;
        float halfL = length * 0.45f;

        // ── Phase 1: Clustered Prop Formations (Reefs & Rock Outcrops) ──────
        float zoneAreaRatio = (width * length) / (300f * 300f);
        int totalClusters = Mathf.RoundToInt(clusterCount * zoneAreaRatio * propSet.propDensity);
        totalClusters = Mathf.Clamp(totalClusters, 6, 90);

        for (int c = 0; c < totalClusters; c++)
        {
            // Pick cluster center
            float cx = Random.Range(-halfW, halfW);
            float cz = Random.Range(-halfL, halfL);

            int itemsInThisCluster = Random.Range(Mathf.Max(3, propsPerCluster - 2), propsPerCluster + 6);

            for (int p = 0; p < itemsInThisCluster; p++)
            {
                // Position within cluster radius
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                float px = Mathf.Clamp(cx + offset.x, -halfW, halfW);
                float pz = Mathf.Clamp(cz + offset.y, -halfL, halfL);

                if (TryPlaceProp(meshGen, terrain, propSet, px, pz, placedPositions))
                    placedCount++;
            }
        }

        // ── Phase 2: Scattered Solitary Props (Dune Clutter / Lone Boulders) ─
        int solitaryCount = Mathf.RoundToInt(width * length / 250f * propSet.propDensity);
        for (int s = 0; s < solitaryCount; s++)
        {
            float sx = Random.Range(-halfW, halfW);
            float sz = Random.Range(-halfL, halfL);

            if (TryPlaceProp(meshGen, terrain, propSet, sx, sz, placedPositions))
                placedCount++;
        }

        // Restore random state
        Random.state = savedState;

        Debug.Log($"[EnvPropScatterer] Placed {placedCount} natural props in {totalClusters} clusters across {width}x{length}m.");

        // Register all props with distance culling for performance
        if (DistanceCullingManager.Instance != null && _propParent != null)
            DistanceCullingManager.Instance.RegisterParent(_propParent);
    }

    // -----------------------------------------------------------------------
    // Placement helper
    // -----------------------------------------------------------------------

    private bool TryPlaceProp(OceanFloorMeshGenerator meshGen, TerrainGenerator terrain,
                              EnvPropSet propSet, float px, float pz,
                              List<Vector3> placedPositions)
    {
        // Spacing check
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float dx = px - placedPositions[i].x;
            float dz = pz - placedPositions[i].z;
            if (dx * dx + dz * dz < minPropSpacing * minPropSpacing)
                return false;
        }

        // Query height & surface normal from procedural mesh
        float floorY = meshGen.SampleHeight(px, pz);
        Vector3 normal = meshGen.SampleNormal(px, pz);

        // Biome-aware selection (Environment Blueprint Section 4)
        BiomeBand currentBiome = terrain != null ? terrain.GetBiomeAt(px, pz) : BiomeBand.Hard;
        EnvPropSet.PropEntry entry = PickWeightedPropForBiome(propSet.props, currentBiome);
        if (entry == null || entry.prefab == null) return false;

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

        // Spawn instance at floorY
        Vector3 spawnPos = new Vector3(px, floorY, pz);
        GameObject go = Instantiate(entry.prefab, spawnPos, rot, _propParent);
        go.transform.localScale = finalScale;
        go.name = entry.prefab.name;

        // Ensure any child transform offsets (e.g. from editor placement) are zeroed
        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            if (child.localPosition.sqrMagnitude > 1f)
            {
                child.localPosition = Vector3.zero;
            }
        }

        // Ensure rocks/props have an authentic marine stone material instead of default flat white
        ApplyPropMaterial(go, entry.materialOverride);

        // Adjust vertical position to sit naturally on the seabed
        AdjustContactHeight(go, floorY);

        // Assign obstacle layer for ContextSteering
        if (entry.isObstacle && _terrainLayerId >= 0)
            SetLayerRecursive(go, _terrainLayerId);

        // Ensure collider exists
        EnsureCollider(go);

        placedPositions.Add(go.transform.position);
        return true;
    }

    /// <summary>
    /// Calculates the bottom of the object's mesh bounds and adjusts position
    /// so the base embeds slightly into the sand rather than floating.
    /// </summary>
    private void AdjustContactHeight(GameObject go, float floorY)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combinedBounds.Encapsulate(renderers[i].bounds);

            float bottomY = combinedBounds.min.y;
            float height = combinedBounds.size.y;
            float pivotY = go.transform.position.y;

            // Distance from pivot to bottom of model
            float bottomOffset = pivotY - bottomY;

            // Target Y: place bottom at floorY, with natural sink into sand
            float sink = height * naturalSinkFraction;
            float targetY = floorY + bottomOffset - sink;

            Vector3 pos = go.transform.position;
            pos.y = targetY;
            go.transform.position = pos;
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

    private EnvPropSet.PropEntry PickWeightedPropForBiome(EnvPropSet.PropEntry[] props, BiomeBand currentBiome)
    {
        if (props == null || props.Length == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i].prefab != null && props[i].targetBiome == currentBiome)
                totalWeight += props[i].weight;
        }

        // If no props configured for this specific biome, fall back to any available prop
        if (totalWeight <= 0f)
        {
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].prefab != null)
                    totalWeight += props[i].weight;
            }
            if (totalWeight <= 0f) return null;

            float fallbackRoll = Random.Range(0f, totalWeight);
            float cumFallback = 0f;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].prefab == null) continue;
                cumFallback += props[i].weight;
                if (fallbackRoll <= cumFallback) return props[i];
            }
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i].prefab == null || props[i].targetBiome != currentBiome) continue;
            cumulative += props[i].weight;
            if (roll <= cumulative) return props[i];
        }

        return null;
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

    private static void ApplyPropMaterial(GameObject go, Material materialOverride)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

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
            bool isUntexturedWhite = r.sharedMaterial == null
                                  || matName.StartsWith("Material", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.StartsWith("Default", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.Equals("Lit", System.StringComparison.OrdinalIgnoreCase)
                                  || matName.Equals("Universal Render Pipeline/Lit", System.StringComparison.OrdinalIgnoreCase);

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
