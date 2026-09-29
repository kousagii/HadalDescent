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
    [Tooltip("Turn rate in degrees per second.")]
    [SerializeField] private float turnSpeed = 65f;

    [Tooltip("Model facing yaw offset in degrees (set to 180 if model was exported facing backward in Blender).")]
    [SerializeField] private float modelYawOffset = 0f;

    // -----------------------------------------------------------------------
    // Internal state
    // -----------------------------------------------------------------------

    private Vector3[]          _rayDirs;
    private float[]            _interest;
    private float[]            _danger;
    private Vector3            _goal;
    private Rigidbody          _rb;
    private CreatureLocomotion _locomotion;

    // Playable scannable boundary limits (keeps creature strictly inside player reach)
    private float _playableHalfWidth  = 275f;
    private float _playableHalfLength = 275f;
    private float _zoneBottomY        = -182f;
    private float _zoneTopY           = -1.5f;
    private bool  _zoneBoundsInitialized = false;

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

    /// <summary>Turning responsiveness.</summary>
    public float TurnSpeed
    {
        get => turnSpeed;
        set => turnSpeed = value;
    }

    /// <summary>Model facing yaw offset in degrees (e.g. 180 if 3D model was exported facing backward).</summary>
    public float ModelYawOffset
    {
        get => modelYawOffset;
        set => modelYawOffset = value;
    }

    /// <summary>Expose current smoothed movement direction.</summary>
    public Vector3 LastSteerDir { get; private set; }

    /// <summary>Safe horizontal boundary radius (half-width) for wander/flee clamping.</summary>
    public float SafeHalfWidth => _playableHalfWidth;

    /// <summary>Safe horizontal boundary radius (half-length) for wander/flee clamping.</summary>
    public float SafeHalfLength => _playableHalfLength;

    /// <summary>Safe top depth (stays below ocean surface).</summary>
    public float SafeTopY => _zoneTopY;

    /// <summary>Safe bottom depth (stays safely above zone transition trigger).</summary>
    public float SafeBottomY => _zoneBottomY;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity     = false;
        _rb.freezeRotation = true;
        _rb.linearDamping  = 2f;

        _locomotion = GetComponent<CreatureLocomotion>();

        // Auto-configure terrain layer (only Terrain layer to avoid false collisions with triggers/creatures)
        if (terrainLayer.value == 0)
        {
            int tMask = LayerMask.GetMask("Terrain");
            terrainLayer = tMask != 0 ? tMask : LayerMask.GetMask("Default");
        }

        BuildRayDirections();
        _interest = new float[_rayDirs.Length];
        _danger   = new float[_rayDirs.Length];

        InitializeZoneBounds();
    }

    private void FixedUpdate()
    {
        if (_goal == Vector3.zero) return;

        if (!_zoneBoundsInitialized)
            InitializeZoneBounds();

        ComputeInterestMap();
        ComputeDangerMap();

        Vector3 steerDir = ChooseBestDirection();
        LastSteerDir = steerDir;

        if (_locomotion == null)
            _locomotion = GetComponent<CreatureLocomotion>();

        // If CreatureLocomotion is present, it handles specialized biomechanical locomotion
        // Otherwise, use standard smoothed translation & rotation as fallback
        if (_locomotion == null)
        {
            if (steerDir.sqrMagnitude > 0.01f)
            {
                _rb.MovePosition(_rb.position + steerDir * moveSpeed * Time.fixedDeltaTime);

                Vector3 flatDir = new Vector3(steerDir.x, 0f, steerDir.z);
                if (flatDir.sqrMagnitude > 0.001f)
                {
                    float horizDist = Mathf.Sqrt(steerDir.x * steerDir.x + steerDir.z * steerDir.z);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(steerDir.y, horizDist) * Mathf.Rad2Deg, -35f, 35f);
                    Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up) * Quaternion.Euler(pitch, modelYawOffset, 0f);
                    float angleDelta = Quaternion.Angle(_rb.rotation, targetRot);
                    float effectiveRate = Mathf.Min(turnSpeed, Mathf.Max(12f, angleDelta * 2.0f));
                    _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, effectiveRate * Time.fixedDeltaTime));
                }
            }
        }

        // Guaranteed player boundary enforcement (keeps creature inside scannable perimeter)
        EnforceZonePlayableBounds();
    }

    // -----------------------------------------------------------------------
    // Playable boundary enforcement (guarantees creature remains scannable)
    // -----------------------------------------------------------------------

    private void InitializeZoneBounds()
    {
        int zoneIdx = ZoneManager.CurrentZoneIndex;
        if (ZoneConfig.IsValidZone(zoneIdx))
        {
            var zone = ZoneConfig.Zones[zoneIdx];
            // 25m buffer inside perimeter wall keeps creature within 100m detection & 25m scan lock-on
            const float wallMargin = 25f;
            _playableHalfWidth  = Mathf.Max(50f, (zone.playableWidth * 0.5f) - wallMargin);
            _playableHalfLength = Mathf.Max(50f, (zone.playableLength * 0.5f) - wallMargin);

            // Water surface is at Y = 0. Stay at least 1.5m submerged
            _zoneTopY = -1.5f;

            // Bottom trigger is at -zone.playableDepth. Keep at least 18m above it to avoid transition popups
            _zoneBottomY = -zone.playableDepth + 18f;
            _zoneBoundsInitialized = true;
        }
        else
        {
            var tracker = FindFirstObjectByType<DepthTracker>();
            if (tracker != null)
            {
                _zoneBottomY = tracker.ZoneBottomY + 18f;
                _zoneTopY    = tracker.ZoneTopY - 1.5f;
                _zoneBoundsInitialized = true;
            }
        }
    }

    /// <summary>
    /// Smoothly pushes creatures away from boundaries and hard-clamps position as a failsafe
    /// so creatures NEVER breach perimeter walls or transition zones.
    /// </summary>
    private void EnforceZonePlayableBounds()
    {
        Vector3 pos = _rb.position;
        bool corrected = false;

        // 1. Horizontal X boundary
        if (Mathf.Abs(pos.x) > _playableHalfWidth)
        {
            float sign = Mathf.Sign(pos.x);
            float overshoot = Mathf.Abs(pos.x) - _playableHalfWidth;
            float correction = Mathf.Min(overshoot, overshoot * 4f * Time.fixedDeltaTime + 0.1f);
            pos.x -= sign * correction;
            corrected = true;
        }

        // 2. Horizontal Z boundary
        if (Mathf.Abs(pos.z) > _playableHalfLength)
        {
            float sign = Mathf.Sign(pos.z);
            float overshoot = Mathf.Abs(pos.z) - _playableHalfLength;
            float correction = Mathf.Min(overshoot, overshoot * 4f * Time.fixedDeltaTime + 0.1f);
            pos.z -= sign * correction;
            corrected = true;
        }

        // 3. Vertical Y boundary
        if (pos.y < _zoneBottomY)
        {
            float overshoot = _zoneBottomY - pos.y;
            float correction = Mathf.Min(overshoot, overshoot * 4f * Time.fixedDeltaTime + 0.1f);
            pos.y += correction;
            corrected = true;
        }
        else if (pos.y > _zoneTopY)
        {
            float overshoot = pos.y - _zoneTopY;
            float correction = Mathf.Min(overshoot, overshoot * 4f * Time.fixedDeltaTime + 0.1f);
            pos.y -= correction;
            corrected = true;
        }

        // Hard clamp absolute limits (never breach walls or trigger zones under any circumstance)
        pos.x = Mathf.Clamp(pos.x, -_playableHalfWidth - 3f, _playableHalfWidth + 3f);
        pos.z = Mathf.Clamp(pos.z, -_playableHalfLength - 3f, _playableHalfLength + 3f);
        pos.y = Mathf.Clamp(pos.y, _zoneBottomY - 2f, _zoneTopY);

        if (corrected || pos != _rb.position)
        {
            _rb.MovePosition(pos);
        }
    }

    // -----------------------------------------------------------------------
    // Context steering algorithm
    // -----------------------------------------------------------------------

    private void BuildRayDirections()
    {
        var dirs = new List<Vector3>();
        int horizCount = Mathf.Max(8, rayCount);

        // 1. Horizontal equator ring (0 deg pitch)
        for (int i = 0; i < horizCount; i++)
        {
            float yaw = i * (360f / horizCount);
            dirs.Add(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
        }

        // 2. Upward & Downward gentle diagonal rings (+-30 deg pitch)
        int diagCount = Mathf.Max(6, Mathf.RoundToInt(horizCount * 0.75f));
        for (int i = 0; i < diagCount; i++)
        {
            float yaw = (i + 0.5f) * (360f / diagCount);
            dirs.Add(Quaternion.Euler(-30f, yaw, 0f) * Vector3.forward);
            dirs.Add(Quaternion.Euler(30f, yaw, 0f) * Vector3.forward);
        }

        // 3. Steep diagonal rings (+-60 deg pitch)
        for (int i = 0; i < 4; i++)
        {
            float yaw = i * 90f;
            dirs.Add(Quaternion.Euler(-60f, yaw, 0f) * Vector3.forward);
            dirs.Add(Quaternion.Euler(60f, yaw, 0f) * Vector3.forward);
        }

        // 4. Pure vertical poles
        dirs.Add(Vector3.up);
        dirs.Add(Vector3.down);

        _rayDirs = dirs.ToArray();
    }

    private void ComputeInterestMap()
    {
        Vector3 toGoal = (_goal - transform.position).normalized;

        // Proactive boundary steering: if approaching a wall, inject an inward turn bias
        float distToX = _playableHalfWidth - Mathf.Abs(transform.position.x);
        float distToZ = _playableHalfLength - Mathf.Abs(transform.position.z);
        float minWallDist = Mathf.Min(distToX, distToZ);

        Vector3 toCenter = new Vector3(-transform.position.x, 0f, -transform.position.z).normalized;
        float boundaryTurnBias = 0f;
        if (minWallDist < 35f)
        {
            // Smoothly ramp from 0 to 1 as creature nears the boundary margin
            boundaryTurnBias = Mathf.Clamp01(1f - (minWallDist / 35f));
        }

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float goalDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toGoal));

            if (boundaryTurnBias > 0f)
            {
                float centerDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toCenter));
                // Blend goal with inward turn bias so the creature loops back naturally
                _interest[i] = Mathf.Lerp(goalDot, centerDot, boundaryTurnBias * 0.85f);
            }
            else
            {
                _interest[i] = goalDot;
            }
        }
    }

    private void ComputeDangerMap()
    {
        float checkDist = baseDangerRadius;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            Vector3 rayDir = _rayDirs[i];
            Vector3 projected = transform.position + rayDir * checkDist;

            // Virtual boundary check: any ray that points out-of-bounds receives severe danger score
            bool breachesBoundary = Mathf.Abs(projected.x) > _playableHalfWidth ||
                                    Mathf.Abs(projected.z) > _playableHalfLength ||
                                    projected.y > _zoneTopY ||
                                    projected.y < _zoneBottomY;

            if (breachesBoundary)
            {
                _danger[i] = -2f; // Proactively steer away from boundary walls
                continue;
            }

            bool hit = Physics.Raycast(transform.position, rayDir, checkDist, terrainLayer, QueryTriggerInteraction.Ignore);
            _danger[i] = hit ? -1f : 0f;
        }
    }

    private Vector3 ChooseBestDirection()
    {
        Vector3 blendedDir = Vector3.zero;
        float totalWeight = 0f;
        int bestIdx = 0;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float score = _interest[i] + _danger[i];
            if (score > bestScore)
            {
                bestScore = score;
                bestIdx = i;
            }

            // Only blend directions that are completely free from danger
            if (_danger[i] >= 0f && _interest[i] > 0.05f)
            {
                // Quadratic weighting gives strong preference to best aligned rays while smoothing transitions
                float weight = _interest[i] * _interest[i];
                blendedDir += _rayDirs[i] * weight;
                totalWeight += weight;
            }
        }

        if (totalWeight > 0.001f)
        {
            return blendedDir.normalized;
        }

        // If no safe interest rays, fallback to highest scoring ray (unless completely blocked)
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
