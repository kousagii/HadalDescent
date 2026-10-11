using System.Collections;
using UnityEngine;

/// <summary>
/// World trigger placed at oceanic hazard areas (thermal vents, trench fissures, current surges).
///
/// Features:
///   - Automatically integrates with SonarTrackable (Type = Hazard) to render as a red danger blip on Sonar.
///   - Triggers Mini-game 4 (Hazard Dodge) when the player submarine enters its proximity.
///   - Configurable cooldown to prevent spamming.
///   - Optional global periodic surge timer for deeper zones (~2-3 min in Abyss/Hadal, ~3-4 min in Midnight).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class HazardZoneTrigger : MonoBehaviour
{
    [Header("Hazard Configuration")]
    [Tooltip("Ocean zone index (0 = Sunlight, 1 = Twilight, 2 = Midnight, 3 = Abyss, 4 = Hadal).")]
    [SerializeField] private int zoneIndex = 0;

    [Tooltip("Detection radius in meters to trigger the minigame.")]
    [SerializeField] private float triggerRadius = 14f;

    [Tooltip("Cooldown in seconds before this specific hazard zone can trigger again.")]
    [SerializeField] private float triggerCooldown = 180f;

    [Header("Testing & Automation")]
    [Tooltip("If true, automatically fires the hazard minigame shortly after game starts for quick testing.")]
    [SerializeField] private bool testTriggerOnStart = false;
    [Tooltip("Seconds to wait before firing the initial test surge.")]
    [SerializeField] private float initialTestDelay = 4f;

    [Header("Periodic Hazard Surge (Optional)")]
    [Tooltip("If true, periodically triggers dynamic hazard alerts while exploring without needing a physical collision.")]
    [SerializeField] private bool enablePeriodicSurge = true;

    [Header("Spatial Proximity Trigger")]
    [Tooltip("If true, triggers when player gets close to this object's world position. False for global managers.")]
    [SerializeField] private bool enableSpatialProximity = true;

    [Header("Grace Period & Safety")]
    [Tooltip("Startup grace period in seconds after zone loading before hazard can trigger.")]
    [SerializeField] private float sceneStartGracePeriod = 25f;

    [Tooltip("Minimum horizontal distance from zone spawn origin (0, y, 0) required for spatial triggering.")]
    [SerializeField] private float minSafeDistanceFromOrigin = 40f;

    private SphereCollider  _collider;
    private SonarTrackable  _sonarTrackable;
    private PlayerMovement  _playerMovement;
    private float           _sceneStartTime;
    private float           _lastTriggerTime = 0f;
    private bool            _isTriggering    = false;

    private void Awake()
    {
        _sceneStartTime  = Time.time;
        _lastTriggerTime = Time.time;

        // Auto-detect manager objects placed at origin (0,0,0): these should NOT be spatial collision triggers!
        if ((transform.parent != null && transform.parent.name.IndexOf("Manager", System.StringComparison.OrdinalIgnoreCase) >= 0)
            || transform.position.sqrMagnitude < 4f)
        {
            enableSpatialProximity = false;
        }

        if (enableSpatialProximity)
        {
            _collider = GetComponent<SphereCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
            _collider.radius = triggerRadius;

            // Auto-configure SonarTrackable
            _sonarTrackable = GetComponent<SonarTrackable>();
            if (_sonarTrackable == null)
            {
                _sonarTrackable = gameObject.AddComponent<SonarTrackable>();
            }
            _sonarTrackable.Initialize(SonarTrackable.SonarTargetType.Hazard, "Thermal / Current Hazard");
        }
        else
        {
            var existingCol = GetComponent<Collider>();
            if (existingCol != null) existingCol.enabled = false;
            var existingTrackable = GetComponent<SonarTrackable>();
            if (existingTrackable != null) existingTrackable.enabled = false;
        }
    }

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        _lastTriggerTime = Time.time;

        if (testTriggerOnStart)
        {
            StartCoroutine(TestTriggerRoutine());
        }
        else if (enablePeriodicSurge)
        {
            StartCoroutine(PeriodicSurgeRoutine());
        }
    }

    private bool IsHazardTriggerSuppressed()
    {
        // 1. Startup grace period after zone loading
        if (Time.time - _sceneStartTime < sceneStartGracePeriod) return true;
        if (ZoneBoundaryTrigger._globalCooldownTimer > 0f) return true;

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            // Allow triggering if player is on Step 7 (Hazard Encounter)
            bool isStep7 = TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Step7_HazardEncounter;
            if (!isStep7) return true;
        }
        return false;
    }

    private void Update()
    {
        if (!enableSpatialProximity) return;
        if (IsHazardTriggerSuppressed()) return;
        if (_isTriggering) return;
        if (UIManager.IsAnyPanelOrModalOpen(includeMinigames: false)) return;
        if (Time.time - _lastTriggerTime < triggerCooldown) return;

        // Proximity check fallback (ensures triggering even if layers/tags on the player are custom)
        if (_playerMovement == null) _playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (_playerMovement != null)
        {
            Vector3 pPos = _playerMovement.transform.position;
            // Never trigger if player is still in the entry / spawn corridor
            if (new Vector2(pPos.x, pPos.z).magnitude < minSafeDistanceFromOrigin) return;

            float dist = Vector3.Distance(transform.position, pPos);
            if (dist <= triggerRadius)
            {
                TriggerHazardMinigame();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enableSpatialProximity) return;
        if (IsHazardTriggerSuppressed()) return;
        if (_isTriggering) return;
        if (UIManager.IsAnyPanelOrModalOpen(includeMinigames: false)) return;
        if (Time.time - _lastTriggerTime < triggerCooldown) return;

        // Check if player submarine entered trigger
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            Vector3 pPos = other.transform.position;
            if (new Vector2(pPos.x, pPos.z).magnitude < minSafeDistanceFromOrigin) return;

            TriggerHazardMinigame();
        }
    }

    private IEnumerator TestTriggerRoutine()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            yield break;
        }

        float timer = initialTestDelay;
        while (timer > 0f)
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                yield break;
            }

            if (!UIManager.IsAnyPanelOrModalOpen())
            {
                timer -= Time.deltaTime;
            }
            yield return null;
        }

        while (UIManager.IsAnyPanelOrModalOpen())
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                yield break;
            }
            yield return null;
        }

        TriggerHazardMinigame();
    }

    /// <summary>
    /// Deactivates this hazard zone trigger so it disappears both from the world (trigger zone) and the sonar map.
    /// </summary>
    public void DeactivateHazard()
    {
        _isTriggering = false;
        if (_sonarTrackable != null)
        {
            _sonarTrackable.enabled = false;
        }
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Initiates Phase 1 & 2 of Mini-game 4.
    /// Can also be clicked directly from the Inspector component context menu!
    /// </summary>
    [ContextMenu("▶ Test Trigger Hazard Minigame Now")]
    public void TriggerHazardMinigame()
    {
        if (IsHazardTriggerSuppressed()) return;
        if (UIManager.IsAnyPanelOrModalOpen(includeMinigames: false)) return;
        if (MinigameManager.Instance == null || MinigameManager.Instance.IsMinigameActive) return;

        _lastTriggerTime = Time.time;
        _isTriggering = true;

        // Disappear both from sonar map and trigger zone after it has been activated
        if (_sonarTrackable != null) _sonarTrackable.enabled = false;
        if (_collider != null) _collider.enabled = false;

        int activeZone = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : zoneIndex;

        // Deactivate the hazard zone trigger GameObject immediately
        DeactivateHazard();

        MinigameManager.Instance.TriggerHazardDodgeMinigame(
            activeZone,
            onSuccess: (bonusEarned) =>
            {
                Debug.Log($"[HazardZoneTrigger] Hazard Dodge Cleared! +{bonusEarned} RDP");
                if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Step7_HazardEncounter)
                {
                    TutorialManager.Instance.OnHazardCompleted(survived: true);
                }
            },
            onFail: () =>
            {
                Debug.Log("[HazardZoneTrigger] Hazard Dodge Failed! Hull compromised, -50 RDP");
                if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Step7_HazardEncounter)
                {
                    TutorialManager.Instance.OnHazardCompleted(survived: false);
                }
            }
        );
    }

    private IEnumerator PeriodicSurgeRoutine()
    {
        // Initial grace period delay on zone entry before any periodic surge countdown starts
        yield return new WaitForSeconds(sceneStartGracePeriod);

        while (true)
        {
            int activeZone = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : zoneIndex;

            // Frequency based on zone depth:
            // Sunlight: ~180s (3m), Twilight: ~180s (3m), Midnight: ~150s (2.5m), Abyss/Hadal: ~120s (2m)
            float interval = activeZone switch
            {
                3 => Random.Range(120f, 180f), // Abyss: ~2-3 min
                4 => Random.Range(120f, 180f), // Hadal: ~2-3 min
                2 => Random.Range(180f, 240f), // Midnight: ~3-4 min
                1 => Random.Range(200f, 260f), // Twilight: ~3.5-4.5 min
                _ => Random.Range(120f, 180f)  // Sunlight test
            };

            // Countdown timer that strictly pauses whenever Shop, Bestiary, Pause, or any popup is open!
            // Time spent browsing menus does NOT count down, preventing sudden activation upon closing.
            float timer = interval;
            while (timer > 0f)
            {
                if (!UIManager.IsAnyPanelOrModalOpen() && (TutorialManager.Instance == null || !TutorialManager.Instance.IsTutorialActive))
                {
                    timer -= Time.deltaTime;
                }
                yield return null;
            }

            // Once countdown elapses, wait until all menus/modals are closed before triggering
            while (UIManager.IsAnyPanelOrModalOpen() || (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive))
            {
                yield return null;
            }

            // Only trigger if player is exploring peacefully (no other minigame or popup open)
            if (MinigameManager.Instance != null && !MinigameManager.Instance.IsMinigameActive)
            {
                TriggerHazardMinigame();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawSphere(transform.position, 1.5f);
    }
}
