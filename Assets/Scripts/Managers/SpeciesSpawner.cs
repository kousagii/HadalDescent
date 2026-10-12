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
    [Tooltip("Global species spawn multiplier (1 = base population as authored in ScriptableObjects).")]
    [Range(1, 4)]
    [SerializeField] private int spawnMultiplier = 1;
    [Tooltip("Max random placement attempts per creature instance before giving up.")]
    [SerializeField] private int   maxAttempts   = 70;
    [Tooltip("Minimum distance between two spawned entities.")]
    [SerializeField] private float minSeparation = 3.5f;
    [Tooltip("Radius used to check for terrain overlap at a candidate position.")]
    [SerializeField] private float overlapRadius = 1.5f;
    [Tooltip("Layer(s) to check against for terrain overlap (should match TerrainGenerator).")]
    [SerializeField] private LayerMask overlapMask;
    [Tooltip("All spawned creatures go here. Auto-created if null.")]
    [SerializeField] private Transform creatureParent;
    [Tooltip("Maximum ground slope angle (degrees) for stationary benthic species. Prevents floating overhangs on steep slopes/cliffs.")]
    [SerializeField] private float maxStationarySlope = 18f;

    [Header("In-Game Scale Variation")]
    [Tooltip("Global toggle to randomize in-game species scales (juvenile to elder). Model previews in Bestiary/UI remain standard reference size.")]
    [SerializeField] private bool enableInGameScaleVariation = true;
    [Tooltip("Global minimum scale multiplier for in-game spawned species (0.75 = 0.75x smaller).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float globalMinScale = 0.75f;
    [Tooltip("Global maximum scale multiplier for in-game spawned species (1.25 = 1.25x bigger).")]
    [Range(1f, 2f)]
    [SerializeField] private float globalMaxScale = 1.25f;
    [Tooltip("Distribution curvature: 1 = linear uniform, 2 = triangular, 3 = smooth bell curve clustering around standard adult size (1.0x).")]
    [Range(1, 3)]
    [SerializeField] private int scaleDistributionCurvature = 3;

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private ZoneDefinition _zoneDef;
    private List<Vector3>  _usedPositions = new List<Vector3>();
    private readonly List<Vector3> _anemonePositions = new List<Vector3>();
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
        if (!_hasSpawned)
        {
            int zoneIdx = DetectCurrentZone();
            SpawnForZone(zoneIdx);
        }
    }

    private int DetectCurrentZone()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Contains("Twilight")) return 1;
        if (sceneName.Contains("Midnight")) return 2;
        if (sceneName.Contains("Abyss"))    return 3;
        if (sceneName.Contains("Hadal"))    return 4;
        if (sceneName.Contains("Sunlight")) return 0;

        if (ZoneManager.Instance != null && ZoneManager.CurrentZoneIndex >= 0)
            return ZoneManager.CurrentZoneIndex;

        return 0;
    }

    public void SpawnForZone(int zoneIndex)
    {
        _hasSpawned = true;
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;
        _zoneDef = ZoneConfig.Zones[zoneIndex];
        _usedPositions.Clear();
        _anemonePositions.Clear();

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

        // Sort so host species (e.g. Bubble-tip Sea Anemone) spawn before their symbiotic partners (Clownfish)
        var sortedSpecies = new List<SpeciesData>(allSpecies);
        sortedSpecies.Sort((a, b) =>
        {
            if (a == null || b == null) return 0;
            bool aAnemone = a.speciesId == "anemone_001" || a.commonName.ToLower().Contains("anemone");
            bool bAnemone = b.speciesId == "anemone_001" || b.commonName.ToLower().Contains("anemone");
            if (aAnemone != bAnemone) return bAnemone.CompareTo(aAnemone);
            if (a.isStationary != b.isStationary)
                return b.isStationary.CompareTo(a.isStationary);
            return 0;
        });

        int totalSpawned = 0;
        foreach (var data in sortedSpecies)
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
        bool isAnemone = data.speciesId == "anemone_001" || data.commonName.ToLower().Contains("anemone");
        bool isDolphin = data.speciesId == "dolphin_001" || data.commonName.ToLower().Contains("dolphin");

        if (spawnMultiplier < 1) spawnMultiplier = 1;
        int targetCount = Mathf.Max(1, data.instanceCount);

        // Idea B: Social Pod Spawning for Indo-Pacific Bottlenose Dolphins
        if (isDolphin)
        {
            return SpawnDolphinPods(data, targetCount);
        }

        for (int i = 0; i < targetCount; i++)
        {
            if (TryFindSpawnPosition(data, out Vector3 pos, out Quaternion rot, out Vector3? wanderCenter))
            {
                SpawnInstance(data, pos, rot, wanderCenter);
                _usedPositions.Add(pos);
                if (isAnemone)
                {
                    _anemonePositions.Add(pos);
                }
                spawned++;
            }
        }
        if (spawned < targetCount)
            Debug.LogWarning($"[SpeciesSpawner] '{data.commonName}': placed {spawned}/{targetCount}.");
        return spawned;
    }

    /// <summary>
    /// Idea B: Spawns dolphins in cohesive social pods (3 to 4 individuals per pod)
    /// cruising in formation within their strictly designated depth layer and habitat.
    /// </summary>
    private int SpawnDolphinPods(SpeciesData data, int targetCount)
    {
        int spawned = 0;
        int remaining = targetCount;

        while (remaining > 0)
        {
            int podSize = Mathf.Min(remaining, Random.Range(3, 5));
            if (remaining <= 2) podSize = remaining;

            // 1. Establish Pod Leader (strictly respects depth range 1m-50m and OpenWater biome)
            if (TryFindSpawnPosition(data, out Vector3 leaderPos, out Quaternion leaderRot))
            {
                SpawnInstance(data, leaderPos, leaderRot, leaderPos);
                _usedPositions.Add(leaderPos);
                spawned++;
                remaining--;

                Vector3 podCenter = leaderPos;
                float leaderYaw = leaderRot.eulerAngles.y;

                // 2. Spawn Pod Companions in loose organic formation around the leader
                for (int c = 1; c < podSize; c++)
                {
                    if (TryFindDolphinPodCompanionPosition(data, podCenter, leaderYaw, out Vector3 compPos, out Quaternion compRot))
                    {
                        // Share podCenter as wander origin so the entire pod cruises together!
                        SpawnInstance(data, compPos, compRot, podCenter);
                        _usedPositions.Add(compPos);
                        spawned++;
                        remaining--;
                    }
                    else if (TryFindSpawnPosition(data, out Vector3 fbPos, out Quaternion fbRot))
                    {
                        SpawnInstance(data, fbPos, fbRot, fbPos);
                        _usedPositions.Add(fbPos);
                        spawned++;
                        remaining--;
                    }
                }
            }
            else
            {
                break;
            }
        }

        if (spawned < targetCount)
            Debug.LogWarning($"[SpeciesSpawner] '{data.commonName}': placed {spawned}/{targetCount} across pods.");
        return spawned;
    }

    /// <summary>
    /// Finds a companion spawn location in loose formation around the pod leader (Idea B),
    /// strictly enforcing depth band (1m - 50m), ground clearance, and separation.
    /// </summary>
    private bool TryFindDolphinPodCompanionPosition(SpeciesData data, Vector3 podCenter, float leaderYaw, out Vector3 resultPos, out Quaternion resultRot)
    {
        float D = _zoneDef != null ? _zoneDef.playableDepth : 200f;
        float upperDepthLimit = -Mathf.Min(data.minDepthFraction, data.maxDepthFraction) * D;
        float rawLowerLimit   = -Mathf.Max(data.minDepthFraction, data.maxDepthFraction) * D;
        float lowerDepthLimit = Mathf.Max(rawLowerLimit, -D + 16f);

        // Pod formation spread: 5.5m to 15.0m horizontal radius
        for (int attempt = 0; attempt < 35; attempt++)
        {
            Vector2 hOffset = Random.insideUnitCircle * Random.Range(5.5f, 15.0f);
            // Compressed vertical depth offset (+/- 2.0m) to keep the pod in the same swimming layer
            float vOffset = Random.Range(-2.0f, 2.0f);

            Vector3 candidatePos = new Vector3(podCenter.x + hOffset.x, podCenter.y + vOffset, podCenter.z + hOffset.y);
            float safeHalfW = _zoneDef != null ? Mathf.Max(40f, (_zoneDef.playableWidth * 0.5f) - 75f) : 225f;
            float safeHalfL = _zoneDef != null ? Mathf.Max(40f, (_zoneDef.playableLength * 0.5f) - 75f) : 225f;
            candidatePos.x = Mathf.Clamp(candidatePos.x, -safeHalfW, safeHalfW);
            candidatePos.z = Mathf.Clamp(candidatePos.z, -safeHalfL, safeHalfL);

            // 1. Biome verification
            BiomeBand biome = terrain != null ? terrain.GetBiomeAt(candidatePos.x, candidatePos.z) : data.preferredBiome;
            if (biome != data.preferredBiome && !IsCompatibleBiome(biome, data.preferredBiome))
                continue;

            // 2. Strict Depth Band and Seabed Clearance Check
            float floorY = terrain != null ? terrain.SampleHeight(candidatePos.x, candidatePos.z) : -D;
            if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
            {
                candidatePos.y = -1.15f;
            }
            else
            {
                float clearance = data.locomotionArchetype == LocomotionArchetype.PelagicCruiser ? 5.5f : 3.0f;
                float minSwimY = Mathf.Max(lowerDepthLimit, floorY + clearance);
                float maxSwimY = Mathf.Min(upperDepthLimit, -3.0f);
                if (minSwimY > maxSwimY) continue;

                candidatePos.y = Mathf.Clamp(candidatePos.y, minSwimY, maxSwimY);
            }

            // 3. Minimum separation between companions (at least 3.2m to prevent overlapping)
            if (!IsSeparated(candidatePos, 3.2f)) continue;

            resultPos = candidatePos;
            // 4. Align cruising yaw with leader (+/- 25 deg organic formation spread)
            float companionYaw = leaderYaw + Random.Range(-25f, 25f);
            resultRot = Quaternion.Euler(0f, companionYaw, 0f);
            return true;
        }

        resultPos = Vector3.zero;
        resultRot = Quaternion.identity;
        return false;
    }

    private bool TryFindSpawnPosition(SpeciesData data, out Vector3 resultPos, out Quaternion resultRot)
    {
        return TryFindSpawnPosition(data, out resultPos, out resultRot, out _);
    }

    private bool TryFindSpawnPosition(SpeciesData data, out Vector3 resultPos, out Quaternion resultRot, out Vector3? wanderCenter)
    {
        float W = _zoneDef != null ? _zoneDef.playableWidth : 600f;
        float L = _zoneDef != null ? _zoneDef.playableLength : 600f;
        float D = _zoneDef != null ? _zoneDef.playableDepth : 200f;

        resultPos = Vector3.zero;
        resultRot = Quaternion.identity;
        wanderCenter = null;

        // Depth bounds in world Y coordinates:
        // minDepthFraction = 0 (surface Y=0m), maxDepthFraction = 1 (floor Y=-Dm)
        float upperDepthLimit = -Mathf.Min(data.minDepthFraction, data.maxDepthFraction) * D;
        float rawLowerLimit   = -Mathf.Max(data.minDepthFraction, data.maxDepthFraction) * D;

        // All species except Bluebottle (SurfaceDrifter) must spawn strictly below 3m depth (Y <= -3.0m)
        if (data.locomotionArchetype != LocomotionArchetype.SurfaceDrifter)
        {
            upperDepthLimit = Mathf.Min(upperDepthLimit, -3.0f);
        }

        bool isBenthic = data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower;
        float lowerDepthLimit = isBenthic ? rawLowerLimit : Mathf.Max(rawLowerLimit, -D + 16f);

        bool isClownfish = data.speciesId == "clownfish_001" || data.commonName.ToLower().Contains("clownfish");
        if (isClownfish)
        {
            if (_anemonePositions.Count == 0 && registry != null)
            {
                var anemoneData = registry.FindById("anemone_001");
                if (anemoneData != null)
                {
                    if (TryFindSpawnPosition(anemoneData, out Vector3 aPos, out Quaternion aRot))
                    {
                        SpawnInstance(anemoneData, aPos, aRot);
                        _usedPositions.Add(aPos);
                        _anemonePositions.Add(aPos);
                    }
                }
            }

            if (_anemonePositions.Count > 0)
            {
                // Biological Symbiosis: Clownfish spawn naturally distributed around host Sea Anemones
                Vector3 anemonePos = _anemonePositions[Random.Range(0, _anemonePositions.Count)];
                Vector2 hOffset = Random.insideUnitCircle.normalized * Random.Range(1.8f, 5.0f);
                float yOffset = Random.Range(1.2f, 2.5f);
                resultPos = new Vector3(anemonePos.x + hOffset.x, anemonePos.y + yOffset, anemonePos.z + hOffset.y);
                resultRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                wanderCenter = anemonePos;
                return true;
            }
        }

        int terrainLayerMask = LayerMask.GetMask("Terrain", "Default");
        if (terrainLayerMask == 0) terrainLayerMask = ~0;

        float halfW = Mathf.Max(40f, (W * 0.5f) - 75f);
        float halfL = Mathf.Max(40f, (L * 0.5f) - 75f);

        Vector3 bestCandidatePos = Vector3.zero;
        Quaternion bestCandidateRot = Quaternion.identity;
        float bestScore = float.MinValue;

        // ── Phase 1: Random Search (Strict Habitat + Depth Band + Separation) ──
        int attempts = Mathf.Max(maxAttempts, 90);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float rx, rz;
            if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter && attempt < 35)
            {
                // Ensure surface drifters (Bluebottles) have visible specimens floating near exploration start area (35m - 80m)
                Vector2 nearOffset = Random.insideUnitCircle.normalized * Random.Range(35f, 80f);
                rx = nearOffset.x;
                rz = nearOffset.y;
            }
            else
            {
                rx = Random.Range(-halfW, halfW);
                rz = Random.Range(-halfL, halfL);
            }

            BiomeBand biome = terrain != null ? terrain.GetBiomeAt(rx, rz) : data.preferredBiome;
            float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -D;

            // Strictly exclude positions inside Habitat Landmark exclusion zones
            if (HabitatLandmarkSpawner.IsInsideLandmarkZone(new Vector3(rx, floorY, rz), 5.0f))
                continue;

            Vector3 candidatePos;
            Quaternion candidateRot;

            if (isBenthic)
            {
                // Benthic species MUST be placed on the ocean floor or reef structures.
                // Seabed height must be within the species' configured depth range.
                if (floorY < lowerDepthLimit - 2.5f || floorY > upperDepthLimit + 2.5f)
                    continue;

                Vector3 surfaceNormal = terrain != null ? terrain.SampleNormal(rx, rz) : Vector3.up;
                float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);

                float maxSlope = GetMaxSlopeForSpecies(data);
                if (slopeAngle > maxSlope)
                    continue;

                if (!CheckFootprintFlatness(rx, rz, data))
                    continue;

                Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    bool isProp = ContextSteering.IsEnvironmentProp(hit.collider) ||
                                  hit.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                                  hit.collider.CompareTag("Environment");
                    if (isProp)
                    {
                        // NEVER spawn benthic species on top of corals, rocks, or landmarks!
                        continue;
                    }

                    if (hit.point.y <= floorY + 0.6f && hit.point.y >= floorY - 1.0f)
                    {
                        candidatePos = hit.point;
                        if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f)
                            surfaceNormal = hit.normal;
                    }
                    else
                    {
                        candidatePos = new Vector3(rx, floorY, rz);
                    }
                }
                else
                {
                    candidatePos = new Vector3(rx, floorY, rz);
                }

                if (!data.isStationary)
                {
                    candidatePos.y += Mathf.Max(0.08f, data.benthicSurfaceOffset);
                }

                candidateRot = GetStationaryRotation(data, surfaceNormal);
            }
            else if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
            {
                candidatePos = new Vector3(rx, -1.15f, rz);
                candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else
            {
                // Pelagic / swimming species: swim in depth band, safely above floor (strictly below 3m depth)
                float clearance = data.locomotionArchetype == LocomotionArchetype.PelagicCruiser ? 5.5f : 3.0f;
                float minSwimY = Mathf.Max(lowerDepthLimit, floorY + clearance);
                float maxSwimY = Mathf.Min(upperDepthLimit, -3.0f);
                if (minSwimY > maxSwimY)
                {
                    continue;
                }
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

            // Strict check: Biome must match preferred habitat
            if (biome != data.preferredBiome) continue;

            // Strict check: Depth
            if (candidatePos.y < lowerDepthLimit - 3f || candidatePos.y > upperDepthLimit + 3f)
                continue;

            // Separation check
            float speciesMinSep = GetMinSeparationForSpecies(data);
            if (!IsSeparated(candidatePos, speciesMinSep)) continue;

            resultPos = candidatePos;
            resultRot = candidateRot;
            return true;
        }

        // ── Phase 2: Targeted Grid Search (Guarantee Habitat & Closest Depth) ──
        float speciesSep = GetMinSeparationForSpecies(data);
        int gridSteps = 18;
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
                if (HabitatLandmarkSpawner.IsInsideLandmarkZone(new Vector3(rx, floorY, rz), 4.0f))
                    continue;

                Vector3 candidatePos;
                Quaternion candidateRot;

                if (isBenthic)
                {
                    if (floorY < lowerDepthLimit - 3.5f || floorY > upperDepthLimit + 3.5f)
                        continue;

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
                        bool isProp = ContextSteering.IsEnvironmentProp(hit.collider) ||
                                      hit.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                                      hit.collider.CompareTag("Environment");
                        if (isProp)
                            continue;

                        if (hit.point.y <= floorY + 0.6f && hit.point.y >= floorY - 1.0f)
                        {
                            candidatePos = hit.point;
                            if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f)
                                surfaceNormal = hit.normal;
                        }
                        else
                        {
                            candidatePos = new Vector3(rx, floorY, rz);
                        }
                    }
                    else
                    {
                        candidatePos = new Vector3(rx, floorY, rz);
                    }

                    if (!data.isStationary)
                    {
                        candidatePos.y += Mathf.Max(0.08f, data.benthicSurfaceOffset);
                    }

                    candidateRot = GetStationaryRotation(data, surfaceNormal);
                }
                else if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
                {
                    candidatePos = new Vector3(rx, -1.15f, rz);
                    candidateRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                else
                {
                    float minSwimY = Mathf.Max(lowerDepthLimit, floorY + 2.5f);
                    float maxSwimY = Mathf.Min(upperDepthLimit, -3.0f);
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

                // In Phase 2, accept slightly relaxed separation (75%) but never stack
                if (IsSeparated(candidatePos, speciesSep * 0.75f))
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

        // ── Phase 3: Guaranteed Placement Fallback (Strict Non-Overlapping Offset) ──
        if (bestCandidatePos != Vector3.zero)
        {
            // If bestCandidate violates separation from an already placed creature, search an outward ring
            if (!IsSeparated(bestCandidatePos, speciesSep * 0.65f))
            {
                bool foundOffset = false;
                for (float dist = speciesSep * 0.85f; dist <= speciesSep * 4.0f; dist += 2.5f)
                {
                    for (int angleDeg = 0; angleDeg < 360; angleDeg += 30)
                    {
                        float rad = angleDeg * Mathf.Deg2Rad;
                        float ox = bestCandidatePos.x + Mathf.Cos(rad) * dist;
                        float oz = bestCandidatePos.z + Mathf.Sin(rad) * dist;
                        if (Mathf.Abs(ox) > halfW || Mathf.Abs(oz) > halfL) continue;

                        float ofy = terrain != null ? terrain.SampleHeight(ox, oz) : bestCandidatePos.y;
                        if (isBenthic)
                        {
                            if (ofy < lowerDepthLimit - 3f || ofy > upperDepthLimit + 3f) continue;
                            if (HabitatLandmarkSpawner.IsInsideLandmarkZone(new Vector3(ox, ofy, oz), 4.0f)) continue;
                        }

                        Vector3 testOff = new Vector3(ox, ofy, oz);
                        if (IsSeparated(testOff, speciesSep * 0.65f))
                        {
                            bestCandidatePos = testOff;
                            foundOffset = true;
                            break;
                        }
                    }
                    if (foundOffset) break;
                }
            }

            if (isBenthic)
            {
                float floorY = terrain != null ? terrain.SampleHeight(bestCandidatePos.x, bestCandidatePos.z) : bestCandidatePos.y;
                Vector3 normal = terrain != null ? terrain.SampleNormal(bestCandidatePos.x, bestCandidatePos.z) : Vector3.up;
                Vector3 rayOrigin = new Vector3(bestCandidatePos.x, floorY + 30f, bestCandidatePos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, terrainLayerMask, QueryTriggerInteraction.Ignore))
                {
                    bool isProp = ContextSteering.IsEnvironmentProp(hit.collider) ||
                                  hit.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                                  hit.collider.CompareTag("Environment");
                    if (!isProp && hit.point.y <= floorY + 0.6f && hit.point.y >= floorY - 1.0f)
                    {
                        bestCandidatePos = hit.point;
                        if (hit.normal.sqrMagnitude > 0.1f && hit.normal.y > 0.1f) normal = hit.normal;
                    }
                    else
                    {
                        bestCandidatePos.y = floorY;
                    }
                }
                else
                {
                    bestCandidatePos.y = floorY;
                }
                if (!data.isStationary)
                {
                    bestCandidatePos.y += Mathf.Max(0.08f, data.benthicSurfaceOffset);
                }
                bestCandidateRot = GetStationaryRotation(data, normal);
            }
            resultPos = bestCandidatePos;
            resultRot = bestCandidateRot;
            return true;
        }

        // Final safety fallback searching along physical depth contour with separation check
        if (isBenthic)
        {
            resultPos = FindBenthicFallbackPosition(data, lowerDepthLimit, upperDepthLimit, halfW, halfL, terrainLayerMask, out resultRot);
            return true;
        }

        float fallbackX = Random.Range(-halfW * 0.5f, halfW * 0.5f);
        float fallbackZ = Random.Range(-halfL * 0.5f, halfL * 0.5f);
        if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
        {
            resultPos = new Vector3(fallbackX, -1.15f, fallbackZ);
            resultRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        else
        {
            float fallbackY = Mathf.Clamp(Mathf.Lerp(lowerDepthLimit, upperDepthLimit, 0.5f), -D + 5f, -3.0f);
            resultPos = new Vector3(fallbackX, fallbackY, fallbackZ);
            resultRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        return true;
    }

    private Vector3 FindBenthicFallbackPosition(SpeciesData data, float lowerDepthLimit, float upperDepthLimit, float halfW, float halfL, int terrainLayerMask, out Quaternion rot)
    {
        float bestScore = float.MinValue;
        Vector3 bestPos = Vector3.zero;
        Quaternion bestRot = Quaternion.identity;
        float speciesMinSep = GetMinSeparationForSpecies(data);

        for (int i = 0; i < 90; i++)
        {
            float fx = Random.Range(-halfW * 0.85f, halfW * 0.85f);
            float fz = Random.Range(-halfL * 0.85f, halfL * 0.85f);
            float fy = terrain != null ? terrain.SampleHeight(fx, fz) : -50f;
            if (HabitatLandmarkSpawner.IsInsideLandmarkZone(new Vector3(fx, fy, fz), 4.0f))
                continue;
            BiomeBand b = terrain != null ? terrain.GetBiomeAt(fx, fz) : data.preferredBiome;

            float score = 0f;
            if (b == data.preferredBiome) score += 200f;
            else if (IsCompatibleBiome(b, data.preferredBiome)) score += 100f;
            else score -= 150f;

            if (fy >= lowerDepthLimit && fy <= upperDepthLimit)
            {
                score += 150f;
            }
            else
            {
                float diff = fy < lowerDepthLimit ? (lowerDepthLimit - fy) : (fy - upperDepthLimit);
                score -= diff * 15f;
            }

            Vector3 testCandidate = new Vector3(fx, fy, fz);
            if (!IsSeparated(testCandidate, speciesMinSep * 0.65f))
            {
                score -= 800f; // Strongly penalize overlapping positions!
            }

            if (score > bestScore)
            {
                bestScore = score;
                Vector3 norm = terrain != null ? terrain.SampleNormal(fx, fz) : Vector3.up;
                float off = data.isStationary ? 0f : Mathf.Max(0.08f, data.benthicSurfaceOffset);
                bestPos = new Vector3(fx, fy + off, fz);
                bestRot = GetStationaryRotation(data, norm);
            }
        }

        rot = bestRot;
        return bestPos;
    }

    private float GetMinSeparationForSpecies(SpeciesData data)
    {
        if (data == null) return minSeparation;
        bool isRay = data.taxonomicClass == TaxonomicClass.Chondrichthyes || (data.commonName != null && data.commonName.IndexOf("ray", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (isRay) return Mathf.Max(minSeparation, 7.0f);
        if (data.locomotionArchetype == LocomotionArchetype.BenthicFollower) return Mathf.Max(minSeparation, 5.0f);
        if (data.locomotionArchetype == LocomotionArchetype.PelagicCruiser) return Mathf.Max(minSeparation, 7.5f);
        return minSeparation;
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
        bool isBenthic = data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower;
        if (isBenthic)
        {
            if (pos.y < lowerDepth) depthDist = lowerDepth - pos.y;
            else if (pos.y > upperDepth) depthDist = pos.y - upperDepth;
        }
        else if (data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
        {
            depthDist = Mathf.Abs(pos.y - (-1.15f));
        }
        else
        {
            if (pos.y < lowerDepth) depthDist = lowerDepth - pos.y;
            else if (pos.y > upperDepth) depthDist = pos.y - upperDepth;
            if (pos.y < floorY + 2f) depthDist += (floorY + 2f - pos.y) * 2f;
        }
        score += Mathf.Max(0f, 300f - depthDist * 4f);

        // 3. Spacing score
        float speciesSep = GetMinSeparationForSpecies(data);
        float nearest = float.MaxValue;
        for (int i = 0; i < _usedPositions.Count; i++)
        {
            float d = Vector3.Distance(pos, _usedPositions[i]);
            if (d < nearest) nearest = d;
        }
        if (nearest < speciesSep) score -= (speciesSep - nearest) * 60f + 250f;
        else score += 100f;

        // 4. Slope flatness score for benthic species (favors flat plateaus & gentle shelves)
        if (isBenthic && terrain != null)
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
        if (data == null) return 90f;
        if (!data.isStationary && data.locomotionArchetype != LocomotionArchetype.BenthicFollower) return 90f;
        // Wide branching corals need flat ground to prevent branches hitting or getting buried in slopes
        if (data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0) return 6f;
        if (data.taxonomicClass == TaxonomicClass.Anthozoa) return 24f; // Sea anemones easily cling to rocky slopes & reef heads
        if (data.taxonomicClass == TaxonomicClass.Demospongiae) return 18f;
        if (data.taxonomicClass == TaxonomicClass.Bivalvia) return 18f;
        if (data.locomotionArchetype == LocomotionArchetype.BenthicFollower) return 25f;
        return maxStationarySlope;
    }

    private bool CheckFootprintFlatness(float cx, float cz, SpeciesData data)
    {
        bool isBenthic = data != null && (data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower);
        if (!isBenthic || terrain == null) return true;

        float checkRadius = 0f;
        float maxAllowedVariance = 0f;

        if (data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Fan Coral is ~7.6m wide! Check 2.5m radius to ensure branches stay in clear water
            checkRadius = 2.5f;
            maxAllowedVariance = 0.20f;
        }
        else if (data.taxonomicClass == TaxonomicClass.Anthozoa ||
                 data.taxonomicClass == TaxonomicClass.Demospongiae ||
                 data.taxonomicClass == TaxonomicClass.Bivalvia)
        {
            checkRadius = 1.0f;
            maxAllowedVariance = 0.40f;
        }
        else
        {
            // Small creeping species (Starfish, Snail, Lobster): check 0.6m radius
            checkRadius = 0.6f;
            maxAllowedVariance = 0.15f;
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

        // 2. Anemones and Giant Clams: mostly upright, natural slope adaptation (70% blend)
        if (data.taxonomicClass == TaxonomicClass.Anthozoa || data.taxonomicClass == TaxonomicClass.Bivalvia)
        {
            Vector3 blendedNormal = Vector3.Slerp(Vector3.up, surfaceNormal, 0.70f);
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

    public static bool IsCompatibleBiome(BiomeBand a, BiomeBand b)
    {
        if (a == b) return true;
        if ((a == BiomeBand.Hard && b == BiomeBand.Rock) || (a == BiomeBand.Rock && b == BiomeBand.Hard)) return true;
        if ((a == BiomeBand.Soft && b == BiomeBand.OpenWater) || (a == BiomeBand.OpenWater && b == BiomeBand.Soft)) return true;
        return false;
    }

    // -----------------------------------------------------------------------
    // Instantiation
    // -----------------------------------------------------------------------

    private void SpawnInstance(SpeciesData data, Vector3 position, Quaternion rotation, Vector3? wanderCenter = null)
    {
        GameObject go = data.modelPrefab != null
            ? Instantiate(data.modelPrefab, position, rotation, creatureParent)
            : CreatePlaceholder(data, position, rotation);

        go.name = $"{data.commonName} [{data.speciesId}]";

        // Natural Individual Scale Variation:
        // Applies organic size differences (juvenile to adult, up to 2x smaller/bigger) and subtle axis offsets without distortion
        Vector3 baseScale = data.modelPrefab != null
            ? data.modelPrefab.transform.localScale
            : (data.placeholderScale != Vector3.zero ? data.placeholderScale : Vector3.one);

        go.transform.localScale = enableInGameScaleVariation
            ? ComputeInstanceScale(data, baseScale, globalMinScale, globalMaxScale, scaleDistributionCurvature)
            : baseScale;

        Vector3 center = wanderCenter.HasValue ? wanderCenter.Value : position;

        if (data.isStationary) SetupStationary(go, data);
        else                   SetupMobile(go, data, center);

        // Hide newly spawned species if tutorial is currently in early orientation/navigation phases
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive && TutorialManager.Instance.CurrentPhase < TutorialManager.TutorialPhase.Step3_SonarSystem)
        {
            go.SetActive(false);
        }
    }

    /// <summary>
    /// Computes a natural individual scale with uniform size variance (up to 2x smaller juvenile to 2x bigger elder)
    /// plus subtle non-uniform axis jitter to give unique body proportions without distortion.
    /// Only applied to in-game spawned entities, preserving default scales for UI/previews.
    /// </summary>
    public static Vector3 ComputeInstanceScale(SpeciesData data, Vector3 defaultBaseScale)
    {
        return ComputeInstanceScale(data, defaultBaseScale, 0.75f, 1.25f, 3);
    }

    /// <summary>
    /// Computes an organic, natural individual scale with safe size variance (0.75x to 1.25x)
    /// strictly preserving uniform scale on all 3 axes to prevent collider distortion,
    /// PhysX jitter, and physics clipping.
    /// </summary>
    public static Vector3 ComputeInstanceScale(
        SpeciesData data,
        Vector3 defaultBaseScale,
        float minMultiplier,
        float maxMultiplier,
        int curvature = 3)
    {
        if (data == null || !data.enableScaleVariation)
            return defaultBaseScale;

        // 1. Resolve scale bounds: respect custom species overrides while defaulting to [0.75, 1.25]
        float minU = minMultiplier > 0.01f ? minMultiplier : 0.75f;
        float maxU = maxMultiplier >= minU ? maxMultiplier : Mathf.Max(minU, 1.25f);

        if (data.uniformScaleRange.x > 0.05f && data.uniformScaleRange.y >= data.uniformScaleRange.x)
        {
            bool isLegacy = Mathf.Approximately(data.uniformScaleRange.x, 0.875f) &&
                            Mathf.Approximately(data.uniformScaleRange.y, 1.0625f);
            if (!isLegacy)
            {
                minU = data.uniformScaleRange.x;
                maxU = data.uniformScaleRange.y;
            }
        }

        // Clamp scale multipliers to safe variation [0.75, 1.25]
        minU = Mathf.Clamp(minU, 0.75f, 1.0f);
        maxU = Mathf.Clamp(maxU, 1.0f, 1.25f);

        // 2. Natural biological size sampling:
        // Irwin-Hall bell-curve distribution in logarithmic space centered on 1.0 (standard adult)
        float uniformMul = SampleNaturalScale(minU, maxU, curvature);

        // 3. Strictly UNIFORM scaling:
        // Preserves collider proportions, BoxCollider aspect ratios, and mass distribution without non-uniform distortion
        return defaultBaseScale * uniformMul;
    }

    /// <summary>
    /// Generates a natural biological scale multiplier between [minScale, maxScale]
    /// centered organically around standard adult size (1.0).
    /// Uses an Irwin-Hall distribution in logarithmic space so that 2x smaller (0.5) and 2x bigger (2.0)
    /// have symmetrical biological rarity, with the majority clustering around healthy adult proportions (0.85–1.25x).
    /// </summary>
    public static float SampleNaturalScale(float minScale, float maxScale, int curvature = 3)
    {
        if (minScale <= 0.01f) minScale = 0.5f;
        if (maxScale < minScale) maxScale = minScale;

        if (Mathf.Approximately(minScale, maxScale))
            return minScale;

        // Generate u in [-1.0, +1.0] centered on 0.0
        float u;
        if (curvature <= 1)
        {
            u = Random.Range(-1f, 1f);
        }
        else if (curvature == 2)
        {
            // Triangular distribution (sum of 2 uniforms)
            u = (Random.value + Random.value) - 1f;
        }
        else
        {
            // Cubic Irwin-Hall bell curve (sum of 3 uniforms)
            u = (Random.value + Random.value + Random.value - 1.5f) / 1.5f;
        }

        // Natural logarithmic mapping:
        // u = 0 -> scale = 1.0 (average adult)
        // u = -1 -> scale = minScale (e.g. 0.5 = 2x smaller)
        // u = +1 -> scale = maxScale (e.g. 2.0 = 2x bigger)
        if (minScale < 1f && maxScale > 1f)
        {
            float logMin = Mathf.Log(minScale);
            float logMax = Mathf.Log(maxScale);
            float logVal = u >= 0f ? (u * logMax) : (-u * logMin);
            return Mathf.Exp(logVal);
        }
        else
        {
            return Mathf.Lerp(minScale, maxScale, (u + 1f) * 0.5f);
        }
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
            rend.material = MaterialUtils.CreateColoredMaterial(data.placeholderColor);
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

        // Assign to Creatures layer for collision matrix filtering
        int creaturesLayer = LayerMask.NameToLayer("Creatures");
        if (creaturesLayer < 0) creaturesLayer = LayerMask.NameToLayer("Default");
        SetLayerRecursive(go, creaturesLayer);

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
        rb.isKinematic    = false;
        rb.linearDamping  = 2f;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = data.placeholderScale != Vector3.zero ? data.placeholderScale * 1.1f : Vector3.one * 1.2f;
            col = box;
        }
        // Trigger collider — creatures should be scannable/interactive but NOT
        // physically block the player submarine (their BoxColliders can be 20m+).
        // ContextSteering still uses raycasts against Terrain layer for AI avoidance.
        col.isTrigger = true;

        try { go.tag = "Species"; } catch { }

        // Assign to Creatures layer for collision matrix filtering
        int creaturesLayer = LayerMask.NameToLayer("Creatures");
        if (creaturesLayer < 0) creaturesLayer = LayerMask.NameToLayer("Default");
        SetLayerRecursive(go, creaturesLayer);

        var locomotion = go.GetComponent<CreatureLocomotion>();
        if (locomotion == null) locomotion = go.AddComponent<CreatureLocomotion>();
        locomotion.Initialize(data);

        var cs = go.GetComponent<ContextSteering>();
        if (cs == null) cs = go.AddComponent<ContextSteering>();

        var ai = go.GetComponent<SpeciesAI>();
        if (ai == null) ai = go.AddComponent<SpeciesAI>();
        ai.Initialize(data, spawnCenter);

        var trackable = go.GetComponent<SonarTrackable>();
        if (trackable == null) trackable = go.AddComponent<SonarTrackable>();
        bool discovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(data.zoneIndex, data.speciesId);
        trackable.Initialize(SonarTrackable.SonarTargetType.Species, data.commonName, discovered);

        if (data.locomotionArchetype == LocomotionArchetype.BenthicFollower)
        {
            AdjustContactHeight(go, data);
        }
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
            float D = _zoneDef != null ? _zoneDef.playableDepth : 200f;
            int terrainMask = LayerMask.GetMask("Terrain", "Default");
            if (terrainMask == 0) terrainMask = ~0;

            float SampleGroundAt(float wx, float wz)
            {
                float trueFloorY = terrain != null ? terrain.SampleHeight(wx, wz) : -D;
                Vector3 ray = new Vector3(wx, trueFloorY + 25f, wz);
                if (Physics.Raycast(ray, Vector3.down, out RaycastHit h, 35f, terrainMask, QueryTriggerInteraction.Ignore))
                {
                    bool isProp = ContextSteering.IsEnvironmentProp(h.collider) ||
                                  h.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                                  h.collider.CompareTag("Environment");
                    if (isProp) return trueFloorY;

                    if (h.point.y <= trueFloorY + 0.5f && h.point.y >= trueFloorY - 1.0f)
                        return h.point.y;
                }
                return trueFloorY;
            }

            float bottomToPivot = 0f;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                // Measure unrotated, object-space local bounds of the visual mesh
                Quaternion originalRot = go.transform.rotation;
                Vector3 originalPos = go.transform.position;

                go.transform.rotation = Quaternion.identity;
                go.transform.position = Vector3.zero;

                Bounds localBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    localBounds.Encapsulate(renderers[i].bounds);

                go.transform.rotation = originalRot;
                go.transform.position = originalPos;

                bool isCoral = data != null && data.commonName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSponge = data != null && data.taxonomicClass == TaxonomicClass.Demospongiae;
                bool isMoundOrClam = data != null && (data.taxonomicClass == TaxonomicClass.Anthozoa || data.taxonomicClass == TaxonomicClass.Bivalvia);

                float cx = localBounds.center.x;
                float cz = localBounds.center.z;
                float yMin = localBounds.min.y;
                bottomToPivot = Mathf.Max(0f, -yMin);

                float scaleFactorY = 1.0f;
                if (data != null && data.modelPrefab != null && data.modelPrefab.transform.localScale.y > 0.001f)
                    scaleFactorY = go.transform.localScale.y / data.modelPrefab.transform.localScale.y;
                else if (data != null && data.placeholderScale.y > 0.001f)
                    scaleFactorY = go.transform.localScale.y / data.placeholderScale.y;
                else
                    scaleFactorY = go.transform.localScale.y;
                scaleFactorY = Mathf.Clamp(scaleFactorY, 0.4f, 2.5f);

                if (isCoral || isSponge)
                {
                    // Branching corals & upright sponges: anchor the central trunk base firmly into the seabed.
                    // The trunk base is at local (cx * 0.2f, yMin, cz * 0.2f)
                    Vector3 trunkLocal = new Vector3(cx * 0.2f, yMin, cz * 0.2f);
                    Vector3 trunkWorld = originalPos + originalRot * trunkLocal;
                    float gTrunk = SampleGroundAt(trunkWorld.x, trunkWorld.z);

                    float sink = (isCoral ? 0.08f : 0.06f) * scaleFactorY;
                    Vector3 pos = originalPos;
                    pos.y = gTrunk - (originalRot * trunkLocal).y - sink;
                    go.transform.position = pos;
                }
                else if (isMoundOrClam)
                {
                    // Sea Anemones & Giant Clams: sample 5 points across the base pad/hinge
                    float baseR = Mathf.Clamp(localBounds.extents.x * 0.35f, 0.25f * scaleFactorY, 0.8f * scaleFactorY);
                    Vector3[] localPts = new Vector3[]
                    {
                        new Vector3(cx, yMin, cz),
                        new Vector3(cx + baseR, yMin, cz),
                        new Vector3(cx - baseR, yMin, cz),
                        new Vector3(cx, yMin, cz + baseR),
                        new Vector3(cx, yMin, cz - baseR)
                    };

                    float sink = ((data != null && data.taxonomicClass == TaxonomicClass.Bivalvia) ? 0.08f : 0.06f) * scaleFactorY;
                    float minRequiredY = float.MaxValue;
                    float centerTargetY = 0f;

                    for (int i = 0; i < localPts.Length; i++)
                    {
                        Vector3 ptWorld = originalPos + originalRot * localPts[i];
                        float gy = SampleGroundAt(ptWorld.x, ptWorld.z);
                        float targetY = gy - (originalRot * localPts[i]).y - sink;
                        if (i == 0) centerTargetY = targetY;
                        if (targetY < minRequiredY) minRequiredY = targetY;
                    }

                    // Sink so lowest edge touches ground; cap at max 12cm below center to keep upper body visible
                    Vector3 pos = originalPos;
                    pos.y = Mathf.Max(minRequiredY, centerTargetY - (0.12f * scaleFactorY));
                    go.transform.position = pos;
                }
                else
                {
                    // Flat / creeping species (Starfish, Snail, Lobster): underside conforms to terrain slope
                    float rx = localBounds.extents.x * 0.65f;
                    float rz = localBounds.extents.z * 0.65f;
                    Vector3[] localPts = new Vector3[]
                    {
                        new Vector3(cx, yMin, cz),
                        new Vector3(cx + rx, yMin, cz),
                        new Vector3(cx - rx, yMin, cz),
                        new Vector3(cx, yMin, cz + rz),
                        new Vector3(cx, yMin, cz - rz)
                    };

                    float sink = 0.035f * scaleFactorY;
                    float maxPen = 0.06f * scaleFactorY;
                    if (data != null)
                    {
                        if (data.taxonomicClass == TaxonomicClass.Asteroidea)
                        {
                            sink = 0.025f * scaleFactorY; // Starfish: proportional embed ensures all 5 arms touch sand
                            maxPen = 0.04f * scaleFactorY;
                        }
                        else if (data.taxonomicClass == TaxonomicClass.Malacostraca)
                        {
                            sink = 0.045f * scaleFactorY; // Lobster: proportional embed ensures legs touch seabed firmly
                            maxPen = 0.08f * scaleFactorY;
                        }
                        else if (data.taxonomicClass == TaxonomicClass.Gastropoda)
                        {
                            sink = 0.035f * scaleFactorY; // Cone Snail: proportional embed
                            maxPen = 0.06f * scaleFactorY;
                        }
                    }

                    float minRequiredY = float.MaxValue;
                    float centerTargetY = 0f;

                    for (int i = 0; i < localPts.Length; i++)
                    {
                        Vector3 ptWorld = originalPos + originalRot * localPts[i];
                        float gy = SampleGroundAt(ptWorld.x, ptWorld.z);
                        float targetY = gy - (originalRot * localPts[i]).y - sink;
                        if (i == 0) centerTargetY = targetY;
                        if (targetY < minRequiredY) minRequiredY = targetY;
                    }

                    Vector3 pos = originalPos;
                    pos.y = Mathf.Max(minRequiredY, centerTargetY - maxPen);
                    go.transform.position = pos;
                }
            }
            else
            {
                float groundY = SampleGroundAt(go.transform.position.x, go.transform.position.z);
                Vector3 pos = go.transform.position;
                pos.y = groundY;
                go.transform.position = pos;
            }

            // Absolute safeguard: stationary and benthic organisms must never float or perch above the true seabed
            float groundTruthY = terrain != null ? terrain.SampleHeight(go.transform.position.x, go.transform.position.z) : -D;
            float maxAllowedY = groundTruthY + bottomToPivot + 0.35f;
            if (go.transform.position.y > maxAllowedY)
            {
                Vector3 clampedPos = go.transform.position;
                clampedPos.y = groundTruthY + bottomToPivot;
                go.transform.position = clampedPos;
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

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
