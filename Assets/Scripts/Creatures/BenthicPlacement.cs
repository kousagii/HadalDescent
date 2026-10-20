using UnityEngine;

/// <summary>Final visual-footprint queries, independent of creature collision-layer filtering.</summary>
public static class BenthicPlacement
{
    public static bool IsBlocked(Vector3 position, Quaternion rotation, Bounds modelBounds,
        Transform owner, Collider[] buffer, out Vector3 away)
    {
        Vector3 center = position + rotation * modelBounds.center;
        Vector3 extents = modelBounds.extents + Vector3.one * 0.04f;
        int count = Physics.OverlapBoxNonAlloc(center, extents, buffer, rotation, ~0, QueryTriggerInteraction.Ignore);
        away = Vector3.zero;
        bool blocked = count == buffer.Length; // Never treat a truncated query as safe.
        for (int i = 0; i < count; i++)
        {
            var col = buffer[i];
            if (col == null || col.transform.IsChildOf(owner)) continue;
            var target = col.GetComponentInParent<ScanTarget>();
            bool stationarySpecies = target != null && target.Data != null && target.Data.isStationary;
            if (!stationarySpecies && !ContextSteering.IsEnvironmentProp(col) &&
                col.GetComponentInParent<EnvironmentFactTarget>() == null) continue;
            blocked = true;
            Vector3 push = center - col.bounds.center;
            push.y = 0f;
            if (push.sqrMagnitude > 0.0001f) away += push.normalized;
        }
        return blocked;
    }

    public static float SampleFloor(TerrainGenerator terrain, Vector3 point, float fallbackY)
    {
        var generator = terrain != null ? terrain.GetComponent<OceanFloorMeshGenerator>() : null;
        var floor = generator != null ? generator.GetComponent<MeshCollider>() : null;
        if (floor != null && floor.enabled && !floor.isTrigger && floor.sharedMesh != null)
        {
            Bounds bounds = floor.bounds;
            var ray = new Ray(new Vector3(point.x, bounds.max.y + 1f, point.z), Vector3.down);
            if (floor.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f) && hit.normal.y > 0f)
                return hit.point.y;
        }
        return terrain != null ? terrain.SampleHeight(point.x, point.z) : fallbackY;
    }
}
