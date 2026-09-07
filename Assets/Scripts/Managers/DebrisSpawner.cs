using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns 3D marine debris clusters across the ocean floor.
/// Integrates with TerrainGenerator / OceanFloorMeshGenerator.
///
/// Features:
///   - Scatters 3 to 6 interactive debris piles per zone.
///   - Automatically creates 3D visual geometry (rusted drums, pipes, plastic waste piles).
///   - Attaches DebrisCluster and SonarTrackable (Amber radar blip).
/// </summary>
public class DebrisSpawner : MonoBehaviour
{
    [Header("Spawn Parameters")]
    [Tooltip("Number of debris clusters to place in this zone.")]
    [Range(2, 8)] [SerializeField] private int clusterCount = 4;
    [Tooltip("Custom 3D prefab for debris clusters (optional - auto-generates 3D cluster if null).")]
    [SerializeField] private GameObject customDebrisPrefab;

    private Transform _debrisParent;
    private bool      _hasSpawned = false;

    private void Start()
    {
        var terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null && !terrain.HasGenerated)
        {
            return;
        }
        if (!_hasSpawned) SpawnDebrisForZone(ZoneManager.CurrentZoneIndex);
    }

    public void SpawnDebrisForZone(int zoneIndex)
    {
        _hasSpawned = true;
        EnsureParent();
        ClearExistingDebris();

        var terrain = FindFirstObjectByType<TerrainGenerator>();
        float w = 500f;
        float l = 500f;
        float d = 150f;

        if (ZoneConfig.IsValidZone(zoneIndex))
        {
            var def = ZoneConfig.Zones[zoneIndex];
            w = def.playableWidth;
            l = def.playableLength;
            d = def.playableDepth;
        }

        float halfW = w * 0.40f;
        float halfL = l * 0.40f;

        for (int i = 0; i < clusterCount; i++)
        {
            float rx = Random.Range(-halfW, halfW);
            float rz = Random.Range(-halfL, halfL);

            float floorY = terrain != null ? terrain.SampleHeight(rx, rz) : -d;

            Vector3 rayOrigin = new Vector3(rx, floorY + 30f, rz);
            Vector3 spawnPos;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point;
            }
            else
            {
                spawnPos = new Vector3(rx, floorY, rz);
            }
            SpawnSingleCluster(spawnPos, i);
        }

        Debug.Log($"[DebrisSpawner] Spawned {clusterCount} 3D marine debris clusters in Zone {zoneIndex}.");

        // Register all debris with distance culling for performance
        if (DistanceCullingManager.Instance != null && _debrisParent != null)
            DistanceCullingManager.Instance.RegisterParent(_debrisParent);
    }

    private void SpawnSingleCluster(Vector3 position, int index)
    {
        GameObject go;
        if (customDebrisPrefab != null)
        {
            go = Instantiate(customDebrisPrefab, position, Quaternion.identity, _debrisParent);
        }
        else
        {
            go = Create3DDebrisClusterObject(position);
        }

        go.name = $"DebrisCluster_{index + 1}";

        var cluster = go.GetComponent<DebrisCluster>();
        if (cluster == null) cluster = go.AddComponent<DebrisCluster>();
    }

    private GameObject Create3DDebrisClusterObject(Vector3 pos)
    {
        var rootGO = new GameObject("DebrisClusterRoot");
        rootGO.transform.SetParent(_debrisParent, false);
        rootGO.transform.position = pos;

        // 1. Rusted Oil Drum
        var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        drum.transform.SetParent(rootGO.transform, false);
        drum.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        drum.transform.localScale = new Vector3(1.2f, 1.4f, 1.2f);
        drum.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 15f);
        drum.GetComponent<Renderer>().material = CreateMaterial(new Color(0.45f, 0.32f, 0.22f)); // Rusted iron

        // 2. Metal Scrap Pipe
        var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.transform.SetParent(rootGO.transform, false);
        pipe.transform.localPosition = new Vector3(0.9f, 0.3f, 0.4f);
        pipe.transform.localScale = new Vector3(0.6f, 1.6f, 0.6f);
        pipe.transform.localRotation = Quaternion.Euler(0f, 45f, 80f);
        pipe.GetComponent<Renderer>().material = CreateMaterial(new Color(0.55f, 0.50f, 0.42f));

        // 3. Plastic Waste Pile
        var plastic = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plastic.transform.SetParent(rootGO.transform, false);
        plastic.transform.localPosition = new Vector3(-0.8f, 0.25f, -0.3f);
        plastic.transform.localScale = new Vector3(1.4f, 0.5f, 1.1f);
        plastic.transform.localRotation = Quaternion.Euler(5f, Random.Range(0f, 360f), 0f);
        plastic.GetComponent<Renderer>().material = CreateMaterial(new Color(0.85f, 0.90f, 0.95f, 0.8f));

        // 4. Toxic / Battery Canister
        var batt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        batt.transform.SetParent(rootGO.transform, false);
        batt.transform.localPosition = new Vector3(0.3f, 0.2f, -0.9f);
        batt.transform.localScale = new Vector3(0.7f, 0.4f, 0.5f);
        batt.GetComponent<Renderer>().material = CreateMaterial(new Color(0.2f, 0.8f, 0.3f)); // Toxic green

        return rootGO;
    }

    private void EnsureParent()
    {
        if (_debrisParent == null)
        {
            var existing = GameObject.Find("_DebrisClusters");
            if (existing != null) _debrisParent = existing.transform;
            else _debrisParent = new GameObject("_DebrisClusters").transform;
        }
    }

    private void ClearExistingDebris()
    {
        if (_debrisParent == null) return;
        for (int i = _debrisParent.childCount - 1; i >= 0; i--)
        {
            Destroy(_debrisParent.GetChild(i).gameObject);
        }
    }

    private static Material CreateMaterial(Color col)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");
        var mat = new Material(shader);
        mat.color = col;
        return mat;
    }
}
