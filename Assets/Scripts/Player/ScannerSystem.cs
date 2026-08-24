using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages both scanning (mobile species) and interact (stationary species & debris clusters) paths.
///
/// SCAN path (mobile species):
///   SphereCast from camera center forward. When a SpeciesAI enters the focus reticle:
///     - SpeciesHighlighter glows on the species
///     - ScanReticleUI animates to Locked state
///     - UIManager.ShowScanButton(true) - scan button glows
///   Player presses SCAN -> MinigameManager.TriggerScanMinigame()
///   Win -> BestiaryDiscoveryPopup shown. Fail -> SpeciesAI.Flee().
///
/// INTERACT path (stationary species & debris):
///   Dual detection:
///     1. Camera forward SphereCast (up to 30m) ignoring the player's own body.
///     2. Direct distance scan against all active DebrisCluster and ScanTarget components (up to 25m).
///   When target in focus/range:
///     - UIManager.ShowInteractButton(true) - interact button glows
///   Player presses INTERACT:
///     - If DebrisCluster -> MinigameManager.TriggerDebrisCleanupMinigame()
///     - If ScanTarget -> SonarPulseVFX -> FactCardUI shown.
/// </summary>
public class ScannerSystem : MonoBehaviour
{
    private static ScannerSystem _instance;
    public static ScannerSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ScannerSystem>();
                if (_instance == null)
                {
                    var player = FindFirstObjectByType<PlayerMovement>();
                    if (player != null)
                    {
                        _instance = player.gameObject.AddComponent<ScannerSystem>();
                    }
                }
            }
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Scan (Mobile)")]
    [Tooltip("SphereCast radius from camera centre (how wide the focus reticle detects).")]
    [SerializeField] private float reticleRadius = 2.5f;
    [Tooltip("Base forward range of the scan SphereCast.")]
    [SerializeField] private float baseRange     = 25f;

    [Header("Interact (Stationary & Debris)")]
    [Tooltip("Detection radius around the submarine for stationary and debris interact detection.")]
    [SerializeField] private float interactRange = 12f;

    [Header("References")]
    [SerializeField] private Camera scanCamera;

    // -----------------------------------------------------------------------
    // Targets
    // -----------------------------------------------------------------------

    private SpeciesAI     _scanTarget;
    private ScanTarget    _interactTarget;
    private DebrisCluster _debrisTarget;
    private GameObject    _highlightedObject;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;
    }

    private void Start()
    {
        FindScanCamera();
    }

    private void FindScanCamera()
    {
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
            if (scanCamera == null)
            {
                var subCam = FindFirstObjectByType<SubmarineCamera>();
                if (subCam != null) scanCamera = subCam.GetComponent<Camera>();
                
                if (scanCamera == null) scanCamera = FindFirstObjectByType<Camera>();
            }
        }
    }

    private void Update()
    {
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) return;

        CheckMobileTarget();
        CheckStationaryTarget();
    }

    // -----------------------------------------------------------------------
    // Detection - Mobile (SphereCast from camera centre, ignoring player body)
    // -----------------------------------------------------------------------

    private void CheckMobileTarget()
    {
        FindScanCamera();
        if (scanCamera == null) return;

        float range = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);

        SpeciesAI foundAI = null;
        float bestDist = float.MaxValue;
        
        // SphereCast from camera center forward to detect mobile species within reticle.
        var hits = Physics.SphereCastAll(scanCamera.transform.position, reticleRadius, scanCamera.transform.forward, range, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            var ai = hit.collider.GetComponentInParent<SpeciesAI>();
            if (ai != null && !ai.IsDiscovered && ai.gameObject.activeInHierarchy)
            {
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    foundAI = ai;
                }
            }
        }

        if (foundAI != _scanTarget)
        {
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
    // Detection - Stationary & Debris (Direct distance scan + Camera Aiming)
    // -----------------------------------------------------------------------

    private void CheckStationaryTarget()
    {
        ScanTarget    foundStatic = null;
        DebrisCluster foundDebris = null;
        float         bestDistStatic = float.MaxValue;
        float         bestDistDebris = float.MaxValue;

        FindScanCamera();
        GameObject raycastTargetObj = null;

        if (scanCamera != null)
        {
            // Use reticleRadius and interactRange to strictly require aiming at the object to interact
            var hits = Physics.SphereCastAll(scanCamera.transform.position, reticleRadius, scanCamera.transform.forward, interactRange, ~0, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                var dc = hit.collider.GetComponentInParent<DebrisCluster>();
                if (dc != null && !dc.IsCleaned && dc.gameObject.activeInHierarchy)
                {
                    if (hit.distance < bestDistDebris)
                    {
                        foundDebris = dc;
                        bestDistDebris = hit.distance;
                        raycastTargetObj = dc.gameObject; // Restore cyan glow for debris
                    }
                }

                var st = hit.collider.GetComponentInParent<ScanTarget>();
                if (st != null && !st.IsDiscovered && st.gameObject.activeInHierarchy)
                {
                    if (hit.distance < bestDistStatic)
                    {
                        foundStatic = st;
                        bestDistStatic = hit.distance;
                        raycastTargetObj = st.gameObject; // Static species DO glow cyan
                    }
                }
            }
        }

        // Handle highlighting for static targets if we don't have a mobile target
        if (_scanTarget == null)
        {
            if (raycastTargetObj != null && _highlightedObject != raycastTargetObj)
            {
                if (_highlightedObject != null) SpeciesHighlighter.SetHighlight(_highlightedObject, false);
                SpeciesHighlighter.SetHighlight(raycastTargetObj, true);
                _highlightedObject = raycastTargetObj;
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Locked);
            }
            else if (raycastTargetObj == null && _highlightedObject != null)
            {
                SpeciesHighlighter.SetHighlight(_highlightedObject, false);
                _highlightedObject = null;
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
            }
        }

        _interactTarget = foundStatic;
        _debrisTarget   = foundDebris;

        bool hasInteractable = (_interactTarget != null) || (_debrisTarget != null);
        UIManager.Instance?.ShowInteractButton(hasInteractable);
    }

    // -----------------------------------------------------------------------
    // Public - called by UIManager buttons or keyboard
    // -----------------------------------------------------------------------

    public void TryScan()
    {
        if (_scanTarget == null || _scanTarget.IsDiscovered)
        {
            Debug.Log("[ScannerSystem] Scan pressed - no mobile target in focus.");
            return;
        }

        MinigameManager.Instance.TriggerScanMinigame(
            _scanTarget.Data,
            OnScanSuccess,
            OnScanFailed);
    }

    public void TryInteract()
    {
        // 1. Direct target already cached
        if (_debrisTarget != null)
        {
            Debug.Log($"[ScannerSystem] Launching Cleanup Minigame for '{_debrisTarget.ClusterName}'!");
            MinigameManager.Instance.TriggerDebrisCleanupMinigame(_debrisTarget);
            return;
        }

        if (_interactTarget != null)
        {
            Debug.Log($"[ScannerSystem] Interacting with stationary species '{_interactTarget.Data.commonName}'!");
            StartCoroutine(StaticScanSequence(_interactTarget));
            return;
        }

        // 2. Emergency Instant Search: find closest DebrisCluster within 15m
        var clusters = FindObjectsByType<DebrisCluster>(FindObjectsSortMode.None);
        DebrisCluster closest = null;
        float closestDist = float.MaxValue;
        foreach (var c in clusters)
        {
            if (c == null || c.IsCleaned || !c.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < closestDist && d <= 15f)
            {
                closestDist = d;
                closest = c;
            }
        }

        if (closest != null)
        {
            Debug.Log($"[ScannerSystem] (Emergency fallback) Found nearby debris '{closest.ClusterName}' at {closestDist:0.0}m - Launching!");
            _debrisTarget = closest;
            MinigameManager.Instance.TriggerDebrisCleanupMinigame(closest);
            return;
        }

        Debug.Log("[ScannerSystem] Interact pressed - no target in range.");
    }

    // -----------------------------------------------------------------------
    // Scan outcomes (mobile)
    // -----------------------------------------------------------------------

    private void OnScanSuccess()
    {
        if (_scanTarget == null) return;
        SpeciesData data = _scanTarget.Data;

        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(data.zoneIndex, data.speciesId)
            : false;

        if (isNew) GameManager.Instance?.AddRDP(data.rdpReward);

        _scanTarget.IsDiscovered = true;
        _scanTarget.GetComponent<SonarTrackable>()?.SetDiscovered(true);

        if (_highlightedObject != null)
        {
            SpeciesHighlighter.SetHighlight(_highlightedObject, false);
            _highlightedObject = null;
        }

        ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
        UIManager.Instance?.ShowScanButton(false);

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
        Debug.Log("[ScannerSystem] Scan failed - species fleeing.");
    }

    // -----------------------------------------------------------------------
    // Stationary interact sequence
    // -----------------------------------------------------------------------

    private IEnumerator StaticScanSequence(ScanTarget target)
    {
        if (target == null) yield break;

        UIManager.Instance?.ShowInteractButton(false);

        SonarPulseVFX.PlayAt(target.transform.position, radius: 2.5f, duration: 0.9f);
        yield return new WaitForSeconds(0.9f);

        SpeciesData data = target.Data;
        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(data.zoneIndex, data.speciesId)
            : false;

        if (isNew) GameManager.Instance?.AddRDP(data.rdpReward);
        target.IsDiscovered = true;
        target.GetComponent<SonarTrackable>()?.SetDiscovered(true);
        _interactTarget = null;

        FactCardUI.Instance?.Show(data, isNew);

        Debug.Log($"[ScannerSystem] Interact: {data.commonName} (new={isNew})");
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (scanCamera != null)
        {
            float range = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            Gizmos.DrawRay(scanCamera.transform.position,
                           scanCamera.transform.forward * range);
        }
    }
}
