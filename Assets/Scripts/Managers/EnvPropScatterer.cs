using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters environment prop prefabs (rocks, corals, sponges) on top of the
/// procedural ocean floor mesh in natural geological and ecological clusters
/// (Endless Ocean Luminous style).
///
/// Features:
///   - Cluster-based spawning: creates natural coral gardens and rock outcroppings.
///   - Precise surface alignment & embed depth: props sit realistically on/in the sand.
///   - Elevation & Biome weighting: corals thrive on shallower reef mounds; rocks on slopes.
///   - Obstacle tagging: tagged for ContextSteering avoidance.
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

    /// <summary>
    /// Scatter props across the zone's ocean floor.
    /// </summary>
    public void Scatter(OceanFloorMeshGenerator meshGen, EnvPropSet propSet,
                        float width, float length, float seed)
    {
        if (propSet == null || propSet.props == null || propSet.props.Length == 0)
        {
            Debug.LogWarning("[EnvPropScatterer] No prop set or empty prop list — skipping.");
            return;
        }

        _terrainLayerId = LayerMask.NameToLayer("Terrain");
        EnsurePropParent();
        ClearExistingProps();

        // Calculate total weight for random prop selection
        float totalWeight = 0f;
        foreach (var entry in propSet.props)
        {
            if (entry.prefab != null) totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[EnvPropScatterer] All prop weights are zero — skipping.");
            return;
        }

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

                if (TryPlaceProp(meshGen, propSet, totalWeight, px, pz, placedPositions))
                    placedCount++;
            }
        }

        // ── Phase 2: Scattered Solitary Props (Dune Clutter / Lone Boulders) ─
        int solitaryCount = Mathf.RoundToInt(width * length / 250f * propSet.propDensity);
        for (int s = 0; s < solitaryCount; s++)
        {
            float sx = Random.Range(-halfW, halfW);
            float sz = Random.Range(-halfL, halfL);

            if (TryPlaceProp(meshGen, propSet, totalWeight, sx, sz, placedPositions))
                placedCount++;
        }

        // Restore random state
        Random.state = savedState;

        Debug.Log($"[EnvPropScatterer] Placed {placedCount} natural props in {totalClusters} clusters across {width}x{length}m.");
    }

    // -----------------------------------------------------------------------
    // Placement helper
    // -----------------------------------------------------------------------

    private bool TryPlaceProp(OceanFloorMeshGenerator meshGen, EnvPropSet propSet,
                              float totalWeight, float px, float pz,
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

        // Pick weighted prop
        EnvPropSet.PropEntry entry = PickWeightedProp(propSet.props, totalWeight);
        if (entry == null || entry.prefab == null) return false;

        // Rotation & Scale with globalScaleMultiplier
        float mult  = propSet != null ? Mathf.Max(0.1f, propSet.globalScaleMultiplier) : 1f;
        float scale = Random.Range(entry.minScale, entry.maxScale) * mult;
        Quaternion rot = Quaternion.identity;

        if (entry.alignToSurface)
        {
            // Slerp towards surface normal (tilted naturally with slope)
            Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion randomSpin = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rot = Quaternion.Slerp(randomSpin, slopeAlign * randomSpin, 0.75f);
        }
        else
        {
            rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // Spawn instance at floorY
        Vector3 spawnPos = new Vector3(px, floorY, pz);
        GameObject go = Instantiate(entry.prefab, spawnPos, rot, _propParent);
        go.transform.localScale = Vector3.one * scale;
        go.name = entry.prefab.name;

        // Adjust vertical position to sit naturally on the seabed
        AdjustContactHeight(go, floorY, scale);

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
    private void AdjustContactHeight(GameObject go, float floorY, float scale)
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
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }
    }

    private EnvPropSet.PropEntry PickWeightedProp(EnvPropSet.PropEntry[] props, float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in props)
        {
            if (entry.prefab == null) continue;
            cumulative += entry.weight;
            if (roll <= cumulative) return entry;
        }

        for (int i = props.Length - 1; i >= 0; i--)
        {
            if (props[i].prefab != null) return props[i];
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

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
