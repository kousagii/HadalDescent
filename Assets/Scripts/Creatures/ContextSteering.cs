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
    private float _zoneDepth          = 200f;
    private bool  _zoneBoundsInitialized = false;
    private float _nextBoundaryTurnTime  = 0f;

    // Continuous temporal filter on steering direction: eliminates 50Hz raycast noise and obstacle jitter
    private Vector3 _smoothedSteerDir        = Vector3.zero;
    private float   _smoothedSteerY          = 0f;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Set the world-space target position.</summary>
    public void SetGoal(Vector3 worldTarget) => _goal = worldTarget;

    /// <summary>Current world-space target position.</summary>
    public Vector3 Goal => _goal;

    /// <summary>Current move speed - raised during fleeing.</summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.01f, value);
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

    /// <summary>True model-facing forward vector in world coordinates, respecting modelYawOffset.</summary>
    public Vector3 TrueForward => modelYawOffset != 0f
        ? (transform.rotation * Quaternion.Euler(0f, -modelYawOffset, 0f) * Vector3.forward)
        : transform.forward;

    /// <summary>Safe horizontal boundary radius (half-width) for wander/flee clamping.</summary>
    public float SafeHalfWidth => _playableHalfWidth;

    /// <summary>Safe horizontal boundary radius (half-length) for wander/flee clamping.</summary>
    public float SafeHalfLength => _playableHalfLength;

    /// <summary>Safe top depth (stays below ocean surface and within species depth range).</summary>
    public float SafeTopY
    {
        get
        {
            bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                    (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);
            return isSurfaceDrifter ? -0.2f : GetEffectiveTopY();
        }
    }

    /// <summary>Safe bottom depth (stays safely above zone transition and within species depth range).</summary>
    public float SafeBottomY => GetEffectiveBottomY();

    public float GetEffectiveTopY()
    {
        bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);
        var aiData = GetComponent<SpeciesAI>()?.Data ?? _locomotion?.Data;
        if (aiData != null)
        {
            float D = _zoneDepth > 0f ? _zoneDepth : 200f;
            float sTop = -Mathf.Min(aiData.minDepthFraction, aiData.maxDepthFraction) * D;
            if (!isSurfaceDrifter)
            {
                sTop = Mathf.Min(sTop, -3.0f);
            }
            return Mathf.Min(_zoneTopY, sTop);
        }
        return isSurfaceDrifter ? _zoneTopY : Mathf.Min(_zoneTopY, -3.0f);
    }

    public float GetEffectiveBottomY()
    {
        var aiData = GetComponent<SpeciesAI>()?.Data ?? _locomotion?.Data;
        if (aiData != null)
        {
            float D = _zoneDepth > 0f ? _zoneDepth : 200f;
            float sBottom = -Mathf.Max(aiData.minDepthFraction, aiData.maxDepthFraction) * D;
            bool isBenthic = aiData.isStationary || aiData.locomotionArchetype == LocomotionArchetype.BenthicFollower || aiData.locomotionArchetype == LocomotionArchetype.HoverBurst;
            return isBenthic ? sBottom : Mathf.Max(_zoneBottomY, sBottom);
        }
        return _zoneBottomY;
    }

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

        // Auto-configure terrain & obstacle layer mask (Terrain + Default only)
        // Creatures are excluded from the terrain obstacle mask so species don't treat neighbors as solid terrain walls
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

        bool isHover = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.HoverBurst) ||
                       (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.HoverBurst);
        _minSeabedClearance = isHover ? Mathf.Max(0.35f, _bodyRadius * 1.0f) : Mathf.Max(2.5f, _bodyRadius * 2.2f);
        _hasMeasuredBody = true;
    }

    private void FixedUpdate()
    {
        if (!_zoneBoundsInitialized)
            InitializeZoneBounds();

        if (!_hasMeasuredBody)
            MeasureBodyDimensions();

        // Guaranteed player boundary enforcement (keeps creature inside scannable perimeter even if idle or drifting)
        EnforceZonePlayableBounds();

        if (_goal == Vector3.zero) return;

        ComputeInterestMap();
        ComputeDangerMap();

        Vector3 rawSteerDir = ChooseBestDirection();

        // 1. Strict pitch limit: creatures tilt up to a maximum of 30 degrees (maxSin30 = 0.5f)
        float maxSin30 = Mathf.Sin(30f * Mathf.Deg2Rad); // 0.5f
        if (Mathf.Abs(rawSteerDir.y) > maxSin30)
        {
            rawSteerDir.y = Mathf.Clamp(rawSteerDir.y, -maxSin30, maxSin30);
            rawSteerDir.Normalize();
        }

        // 2. Continuous low-pass temporal filter on 3D steer direction:
        // Exponential smoothing (Slerp) filters out high-frequency raycast noise (50Hz jitter)
        // while remaining 100% continuous and responsive every frame (eliminates laggy latch timers)
        if (_smoothedSteerDir.sqrMagnitude < 0.01f)
        {
            _smoothedSteerDir = rawSteerDir;
        }
        else
        {
            _smoothedSteerDir = Vector3.Slerp(_smoothedSteerDir, rawSteerDir, 5.0f * Time.fixedDeltaTime).normalized;
        }

        // 3. Gentle vertical pitch cadence: smooth vertical changes calmly so creatures never "bop" up and down
        float targetY = Mathf.Clamp(_smoothedSteerDir.y, -maxSin30, maxSin30);
        _smoothedSteerY = Mathf.Lerp(_smoothedSteerY, targetY, 3.2f * Time.fixedDeltaTime);

        Vector3 flatDir = new Vector3(_smoothedSteerDir.x, 0f, _smoothedSteerDir.z);
        Vector3 steerDir;
        if (flatDir.sqrMagnitude > 0.001f)
        {
            flatDir.Normalize();
            float horizFactor = Mathf.Sqrt(Mathf.Max(0.01f, 1f - _smoothedSteerY * _smoothedSteerY));
            steerDir = (flatDir * horizFactor + Vector3.up * _smoothedSteerY).normalized;
        }
        else
        {
            steerDir = _smoothedSteerDir;
        }

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

                Vector3 fallbackFlatDir = new Vector3(steerDir.x, 0f, steerDir.z);
                if (fallbackFlatDir.sqrMagnitude > 0.001f)
                {
                    float horizDist = Mathf.Sqrt(steerDir.x * steerDir.x + steerDir.z * steerDir.z);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(steerDir.y, horizDist) * Mathf.Rad2Deg, -30f, 30f);
                    Quaternion targetRot = Quaternion.LookRotation(fallbackFlatDir.normalized, Vector3.up) * Quaternion.Euler(pitch, modelYawOffset, 0f);
                    float angleDelta = Quaternion.Angle(_rb.rotation, targetRot);
                    float effectiveRate = Mathf.Min(turnSpeed, Mathf.Max(12f, angleDelta * 2.0f));
                    _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, effectiveRate * Time.fixedDeltaTime));
                }
            }
        }

        // Proactive contact cushion for fallback translation (only if CreatureLocomotion is absent)
        if (_locomotion == null)
        {
            float sphereRadius = Mathf.Clamp(_bodyRadius * 0.7f, 0.35f, 2.0f);
            if (Physics.SphereCast(transform.position, sphereRadius, transform.forward, out RaycastHit forwardHit, 0.7f, terrainLayer, QueryTriggerInteraction.Ignore))
            {
                if (IsValidObstacle(forwardHit.collider))
                {
                    Vector3 push = forwardHit.normal * Mathf.Max(0.2f, 0.7f - forwardHit.distance);
                    _rb.MovePosition(_rb.position + push * (5f * Time.fixedDeltaTime));
                }
            }
        }
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
            // 60m buffer inside perimeter wall keeps creatures at <= 240m, safely within player warning (275m) and walls (300m)
            const float wallMargin = 60f;
            _playableHalfWidth  = Mathf.Max(50f, (zone.playableWidth * 0.5f) - wallMargin);
            _playableHalfLength = Mathf.Max(50f, (zone.playableLength * 0.5f) - wallMargin);

            // Water surface is at Y = 0. Stay at least 3.5m submerged well below player surface boundary
            _zoneTopY = -3.5f;

            // Bottom trigger is at -zone.playableDepth. Keep at least 18m above it to avoid transition popups
            _zoneDepth = zone.playableDepth;
            _zoneBottomY = -zone.playableDepth + 18f;
            _zoneBoundsInitialized = true;
        }
        else
        {
            var tracker = FindFirstObjectByType<DepthTracker>();
            if (tracker != null)
            {
                _zoneDepth   = Mathf.Abs(tracker.ZoneBottomY - tracker.ZoneTopY);
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

        // 3. Vertical Y boundary (Surface Drifters float at water surface Y = -0.45m; Benthic followers are clamped to seabed)
        bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);
        bool isBenthic = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.BenthicFollower) ||
                         (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.BenthicFollower);

        bool isHoverBurst = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.HoverBurst) ||
                            (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.HoverBurst);

        float effTopY = GetEffectiveTopY();
        float effBottomY = GetEffectiveBottomY();

        if (!isSurfaceDrifter && !isBenthic && !isHoverBurst)
        {
            if (pos.y < effBottomY)
            {
                pos.y = effBottomY;
                corrected = true;
                boundaryTurnNormal += Vector3.up;
            }
            else if (pos.y > effTopY)
            {
                pos.y = effTopY;
                corrected = true;
                boundaryTurnNormal += Vector3.down;
            }
        }

        // Hard clamp horizontal limits (never breach walls under any circumstance)
        pos.x = Mathf.Clamp(pos.x, -_playableHalfWidth, _playableHalfWidth);
        pos.z = Mathf.Clamp(pos.z, -_playableHalfLength, _playableHalfLength);

        if (corrected)
        {
            if (_locomotion == null)
            {
                _rb.MovePosition(pos);
            }
            if (isSurfaceDrifter && _locomotion != null && boundaryTurnNormal.sqrMagnitude > 0.01f)
            {
                _locomotion.DeflectSurfaceDrifter(boundaryTurnNormal.normalized);
            }
            if (boundaryTurnNormal.sqrMagnitude > 0.01f && Time.time >= _nextBoundaryTurnTime)
            {
                _nextBoundaryTurnTime = Time.time + 1.5f;
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
        float distToGoal = Vector3.Distance(transform.position, _goal);
        float arrivalWeight = Mathf.Clamp01(distToGoal / 2.5f);
        Vector3 trueFacing = TrueForward;
        Vector3 smoothedGoalDir = Vector3.Lerp(trueFacing, toGoal, arrivalWeight).normalized;
        if (smoothedGoalDir.sqrMagnitude < 0.01f) smoothedGoalDir = trueFacing;

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

        bool isPelagicCruiser = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.PelagicCruiser) ||
                                (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser);

        Vector3 turnSide = Vector3.Cross(Vector3.up, trueFacing);
        float lateralSign = 1f;
        if (LastSteerDir.sqrMagnitude > 0.01f)
        {
            float sideProj = Vector3.Dot(LastSteerDir, turnSide);
            if (Mathf.Abs(sideProj) > 0.05f) lateralSign = Mathf.Sign(sideProj);
        }

        for (int i = 0; i < _rayDirs.Length; i++)
        {
            float goalDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], smoothedGoalDir));
            float fwdDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], trueFacing));

            if (boundaryTurnBias > 0f)
            {
                float centerDot = Mathf.Max(0f, Vector3.Dot(_rayDirs[i], toCenter));
                _interest[i] = Mathf.Lerp(goalDot, centerDot, boundaryTurnBias * 0.85f);
            }
            else
            {
                // Directional hysteresis / persistence: breaks left/right symmetry when facing obstacles
                _interest[i] = goalDot * 0.80f + fwdDot * 0.20f;
            }

            if (isPelagicCruiser)
            {
                float fwdAlignment = Vector3.Dot(_rayDirs[i], trueFacing);
                // Biomechanical forward-only propulsion:
                // Cetaceans & large pelagic cruisers cannot swim backwards.
                // Suppress interest for rays pointing backwards (fwdAlignment < 0.15f) so the agent
                // naturally executes a continuous forward turning arc (Dubins path) rather than attempting to reverse.
                if (fwdAlignment < 0.15f)
                {
                    float forwardPenalty = Mathf.Clamp01((fwdAlignment + 0.35f) / 0.50f);
                    _interest[i] *= forwardPenalty;
                }

                // If goal is behind the creature, break symmetry so it chooses a decisive, smooth lateral turning arc
                if (Vector3.Dot(trueFacing, smoothedGoalDir) < -0.2f)
                {
                    float sideDot = Vector3.Dot(_rayDirs[i], turnSide * lateralSign);
                    if (sideDot > 0f)
                    {
                        _interest[i] += sideDot * 0.25f;
                    }
                }
            }
        }
    }

    private void ComputeDangerMap()
    {
        if (!_hasMeasuredBody) MeasureBodyDimensions();

        // Dynamic lookahead capped to prevent sensor ray saturation in shallow zones / near boundaries
        float dynamicLookahead = Mathf.Clamp(_bodyLength * 0.5f + Mathf.Max(baseDangerRadius, moveSpeed * 1.5f), 2.5f, 9.0f);
        float sphereRadius = Mathf.Clamp(_bodyRadius * 0.7f, 0.35f, 2.0f);
        float castDist = Mathf.Max(2.0f, dynamicLookahead - sphereRadius);

        _hasObstacleNearby = false;
        _closestHitNormal = Vector3.up;
        float closestDist = float.MaxValue;

        // Downward seabed probe for swimming species (prevents skimming/clipping into seabed contours)
        bool isSurfaceDrifter = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.SurfaceDrifter) ||
                                (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter);
        bool isBenthic = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.BenthicFollower) ||
                         (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.BenthicFollower);
        bool isHoverBurst = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.HoverBurst) ||
                            (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.HoverBurst);

        float effTopY = GetEffectiveTopY();
        float effBottomY = GetEffectiveBottomY();

        bool isSwimmingSpecies = !isBenthic &&
            (_locomotion == null ||
             (_locomotion.Archetype != LocomotionArchetype.Sessile &&
              _locomotion.Archetype != LocomotionArchetype.SurfaceDrifter));

        float floorDangerMultiplier = 0f;
        // Large pelagic/swimming species avoid skimming seabed; HoverBurst (clownfish) hover intimately over sea anemones without floor repulsion
        if (isSwimmingSpecies && !isHoverBurst)
        {
            if (Physics.SphereCast(transform.position, sphereRadius, Vector3.down, out RaycastHit floorHit, _minSeabedClearance, terrainLayer, QueryTriggerInteraction.Ignore))
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
        if (isSwimmingSpecies && !isHoverBurst && floorDangerMultiplier > 0.05f)
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
                                    (!isSurfaceDrifter && projected.y > effTopY) ||
                                    (!isBenthic && projected.y < effBottomY);

            if (!breachesBoundary && isBenthic)
            {
                var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
                if (tg != null && tg.HasGenerated)
                {
                    float pFloorY = tg.SampleHeight(projected.x, projected.z);
                    if (pFloorY > effTopY + 1.0f || pFloorY < effBottomY - 1.0f)
                    {
                        breachesBoundary = true;
                    }
                }
            }

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

            // Volumetric SphereCast detects rocks, corals, props, and seabed contours on all sides.
            // For benthic creatures, elevate origin slightly so the cast glides above flat sand without self-intersecting ground.
            Vector3 castOrigin = isBenthic ? transform.position + Vector3.up * Mathf.Max(0.35f, sphereRadius * 0.75f) : transform.position;

            if (Physics.SphereCast(castOrigin, sphereRadius, rayDir, out RaycastHit hit, castDist, terrainLayer, QueryTriggerInteraction.Ignore))
            {
                if (IsValidObstacle(hit.collider, isBenthic, hit))
                {
                    float normDist = Mathf.Clamp01(hit.distance / castDist);
                    // Closer obstacles yield higher danger, smoothly decaying with distance
                    float obstacleDanger = Mathf.Lerp(0.85f, 0.05f, normDist);

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

    /// <summary>
    /// Checks whether a collider or its parent hierarchy belongs to an Environment prop, rock, coral, or landmark.
    /// </summary>
    public static bool IsEnvironmentProp(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Environment")) return true;
        if (col.transform.root != null && col.transform.root.CompareTag("Environment")) return true;
        Transform t = col.transform.parent;
        while (t != null)
        {
            if (t.CompareTag("Environment")) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Checks whether a GameObject or its parent hierarchy belongs to an Environment prop, rock, coral, or landmark.
    /// </summary>
    public static bool IsEnvironmentProp(GameObject go)
    {
        if (go == null) return false;
        if (go.CompareTag("Environment")) return true;
        if (go.transform.root != null && go.transform.root.CompareTag("Environment")) return true;
        Transform t = go.transform.parent;
        while (t != null)
        {
            if (t.CompareTag("Environment")) return true;
            t = t.parent;
        }
        return false;
    }

    private bool IsValidObstacle(Collider col, bool isBenthic = false, RaycastHit hit = default)
    {
        if (col == null) return false;
        // Never collide with trigger colliders (seagrass meadows, scan triggers, fact volumes)
        if (col.isTrigger) return false;
        // Never collide with our own creature's colliders
        if (col.transform.root == transform.root) return false;
        // Ignore other creatures — creature-to-creature interaction is handled organically by AI;
        // creatures must never be treated as solid navigation obstacles in ContextSteering!
        if (col.CompareTag("Species") || col.gameObject.layer == LayerMask.NameToLayer("Creatures"))
            return false;
        // Ignore zone boundary triggers
        if (col.CompareTag("Boundary")) return false;
        // Ignore player submarine (player avoidance is handled by shy-flee AI)
        if (col.CompareTag("Player")) return false;

        // Benthic creatures (rays, snails, lobsters) traverse low environment props (seagrass, clams <= 0.7m)
        // while steering smoothly around tall solid boulders/corals (> 0.7m) laterally along the seabed
        if (isBenthic)
        {
            if (IsEnvironmentProp(col))
            {
                // Low props (seagrass, small clams, flat coral plates <= 0.7m) are traversable on the seabed
                if (col.bounds.size.y <= 0.7f) return false;
                // Tall boulders, massive coral pillars (> 0.7m) should be steered around laterally
                return true;
            }
            if (hit.collider != null && hit.normal.y >= 0.25f) return false;
            return true;
        }

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

            // Continuous weighting: smoothly downweight directions with higher danger rather than binary disqualification
            if (danger < 0.80f)
            {
                float clearance = 1f - danger;
                float safeScore = interest * clearance * clearance;
                if (safeScore > bestSafeScore)
                {
                    bestSafeScore = safeScore;
                    bestSafeIdx = i;
                }

                if (safeScore > 0.02f)
                {
                    float weight = safeScore * safeScore;
                    blendedDir += _rayDirs[i] * weight;
                    totalWeight += weight;
                }
            }
        }

        // 1. Safe blended trajectory toward goal
        if (totalWeight > 0.005f)
        {
            return blendedDir.normalized;
        }

        // 2. Best individual safe ray
        if (bestSafeIdx >= 0 && bestSafeScore > 0f)
        {
            return _rayDirs[bestSafeIdx];
        }

        // 3. OBSTACLE ESCAPE (Only when truly trapped, lowestDanger >= 0.80f):
        // Deflect along the surface normal or lateral tangent of the obstacle.
        if (_hasObstacleNearby && _closestHitNormal.sqrMagnitude > 0.01f)
        {
            Vector3 forward = TrueForward;
            Vector3 deflectDir = Vector3.ProjectOnPlane(forward, _closestHitNormal);
            if (deflectDir.sqrMagnitude < 0.05f)
            {
                Vector3 sideTangent = Vector3.Cross(Vector3.up, _closestHitNormal);
                if (sideTangent.sqrMagnitude < 0.01f)
                    sideTangent = Vector3.Cross(forward, Vector3.up);
                sideTangent.Normalize();

                Vector3 toGoal = (_goal - transform.position).normalized;
                if (Vector3.Dot(sideTangent, toGoal) < 0f)
                    sideTangent = -sideTangent;

                deflectDir = (sideTangent * 0.8f + _closestHitNormal * 0.4f).normalized;
            }

            // Steer horizontally around obstacles rather than pitching straight up into the water column
            deflectDir.y = 0f;
            if (deflectDir.sqrMagnitude > 0.01f) deflectDir.Normalize();

            Vector3 escapeDir = (deflectDir * 0.75f + _rayDirs[leastDangerousIdx] * 0.25f).normalized;

            bool isPelagicCruiser = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.PelagicCruiser) ||
                                    (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser);
            if (isPelagicCruiser && Vector3.Dot(escapeDir, forward) < 0.1f)
            {
                // Cetaceans & cruisers cannot swim backwards: project tangentially forward along obstacle
                Vector3 sideTangent = Vector3.Cross(Vector3.up, _closestHitNormal);
                if (sideTangent.sqrMagnitude < 0.01f) sideTangent = Vector3.Cross(forward, Vector3.up);
                if (Vector3.Dot(sideTangent, forward) < 0f) sideTangent = -sideTangent;
                escapeDir = (sideTangent * 0.85f + forward * 0.15f).normalized;
            }

            return escapeDir;
        }

        bool isCruiser = (_locomotion != null && _locomotion.Archetype == LocomotionArchetype.PelagicCruiser) ||
                         (GetComponent<SpeciesAI>()?.Data != null && GetComponent<SpeciesAI>().Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser);
        if (isCruiser)
        {
            Vector3 forward = TrueForward;
            if (Vector3.Dot(_rayDirs[leastDangerousIdx], forward) < 0.1f)
            {
                // If least dangerous ray points backwards, steer tangentially forward to preserve momentum
                Vector3 sideTangent = Vector3.Cross(Vector3.up, forward);
                Vector3 turnDir = (sideTangent * 0.85f + forward * 0.15f).normalized;
                return turnDir;
            }
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
