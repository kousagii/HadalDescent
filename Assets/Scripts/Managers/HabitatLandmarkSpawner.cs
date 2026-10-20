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

    public struct LandmarkZone
    {
        public Vector3 center;
        public float radius;
        public string name;
    }

    private static readonly List<LandmarkZone> _registeredLandmarks = new List<LandmarkZone>();
    public static IReadOnlyList<LandmarkZone> RegisteredLandmarks => _registeredLandmarks;

    /// <summary>
    /// Checks whether a given world coordinate falls inside the footprint of any active Habitat Landmark.
    /// Used by SpeciesSpawner and SpeciesAI to strictly prevent species from spawning or wandering into landmark formations.
    /// </summary>
    public static bool IsInsideLandmarkZone(Vector3 worldPos, float extraPadding = 5.0f)
    {
        for (int i = 0; i < _registeredLandmarks.Count; i++)
        {
            float dx = worldPos.x - _registeredLandmarks[i].center.x;
            float dz = worldPos.z - _registeredLandmarks[i].center.z;
            float r = _registeredLandmarks[i].radius + extraPadding;
            if (dx * dx + dz * dz <= r * r) return true;
        }

        // Fallback: query active EnvironmentFactTargets if list is empty
        var targets = FindObjectsByType<EnvironmentFactTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            Vector3 c = targets[i].transform.position;
            float dx = worldPos.x - c.x;
            float dz = worldPos.z - c.z;
            float r = 24.0f + extraPadding;
            if (dx * dx + dz * dz <= r * r) return true;
        }
        return false;
    }

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
            int zoneIdx = DetectZoneFromScene();
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
        float length = 600f;

        if (ZoneConfig.IsValidZone(zoneIndex))
        {
            var def = ZoneConfig.Zones[zoneIndex];
            w = def.playableWidth;
            length = def.playableLength;
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

        _registeredLandmarks.Clear();
        int count = Mathf.Max(landmarks.Count, existingTargets.Count);
        if (count == 0) return;

        Debug.Log($"[HabitatLandmarkSpawner] Aligning/Spawning {count} landmarks for Zone {zoneIndex} on terrain (seed={seed}).");

        for (int i = 0; i < count; i++)
        {
            HabitatLandmarkData data = i < landmarks.Count ? landmarks[i] : null;

            // Seed random state deterministically for this specific landmark index
            Random.InitState((int)(seed * 8191 + i * 1337 + 777));

            // Distribute landmarks in distinct angular sectors so they never bunch up
            float baseAngle = (i * (360f / Mathf.Max(1, count)) + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;

            // 1. Resolve existing instance or instantiate prefab early to measure its true 3D footprint
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

            if (data == null && i < existingTargets.Count)
            {
                matchingTarget = existingTargets[i];
            }

            if (matchingTarget != null)
            {
                landmarkGO = matchingTarget.gameObject;
            }
            else
            {
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

            if (landmarkGO == null) continue;
            landmarkGO.transform.SetParent(_landmarksParent, true);

            // Identify the actual landmark rather than assuming registry/scene ordering.
            var target = landmarkGO.GetComponent<EnvironmentFactTarget>();
            string landmarkId = (target != null ? target.FactId : data != null ? data.factId : landmarkGO.name).ToLowerInvariant();
            bool isCoral = landmarkId.Contains("coral_reef") || landmarkId.Contains("barrier_reef");
            bool isSeagrass = landmarkId.Contains("seagrass") || landmarkId.Contains("sea_grass") || landmarkId.Contains("kelp");
            BiomeBand targetBiome = isCoral ? BiomeBand.Hard : isSeagrass ? BiomeBand.Soft
                : target != null ? target.TargetBiome : GetTargetBiomeForLandmark(data, zoneIndex, i);

            // 2. Measure actual model footprint radius and geometry center offset in unrotated pose
            landmarkGO.transform.rotation = Quaternion.identity;
            landmarkGO.transform.localScale = Vector3.one;
            float measuredFootprintRadius = 14f;
            Vector3 boundsCenterOffset = Vector3.zero;
            var initialRenderers = landmarkGO.GetComponentsInChildren<Renderer>(true);
            if (initialRenderers != null && initialRenderers.Length > 0)
            {
                Bounds initBounds = initialRenderers[0].bounds;
                for (int r = 1; r < initialRenderers.Length; r++)
                    initBounds.Encapsulate(initialRenderers[r].bounds);

                measuredFootprintRadius = new Vector2(initBounds.extents.x, initBounds.extents.z).magnitude;
                boundsCenterOffset = initBounds.center - landmarkGO.transform.position;
            }
            measuredFootprintRadius = Mathf.Max(measuredFootprintRadius, isSeagrass ? 22f : (isCoral ? 24f : 14f));

            Vector3 surfacePos = Vector3.zero;
            Vector3 candidatePos = Vector3.zero;
            Vector3 surfaceNormal = Vector3.up;

            // 3. Search the playable seabed, keeping the whole footprint in its habitat.
            bool foundFlatSpot = FindFlatBiomeLocation(
                terrain,
                targetBiome,
                baseAngle,
                w * 0.40f,
                length * 0.40f,
                footprintRadius: measuredFootprintRadius,
                out candidatePos,
                out Vector3 candidateNormal,
                out float bestHeightVariance,
                zoneIndex,
                isCoral,
                isSeagrass
            );

            // Retry with a smaller footprint, without relaxing habitat or depth rules.
            float placementScale = 1f;
            for (int retry = 0; !foundFlatSpot && retry < 2; retry++)
            {
                placementScale = retry == 0 ? 0.85f : 0.7f;
                foundFlatSpot = FindFlatBiomeLocation(terrain, targetBiome, baseAngle,
                    w * 0.40f, length * 0.40f, measuredFootprintRadius * placementScale,
                    out candidatePos, out candidateNormal, out bestHeightVariance,
                    zoneIndex, isCoral, isSeagrass);
            }

            if (!foundFlatSpot)
            {
                landmarkGO.SetActive(false);
                Debug.LogWarning($"[HabitatLandmarkSpawner] No suitable {targetBiome} habitat found for '{landmarkGO.name}' in Zone {zoneIndex}; landmark disabled instead of placed in the wrong habitat.");
                continue;
            }

            landmarkGO.transform.localScale = Vector3.one * placementScale;
            boundsCenterOffset *= placementScale;
            measuredFootprintRadius *= placementScale;
            landmarkGO.SetActive(true);

            // Raycast down onto the physical terrain mesh collider from above candidate point, ignoring props, species, and landmark colliders
            surfacePos = candidatePos;
            surfaceNormal = candidateNormal;
            int terrainMask = LayerMask.GetMask("Terrain", "Default");
            if (terrainMask == 0) terrainMask = ~0;
            Vector3 rayOrigin = new Vector3(candidatePos.x, candidatePos.y + raycastDropHeight, candidatePos.z);
            var hits = Physics.RaycastAll(rayOrigin, Vector3.down, raycastDropHeight * 2.5f, terrainMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int hIdx = 0; hIdx < hits.Length; hIdx++)
            {
                var hit = hits[hIdx];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(landmarkGO.transform)) continue;
                if (ContextSteering.IsEnvironmentProp(hit.collider) ||
                    hit.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                    hit.collider.CompareTag("Environment") ||
                    hit.collider.CompareTag("Species"))
                {
                    continue;
                }
                if (Mathf.Abs(hit.point.y - candidatePos.y) <= 1.0f && hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    surfacePos = hit.point;
                    if (hit.normal.y > 0.5f) surfaceNormal = hit.normal;
                }
            }

            // Subtle slope compliance (15%) + random yaw
            Quaternion randomYaw  = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            Quaternion finalRot   = Quaternion.Slerp(Quaternion.identity, slopeAlign, 0.15f) * randomYaw;
            landmarkGO.transform.rotation = finalRot;

            // Align landmark's geometric bounds center to surfacePos (so it's not lopsided from uncentered prefab pivot)
            landmarkGO.transform.position = surfacePos - (finalRot * boundsCenterOffset);

            // 4. Grounding to Lowest Footprint Point:
            // Samples the seabed across the entire landmark footprint centered on the geometry
            var renderers = landmarkGO.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                    combinedBounds.Encapsulate(renderers[r].bounds);

                float pivotY = landmarkGO.transform.position.y;
                float bottomOffset = pivotY - combinedBounds.min.y;
                float curFootprintRadius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z);
                Vector3 boundsCenter = combinedBounds.center;

                // Sample terrain height across outer and inner rings centered at the GEOMETRIC BOUNDS CENTER
                float minGroundY = terrain != null ? terrain.SampleHeight(boundsCenter.x, boundsCenter.z) : surfacePos.y;
                int outerSamples = 20;
                for (int s = 0; s < outerSamples; s++)
                {
                    float ang = s * (Mathf.PI * 2f / outerSamples);
                    float sx = boundsCenter.x + Mathf.Cos(ang) * curFootprintRadius;
                    float sz = boundsCenter.z + Mathf.Sin(ang) * curFootprintRadius;
                    float sy = terrain != null ? terrain.SampleHeight(sx, sz) : surfacePos.y;
                    if (sy < minGroundY) minGroundY = sy;
                }
                for (int s = 0; s < 10; s++)
                {
                    float ang = s * (Mathf.PI * 2f / 10f) + 0.39f;
                    float sx = boundsCenter.x + Mathf.Cos(ang) * (curFootprintRadius * 0.5f);
                    float sz = boundsCenter.z + Mathf.Sin(ang) * (curFootprintRadius * 0.5f);
                    float sy = terrain != null ? terrain.SampleHeight(sx, sz) : surfacePos.y;
                    if (sy < minGroundY) minGroundY = sy;
                }

                // Sinking depth embeds base rock into the sediment so that every edge rests solidly on the seabed
                float sink = isSeagrass ? 1.2f : (isCoral ? 0.75f : 0.6f);
                landmarkGO.transform.position = new Vector3(
                    landmarkGO.transform.position.x,
                    minGroundY + bottomOffset - sink + surfaceOffset,
                    landmarkGO.transform.position.z
                );
            }

            // 5. Adaptive Child Element Grounding:
            // Individually conform each child boulder/coral so that across sloped or undulating terrain,
            // every single rock and coral cluster rests solidly on the seabed with ZERO overhanging/floating parts!
            GroundCompositeChildren(landmarkGO, terrain);

            // Ensure survey and sonar components
            var factComp = landmarkGO.GetComponent<EnvironmentFactTarget>() ?? landmarkGO.AddComponent<EnvironmentFactTarget>();
            var trackable = landmarkGO.GetComponent<SonarTrackable>() ?? landmarkGO.AddComponent<SonarTrackable>();
            trackable.Initialize(SonarTrackable.SonarTargetType.Environment, factComp.HabitatName, factComp.IsSurveyed);

            // Ensure landmarks are on Terrain layer and have valid trigger colliders for creature avoidance
            EnsureCollidersAndLayer(landmarkGO);

            _registeredLandmarks.Add(new LandmarkZone
            {
                center = new Vector3(surfacePos.x, surfacePos.y, surfacePos.z),
                radius = measuredFootprintRadius,
                name = factComp.HabitatName
            });

            Debug.Log($"[HabitatLandmarkSpawner] Successfully placed '{factComp.HabitatName}' atop ocean floor at {surfacePos} (normal: {surfaceNormal}, biome: {targetBiome}, scale: {landmarkGO.transform.localScale.x:F2}).");
        }
    }

    /// <summary>
    /// Searches for a flat, level position matching the landmark's target biome band.
    /// Checks habitat, depth and slope across a 19-point footprint, including near zone edges.
    /// </summary>
    private bool FindFlatBiomeLocation(
        TerrainGenerator terrain,
        BiomeBand targetBiome,
        float baseAngle,
        float halfWidth,
        float halfLength,
        float footprintRadius,
        out Vector3 bestPos,
        out Vector3 bestNormal,
        out float bestHeightVariance,
        int zoneIndex = 0,
        bool isCoralReef = false,
        bool isSeagrass = false)
    {
        bestPos = Vector3.zero;
        bestNormal = Vector3.up;
        bestHeightVariance = float.MaxValue;

        if (terrain == null) return false;

        float bestScore = float.MinValue;
        float maxX = halfWidth - footprintRadius;
        float maxZ = halfLength - footprintRadius;
        if (maxX <= 0f || maxZ <= 0f) return false;

        const int sectorAttempts = 96;
        const int gridSize = 40;
        int totalAttempts = sectorAttempts + gridSize * gridSize;

        for (int attempt = 0; attempt < totalAttempts; attempt++)
        {
            float cx, cz;
            if (attempt < sectorAttempts)
            {
                float angle = baseAngle + Random.Range(-65f, 65f) * Mathf.Deg2Rad;
                float dist = Random.Range(0.16f, 0.85f) * Mathf.Min(maxX, maxZ);
                cx = Mathf.Cos(angle) * dist;
                cz = Mathf.Sin(angle) * dist;
            }
            else
            {
                // Cover the full rectangle when the preferred angular sector has no suitable habitat.
                int cell = attempt - sectorAttempts;
                cx = Mathf.Lerp(-maxX, maxX, (cell % gridSize + 0.5f) / gridSize);
                cz = Mathf.Lerp(-maxZ, maxZ, (cell / gridSize + 0.5f) / gridSize);
            }

            bool overlapsLandmark = false;
            foreach (var placed in _registeredLandmarks)
            {
                float dx = cx - placed.center.x;
                float dz = cz - placed.center.z;
                float clearance = footprintRadius + placed.radius + 5f;
                if (dx * dx + dz * dz < clearance * clearance)
                {
                    overlapsLandmark = true;
                    break;
                }
            }
            if (overlapsLandmark) continue;

            float centerH = terrain.SampleHeight(cx, cz);
            Vector3 centerNormal = terrain.SampleNormal(cx, cz);
            if (centerNormal.y < 0.90f) continue;

            float minH = centerH;
            float maxH = centerH;
            float minNormalY = centerNormal.y;
            bool validFootprint = true;
            for (int sample = 0; sample < 19; sample++)
            {
                // Center, 12 outer points and 6 inner points.
                float radius = sample == 0 ? 0f : sample <= 12 ? footprintRadius : footprintRadius * 0.5f;
                float angle = sample <= 12
                    ? (sample - 1) * Mathf.PI * 2f / 12f
                    : (sample - 13) * Mathf.PI * 2f / 6f + 0.39f;
                float px = cx + Mathf.Cos(angle) * radius;
                float pz = cz + Mathf.Sin(angle) * radius;
                float height = terrain.SampleHeight(px, pz);
                float normalY = terrain.SampleNormal(px, pz).y;
                float depth = -height;

                // Habitat and sunlight depth requirements never relax on later attempts.
                bool wrongDepth = zoneIndex == 0 &&
                    ((isSeagrass && (depth < 5f || depth > 40f)) ||
                     (isCoralReef && (depth < 10f || depth > 50f)));
                if (terrain.GetBiomeAt(px, pz) != targetBiome || wrongDepth || normalY < 0.85f)
                {
                    validFootprint = false;
                    break;
                }
                minH = Mathf.Min(minH, height);
                maxH = Mathf.Max(maxH, height);
                minNormalY = Mathf.Min(minNormalY, normalY);
            }
            float heightVariance = maxH - minH;
            if (!validFootprint || heightVariance > 2.5f) continue;

            float score = centerNormal.y * 60f + minNormalY * 40f - heightVariance * 15f;

            if (zoneIndex == 0 && isCoralReef)
            {
                float depthScore = 60f - Mathf.Abs(centerH - (-28f)) * 2.5f;
                score += depthScore;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = new Vector3(cx, centerH, cz);
                bestNormal = centerNormal;
                bestHeightVariance = heightVariance;
                if (centerNormal.y >= 0.95f && heightVariance <= 1.4f)
                    break;
            }
        }

        // No unchecked position fallback: the caller retries a smaller footprint or skips placement.
        return bestScore > float.MinValue;
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

    private void EnsureCollidersAndLayer(GameObject go)
    {
        if (go == null) return;

        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0)
        {
            SetLayerRecursive(go, terrainLayer);
        }

        string landmarkName = go.name.ToLowerInvariant();
        bool isLandmarkSeagrass = landmarkName.Contains("seagrass") || landmarkName.Contains("sea_grass") || landmarkName.Contains("kelp");

        // Keep root collider as a trigger (it is the discovery/scan area for the landmark)
        var rootCollider = go.GetComponent<Collider>();
        if (rootCollider != null)
        {
            rootCollider.isTrigger = true;
        }

        // Process all child renderers to ensure solid (corals, rocks) or passable (seagrass) colliders
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject == go) continue; // Skip root

                string childName = r.gameObject.name.ToLowerInvariant();
                bool isPassable = isLandmarkSeagrass && (childName.Contains("seagrass") || childName.Contains("sea_grass") || childName.Contains("kelp") || childName.Contains("grass"));

                var childCols = r.GetComponents<Collider>();
                if (childCols == null || childCols.Length == 0)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = r.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = isPassable;
                        mc.isTrigger = isPassable;
                    }
                }
                else
                {
                    for (int c = 0; c < childCols.Length; c++)
                    {
                        if (childCols[c] != null)
                        {
                            if (isPassable)
                            {
                                if (childCols[c] is MeshCollider mc)
                                    mc.convex = true;
                                childCols[c].isTrigger = true;
                            }
                            else
                            {
                                childCols[c].isTrigger = false;
                            }
                        }
                    }
                }
            }
        }

        // Sweep all colliders in landmark hierarchy to ensure correct trigger flags
        var allColliders = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            var c = allColliders[i];
            if (c == null) continue;
            if (c.gameObject == go)
            {
                c.isTrigger = true; // Root landmark discovery trigger volume
                continue;
            }

            string cName = c.gameObject.name.ToLowerInvariant();
            bool isPassable = isLandmarkSeagrass && (cName.Contains("seagrass") || cName.Contains("sea_grass") || cName.Contains("kelp") || cName.Contains("grass"));

            if (isPassable)
            {
                if (c is MeshCollider mc) mc.convex = true;
                c.isTrigger = true;
            }
            else
            {
                c.isTrigger = false;
            }
        }

        SetTagRecursive(go, "Environment");
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static void SetTagRecursive(GameObject go, string tag)
    {
        if (go == null) return;
        go.tag = tag;
        foreach (Transform child in go.transform)
            SetTagRecursive(child.gameObject, tag);
    }

    /// <summary>
    /// For large composite landmarks (e.g. Coral Reefs, Kelp/Seagrass beds) that span 30-45 meters across undulating seabed,
    /// dynamically grounds each child rock/coral element so no boulder or coral overhangs or floats into open water.
    /// </summary>
    private void GroundCompositeChildren(GameObject rootGO, TerrainGenerator terrain)
    {
        if (rootGO == null || terrain == null) return;

        foreach (Transform child in rootGO.transform)
        {
            var childRenderers = child.GetComponentsInChildren<Renderer>();
            if (childRenderers == null || childRenderers.Length == 0) continue;

            string n = child.name.ToLower();
            // Preserve spires intentionally perched atop a carbonate mound (e.g. LopheliaSpire on CarbonateBioherm)
            if (n.Contains("lopheliaspire")) continue;

            Bounds b = childRenderers[0].bounds;
            for (int i = 1; i < childRenderers.Length; i++)
                b.Encapsulate(childRenderers[i].bounds);

            // Sample seabed at this child's world coordinate (center of bounds)
            float floorY = terrain.SampleHeight(b.center.x, b.center.z);

            // Also sample 4 perimeter points around child footprint
            float r = Mathf.Max(b.extents.x, b.extents.z) * 0.65f;
            float minFloorY = floorY;
            for (int a = 0; a < 4; a++)
            {
                float ang = a * (Mathf.PI * 0.5f);
                float px = b.center.x + Mathf.Cos(ang) * r;
                float pz = b.center.z + Mathf.Sin(ang) * r;
                float fy = terrain.SampleHeight(px, pz);
                if (fy < minFloorY) minFloorY = fy;
            }

            // Determine natural embed sink depth based on object type
            float sink = n.Contains("rock")
                ? Mathf.Clamp(b.size.y * 0.22f, 0.4f, 1.8f)
                : Mathf.Min(0.35f, b.size.y * 0.22f);

            // Align the bottom of this child's geometry to the local seabed (both lowering floating elements
            // and raising elements that would otherwise be buried under sloped sediment)
            float bottomY = b.min.y;
            float targetBottomY = minFloorY - sink;

            float dy = targetBottomY - bottomY;
            if (Mathf.Abs(dy) > 0.03f)
            {
                child.position += new Vector3(0f, dy, 0f);
            }
        }
    }
}
