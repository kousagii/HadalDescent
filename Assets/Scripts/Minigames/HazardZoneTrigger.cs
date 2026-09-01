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

    private SphereCollider  _collider;
    private SonarTrackable  _sonarTrackable;
    private PlayerMovement  _playerMovement;
    private float           _lastTriggerTime = -999f;
    private bool            _isTriggering    = false;

    private void Awake()
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

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (testTriggerOnStart)
        {
            StartCoroutine(TestTriggerRoutine());
        }
        else if (enablePeriodicSurge)
        {
            StartCoroutine(PeriodicSurgeRoutine());
        }
    }

    private void Update()
    {
        if (_isTriggering) return;
        if (Time.time - _lastTriggerTime < triggerCooldown) return;

        // Proximity check fallback (ensures triggering even if layers/tags on the player are custom)
        if (_playerMovement == null) _playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (_playerMovement != null)
        {
            float dist = Vector3.Distance(transform.position, _playerMovement.transform.position);
            if (dist <= triggerRadius)
            {
                TriggerHazardMinigame();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isTriggering) return;
        if (Time.time - _lastTriggerTime < triggerCooldown) return;

        // Check if player submarine entered trigger
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            TriggerHazardMinigame();
        }
    }

    private IEnumerator TestTriggerRoutine()
    {
        yield return new WaitForSeconds(initialTestDelay);
        TriggerHazardMinigame();
    }

    /// <summary>
    /// Initiates Phase 1 & 2 of Mini-game 4.
    /// Can also be clicked directly from the Inspector component context menu!
    /// </summary>
    [ContextMenu("▶ Test Trigger Hazard Minigame Now")]
    public void TriggerHazardMinigame()
    {
        if (MinigameManager.Instance == null || MinigameManager.Instance.IsMinigameActive) return;

        _lastTriggerTime = Time.time;
        _isTriggering = true;

        int activeZone = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : zoneIndex;

        MinigameManager.Instance.TriggerHazardDodgeMinigame(
            activeZone,
            onSuccess: (bonusEarned) =>
            {
                _isTriggering = false;
                Debug.Log($"[HazardZoneTrigger] Hazard Dodge Cleared! +{bonusEarned} RDP");
            },
            onFail: () =>
            {
                _isTriggering = false;
                Debug.Log("[HazardZoneTrigger] Hazard Dodge Failed! Hull compromised, -50 RDP");
            }
        );
    }

    private IEnumerator PeriodicSurgeRoutine()
    {
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

            yield return new WaitForSeconds(interval);

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
