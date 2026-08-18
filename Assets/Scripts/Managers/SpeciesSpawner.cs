using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads SpeciesRegistry and spawns species into the current zone following
/// ecological placement rules (biome band + depth fraction match).
///
/// Mobile species  → gets Rigidbody + ContextSteering + SpeciesAI.
/// Stationary      → gets BoxCollider trigger + ScanTarget.
/// Both types are detected by ScannerSystem via OverlapSphere.
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

    private ZoneDefinition _zoneDef;
    private List<Vector3>  _usedPositions = new List<Vector3>();
    private bool           _hasSpawned = false;

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (!_hasSpawned) SpawnForZone(0);
    }

    public void SpawnForZone(int zoneIndex)
    {
        _hasSpawned = true;
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;
        _zoneDef = ZoneConfig.Zones[zoneIndex];
        _usedPositions.Clear();

        if (registry == null)
        {
            registry = Resources.Load<SpeciesRegistry>("SpeciesRegistry");
        }
        if (registry == null) { Debug.LogWarning("[SpeciesSpawner] SpeciesRegistry not found."); return; }

        if (terrain == null)
        {
            terrain = FindFirstObjectByType<TerrainGenerator>();
        }

        EnsureCreatureParent();

        var allSpecies = registry.GetSpeciesForZone(zoneIndex);
        int totalSpawned = 0;
        foreach (var data in allSpecies)
        {
            if (data != null)
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
            Debug.LogWarning($"[SpeciesSpawner] '{data.commonName}': placed {spawned}/{data.instanceCount}.");
        return spawned;
    }

    private bool TryFindSpawnPosition(SpeciesData data, out Vector3 result)
    {
        float W = _zoneDef != null ? _zoneDef.playableWidth : 600f;
        float L = _zoneDef != null ? _zoneDef.playableLength : 600f;
        float D = _zoneDef != null ? _zoneDef.playableDepth : 200f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float rx = Random.Range(-W * 0.45f, W * 0.45f);
            float rz = Random.Range(-L * 0.45f, L * 0.45f);

            // Check biome match (strict for first 15 attempts, relaxed after)
            if (attempt < 15 && terrain != null && terrain.GetBiomeAt(rx, rz) != data.preferredBiome)
                continue;

            // Compute Y position
            float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -D;
            float ry;

            if (data.isStationary)
            {
                // Sit directly on the mesh surface
                ry = floorY + Random.Range(0.2f, 1.2f);
            }
            else
            {
                // Swim in depth band, but never below the floor or above surface
                float topY    = Mathf.Lerp(-2f, -D, data.minDepthFraction);
                float bottomY = Mathf.Lerp(-2f, -D, data.maxDepthFraction);
                ry = Random.Range(Mathf.Min(topY, bottomY), Mathf.Max(topY, bottomY));
                ry = Mathf.Clamp(ry, floorY + 2.5f, -2f);
            }

            Vector3 candidate = new Vector3(rx, ry, rz);

            // Minimum separation check
            bool tooClose = false;
            foreach (var used in _usedPositions)
            {
                if (Vector3.Distance(candidate, used) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

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
        go.transform.localScale = data.placeholderScale != Vector3.zero ? data.placeholderScale : Vector3.one;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            // Use URP Lit shader to avoid pink / violet rendering
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");

            if (shader != null)
            {
                var mat = new Material(shader)
                {
                    color = data.placeholderColor
                };
                rend.material = mat;
            }
        }

        return go;
    }

    private void SetupStationary(GameObject go, SpeciesData data)
    {
        // Safe check for collider without using ?? operator on UnityEngine.Object
        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = data.placeholderScale != Vector3.zero ? data.placeholderScale * 1.5f : Vector3.one * 2f;
            col = box;
        }
        col.isTrigger = true;

        try { go.tag = "Species"; } catch { }

        var target = go.GetComponent<ScanTarget>();
        if (target == null) target = go.AddComponent<ScanTarget>();
        target.Initialize(data);
    }

    private void SetupMobile(GameObject go, SpeciesData data, Vector3 spawnCenter)
    {
        // Rigidbody (required by ContextSteering) - Safe check without ?? operator
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
        }
        rb.useGravity     = false;
        rb.freezeRotation = true;

        // Ensure trigger collider exists for scanner
        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = data.placeholderScale != Vector3.zero ? data.placeholderScale * 1.3f : Vector3.one * 1.5f;
            col = box;
        }
        col.isTrigger = true;

        try { go.tag = "Species"; } catch { }

        var cs = go.GetComponent<ContextSteering>();
        if (cs == null) cs = go.AddComponent<ContextSteering>();

        var ai = go.GetComponent<SpeciesAI>();
        if (ai == null) ai = go.AddComponent<SpeciesAI>();
        ai.Initialize(data, spawnCenter);
    }

    private void EnsureCreatureParent()
    {
        if (creatureParent == null)
        {
            var existing = GameObject.Find("_Creatures");
            if (existing != null)
                creatureParent = existing.transform;
            else
                creatureParent = new GameObject("_Creatures").transform;
        }

        creatureParent.position   = Vector3.zero;
        creatureParent.rotation   = Quaternion.identity;
        creatureParent.localScale = Vector3.one;
    }
}
