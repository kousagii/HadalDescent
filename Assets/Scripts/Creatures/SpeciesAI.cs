using UnityEngine;

/// <summary>
/// Per-creature state machine that drives ContextSteering.
/// 
/// States:
///   Wandering  — default; picks random targets within wanderRadius of spawn.
///   Fleeing    — triggered when isShy and submarine enters fleeRange.
///                ContextSteering goal is set AWAY from the threat.
///   Returning  — after fleeing far enough, creature returns to spawn centroid.
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
    private Transform        _playerTransform;

    // -----------------------------------------------------------------------
    // Wander data
    // -----------------------------------------------------------------------

    private Vector3 _spawnCenter;
    private Vector3 _wanderTarget;
    private float   _wanderArrivalThreshold = 1.5f;

    // -----------------------------------------------------------------------
    // Public state
    // -----------------------------------------------------------------------

    public SpeciesData Data        { get; private set; }
    public bool        IsDiscovered { get; set; }

    // -----------------------------------------------------------------------
    // Initialization (called by SpeciesSpawner after AddComponent)
    // -----------------------------------------------------------------------

    public void Initialize(SpeciesData data, Vector3 spawnCenter)
    {
        Data         = data;
        _spawnCenter = spawnCenter;
        _steering.MoveSpeed = data.moveSpeed;
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
        _steering.MoveSpeed = Data != null ? Data.fleeSpeed : 5f;
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
        // Cache player transform (tagged "Player" — the Submarine root object)
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;

        if (Data == null)
        {
            // If not initialized by SpeciesSpawner yet, pick a wander target anyway
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
        _steering.SetGoal(_wanderTarget);

        if (Vector3.Distance(transform.position, _wanderTarget) < _wanderArrivalThreshold)
            PickNewWanderTarget();
    }

    private void UpdateFlee()
    {
        // Keep updating flee direction while player is still close
        if (_playerTransform != null)
            SetFleeGoalAwayFrom(_playerTransform.position);

        float distFromSpawn = Vector3.Distance(transform.position, _spawnCenter);
        float threshold     = Data != null ? Data.returnThreshold : 40f;
        if (distFromSpawn > threshold)
            EnterReturning();
    }

    private void UpdateReturn()
    {
        _steering.SetGoal(_spawnCenter);
        if (Vector3.Distance(transform.position, _spawnCenter) < 3f)
            EnterWandering();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void PickNewWanderTarget()
    {
        float radius = Data != null ? Data.wanderRadius : 15f;
        _wanderTarget = _spawnCenter + new Vector3(
            Random.Range(-radius, radius),
            Random.Range(-3f, 3f),
            Random.Range(-radius, radius));
    }

    private void SetFleeGoalAwayFrom(Vector3 threat)
    {
        Vector3 fleeDir = (transform.position - threat).normalized;
        _steering.SetGoal(transform.position + fleeDir * 25f);
    }

    private void EnterReturning()
    {
        _state = AIState.Returning;
        _steering.MoveSpeed = Data != null ? Data.moveSpeed : 2f;
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
