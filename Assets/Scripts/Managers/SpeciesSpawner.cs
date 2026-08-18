using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads SpeciesRegistry and spawns species into the current zone following
/// strict ecological placement rules:
///   1. Exact depth range match (minDepthFraction to maxDepthFraction).
///   2. Biome preference match (Hard reef, Soft seagrass, Rock boulders, Open water).
///   3. Raycast ground/rock snapping for stationary species.
///
/// Mobile species     → Rigidbody + ContextSteering + SpeciesAI (swims in depth band).
/// Stationary species → snaps to rocks/reef structures/seabed via Raycast,
///                      aligns to surface normal, gets BoxCollider trigger + ScanTarget.
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
    [SerializeField] private int   maxAttempts   = 60;
    [Tooltip("Minimum distance between two spawned entities.")]
    [SerializeField] private float minSeparation = 5f;
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
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null && !terrain.HasGenerated)
        {
            // TerrainGenerator will call SpawnForZone once mesh & props are baked!
            return;
        }
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
        ClearExistingCreatures();

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
            if (TryFindSpawnPosition(data, out Vector3 pos, out Quaternion rot))
            {
                SpawnInstance(data, pos, rot);
                _usedPositions.Add(pos);
                spawned++;
            }
        }
        if (spawned < data.instanceCount)
            Debug.LogWarning($"[SpeciesSpawner] '{data.commonName}': placed {spawned}/{data.instanceCount}.");
        return spawned;
    }

    private bool TryFindSpawnPosition(SpeciesData data, out Vector3 resultPos, out Quaternion resultRot)
    {
        float W = _zoneDef != null ? _zoneDef.playableWidth : 600f;
        float L = _zoneDef != null ? _zoneDef.playableLength : 600f;
        float D = _zoneDef != null ? _zoneDef.playableDepth : 200f;

        resultPos = Vector3.zero;
        resultRot = Quaternion.identity;

        int terrainLayerMask = LayerMask.GetMask("Terrain", "Default");
        if (terrainLayerMask == 0) terrainLayerMask = ~0;

        // Depth bounds in world Y coordinates:
        // minDepthFraction = 0 (surface Y=0m), maxDepthFraction = 1 (floor Y=-Dm)
        float depthMinY = Mathf.Lerp(0f, -D, data.minDepthFraction);
        float depthMaxY = Mathf.Lerp(0f, -D, data.maxDepthFraction);
        float upperDepthLimit = Mathf.Max(depthMinY, depthMaxY);
        float lowerDepthLimit = Mathf.Min(depthMinY, depthMaxY);

        Vector3 bestCandidatePos = Vector3.zero;
        Quaternion bestCandidateRot = Quaternion.identity;
        float bestDepthDiff = float.MaxValue;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float rx = Random.Range(-W * 0.44f, W * 0.44f);
            float rz = Random.Range(-L * 0.44f, L * 0.44f);

            float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -D;
            Vector3 candidatePos;
            Quaternion candidateRot = Quaternion.identity;

            if (data.isStationary)
            {
                // Raycast downward to snap onto rocks, reef props, or the seabed floor
                Vector3 rayOrigin = new Vector3(rx, 5f, rz);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, D + 50f, terrainLayerMask))
                {
                    candidatePos = hit.point;
                    Quaternion surfaceAlign = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    Quaternion randomSpin  = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    candidateRot = surfaceAlign * randomSpin;
                }
                else
                {
                    candidatePos = new Vector3(rx, floorY, rz);
                    candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                // Check depth difference to preferred biological depth window
                float depthDiff = 0f;
                if (candidatePos.y > upperDepthLimit) depthDiff = candidatePos.y - upperDepthLimit;
                else if (candidatePos.y < lowerDepthLimit) depthDiff = lowerDepthLimit - candidatePos.y;

                if (depthDiff < bestDepthDiff)
                {
                    bestDepthDiff = depthDiff;
                    bestCandidatePos = candidatePos;
                    bestCandidateRot = candidateRot;
                }

                // Strict biological match for first 25 attempts
                float tolerance = attempt > 25 ? 15f + (attempt - 25) * 1.5f : 4f;
                if (depthDiff > tolerance)
                {
                    continue; // Keep searching for a closer reef summit or rock structure
                }
            }
            else
            {
                // Mobile species: swim in depth band, stay safely above the floor
                float ry = Random.Range(lowerDepthLimit, upperDepthLimit);
                ry       = Mathf.Clamp(ry, floorY + 2.5f, -1.5f);

                candidatePos = new Vector3(rx, ry, rz);
                candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            // Minimum separation check against already placed entities
            bool tooClose = false;
            foreach (var used in _usedPositions)
            {
                if (Vector3.Distance(candidatePos, used) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            resultPos = candidatePos;
            resultRot = candidateRot;
            return true;
        }

        // Fallback: use best candidate found during search
        if (bestCandidatePos != Vector3.zero)
        {
            resultPos = bestCandidatePos;
            resultRot = bestCandidateRot;
            return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // Instantiation
    // -----------------------------------------------------------------------

    private void SpawnInstance(SpeciesData data, Vector3 position, Quaternion rotation)
    {
        GameObject go = data.modelPrefab != null
            ? Instantiate(data.modelPrefab, position, rotation, creatureParent)
            : CreatePlaceholder(data, position, rotation);

        go.name = $"{data.commonName} [{data.speciesId}]";

        if (data.isStationary) SetupStationary(go, data);
        else                   SetupMobile(go, data, position);
    }

    private GameObject CreatePlaceholder(SpeciesData data, Vector3 position, Quaternion rotation)
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
        go.transform.rotation   = rotation;
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
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
        }
        rb.useGravity     = false;
        rb.freezeRotation = true;

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

    private void ClearExistingCreatures()
    {
        if (creatureParent == null) return;
        for (int i = creatureParent.childCount - 1; i >= 0; i--)
        {
            Destroy(creatureParent.GetChild(i).gameObject);
        }
    }
}
