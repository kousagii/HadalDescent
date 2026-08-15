using UnityEngine;

/// <summary>
/// Reusable 3D context-steering component. Attach alongside SpeciesAI on every
/// mobile creature. Works entirely in FixedUpdate so physics stay deterministic.
///
/// Algorithm (per FixedUpdate):
///   1. Build interest map  — score each ray direction by how well it faces the goal.
///   2. Build danger  map  — zero out (mark -1) directions that hit an obstacle within dangerRadius.
///   3. Best direction = highest (interest + danger) slot.
///   4. Move rigidbody toward that direction at moveSpeed.
///
/// Layer setup:
///   Set terrainLayer to include the layer you assign to terrain/rock GameObjects
///   so the danger map actually avoids them.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ContextSteering : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Steering")]
    [Tooltip("Number of horizontal rays cast around the creature. 8 = 45-degree increments.")]
    [SerializeField] private int   rayCount      = 8;
    [Tooltip("Ray length used for obstacle detection.")]
    [SerializeField] private float dangerRadius  = 4f;
    [SerializeField] private float moveSpeed     = 2f;
    [Tooltip("Layer(s) considered as obstacles (rocks, terrain, coral structures).")]
    [SerializeField] private LayerMask terrainLayer;

    [Header("Smoothing")]
    [Tooltip("How quickly the creature rotates to face its movement direction.")]
    [SerializeField] private float turnSpeed     = 4f;

    // -----------------------------------------------------------------------
    // Internal state
    // -----------------------------------------------------------------------

    private Vector3[] _rayDirs;
    private float[]   _interest;
    private float[]   _danger;
    private Vector3   _goal;
    private Rigidbody _rb;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Set the world-space position the creature wants to reach.</summary>
    public void SetGoal(Vector3 worldTarget) => _goal = worldTarget;

    /// <summary>Current move speed — SpeciesAI raises this during flee.</summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    /// <summary>Expose the chosen steering direction (used by SpeciesAI for state queries).</summary>
    public Vector3 LastSteerDir { get; private set; }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity     = false;
        _rb.freezeRotation = true;
        _rb.linearDamping  = 2f;   // gentle drag so creatures don't glide forever

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

            // Smoothly face direction of travel (horizontal only — fish don't roll)
            Vector3 flatDir = new Vector3(steerDir.x, 0f, steerDir.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDir);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Context steering algorithm
    // -----------------------------------------------------------------------

    private void BuildRayDirections()
    {
        // 8 evenly-spaced horizontal directions + up + down = 10 rays total
        var dirs = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            dirs.Add(Quaternion.Euler(0, angle, 0) * Vector3.forward);
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
        for (int i = 0; i < _rayDirs.Length; i++)
            _danger[i] = Physics.Raycast(transform.position, _rayDirs[i], dangerRadius, terrainLayer) ? -1f : 0f;
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
        // If the best direction is fully blocked, return zero (creature pauses briefly)
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
            bool hit = Physics.Raycast(transform.position, _rayDirs[i], dangerRadius, terrainLayer);
            Gizmos.color = hit ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, _rayDirs[i] * dangerRadius);
        }
        if (_goal != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_goal, 0.6f);
            Gizmos.DrawLine(transform.position, _goal);
        }
    }
}
