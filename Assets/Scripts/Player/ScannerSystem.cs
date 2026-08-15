using UnityEngine;

/// <summary>
/// Attaches to the Submarine root. Detects nearby species (both mobile SpeciesAI
/// and stationary ScanTarget) using Physics.OverlapSphere from the camera position.
///
/// Flow:
///   1. Every Update: OverlapSphere finds the nearest undiscovered species in range.
///   2. If found ? UIManager.ShowScanPrompt(true).
///   3. Player presses SCAN button ? UIManager.OnScanButtonPressed() ? TryScan().
///   4. Phase 3 stub: scan succeeds immediately (Phase 4 adds mini-game here).
///   5. On success ? GameManager.DiscoverSpecies + AddRDP, species marked discovered.
///   6. On failure ? SpeciesAI.Flee() if mobile.
///
/// Setup:
///   Add ScannerSystem to the same GameObject as PlayerMovement.
///   Assign scanCamera (usually Camera.main / the sub's camera child).
///   Wire UIManager.OnScanButtonPressed() to call scannerSystem.TryScan().
/// </summary>
public class ScannerSystem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Detection")]
    [UnityEngine.Tooltip("Base scan range in Unity units. Increases with ScannerTier.")]
    [UnityEngine.SerializeField] private float baseRange  = 15f;
    [UnityEngine.Tooltip("Overlap sphere radius for initial detection (not directional).")]
    [UnityEngine.SerializeField] private float scanRadius = 5f;

    [Header("References")]
    [UnityEngine.SerializeField] private UnityEngine.Camera scanCamera;

    // -----------------------------------------------------------------------
    // Singleton so UIManager can call TryScan() without FindObjectOfType
    // -----------------------------------------------------------------------

    public static ScannerSystem Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Current scan target
    // -----------------------------------------------------------------------

    private SpeciesAI  _targetAI;
    private ScanTarget _targetStatic;
    private SpeciesData _targetData;
    private bool        _promptShowing;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { UnityEngine.Object.Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (scanCamera == null) scanCamera = UnityEngine.Camera.main;
    }

    private void Update()
    {
        DetectNearestTarget();
    }

    // -----------------------------------------------------------------------
    // Detection
    // -----------------------------------------------------------------------

    private void DetectNearestTarget()
    {
        float tier      = GameManager.Instance != null ? GameManager.Instance.ScannerTier : 1;
        float range     = baseRange + tier * 8f;
        float radius    = scanRadius + tier * 1.5f;
        UnityEngine.Vector3 origin = scanCamera != null ? scanCamera.transform.position : transform.position;

        UnityEngine.Collider[] hits = UnityEngine.Physics.OverlapSphere(origin, range);

        SpeciesAI   bestAI     = null;
        ScanTarget  bestStatic = null;
        float       bestDist   = float.MaxValue;

        foreach (var col in hits)
        {
            float dist = UnityEngine.Vector3.Distance(origin, col.transform.position);
            if (dist >= bestDist) continue;

            var ai = col.GetComponent<SpeciesAI>();
            if (ai != null && !ai.IsDiscovered) { bestAI = ai; bestStatic = null; bestDist = dist; continue; }

            var st = col.GetComponent<ScanTarget>();
            if (st != null && !st.IsDiscovered) { bestStatic = st; bestAI = null; bestDist = dist; }
        }

        bool hasTarget = bestAI != null || bestStatic != null;

        if (hasTarget)
        {
            _targetAI     = bestAI;
            _targetStatic = bestStatic;
            _targetData   = bestAI != null ? bestAI.Data : bestStatic?.Data;
        }
        else
        {
            _targetAI     = null;
            _targetStatic = null;
            _targetData   = null;
        }

        if (hasTarget != _promptShowing)
        {
            _promptShowing = hasTarget;
            UIManager.Instance?.ShowScanPrompt(_promptShowing);
        }
    }

    // -----------------------------------------------------------------------
    // Public — called by UIManager.OnScanButtonPressed()
    // -----------------------------------------------------------------------

    public void TryScan()
    {
        if (_targetData == null)
        {
            UnityEngine.Debug.Log("[ScannerSystem] Scan pressed but no target in range.");
            return;
        }
        // Phase 3: Immediate success stub.
        // Phase 4: Replace this call with MinigameManager.TriggerMinigame(OnScanSuccess, OnScanFailed).
        OnScanSuccess();
    }

    // -----------------------------------------------------------------------
    // Scan outcomes
    // -----------------------------------------------------------------------

    private void OnScanSuccess()
    {
        if (_targetData == null) return;

        int zone  = ZoneManager.CurrentZoneIndex;
        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(zone, _targetData.speciesId)
            : false;

        if (isNew)
        {
            GameManager.Instance?.AddRDP(_targetData.rdpReward);
            UnityEngine.Debug.Log($"[ScannerSystem] ? New: {_targetData.commonName} (+{_targetData.rdpReward} RDP)");
        }
        else
        {
            UnityEngine.Debug.Log($"[ScannerSystem] Already discovered: {_targetData.commonName}");
        }

        if (_targetAI != null)     _targetAI.IsDiscovered = true;
        if (_targetStatic != null) _targetStatic.IsDiscovered = true;

        ClearTarget();
    }

    public void OnScanFailed()
    {
        if (_targetAI != null)
            _targetAI.Flee(transform.position);

        ClearTarget();
    }

    private void ClearTarget()
    {
        _targetData   = null;
        _targetAI     = null;
        _targetStatic = null;
        _promptShowing = false;
        UIManager.Instance?.ShowScanPrompt(false);
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        float range = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);
        UnityEngine.Gizmos.color = new UnityEngine.Color(0f, 1f, 0.5f, 0.2f);
        UnityEngine.Vector3 origin = scanCamera != null ? scanCamera.transform.position : transform.position;
        UnityEngine.Gizmos.DrawWireSphere(origin, range);
    }
}
