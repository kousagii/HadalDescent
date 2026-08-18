using System.Collections;
using UnityEngine;

/// <summary>
/// Manages both scanning (mobile species) and interact (stationary species) paths.
///
/// SCAN path (mobile species):
///   SphereCast from camera center forward. When a SpeciesAI enters the focus reticle:
///     ? SpeciesHighlighter glows on the species
///     ? ScanReticleUI animates to Locked state
///     ? UIManager.ShowScanButton(true) — scan button glows
///   Player presses SCAN ? MinigameManager.TriggerScanMinigame()
///   Win ? BestiaryDiscoveryPopup shown. Fail ? SpeciesAI.Flee().
///
/// INTERACT path (stationary species):
///   OverlapSphere from submarine position. When a ScanTarget (isStationary) is close:
///     ? UIManager.ShowInteractButton(true) — interact button glows
///   Player presses INTERACT ? SonarPulseVFX ? FactCardUI shown.
///
/// Setup:
///   Add to the Submarine root GameObject (same as PlayerMovement).
///   Assign scanCamera (camera child of submarine).
///   MinigameManager + FactCardUI + BestiaryDiscoveryPopup are auto-found.
/// </summary>
public class ScannerSystem : MonoBehaviour
{
    public static ScannerSystem Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Scan (Mobile)")]
    [Tooltip("SphereCast radius from camera centre (how wide the focus reticle detects).")]
    [SerializeField] private float reticleRadius = 2.5f;
    [Tooltip("Base forward range of the scan SphereCast.")]
    [SerializeField] private float baseRange     = 18f;

    [Header("Interact (Stationary)")]
    [Tooltip("OverlapSphere radius around the submarine for stationary interact detection.")]
    [SerializeField] private float interactRange = 7f;

    [Header("References")]
    [SerializeField] private Camera scanCamera;

    // -----------------------------------------------------------------------
    // Targets
    // -----------------------------------------------------------------------

