using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to configure the Submarine visual prefab for both gameplay and Minigame 4 (Hazard Dodge).
/// 
/// Solves:
/// 1. Correct scale (0.08x) so it matches the realistic 3.2m length and fits 3-lane tracks.
/// 2. Correct rotation (Y = 90 deg) so the cockpit points forward (+Z).
/// 3. Correct pivot centering (offset Y = -0.75m).
/// 4. Strips unwanted collision/physics so it never conflicts with gameplay movement or minigame logic.
/// </summary>
public static class SubmarinePrefabUtility
{
    private const string GlbPath = "Assets/Arts/Submarine/Submarine.glb";
    private const string PrefabSavePath = "Assets/Prefabs/Submarine/Submarine_Visual.prefab";
    private const string ResourcesSavePath = "Assets/Resources/Submarine_Visual.prefab";

    [InitializeOnLoadMethod]
    private static void AutoEnsureVisualPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath) == null)
        {
            CreateSubmarineVisualPrefab();
        }
    }

    [MenuItem("HadalDescent/Submarine/1. Create Calibrated Submarine Visual Prefab")]
    public static GameObject CreateSubmarineVisualPrefab()
    {
        var glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
        if (glbModel == null)
        {
            Debug.LogError($"[SubmarinePrefabUtility] Could not load Submarine.glb at '{GlbPath}'");
            return null;
        }

        // Create root wrapper
        var rootGO = new GameObject("Submarine_Visual");

        // Instantiate model as child
        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(glbModel, rootGO.transform);
        modelInstance.name = "Submarine_Model";

        // Calibrate transform:
        // Raw GLB: Length ~40m along X, Cockpit at +X, Propeller at -X, Center Y at ~9.4m
        // Target: 2.5x larger scale (0.08 * 2.5 = 0.20f), Cockpit facing +Z Forward (rotated 180 deg so it faces forward down the track)
        const float uniformScale = 0.20f;
        modelInstance.transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
        modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // Cockpit faces +Z Forward down the minigame track
        modelInstance.transform.localPosition = new Vector3(0f, -1.875f, 0.375f); // Centers hull at root (0, 0, 0)

        // Clean any Rigidbodies/Colliders on the visual model so they don't break player physics
        foreach (var rb in rootGO.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(rb);
        }
        foreach (var col in rootGO.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(col);
        }

        // Ensure directories exist
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Submarine"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Submarine");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Save prefab
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, PrefabSavePath);
        PrefabUtility.SaveAsPrefabAsset(rootGO, ResourcesSavePath);

        Object.DestroyImmediate(rootGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#55ff88>[SubmarinePrefabUtility] Successfully created calibrated visual prefab at '{PrefabSavePath}' and '{ResourcesSavePath}'!</color>");
        return savedPrefab;
    }

    [MenuItem("HadalDescent/Submarine/2. Apply Submarine Visual to Active Scene Player")]
    public static void ApplyToScenePlayer()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) playerGO = pm.gameObject;
        }

        if (playerGO == null)
        {
            Debug.LogWarning("[SubmarinePrefabUtility] No GameObject with 'Player' tag or PlayerMovement found in active scene.");
            return;
        }

        // Load or create the visual prefab
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath);
        if (visualPrefab == null)
        {
            visualPrefab = CreateSubmarineVisualPrefab();
        }

        if (visualPrefab == null) return;

        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Apply Submarine Visual");

        // Disable or remove the primitive capsule renderer on the player root
        var meshRenderer = playerGO.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        // Remove any old Submarine_Visual child
        var oldVisual = playerGO.transform.Find("Submarine_Visual");
        if (oldVisual != null)
        {
            Undo.DestroyObjectImmediate(oldVisual.gameObject);
        }

        // Attach calibrated visual prefab
        var newVisual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, playerGO.transform);
        newVisual.name = "Submarine_Visual";
        newVisual.transform.localPosition = Vector3.zero;
        newVisual.transform.localRotation = Quaternion.identity;
        newVisual.transform.localScale = Vector3.one;

        // Position first-person camera at cockpit window
        var camTransform = playerGO.transform.Find("Main Camera");
        if (camTransform != null)
        {
            Undo.RecordObject(camTransform, "Reposition Cockpit Camera");
            // Cockpit glass is located around Z = +0.65m, Y = +0.25m
            camTransform.localPosition = new Vector3(0f, 0.25f, 0.65f);
            var cam = camTransform.GetComponent<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.15f; // Prevents interior hull clipping
            }
        }

        EditorUtility.SetDirty(playerGO);
        Debug.Log($"<color=#55ff88>[SubmarinePrefabUtility] Applied Submarine Visual to '{playerGO.name}' in scene! (Cockpit camera positioned at Z=+0.65m)</color>");
    }

    [MenuItem("HadalDescent/Submarine/3. Make Submarine Invisible to Camera (First-Person View)")]
    public static void MakeSubmarineInvisibleInActiveScene()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) playerGO = pm.gameObject;
        }

        if (playerGO == null)
        {
            Debug.LogWarning("[SubmarinePrefabUtility] No Player found in active scene.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(playerGO, "Make Submarine Invisible");

        // 1. Disable root MeshRenderer
        var rootRenderer = playerGO.GetComponent<MeshRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        // 2. Disable all child visual renderers (Submarine_Visual, capsule, etc.)
        var childRenderers = playerGO.GetComponentsInChildren<Renderer>(true);
        foreach (var r in childRenderers)
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null)
                continue;
            r.enabled = false;
        }

        EditorUtility.SetDirty(playerGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(playerGO.scene);
        Debug.Log($"<color=#55ff88>[SubmarinePrefabUtility] Successfully made submarine invisible to camera in '{playerGO.scene.name}'!</color>");
    }
}
