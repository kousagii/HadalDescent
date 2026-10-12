using UnityEngine;

/// <summary>
/// Biomechanical locomotion controller for marine species.
/// Translates ContextSteering's spatial decisions into authentic biological propulsion
/// based on the creature's LocomotionArchetype.
///
/// Archetypes supported:
///   1. PelagicCruiser  - Sharks, Dolphins (continuous forward momentum, banking into turns)
///   2. HoverBurst      - Clownfish (micro-hovering station holding + rapid explosive darts)
///   3. PulsatileJetter - Jellyfish, Squid (periodic impulse contraction + viscous glide, mesh pulsation)
///   4. BenthicFollower - Rays, Lobsters, Snails (ground-clamped, terrain normal alignment, backward tail-flip on flee)
///   5. SurfaceDrifter  - Bluebottles (water-surface clamp, passive swell & current advection)
///   6. Serpentine      - Sea Kraits (continuous lateral sinusoidal yaw undulation)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ContextSteering))]
public class CreatureLocomotion : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // References
    // -----------------------------------------------------------------------

    private Rigidbody       _rb;
    private ContextSteering _steering;
    private SpeciesAI       _ai;
    private SpeciesData     _data;
    private Animator        _animator;

    // -----------------------------------------------------------------------
    // Visual transforms for procedural deformation
    // -----------------------------------------------------------------------

    private Transform _visualModel;
    private Vector3   _baseModelScale = Vector3.one;

    // -----------------------------------------------------------------------
    // Runtime state
    // -----------------------------------------------------------------------

    private float _cycleTimer;
    private float _burstTimer;
    private bool  _isBursting;
    private float _currentSpeed;
    private float _currentBankAngle;
    private float _previousYaw;

    public LocomotionArchetype Archetype => _data != null ? _data.locomotionArchetype : LocomotionArchetype.PelagicCruiser;
    public SpeciesData Data => _data;

    // Per-instance random phase offsets for animation desync
    private float _hoverPhaseX;
    private float _hoverPhaseY;
    private float _hoverPhaseZ;
    private Vector3 _smoothedHoverDir = Vector3.forward;

    // Benthic state
    private Vector3 _groundNormal = Vector3.up;
    private float   _groundY = -200f;
    private bool    _hasGroundHit;
    private float   _bottomToPivot = 0f;
    private float   _bodyRadius = 1.0f;
    private float   _bodyLength = 2.0f;
    private float   _benthicGlideLift = 0f;
    private float   _prevBenthicLift = 0f;
    private float   _benthicLiftHoldTimer = 0f;
    private bool    _groundInitialized = false;
    private Vector3 _smoothedSurfaceTangent = Vector3.forward;
    private Vector3 _smoothedSlopeForward = Vector3.forward;
    private bool    _hasInitializedSlope = false;

    // Surface drifter state
    private Vector3 _ambientCurrent = new Vector3(0.35f, 0f, 0.2f);
    private float   _surfaceDriftYaw;
    private float   _nextBoundaryTurnCooldown;

    // -----------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _steering = GetComponent<ContextSteering>();
        _ai       = GetComponent<SpeciesAI>();
        _animator = GetComponentInChildren<Animator>();

        // Cache best visual renderer transform for procedural deformation
        var smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            _visualModel = smr.transform;
        }
        else
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null)
                _visualModel = mr.transform;
            else if (transform.childCount > 0)
                _visualModel = transform.GetChild(0);
            else
                _visualModel = transform;
        }
        _baseModelScale = _visualModel.localScale;

        _previousYaw = transform.eulerAngles.y;
        _surfaceDriftYaw = transform.eulerAngles.y;

        if (_rb != null)
        {
            _rb.useGravity     = false;
            _rb.freezeRotation = true;
            _rb.isKinematic    = false;
            _rb.linearDamping  = 2f;
            _rb.interpolation  = RigidbodyInterpolation.Interpolate;
        }

        MeasureBodyDimensions();
    }

    private void MeasureBodyDimensions()
    {
        var rends = GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null) b.Encapsulate(rends[i].bounds);
            }
            _bottomToPivot = Mathf.Max(0.4f, transform.position.y - b.min.y);
            _bodyRadius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.y), 0.4f, 4.0f);
            _bodyLength = Mathf.Max(1.0f, b.extents.z);
        }
        else
        {
            Vector3 s = transform.lossyScale;
            _bodyRadius = Mathf.Clamp(Mathf.Max(s.x, s.y) * 0.5f, 0.4f, 4.0f);
            _bodyLength = Mathf.Max(1.0f, s.z);
            _bottomToPivot = _bodyRadius;
        }
    }

    public void Initialize(SpeciesData data)
    {
        _data = data;
        MeasureBodyDimensions();
        _cycleTimer = Random.Range(0f, 20f); // wide desync window across individuals

        if (_data != null && _data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter)
        {
            float driftAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _ambientCurrent = new Vector3(Mathf.Sin(driftAngle), 0f, Mathf.Cos(driftAngle)) * 0.35f;
            _surfaceDriftYaw = Mathf.Atan2(_ambientCurrent.x, _ambientCurrent.z) * Mathf.Rad2Deg;
            EnhanceSurfaceDrifterVisuals();
        }

        // Per-instance random phase offsets for hover wobble desync
        _hoverPhaseX = Random.Range(0f, Mathf.PI * 2f);
        _hoverPhaseY = Random.Range(0f, Mathf.PI * 2f);
        _hoverPhaseZ = Random.Range(0f, Mathf.PI * 2f);

        if (_visualModel != null)
        {
            _baseModelScale = _visualModel.localScale;
        }

        // Desynchronize skeletal animation playback across individuals
        // Each creature starts at a random point in its animation clip with a
        // subtle speed variation so they drift further apart over time.
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            // Wait one frame for the state machine to initialize, then offset
            StartCoroutine(DesyncAnimatorCoroutine());
        }
    }

    private System.Collections.IEnumerator DesyncAnimatorCoroutine()
    {
        yield return null; // let Animator.Start() execute first

        if (_animator != null)
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            float randomOffset = Random.Range(0f, 1f);
            _animator.Play(stateInfo.fullPathHash, 0, randomOffset);

            // Natural biological animation cadence based on individual size:
            // smaller juveniles beat fins/tails slightly faster; giant elders beat with slower, majestic sweeps
            float scaleRatio = 1f;
            if (_data != null && _data.modelPrefab != null && _data.modelPrefab.transform.localScale.magnitude > 0.01f)
            {
                scaleRatio = transform.localScale.magnitude / _data.modelPrefab.transform.localScale.magnitude;
            }
            float cadenceFactor = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Clamp(scaleRatio, 0.4f, 2.5f)), 0.75f, 1.35f);
            _animator.speed *= cadenceFactor * Random.Range(0.94f, 1.06f);
        }
    }

    // -----------------------------------------------------------------------
    // Physics loop
    // -----------------------------------------------------------------------

    private void FixedUpdate()
    {
        if (_data == null)
        {
            if (_ai != null && _ai.Data != null)
                _data = _ai.Data;
            else
                return;
        }

        Vector3 steerDir = _steering != null ? _steering.LastSteerDir : Vector3.zero;
        float targetSpeed = _steering != null ? _steering.MoveSpeed : _data.moveSpeed;

        _cycleTimer += Time.fixedDeltaTime;

        switch (_data.locomotionArchetype)
        {
            case LocomotionArchetype.PelagicCruiser:
                UpdatePelagicCruiser(steerDir, targetSpeed);
                break;

            case LocomotionArchetype.HoverBurst:
                UpdateHoverBurst(steerDir, targetSpeed);
                break;

            case LocomotionArchetype.PulsatileJetter:
                UpdatePulsatileJetter(steerDir, targetSpeed);
                break;

            case LocomotionArchetype.BenthicFollower:
                UpdateBenthicFollower(steerDir, targetSpeed);
                break;

            case LocomotionArchetype.SurfaceDrifter:
                UpdateSurfaceDrifter();
                break;

            case LocomotionArchetype.Serpentine:
                UpdateSerpentine(steerDir, targetSpeed);
                break;

            case LocomotionArchetype.Sessile:
                // Sessile fauna remain stationary
                break;

            default:
                UpdatePelagicCruiser(steerDir, targetSpeed);
                break;
        }

        UpdateBankingAndYaw();
        EnforcePlayableAndDepthBounds();
    }

    // -----------------------------------------------------------------------
    // Archetype 1: Pelagic Cruiser (Sharks, Dolphins)
    // Continuous forward glide, never stops, banks smoothly into turns
    // -----------------------------------------------------------------------

    private void UpdatePelagicCruiser(Vector3 steerDir, float targetSpeed)
    {
        // Enforce minimum cruising speed so pelagic predators never stall
        float minSpeed = targetSpeed * Mathf.Max(0.25f, _data.minCruiseSpeedFraction);
        if (steerDir.sqrMagnitude < 0.01f)
        {
            steerDir = transform.forward;
        }

        _currentSpeed = Mathf.Lerp(_currentSpeed, Mathf.Max(minSpeed, targetSpeed), 2.0f * Time.fixedDeltaTime);

        // Biomechanical hydrodynamic forward propulsion:
        // Marine vertebrates generate thrust strictly along their longitudinal body axis (transform.forward).
        // Propelling strictly forward guarantees dolphins and sharks will NEVER slide sideways or swim backward!
        float forwardDot = Vector3.Dot(transform.forward, steerDir.normalized);
        float turnDrag = Mathf.Clamp01((forwardDot + 1f) * 0.5f); // 1.0 when facing target, ~0.5 at 90 deg, ~0 at 180 deg
        float forwardSpeed = Mathf.Max(minSpeed * 0.45f, _currentSpeed * Mathf.Lerp(0.40f, 1.0f, turnDrag));

        Vector3 moveStep = transform.forward * forwardSpeed * Time.fixedDeltaTime;
        Vector3 newPos = _rb.position + moveStep;

        // Boundaries and depth safety
        float safeHalfW = _steering != null ? _steering.SafeHalfWidth : 240f;
        float safeHalfL = _steering != null ? _steering.SafeHalfLength : 240f;
        newPos.x = Mathf.Clamp(newPos.x, -safeHalfW, safeHalfW);
        newPos.z = Mathf.Clamp(newPos.z, -safeHalfL, safeHalfL);

        float effTopY = _steering != null ? _steering.SafeTopY : -1.8f;
        effTopY = Mathf.Min(effTopY, -1.8f);
        float effBottomY = _steering != null ? _steering.SafeBottomY : -180f;

        var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
        float floorAtPos = tg != null && tg.HasGenerated ? tg.SampleHeight(newPos.x, newPos.z) : -200f;
        float safeClearance = Mathf.Max(2.5f, _bottomToPivot + 1.0f);
        float minSwimY = Mathf.Max(effBottomY, floorAtPos + safeClearance);

        // In very shallow water where seabed rises near surface, stay halfway between seabed and surface
        if (minSwimY > effTopY)
        {
            newPos.y = Mathf.Lerp(floorAtPos + 0.8f, effTopY, 0.5f);
        }
        else
        {
            newPos.y = Mathf.Clamp(newPos.y, minSwimY, effTopY);
        }

        // Hydrodynamic velocity sanitization:
        // Ensure dynamic physics collisions or external contacts can NEVER impart a backward velocity
        float backwardDot = Vector3.Dot(_rb.linearVelocity, transform.forward);
        if (backwardDot < 0f)
        {
            _rb.linearVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.forward);
        }

        // Exactly ONCE per FixedUpdate: apply unified position (eliminates jitter from multiple calls)
        _rb.MovePosition(newPos);

        // Smooth heading rotation towards steerDir (horizontal when not going upward)
        float turnBoost = forwardDot < 0.5f ? 1.6f : 1.0f;
        ApplySteeringRotation(steerDir, 0f, full3D: false, maxPitch: 25f, turnBoost: turnBoost);

        // Synchronize skeletal swimming animation speed with forward velocity
        if (_animator != null)
        {
            float targetAnimSpeed = Mathf.Clamp(forwardSpeed / Mathf.Max(0.8f, targetSpeed), 0.4f, 1.25f);
            _animator.speed = Mathf.Lerp(_animator.speed, targetAnimSpeed, 3f * Time.fixedDeltaTime);
        }
    }

    // -----------------------------------------------------------------------
    // Archetype 2: Hover & Burst (Clownfish)
    // Micro-hovering station holding with rapid, nervous C-start dashes
    // -----------------------------------------------------------------------

    private void UpdateHoverBurst(Vector3 steerDir, float targetSpeed)
    {
        _burstTimer += Time.fixedDeltaTime;
        float burstInterval = Mathf.Max(0.9f, 1f / Mathf.Max(0.1f, _data.pulseFrequency));

        float steerMagnitude = steerDir.magnitude;

        if (targetSpeed < 0.1f)
        {
            // Soft deceleration if explicitly ordered to rest
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, 4f * Time.fixedDeltaTime);
            if (_currentSpeed < 0.01f) _currentSpeed = 0f;
            _isBursting = false;
        }
        else
        {
            if (_burstTimer >= burstInterval)
            {
                _burstTimer = 0f;
                _isBursting = true;
            }

            if (_isBursting)
            {
                // Rapid energetic fin flutter burst toward wander target
                _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed * 1.55f, 9f * Time.fixedDeltaTime);
                if (_burstTimer > 0.35f)
                {
                    _isBursting = false;
                }
            }
            else
            {
                // Active smooth cruising toward wander goal
                _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed * 0.92f, 4.5f * Time.fixedDeltaTime);
            }
        }

        // Temporal low-pass filter on heading direction
        if (_smoothedHoverDir.sqrMagnitude < 0.01f)
        {
            _smoothedHoverDir = transform.forward;
        }
        if (steerMagnitude > 0.05f)
        {
            _smoothedHoverDir = Vector3.Slerp(_smoothedHoverDir, steerDir.normalized, 6f * Time.fixedDeltaTime).normalized;
        }

        // Gentle, serene breathing float (soft 1.2Hz organic drift without high-frequency shaking)
        Vector3 hoverFloat = new Vector3(
            Mathf.Sin(_cycleTimer * 1.5f + _hoverPhaseX) * 0.02f,
            Mathf.Cos(_cycleTimer * 1.2f + _hoverPhaseY) * 0.025f,
            0f);

        Vector3 moveStep = (_smoothedHoverDir * _currentSpeed + hoverFloat) * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        // Smooth heading alignment: maximum 25 degrees pitch clamp keeps clownfish level and upright
        if (_currentSpeed > 0.08f)
            ApplySteeringRotation(_smoothedHoverDir, 0f, full3D: false, maxPitch: 25f);
    }

    // -----------------------------------------------------------------------
    // Archetype 3: Pulsatile Jetter (Jellyfish, Squids)
    // Periodic contraction impulse followed by hydrodynamic drag glide
    // -----------------------------------------------------------------------

    private void UpdatePulsatileJetter(Vector3 steerDir, float targetSpeed)
    {
        // 1. Natural propulsion pulse cycle (scaled subtly by individual size)
        float scaleRatio = 1f;
        if (_data != null && _data.modelPrefab != null && _data.modelPrefab.transform.localScale.magnitude > 0.01f)
        {
            scaleRatio = transform.localScale.magnitude / _data.modelPrefab.transform.localScale.magnitude;
        }
        float cadenceFactor = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Clamp(scaleRatio, 0.4f, 2.5f)), 0.75f, 1.35f);
        float freq = Mathf.Max(0.2f, _data.pulseFrequency * cadenceFactor);
        float period = 1f / freq;
        float tau = _cycleTimer % period;
        float normalizedCycle = tau / period; // 0.0 to 1.0
        float duty = Mathf.Clamp(_data.pulseDutyCycle, 0.2f, 0.55f);

        // Smooth continuous thrust impulse & visual pulse
        float impulse = 0f;
        float visualDeform = 0f;

        if (normalizedCycle < duty)
        {
            // Power stroke / contraction phase: smooth squared-sine curve (C1 continuous, zero jerk)
            float u = normalizedCycle / duty;
            float sinU = Mathf.Sin(u * Mathf.PI);
            impulse = sinU * sinU;
            visualDeform = impulse;
        }
        else
        {
            // Recovery stroke / glide phase: gentle damped elastic recoil
            float v = (normalizedCycle - duty) / (1f - duty);
            impulse = 0f;
            visualDeform = -Mathf.Sin(v * Mathf.PI) * 0.25f * Mathf.Exp(-2f * v);
        }

        // 2. Smooth velocity response
        // Accelerate smoothly during thrust; coast with hydrodynamic momentum during glide
        float peakSpeed = targetSpeed * 1.55f;
        float minGlideSpeed = targetSpeed * 0.3f;
        float desiredSpeed = Mathf.Lerp(minGlideSpeed, peakSpeed, impulse);
        _currentSpeed = Mathf.Lerp(_currentSpeed, desiredSpeed, 5f * Time.fixedDeltaTime);

        // 3. Direction & 3D orientation
        bool isSquid = _data.taxonomicClass == TaxonomicClass.Cephalopoda;
        bool isJellyfish = _data.taxonomicClass == TaxonomicClass.Scyphozoa;

        // Jellyfish have natural upward buoyancy & gentle drifting bias when cruising
        Vector3 effectiveDir = steerDir;
        if (isJellyfish)
        {
            // If approaching or hitting the top surface boundary (Y >= -4.0m), invert upward buoyancy
            // so the jellyfish turns and dives downward instead of breaching the surface boundary!
            if (transform.position.y >= -4.0f)
            {
                effectiveDir = (effectiveDir.normalized * 0.65f + Vector3.down * 0.45f).normalized;
            }
            else if (effectiveDir.sqrMagnitude < 0.01f)
            {
                effectiveDir = (Vector3.up * 0.55f + transform.forward * 0.25f).normalized;
            }
            else
            {
                // Blend steering direction with gentle upward buoyancy
                effectiveDir = (effectiveDir.normalized * 0.75f + Vector3.up * 0.25f).normalized;
            }
        }
        else if (effectiveDir.sqrMagnitude < 0.01f)
        {
            effectiveDir = transform.forward;
        }

        // 4. Translation step
        Vector3 moveStep = effectiveDir.normalized * _currentSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        // 5. 3D Rotation to face movement direction
        if (effectiveDir.sqrMagnitude > 0.001f)
        {
            if (isSquid)
            {
                // Squids orient directly along their 3D movement vector (full pitch & yaw)
                ApplySteeringRotation(effectiveDir, 0f, full3D: true);
            }
            else if (isJellyfish)
            {
                // Jellyfish tilt smoothly in their travel direction (up to 30 deg tilt)
                ApplySteeringRotation(effectiveDir, 0f, full3D: false, maxPitch: 30f);
            }
            else
            {
                ApplySteeringRotation(effectiveDir, 0f, full3D: false, maxPitch: 30f);
            }
        }

        // 6. Subtle, organic visual model pulsation (only applied when no skeletal animator is active)
        if (_visualModel != null)
        {
            float deformMagnitude = _animator != null ? 0f : 0.065f;
            float squash = 1f - visualDeform * deformMagnitude;
            float stretch = 1f + visualDeform * (deformMagnitude * 1.15f);

            Vector3 targetScale = new Vector3(_baseModelScale.x * squash, _baseModelScale.y * stretch, _baseModelScale.z * squash);
            _visualModel.localScale = Vector3.Lerp(_visualModel.localScale, targetScale, 6f * Time.fixedDeltaTime);
        }

        // 7. Harmonize animator playback speed with pulse rate
        if (_animator != null)
        {
            float targetAnimSpeed = Mathf.Clamp(freq / 0.8f, 0.5f, 1.6f);
            _animator.speed = Mathf.Lerp(_animator.speed, targetAnimSpeed, 3f * Time.fixedDeltaTime);
        }
    }

    // -----------------------------------------------------------------------
    // Archetype 4: Benthic Follower (Rays, Lobsters, Snails, Starfish)
    // Ground-clamped to ocean floor contours; backward tail-flip on flee
    // -----------------------------------------------------------------------

    private void UpdateBenthicFollower(Vector3 steerDir, float targetSpeed)
    {
        // 1. Sample ground beneath creature with temporal filtering
        SampleGround();

        float yawOffset = _steering != null ? _steering.ModelYawOffset : 0f;

        // 2. Check for crustacean backward tail-flip escape
        bool isFleeing = _ai != null && _ai.IsFleeing;
        bool isLobster = _data != null && _data.taxonomicClass == TaxonomicClass.Malacostraca;

        Quaternion curRot = _rb != null ? _rb.rotation : transform.rotation;
        Vector3 effectiveDir = steerDir;
        if (isLobster && isFleeing)
        {
            // Backward tail-flip: propel backward relative to current true facing
            Vector3 fleeBack = yawOffset != 0f
                ? -(curRot * Quaternion.Euler(0f, -yawOffset, 0f) * Vector3.forward)
                : -(curRot * Vector3.forward);
            effectiveDir = fleeBack;
            targetSpeed = Mathf.Clamp(targetSpeed, 1.4f, 2.0f);
        }

        Vector3 trueFacing = yawOffset != 0f
            ? (curRot * Quaternion.Euler(0f, -yawOffset, 0f) * Vector3.forward)
            : curRot * Vector3.forward;
        Vector3 flatFacing = Vector3.ProjectOnPlane(trueFacing, _groundNormal).normalized;
        if (flatFacing.sqrMagnitude < 0.001f) flatFacing = transform.forward;

        // 3. Project steering vector onto terrain tangent plane
        Vector3 rawTangent = Vector3.ProjectOnPlane(effectiveDir, _groundNormal).normalized;
        if (rawTangent.sqrMagnitude < 0.001f)
            rawTangent = flatFacing;

        Vector3 targetTangent = rawTangent;

        float scaleFactorY = 1.0f;
        if (_data != null && _data.modelPrefab != null && _data.modelPrefab.transform.localScale.y > 0.001f)
        {
            scaleFactorY = transform.localScale.y / _data.modelPrefab.transform.localScale.y;
        }
        else if (_data != null && _data.placeholderScale.y > 0.001f)
        {
            scaleFactorY = transform.localScale.y / _data.placeholderScale.y;
        }
        else
        {
            scaleFactorY = transform.localScale.y;
        }
        float effectiveOffset = _bottomToPivot + Mathf.Max(0.06f, (_data != null ? _data.benthicSurfaceOffset : 0.1f) * Mathf.Clamp(scaleFactorY, 0.5f, 1.5f));
        float baseTargetY = _groundY + effectiveOffset;

        bool isRay = _data != null && (_data.taxonomicClass == TaxonomicClass.Chondrichthyes || (_data.commonName != null && _data.commonName.IndexOf("ray", System.StringComparison.OrdinalIgnoreCase) >= 0));

        // Proactive coral/prop evasion & upward glide lift for benthic crawlers:
        // Proactively probe ahead along forward heading using SphereCastAll
        // Low obstacles (<= 0.85m) are glided over; tall obstacles (> 0.85m) are steered around laterally along the seabed
        float probeRadius = Mathf.Clamp(_bodyRadius * 0.75f, 0.45f, 1.2f);
        float probeDist = Mathf.Clamp(_currentSpeed * 1.5f + 1.2f, 1.8f, 3.8f);
        int propMask = LayerMask.GetMask("Default", "Terrain");
        Vector3 probeOrigin = transform.position + Vector3.up * 0.35f;
        float desiredLift = 0f;

        Vector3 probeDir = flatFacing;
        if (targetTangent.sqrMagnitude > 0.01f)
            probeDir = targetTangent;

        float highestLowPropTop = float.MinValue;
        float closestPropDist = probeDist;
        bool hasLowPropAhead = false;
        Vector3 lateralAvoidance = Vector3.zero;

        // 1. Proactive forward SphereCastAll to detect low props (clams, small stones, low coral plates) and deflect around tall ones
        var aheadHits = Physics.SphereCastAll(probeOrigin, probeRadius, probeDir, probeDist, propMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < aheadHits.Length; i++)
        {
            var h = aheadHits[i];
            if (h.collider != null && h.collider.transform.root != transform.root && !h.collider.CompareTag("Species"))
            {
                if (ContextSteering.IsEnvironmentProp(h.collider))
                {
                    float propHeight = h.collider.bounds.size.y;
                    float topY = h.collider.bounds.max.y;
                    if (propHeight <= 0.85f)
                    {
                        if (topY > highestLowPropTop) highestLowPropTop = topY;
                        if (h.distance < closestPropDist) closestPropDist = h.distance;
                        hasLowPropAhead = true;
                    }
                    else
                    {
                        // Tall obstacle: deflect laterally along seabed tangent away from obstacle center
                        Vector3 away = Vector3.ProjectOnPlane(transform.position - h.collider.bounds.center, _groundNormal).normalized;
                        if (away.sqrMagnitude > 0.01f)
                        {
                            float urgency = 1f - Mathf.Clamp01(h.distance / probeDist);
                            lateralAvoidance += away * urgency;
                        }
                    }
                }
            }
        }

        // 2. Direct overlap sphere check directly under/around the creature so lift is maintained continuously while moving over the prop
        var directProps = Physics.OverlapSphere(transform.position + Vector3.up * 0.35f, probeRadius * 1.25f, propMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < directProps.Length; i++)
        {
            var dp = directProps[i];
            if (dp != null && dp.transform.root != transform.root && !dp.CompareTag("Species"))
            {
                if (ContextSteering.IsEnvironmentProp(dp) && dp.bounds.size.y <= 0.85f)
                {
                    float topY = dp.bounds.max.y;
                    if (topY > highestLowPropTop) highestLowPropTop = topY;
                    closestPropDist = 0f;
                    hasLowPropAhead = true;
                }
            }
        }

        if (hasLowPropAhead && highestLowPropTop > _groundY)
        {
            float neededHeight = (highestLowPropTop + effectiveOffset + 0.20f) - baseTargetY;
            if (neededHeight > 0f)
            {
                float distRatio = 1f - Mathf.Clamp01(closestPropDist / probeDist);
                float maxPropLift = isRay ? 0.85f : 0.60f;
                desiredLift = Mathf.Clamp(neededHeight * distRatio, 0f, maxPropLift);
            }
        }

        if (lateralAvoidance.sqrMagnitude > 0.01f)
        {
            targetTangent = Vector3.Slerp(targetTangent, lateralAvoidance.normalized, 0.45f).normalized;
        }

        // Active creature-to-creature dynamic separation & repulsion:
        // Physically prevents two specimens (e.g. flat rays) from overlapping, stacking, or fighting over the same seabed coordinates
        Vector3 creatureRepulsion = Vector3.zero;
        Vector3 creatureEvadeTangent = Vector3.zero;
        float neighborRadius = Mathf.Clamp(_bodyRadius * 2.4f, 2.2f, 5.5f);
        int creatureMask = LayerMask.GetMask("Creatures", "Default");
        var nearbyCreatures = Physics.OverlapSphere(transform.position + Vector3.up * 0.25f, neighborRadius, creatureMask, QueryTriggerInteraction.Collide);

        int processedNeighbors = 0;
        int[] processedIds = new int[8];

        for (int c = 0; c < nearbyCreatures.Length; c++)
        {
            var otherCol = nearbyCreatures[c];
            if (otherCol == null || otherCol.transform.root == transform.root) continue;
            if (!otherCol.CompareTag("Species") && otherCol.gameObject.layer != LayerMask.NameToLayer("Creatures")) continue;

            // Deduplicate neighbor root GameObjects so multi-collider creatures don't apply duplicate forces
            int rootId = otherCol.transform.root.gameObject.GetInstanceID();
            bool alreadyProcessed = false;
            for (int p = 0; p < processedNeighbors; p++)
            {
                if (processedIds[p] == rootId) { alreadyProcessed = true; break; }
            }
            if (alreadyProcessed) continue;
            if (processedNeighbors < processedIds.Length) processedIds[processedNeighbors++] = rootId;

            Vector3 otherRootPos = otherCol.transform.root.position;
            Vector3 diff = transform.position - otherRootPos;
            Vector3 horizDiff = Vector3.ProjectOnPlane(diff, _groundNormal);
            float hDist = horizDiff.magnitude;

            if (hDist < neighborRadius)
            {
                Vector3 pushDir;
                if (hDist < 0.25f)
                {
                    // Directly stacked / overlapping: break symmetry deterministically using Instance IDs
                    int myId = gameObject.GetInstanceID();
                    int otherId = rootId;
                    Vector3 sideRef = Vector3.Cross(_groundNormal, flatFacing).normalized;
                    if (sideRef.sqrMagnitude < 0.01f) sideRef = Vector3.right;
                    pushDir = (myId > otherId ? sideRef : -sideRef).normalized;
                }
                else
                {
                    pushDir = (horizDiff / hDist).normalized;
                }

                float overlapFactor = 1f - Mathf.Clamp01(hDist / neighborRadius);
                // Strong progressive repulsion pushes overlapping bodies apart smoothly
                float pushSpeed = Mathf.Lerp(1.8f, 5.5f, overlapFactor * overlapFactor);
                creatureRepulsion += pushDir * (pushSpeed * Time.fixedDeltaTime);

                // Blend evasion tangent away from neighbor
                creatureEvadeTangent += pushDir * overlapFactor;
            }
        }

        if (creatureEvadeTangent.sqrMagnitude > 0.01f)
        {
            targetTangent = Vector3.Slerp(targetTangent, creatureEvadeTangent.normalized, 0.65f).normalized;
        }

        // Temporal low-pass filter on surfaceTangent with physical angular momentum:
        // Eliminates 100% of frame-to-frame direction flip-flopping and steering twitch
        if (_smoothedSurfaceTangent.sqrMagnitude < 0.01f)
        {
            _smoothedSurfaceTangent = targetTangent;
        }
        else
        {
            float slerpRate = _currentSpeed > 0.25f ? 7.0f : 3.5f;
            _smoothedSurfaceTangent = Vector3.Slerp(_smoothedSurfaceTangent, targetTangent, slerpRate * Time.fixedDeltaTime).normalized;
        }
        Vector3 surfaceTangent = _smoothedSurfaceTangent;

        // Contact push cushion: only applies to solid, non-trigger obstacles that are TALLER than current glide lift
        Vector3 contactPush = Vector3.zero;
        if (_benthicGlideLift < 0.20f)
        {
            var touchingProps = Physics.OverlapSphere(transform.position + Vector3.up * 0.3f, probeRadius * 1.1f, propMask, QueryTriggerInteraction.Ignore);
            for (int p = 0; p < touchingProps.Length; p++)
            {
                var col = touchingProps[p];
                if (col != null && col.transform.root != transform.root && !col.isTrigger && ContextSteering.IsEnvironmentProp(col))
                {
                    if (col.bounds.max.y > transform.position.y)
                    {
                        Vector3 away = Vector3.ProjectOnPlane(transform.position - col.bounds.center, _groundNormal).normalized;
                        if (away.sqrMagnitude > 0.01f)
                        {
                            Vector3 slideTangent = Vector3.Cross(_groundNormal, away).normalized;
                            if (Vector3.Dot(slideTangent, surfaceTangent) < 0f) slideTangent = -slideTangent;
                            contactPush += (slideTangent * 0.6f + away * 0.4f) * (1.5f * Time.fixedDeltaTime);
                        }
                    }
                }
            }
        }

        // Benthic creatures glide upward over low seabed props up to 0.85m (rays) or 0.60m (lobsters/snails)
        float maxLift = isRay ? 0.85f : 0.60f;
        desiredLift = Mathf.Clamp(desiredLift, 0f, maxLift);

        // Stable vertical lift cadence: hold obstacle clearance for 1.8 seconds.
        // Prevents rapid up-and-down bopping and jitter when grazing irregular prop geometry.
        if (desiredLift > _benthicGlideLift)
        {
            _benthicGlideLift = Mathf.MoveTowards(_benthicGlideLift, desiredLift, 2.0f * Time.fixedDeltaTime);
            _benthicLiftHoldTimer = Time.time + 1.8f;
        }
        else if (Time.time >= _benthicLiftHoldTimer)
        {
            _benthicGlideLift = Mathf.MoveTowards(_benthicGlideLift, desiredLift, 0.75f * Time.fixedDeltaTime);
        }

        float liftDelta = (_benthicGlideLift - _prevBenthicLift) / Mathf.Max(0.001f, Time.fixedDeltaTime);
        _prevBenthicLift = _benthicGlideLift;

        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 4f * Time.fixedDeltaTime);

        // 4. Lateral Evasion Glide & Propulsion on the seabed:
        Vector3 moveDir;
        if (isRay)
        {
            // Rays are flat swimming discs with full lateral hydrodynamics: glide along evasion tangent
            moveDir = Vector3.Slerp(flatFacing, surfaceTangent, 0.85f).normalized;
        }
        else
        {
            // Crawlers blend toward evasion tangent when facing is blocked
            float facingDot = Vector3.Dot(flatFacing, surfaceTangent);
            float lateralBlend = facingDot < 0.6f ? 0.70f : 0.25f;
            moveDir = Vector3.Slerp(flatFacing, surfaceTangent, lateralBlend).normalized;
        }
        if (moveDir.sqrMagnitude < 0.001f) moveDir = surfaceTangent;

        // Turn-in-place before advancing: maintains continuous glide velocity while turning to eliminate freezing
        float forwardDot = Vector3.Dot(moveDir, surfaceTangent);
        float turnDrag = Mathf.Clamp01(forwardDot);
        float forwardSpeed = _currentSpeed * Mathf.Lerp(isRay ? 0.60f : 0.35f, 1.0f, turnDrag);

        Vector3 newPos = _rb.position + moveDir * forwardSpeed * Time.fixedDeltaTime + contactPush + creatureRepulsion;

        // 5. Smooth height calculation with upward gliding over obstacles (3.2f lerp rate eliminates popping)
        float targetY = baseTargetY + _benthicGlideLift;
        if (_hasGroundHit || _benthicGlideLift > 0.01f)
        {
            newPos.y = Mathf.Lerp(newPos.y, targetY, 3.2f * Time.fixedDeltaTime);
        }
        else
        {
            newPos.y = targetY;
        }

        _rb.MovePosition(newPos);

        // 6. Dynamic seabed slope alignment ("Angled at the bottom when moving"):
        // Conforms pitch & roll dynamically to the terrain contours along the direction of travel
        // When stopped or moving very slowly, stabilizes heading to current flat facing to eliminate in-place rotational jitter
        Vector3 hHeading;
        if (_currentSpeed > 0.25f && surfaceTangent.sqrMagnitude > 0.01f)
        {
            hHeading = Vector3.ProjectOnPlane(surfaceTangent, Vector3.up).normalized;
        }
        else
        {
            hHeading = flatFacing;
        }
        if (hHeading.sqrMagnitude < 0.001f)
        {
            hHeading = flatFacing;
            if (hHeading.sqrMagnitude < 0.001f) hHeading = Vector3.forward;
        }

        var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
        float probeSpan = Mathf.Clamp(_bodyLength * 0.5f, 0.8f, 2.0f);
        Vector3 frontPos = newPos + hHeading * probeSpan;
        Vector3 rearPos  = newPos - hHeading * probeSpan;

        float frontFloorY = (tg != null && tg.HasGenerated) ? tg.SampleHeight(frontPos.x, frontPos.z) : _groundY;
        float rearFloorY  = (tg != null && tg.HasGenerated) ? tg.SampleHeight(rearPos.x, rearPos.z) : _groundY;

        // Compute the true terrain slope forward vector along travel heading
        Vector3 rawSlopeForward = (new Vector3(hHeading.x * (probeSpan * 2f), frontFloorY - rearFloorY, hHeading.z * (probeSpan * 2f))).normalized;

        // Orthogonalize with smoothed ground normal
        Vector3 slopeNormal = _groundNormal;
        rawSlopeForward = Vector3.ProjectOnPlane(rawSlopeForward, slopeNormal).normalized;
        if (rawSlopeForward.sqrMagnitude < 0.001f) rawSlopeForward = hHeading;

        // Temporal low-pass filter on slope forward vector: eliminates poly-seam pitch bumps and shaking
        if (!_hasInitializedSlope || _smoothedSlopeForward.sqrMagnitude < 0.01f)
        {
            _smoothedSlopeForward = rawSlopeForward;
            _hasInitializedSlope = true;
        }
        else
        {
            float slopeLerpRate = _currentSpeed > 0.25f ? 7.0f : 2.5f;
            _smoothedSlopeForward = Vector3.Slerp(_smoothedSlopeForward, rawSlopeForward, slopeLerpRate * Time.fixedDeltaTime).normalized;
        }
        Vector3 slopeForward = _smoothedSlopeForward;

        // Dynamic pitch contour ("Angled at the bottom when moving"):
        // Pitch nose up while climbing upward towards/over props, level out at crest, pitch down gently when descending
        if (Mathf.Abs(liftDelta) > 0.08f)
        {
            if (liftDelta > 0.08f)
            {
                // Climbing up towards/over obstacle: pitch nose upward smoothly
                float climbRatio = Mathf.Clamp01(liftDelta / 1.6f);
                slopeForward = Vector3.Slerp(slopeForward, (slopeForward + slopeNormal * 0.45f).normalized, climbRatio * 0.75f);
            }
            else
            {
                // Gliding down back to seabed: pitch nose downward gently
                float descendRatio = Mathf.Clamp01(-liftDelta / 1.4f);
                slopeForward = Vector3.Slerp(slopeForward, (slopeForward - slopeNormal * 0.30f).normalized, descendRatio * 0.65f);
            }
        }

        // Strict limit: all fishes and benthic species tilt up to a maximum of 30 degrees
        float maxSin30 = Mathf.Sin(30f * Mathf.Deg2Rad);
        if (Mathf.Abs(slopeForward.y) > maxSin30)
        {
            slopeForward.y = Mathf.Clamp(slopeForward.y, -maxSin30, maxSin30);
            slopeForward.Normalize();
        }

        // Proper orientation: construct slope rotation first, then rotate by modelYawOffset around local up (slopeNormal)
        // This guarantees that models with yaw offsets (180 deg for stingray, -90 for lobster) pitch correctly along the slope!
        Quaternion slopeBaseRot = Quaternion.LookRotation(slopeForward, slopeNormal);
        Quaternion targetRot = slopeBaseRot * Quaternion.Euler(0f, yawOffset, 0f);

        float maxTurnRate = _steering != null ? _steering.TurnSpeed : 50f;
        float angleDelta = Quaternion.Angle(_rb.rotation, targetRot);

        // Continuous smooth rotation when moving, stable calm resting when stationary
        if (angleDelta > 0.02f && (_currentSpeed > 0.08f || angleDelta > 6f))
        {
            float turnRatio = Mathf.Clamp01(angleDelta / 20f);
            float effectiveRate = Mathf.Lerp(8f, maxTurnRate, turnRatio);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, effectiveRate * Time.fixedDeltaTime));
        }
    }

    private void SampleGround()
    {
        // 1. Primary ground reference: Analytical terrain generator height & normal
        var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
        float analyticalY = (tg != null && tg.HasGenerated) ? tg.SampleHeight(transform.position.x, transform.position.z) : -200f;
        Vector3 analyticalNormal = (tg != null && tg.HasGenerated) ? tg.SampleNormal(transform.position.x, transform.position.z) : Vector3.up;

        // 2. Physics raycast verification against Terrain mesh layer
        int terrainLayerIndex = LayerMask.NameToLayer("Terrain");
        int mask = terrainLayerIndex >= 0 ? (1 << terrainLayerIndex) : LayerMask.GetMask("Default");

        Vector3 rayOrigin = transform.position + Vector3.up * 6f;
        var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 50f, mask, QueryTriggerInteraction.Collide);
        float bestY = float.MinValue;
        Vector3 bestNormal = Vector3.up;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider != null && h.collider.transform.root != transform.root)
            {
                if (h.collider.CompareTag("Boundary") || h.collider.CompareTag("Species") ||
                    ContextSteering.IsEnvironmentProp(h.collider) ||
                    h.collider.GetComponentInParent<EnvironmentFactTarget>() != null ||
                    h.collider.CompareTag("Environment")) continue;

                // STRICT: Only accept surfaces that are valid walkable seabed slopes (Y normal >= 0.35).
                // Reject vertical walls, vertical coral branches, and sideways mesh faces!
                if (h.normal.y < 0.35f) continue;

                if (h.point.y > bestY)
                {
                    bestY = h.point.y;
                    bestNormal = h.normal;
                    found = true;
                }
            }
        }

        // Determine ground surface target
        float targetGroundY = analyticalY;
        Vector3 targetGroundNormal = analyticalNormal;

        if (found)
        {
            // If raycast hit is close to analytical seabed (+/- 0.6m), it is the physical mesh surface.
            // Blend smooth analytical normal (continuous C1 gradient) with raycast normal (local mesh facet)
            // to completely eliminate polygon facet edge snapping while preserving local slope detail.
            if (tg != null && tg.HasGenerated && Mathf.Abs(bestY - analyticalY) <= 0.6f)
            {
                targetGroundY = bestY;
                targetGroundNormal = Vector3.Slerp(analyticalNormal, bestNormal, 0.35f).normalized;
            }
            else if (tg == null || !tg.HasGenerated)
            {
                targetGroundY = bestY;
                targetGroundNormal = bestNormal;
            }
        }

        // 3. Normal safety clamp: allow up to 48 degrees slope tilt so creature conforms beautifully to dunes and slopes
        // while preventing flipping upside down or standing straight up on vertical walls.
        if (Vector3.Angle(Vector3.up, targetGroundNormal) > 48f)
        {
            targetGroundNormal = Vector3.RotateTowards(Vector3.up, targetGroundNormal, 48f * Mathf.Deg2Rad, 0f);
        }
        if (targetGroundNormal.y < 0.40f)
        {
            targetGroundNormal.y = Mathf.Max(targetGroundNormal.y, 0.40f);
            targetGroundNormal.Normalize();
        }

        // 4. Low-pass temporal smoothing: eliminates poly-seam snapping and single-frame bumps
        if (!_groundInitialized)
        {
            _groundY = targetGroundY;
            _groundNormal = targetGroundNormal;
            _groundInitialized = true;
            _hasGroundHit = true;
        }
        else
        {
            _groundY = Mathf.Lerp(_groundY, targetGroundY, 14f * Time.fixedDeltaTime);
            _groundNormal = Vector3.Slerp(_groundNormal, targetGroundNormal, 10f * Time.fixedDeltaTime);
            _hasGroundHit = true;
        }
    }

    // -----------------------------------------------------------------------
    // Archetype 5: Surface Drifter (Bluebottles)
    // Water-surface clamp with gentle swell bobbing and ambient drift
    // -----------------------------------------------------------------------

    private void UpdateSurfaceDrifter()
    {
        // Locked at water surface (Y = -1.15m) with gentle swell bobbing
        // Float sits right at the waterline with tentacles cascading visibly down into view
        float bobbing = Mathf.Sin(_cycleTimer * 1.5f) * 0.12f;
        float targetY = -1.15f + bobbing;

        Vector3 pos = _rb.position;
        pos.y = targetY;

        // Drift slowly with ambient ocean current
        pos += _ambientCurrent * (0.25f * Time.fixedDeltaTime);
        _rb.MovePosition(pos);

        // Gentle roll and pitch swell bobbing without spinning or jittering yaw
        float roll = Mathf.Sin(_cycleTimer * 1.2f) * 5f;
        float pitch = Mathf.Cos(_cycleTimer * 0.9f) * 2.5f;
        _rb.MoveRotation(Quaternion.Euler(pitch, _surfaceDriftYaw, roll));
    }

    private void EnhanceSurfaceDrifterVisuals()
    {
        // 1. Remove any bioluminescent point light so it doesn't create a floating light flare
        var lights = GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
        {
            if (l != null) Destroy(l);
        }

        // 2. Enhance materials: translucent azure body with natural shading
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (m == null) continue;
                // Render in front of the water underside plane
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;

                Color azure = new Color(0.15f, 0.72f, 1.0f, 0.95f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", azure);
                if (m.HasProperty("_Color")) m.SetColor("_Color", azure);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            }
        }
    }

    /// <summary>
    /// Deflects surface drifter ambient drift current horizontally away from perimeter walls.
    /// </summary>
    public void DeflectSurfaceDrifter(Vector3 normal)
    {
        normal.y = 0f;
        if (normal.sqrMagnitude > 0.01f)
        {
            normal.Normalize();
            _ambientCurrent = Vector3.Reflect(_ambientCurrent, normal);
            // Ensure ambient current always points in the direction of the inward normal
            if (Vector3.Dot(_ambientCurrent, normal) < 0.1f)
            {
                Vector3 tangent = new Vector3(-normal.z, 0f, normal.x) * Random.Range(-0.4f, 0.4f);
                _ambientCurrent = (normal + tangent).normalized * 0.35f;
            }
            _ambientCurrent.y = 0f;
            _surfaceDriftYaw = Mathf.Atan2(_ambientCurrent.x, _ambientCurrent.z) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Enforces that creature NEVER breaches survey boundary limits or depth band,
    /// even if individual steering commands or physics steps overshoot.
    /// </summary>
    private void EnforcePlayableAndDepthBounds()
    {
        if (_rb == null) return;

        float safeHalfW = _steering != null ? _steering.SafeHalfWidth : 240f;
        float safeHalfL = _steering != null ? _steering.SafeHalfLength : 240f;

        Vector3 pos = _rb.position;
        bool clamped = false;
        Vector3 boundaryNormal = Vector3.zero;

        // 1. Horizontal perimeter bounds
        if (pos.x < -safeHalfW)
        {
            pos.x = -safeHalfW;
            boundaryNormal.x = 1f;
            clamped = true;
        }
        else if (pos.x > safeHalfW)
        {
            pos.x = safeHalfW;
            boundaryNormal.x = -1f;
            clamped = true;
        }

        if (pos.z < -safeHalfL)
        {
            pos.z = -safeHalfL;
            boundaryNormal.z = 1f;
            clamped = true;
        }
        else if (pos.z > safeHalfL)
        {
            pos.z = safeHalfL;
            boundaryNormal.z = -1f;
            clamped = true;
        }

        // PelagicCruiser handles its bounds clamping in its unified move step
        if (_data != null && _data.locomotionArchetype == LocomotionArchetype.PelagicCruiser)
            return;

        // 2. Vertical depth bounds
        bool isSurfaceDrifter = _data != null && _data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;
        bool isBenthic = _data != null && (_data.locomotionArchetype == LocomotionArchetype.BenthicFollower || _data.isStationary);

        if (isSurfaceDrifter)
        {
            if (clamped && boundaryNormal.sqrMagnitude > 0.01f)
            {
                DeflectSurfaceDrifter(boundaryNormal);
            }
        }
        else if (!isBenthic)
        {
            float effTopY = _steering != null ? _steering.SafeTopY : -3.0f;
            effTopY = Mathf.Min(effTopY, -3.0f); // Strictly below surface water (depth >= 3m)
            float effBottomY = _steering != null ? _steering.SafeBottomY : -180f;

            bool isPelagic = _data != null && _data.locomotionArchetype == LocomotionArchetype.PelagicCruiser;
            bool isHoverBurst = _data != null && _data.locomotionArchetype == LocomotionArchetype.HoverBurst;
            float safeClearance = isHoverBurst
                ? Mathf.Max(0.35f, _bottomToPivot + 0.15f)
                : Mathf.Max(isPelagic ? 4.8f : 3.0f, _bottomToPivot + 1.8f);

            var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
            float floorAtPos = tg != null && tg.HasGenerated ? tg.SampleHeight(pos.x, pos.z) : -200f;

            // Probe seabed under entire longitudinal body length so pitching nose and tail never clip into hills
            Vector3 fwd = transform.forward;
            float probeSpan = isHoverBurst ? 0.35f : Mathf.Max(3.0f, _bodyLength * 0.45f);
            float floorNose = tg != null && tg.HasGenerated ? tg.SampleHeight(pos.x + fwd.x * probeSpan, pos.z + fwd.z * probeSpan) : floorAtPos;
            float floorTail = tg != null && tg.HasGenerated ? tg.SampleHeight(pos.x - fwd.x * probeSpan, pos.z - fwd.z * probeSpan) : floorAtPos;
            float highestFloorUnderBody = Mathf.Max(floorAtPos, Mathf.Max(floorNose, floorTail));

            // HoverBurst (clownfish) stays safely above the local reef terrain without artificial depth floor clamp
            float minSwimY = isHoverBurst
                ? (highestFloorUnderBody + safeClearance)
                : Mathf.Max(effBottomY, highestFloorUnderBody + safeClearance);

            if (pos.y > effTopY)
            {
                pos.y = effTopY;
                boundaryNormal.y = -1f;
                clamped = true;
            }
            else if (pos.y < minSwimY)
            {
                pos.y = minSwimY;
                boundaryNormal.y = 1f;
                clamped = true;
            }
        }

        if (clamped)
        {
            _rb.MovePosition(pos);
            if (_ai != null && boundaryNormal.sqrMagnitude > 0.01f && Time.time >= _nextBoundaryTurnCooldown)
            {
                _nextBoundaryTurnCooldown = Time.time + 1.5f;
                _ai.TurnAwayFromBoundary(boundaryNormal.normalized);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Archetype 6: Serpentine (Sea Krait)
    // Continuous lateral sinusoidal yaw undulation (S-curve swimming)
    // -----------------------------------------------------------------------

    private void UpdateSerpentine(Vector3 steerDir, float targetSpeed)
    {
        if (steerDir.sqrMagnitude < 0.01f)
        {
            steerDir = transform.forward;
        }

        float freq = Mathf.Max(0.5f, _data.undulationFrequency);
        float amp  = Mathf.Clamp(_data.undulationAmplitude, 2f, 15f);

        // 1. Natural forward speed interpolation
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 3f * Time.fixedDeltaTime);

        // 2. Physical propulsion along body forward with obstacle deflection
        float forwardDot = Vector3.Dot(transform.forward, steerDir.normalized);
        float turnDrag = Mathf.Clamp01((forwardDot + 1f) * 0.5f);
        float forwardSpeed = _currentSpeed * Mathf.Lerp(0.35f, 1.0f, turnDrag);

        Vector3 moveDir = forwardDot < 0.6f
            ? Vector3.Slerp(transform.forward, steerDir.normalized, 0.40f).normalized
            : transform.forward;

        Vector3 moveStep = moveDir * forwardSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        // 3. Smooth heading rotation
        float turnBoost = forwardDot < 0.5f ? 1.6f : 1.0f;
        ApplySteeringRotation(steerDir, 0f, full3D: false, maxPitch: 30f, turnBoost: turnBoost);

        // 4. Animation / Visual deformation
        if (_animator != null)
        {
            // If skeletal Animator is active (BandedSeaKrate_Controller), synchronize playback speed with swim speed
            float targetAnimSpeed = Mathf.Clamp(_currentSpeed / Mathf.Max(0.5f, targetSpeed), 0.5f, 1.4f);
            _animator.speed = Mathf.Lerp(_animator.speed, targetAnimSpeed, 3f * Time.fixedDeltaTime);

            // Keep visual child aligned (let the armature bones perform the natural S-curve)
            if (_visualModel != null && _visualModel != transform)
            {
                _visualModel.localRotation = Quaternion.Slerp(_visualModel.localRotation, Quaternion.identity, 4f * Time.fixedDeltaTime);
            }
        }
        else if (_visualModel != null && _visualModel != transform)
        {
            // Fallback for models without skeletal animation: subtle organic spine yaw undulation
            float lateralWave = Mathf.Sin(_cycleTimer * freq * Mathf.PI * 2f) * (amp * 0.35f);
            _visualModel.localRotation = Quaternion.Euler(0f, lateralWave, 0f);
        }
    }

    // -----------------------------------------------------------------------
    // Rotation & Dynamic Banking
    // -----------------------------------------------------------------------

    private void ApplySteeringRotation(Vector3 dir, float extraPitch, bool full3D = false, float maxPitch = 30f, float turnBoost = 1.0f)
    {
        if (dir.sqrMagnitude < 0.001f) return;

        float maxTurnRate = _steering != null ? _steering.TurnSpeed : 65f;
        float yawOffset = _steering != null ? _steering.ModelYawOffset : 0f;

        // Strict limit: all fishes can tilt up to a maximum of 30 degrees
        float clampedMaxPitch = Mathf.Clamp(maxPitch, 0f, 30f);

        Quaternion targetRot;

        if (full3D)
        {
            Vector3 lookDir = dir.normalized;
            // Strict 30 degree vertical clamp in full 3D as well
            float maxSin30 = Mathf.Sin(30f * Mathf.Deg2Rad);
            lookDir.y = Mathf.Clamp(lookDir.y, -maxSin30, maxSin30);
            lookDir.Normalize();

            Vector3 upVector = Mathf.Abs(lookDir.y) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion baseLook = Quaternion.LookRotation(lookDir, upVector);
            targetRot = baseLook * Quaternion.Euler(Mathf.Clamp(extraPitch, -30f, 30f), yawOffset, _currentBankAngle);
        }
        else
        {
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude < 0.001f)
            {
                flatDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
            }
            flatDir.Normalize();

            // Calculate vertical pitch from 3D direction vector:
            // "MAKE THEM HORIZONTAL WHEN NOT GOING UPWARD."
            // In Unity Euler angles: negative pitch tilts nose up (+Y), positive tilts nose down (-Y).
            // When cruising horizontally or swimming down, maintain strictly 0 pitch (horizontal).
            // Only tilt nose up when actively swimming upward (dir.y > 0.05f).
            float verticalPitch = 0f;
            if (dir.sqrMagnitude > 0.001f && dir.y > 0.05f)
            {
                float horizontalDist = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                float angleRad = Mathf.Atan2(dir.y, horizontalDist);
                verticalPitch = Mathf.Clamp(-angleRad * Mathf.Rad2Deg, -clampedMaxPitch, 0f);
            }

            float totalPitch = Mathf.Clamp(verticalPitch + extraPitch, -30f, 0f);
            Vector3 pitchAxis = Vector3.Cross(Vector3.up, flatDir);
            Vector3 pitchedForward = (pitchAxis.sqrMagnitude > 0.001f)
                ? (Quaternion.AngleAxis(totalPitch, pitchAxis) * flatDir).normalized
                : flatDir;

            Quaternion baseLook = Quaternion.LookRotation(pitchedForward, Vector3.up);
            // Apply modelYawOffset around the model's true local up axis, and bank angle around local forward
            targetRot = baseLook * Quaternion.Euler(0f, yawOffset, _currentBankAngle);
        }

        // Natural, smooth hydrodynamic turning without stepping or ratcheting
        float angleDelta = Quaternion.Angle(_rb.rotation, targetRot);
        if (angleDelta > 0.02f)
        {
            // For large angle changes, turn at maxTurnRate (degrees/sec)
            // For small angle changes, gently ease in with smooth proportional damping
            float turnFactor = Mathf.Clamp01(angleDelta / 15f);
            float effectiveRate = Mathf.Lerp(10f, maxTurnRate, turnFactor) * turnBoost;
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, effectiveRate * Time.fixedDeltaTime));
        }
    }

    private void UpdateBankingAndYaw()
    {
        if (_data != null && (_data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter || _data.locomotionArchetype == LocomotionArchetype.Sessile))
            return;

        float currentYaw = transform.eulerAngles.y;
        float deltaYaw   = Mathf.DeltaAngle(_previousYaw, currentYaw);
        _previousYaw     = currentYaw;

        if (_data != null && _data.bankingAngle > 0f)
        {
            // Bank into turns like an airplane / fast shark
            float targetBank = Mathf.Clamp(-deltaYaw * 8f, -_data.bankingAngle, _data.bankingAngle);
            _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBank, 5f * Time.fixedDeltaTime);
        }
        else
        {
            _currentBankAngle = Mathf.Lerp(_currentBankAngle, 0f, 6f * Time.fixedDeltaTime);
        }
    }
}
