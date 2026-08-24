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
        var sc = GetComponent<SphereCollider>();
        if (sc == null)
        {
            var box = GetComponent<BoxCollider>();
            if (box != null) Destroy(box);

            sc = gameObject.AddComponent<SphereCollider>();
        }
        sc.radius = interactRadius;
        sc.isTrigger = true;
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
        Gizmos.color = new Color(1f, 0.72f, 0.15f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}