using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads SpeciesRegistry and spawns species into the current zone following
/// ecological placement rules (biome band + depth fraction match).
///
/// Mobile species  ? gets Rigidbody + ContextSteering + SpeciesAI.
/// Stationary      ? gets SphereCollider trigger + ScanTarget.
/// Both types are detected by ScannerSystem via OverlapSphere.
///
/// Setup:
///   Place on a GameObject in each zone scene alongside TerrainGenerator.
///   Assign registry + terrain references in the Inspector.
///   ZoneManager calls SpawnForZone(zoneIndex) after TerrainGenerator.Generate().
/// </summary>
public class SpeciesSpawner : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Registry + Terrain")]
    [SerializeField] private SpeciesRegistry registry;
    [SerializeField] private TerrainGenerator terrain;

    [Header("Spawn Parameters")]
    [Tooltip("Max random placement attempts per creature instance before giving up.")]
    [SerializeField] private int   maxAttempts   = 30;
    [Tooltip("Minimum distance between two spawned entities.")]
    [SerializeField] private float minSeparation = 8f;
    [Tooltip("Radius used to check for terrain overlap at a candidate position.")]
    [SerializeField] private float overlapRadius = 1.5f;
    [Tooltip("Layer(s) to check against for terrain overlap (should match TerrainGenerator).")]
    [SerializeField] private LayerMask overlapMask;
    [Tooltip("All spawned creatures go here. Auto-created if null.")]
    [SerializeField] private Transform creatureParent;

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private ZoneDefinition      _zoneDef;
    private List<Vector3>       _usedPositions = new List<Vector3>();

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

    public void SpawnForZone(int zoneIndex)
    {
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;
        _zoneDef = ZoneConfig.Zones[zoneIndex];
        _usedPositions.Clear();

        if (registry == null) { Debug.LogWarning("[SpeciesSpawner] SpeciesRegistry not assigned."); return; }
        if (terrain  == null) { Debug.LogWarning("[SpeciesSpawner] TerrainGenerator not assigned.");  return; }

        EnsureCreatureParent();

        var allSpecies = registry.GetSpeciesForZone(zoneIndex);
        int totalSpawned = 0;
        foreach (var data in allSpecies)
        {
            totalSpawned += SpawnSpecies(data);
        }

        Debug.Log($"[SpeciesSpawner] Zone {zoneIndex}: {totalSpawned} entities spawned " +
                  $"({allSpecies.Count} species defined).");
    }

    // -----------------------------------------------------------------------
    // Per-species spawning
    // -----------------------------------------------------------------------

    private int SpawnSpecies(SpeciesData data)
    {
        int spawned = 0;
        for (int i = 0; i < data.instanceCount; i++)
        {
            if (TryFindSpawnPosition(data, out Vector3 pos))
            {
                SpawnInstance(data, pos);
                _usedPositions.Add(pos);
                spawned++;
            }
        }
        if (spawned < data.instanceCount)
            Debug.LogWarning($"[SpeciesSpawner] '{data.commonName}': only placed {spawned}/{data.instanceCount}.");
        return spawned;
    }

    private bool TryFindSpawnPosition(SpeciesData data, out Vector3 result)
    {
        float W = _zoneDef.playableWidth;
        float L = _zoneDef.playableLength;
        float D = _zoneDef.playableDepth;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float rx = Random.Range(-W * 0.5f, W * 0.5f);
            float rz = Random.Range(-L * 0.5f, L * 0.5f);

            // Check biome match
            if (terrain.GetBiomeAt(rx, rz) != data.preferredBiome) continue;

            // Compute Y position
            float ry;
            if (data.isStationary)
            {
                ry = terrain.SeabedY + Random.Range(0.5f, 2f);
            }
            else
            {
                float topY    = Mathf.Lerp(0f, -D, data.minDepthFraction);
                float bottomY = Mathf.Lerp(0f, -D, data.maxDepthFraction);
                ry = Random.Range(Mathf.Min(topY, bottomY), Mathf.Max(topY, bottomY));
            }

            Vector3 candidate = new Vector3(rx, ry, rz);

            // Minimum separation check
            bool tooClose = false;
            foreach (var used in _usedPositions)
            {
                if (Vector3.Distance(candidate, used) < minSeparation) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Terrain overlap check
            if (Physics.CheckSphere(candidate, overlapRadius, overlapMask)) continue;

            result = candidate;
            return true;
        }
        result = Vector3.zero;
        return false;
    }

    // -----------------------------------------------------------------------
    // Instantiation
    // -----------------------------------------------------------------------

    private void SpawnInstance(SpeciesData data, Vector3 position)
    {
        GameObject go = data.modelPrefab != null
            ? Instantiate(data.modelPrefab, position, Quaternion.identity, creatureParent)
            : CreatePlaceholder(data, position);

        go.name = $"{data.commonName} [{data.speciesId}]";

        if (data.isStationary) SetupStationary(go, data);
        else                   SetupMobile(go, data, position);
    }

    private GameObject CreatePlaceholder(SpeciesData data, Vector3 position)
    {
        PrimitiveType pType = data.placeholderShape switch
        {
            PlaceholderShape.Capsule  => PrimitiveType.Capsule,
            PlaceholderShape.Cube     => PrimitiveType.Cube,
            PlaceholderShape.Cylinder => PrimitiveType.Cylinder,
            _                         => PrimitiveType.Sphere,
        };

        var go = GameObject.CreatePrimitive(pType);
        go.transform.SetParent(creatureParent, false);
        go.transform.position   = position;
        go.transform.localScale = data.placeholderScale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Standard")) { color = data.placeholderColor };
        }

        return go;
    }

    private void SetupStationary(GameObject go, SpeciesData data)
    {
        // Ensure a trigger collider for scanner detection
        var col = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 3f;

        var target = go.AddComponent<ScanTarget>();
        target.Initialize(data);
    }

    private void SetupMobile(GameObject go, SpeciesData data, Vector3 spawnCenter)
    {
        // Rigidbody (required by ContextSteering)
        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.useGravity     = false;
        rb.freezeRotation = true;

        // Trigger collider so ScannerSystem OverlapSphere finds it
        var col = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 1.2f;

        try { go.tag = "Species"; } catch { }

        go.AddComponent<ContextSteering>();

        var ai = go.AddComponent<SpeciesAI>();
        ai.Initialize(data, spawnCenter);
    }

    private void EnsureCreatureParent()
    {
        if (creatureParent == null)
            creatureParent = new GameObject("_Creatures").transform;
    }
}
