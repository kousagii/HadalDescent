using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Projects a cached underside footprint onto seabed samples without per-frame skin baking.</summary>
public static class BenthicClearance
{
    /// <summary>Actual feet/underside samples, rather than imaginary flat bounding-box corners.</summary>
    public static Vector3[] BuildSupportPoints(IReadOnlyList<Vector3> vertices, Bounds bounds)
    {
        const int grid = 4;
        var lowest = new Vector3[grid * grid];
        var occupied = new bool[lowest.Length];
        foreach (Vector3 vertex in vertices)
        {
            int x = (int)Mathf.Clamp((vertex.x - bounds.min.x) / Mathf.Max(0.001f, bounds.size.x) * grid, 0, grid - 1);
            int z = (int)Mathf.Clamp((vertex.z - bounds.min.z) / Mathf.Max(0.001f, bounds.size.z) * grid, 0, grid - 1);
            int index = z * grid + x;
            if (!occupied[index] || vertex.y < lowest[index].y)
            {
                occupied[index] = true;
                lowest[index] = vertex;
            }
        }
        var result = new List<Vector3>();
        for (int i = 0; i < lowest.Length; i++)
            if (occupied[i]) result.Add(lowest[i]);
        return result.ToArray();
    }

    public static float MinimumRootY(Vector3 position, Quaternion travelRotation, Bounds bounds,
        float clearance, Func<Vector3, float> sampleFloor, Vector3[] supportPoints = null)
    {
        float required = float.NegativeInfinity;
        bool authored = supportPoints != null && supportPoints.Length > 0;
        for (int i = 0; i < (authored ? supportPoints.Length : 5); i++)
        {
            Vector3 local = authored ? supportPoints[i] : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (!authored)
            {
                if (i == 1) local.x = bounds.min.x;
                else if (i == 2) local.x = bounds.max.x;
                else if (i == 3) local.z = bounds.max.z;
                else if (i == 4) local.z = bounds.min.z;
            }
            Vector3 offset = travelRotation * local;
            required = Mathf.Max(required, sampleFloor(position + offset) + Mathf.Max(0f, clearance) - offset.y);
        }
        return required;
    }
}
