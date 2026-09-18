using UnityEngine;

/// <summary>
/// World-space component attached to 3D marine debris clusters scattered on the ocean floor.
/// 
/// Features:
///   - Auto-registers with SonarMapUI via SonarTrackable (TargetType = Debris, Amber radar blip).
///   - Contains a randomized distribution of waste (Plastics, Metals, Hazardous).
///   - Provides a generous trigger area for player submarine interaction.
///   - Destroys / despawns itself when cleaned via EnvironmentalCleanupMinigame.
/// </summary>
public class DebrisCluster : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Debris Content
    // -----------------------------------------------------------------------

    [Header("Cluster Content")]
    [SerializeField] private string clusterName = "Marine Debris Cluster";
    [Range(1, 4)] [SerializeField] private int plasticCount = 2;
    [Range(1, 4)] [SerializeField] private int metalCount   = 2;
    [Range(1, 3)] [SerializeField] private int hazardCount  = 1;
    [SerializeField] private int baseRdpReward = 40;

    [Header("Interaction Radius")]
    [SerializeField] private float interactRadius = 10f;

    private SonarTrackable _trackable;
    private bool           _isCleaned = false;

    // -----------------------------------------------------------------------
    // Public Properties
    // -----------------------------------------------------------------------

    public string ClusterName => clusterName;
    public int PlasticCount   => plasticCount;
    public int MetalCount     => metalCount;
    public int HazardCount    => hazardCount;
    public int TotalItems     => plasticCount + metalCount + hazardCount;
    public int BaseRdpReward  => baseRdpReward;
    public bool IsCleaned     => _isCleaned;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        EnsureCollider();
        EnsureSonarTrackable();
    }

    private void Start()
    {
        EnsureCollider();
        EnsureSonarTrackable();
    }

    private void OnEnable()
    {
        EnsureSonarTrackable();
    }

    // -----------------------------------------------------------------------
    // Setup
    // -----------------------------------------------------------------------

    public void EnsureCollider()
    {
        // 1. Ensure all child colliders are triggers so they don't block physical submarine movement
        var childCols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null)
                childCols[i].isTrigger = true;
        }

        // 2. Remove any oversized SphereCollider from older versions
        var sc = GetComponent<SphereCollider>();
        if (sc != null) Destroy(sc);

        // 3. Compute compound bounds from child renderers to form an exact bounding box
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    worldBounds.Encapsulate(renderers[i].bounds);
            }

            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // Transform world bounds into local box collider center and size
            box.center = transform.InverseTransformPoint(worldBounds.center);
            Vector3 localExtents = transform.InverseTransformVector(worldBounds.extents);
            box.size = new Vector3(Mathf.Max(0.5f, Mathf.Abs(localExtents.x) * 2f),
                                   Mathf.Max(0.5f, Mathf.Abs(localExtents.y) * 2f),
                                   Mathf.Max(0.5f, Mathf.Abs(localExtents.z) * 2f));
        }
        else
        {
            // Fallback if no renderers found yet
            var box = GetComponent<BoxCollider>() ?? gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 2f;
        }
    }

    public void EnsureSonarTrackable()
    {
        if (_trackable == null)
            _trackable = GetComponent<SonarTrackable>();

        if (_trackable == null)
            _trackable = gameObject.AddComponent<SonarTrackable>();

        _trackable.Initialize(SonarTrackable.SonarTargetType.Debris, clusterName, false);
    }

    // -----------------------------------------------------------------------
    // Cleanup Outcome
    // -----------------------------------------------------------------------

    public void CleanUp()
    {
        _isCleaned = true;
        if (_trackable != null)
            Destroy(_trackable);

        Debug.Log($"[DebrisCluster] '{clusterName}' successfully cleaned up.");
        Destroy(gameObject, 0.1f);
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(0f, 0.95f, 0.90f, 0.75f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        Gizmos.color = new Color(1f, 0.72f, 0.15f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}