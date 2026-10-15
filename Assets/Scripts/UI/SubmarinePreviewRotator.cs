using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to the Submarine Viewport RawImage in the Shop UI.
/// Provides 360° interactive touch/mouse drag rotation, pitch limits, and smooth idle spin.
/// Matches the 3D model viewer behavior in the Bestiary.
/// </summary>
public class SubmarinePreviewRotator : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Target to Rotate")]
    [Tooltip("The Transform of the submarine 3D model or its pivot.")]
    [SerializeField] private Transform targetTransform;

    [Header("Drag Sensitivity")]
    [Tooltip("Sensitivity of touch/mouse horizontal drag (yaw).")]
    [SerializeField] private float yawSensitivity = 0.45f;

    [Tooltip("Sensitivity of touch/mouse vertical drag (pitch).")]
    [SerializeField] private float pitchSensitivity = 0.35f;

    [Header("Pitch Limits (Vertical Tilt)")]
    [Tooltip("Allow vertical tilt when dragging up/down.")]
    [SerializeField] private bool allowPitch = true;

    [Tooltip("Minimum downward pitch in degrees.")]
    [SerializeField] private float minPitch = -35f;

    [Tooltip("Maximum upward pitch in degrees.")]
    [SerializeField] private float maxPitch = 35f;

    [Header("Idle Turntable Animation")]
    [Tooltip("Degrees per second the submarine rotates around Y axis when idle.")]
    [SerializeField] private float idleRotationSpeed = 18f;

    [Tooltip("Seconds of inactivity before idle rotation resumes.")]
    [SerializeField] private float idleDelay = 0.8f;

    [Header("Inertia & Smoothing")]
    [Tooltip("Inertia momentum decay speed after drag release.")]
    [SerializeField] private float inertiaDamping = 6f;

    private bool    _isDragging = false;
    private float   _idleTimer  = 0f;
    private float   _currentPitch = 12f; // Slight natural top-down angle
    private float   _currentYaw   = -45f; // Sleek 3/4 front-side profile
    private Vector2 _velocity;

    public static bool IsAnyDragging { get; private set; }

    public Transform TargetTransform
    {
        get => targetTransform;
        set => targetTransform = value;
    }

    private void Start()
    {
        ApplyRotation();
    }

    private void OnDisable()
    {
        _isDragging = false;
        IsAnyDragging = false;
    }

    private void Update()
    {
        if (targetTransform == null) return;

        float dt = Time.unscaledDeltaTime;

        if (_isDragging)
        {
            _idleTimer = 0f;
        }
        else
        {
            // Apply inertia momentum decay
            if (_velocity.sqrMagnitude > 0.001f)
            {
                _currentYaw   -= _velocity.x * dt;
                if (allowPitch)
                {
                    _currentPitch = Mathf.Clamp(_currentPitch + (_velocity.y * dt), minPitch, maxPitch);
                }
                _velocity = Vector2.Lerp(_velocity, Vector2.zero, dt * inertiaDamping);
            }

            // Idle turntable spin
            _idleTimer += dt;
            if (_idleTimer >= idleDelay)
            {
                _currentYaw += idleRotationSpeed * dt;
            }

            ApplyRotation();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        IsAnyDragging = true;
        _idleTimer  = 0f;
        _velocity   = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetTransform == null) return;

        _idleTimer = 0f;

        float deltaYaw   = eventData.delta.x * yawSensitivity;
        float deltaPitch = eventData.delta.y * pitchSensitivity;

        _currentYaw -= deltaYaw;
        if (allowPitch)
        {
            _currentPitch = Mathf.Clamp(_currentPitch + deltaPitch, minPitch, maxPitch);
        }

        // Store velocity for inertia momentum
        float dt = Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
        _velocity = new Vector2(deltaYaw / dt, deltaPitch / dt) * 0.15f;

        ApplyRotation();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        IsAnyDragging = false;
    }

    private void ApplyRotation()
    {
        if (targetTransform == null) return;

        // Apply smooth compound rotation: Yaw around world Y, Pitch around local X
        targetTransform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }

    /// <summary>
    /// Resets the preview submarine to the default attractive 3/4 isometric profile.
    /// </summary>
    public void ResetToDefaultAngle()
    {
        _currentYaw   = -45f;
        _currentPitch = 12f;
        _velocity     = Vector2.zero;
        _idleTimer    = 0f;
        ApplyRotation();
    }
}
