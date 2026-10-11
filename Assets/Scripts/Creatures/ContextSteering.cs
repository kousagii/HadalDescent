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

    [Tooltip("Layer mask for solid obstacles and environment props (Terrain + Default).")]
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

    // Physical dimensions & obstacle evasion state
    private float   _bodyRadius = 0.8f;
    private float   _bodyLength = 2.0f;
    private float   _minSeabedClearance = 3.0f;
    private bool    _hasMeasuredBody = false;
    private Vector3 _closestHitNormal = Vector3.up;
    private bool    _hasObstacleNearby = false;

    // Playable scannable boundary limits (keeps creature strictly inside player reach)
    private float _playableHalfWidth  = 275f;
    private float _playableHalfLength = 275f;
    private float _zoneBottomY        = -182f;
    private float _zoneTopY           = -3.5f;
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
    public float SafeTopY
    {
        get
        {
            bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                    (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);
            return isSurfaceDrifter ? -0.2f : _zoneTopY;
        }
    }

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

        // Auto-configure terrain & obstacle layer mask (Terrain + Default)
        if (terrainLayer.value == 0)
        {
            int tMask = LayerMask.GetMask("Terrain", "Default");
            terrainLayer = tMask != 0 ? tMask : ~0;
        }

        MeasureBodyDimensions();
        BuildRayDirections();
        _interest = new float[_rayDirs.Length];
        _danger   = new float[_rayDirs.Length];

        InitializeZoneBounds();
    }

    private void MeasureBodyDimensions()
    {
        var cols = GetComponentsInChildren<Collider>();
        if (cols != null && cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++)
            {
                if (cols[i] != null) b.Encapsulate(cols[i].bounds);
            }
            _bodyRadius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.y), 0.4f, 4.0f);
            _bodyLength = Mathf.Max(1.0f, b.extents.z);
        }
        else
        {
            Vector3 s = transform.lossyScale;
            _bodyRadius = Mathf.Clamp(Mathf.Min(s.x, s.y) * 0.5f, 0.4f, 4.0f);
            _bodyLength = Mathf.Max(1.0f, s.z);
        }

        _minSeabedClearance = Mathf.Max(2.5f, _bodyRadius * 2.2f);
        _hasMeasuredBody = true;
    }

    private void FixedUpdate()
    {
        if (_goal == Vector3.zero) return;

        if (!_zoneBoundsInitialized)
            InitializeZoneBounds();

        if (!_hasMeasuredBody)
            MeasureBodyDimensions();

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

        // Proactive contact cushion: if creature hull is touching/penetrating an obstacle, smoothly push away
        float sphereRadius = Mathf.Clamp(_bodyRadius * 0.7f, 0.35f, 2.0f);
        if (Physics.SphereCast(transform.position, sphereRadius, transform.forward, out RaycastHit forwardHit, 0.7f, terrainLayer, QueryTriggerInteraction.Collide))
        {
            if (IsValidObstacle(forwardHit.collider))
            {
                Vector3 push = forwardHit.normal * Mathf.Max(0.2f, 0.7f - forwardHit.distance);
                _rb.MovePosition(_rb.position + push * (5f * Time.fixedDeltaTime));
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

            // Water surface is at Y = 0. Stay at least 3.5m submerged well below player surface boundary
            _zoneTopY = -3.5f;

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
                _zoneTopY    = tracker.ZoneTopY - 3.5f;
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
        Vector3 boundaryTurnNormal = Vector3.zero;

        // 1. Horizontal X boundary
        if (Mathf.Abs(pos.x) > _playableHalfWidth)
        {
            float sign = Mathf.Sign(pos.x);
            float overshoot = Mathf.Abs(pos.x) - _playableHalfWidth;
            float correction = Mathf.Min(overshoot, overshoot * 6f * Time.fixedDeltaTime + 0.2f);
            pos.x -= sign * correction;
            corrected = true;
            boundaryTurnNormal += new Vector3(-sign, 0f, 0f);
        }

        // 2. Horizontal Z boundary
        if (Mathf.Abs(pos.z) > _playableHalfLength)
        {
            float sign = Mathf.Sign(pos.z);
            float overshoot = Mathf.Abs(pos.z) - _playableHalfLength;
            float correction = Mathf.Min(overshoot, overshoot * 6f * Time.fixedDeltaTime + 0.2f);
            pos.z -= sign * correction;
            corrected = true;
            boundaryTurnNormal += new Vector3(0f, 0f, -sign);
        }

        // 3. Vertical Y boundary (Surface Drifters float at water surface Y = -0.45m; vertical bounds do not clamp them)
        bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);

        if (!isSurfaceDrifter)
        {
            if (pos.y < _zoneBottomY)
            {
                float overshoot = _zoneBottomY - pos.y;
                float correction = Mathf.Min(overshoot, overshoot * 6f * Time.fixedDeltaTime + 0.2f);
                pos.y += correction;
                corrected = true;
                boundaryTurnNormal += Vector3.up;
            }
            else if (pos.y > _zoneTopY)
            {
                float overshoot = pos.y - _zoneTopY;
                float correction = Mathf.Min(overshoot, overshoot * 6f * Time.fixedDeltaTime + 0.2f);
                pos.y -= correction;
                corrected = true;
                boundaryTurnNormal += Vector3.down;
            }

            pos.y = Mathf.Clamp(pos.y, _zoneBottomY, _zoneTopY);
        }

        // Hard clamp horizontal limits (never breach walls under any circumstance)
        pos.x = Mathf.Clamp(pos.x, -_playableHalfWidth, _playableHalfWidth);
        pos.z = Mathf.Clamp(pos.z, -_playableHalfLength, _playableHalfLength);

        if (corrected || pos != _rb.position)
        {
            _rb.MovePosition(pos);
            if (boundaryTurnNormal.sqrMagnitude > 0.01f)
            {
                var ai = GetComponent<SpeciesAI>();
                if (ai != null)
                {
                    ai.TurnAwayFromBoundary(boundaryTurnNormal.normalized);
                }
            }
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
            boundaryTurnBias = Mathf.Clamp01(1f - (minWallDist / 35f));
        }

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float goalDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toGoal));

            if (boundaryTurnBias > 0f)
            {
                float centerDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toCenter));
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
        if (!_hasMeasuredBody) MeasureBodyDimensions();

        // Lookahead dynamically scales with speed and creature body length (crucial for 2x specimens)
        float dynamicLookahead = _bodyLength + Mathf.Max(baseDangerRadius, moveSpeed * 2.2f);
        float sphereRadius = Mathf.Clamp(_bodyRadius * 0.7f, 0.35f, 2.0f);
        float castDist = Mathf.Max(2.0f, dynamicLookahead - sphereRadius);

        _hasObstacleNearby = false;
        _closestHitNormal = Vector3.up;
        float closestDist = float.MaxValue;

        // Downward seabed probe for swimming species (prevents skimming/clipping into seabed contours)
        bool isSwimmingSpecies = _locomotion == null ||
            (_locomotion.Archetype != LocomotionArchetype.BenthicFollower &&
             _locomotion.Archetype != LocomotionArchetype.Sessile &&
             _locomotion.Archetype != LocomotionArchetype.SurfaceDrifter);

        float floorDangerMultiplier = 0f;
        if (isSwimmingSpecies)
        {
            if (Physics.SphereCast(transform.position, sphereRadius, Vector3.down, out RaycastHit floorHit, _minSeabedClearance, terrainLayer, QueryTriggerInteraction.Collide))
            {
                if (IsValidObstacle(floorHit.collider))
                {
                    floorDangerMultiplier = 1f - Mathf.Clamp01(floorHit.distance / _minSeabedClearance);
                    if (floorHit.distance < closestDist)
                    {
                        closestDist = floorHit.distance;
                        _closestHitNormal = floorHit.normal;
                        _hasObstacleNearby = true;
                    }
                }
            }
        }

        // Inject upward lift into interest when swimming species approach seabed
        if (isSwimmingSpecies && floorDangerMultiplier > 0.05f)
        {
            for (int i = 0; i < _rayDirs.Length; i++)
            {
                if (_rayDirs[i].y > 0.15f)
                {
                    _interest[i] = Mathf.Max(_interest[i], _rayDirs[i].y * floorDangerMultiplier * 1.1f);
                }
            }
        }

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            Vector3 rayDir = _rayDirs[i];
            Vector3 projected = transform.position + rayDir * dynamicLookahead;

            // Virtual boundary check: any ray that points out-of-bounds receives severe danger score
            bool breachesBoundary = Mathf.Abs(projected.x) > _playableHalfWidth ||
                                    Mathf.Abs(projected.z) > _playableHalfLength ||
                                    projected.y > _zoneTopY ||
                                    projected.y < _zoneBottomY;

            if (breachesBoundary)
            {
                _danger[i] = 1.0f; // Maximum danger
                continue;
            }

            // Downward rays receive extra danger when near the seabed
            float floorPenalty = 0f;
            if (isSwimmingSpecies && floorDangerMultiplier > 0f && rayDir.y < -0.1f)
            {
                floorPenalty = (-rayDir.y) * floorDangerMultiplier;
            }

            // Volumetric SphereCast detects rocks, corals, props, and seabed contours on all sides
            if (Physics.SphereCast(transform.position, sphereRadius, rayDir, out RaycastHit hit, castDist, terrainLayer, QueryTriggerInteraction.Collide))
            {
                if (IsValidObstacle(hit.collider))
                {
                    float normDist = Mathf.Clamp01(hit.distance / castDist);
                    // Closer obstacles yield exponentially higher danger
                    float obstacleDanger = Mathf.Lerp(1.0f, 0.25f, normDist);
                    _danger[i] = Mathf.Clamp01(Mathf.Max(obstacleDanger, floorPenalty));

                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        _closestHitNormal = hit.normal;
                        _hasObstacleNearby = true;
                    }
                    continue;
                }
            }

            _danger[i] = Mathf.Clamp01(floorPenalty);
        }
    }

    private bool IsValidObstacle(Collider col)
    {
        if (col == null) return false;
        // Never collide with our own creature's colliders
        if (col.transform.root == transform.root) return false;
        // Ignore other creatures
        if (col.CompareTag("Species") || col.gameObject.layer == LayerMask.NameToLayer("Creatures")) return false;
        // Ignore zone boundary triggers
        if (col.CompareTag("Boundary")) return false;
        // Ignore player submarine (player avoidance is handled by shy-flee AI)
        if (col.CompareTag("Player")) return false;
        return true;
    }

    private Vector3 ChooseBestDirection()
    {
        Vector3 blendedDir = Vector3.zero;
        float totalWeight = 0f;

        float bestSafeScore = float.NegativeInfinity;
        int bestSafeIdx = -1;

        float lowestDanger = float.PositiveInfinity;
        int leastDangerousIdx = 0;

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float danger = Mathf.Clamp01(_danger[i]);
            float interest = Mathf.Clamp01(_interest[i]);

            if (danger < lowestDanger)
            {
                lowestDanger = danger;
                leastDangerousIdx = i;
            }

            // Ray is viable if danger is low enough to navigate
            if (danger < 0.40f)
            {
                float safeScore = interest * (1f - danger * 2.2f);
                if (safeScore > bestSafeScore)
                {
                    bestSafeScore = safeScore;
                    bestSafeIdx = i;
                }

                if (safeScore > 0.05f)
                {
                    float weight = safeScore * safeScore;
                    blendedDir += _rayDirs[i] * weight;
                    totalWeight += weight;
                }
            }
        }

        // 1. Safe blended trajectory toward goal
        if (totalWeight > 0.01f)
        {
            return blendedDir.normalized;
        }

        // 2. Best individual safe ray
        if (bestSafeIdx >= 0 && bestSafeScore > 0f)
        {
            return _rayDirs[bestSafeIdx];
        }

        // 3. OBSTACLE ESCAPE (Goal path is blocked):
        // Deflect along the surface normal of the obstacle or choose the least dangerous ray.
        // NEVER returns Vector3.zero!
        if (_hasObstacleNearby && _closestHitNormal.sqrMagnitude > 0.01f)
        {
            Vector3 deflectDir = Vector3.ProjectOnPlane(transform.forward, _closestHitNormal).normalized;
            if (deflectDir.sqrMagnitude < 0.01f)
                deflectDir = _closestHitNormal;

            Vector3 escapeDir = (deflectDir * 0.75f + _rayDirs[leastDangerousIdx] * 0.25f).normalized;
            return escapeDir;
        }

        return _rayDirs[leastDangerousIdx];
    }

    // -----------------------------------------------------------------------
    // Editor gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (_rayDirs == null) return;
        float checkDist = baseDangerRadius;
        for (int i = 0; i < _rayDirs.Length; i++)
        {
            bool hit = Physics.Raycast(transform.position, _rayDirs[i], checkDist, terrainLayer, QueryTriggerInteraction.Collide);
            Gizmos.color = hit ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, _rayDirs[i] * checkDist);
        }
        if (_goal != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_goal, 0.8f);
            Gizmos.DrawLine(transform.position, _goal);
        }
    }
}
