using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads SpeciesRegistry and spawns species into the current zone following
/// strict ecological placement rules:
///   1. Exact depth range match (minDepthFraction to maxDepthFraction).
///   2. Biome preference match (Hard reef, Soft seagrass, Rock boulders, Open water).
///   3. Raycast ground/rock snapping for stationary species.
///
/// Mobile species     Ã¢â€ â€™ Rigidbody + ContextSteering + SpeciesAI (swims in depth band).
/// Stationary species Ã¢â€ â€™ snaps to rocks/reef structures/seabed via Raycast,
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

        // Deterministic seeding based on generated map seed & zone
        if (terrain != null)
        {
            int seedVal = Mathf.RoundToInt(terrain.PcgSeed * 1337f + zoneIndex * 7919f);
            Random.InitState(seedVal);
        }

        EnsureCreatureParent();
        ClearExistingCreatures();

        var allSpecies = registry.GetSpeciesAvailableForZone(zoneIndex);
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
        float upperDepthLimit = -Mathf.Min(data.minDepthFraction, data.maxDepthFraction) * D;
        float lowerDepthLimit = -Mathf.Max(data.minDepthFraction, data.maxDepthFraction) * D;

        float halfW = W * 0.44f;
        float halfL = L * 0.44f;

        Vector3 bestCandidatePos = Vector3.zero;
        Quaternion bestCandidateRot = Quaternion.identity;
        float bestScore = float.MinValue;

        // ── Phase 1: Random Search (Strict Habitat + Depth Band + Separation) ──
        int attempts = Mathf.Max(maxAttempts, 80);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float rx = Random.Range(-halfW, halfW);
            float rz = Random.Range(-halfL, halfL);

            BiomeBand biome = terrain != null ? terrain.GetBiomeAt(rx, rz) : data.preferredBiome;
            float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -D;

            Vector3 candidatePos;
            Quaternion candidateRot;

            if (data.isStationary)
            {
                // Benthic / stationary species MUST be planted on the ocean floor or reef structures.
                Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    candidatePos = hit.point;
                    Quaternion surfaceAlign = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    candidateRot = surfaceAlign * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                else
                {
                    candidatePos = new Vector3(rx, floorY, rz);
                    candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
            }
            else
            {
                // Mobile species: swim in depth band, safely above floor
                float minSwimY = Mathf.Max(lowerDepthLimit, floorY + 2.5f);
                float maxSwimY = Mathf.Min(upperDepthLimit, -1.5f);
                if (minSwimY > maxSwimY)
                {
                    // Water column too shallow for this depth band here
                    continue;
                }
                float ry = Random.Range(minSwimY, maxSwimY);
                candidatePos = new Vector3(rx, ry, rz);
                candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            // Track candidate score for fallback
            float score = EvaluateCandidate(candidatePos, biome, data, lowerDepthLimit, upperDepthLimit, floorY);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidatePos = candidatePos;
                bestCandidateRot = candidateRot;
            }

            // Strict check: Biome must match preferred habitat
            if (biome != data.preferredBiome) continue;

            // Strict check: Depth for stationary species
            if (data.isStationary)
            {
                if (candidatePos.y < lowerDepthLimit - 3f || candidatePos.y > upperDepthLimit + 3f)
                    continue;
            }

            // Separation check
            if (!IsSeparated(candidatePos, minSeparation)) continue;

            resultPos = candidatePos;
            resultRot = candidateRot;
            return true;
        }

        // ── Phase 2: Targeted Grid Search (Guarantee Habitat & Closest Depth) ──
        int gridSteps = 16;
        float stepX = (halfW * 2f) / gridSteps;
        float stepZ = (halfL * 2f) / gridSteps;

        for (int ix = 0; ix < gridSteps; ix++)
        {
            for (int iz = 0; iz < gridSteps; iz++)
            {
                float rx = -halfW + (ix + Random.Range(0.2f, 0.8f)) * stepX;
                float rz = -halfL + (iz + Random.Range(0.2f, 0.8f)) * stepZ;

                BiomeBand biome = terrain != null ? terrain.GetBiomeAt(rx, rz) : data.preferredBiome;
                if (biome != data.preferredBiome && !IsCompatibleBiome(biome, data.preferredBiome))
                    continue;

                float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -D;
                Vector3 candidatePos;
                Quaternion candidateRot;

                if (data.isStationary)
                {
                    Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        candidatePos = hit.point;
                        Quaternion surfaceAlign = Quaternion.FromToRotation(Vector3.up, hit.normal);
                        candidateRot = surfaceAlign * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    }
                    else
                    {
                        candidatePos = new Vector3(rx, floorY, rz);
                        candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    }
                }
                else
                {
                    float minSwimY = Mathf.Max(lowerDepthLimit, floorY + 2.5f);
                    float maxSwimY = Mathf.Min(upperDepthLimit, -1.5f);
                    if (minSwimY > maxSwimY) continue;
                    float ry = Random.Range(minSwimY, maxSwimY);
                    candidatePos = new Vector3(rx, ry, rz);
                    candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                float score = EvaluateCandidate(candidatePos, biome, data, lowerDepthLimit, upperDepthLimit, floorY);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidatePos = candidatePos;
                    bestCandidateRot = candidateRot;
                }

                // In Phase 2, accept relaxed separation (50%)
                if (IsSeparated(candidatePos, minSeparation * 0.5f))
                {
                    if (biome == data.preferredBiome)
                    {
                        resultPos = candidatePos;
                        resultRot = candidateRot;
                        return true;
                    }
                }
            }
        }

        // ── Phase 3: Guaranteed Placement Fallback ──
        if (bestCandidatePos != Vector3.zero)
        {
            if (data.isStationary)
            {
                float floorY = terrain != null ? terrain.SampleHeight(bestCandidatePos.x, bestCandidatePos.z) : bestCandidatePos.y;
                Vector3 rayOrigin = new Vector3(bestCandidatePos.x, floorY + 30f, bestCandidatePos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    bestCandidatePos = hit.point;
                    bestCandidateRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                else
                {
                    bestCandidatePos.y = floorY;
                }
            }
            resultPos = bestCandidatePos;
            resultRot = bestCandidateRot;
            return true;
        }

        // Final safety fallback if terrain sampling was completely degenerate
        float fallbackX = Random.Range(-halfW * 0.5f, halfW * 0.5f);
        float fallbackZ = Random.Range(-halfL * 0.5f, halfL * 0.5f);
        if (data.isStationary)
        {
            float floorY = terrain != null ? terrain.SampleHeight(fallbackX, fallbackZ) : -D;
            Vector3 rayOrigin = new Vector3(fallbackX, floorY + 30f, fallbackZ);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
            {
                resultPos = hit.point;
                resultRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else
            {
                resultPos = new Vector3(fallbackX, floorY, fallbackZ);
                resultRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }
        else
        {
            float fallbackY = Mathf.Clamp(Mathf.Lerp(lowerDepthLimit, upperDepthLimit, 0.5f), -D + 5f, -2f);
            resultPos = new Vector3(fallbackX, fallbackY, fallbackZ);
            resultRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        return true;
    }

    private bool IsSeparated(Vector3 pos, float minDist)
    {
        for (int i = 0; i < _usedPositions.Count; i++)
        {
            if (Vector3.Distance(pos, _usedPositions[i]) < minDist)
                return false;
        }
        return true;
    }

    private float EvaluateCandidate(Vector3 pos, BiomeBand biome, SpeciesData data, float lowerDepth, float upperDepth, float floorY)
    {
        float score = 0f;

        // 1. Biome alignment score
        if (biome == data.preferredBiome) score += 400f;
        else if (IsCompatibleBiome(biome, data.preferredBiome)) score += 150f;

        // 2. Depth alignment score
        float depthDist = 0f;
        if (data.isStationary)
        {
            if (pos.y < lowerDepth) depthDist = lowerDepth - pos.y;
            else if (pos.y > upperDepth) depthDist = pos.y - upperDepth;
        }
        else
        {
            if (pos.y < lowerDepth) depthDist = lowerDepth - pos.y;
            else if (pos.y > upperDepth) depthDist = pos.y - upperDepth;
            if (pos.y < floorY + 2f) depthDist += (floorY + 2f - pos.y) * 2f;
        }
        score += Mathf.Max(0f, 300f - depthDist * 4f);

        // 3. Spacing score
        float nearest = float.MaxValue;
        for (int i = 0; i < _usedPositions.Count; i++)
        {
            float d = Vector3.Distance(pos, _usedPositions[i]);
            if (d < nearest) nearest = d;
        }
        if (nearest < minSeparation) score -= (minSeparation - nearest) * 20f;
        else score += 100f;

        return score;
    }

    private static bool IsCompatibleBiome(BiomeBand a, BiomeBand b)
    {
        if (a == b) return true;
        if ((a == BiomeBand.Hard && b == BiomeBand.Rock) || (a == BiomeBand.Rock && b == BiomeBand.Hard)) return true;
        if ((a == BiomeBand.Soft && b == BiomeBand.OpenWater) || (a == BiomeBand.OpenWater && b == BiomeBand.Soft)) return true;
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
        // Align bottom of object to surface so it doesn't sink into the seabed/rocks
        AdjustContactHeight(go);

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

        var trackable = go.GetComponent<SonarTrackable>();
        if (trackable == null) trackable = go.AddComponent<SonarTrackable>();
        bool discovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(data.zoneIndex, data.speciesId);
        trackable.Initialize(SonarTrackable.SonarTargetType.Species, data.commonName, discovered);
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
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = data.placeholderScale != Vector3.zero ? data.placeholderScale * 1.1f : Vector3.one * 1.2f;
            col = box;
        }
        // Solid collider so physics stops creature from passing through seabed and rocks
        col.isTrigger = false;

        try { go.tag = "Species"; } catch { }

        var cs = go.GetComponent<ContextSteering>();
        if (cs == null) cs = go.AddComponent<ContextSteering>();

        var ai = go.GetComponent<SpeciesAI>();
        if (ai == null) ai = go.AddComponent<SpeciesAI>();
        ai.Initialize(data, spawnCenter);

        var trackable = go.GetComponent<SonarTrackable>();
        if (trackable == null) trackable = go.AddComponent<SonarTrackable>();
        bool discovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(data.zoneIndex, data.speciesId);
        trackable.Initialize(SonarTrackable.SonarTargetType.Species, data.commonName, discovered);
    }

    private void AdjustContactHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combinedBounds.Encapsulate(renderers[i].bounds);

            float bottomY = combinedBounds.min.y;
            float pivotY  = go.transform.position.y;
            float bottomOffset = pivotY - bottomY;

            // Lift so the bottom sits flush on the surface with tiny natural embedding (5%)
            float height = combinedBounds.size.y;
            float sink = height * 0.05f;
            Vector3 pos = go.transform.position;
            pos.y = pos.y + bottomOffset - sink;
            go.transform.position = pos;
        }
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
