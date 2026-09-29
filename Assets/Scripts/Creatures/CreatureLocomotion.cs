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

    // Benthic state
    private Vector3 _groundNormal = Vector3.up;
    private float   _groundY = -200f;
    private bool    _hasGroundHit;

    // Surface drifter state
    private Vector3 _ambientCurrent = new Vector3(0.35f, 0f, 0.2f);

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
    }

    public void Initialize(SpeciesData data)
    {
        _data = data;
        _cycleTimer = Random.Range(0f, 5f); // desynchronize individuals

        if (_visualModel != null && _visualModel != transform)
        {
            _baseModelScale = _visualModel.localScale;
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
        // Marine vertebrates generate thrust along their longitudinal body axis (transform.forward).
        // Propelling strictly along the creature's facing guarantees it will NEVER drift
        // sideways or slide backwards into a turn, producing authentic hydrodynamic arcs.
        float forwardDot = Vector3.Dot(transform.forward, steerDir.normalized);
        float turnDrag = Mathf.Clamp01((forwardDot + 1f) * 0.5f); // 1.0 when facing target, ~0.5 at 90 deg
        float forwardSpeed = _currentSpeed * Mathf.Lerp(0.55f, 1.0f, turnDrag);

        Vector3 moveStep = transform.forward * forwardSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        // Smooth 3D swimming rotation towards steerDir
        ApplySteeringRotation(steerDir, 0f, full3D: false, maxPitch: 30f);

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
        float burstInterval = Mathf.Max(0.5f, 1f / Mathf.Max(0.1f, _data.pulseFrequency));

        if (_burstTimer >= burstInterval)
        {
            _burstTimer = 0f;
            _isBursting = true;
        }

        if (_isBursting)
        {
            // Explosive dash for short duration
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed * 2.2f, 12f * Time.fixedDeltaTime);
            if (_burstTimer > 0.35f)
            {
                _isBursting = false;
            }
        }
        else
        {
            // Gentle hovering station drift
            float hoverSpeed = targetSpeed * 0.2f;
            _currentSpeed = Mathf.Lerp(_currentSpeed, hoverSpeed, 4f * Time.fixedDeltaTime);
        }

        // Add subtle hummingbird-like hover jitter
        Vector3 hoverWobble = new Vector3(
            Mathf.Cos(_cycleTimer * 7f) * 0.15f,
            Mathf.Sin(_cycleTimer * 5f) * 0.15f,
            Mathf.Sin(_cycleTimer * 6f) * 0.15f);

        Vector3 moveStep = (steerDir.normalized * _currentSpeed + hoverWobble) * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        ApplySteeringRotation(steerDir, 0f, full3D: false, maxPitch: 40f);
    }

    // -----------------------------------------------------------------------
    // Archetype 3: Pulsatile Jetter (Jellyfish, Squids)
    // Periodic contraction impulse followed by hydrodynamic drag glide
    // -----------------------------------------------------------------------

    private void UpdatePulsatileJetter(Vector3 steerDir, float targetSpeed)
    {
        // 1. Natural propulsion pulse cycle
        float freq = Mathf.Max(0.2f, _data.pulseFrequency);
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
            if (effectiveDir.sqrMagnitude < 0.01f)
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
                // Jellyfish tilt smoothly in their travel direction (up to 45 deg tilt)
                ApplySteeringRotation(effectiveDir, 0f, full3D: false, maxPitch: 45f);
            }
            else
            {
                ApplySteeringRotation(effectiveDir, 0f, full3D: false, maxPitch: 40f);
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
        // 1. Sample ground beneath creature
        SampleGround();

        // 2. Check for crustacean backward tail-flip escape
        bool isFleeing = _ai != null && _ai.IsFleeing;
        bool isLobster = _data.taxonomicClass == TaxonomicClass.Malacostraca;

        Vector3 effectiveDir = steerDir;
        if (isLobster && isFleeing)
        {
            // Backward tail-flip: propel backward relative to current facing
            effectiveDir = -transform.forward;
            targetSpeed = Mathf.Max(targetSpeed, 4.0f);
        }

        // 3. Project steering vector onto terrain tangent plane
        Vector3 surfaceTangent = Vector3.ProjectOnPlane(effectiveDir, _groundNormal).normalized;
        if (surfaceTangent.sqrMagnitude < 0.001f)
            surfaceTangent = Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;

        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 4f * Time.fixedDeltaTime);

        // 4. Physical propulsion along the model's true facing on the seabed:
        // Ensures rays and crawlers glide in the exact direction their head is facing,
        // preventing any backward or sideways drift during steering turns.
        float yawOffset = _steering != null ? _steering.ModelYawOffset : 0f;
        Vector3 trueFacing = yawOffset != 0f
            ? (transform.rotation * Quaternion.Euler(0f, -yawOffset, 0f) * Vector3.forward)
            : transform.forward;
        Vector3 moveDir = Vector3.ProjectOnPlane(trueFacing, _groundNormal).normalized;
        if (moveDir.sqrMagnitude < 0.001f) moveDir = surfaceTangent;

        float forwardDot = Vector3.Dot(moveDir, surfaceTangent);
        float turnDrag = Mathf.Clamp01((forwardDot + 1f) * 0.5f);
        float forwardSpeed = _currentSpeed * Mathf.Lerp(0.55f, 1.0f, turnDrag);

        Vector3 newPos = _rb.position + moveDir * forwardSpeed * Time.fixedDeltaTime;

        // 5. Firmly clamp height to seabed clearance offset
        float targetY = _groundY + Mathf.Max(0.2f, _data.benthicSurfaceOffset);
        if (_hasGroundHit)
        {
            newPos.y = Mathf.Lerp(newPos.y, targetY, 8f * Time.fixedDeltaTime);
        }

        _rb.MovePosition(newPos);

        // 6. Align rotation to surface normal while facing swim direction
        if (surfaceTangent.sqrMagnitude > 0.001f)
        {
            float maxTurnRate = _steering != null ? _steering.TurnSpeed : 50f;
            Quaternion forwardRot = Quaternion.LookRotation(surfaceTangent, _groundNormal) * Quaternion.Euler(0f, yawOffset, 0f);
            float angleDelta = Quaternion.Angle(_rb.rotation, forwardRot);
            float effectiveRate = Mathf.Min(maxTurnRate, Mathf.Max(12f, angleDelta * 2.0f));
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, forwardRot, effectiveRate * Time.fixedDeltaTime));
        }
    }

    private void SampleGround()
    {
        int mask = LayerMask.GetMask("Terrain", "Default");
        if (mask == 0) mask = ~0;

        Vector3 rayOrigin = transform.position + Vector3.up * 5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, mask, QueryTriggerInteraction.Ignore))
        {
            _groundNormal = hit.normal;
            _groundY      = hit.point.y;
            _hasGroundHit = true;
        }
        else
        {
            _hasGroundHit = false;
        }
    }

    // -----------------------------------------------------------------------
    // Archetype 5: Surface Drifter (Bluebottles)
    // Water-surface clamp with gentle swell bobbing and ambient drift
    // -----------------------------------------------------------------------

    private void UpdateSurfaceDrifter()
    {
        // Locked at water surface (Y = -0.45m) with gentle swell bobbing
        float bobbing = Mathf.Sin(_cycleTimer * 1.5f) * 0.12f;
        float targetY = -0.45f + bobbing;

        Vector3 pos = _rb.position;
        pos.y = targetY;

        // Drift slowly with ambient ocean current
        pos += _ambientCurrent * (0.25f * Time.fixedDeltaTime);
        _rb.MovePosition(pos);

        // Gentle roll bobbing
        float roll = Mathf.Sin(_cycleTimer * 1.2f) * 6f;
        _rb.MoveRotation(Quaternion.Euler(0f, transform.eulerAngles.y, roll));
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

        // 2. Physical propulsion along body forward
        float forwardDot = Vector3.Dot(transform.forward, steerDir.normalized);
        float turnDrag = Mathf.Clamp01((forwardDot + 1f) * 0.5f);
        float forwardSpeed = _currentSpeed * Mathf.Lerp(0.5f, 1.0f, turnDrag);

        Vector3 moveStep = transform.forward * forwardSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + moveStep);

        // 3. Smooth heading rotation
        ApplySteeringRotation(steerDir, 0f, full3D: false, maxPitch: 30f);

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

    private void ApplySteeringRotation(Vector3 dir, float extraPitch, bool full3D = false, float maxPitch = 45f)
    {
        if (dir.sqrMagnitude < 0.001f) return;

        float maxTurnRate = _steering != null ? _steering.TurnSpeed : 65f;
        float yawOffset = _steering != null ? _steering.ModelYawOffset : 0f;

        Quaternion targetRot;

        if (full3D)
        {
            Vector3 lookDir = dir.normalized;
            // Prevent gimbal singularity if aiming straight up or down
            Vector3 upVector = Mathf.Abs(lookDir.y) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion baseLook = Quaternion.LookRotation(lookDir, upVector);
            targetRot = baseLook * Quaternion.Euler(extraPitch, yawOffset, _currentBankAngle);
        }
        else
        {
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude < 0.001f)
            {
                flatDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
            }

            // Calculate vertical pitch from 3D direction vector
            // In Unity Euler angles: negative pitch tilts nose up (+Y), positive tilts nose down (-Y)
            float verticalPitch = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                float horizontalDist = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                float angleRad = Mathf.Atan2(dir.y, horizontalDist);
                verticalPitch = Mathf.Clamp(-angleRad * Mathf.Rad2Deg, -maxPitch, maxPitch);
            }

            Quaternion baseLook = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            targetRot = baseLook * Quaternion.Euler(verticalPitch + extraPitch, yawOffset, _currentBankAngle);
        }

        // Natural, smooth hydrodynamic turning without instant snapping
        float angleDelta = Quaternion.Angle(_rb.rotation, targetRot);
        if (angleDelta > 0.01f)
        {
            // For large angle changes, turn at maxTurnRate (degrees/sec)
            // For small angle changes, gently ease in with a minimum floor
            float effectiveRate = Mathf.Min(maxTurnRate, Mathf.Max(12f, angleDelta * 2.0f));
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, effectiveRate * Time.fixedDeltaTime));
        }
    }

    private void UpdateBankingAndYaw()
    {
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
