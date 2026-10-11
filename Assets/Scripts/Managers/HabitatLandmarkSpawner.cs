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

            // 2. Measure actual model footprint radius and geometry center offset
            float measuredFootprintRadius = 14f;
            Vector3 boundsCenterOffset = Vector3.zero;
            var initialRenderers = landmarkGO.GetComponentsInChildren<Renderer>(true);
            if (initialRenderers != null && initialRenderers.Length > 0)
            {
                Bounds initBounds = initialRenderers[0].bounds;
                for (int r = 1; r < initialRenderers.Length; r++)
                    initBounds.Encapsulate(initialRenderers[r].bounds);

                measuredFootprintRadius = Mathf.Max(initBounds.extents.x, initBounds.extents.z);
                boundsCenterOffset = initBounds.center - landmarkGO.transform.position;
            }
            measuredFootprintRadius = Mathf.Max(measuredFootprintRadius, isSeagrass ? 22f : (isCoral ? 24f : 14f));

            Vector3 surfacePos;
            Vector3 surfaceNormal = Vector3.up;

            // 3. Search for a flat, level surface using 13-point concentric footprint sampling
            bool foundFlatSpot = FindFlatBiomeLocation(
                terrain,
                targetBiome,
                baseAngle,
                w * 0.40f,
                footprintRadius: measuredFootprintRadius,
                out Vector3 candidatePos,
                out Vector3 candidateNormal,
                out float bestHeightVariance,
                zoneIndex,
                isCoral,
                isSeagrass
            );

            // If rugged seabed terrain detected (variance > 2.5m), scale down landmark slightly (85%)
            bool shouldScaleDown = bestHeightVariance > 2.5f || !foundFlatSpot;
            if (shouldScaleDown)
            {
                landmarkGO.transform.localScale = Vector3.one * 0.85f;
                Debug.Log($"[HabitatLandmarkSpawner] Rugged seabed terrain detected for '{landmarkGO.name}' (variance={bestHeightVariance:F1}m). Scaled to 85% to fit snugly without overhanging.");
            }
            else
            {
                landmarkGO.transform.localScale = Vector3.one;
            }

            // Raycast down onto the physical terrain mesh collider from above candidate point
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

            Debug.Log($"[HabitatLandmarkSpawner] Successfully placed '{factComp.HabitatName}' atop ocean floor at {surfacePos} (normal: {surfaceNormal}, biome: {targetBiome}, scale: {landmarkGO.transform.localScale.x:F2}).");
        }
    }

    /// <summary>
    /// Searches for a flat, level position matching the landmark's target biome band.
    /// Samples a 13-point concentric footprint grid to detect and avoid ridges, slopes, and drop-offs.
    /// </summary>
    private bool FindFlatBiomeLocation(
        TerrainGenerator terrain,
        BiomeBand targetBiome,
        float baseAngle,
        float maxDist,
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
        bool found = false;

        int totalAttempts = (isCoralReef || isSeagrass) ? 120 : 80;

        for (int attempt = 0; attempt < totalAttempts; attempt++)
        {
            float angleOffset = Random.Range(-65f, 65f) * Mathf.Deg2Rad;
            float angle = baseAngle + angleOffset;
            float dist = Random.Range(maxDist * 0.16f, maxDist * 0.48f);

            float cx = Mathf.Cos(angle) * dist;
            float cz = Mathf.Sin(angle) * dist;

            // 1. Biome Check
            BiomeBand currentBiome = terrain.GetBiomeAt(cx, cz);
            bool biomeMatch = (currentBiome == targetBiome);

            // Coral reef landmark MUST be in Hard biome if possible
            if (isCoralReef && currentBiome != BiomeBand.Hard && attempt < 80)
                continue;

            // Seagrass bed prefers soft sediment
            if (isSeagrass && currentBiome != BiomeBand.Soft && attempt < 80)
                continue;

            if (!biomeMatch && !isCoralReef && attempt < 35)
                continue;

            float centerH = terrain.SampleHeight(cx, cz);

            // 2. Depth Check: Coral Reef in Sunlight Zone belongs on shallow sunlit plateau at depth 18m to 42m (Y in [-42f, -18f])
            if (zoneIndex == 0 && isCoralReef)
            {
                if (centerH < -45f || centerH > -15f)
                {
                    if (attempt < 80 || centerH < -55f || centerH > -10f)
                        continue;
                }
            }

            // 3. Center Flatness Check
            Vector3 centerNormal = terrain.SampleNormal(cx, cz);
            float centerFlatness = centerNormal.y;
            if (centerFlatness < 0.90f)
                continue;

            // 4. Multi-Point Footprint Sampling (Center + 12 outer ring + 6 inner ring = 19 points)
            float minH = centerH;
            float maxH = centerH;
            float minNormalY = centerFlatness;

            // Outer ring (12 points)
            for (int k = 0; k < 12; k++)
            {
                float rad = k * (Mathf.PI * 2f / 12f);
                float px = cx + Mathf.Cos(rad) * footprintRadius;
                float pz = cz + Mathf.Sin(rad) * footprintRadius;
                float ph = terrain.SampleHeight(px, pz);
                Vector3 pn = terrain.SampleNormal(px, pz);
                if (ph < minH) minH = ph;
                if (ph > maxH) maxH = ph;
                if (pn.y < minNormalY) minNormalY = pn.y;
            }

            // Inner ring (6 points)
            for (int k = 0; k < 6; k++)
            {
                float rad = (k * (Mathf.PI * 2f / 6f)) + 0.39f;
                float px = cx + Mathf.Cos(rad) * (footprintRadius * 0.5f);
                float pz = cz + Mathf.Sin(rad) * (footprintRadius * 0.5f);
                float ph = terrain.SampleHeight(px, pz);
                Vector3 pn = terrain.SampleNormal(px, pz);
                if (ph < minH) minH = ph;
                if (ph > maxH) maxH = ph;
                if (pn.y < minNormalY) minNormalY = pn.y;
            }

            float heightVariance = maxH - minH;

            // Reject if slope drops off too severely across landmark footprint
            if (minNormalY < 0.85f)
                continue;

            if (heightVariance > 2.5f)
                continue;

            // Score candidate
            float score = (biomeMatch ? 150f : 0f) + (centerFlatness * 60f) + (minNormalY * 40f) - (heightVariance * 15f);

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
                found = (heightVariance <= 2.0f);

                // Early exit on near-ideal plateau
                if (biomeMatch && centerFlatness >= 0.95f && heightVariance <= 1.4f)
                {
                    if (!isCoralReef || (centerH >= -40f && centerH <= -18f))
                        break;
                }
            }
        }

        // If no candidate met strict variance <= 2.8m, but we found a valid best score, use bestPos
        if (bestScore > float.MinValue)
        {
            return found;
        }

        bestPos = new Vector3(Mathf.Cos(baseAngle) * maxDist * 0.28f, terrain.SampleHeight(Mathf.Cos(baseAngle) * maxDist * 0.28f, Mathf.Sin(baseAngle) * maxDist * 0.28f), Mathf.Sin(baseAngle) * maxDist * 0.28f);
        bestNormal = terrain.SampleNormal(bestPos.x, bestPos.z);
        bestHeightVariance = 4.0f;
        return false;
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
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0)
        {
            SetLayerRecursive(go, terrainLayer);
        }

        var colliders = go.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var mf = renderers[i].GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = renderers[i].gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                        mc.isTrigger = true;
                    }
                }
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
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
            string n = child.name.ToLower();
            float sink = n.Contains("rock") ? Mathf.Clamp(b.size.y * 0.22f, 0.4f, 1.8f) : 0.35f;

            // If the bottom of this child's geometry is above the seabed (floating in open water),
            // or hanging off a slope drop-off, lower it so it embeds solidly into the sediment!
            float bottomY = b.min.y;
            float targetBottomY = minFloorY - sink;

            float dy = targetBottomY - bottomY;
            // Lower floating elements, or slightly raise elements that are buried too deeply under sediment
            if (dy < -0.05f || dy > 1.5f)
            {
                child.position += new Vector3(0f, dy, 0f);
            }
        }
    }
}
