using UnityEngine;

/// <summary>
/// Per-creature state machine that drives ContextSteering.
/// 
/// States:
///   Wandering  - default; picks random targets within wanderRadius of spawn.
///   Fleeing    - triggered when isShy and submarine enters fleeRange.
///                ContextSteering goal is set AWAY from the threat.
///   Returning  - after fleeing far enough, creature returns to spawn centroid.
///
/// Attach to: any mobile creature prefab alongside ContextSteering + Rigidbody.
/// Initialize via: speciesAI.Initialize(data, spawnCenter) called by SpeciesSpawner.
/// </summary>
[RequireComponent(typeof(ContextSteering))]
public class SpeciesAI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // State machine
    // -----------------------------------------------------------------------

    private enum AIState { Wandering, Fleeing, Returning }
    private AIState _state = AIState.Wandering;

    // -----------------------------------------------------------------------
    // References
    // -----------------------------------------------------------------------

    private ContextSteering _steering;
    private Transform       _playerTransform;

    // -----------------------------------------------------------------------
    // Wander data
    // -----------------------------------------------------------------------

    private Vector3 _spawnCenter;
    private Vector3 _wanderTarget;
    private float   _wanderArrivalThreshold = 2.5f;

    // -----------------------------------------------------------------------
    // Public state
    // -----------------------------------------------------------------------

    public SpeciesData Data        { get; private set; }
    public bool IsFleeing          => _state == AIState.Fleeing;

    private bool _isDiscovered;
    public bool IsDiscovered
    {
        get => _isDiscovered;
        set
        {
            _isDiscovered = value;
            var trackable = GetComponent<SonarTrackable>();
            if (trackable != null) trackable.IsDiscovered = value;
        }
    }

    // -----------------------------------------------------------------------
    // Initialization (called by SpeciesSpawner after AddComponent)
    // -----------------------------------------------------------------------

    public void Initialize(SpeciesData data, Vector3 spawnCenter)
    {
        Data         = data;
        _spawnCenter = spawnCenter;
        if (_steering == null) _steering = GetComponent<ContextSteering>();
        if (_steering != null && data != null)
        {
            _steering.MoveSpeed = data.moveSpeed;
            _steering.TurnSpeed = data.turnSpeed > 0f ? data.turnSpeed : 65f;
            _steering.ModelYawOffset = data.modelYawOffset;
        }

        var locomotion = GetComponent<CreatureLocomotion>();
        if (locomotion != null && data != null)
        {
            locomotion.Initialize(data);
        }

        UpdateDynamicArrivalThreshold();
        PickNewWanderTarget();
    }

    // -----------------------------------------------------------------------
    // External commands
    // -----------------------------------------------------------------------

    /// <summary>
    /// Force the creature into Fleeing state.
    /// Called by ScannerSystem when a scan attempt fails on a shy species.
    /// </summary>
    public void Flee(Vector3 threatWorldPosition)
    {
        if (_state == AIState.Fleeing) return;
        _state = AIState.Fleeing;
        if (_steering != null) _steering.MoveSpeed = Data != null ? Data.fleeSpeed : 5f;
        SetFleeGoalAwayFrom(threatWorldPosition);
    }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _steering    = GetComponent<ContextSteering>();
        _spawnCenter = transform.position;
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;

        UpdateDynamicArrivalThreshold();

        if (Data == null)
        {
            PickNewWanderTarget();
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case AIState.Wandering:  UpdateWander();  break;
            case AIState.Fleeing:    UpdateFlee();    break;
            case AIState.Returning:  UpdateReturn();  break;
        }

        // Autonomous shy-flee: if submarine enters fleeRange, flee independently of scanner
        if (_state == AIState.Wandering
            && Data != null && Data.isShy
            && _playerTransform != null)
        {
            if (Vector3.Distance(transform.position, _playerTransform.position) < Data.fleeRange)
                Flee(_playerTransform.position);
        }
    }

    // -----------------------------------------------------------------------
    // State updates
    // -----------------------------------------------------------------------

    private void UpdateWander()
    {
        if (_steering != null) _steering.SetGoal(_wanderTarget);

        if (Vector3.Distance(transform.position, _wanderTarget) < _wanderArrivalThreshold)
            PickNewWanderTarget();
    }

    private void UpdateFlee()
    {
        if (_playerTransform != null)
            SetFleeGoalAwayFrom(_playerTransform.position);

        float distFromSpawn = Vector3.Distance(transform.position, _spawnCenter);
        float threshold     = Data != null ? Data.returnThreshold : 40f;
        if (distFromSpawn > threshold)
            EnterReturning();
    }

    private void UpdateReturn()
    {
        if (_steering != null) _steering.SetGoal(_spawnCenter);
        if (Vector3.Distance(transform.position, _spawnCenter) < _wanderArrivalThreshold * 1.5f)
            EnterWandering();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void UpdateDynamicArrivalThreshold()
    {
        float scaleMag = transform.lossyScale.magnitude;
        if (Data != null && Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser)
        {
            // Pelagic cruisers (sharks, dolphins) move continuously with wide turning circles;
            // generous arrival threshold prevents tight 360 circling
            _wanderArrivalThreshold = Mathf.Max(16f, scaleMag * 2.5f);
        }
        else
        {
            _wanderArrivalThreshold = Mathf.Max(2.5f, scaleMag * 0.8f);
        }
    }

    private DepthTracker _cachedDepthTracker;

    private void PickNewWanderTarget()
    {
        float radius = Data != null ? Data.wanderRadius : 15f;
        radius = Mathf.Max(radius, _wanderArrivalThreshold * 2.0f);

        Vector3 candidate;

        if (Data != null && Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser)
        {
            // Open-water cruisers cruise forward along expansive patrol vectors (preventing 180/360 spin traps)
            float distFromCenter = Vector3.Distance(transform.position, _spawnCenter);
            Vector3 patrolHeading;

            if (distFromCenter > radius * 0.85f)
            {
                // Reached outer boundary of patrol territory:
                // Smoothly bank inward by curving horizontally toward spawn center (deflect by up to 40 deg).
                // Never perform an abrupt 180 hairpin turn behind the animal!
                Vector3 toCenter = (_spawnCenter - transform.position).normalized;
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 flatToCenter = Vector3.ProjectOnPlane(toCenter, Vector3.up).normalized;
                if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
                if (flatToCenter.sqrMagnitude < 0.01f) flatToCenter = -flatForward;

                float turnAngle = Vector3.SignedAngle(flatForward, flatToCenter, Vector3.up);
                float bankYaw = Mathf.Clamp(turnAngle, -40f, 40f);
                float pitchDev = Random.Range(-4f, 4f);
                patrolHeading = Quaternion.Euler(pitchDev, bankYaw, 0f) * transform.forward;
            }
            else
            {
                // Expansive straight cruise: gentle sweep (+-20 deg yaw, +-5 deg pitch)
                float yawDev = Random.Range(-20f, 20f);
                float pitchDev = Random.Range(-5f, 5f);
                patrolHeading = Quaternion.Euler(pitchDev, yawDev, 0f) * transform.forward;
            }

            // Long, majestic patrol sweeps before altering heading (65m to 130m)
            float minStep = Mathf.Max(60f, radius * 0.55f);
            float maxStep = Mathf.Max(95f, radius * 0.90f);
            float forwardStep = Random.Range(minStep, maxStep);
            candidate = transform.position + patrolHeading.normalized * forwardStep;
        }
        else
        {
            // Standard point-based wander for reef/benthic/pulsatile species
            float yRange = (Data != null && Data.locomotionArchetype == LocomotionArchetype.PulsatileJetter)
                ? Mathf.Min(8f, radius * 0.4f)
                : 3f;

            Vector3 offset = new Vector3(
                Random.Range(-radius, radius),
                Random.Range(-yRange, yRange),
                Random.Range(-radius, radius));

            if (offset.sqrMagnitude < 9f)
                offset = offset.normalized * 4f;

            candidate = _spawnCenter + offset;
        }

        // Ensure wander target is safely above the ocean floor
        float floorY = -300f;
        var tg = FindFirstObjectByType<TerrainGenerator>();
        if (tg != null && tg.HasGenerated)
        {
            floorY = tg.SampleHeight(candidate.x, candidate.z);
        }
        else if (Physics.Raycast(new Vector3(candidate.x, 10f, candidate.z), Vector3.down, out RaycastHit hit, 500f))
        {
            floorY = hit.point.y;
        }

        float minSafeY = floorY + Mathf.Max(2.5f, _wanderArrivalThreshold);
        candidate.y = Mathf.Max(candidate.y, minSafeY);

        // Retrieve safe boundary bounds (ensures creature never wanders outside player reach)
        float safeHalfW  = _steering != null ? _steering.SafeHalfWidth  : 275f;
        float safeHalfL  = _steering != null ? _steering.SafeHalfLength : 275f;
        float safeTop    = _steering != null ? _steering.SafeTopY       : -1.5f;
        float safeBottom = _steering != null ? _steering.SafeBottomY    : -182f;

        candidate.x = Mathf.Clamp(candidate.x, -safeHalfW, safeHalfW);
        candidate.z = Mathf.Clamp(candidate.z, -safeHalfL, safeHalfL);
        candidate.y = Mathf.Clamp(candidate.y, Mathf.Max(minSafeY, safeBottom), safeTop);

        _wanderTarget = candidate;
    }

    private void SetFleeGoalAwayFrom(Vector3 threat)
    {
        Vector3 rawFleeDir = (transform.position - threat).normalized;
        if (rawFleeDir == Vector3.zero) rawFleeDir = transform.forward;

        float safeHalfW  = _steering != null ? _steering.SafeHalfWidth  : 275f;
        float safeHalfL  = _steering != null ? _steering.SafeHalfLength : 275f;
        float safeTop    = _steering != null ? _steering.SafeTopY       : -1.5f;
        float safeBottom = _steering != null ? _steering.SafeBottomY    : -182f;

        Vector3 candidateFleeGoal = transform.position + rawFleeDir * 25f;
        Vector3 deflectedFleeDir = rawFleeDir;

        // Boundary deflection: if fleeing directly toward a perimeter wall, deflect along the wall
        // with an inward bias so the creature loops safely inside the scannable zone
        if (Mathf.Abs(candidateFleeGoal.x) > safeHalfW)
        {
            float wallSign = Mathf.Sign(candidateFleeGoal.x);
            deflectedFleeDir.x = -wallSign * 0.5f;
            deflectedFleeDir.z = Mathf.Sign(deflectedFleeDir.z != 0 ? deflectedFleeDir.z : 1f) * 0.85f;
        }

        if (Mathf.Abs(candidateFleeGoal.z) > safeHalfL)
        {
            float wallSign = Mathf.Sign(candidateFleeGoal.z);
            deflectedFleeDir.z = -wallSign * 0.5f;
            deflectedFleeDir.x = Mathf.Sign(deflectedFleeDir.x != 0 ? deflectedFleeDir.x : 1f) * 0.85f;
        }

        deflectedFleeDir.Normalize();
        Vector3 fleeGoal = transform.position + deflectedFleeDir * 25f;

        // Clamp goal to guarantee it remains inside player reach
        fleeGoal.x = Mathf.Clamp(fleeGoal.x, -safeHalfW, safeHalfW);
        fleeGoal.z = Mathf.Clamp(fleeGoal.z, -safeHalfL, safeHalfL);
        fleeGoal.y = Mathf.Clamp(fleeGoal.y, safeBottom, safeTop);

        if (_steering != null) _steering.SetGoal(fleeGoal);
    }

    private void EnterReturning()
    {
        _state = AIState.Returning;
        if (_steering != null) _steering.MoveSpeed = Data != null ? Data.moveSpeed : 2f;
    }

    private void EnterWandering()
    {
        _state = AIState.Wandering;
        PickNewWanderTarget();
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        float r = Data != null ? Data.wanderRadius : 15f;
        Gizmos.color = new Color(0, 1, 1, 0.25f);
        Gizmos.DrawWireSphere(_spawnCenter, r);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_wanderTarget, 0.5f);
    }
}
