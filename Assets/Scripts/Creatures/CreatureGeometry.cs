using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawn-time visual measurements independent of cached world renderer bounds.</summary>
public static class CreatureGeometry
{
    // Returns world-sized geometry in the requested rotation, with the root at
    // the origin. No temporary transform writes or physics synchronization needed.
    public static bool TryMeasure(Transform root, Quaternion rotation, out Bounds bounds)
    {
        return TryMeasure(root, rotation, out bounds, out _, false);
    }

    /// <summary>Also caches the lowest real geometry in each footprint cell for ground contact.</summary>
    public static bool TryMeasure(Transform root, Quaternion rotation, out Bounds bounds,
        out Vector3[] supportPoints, bool measureSupport = true)
    {
        bounds = new Bounds();
        supportPoints = null;
        bool measured = false;
        var vertices = new List<Vector3>();
        var contactVertices = measureSupport ? new List<Vector3>() : null;
        Mesh baked = null;
        Matrix4x4 toPose = Matrix4x4.Rotate(rotation * Quaternion.Inverse(root.rotation))
            * Matrix4x4.Translate(-root.position);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Matrix4x4 matrix = toPose * renderer.localToWorldMatrix;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    // Imported skinned bounds may contain the entire animation
                    // envelope. Ground against the actual current pose instead.
                    if (baked == null) baked = new Mesh { name = "CreatureContactMeasurement" };
                    baked.Clear();
                    skin.BakeMesh(baked, false);
                    baked.GetVertices(vertices);
                    foreach (var vertex in vertices)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(vertex);
                        Include(point, ref bounds, ref measured);
                        contactVertices?.Add(point);
                    }
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                {
                    if (filter.sharedMesh.isReadable)
                    {
                        filter.sharedMesh.GetVertices(vertices);
                        foreach (var vertex in vertices)
                        {
                            Vector3 point = matrix.MultiplyPoint3x4(vertex);
                            Include(point, ref bounds, ref measured);
                            contactVertices?.Add(point);
                        }
                    }
                    else IncludeBounds(filter.sharedMesh.bounds, matrix, ref bounds, ref measured, contactVertices);
                }
                // Particles, trails and light effects are not contact geometry.
            }
        }
        finally
        {
            if (baked != null)
            {
                if (Application.isPlaying) Object.Destroy(baked);
                else Object.DestroyImmediate(baked);
            }
        }
        if (measured && measureSupport)
            supportPoints = BenthicClearance.BuildSupportPoints(contactVertices, bounds);
        return measured;
    }

    private static void IncludeBounds(Bounds local, Matrix4x4 matrix, ref Bounds bounds, ref bool measured, List<Vector3> contactVertices)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y,
                (i & 4) == 0 ? local.min.z : local.max.z);
            Vector3 point = matrix.MultiplyPoint3x4(corner);
            Include(point, ref bounds, ref measured);
            contactVertices?.Add(point);
        }
    }

    private static void Include(Vector3 point, ref Bounds bounds, ref bool measured)
    {
        if (!measured) { bounds = new Bounds(point, Vector3.zero); measured = true; }
        else bounds.Encapsulate(point);
    }
}
