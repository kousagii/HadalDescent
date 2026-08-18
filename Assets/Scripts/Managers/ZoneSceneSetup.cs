using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach this to any zone scene root object to automatically:
///   1. Size invisible boundary walls to match this zone's ZoneConfig dimensions.
///   2. Create top and bottom trigger colliders at the correct Y positions.
///   3. Create spawn point GameObjects if they don't already exist.
///
/// Click "Setup Zone Boundaries" in the Inspector (Editor-only button) to run it.
/// At runtime it also self-validates on Awake.
/// </summary>
public class ZoneSceneSetup : MonoBehaviour
{
    [Header("Which zone is this scene?")]
    [SerializeField] private int zoneIndex = 0;

    [Header("Boundary objects (auto-created / found)")]
    [SerializeField] private GameObject topBoundary;
    [SerializeField] private GameObject bottomBoundary;
    [SerializeField] private GameObject topSpawnPoint;
    [SerializeField] private GameObject bottomSpawnPoint;
    [SerializeField] private GameObject oceanFloor;

    private void Awake()
    {
        Validate();
    }

    private void Validate()
    {
        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            Debug.LogError($"[ZoneSceneSetup] zoneIndex {zoneIndex} is out of range!");
            return;
        }

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];
        Debug.Log($"[ZoneSceneSetup] Scene validated for '{zone.zoneName}' | " +
                  $"Playable: {zone.playableWidth}×{zone.playableLength}×{zone.playableDepth} m");
    }

    // -----------------------------------------------------------------------
    // Editor-only setup method
    // -----------------------------------------------------------------------

#if UNITY_EDITOR
    [ContextMenu("Setup Zone Boundaries")]
    public void SetupInEditor()
    {
        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            Debug.LogError("Invalid zoneIndex.");
            return;
        }

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];

        float W = zone.playableWidth;
        float L = zone.playableLength;
        float D = zone.playableDepth;
        float thickness = 5f;

        // ── Top Boundary (Y = 10, well above surface spawn) ───────────────
        topBoundary = EnsureGameObject("TopBoundary", topBoundary);
        topBoundary.transform.position = new Vector3(0f, 10f, 0f);
        SetupTriggerBox(topBoundary, new Vector3(W, thickness, L));
        EnsureTag(topBoundary, "Untagged");
        var topTrigger = EnsureComponent<ZoneBoundaryTrigger>(topBoundary);
        SetPrivateField(topTrigger, "isBottomBoundary", false);

        // ── Bottom Boundary (Y = -D - thickness/2) ─────────────────────────
        bottomBoundary = EnsureGameObject("BottomBoundary", bottomBoundary);
        bottomBoundary.transform.position = new Vector3(0f, -D - thickness / 2f, 0f);
        SetupTriggerBox(bottomBoundary, new Vector3(W, thickness, L));
        var botTrigger = EnsureComponent<ZoneBoundaryTrigger>(bottomBoundary);
        SetPrivateField(botTrigger, "isBottomBoundary", true);

        // ── Perimeter Side Boundaries (Invisible Colliders) ───────────────
        EnsureSideWall("Wall_North", new Vector3(0f, -D * 0.5f,  L * 0.5f), new Vector3(W, D + 40f, thickness));
        EnsureSideWall("Wall_South", new Vector3(0f, -D * 0.5f, -L * 0.5f), new Vector3(W, D + 40f, thickness));
        EnsureSideWall("Wall_East",  new Vector3( W * 0.5f, -D * 0.5f, 0f), new Vector3(thickness, D + 40f, L));
        EnsureSideWall("Wall_West",  new Vector3(-W * 0.5f, -D * 0.5f, 0f), new Vector3(thickness, D + 40f, L));

        // ── Spawn Points ───────────────────────────────────────────────────
        topSpawnPoint = EnsureGameObject("ZoneTopSpawn", topSpawnPoint);
        topSpawnPoint.transform.position = new Vector3(0f, -10f, 0f); // slightly below surface
        EnsureTag(topSpawnPoint, "ZoneTopSpawn");

        bottomSpawnPoint = EnsureGameObject("ZoneBottomSpawn", bottomSpawnPoint);
        bottomSpawnPoint.transform.position = new Vector3(0f, -D + 15f, 0f); // slightly above floor
        EnsureTag(bottomSpawnPoint, "ZoneBottomSpawn");

        Debug.Log($"[ZoneSceneSetup] '{zone.zoneName}' boundaries created: " +
                  $"{W}×{L}×{D} m | Top Y=0 | Bottom Y={-D}");

        EditorUtility.SetDirty(gameObject);
    }

    private void EnsureSideWall(string name, Vector3 pos, Vector3 size)
    {
        var wall = EnsureGameObject(name, null);
        wall.transform.position = pos;
        var col = EnsureComponent<BoxCollider>(wall);
        col.isTrigger = false; // Solid invisible physics wall
        col.size = size;
        wall.layer = LayerMask.NameToLayer("Terrain") >= 0 ? LayerMask.NameToLayer("Terrain") : 0;
    }

    // ── Editor helpers ──────────────────────────────────────────────────────

    private static GameObject EnsureGameObject(string name, GameObject existing)
    {
        if (existing != null) return existing;
        var found = GameObject.Find(name);
        return found != null ? found : new GameObject(name);
    }

    private static void SetupTriggerBox(GameObject go, Vector3 size)
    {
        var col = EnsureComponent<BoxCollider>(go);
        col.isTrigger = true;
        col.size      = size;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void EnsureTag(GameObject go, string tag)
    {
        try { go.tag = tag; }
        catch { /* tag not defined in project yet — add it in Tags & Layers */ }
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }
#endif
}
