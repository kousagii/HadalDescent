using System.Collections;
using UnityEngine;

/// <summary>
/// Diegetic sector boundary enforcement system.
/// Implements natural, in-universe feedback when approaching the edge of the exploration zone:
/// 
/// 1. Narrative Comms from Dr. Santos:
///    Radios in to explain that the submarine is drifting outside authorized survey coordinates,
///    acoustic relay coverage, or research permits (without breaking character or saying "boundary wall").
/// 
/// 2. Hydrodynamic Counter-Current (Soft Cushion):
///    Instead of a jarring dead-stop against an invisible brick wall, an oceanic counter-current
///    gently decelerates and deflects the submarine back toward the survey transect.
/// 
/// 3. Acoustic Telemetry SFX:
///    Subtle radio chirp and telemetry alert when reaching the peripheral survey margin.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SectorBoundaryWarning : MonoBehaviour
{
    [Header("Boundary Margin Settings")]
    [Tooltip("Distance from the physical boundary wall (in meters) where the soft counter-current and radio alert begin.")]
    [SerializeField] private float warningBufferDistance = 25f;

    [Tooltip("Maximum counter-acceleration force applied as the submarine reaches the outer boundary edge.")]
    [SerializeField] private float maxRepelForce = 16f;

    [Tooltip("Cooldown in seconds between Dr. Santos boundary warning transmissions to prevent repetitive chatter.")]
    [SerializeField] private float dialogueCooldown = 22f;

    [Header("Testing & Debug")]
    [SerializeField] private bool showGizmos = true;

    private Rigidbody _rb;
    private float _lastDialogueTime = -999f;
    private int _dialogueIndex = 0;

    // Immersion-preserving dialogue lines spoken by Dr. Santos
    private static readonly string[] SantosBoundaryDialogues = new string[]
    {
        "Dr. Santos here. You're drifting outside our designated survey coordinates. The acoustic sensor relay won't reach out there—bring her back around to the active transect.",
        "Researcher, our telemetry indicates you're approaching the edge of our authorized study sector. There's no cataloging scheduled out in open water; steer back to the survey grid.",
        "Telemetry warning: acoustic tracking signal is degrading. You're beyond our expedition's research perimeter. Adjust heading back toward the central trench.",
        "Careful out there. That area lies beyond our bathymetric sensor grid and acoustic beacon coverage. Let's keep our focus within the active research sector."
    };

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Add a 10s grace period on scene load so it doesn't fire immediately at start
        _lastDialogueTime = Time.time + 10f;
    }

    private void FixedUpdate()
    {
        int zoneIdx = ZoneManager.CurrentZoneIndex;
        if (!ZoneConfig.IsValidZone(zoneIdx)) return;

        ZoneDefinition zone = ZoneConfig.Zones[zoneIdx];
        float halfW = zone.playableWidth * 0.5f;
        float halfL = zone.playableLength * 0.5f;

        Vector3 pos = transform.position;
        float absX = Mathf.Abs(pos.x);
        float absZ = Mathf.Abs(pos.z);

        // Check if inside the warning margin
        float overflowX = absX - (halfW - warningBufferDistance);
        float overflowZ = absZ - (halfL - warningBufferDistance);

        if (overflowX > 0f || overflowZ > 0f)
        {
            // Calculate normalized depth into buffer [0, 1]
            float normX = Mathf.Clamp01(overflowX / warningBufferDistance);
            float normZ = Mathf.Clamp01(overflowZ / warningBufferDistance);
            float penetration = Mathf.Max(normX, normZ);

            // Vector pointing horizontally toward the sector origin (0, pos.y, 0)
            Vector3 toCenter = new Vector3(-pos.x, 0f, -pos.z).normalized;

            // Check if player is steering toward or away from the center
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            float dotToCenter = Vector3.Dot(forward, toCenter);

            // If heading outward or perpendicular, apply hydrodynamic counter-current
            if (dotToCenter < 0.35f)
            {
                float repelStrength = Mathf.Lerp(3f, maxRepelForce, penetration);
                _rb.AddForce(toCenter * repelStrength, ForceMode.Acceleration);

                // Damp outward velocity
                Vector3 currentVel = _rb.linearVelocity;
                Vector3 outwardVel = Vector3.Project(currentVel, -toCenter);
                if (Vector3.Dot(outwardVel, -toCenter) > 0f)
                {
                    _rb.linearVelocity -= outwardVel * (penetration * 0.5f);
                }
            }

            // Trigger Dr. Santos narrative comms
            TryTriggerBoundaryDialogue();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Check if colliding with boundary perimeter walls
        string colName = collision.gameObject.name;
        if (colName.StartsWith("Wall_") || colName.Contains("Boundary"))
        {
            TryTriggerBoundaryDialogue();

            // Gently deflect velocity inward
            Vector3 toCenter = new Vector3(-transform.position.x, 0f, -transform.position.z).normalized;
            _rb.AddForce(toCenter * 10f, ForceMode.VelocityChange);
        }
    }

    private void TryTriggerBoundaryDialogue()
    {
        // Suppress during tutorial sequences to avoid talking over scripted story
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive) return;

        // Suppress if any modal or minigame is active
        if (UIManager.IsAnyPanelOrModalOpen(includeMinigames: true)) return;

        // Check cooldown
        if (Time.time - _lastDialogueTime < dialogueCooldown) return;

        // Check if radio box is already active
        if (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy) return;

        _lastDialogueTime = Time.time;

        // Pick next line in rotation
        string speech = SantosBoundaryDialogues[_dialogueIndex % SantosBoundaryDialogues.Length];
        _dialogueIndex++;

        // Transmit via radio textbox
        RadioTextboxUI.Instance?.DisplayMessage(
            speaker: "DR. SANTOS",
            dialogue: speech,
            autoDismissDelay: 4.5f
        );

        // Telemetry audio cue
        AudioManager.Instance?.PlayRadioChirp();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        int zoneIdx = ZoneManager.CurrentZoneIndex;
        if (!ZoneConfig.IsValidZone(zoneIdx)) zoneIdx = 0;
        var zone = ZoneConfig.Zones[zoneIdx];

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Vector3 size = new Vector3(zone.playableWidth - warningBufferDistance * 2f, zone.playableDepth, zone.playableLength - warningBufferDistance * 2f);
        Vector3 center = new Vector3(0f, -zone.playableDepth * 0.5f, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
