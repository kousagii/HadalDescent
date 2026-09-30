using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns or dynamically snaps Habitat Landmark survey POIs onto the generated ocean floor.
///
/// Features:
///   1. Biome-Aware Placement: Places landmarks strictly in their corresponding habitat biome
///      (e.g., Coral Reef in BiomeBand.Hard, Seagrass Bed in BiomeBand.Soft).
///   2. Flat Surface Detection: Analyzes terrain normal (normal.y >= 0.94) and footprint height variance
///      to guarantee large landmarks land on broad, level plateaus without cliff drop-offs.
///   3. Deterministic Placement: Uses the zone's PCG seed to place landmarks at consistent,
///      authentic coordinates for that world seed.
///   4. Surface Snapping: Samples the procedural seabed mesh height (via TerrainGenerator)
///      and raycasts down onto the actual mesh collider, ensuring the landmark rests
///      strictly ON TOP of the ocean floor, never buried underneath.
///   5. Sonar & Survey Integration: Ensures SonarTrackable (Radiant Yellow) and
///      EnvironmentFactTarget are properly configured on every placed landmark.
/// </summary>
public class HabitatLandmarkSpawner : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Distance above terrain mesh to raycast down from.")]
    [SerializeField] private float raycastDropHeight = 50f;

    [Tooltip("Slight vertical lift above the hit point to prevent mesh clipping.")]
    [SerializeField] private float surfaceOffset = 0.05f;

    [Header("Registry")]
    [Tooltip("Habitat Landmark Registry. Loaded from Resources if null.")]
    [SerializeField] private HabitatLandmarkRegistry landmarkRegistry;

    private Transform _landmarksParent;
    private bool      _hasSpawned = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        var terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null && !terrain.HasGenerated)
        {
            // TerrainGenerator will call SpawnLandmarksForZone after generating the mesh
            return;
        }

        if (!_hasSpawned)
        {
            int zoneIdx = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : DetectZoneFromScene();
            SpawnLandmarksForZone(zoneIdx);
        }
    }

    private int DetectZoneFromScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Contains("Twilight")) return 1;
        if (sceneName.Contains("Midnight")) return 2;
        if (sceneName.Contains("Abyss"))    return 3;
        if (sceneName.Contains("Hadal"))    return 4;
        return 0; // Default Sunlight Zone
    }

    // -----------------------------------------------------------------------
    // Spawning & Snapping Logic
    // -----------------------------------------------------------------------

    public void SpawnLandmarksForZone(int zoneIndex)
    {
        _hasSpawned = true;
        EnsureParent();

        if (landmarkRegistry == null)
            landmarkRegistry = Resources.Load<HabitatLandmarkRegistry>("HabitatLandmarkRegistry");

        var terrain = FindFirstObjectByType<TerrainGenerator>();

        float w = 600f;
        float d = 150f;

        if (ZoneConfig.IsValidZone(zoneIndex))
        {
            var def = ZoneConfig.Zones[zoneIndex];
            w = def.playableWidth;
            d = def.playableDepth;
        }

        int seed = GameManager.Instance != null
            ? GameManager.Instance.GetOrCreateZoneSeed(zoneIndex)
            : 42;

        var landmarks = landmarkRegistry != null
            ? landmarkRegistry.GetLandmarksForZone(zoneIndex)
            : new List<HabitatLandmarkData>();

        // Gather existing landmark targets in scene if any
        var existingTargets = new List<EnvironmentFactTarget>(
            _landmarksParent.GetComponentsInChildren<EnvironmentFactTarget>(true)
        );

        // Also search full scene under Environment
        var envObj = GameObject.Find("Environment");
        if (envObj != null)
        {
            foreach (var eft in envObj.GetComponentsInChildren<EnvironmentFactTarget>(true))
            {
                if (!existingTargets.Contains(eft))
                    existingTargets.Add(eft);
            }
        }

        int count = Mathf.Max(landmarks.Count, existingTargets.Count);
        if (count == 0) return;

        Debug.Log($"[HabitatLandmarkSpawner] Aligning/Spawning {count} landmarks for Zone {zoneIndex} on terrain (seed={seed}).");

        for (int i = 0; i < count; i++)
        {
            HabitatLandmarkData data = i < landmarks.Count ? landmarks[i] : null;

            bool isCoral = (data != null && (data.factId.Contains("coral_reef") || data.factId.Contains("barrier_reef"))) || (zoneIndex == 0 && i == 0);
            bool isSeagrass = (data != null && (data.factId.Contains("seagrass") || data.factId.Contains("kelp"))) || (zoneIndex == 0 && i == 1);

            // Seed random state deterministically for this specific landmark index
            Random.InitState((int)(seed * 8191 + i * 1337 + 777));

            // Default target biome based on data or landmark type
            BiomeBand targetBiome = GetTargetBiomeForLandmark(data, zoneIndex, i);

            // Distribute landmarks in distinct angular sectors so they never bunch up
            float baseAngle = (i * (360f / Mathf.Max(1, count)) + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;

            Vector3 surfacePos;
            Vector3 surfaceNormal = Vector3.up;

            // 1. Search for a flat, level surface in the landmark's corresponding biome band
            bool foundFlatSpot = FindFlatBiomeLocation(
                terrain,
                targetBiome,
                baseAngle,
                w * 0.40f,
                footprintRadius: isSeagrass ? 20f : 16f,
                out Vector3 candidatePos,
                out Vector3 candidateNormal,
                zoneIndex,
                isCoral,
                isSeagrass
            );

            if (foundFlatSpot)
            {
                // Raycast down onto the physical terrain mesh collider from above the candidate point
                Vector3 rayOrigin = new Vector3(candidatePos.x, candidatePos.y + raycastDropHeight, candidatePos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDropHeight * 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    surfacePos = hit.point;
                    surfaceNormal = hit.normal;
                }
                else
                {
                    surfacePos = candidatePos;
                    surfaceNormal = candidateNormal;
                }
            }
            else if (zoneIndex == 0 && isCoral)
            {
                // Dedicated Coral Reef fallback: strictly search for Hard biome at depth 50-100m
                float bestDepthDiff = float.MaxValue;
                Vector3 fallbackPos = new Vector3(Mathf.Cos(baseAngle) * w * 0.28f, -75f, Mathf.Sin(baseAngle) * w * 0.28f);
                for (int fb = 0; fb < 60; fb++)
                {
                    float fAngle = Random.Range(0f, Mathf.PI * 2f);
                    float fDist = Random.Range(w * 0.15f, w * 0.40f);
                    float fx = Mathf.Cos(fAngle) * fDist;
                    float fz = Mathf.Sin(fAngle) * fDist;
                    BiomeBand b = terrain != null ? terrain.GetBiomeAt(fx, fz) : BiomeBand.Hard;
                    if (b != BiomeBand.Hard) continue;
                    float fy = terrain != null ? terrain.SampleHeight(fx, fz) : -75f;
                    if (fy < -105f || fy > -45f) continue;
                    float diff = Mathf.Abs(fy - (-75f));
                    if (diff < bestDepthDiff)
                    {
                        bestDepthDiff = diff;
                        fallbackPos = new Vector3(fx, fy, fz);
                    }
                }
                surfacePos = fallbackPos;
                if (terrain != null) surfaceNormal = terrain.SampleNormal(surfacePos.x, surfacePos.z);
            }
            else
            {
                // Fallback sector placement
                float distFromCenter = Random.Range(w * 0.22f, w * 0.36f);
                float targetX = Mathf.Cos(baseAngle) * distFromCenter;
                float targetZ = Mathf.Sin(baseAngle) * distFromCenter;
                float floorY = terrain != null ? terrain.SampleHeight(targetX, targetZ) : -d;

                Vector3 rayOrigin = new Vector3(targetX, floorY + raycastDropHeight, targetZ);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDropHeight * 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    surfacePos = hit.point;
                    surfaceNormal = hit.normal;
                }
                else
                {
                    surfacePos = new Vector3(targetX, floorY, targetZ);
                    if (terrain != null) surfaceNormal = terrain.SampleNormal(targetX, targetZ);
                }
            }

            // Ensure landmark rests cleanly atop or embedded in the surface
            if (isSeagrass)
            {
                // To guarantee the seagrass bed NEVER floats on terrain slopes,
                // sample the seabed across its 40x30m footprint (including its +7m, +9m visual center)
                // and sink the root so that the lowest grass blades/rocks are anchored into the seafloor.
                float minH = surfacePos.y;
                Vector2[] footprintOffsets = new Vector2[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(7f, 9f),
                    new Vector2(25f, 6f),
                    new Vector2(-12f, 9f),
                    new Vector2(7f, 23f),
                    new Vector2(7f, -6f),
                    new Vector2(26f, 20f),
                    new Vector2(-10f, 20f),
                    new Vector2(20f, -4f),
                    new Vector2(-10f, -4f)
                };

                foreach (var offset in footprintOffsets)
                {
                    float sx = surfacePos.x + offset.x;
                    float sz = surfacePos.z + offset.y;
                    float sy = terrain != null ? terrain.SampleHeight(sx, sz) : surfacePos.y;
                    if (sy < minH) minH = sy;
                }

                // Sinking ensures all perimeter blades and base rocks embed firmly into the sediment
                surfacePos.y = Mathf.Min(surfacePos.y, minH) - 1.2f;
            }
            else
            {
                surfacePos.y += surfaceOffset;
            }

            // Find existing instance or instantiate prefab
            GameObject landmarkGO = null;
            EnvironmentFactTarget matchingTarget = null;

            if (data != null)
            {
                matchingTarget = existingTargets.Find(e =>
                    e != null && (
                        e.FactId == data.factId ||
                        (data.factId.Contains("coral_reef") && (e.FactId.Contains("barrier_reef") || e.gameObject.name.Contains("CoralReef") || e.gameObject.name.Contains("BarrierReef"))) ||
                        (data.factId.Contains("seagrass") && (e.FactId.Contains("kelp") || e.gameObject.name.Contains("SeaGrass") || e.gameObject.name.Contains("Kelp")))
                    )
                );
            }

            if (matchingTarget == null && i < existingTargets.Count)
            {
                matchingTarget = existingTargets[i];
            }

            if (matchingTarget != null)
            {
                landmarkGO = matchingTarget.gameObject;
            }
            else
            {
                // Instantiate from prefab
                string prefabName = GetPrefabNameForData(data, zoneIndex, i);
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/Environment/Landmarks/{prefabName}")
                                 ?? Resources.Load<GameObject>($"Landmarks/{prefabName}");

                #if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Environment/Landmarks/{prefabName}.prefab");
                }
                #endif

                if (prefab != null)
                {
                    landmarkGO = Instantiate(prefab, _landmarksParent);
                    landmarkGO.name = prefabName;
                }
            }

            if (landmarkGO != null)
            {
                landmarkGO.transform.SetParent(_landmarksParent, true);
                landmarkGO.transform.position = surfacePos;

                // Keep large landmarks level with slight slope compliance (prevents awkward sideways tilting)
                Quaternion randomYaw  = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                landmarkGO.transform.rotation = Quaternion.Slerp(Quaternion.identity, slopeAlign, 0.15f) * randomYaw;

                // Ensure components
                var factComp = landmarkGO.GetComponent<EnvironmentFactTarget>() ?? landmarkGO.AddComponent<EnvironmentFactTarget>();
                var trackable = landmarkGO.GetComponent<SonarTrackable>() ?? landmarkGO.AddComponent<SonarTrackable>();
                trackable.Initialize(SonarTrackable.SonarTargetType.Environment, factComp.HabitatName, factComp.IsSurveyed);

                Debug.Log($"[HabitatLandmarkSpawner] Successfully placed '{factComp.HabitatName}' atop ocean floor at {surfacePos} (normal: {surfaceNormal}, biome: {targetBiome}).");
            }
        }
    }

    /// <summary>
    /// Searches for a flat, level position matching the landmark's target biome band.
    /// Checks center flatness and perimeter footprint points to prevent floating or steep cliff placement.
    /// </summary>
    private bool FindFlatBiomeLocation(
        TerrainGenerator terrain,
        BiomeBand targetBiome,
        float baseAngle,
        float maxDist,
        float footprintRadius,
        out Vector3 bestPos,
        out Vector3 bestNormal,
        int zoneIndex = 0,
        bool isCoralReef = false,
        bool isSeagrass = false)
    {
        bestPos = Vector3.zero;
        bestNormal = Vector3.up;

        if (terrain == null) return false;

        float bestScore = float.MinValue;
        bool found = false;

        int totalAttempts = (isCoralReef || isSeagrass) ? 100 : 50;

        for (int attempt = 0; attempt < totalAttempts; attempt++)
        {
            float angleOffset = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
            float angle = baseAngle + angleOffset;
            float dist = Random.Range(maxDist * 0.18f, maxDist * 0.48f);

            float cx = Mathf.Cos(angle) * dist;
            float cz = Mathf.Sin(angle) * dist;

            // 1. Biome Check
            BiomeBand currentBiome = terrain.GetBiomeAt(cx, cz);
            bool biomeMatch = (currentBiome == targetBiome);

            // Coral reef landmark MUST be in coral reef (Hard) biome with other corals
            if (isCoralReef && currentBiome != BiomeBand.Hard)
                continue;

            // Seagrass bed prefers soft sediment
            if (isSeagrass && currentBiome != BiomeBand.Soft && attempt < 60)
                continue;

            if (!biomeMatch && !isCoralReef && attempt < 25)
                continue;

            float centerH = terrain.SampleHeight(cx, cz);

            // 2. Depth Check: Coral Reef in Sunlight Zone must be at depth 50m to 100m (Y in [-100f, -50f])
            if (zoneIndex == 0 && isCoralReef)
            {
                // In sunlight zone: Depth is -Y. 50m to 100m depth => Y between -100 and -50
                if (centerH < -100f || centerH > -50f)
                {
                    // In later attempts, allow slight tolerance [45m to 105m]
                    if (attempt < 70 || centerH < -105f || centerH > -45f)
                        continue;
                }
            }

            // 3. Center Flatness Check (for Seagrass, center of visual footprint is offset at +7, +9)
            float testCx = isSeagrass ? cx + 7f : cx;
            float testCz = isSeagrass ? cz + 9f : cz;

            Vector3 centerNormal = terrain.SampleNormal(testCx, testCz);
            float centerFlatness = centerNormal.y; // 1.0 is completely flat horizontal ground
            if (centerFlatness < 0.90f)
                continue;

            // 4. Perimeter Footprint Check
            float hN = terrain.SampleHeight(testCx, testCz + footprintRadius);
            float hS = terrain.SampleHeight(testCx, testCz - footprintRadius);
            float hE = terrain.SampleHeight(testCx + footprintRadius, testCz);
            float hW = terrain.SampleHeight(testCx - footprintRadius, testCz);

            float maxHDiff = Mathf.Max(
                Mathf.Abs(hN - centerH),
                Mathf.Abs(hS - centerH),
                Mathf.Abs(hE - centerH),
                Mathf.Abs(hW - centerH)
            );

            // Reject if terrain drops off or spikes more than 4 meters across the landmark
            if (maxHDiff > 4.0f)
                continue;

            Vector3 normN = terrain.SampleNormal(testCx, testCz + footprintRadius);
            Vector3 normS = terrain.SampleNormal(testCx, testCz - footprintRadius);
            Vector3 normE = terrain.SampleNormal(testCx + footprintRadius, testCz);
            Vector3 normW = terrain.SampleNormal(testCx - footprintRadius, testCz);

            float minPerimFlatness = Mathf.Min(normN.y, normS.y, normE.y, normW.y);
            if (minPerimFlatness < 0.85f)
                continue;

            // Score calculation
            float score = (biomeMatch ? 100f : 0f) + (centerFlatness * 50f) + (minPerimFlatness * 20f) - (maxHDiff * 4f);

            // Bonus for Coral Reef landing close to target depth 75m (depth 50-100m)
            if (zoneIndex == 0 && isCoralReef)
            {
                float depthScore = 50f - Mathf.Abs(centerH - (-75f)) * 2f;
                score += depthScore;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = new Vector3(cx, centerH, cz);
                bestNormal = centerNormal;
                found = true;

                // If found a near-perfect flat plateau in exact biome at ideal depth, accept immediately
                if (biomeMatch && centerFlatness >= 0.96f && maxHDiff <= 1.8f)
                {
                    if (!isCoralReef || (centerH >= -95f && centerH <= -55f))
                        break;
                }
            }
        }

        return found;
    }

    private BiomeBand GetTargetBiomeForLandmark(HabitatLandmarkData data, int zoneIndex, int index)
    {
        if (data != null) return data.targetBiome;

        if (zoneIndex == 0)
        {
            // Index 0: Coral Reef (Hard Biome); Index 1: Seagrass Bed (Soft Biome)
            return index == 0 ? BiomeBand.Hard : BiomeBand.Soft;
        }

        // Twilight Zone: Whale Fall (Soft Biome); Cold Coral Mound (Hard Biome)
        return index == 0 ? BiomeBand.Soft : BiomeBand.Hard;
    }

    private string GetPrefabNameForData(HabitatLandmarkData data, int zoneIndex, int index)
    {
        if (data != null)
        {
            string id = data.factId.ToLower();
            if (id.Contains("coral_reef") || id.Contains("barrier_reef")) return "Landmark_CoralReef";
            if (id.Contains("seagrass") || id.Contains("sea_grass") || id.Contains("kelp")) return "Landmark_SeaGrassBed";
            if (id.Contains("whale")) return "Landmark_WhaleFall";
            if (id.Contains("cold_coral")) return "Landmark_ColdWaterCoralMound";
        }

        if (zoneIndex == 0)
            return index == 0 ? "Landmark_CoralReef" : "Landmark_SeaGrassBed";

        return index == 0 ? "Landmark_WhaleFall" : "Landmark_ColdWaterCoralMound";
    }

    private void EnsureParent()
    {
        if (_landmarksParent != null) return;

        var env = GameObject.Find("Environment");
        Transform parentTransform = env != null ? env.transform : null;

        var existing = parentTransform != null ? parentTransform.Find("Landmarks") : GameObject.Find("Landmarks")?.transform;
        if (existing != null)
        {
            _landmarksParent = existing;
        }
        else
        {
            var go = new GameObject("Landmarks");
            if (parentTransform != null) go.transform.SetParent(parentTransform, false);
            _landmarksParent = go.transform;
        }
    }
}
