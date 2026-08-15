using UnityEngine;

/// <summary>
/// Manages the submarine's first-person camera.
///
/// Responsibilities:
///   - Applies drag input (from TouchDragZone) as camera yaw and pitch.
///   - Exposes CameraYaw and CameraPitch for PlayerMovement to consume.
///   - Runs in LateUpdate so it always reflects the body's final position.
///
/// Setup:
///   Attach this to the Camera GameObject that is a child of the submarine body.
///   The camera's local position should be set in the Inspector (cockpit offset).
/// </summary>
public class SubmarineCamera : MonoBehaviour
{
    [Header("Look Sensitivity")]
    [SerializeField] private float yawSensitivity   = 0.15f;
    [SerializeField] private float pitchSensitivity = 0.12f;

    [Header("Pitch Clamp (degrees)")]
    [SerializeField] private float minPitch = -50f;
    [SerializeField] private float maxPitch =  50f;

    [Header("Smoothing")]
    [Tooltip("Higher = snappier camera response. 0 = no smoothing.")]
    [SerializeField] private float smoothSpeed = 20f;

    [Header("References")]
    [SerializeField] private TouchDragZone dragZone;

    // Accumulated angles (world-space yaw, local-space pitch)
    private float _yaw;
    private float _pitch;

    // Smoothed angles
    private float _smoothYaw;
    private float _smoothPitch;

    /// <summary>World-space yaw the camera is currently pointing.</summary>
    public float CameraYaw   => _smoothYaw;

    /// <summary>Local-space pitch (positive = nose up).</summary>
    public float CameraPitch => _smoothPitch;

    private void Awake()
    {
        // Initialise from the body's current world rotation so there is no snap on start.
        Vector3 euler = transform.parent != null
            ? transform.parent.eulerAngles
            : transform.eulerAngles;

        _yaw        = euler.y;
        _pitch      = 0f;
        _smoothYaw  = _yaw;
        _smoothPitch = _pitch;
    }

    private void Start()
    {
        if (dragZone == null)
            dragZone = FindFirstObjectByType<TouchDragZone>();
    }

    private void LateUpdate()
    {
        // --- 1. Read drag delta from the UI touch zone ---
        Vector2 drag = dragZone != null ? dragZone.TouchDelta : Vector2.zero;

        // Accumulate angles
        _yaw   += drag.x * yawSensitivity;
        _pitch -= drag.y * pitchSensitivity;           // negative: drag down -> look up
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // --- 2. Smooth towards target ---
        float t = smoothSpeed > 0f ? Time.deltaTime * smoothSpeed : 1f;
        _smoothYaw   = Mathf.LerpAngle(_smoothYaw,   _yaw,   t);
        _smoothPitch = Mathf.Lerp     (_smoothPitch, _pitch, t);

        // --- 3. Apply rotation: pitch is always relative to world up ---
        transform.rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);
    }

    // ------------------------------------------------------------------
    // Public helpers
    // ------------------------------------------------------------------

    /// <summary>Force-sync the camera yaw to the body's yaw (called on spawn / zone load).</summary>
    public void SnapYawToBody()
    {
        if (transform.parent != null)
            _yaw = transform.parent.eulerAngles.y;
        else
            _yaw = transform.eulerAngles.y;

        _smoothYaw = _yaw;
    }
}
