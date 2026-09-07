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
    [Tooltip("Maximum ground slope angle (degrees) for stationary benthic species. Prevents floating overhangs on steep slopes/cliffs.")]
    [SerializeField] private float maxStationarySlope = 18f;

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

        // Register all creatures with distance culling for performance
        if (DistanceCullingManager.Instance != null && creatureParent != null)
            DistanceCullingManager.Instance.RegisterParent(creatureParent);
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
        float rawLowerLimit   = -Mathf.Max(data.minDepthFraction, data.maxDepthFraction) * D;
        // Mobile swimming species must never spawn in the bottom boundary popup zone
        float lowerDepthLimit = data.isStationary ? rawLowerLimit : Mathf.Max(rawLowerLimit, -D + 16f);

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
                Vector3 surfaceNormal = terrain != null ? terrain.SampleNormal(rx, rz) : Vector3.up;
                float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);

                // Pillar 1: Filter out steep slopes based on species anatomy (gentle slope for wide branching corals)
                float maxSlope = GetMaxSlopeForSpecies(data);
                if (slopeAngle > maxSlope)
                    continue;

                // Footprint clearance check: prevents wide branches/bases from intersecting dunes or ridges
                if (!CheckFootprintFlatness(rx, rz, data))
                    continue;

                Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    candidatePos = hit.point;
                    if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f)
                        surfaceNormal = hit.normal;
                }
                else
                {
                    candidatePos = new Vector3(rx, floorY, rz);
                }

                // Pillar 2: Natural, biologically accurate rotation (upright for branching corals & sponges, aligned for crawlers)
                candidateRot = GetStationaryRotation(data, surfaceNormal);
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
                    Vector3 surfaceNormal = terrain != null ? terrain.SampleNormal(rx, rz) : Vector3.up;
                    float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);

                    float maxSlope = GetMaxSlopeForSpecies(data);
                    if (slopeAngle > maxSlope * 1.25f)
                        continue;

                    if (!CheckFootprintFlatness(rx, rz, data))
                        continue;

                    Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        candidatePos = hit.point;
                        if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f)
                            surfaceNormal = hit.normal;
                    }
                    else
                    {
                        candidatePos = new Vector3(rx, floorY, rz);
                    }

                    candidateRot = GetStationaryRotation(data, surfaceNormal);
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
                Vector3 normal = terrain != null ? terrain.SampleNormal(bestCandidatePos.x, bestCandidatePos.z) : Vector3.up;
                Vector3 rayOrigin = new Vector3(bestCandidatePos.x, floorY + 30f, bestCandidatePos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    bestCandidatePos = hit.point;
                    if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f) normal = hit.normal;
                }
                else
                {
                    bestCandidatePos.y = floorY;
                }
                bestCandidateRot = GetStationaryRotation(data, normal);
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
            Vector3 normal = terrain != null ? terrain.SampleNormal(fallbackX, fallbackZ) : Vector3.up;
            Vector3 rayOrigin = new Vector3(fallbackX, floorY + 30f, fallbackZ);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
            {
                resultPos = hit.point;
                if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f) normal = hit.normal;
            }
            else
            {
                resultPos = new Vector3(fallbackX, floorY, fallbackZ);
            }
            resultRot = GetStationaryRotation(data, normal);
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

        // 4. Slope flatness score for stationary species (favors flat plateaus & basins)
        if (data.isStationary && terrain != null)
        {
            Vector3 normal = terrain.SampleNormal(pos.x, pos.z);
            float slope = Vector3.Angle(Vector3.up, normal);
            float maxSlope = GetMaxSlopeForSpecies(data);
            if (slope <= maxSlope)
                score += (maxSlope - slope) * 25f;
            else
                score -= (slope - maxSlope) * 50f;
        }

        return score;
    }

    private float GetMaxSlopeForSpecies(SpeciesData data)
    {
        if (data == null || !data.isStationary) return 90f;
        // Wide branching corals need flat ground to prevent branches hitting or getting buried in slopes
        if (data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0) return 8f;
        if (data.taxonomicClass == TaxonomicClass.Anthozoa) return 12f;
        if (data.taxonomicClass == TaxonomicClass.Demospongiae) return 12f;
        if (data.taxonomicClass == TaxonomicClass.Bivalvia) return 14f;
        return maxStationarySlope;
    }

    private bool CheckFootprintFlatness(float cx, float cz, SpeciesData data)
    {
        if (data == null || !data.isStationary || terrain == null) return true;

        float checkRadius = 0f;
        float maxAllowedVariance = 0f;

        if (data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Fan Coral is ~7.6m wide! Check 2.8m radius to ensure branches stay in clear water
            checkRadius = 2.8f;
            maxAllowedVariance = 0.35f;
        }
        else if (data.taxonomicClass == TaxonomicClass.Anthozoa ||
                 data.taxonomicClass == TaxonomicClass.Demospongiae ||
                 data.taxonomicClass == TaxonomicClass.Bivalvia)
        {
            checkRadius = 1.2f;
            maxAllowedVariance = 0.25f;
        }
        else
        {
            // Small creeping species (Starfish, Snail, Lobster) do not require broad clearance checks
            return true;
        }

        float centerH = terrain.SampleHeight(cx, cz);
        float h0 = terrain.SampleHeight(cx + checkRadius, cz);
        float h1 = terrain.SampleHeight(cx - checkRadius, cz);
        float h2 = terrain.SampleHeight(cx, cz + checkRadius);
        float h3 = terrain.SampleHeight(cx, cz - checkRadius);

        if (Mathf.Abs(h0 - centerH) > maxAllowedVariance ||
            Mathf.Abs(h1 - centerH) > maxAllowedVariance ||
            Mathf.Abs(h2 - centerH) > maxAllowedVariance ||
            Mathf.Abs(h3 - centerH) > maxAllowedVariance)
        {
            return false;
        }

        return true;
    }

    private Quaternion GetStationaryRotation(SpeciesData data, Vector3 surfaceNormal)
    {
        Quaternion randomYaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // 1. Upright branching colonies & tall sponges grow straight up against gravity
        if (data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            data.taxonomicClass == TaxonomicClass.Demospongiae)
        {
            return randomYaw;
        }

        // 2. Anemones and Giant Clams: mostly upright, subtle slope adaptation (max 8 degrees)
        if (data.taxonomicClass == TaxonomicClass.Anthozoa || data.taxonomicClass == TaxonomicClass.Bivalvia)
        {
            Vector3 blendedNormal = Vector3.Slerp(Vector3.up, surfaceNormal, 0.35f);
            return Quaternion.FromToRotation(Vector3.up, blendedNormal) * randomYaw;
        }

        // 3. Flat crawling species (Starfish, Snail, Lobster): cling to surface normal (clamped to 22 deg)
        Vector3 clampedNormal = surfaceNormal;
        if (Vector3.Angle(Vector3.up, surfaceNormal) > 22f)
        {
            clampedNormal = Vector3.RotateTowards(Vector3.up, surfaceNormal, 22f * Mathf.Deg2Rad, 0f);
        }
        return Quaternion.FromToRotation(Vector3.up, clampedNormal) * randomYaw;
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
        // 1. Remove any Rigidbodies on stationary species so physics NEVER simulates or moves them
        var rbs = go.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] != null)
            {
                rbs[i].isKinematic = true;
                Destroy(rbs[i]);
            }
        }

        // 2. Set all colliders on stationary species to triggers (for scanning / interaction, no physics push)
        var allCols = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < allCols.Length; i++)
        {
            if (allCols[i] != null)
                allCols[i].isTrigger = true;
        }

        if (allCols.Length == 0)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = data.placeholderScale != Vector3.zero ? data.placeholderScale * 1.5f : Vector3.one * 2f;
            box.isTrigger = true;
        }

        // 3. Align bottom of object to surface so it sits firmly on the seabed/rocks
        AdjustContactHeight(go, data);

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

    private void AdjustContactHeight(GameObject go, SpeciesData data)
    {
        // Temporarily disable the creature's own colliders so our ground-finding
        // raycast passes through to the actual terrain surface beneath.
        var ownColliders = go.GetComponentsInChildren<Collider>();
        bool[] wasEnabled = new bool[ownColliders.Length];
        for (int i = 0; i < ownColliders.Length; i++)
        {
            wasEnabled[i] = ownColliders[i].enabled;
            ownColliders[i].enabled = false;
        }

        try
        {
            int terrainMask = LayerMask.GetMask("Terrain", "Default");
            if (terrainMask == 0) terrainMask = ~0;

            float SampleGroundAt(float wx, float wz)
            {
                Vector3 ray = new Vector3(wx, go.transform.position.y + 25f, wz);
                if (Physics.Raycast(ray, Vector3.down, out RaycastHit h, 60f, terrainMask, QueryTriggerInteraction.Ignore))
                    return h.point.y;
                if (terrain != null)
                    return terrain.SampleHeight(wx, wz);
                return go.transform.position.y;
            }

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combinedBounds.Encapsulate(renderers[i].bounds);

                float pivotX = go.transform.position.x;
                float pivotZ = go.transform.position.z;
                float gPivot = SampleGroundAt(pivotX, pivotZ);

                // Distance from object transform to the lowest visual vertex in world space
                float bottomOffset = combinedBounds.min.y - go.transform.position.y;

                // Subtle natural embed depth into the seabed sand:
                // - Starfish: 1.5 cm (so it lies flat on the seabed without getting submerged)
                // - Snails & Lobsters: 2 cm
                // - Fan Coral: 3 cm (anchors the central trunk in sand without burying branches)
                // - Sponges: 3 cm
                // - Anemone & Giant Clam: 4 cm (fleshy base / shell rests in sediment)
                float sink = 0.03f;
                bool isCoral = data != null && data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (data != null)
                {
                    if (data.taxonomicClass == TaxonomicClass.Asteroidea)
                        sink = 0.015f;
                    else if (data.taxonomicClass == TaxonomicClass.Gastropoda || data.taxonomicClass == TaxonomicClass.Malacostraca)
                        sink = 0.02f;
                    else if (isCoral)
                        sink = 0.03f;
                    else if (data.taxonomicClass == TaxonomicClass.Demospongiae)
                        sink = 0.03f;
                    else if (data.taxonomicClass == TaxonomicClass.Anthozoa || data.taxonomicClass == TaxonomicClass.Bivalvia)
                        sink = 0.04f;
                }

                // For species with wide fleshy/flat base pads (like Sea Anemones), check nearby ground
                // to avoid downhill floating, but clamp strictly to prevent pulling uphill/center down.
                float groundTargetY = gPivot;
                if (!isCoral && data != null && (data.taxonomicClass == TaxonomicClass.Anthozoa || data.taxonomicClass == TaxonomicClass.Bivalvia))
                {
                    float baseR = Mathf.Clamp(combinedBounds.extents.x * 0.25f, 0.2f, 0.5f);
                    float g0 = SampleGroundAt(pivotX - baseR, pivotZ);
                    float g1 = SampleGroundAt(pivotX + baseR, pivotZ);
                    float g2 = SampleGroundAt(pivotX, pivotZ - baseR);
                    float g3 = SampleGroundAt(pivotX, pivotZ + baseR);
                    float minEdge = Mathf.Min(g0, Mathf.Min(g1, Mathf.Min(g2, g3)));

                    // Never pull the base more than 4cm below center ground
                    groundTargetY = Mathf.Max(gPivot - 0.04f, Mathf.Lerp(gPivot, minEdge, 0.4f));
                }

                Vector3 pos = go.transform.position;
                pos.y = groundTargetY - bottomOffset - sink;
                go.transform.position = pos;
            }
            else
            {
                float groundY = SampleGroundAt(go.transform.position.x, go.transform.position.z);
                Vector3 pos = go.transform.position;
                pos.y = groundY;
                go.transform.position = pos;
            }
        }
        finally
        {
            // Always re-enable colliders
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] != null)
                    ownColliders[i].enabled = wasEnabled[i];
            }
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
