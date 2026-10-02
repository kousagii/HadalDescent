using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that ensures the Spotted Jellyfish tentacles (Cylinder.001 mesh)
/// are active and properly restored in both Edit Mode and Play Mode.
/// </summary>
public static class SpottedJellyfishPrefabUtility
{
    private const string PrefabPath = "Assets/Prefabs/Creatures/Sunlight Zone/Spotted Jellyfish.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRestoreJellyfishTentacles()
    {
        RestoreSpottedJellyfishTentacles();
    }

    [MenuItem("HadalDescent/Creatures/Restore Spotted Jellyfish Tentacles")]
    public static void RestoreSpottedJellyfishTentacles()
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
                    if (!t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(true);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Debug.Log($"[SpottedJellyfishPrefabUtility] Restored tentacles in '{PrefabPath}'");
            }
        }
    }
}
