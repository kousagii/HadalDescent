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
            _steering.ModelYawOffset = data.modelYawOffset;
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
        _wanderArrivalThreshold = Mathf.Max(2.5f, scaleMag * 0.8f);
    }

    private DepthTracker _cachedDepthTracker;

    private void PickNewWanderTarget()
    {
        float radius = Data != null ? Data.wanderRadius : 15f;
        radius = Mathf.Max(radius, _wanderArrivalThreshold * 2.0f);

        // Pick a target that is at least a minimum distance away
        Vector3 offset = new Vector3(
            Random.Range(-radius, radius),
            Random.Range(-3f, 3f),
            Random.Range(-radius, radius));

        if (offset.sqrMagnitude < 9f)
            offset = offset.normalized * 4f;

        Vector3 candidate = _spawnCenter + offset;

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
        candidate.y = Mathf.Min(candidate.y, -1.5f); // Stay below water surface

        // Clamp to zone depth boundaries with safe margin so creatures never
        // wander or flee into the boundary trigger volume
        if (_cachedDepthTracker == null)
            _cachedDepthTracker = FindFirstObjectByType<DepthTracker>();

        if (_cachedDepthTracker != null)
        {
            float safeBottom = _cachedDepthTracker.ZoneBottomY + 16f;
            float safeTop    = _cachedDepthTracker.ZoneTopY - 2.5f;
            candidate.y = Mathf.Clamp(candidate.y, safeBottom, safeTop);
        }

        _wanderTarget = candidate;
    }

    private void SetFleeGoalAwayFrom(Vector3 threat)
    {
        Vector3 fleeDir = (transform.position - threat).normalized;
        if (fleeDir == Vector3.zero) fleeDir = Vector3.forward;

        Vector3 fleeGoal = transform.position + fleeDir * 25f;

        // Ensure flee goal also respects zone boundaries so creatures don't flee past boundaries
        if (_cachedDepthTracker == null)
            _cachedDepthTracker = FindFirstObjectByType<DepthTracker>();

        if (_cachedDepthTracker != null)
        {
            float safeBottom = _cachedDepthTracker.ZoneBottomY + 16f;
            float safeTop    = _cachedDepthTracker.ZoneTopY - 2.5f;
            fleeGoal.y = Mathf.Clamp(fleeGoal.y, safeBottom, safeTop);
        }

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