    private SpeciesAI  _scanTarget;
    private ScanTarget _interactTarget;
    private GameObject _highlightedObject;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (scanCamera == null) scanCamera = Camera.main;
    }

    private void Update()
    {
        // Don't detect while minigame is running
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) return;

        CheckMobileTarget();
        CheckStationaryTarget();
    }

    // -----------------------------------------------------------------------
    // Detection — Mobile (SphereCast from camera centre)
    // -----------------------------------------------------------------------

    private void CheckMobileTarget()
    {
        if (scanCamera == null) return;

        float range = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);

        SpeciesAI foundAI = null;
        if (Physics.SphereCast(scanCamera.transform.position,
                               reticleRadius,
                               scanCamera.transform.forward,
                               out RaycastHit hit, range))
        {
            var ai = hit.collider.GetComponent<SpeciesAI>();
            if (ai != null && !ai.IsDiscovered) foundAI = ai;
        }

        if (foundAI != _scanTarget)
        {
            // Clear old highlight
            if (_highlightedObject != null)
            {
                SpeciesHighlighter.SetHighlight(_highlightedObject, false);
                _highlightedObject = null;
            }

            _scanTarget = foundAI;

            if (_scanTarget != null)
            {
                SpeciesHighlighter.SetHighlight(_scanTarget.gameObject, true);
                _highlightedObject = _scanTarget.gameObject;
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Locked);
                UIManager.Instance?.ShowScanButton(true);
            }
            else
            {
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
                UIManager.Instance?.ShowScanButton(false);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Detection — Stationary (OverlapSphere from submarine)
    // -----------------------------------------------------------------------

    private void CheckStationaryTarget()
    {
        ScanTarget foundStatic = null;
        float      bestDist    = float.MaxValue;

        var cols = Physics.OverlapSphere(transform.position, interactRange);
        foreach (var col in cols)
        {
            var st = col.GetComponent<ScanTarget>();
            if (st == null || st.IsDiscovered) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = bestDist; foundStatic = st; }
        }

        if (foundStatic != _interactTarget)
        {
            _interactTarget = foundStatic;
            UIManager.Instance?.ShowInteractButton(_interactTarget != null);
        }
    }

    // -----------------------------------------------------------------------
    // Public — called by UIManager buttons
    // -----------------------------------------------------------------------

    /// <summary>Called by UIManager.OnScanButtonPressed().</summary>
    public void TryScan()
    {
        if (_scanTarget == null || _scanTarget.IsDiscovered)
        {
            Debug.Log("[ScannerSystem] Scan pressed — no mobile target in focus.");
            return;
        }

        if (MinigameManager.Instance == null)
        {
            Debug.LogWarning("[ScannerSystem] MinigameManager not in scene.");
            return;
        }

        MinigameManager.Instance.TriggerScanMinigame(
            _scanTarget.Data,
            OnScanSuccess,
            OnScanFailed);
    }

    /// <summary>Called by UIManager.OnInteractButtonPressed().</summary>
    public void TryInteract()
    {
        if (_interactTarget == null)
        {
            Debug.Log("[ScannerSystem] Interact pressed — no stationary target in range.");
            return;
        }
        StartCoroutine(StaticScanSequence(_interactTarget));
    }

    // -----------------------------------------------------------------------
    // Scan outcomes (mobile)
    // -----------------------------------------------------------------------

    private void OnScanSuccess()
    {
        if (_scanTarget == null) return;
        SpeciesData data = _scanTarget.Data;

        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(ZoneManager.CurrentZoneIndex, data.speciesId)
            : false;

        if (isNew) GameManager.Instance?.AddRDP(data.rdpReward);

        _scanTarget.IsDiscovered = true;

        // Clear highlight
        if (_highlightedObject != null)
        {
            SpeciesHighlighter.SetHighlight(_highlightedObject, false);
            _highlightedObject = null;
        }

        ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
        UIManager.Instance?.ShowScanButton(false);

        // Show discovery popup
        BestiaryDiscoveryPopup.Instance?.Show(data, isNew);

        _scanTarget = null;
        Debug.Log($"[ScannerSystem] Scan success: {data.commonName} (new={isNew})");
    }

    public void OnScanFailed()
    {
        if (_scanTarget != null)
        {
            _scanTarget.Flee(transform.position);
            SpeciesHighlighter.SetHighlight(_scanTarget.gameObject, false);
        }

        _scanTarget        = null;
        _highlightedObject = null;
        ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
        UIManager.Instance?.ShowScanButton(false);
        Debug.Log("[ScannerSystem] Scan failed — species fleeing.");
    }

    // -----------------------------------------------------------------------
    // Stationary interact sequence
    // -----------------------------------------------------------------------

    private IEnumerator StaticScanSequence(ScanTarget target)
    {
        if (target == null) yield break;

        // Disable interact button during sequence
        UIManager.Instance?.ShowInteractButton(false);

        // 1. Sonar pulse VFX
        SonarPulseVFX.PlayAt(target.transform.position, radius: 2.5f, duration: 0.9f);
        yield return new WaitForSeconds(0.9f);

        // 2. Record in bestiary
        SpeciesData data = target.Data;
        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(ZoneManager.CurrentZoneIndex, data.speciesId)
            : false;

        if (isNew) GameManager.Instance?.AddRDP(data.rdpReward);
        target.IsDiscovered = true;
        _interactTarget = null;

        // 3. Show fact card
        FactCardUI.Instance?.Show(data, isNew);

        Debug.Log($"[ScannerSystem] Interact: {data.commonName} (new={isNew})");
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        // Interact range
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        // Scan range (approximate — from camera position, forward)
        if (scanCamera != null)
        {
            float range = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            Gizmos.DrawRay(scanCamera.transform.position,
                           scanCamera.transform.forward * range);
        }
    }
}
