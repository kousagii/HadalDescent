using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages both scanning (mobile species) and interaction (stationary species & debris clusters).
///
/// Precise Reticle Aiming:
///   - Casts a center-view ray/beam directly from the camera viewport center (0.5, 0.5) forward up to detectionRange (100m).
///   - Validates that detected targets are in front of the camera and within the center reticle viewport bounds.
///   - Detects both mobile species (SpeciesAI), stationary species (ScanTarget), and debris (DebrisCluster).
///   - If no valid object is within the reticle, all states immediately reset to Idle (no floating text, no glowing buttons).
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
                        _instance = player.gameObject.AddComponent<ScannerSystem>();
                }
            }
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Detection Ranges")]
    [Tooltip("Effective scan range for mobile species (minigame lock-on). Upgraded by ScannerTier.")]
    [SerializeField] private float baseRange       = 25f;

    [Tooltip("Effective interact range for stationary species and debris clusters.")]
    [SerializeField] private float interactRange   = 15f;

    [Tooltip("Maximum detection range for aiming at distant species (shows 'Get closer to scan').")]
    [SerializeField] private float detectionRange  = 100f;

    [Tooltip("Narrow raycast beam radius (in meters) to match the center reticle frame on screen.")]
    [SerializeField] private float reticleBeamRadius = 0.5f;

    [Tooltip("Maximum viewport distance from screen center (0.5, 0.5) to count as inside the reticle.")]
    [SerializeField] private float viewportAimThreshold = 0.12f;

    [Header("References")]
    [SerializeField] private Camera scanCamera;

    // -----------------------------------------------------------------------
    // Active Targets
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

    public bool IsAnyPopupOpen()
    {
        if (BestiaryDiscoveryPopup.Instance != null && BestiaryDiscoveryPopup.Instance.IsOpen) return true;
        if (FactCardUI.Instance != null && FactCardUI.Instance.IsOpen) return true;
        if (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen) return true;
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) return true;
        return false;
    }

    private void Update()
    {
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            return; // Retain active target during minigame; do not wipe or raycast
        }

        if (IsAnyPopupOpen())
        {
            ClearAllDetection();
            return;
        }

        PerformReticleDetection();
    }

    // -----------------------------------------------------------------------
    // Unified Reticle-Based Detection (Mobile + Stationary + Debris)
    // -----------------------------------------------------------------------

    private void PerformReticleDetection()
    {
        if (IsAnyPopupOpen())
        {
            ClearAllDetection();
            return;
        }

        FindScanCamera();
        if (scanCamera == null)
        {
            ClearAllDetection();
            return;
        }

        float effectiveMobileRange = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);

        Ray centerRay = scanCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 rayStart = centerRay.origin + centerRay.direction * 0.6f; // Offset forward to avoid submarine hull clipping

        // Collect all colliders hit by narrow center beam
        var hits = Physics.SphereCastAll(rayStart, reticleBeamRadius, centerRay.direction, detectionRange, ~0, QueryTriggerInteraction.Collide);

        // Also do a direct line-of-sight raycast
        var directHits = Physics.RaycastAll(rayStart, centerRay.direction, detectionRange, ~0, QueryTriggerInteraction.Collide);

        // Find the best valid target strictly inside the reticle
        SpeciesAI     bestAI = null;
        ScanTarget    bestStatic = null;
        DebrisCluster bestDebris = null;
        float         bestDist = float.MaxValue;

        // Process SphereCast hits
        foreach (var hit in hits)
        {
            EvaluateHit(hit.collider, hit.point, ref bestAI, ref bestStatic, ref bestDebris, ref bestDist);
        }

        // Process Direct Raycast hits (higher precision)
        foreach (var hit in directHits)
        {
            EvaluateHit(hit.collider, hit.point, ref bestAI, ref bestStatic, ref bestDebris, ref bestDist);
        }

        // ── Apply Detection State ──

        if (bestAI != null)
        {
            // Mobile Species Target
            _debrisTarget = null;
            _interactTarget = null;

            bool isDiscovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(bestAI.Data.speciesId);

            if (isDiscovered)
            {
                // Already Cataloged
                _scanTarget = null;
                ClearHighlight();
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.AlreadyScanned, bestAI.Data.speciesId);
                UIManager.Instance?.ShowScanButton(false);
                UIManager.Instance?.ShowInteractButton(false);
            }
            else if (bestDist <= effectiveMobileRange)
            {
                // In Scan Range → Ready to Scan
                _scanTarget = bestAI;
                SetHighlight(bestAI.gameObject);
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Locked);
                UIManager.Instance?.ShowScanButton(true);
                UIManager.Instance?.ShowInteractButton(false);
            }
            else
            {
                // Too Far → "Get closer to scan"
                _scanTarget = null;
                ClearHighlight();
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.TooFar);
                UIManager.Instance?.ShowScanButton(false);
                UIManager.Instance?.ShowInteractButton(false);
            }
        }
        else if (bestStatic != null)
        {
            // Stationary Species Target (Coral, Sponge, Bivalve, etc.)
            _scanTarget = null;
            _debrisTarget = null;

            bool isDiscovered = GameManager.Instance != null && GameManager.Instance.IsDiscovered(bestStatic.Data.speciesId);

            if (isDiscovered)
            {
                // Already Cataloged
                _interactTarget = null;
                ClearHighlight();
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.AlreadyScanned, bestStatic.Data.speciesId);
                UIManager.Instance?.ShowScanButton(false);
                UIManager.Instance?.ShowInteractButton(false);
            }
            else if (bestDist <= interactRange)
            {
                // In Interact Range → Ready to Interact
                _interactTarget = bestStatic;
                SetHighlight(bestStatic.gameObject);
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Locked);
                UIManager.Instance?.ShowInteractButton(true);
                UIManager.Instance?.ShowScanButton(false);
            }
            else
            {
                // Too Far → "Get closer to scan"
                _interactTarget = null;
                ClearHighlight();
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.TooFar);
                UIManager.Instance?.ShowScanButton(false);
                UIManager.Instance?.ShowInteractButton(false);
            }
        }
        else if (bestDebris != null)
        {
            // Debris Cluster Target
            _scanTarget = null;
            _interactTarget = null;

            if (bestDist <= interactRange)
            {
                // In Range → Ready to Clean
                _debrisTarget = bestDebris;
                SetHighlight(bestDebris.gameObject);
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Locked);
                UIManager.Instance?.ShowInteractButton(true);
                UIManager.Instance?.ShowScanButton(false);
            }
            else
            {
                // Too Far
                _debrisTarget = null;
                ClearHighlight();
                ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.TooFar);
                UIManager.Instance?.ShowScanButton(false);
                UIManager.Instance?.ShowInteractButton(false);
            }
        }
        else
        {
            // Nothing in Reticle → Immediate Idle Reset
            ClearAllDetection();
        }
    }

    private void EvaluateHit(Collider col, Vector3 hitPoint, ref SpeciesAI bestAI, ref ScanTarget bestStatic, ref DebrisCluster bestDebris, ref float bestDist)
    {
        if (col == null || !col.gameObject.activeInHierarchy) return;

        // Skip player submarine colliders
        if (col.GetComponentInParent<PlayerMovement>() != null) return;

        // Screen-space confirmation: ensure target collider is actually in front of the camera and within the center reticle
        Vector3 targetPos = col.bounds.center;
        Vector3 vp = scanCamera.WorldToViewportPoint(targetPos);
        if (vp.z <= 0.2f) return; // Behind camera or too close to lens

        float vpDistFromCenter = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
        if (vpDistFromCenter > viewportAimThreshold) return; // Outside reticle viewport threshold

        float dist = Vector3.Distance(scanCamera.transform.position, targetPos);
        if (dist > detectionRange) return;

        // 1. Mobile Species
        var ai = col.GetComponentInParent<SpeciesAI>();
        if (ai != null && ai.gameObject.activeInHierarchy && ai.Data != null)
        {
            if (dist < bestDist)
            {
                bestDist = dist;
                bestAI = ai;
                bestStatic = null;
                bestDebris = null;
            }
            return;
        }

        // 2. Stationary Species
        var st = col.GetComponentInParent<ScanTarget>();
        if (st != null && st.gameObject.activeInHierarchy && st.Data != null)
        {
            if (dist < bestDist)
            {
                bestDist = dist;
                bestStatic = st;
                bestAI = null;
                bestDebris = null;
            }
            return;
        }

        // 3. Debris Cluster
        var dc = col.GetComponentInParent<DebrisCluster>();
        if (dc != null && dc.gameObject.activeInHierarchy && !dc.IsCleaned)
        {
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDebris = dc;
                bestAI = null;
                bestStatic = null;
            }
            return;
        }
    }

    private void SetHighlight(GameObject target)
    {
        if (_highlightedObject == target) return;
        ClearHighlight();
        _highlightedObject = target;
        SpeciesHighlighter.SetHighlight(target, true);
    }

    private void ClearHighlight()
    {
        if (_highlightedObject != null)
        {
            SpeciesHighlighter.SetHighlight(_highlightedObject, false);
            _highlightedObject = null;
        }
    }

    private void ClearAllDetection()
    {
        _scanTarget = null;
        _interactTarget = null;
        _debrisTarget = null;
        ClearHighlight();

        ScanReticleUI.Instance?.SetState(ScanReticleUI.ReticleState.Idle);
        UIManager.Instance?.ShowScanButton(false);
        UIManager.Instance?.ShowInteractButton(false);
    }

    // -----------------------------------------------------------------------
    // Public - called by UIManager buttons or keyboard
    // -----------------------------------------------------------------------

    public void TryScan()
    {
        if (_scanTarget == null || _scanTarget.Data == null)
        {
            Debug.Log("[ScannerSystem] Scan pressed - no mobile target in focus.");
            return;
        }

        SpeciesData data = _scanTarget.Data;
        SpeciesAI targetAI = _scanTarget;

        // Per-species check: if already discovered globally, reject
        if (GameManager.Instance != null && GameManager.Instance.IsDiscovered(data.speciesId))
        {
            Debug.Log($"[ScannerSystem] Scan pressed - species '{data.commonName}' already cataloged.");
            return;
        }

        MinigameManager.Instance.TriggerScanMinigame(
            data,
            () => OnScanSuccess(data, targetAI),
            () => OnScanFailed(targetAI));
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

    private void OnScanSuccess(SpeciesData data, SpeciesAI targetAI)
    {
        if (data == null) return;

        bool isNew = GameManager.Instance != null
            ? GameManager.Instance.DiscoverSpecies(data.zoneIndex, data.speciesId)
            : false;

        if (isNew) GameManager.Instance?.AddRDP(data.rdpReward);

        // Mark ALL instances of this species as discovered (per-species, not per-instance)
        foreach (var otherAI in FindObjectsByType<SpeciesAI>(FindObjectsSortMode.None))
        {
            if (otherAI != null && otherAI.Data != null && otherAI.Data.speciesId == data.speciesId)
            {
                otherAI.IsDiscovered = true;
                otherAI.GetComponent<SonarTrackable>()?.SetDiscovered(true);
            }
        }

        if (targetAI != null)
        {
            targetAI.IsDiscovered = true;
            targetAI.GetComponent<SonarTrackable>()?.SetDiscovered(true);
        }

        ClearAllDetection();

        BestiaryDiscoveryPopup.Instance?.Show(data, isNew);
        Debug.Log($"[ScannerSystem] Scan success: {data.commonName} (new={isNew})");
    }

    public void OnScanFailed(SpeciesAI targetAI)
    {
        if (targetAI != null)
        {
            targetAI.Flee(transform.position);
        }

        ClearAllDetection();
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

        // Mark ALL instances of this stationary species as discovered
        foreach (var otherST in FindObjectsByType<ScanTarget>(FindObjectsSortMode.None))
        {
            if (otherST != null && otherST.Data != null && otherST.Data.speciesId == data.speciesId)
            {
                otherST.IsDiscovered = true;
                otherST.GetComponent<SonarTrackable>()?.SetDiscovered(true);
            }
        }

        ClearAllDetection();

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
            float effectiveMobileRange = baseRange + (GameManager.Instance != null ? GameManager.Instance.ScannerTier * 8f : 0f);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawRay(scanCamera.transform.position, scanCamera.transform.forward * effectiveMobileRange);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.12f);
            Gizmos.DrawRay(scanCamera.transform.position, scanCamera.transform.forward * detectionRange);
        }
    }
}
