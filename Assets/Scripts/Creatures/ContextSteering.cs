using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable 3D context-steering component for marine creatures.
///
/// Features:
///   - Self-collision immunity: ignores the creature's own colliders regardless of scale.
///   - Dynamic scale adaptation: ray length & start offsets scale with prefab size.
///   - Continuous vector blending: smooth, organic swimming curves (no discrete 45° snap twitching).
///   - Velocity smoothing & gradual turning: natural, realistic fish locomotion.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ContextSteering : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Steering")]
    [Tooltip("Number of horizontal steering rays around the creature.")]
    [SerializeField] private int rayCount = 12;

    [Tooltip("Forward distance to look ahead for obstacles (boulders, terrain).")]
    [SerializeField] private float baseDangerRadius = 5f;

    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Layer mask for solid obstacles (Terrain layer).")]
    [SerializeField] private LayerMask terrainLayer;

    [Header("Smoothing")]
    [Tooltip("How smoothly the creature turns to face its swim direction.")]
    [SerializeField] private float turnSpeed = 3.5f;

    [Tooltip("How quickly velocity accelerates and decelerates.")]
    [SerializeField] private float acceleration = 4.0f;

    [Tooltip("Model facing yaw offset in degrees (set to 180 if model was exported facing backward in Blender).")]
    [SerializeField] private float modelYawOffset = 0f;

    // -----------------------------------------------------------------------
    // Internal state
    // -----------------------------------------------------------------------

    private Vector3[]   _rayDirs;
    private float[]     _interest;
    private float[]     _danger;
    private Vector3     _goal;
    private Rigidbody   _rb;
    private Vector3     _currentVelocity;
    private float       _creatureRadius = 1.0f;
    private HashSet<Collider> _myColliders = new HashSet<Collider>();

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Set the world-space target position.</summary>
    public void SetGoal(Vector3 worldTarget) => _goal = worldTarget;

    /// <summary>Current move speed - raised during fleeing.</summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.1f, value);
    }

    /// <summary>Model facing yaw offset in degrees (e.g. 180 if 3D model was exported facing backward).</summary>
    public float ModelYawOffset
    {
        get => modelYawOffset;
        set => modelYawOffset = value;
    }

    /// <summary>Expose current smoothed movement direction.</summary>
    public Vector3 LastSteerDir { get; private set; }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity     = false;
        _rb.freezeRotation = true;
        _rb.linearDamping  = 1.5f;

        // Auto-configure terrain layer (include both Terrain and Default layers)
        if (terrainLayer.value == 0)
        {
            int tMask = LayerMask.GetMask("Terrain");
            int dMask = LayerMask.GetMask("Default");
            terrainLayer = (tMask != 0 ? tMask : 0) | (dMask != 0 ? dMask : 1);
            if (terrainLayer.value == 0) terrainLayer = ~0;
        }

        CacheCreatureSize();
        BuildRayDirections();

        _interest = new float[_rayDirs.Length];
        _danger   = new float[_rayDirs.Length];
    }

    private void Start()
    {
        CacheCreatureSize();
    }

    private void FixedUpdate()
    {
        if (_goal == Vector3.zero) return;

        ComputeInterestMap();
        ComputeDangerMap();

        Vector3 desiredDir = ComputeBlendedDirection();
        LastSteerDir = desiredDir;

        // Smooth acceleration towards desired direction
        Vector3 targetVelocity = desiredDir * moveSpeed;
        _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);

        // Ground proximity buoyancy: prevent swimming under the seabed
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, _creatureRadius + 2.0f, terrainLayer, QueryTriggerInteraction.Ignore))
        {
            if (!_myColliders.Contains(groundHit.collider))
            {
                float groundDistance = groundHit.distance - _creatureRadius;
                if (groundDistance < 1.2f)
                {
                    float pushUp = (1.2f - groundDistance) * 4.0f;
                    _currentVelocity.y = Mathf.Max(_currentVelocity.y, pushUp);
                }
            }
        }

        if (_currentVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 moveDelta = _currentVelocity * Time.fixedDeltaTime;

            // Physical collision slide check: prevent clipping into rocks, corals, or seabed
            if (Physics.SphereCast(transform.position, _creatureRadius * 0.75f, moveDelta.normalized, out RaycastHit obstacleHit, moveDelta.magnitude + 0.15f, terrainLayer, QueryTriggerInteraction.Ignore))
            {
                if (!_myColliders.Contains(obstacleHit.collider) && obstacleHit.transform.root != transform.root)
                {
                    // Deflect velocity along obstacle surface normal
                    _currentVelocity = Vector3.ProjectOnPlane(_currentVelocity, obstacleHit.normal);
                    moveDelta = _currentVelocity * Time.fixedDeltaTime;
                }
            }

            _rb.MovePosition(_rb.position + moveDelta);

            // Smooth horizontal turning (no roll jitter) with customizable model yaw offset
            Vector3 flatDir = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            if (flatDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Context steering algorithm
    // -----------------------------------------------------------------------

    private void CacheCreatureSize()
    {
        _myColliders.Clear();
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) _myColliders.Add(c);

        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            _creatureRadius = Mathf.Max(0.6f, Mathf.Max(b.extents.x, b.extents.z));
        }
        else
        {
            _creatureRadius = Mathf.Max(0.6f, transform.lossyScale.magnitude * 0.5f);
        }
    }

    private void BuildRayDirections()
    {
        var dirs = new List<Vector3>();
        // Evenly spaced horizontal rays around creature
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            dirs.Add(Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
        }
        // Elevation angle rays for 3D navigation
        dirs.Add(Vector3.up);
        dirs.Add(Vector3.down);
        dirs.Add(new Vector3(0.7f, 0.7f, 0f).normalized);
        dirs.Add(new Vector3(-0.7f, 0.7f, 0f).normalized);
        dirs.Add(new Vector3(0.7f, -0.7f, 0f).normalized);
        dirs.Add(new Vector3(-0.7f, -0.7f, 0f).normalized);

        _rayDirs = dirs.ToArray();
    }

    private void ComputeInterestMap()
    {
        Vector3 toGoal = (_goal - transform.position).normalized;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float dot = Vector3.Dot(_rayDirs[i], toGoal);
            // Non-linear interest curve gives clear directional preference
            _interest[i] = dot > 0f ? Mathf.Pow(dot, 1.5f) : 0f;
        }
    }

    private void ComputeDangerMap()
    {
        float checkDist = baseDangerRadius + _creatureRadius;

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            Vector3 rayDir = _rayDirs[i];
            // Start ray outside creature's own body to completely prevent self-collision
            Vector3 rayStart = transform.position + rayDir * (_creatureRadius * 0.95f);

            _danger[i] = 0f;

            var hits = Physics.RaycastAll(rayStart, rayDir, checkDist, terrainLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                // Verify hit is NOT part of this creature
                if (hit.collider != null && !_myColliders.Contains(hit.collider) && hit.transform.root != transform.root)
                {
                    // Closer obstacles create higher danger penalty
                    float closeness = 1.0f - Mathf.Clamp01(hit.distance / checkDist);
                    _danger[i] = -closeness * 2.0f;
                    break;
                }
            }
        }
    }

    private Vector3 ComputeBlendedDirection()
    {
        Vector3 blended = Vector3.zero;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float score = Mathf.Max(0f, _interest[i] + _danger[i]);
            blended += _rayDirs[i] * score;
        }

        if (blended.sqrMagnitude > 0.001f)
            return blended.normalized;

        // Fallback: if forward path is fully blocked, steer upwards to open water
        return Vector3.up;
    }

    // -----------------------------------------------------------------------
    // Editor gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (_rayDirs == null) return;
        float checkDist = baseDangerRadius + _creatureRadius;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            Vector3 start = transform.position + _rayDirs[i] * (_creatureRadius * 0.95f);
            Gizmos.color = _danger != null && i < _danger.Length && _danger[i] < 0f ? Color.red : Color.green;
            Gizmos.DrawRay(start, _rayDirs[i] * checkDist);
        }
        if (_goal != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_goal, 0.8f);
            Gizmos.DrawLine(transform.position, _goal);
        }
    }
}
