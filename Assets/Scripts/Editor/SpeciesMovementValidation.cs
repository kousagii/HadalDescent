#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only imported-model and contact checks, runnable in batch or from the menu.</summary>
public static class SpeciesMovementValidation
{
    [MenuItem("HadalDescent/Species/Validate Movement and Grounding")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run species asset validation outside Play Mode.");
        int checks = 0;
        int models = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("SpeciesGroundingChecks");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        var spawner = host.AddComponent<SpeciesSpawner>();
        var ground = typeof(SpeciesSpawner).GetMethod("AdjustContactHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SpeciesData", new[] { "Assets/Scripts/ScriptableObjects/Species" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<SpeciesData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data.zoneIndex != 1 && data.zoneIndex != 2 &&
                    !(data.zoneIndex == 0 && (data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower))) continue;
                Vector3 authoredNose = Vector3.zero;
                if (data.speciesId == "albatross_coffinfish_001" || data.speciesId == "sulu_ribbonfish_001") authoredNose = Vector3.right;
                else if (data.speciesId == "threelight_hatchetfish_001" || data.speciesId == "shorttail_lanternshark_001") authoredNose = Vector3.left;
                if (authoredNose != Vector3.zero)
                {
                    Quaternion heading = Quaternion.Euler(-10f, 125f, 15f);
                    Quaternion model = CreatureFacing.ModelRotation(heading, data.modelYawOffset);
                    Require(Vector3.Dot(model * authoredNose, heading * Vector3.forward) > 0.999f, data.commonName + " facing correction does not align its nose with travel.");
                    checks++;
                }
                if (data.speciesId == "deepwater_stingray_001")
                {
                    Require(data.benthicSupportPoints != null && data.benthicSupportPoints.Length == 5 && data.benthicSurfaceOffset > 0f, "Stingray animation/underside allowance is missing.");
                    foreach (Vector3 support in data.benthicSupportPoints)
                        Require(support.y <= -1.32f, "Stingray support point misses the downward wing stroke.");
                    checks++;
                }
                if (data.speciesId == "ring_batfish_001" || data.speciesId == "albatross_coffinfish_001")
                {
                    Require(!data.isStationary && data.locomotionArchetype == LocomotionArchetype.BenthicFollower && data.moveSpeed > 0f,
                        data.commonName + " must have active seabed movement.");
                    checks++;
                }
                if (data.modelPrefab == null) continue;
                models++;
                var instance = UnityEngine.Object.Instantiate(data.modelPrefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                try
                {
                    foreach (var animator in instance.GetComponentsInChildren<Animator>())
                    {
                        animator.applyRootMotion = false;
                        if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null) animator.Update(0f);
                    }
                    Quaternion pose = data.modelPrefab.transform.localRotation;
                    instance.transform.SetPositionAndRotation(new Vector3(73f, -85f, -119f), pose);
                    Require(CreatureGeometry.TryMeasure(instance.transform, pose, out Bounds first), data.commonName + " has no measurable mesh.");
                    Vector3 oldPosition = instance.transform.position;
                    Quaternion oldRotation = instance.transform.rotation;
                    Require(first.size.sqrMagnitude > 0.0001f, data.commonName + " has empty geometry.");
                    if (data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower)
                    {
                        Require(CreatureGeometry.TryMeasure(instance.transform, pose, out Bounds supportBounds, out Vector3[] supports) &&
                            supports != null && supports.Length > 0, data.commonName + " has no measured ground contacts.");
                        foreach (Vector3 support in supports)
                            Require(support.y >= supportBounds.min.y - 0.001f && support.y <= supportBounds.max.y + 0.001f,
                                data.commonName + " has ground contacts outside its measured geometry.");
                        checks++;
                    }
                    Require(instance.transform.position == oldPosition && instance.transform.rotation == oldRotation, "Measurement moved a live transform.");
                    instance.transform.SetPositionAndRotation(new Vector3(-105f, -32f, 134f), Quaternion.Euler(0f, 123f, 0f) * pose);
                    Require(CreatureGeometry.TryMeasure(instance.transform, pose, out Bounds second), "Second measurement failed.");
                    Require(Vector3.Distance(first.min, second.min) < 0.01f && Vector3.Distance(first.max, second.max) < 0.01f,
                        data.commonName + " measurement depends on world placement.");
                    checks += 5;
                    if (data.isStationary)
                    {
                        Vector3 beforeGrounding = instance.transform.position;
                        ground.Invoke(spawner, new object[] { instance, data });
                        var contact = instance.GetComponent<CreatureGroundContact>();
                        if (contact != null)
                        {
                            Require(contact.ContactPoint != null && contact.ContactPoint.IsChildOf(instance.transform), "Missing authored ground contact.");
                            Require(Mathf.Abs(contact.ContactPoint.position.y - (-200f - contact.EmbedDepth)) < 0.002f, "Authored base is not embedded in the fallback seabed.");
                            Require(Mathf.Abs(beforeGrounding.x - instance.transform.position.x) < 0.001f && Mathf.Abs(beforeGrounding.z - instance.transform.position.z) < 0.001f, "Anchored placement shifted the spawn footprint.");
                            checks += 3;
                        }
                        else
                        {
                            Require(CreatureGeometry.TryMeasure(instance.transform, instance.transform.rotation, out Bounds placed), "Contact measurement failed.");
                            float lowest = instance.transform.position.y + placed.min.y;
                            // No terrain reference in this isolated preview: placement's fallback floor is -200.
                            Require(lowest >= -200.4f && lowest <= -199.9f, data.commonName + " floats or is buried: bottom=" + lowest);
                            checks += 2;
                        }
                    }
                    Debug.Log($"[SpeciesMovementChecks] {data.commonName}: size={first.size}, bottom={first.min.y:F3}, stationary={data.isStationary}.");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Debug.Log($"[SpeciesMovementChecks] PASS: {checks} checks across {models} imported models (Sunlight benthics, Twilight and Midnight). No assets saved.");
    }

    public static void RunBatch()
    {
        try { Validate(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
