using UnityEngine;

/// <summary>Optional authored attachment point for stationary species with uneven mesh bases.</summary>
[DisallowMultipleComponent]
public class CreatureGroundContact : MonoBehaviour
{
    [SerializeField] private Transform contactPoint;
    [Tooltip("World-space depth to embed the attachment point below the seabed, regardless of specimen scale.")]
    [SerializeField, Min(0f)] private float embedDepth = 0.05f;

    public Transform ContactPoint => contactPoint;
    public float EmbedDepth => Mathf.Max(0f, embedDepth);

    public bool TryPlace(TerrainGenerator terrain, float fallbackFloorY)
    {
        if (!enabled || contactPoint == null || !contactPoint.IsChildOf(transform)) return false;

        // Sample directly below the authored attachment point. Preserve the
        // spawn's horizontal placement and heading, including on repeated calls.
        Vector3 destination = contactPoint.position;
        destination.y = terrain != null ? terrain.SampleHeight(destination.x, destination.z) : fallbackFloorY;
        var meshGenerator = terrain != null ? terrain.GetComponent<OceanFloorMeshGenerator>() : null;
        var floor = meshGenerator != null ? meshGenerator.GetComponent<MeshCollider>() : null;
        if (floor != null && floor.enabled && !floor.isTrigger && floor.sharedMesh != null)
        {
            // Query only the generated seabed. A coral's own Box Collider, props,
            // and nearby species cannot intercept this ray or become its support.
            Bounds bounds = floor.bounds;
            var ray = new Ray(new Vector3(destination.x, bounds.max.y + 1f, destination.z), Vector3.down);
            if (floor.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f) && hit.normal.y > 0f)
                destination.y = hit.point.y;
        }

        transform.position += destination - contactPoint.position - Vector3.up * EmbedDepth;
        return true;
    }
}
