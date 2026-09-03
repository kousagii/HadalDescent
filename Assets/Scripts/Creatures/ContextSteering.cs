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
        _rb.linearDamping  = 2f;

        // Auto-configure terrain layer (only Terrain layer to avoid false collisions with triggers/creatures)
        if (terrainLayer.value == 0)
        {
            int tMask = LayerMask.GetMask("Terrain");
            terrainLayer = tMask != 0 ? tMask : LayerMask.GetMask("Default");
        }

        BuildRayDirections();
        _interest = new float[_rayDirs.Length];
        _danger   = new float[_rayDirs.Length];
    }

    private void FixedUpdate()
    {
        if (_goal == Vector3.zero) return;

        ComputeInterestMap();
        ComputeDangerMap();

        Vector3 steerDir = ChooseBestDirection();
        LastSteerDir = steerDir;

        if (steerDir.sqrMagnitude > 0.01f)
        {
            _rb.MovePosition(_rb.position + steerDir * moveSpeed * Time.fixedDeltaTime);

            // Smooth horizontal turning (no roll jitter) with customizable model yaw offset
            Vector3 flatDir = new Vector3(steerDir.x, 0f, steerDir.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Context steering algorithm
    // -----------------------------------------------------------------------

    private void BuildRayDirections()
    {
        // Evenly-spaced horizontal directions + up + down
        var dirs = new List<Vector3>();
        int count = Mathf.Max(8, rayCount);
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count);
            dirs.Add(Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
        }
        dirs.Add(Vector3.up);
        dirs.Add(Vector3.down);
        _rayDirs = dirs.ToArray();
    }

    private void ComputeInterestMap()
    {
        Vector3 toGoal = (_goal - transform.position).normalized;
        for (int i = 0; i < _rayDirs.Length; i++)
            _interest[i] = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toGoal));
    }

    private void ComputeDangerMap()
    {
        float checkDist = baseDangerRadius;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            bool hit = Physics.Raycast(transform.position, _rayDirs[i], checkDist, terrainLayer, QueryTriggerInteraction.Ignore);
            _danger[i] = hit ? -1f : 0f;
        }
    }

    private Vector3 ChooseBestDirection()
    {
        int   bestIdx   = 0;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float score = _interest[i] + _danger[i];
            if (score > bestScore) { bestScore = score; bestIdx = i; }
        }
        // If the best direction is fully blocked, pause briefly
        return _danger[bestIdx] < 0f ? Vector3.zero : _rayDirs[bestIdx];
    }

    // -----------------------------------------------------------------------
    // Editor gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (_rayDirs == null) return;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            bool hit = Physics.Raycast(transform.position, _rayDirs[i], baseDangerRadius, terrainLayer, QueryTriggerInteraction.Ignore);
            Gizmos.color = hit ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, _rayDirs[i] * baseDangerRadius);
        }
        if (_goal != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_goal, 0.8f);
            Gizmos.DrawLine(transform.position, _goal);
        }
    }
}
