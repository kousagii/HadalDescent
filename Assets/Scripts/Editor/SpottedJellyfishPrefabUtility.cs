using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that automatically deactivates the legacy opaque Cylinder artifact
/// inside the Spotted Jellyfish prefab so it is clean both in Edit Mode and in Play Mode.
/// </summary>
public static class SpottedJellyfishPrefabUtility
{
    private const string PrefabPath = "Assets/Prefabs/Creatures/Sunlight Zone/Spotted Jellyfish.prefab";

    [InitializeOnLoadMethod]
    private static void AutoCleanJellyfishPrefab()
    {
        CleanSpottedJellyfishPrefab();
    }

    [MenuItem("HadalDescent/Creatures/Fix Spotted Jellyfish Cylinders")]
    public static void CleanSpottedJellyfishPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            return;

        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = editingScope.prefabContentsRoot;
            if (root == null) return;

            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject != root && t.name.IndexOf("Cylinder", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Debug.Log($"[SpottedJellyfishPrefabUtility] Deactivated cylinder artifact in '{PrefabPath}'");
            }
        }
    }
}
