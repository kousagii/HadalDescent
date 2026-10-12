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
    private Rigidbody       _rb;
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

    /// <summary>Spawn center origin used as home anchor for wander and return.</summary>
    public Vector3 SpawnCenter => _spawnCenter;

    // -----------------------------------------------------------------------
    // Initialization (called by SpeciesSpawner after AddComponent)
    // -----------------------------------------------------------------------

    private float _speedMultiplier = 1f;

    public void Initialize(SpeciesData data, Vector3 spawnCenter)
    {
        Data         = data;
        _spawnCenter = spawnCenter;
        if (_steering == null) _steering = GetComponent<ContextSteering>();

        float scaleRatio = 1f;
        if (data != null && data.modelPrefab != null && data.modelPrefab.transform.localScale.magnitude > 0.01f)
        {
            scaleRatio = transform.localScale.magnitude / data.modelPrefab.transform.localScale.magnitude;
        }
        else if (data != null && data.placeholderScale.magnitude > 0.01f)
        {
            scaleRatio = transform.localScale.magnitude / data.placeholderScale.magnitude;
        }

        // Biological speed scaling: larger specimens cruise slightly faster; smaller specimens are more agile
        _speedMultiplier = Mathf.Clamp(Mathf.Pow(scaleRatio, 0.35f), 0.75f, 1.35f);
        float turnMul    = Mathf.Clamp(Mathf.Pow(scaleRatio, -0.25f), 0.8f, 1.25f);

        if (_steering != null && data != null)
        {
            _steering.MoveSpeed = data.moveSpeed * _speedMultiplier;
            _steering.TurnSpeed = (data.turnSpeed > 0f ? data.turnSpeed : 65f) * turnMul;
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
        _isWaitingAtWanderTarget = false;
        if (_state == AIState.Fleeing) return;
        _state = AIState.Fleeing;
        if (_steering != null) _steering.MoveSpeed = (Data != null ? Data.fleeSpeed : 5f) * _speedMultiplier;
        SetFleeGoalAwayFrom(threatWorldPosition);
    }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rb          = GetComponent<Rigidbody>();
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

    private float _wanderTargetDuration = 0f;
    private float _wanderWaitTimer = 0f;
    private bool _isWaitingAtWanderTarget = false;

    private void UpdateWander()
    {
        bool isBenthic = Data != null && Data.locomotionArchetype == LocomotionArchetype.BenthicFollower;

        // 1. Resting / Pausing on the seabed at destination (benthic species)
        if (_isWaitingAtWanderTarget)
        {
            _wanderWaitTimer -= Time.deltaTime;
            if (_steering != null) _steering.MoveSpeed = 0f;

            if (_wanderWaitTimer <= 0f)
            {
                _isWaitingAtWanderTarget = false;
                if (_steering != null && Data != null)
                {
                    _steering.MoveSpeed = Data.moveSpeed * _speedMultiplier;
                }
                PickNewWanderTarget();
            }
            return;
        }

        if (_steering != null) _steering.SetGoal(_wanderTarget);

        _wanderTargetDuration += Time.deltaTime;
        float dist = isBenthic
            ? Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(_wanderTarget.x, _wanderTarget.z))
            : Vector3.Distance(transform.position, _wanderTarget);

        // 2. Smooth arrival deceleration as benthic creatures close in on the waypoint
        if (isBenthic && _steering != null && Data != null)
        {
            float slowRadius = _wanderArrivalThreshold * 2.2f;
            if (dist < slowRadius)
            {
                float t = Mathf.Clamp01((dist - _wanderArrivalThreshold) / (slowRadius - _wanderArrivalThreshold));
                float minGlideSpeed = Data.moveSpeed * _speedMultiplier * 0.35f;
                _steering.MoveSpeed = Mathf.Lerp(minGlideSpeed, Data.moveSpeed * _speedMultiplier, t);
            }
            else
            {
                _steering.MoveSpeed = Data.moveSpeed * _speedMultiplier;
            }
        }

        bool isHoverBurst = Data != null && Data.locomotionArchetype == LocomotionArchetype.HoverBurst;

        // 3. Arrive when reaching threshold OR if target is held for over 14 seconds (prevents stationary freezing)
        if (dist < _wanderArrivalThreshold || _wanderTargetDuration > 14f)
        {
            _wanderTargetDuration = 0f;
            if (isBenthic)
            {
                // Pause peacefully on the seabed for 2.0 to 4.5 seconds like wild rays/snails
                _isWaitingAtWanderTarget = true;
                _wanderWaitTimer = Random.Range(2.0f, 4.5f);
                if (_steering != null) _steering.MoveSpeed = 0f;
            }
            else
            {
                // Pelagic cruisers and HoverBurst (clownfish) continuously cruise without dead pauses
                PickNewWanderTarget();
            }
        }
        else if (!isBenthic && Data != null && Data.preferredBiome == BiomeBand.OpenWater && _wanderTargetDuration > 0.8f)
        {
            // 4. Runtime Habitat Guard: If an open-water pelagic creature (e.g. dolphin, shark) strays into
            // a prohibited biome (Hard coral reef or shallow reef plateau), promptly pick a new goal steering back to open water.
            var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
            if (tg != null && tg.HasGenerated)
            {
                BiomeBand curBiome = tg.GetBiomeAt(transform.position.x, transform.position.z);
                float curFloorY = tg.SampleHeight(transform.position.x, transform.position.z);
                if (curBiome == BiomeBand.Hard || curBiome == BiomeBand.Rock || curFloorY > -28.0f)
                {
                    PickNewWanderTarget();
                }
            }
        }
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
        else if (Data != null && Data.locomotionArchetype == LocomotionArchetype.HoverBurst)
        {
            // HoverBurst (clownfish) smoothly transitions within 2.0m without overshooting or oscillation
            _wanderArrivalThreshold = Mathf.Max(2.0f, scaleMag * 0.8f);
        }
        else
        {
            _wanderArrivalThreshold = Mathf.Max(2.5f, scaleMag * 0.8f);
        }
    }

    private DepthTracker _cachedDepthTracker;

    private void PickNewWanderTarget()
    {
        _wanderTargetDuration = 0f;
        bool isPelagicCruiser = Data != null && Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser;
        bool isBenthicFollower = Data != null && Data.locomotionArchetype == LocomotionArchetype.BenthicFollower;
        bool isSurfaceDrifter = Data != null && Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;
        bool isClownfish = Data != null && (Data.speciesId == "clownfish_001" || Data.commonName.ToLower().Contains("clownfish"));

        var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();

        float safeHalfW  = _steering != null ? _steering.SafeHalfWidth  : 240f;
        float safeHalfL  = _steering != null ? _steering.SafeHalfLength : 240f;
        float safeTop    = _steering != null ? _steering.SafeTopY       : (isSurfaceDrifter ? -0.2f : -3.0f);
        float safeBottom = _steering != null ? _steering.SafeBottomY    : -180f;

        float maxWanderHalfW = Mathf.Max(50f, safeHalfW - 35f);
        float maxWanderHalfL = Mathf.Max(50f, safeHalfL - 35f);

        float radius = Data != null ? Data.wanderRadius : 15f;
        // Cap wander radius so species remain localized and never stray towards survey perimeter
        radius = Mathf.Min(radius, safeHalfW * 0.40f);
        radius = Mathf.Max(radius, _wanderArrivalThreshold * 1.5f);

        Vector3 candidate;

        if (isPelagicCruiser)
        {
            Vector3 toCenter = new Vector3(_spawnCenter.x - transform.position.x, 0f, _spawnCenter.z - transform.position.z);
            float distFromOrigin = toCenter.magnitude;
            bool foundValid = false;
            Vector3 chosenCandidate = transform.position;

            // Attempt up to 12 headings to find an exploratory path remaining inside the preferred pelagic biome & depth
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 patrolHeading;
                if (distFromOrigin > radius * 0.75f || attempt >= 6)
                {
                    // Steer back towards spawn center when drifting far or when initial forward attempts breach habitat
                    float bankYaw = Random.Range(-30f, 30f);
                    float pitchDev = Random.Range(-3f, 3f);
                    Vector3 baseHeading = (toCenter.normalized * 0.75f + transform.forward * 0.25f).normalized;
                    patrolHeading = Quaternion.Euler(pitchDev, bankYaw, 0f) * baseHeading;
                }
                else if (attempt == 0 && Random.value < 0.45f)
                {
                    float bankYaw = (Random.value > 0.5f ? 1f : -1f) * Random.Range(25f, 50f);
                    float pitchDev = Random.Range(-6f, 6f);
                    patrolHeading = Quaternion.Euler(pitchDev, bankYaw, 0f) * transform.forward;
                }
                else
                {
                    float yawDev = Random.Range(-35f, 35f) * (attempt + 1) * 0.5f;
                    float pitchDev = Random.Range(-4f, 4f);
                    patrolHeading = Quaternion.Euler(pitchDev, yawDev, 0f) * transform.forward;
                }

                float minStep = Mathf.Max(25f, radius * 0.35f);
                float maxStep = Mathf.Min(55f, radius * 0.65f);
                float forwardStep = Random.Range(minStep, maxStep);
                Vector3 testCand = transform.position + patrolHeading.normalized * forwardStep;

                testCand.x = Mathf.Clamp(testCand.x, -maxWanderHalfW, maxWanderHalfW);
                testCand.z = Mathf.Clamp(testCand.z, -maxWanderHalfL, maxWanderHalfL);

                if (IsValidPelagicBiome(testCand, tg))
                {
                    chosenCandidate = testCand;
                    foundValid = true;
                    break;
                }
            }

            if (!foundValid)
            {
                // Steer directly toward _spawnCenter (which was validated at spawn time)
                Vector3 returnDir = toCenter.normalized;
                if (returnDir.sqrMagnitude < 0.01f) returnDir = transform.forward;
                chosenCandidate = transform.position + returnDir * Mathf.Min(35f, distFromOrigin);
            }

            candidate = chosenCandidate;
        }
        else if (isClownfish)
        {
            // Clownfish can wander outside the sea anemone across the reef (wide exploratory range)
            Vector3 toSpawn = _spawnCenter - transform.position;
            float distFromSpawn = toSpawn.magnitude;
            // When far from territory, smoothly curve exploration back toward home reef area
            Vector3 wanderOrigin = (distFromSpawn > radius * 1.5f)
                ? (transform.position + toSpawn.normalized * (radius * 0.45f))
                : transform.position;

            Vector2 hOffset = Random.insideUnitCircle * Random.Range(3.5f, radius);
            candidate = new Vector3(wanderOrigin.x + hOffset.x, transform.position.y, wanderOrigin.z + hOffset.y);
        }
        else if (isBenthicFollower)
        {
            // Benthic crawlers (Lobsters, Cone Snails, Rays) explore seabed terrain within their depth range and preferred biome
            Vector3 bestCandidate = _spawnCenter;
            bool foundValidCandidate = false;
            float bestScore = float.MinValue;
            int speciesMask = LayerMask.GetMask("Creatures", "Default");

            Vector3 trueFacing = _steering != null ? _steering.TrueForward : transform.forward;
            Vector3 flatFacing = Vector3.ProjectOnPlane(trueFacing, Vector3.up).normalized;
            if (flatFacing.sqrMagnitude < 0.01f) flatFacing = transform.forward;

            // Organic exploratory anchor: explore outward from current position while tethered to spawnCenter
            Vector3 toSpawn = _spawnCenter - transform.position;
            float distFromSpawn = toSpawn.magnitude;
            Vector3 sampleOrigin = (distFromSpawn > radius * 1.25f)
                ? (transform.position + toSpawn.normalized * (radius * 0.45f))
                : transform.position;

            for (int attempt = 0; attempt < 28; attempt++)
            {
                // Sample outward around sampleOrigin
                Vector2 hOffset = Random.insideUnitCircle * Random.Range(Mathf.Max(3.5f, radius * 0.3f), radius);
                Vector3 testPos = new Vector3(sampleOrigin.x + hOffset.x, 0f, sampleOrigin.z + hOffset.y);
                testPos.x = Mathf.Clamp(testPos.x, -safeHalfW, safeHalfW);
                testPos.z = Mathf.Clamp(testPos.z, -safeHalfL, safeHalfL);

                float fy = tg != null && tg.HasGenerated ? tg.SampleHeight(testPos.x, testPos.z) : _spawnCenter.y;

                // Strictly reject points outside species depth band
                if (fy > safeTop + 0.5f || fy < safeBottom - 0.5f)
                    continue;

                BiomeBand b = tg != null && tg.HasGenerated && Data != null ? tg.GetBiomeAt(testPos.x, testPos.z) : (Data != null ? Data.preferredBiome : BiomeBand.OpenWater);
                bool biomeMatches = (Data == null || b == Data.preferredBiome);
                bool biomeCompatible = (Data != null && SpeciesSpawner.IsCompatibleBiome(b, Data.preferredBiome));
                if (!biomeMatches && !biomeCompatible)
                    continue;

                // Strictly reject candidate points inside Habitat Landmark formations
                if (HabitatLandmarkSpawner.IsInsideLandmarkZone(new Vector3(testPos.x, fy, testPos.z), 4.0f))
                    continue;

                // Reject candidate points placed inside massive solid boulders or landmark structures (allow flexible seagrass & low props)
                int propMask = LayerMask.GetMask("Default", "Terrain");
                var hits = Physics.OverlapSphere(new Vector3(testPos.x, fy + 0.4f, testPos.z), 1.0f, propMask, QueryTriggerInteraction.Collide);
                bool insideSolidObstacle = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    var hc = hits[h];
                    if (hc != null && ContextSteering.IsEnvironmentProp(hc) && !hc.isTrigger && hc.bounds.size.y > 1.8f)
                    {
                        insideSolidObstacle = true;
                        break;
                    }
                }
                if (insideSolidObstacle) continue;

                // Dynamic separation: reject wander targets that are within 3.8m of any other active creature
                var nearbyCols = Physics.OverlapSphere(new Vector3(testPos.x, fy + 0.3f, testPos.z), 3.8f, speciesMask, QueryTriggerInteraction.Collide);
                bool nearOtherSpecies = false;
                for (int s = 0; s < nearbyCols.Length; s++)
                {
                    var sc = nearbyCols[s];
                    if (sc != null && sc.transform.root != transform.root && (sc.CompareTag("Species") || sc.gameObject.layer == LayerMask.NameToLayer("Creatures")))
                    {
                        nearOtherSpecies = true;
                        break;
                    }
                }
                if (nearOtherSpecies) continue;

                float score = biomeMatches ? 100f : 40f;
                float targetDepthY = (safeTop + safeBottom) * 0.5f;
                score -= Mathf.Abs(fy - targetDepthY) * 2f;

                // Forward bias: prefer points roughly in the direction creature is already facing
                Vector3 toCandidate = (new Vector3(testPos.x, 0f, testPos.z) - new Vector3(transform.position.x, 0f, transform.position.z)).normalized;
                float forwardDot = Vector3.Dot(flatFacing, toCandidate);
                score += forwardDot * 25f; // Rewards continuing forward, gently penalizes doing an immediate 180° hairpin

                // Prefer targets that encourage meaningful movement away from current position (at least 3.5m)
                float distFromCur = Vector2.Distance(new Vector2(testPos.x, testPos.z), new Vector2(transform.position.x, transform.position.z));
                if (distFromCur < _wanderArrivalThreshold * 1.3f)
                {
                    score -= 50f;
                }
                else
                {
                    score += Mathf.Min(distFromCur * 2f, 30f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = new Vector3(testPos.x, fy, testPos.z);
                    foundValidCandidate = true;
                }
            }

            if (!foundValidCandidate)
            {
                // If all attempts failed, project forward along TRUE heading with a gentle random yaw arc
                float stepDist = Mathf.Max(5.0f, radius * 0.45f);
                Vector3 fwdDir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * flatFacing;
                Vector3 fwdCandidate = transform.position + fwdDir * stepDist;
                fwdCandidate.x = Mathf.Clamp(fwdCandidate.x, -safeHalfW, safeHalfW);
                fwdCandidate.z = Mathf.Clamp(fwdCandidate.z, -safeHalfL, safeHalfL);
                bestCandidate = fwdCandidate;
            }

            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(bestCandidate.x, bestCandidate.z) : bestCandidate.y;
            float offset = Data != null && Data.benthicSurfaceOffset > 0f ? Data.benthicSurfaceOffset : 0.15f;
            bestCandidate.y = floorY + offset;
            candidate = bestCandidate;
        }
        else
        {
            // Standard point-based wander for reef/pulsatile species
            float yRange = (Data != null && Data.locomotionArchetype == LocomotionArchetype.PulsatileJetter)
                ? Mathf.Min(8f, radius * 0.4f)
                : 3f;

            Vector3 bestCand = _spawnCenter;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-radius, radius),
                    Random.Range(-yRange, yRange),
                    Random.Range(-radius, radius));

                if (offset.sqrMagnitude < 9f)
                    offset = offset.normalized * 4f;

                Vector3 testCand = _spawnCenter + offset;
                if (IsValidPelagicBiome(testCand, tg))
                {
                    bestCand = testCand;
                    break;
                }
            }

            candidate = bestCand;
        }

        // Horizontal scannable boundaries — keep goals well within survey perimeter (margin 35m) so creatures never aim into walls
        candidate.x = Mathf.Clamp(candidate.x, -maxWanderHalfW, maxWanderHalfW);
        candidate.z = Mathf.Clamp(candidate.z, -maxWanderHalfL, maxWanderHalfL);

        if (isSurfaceDrifter)
        {
            candidate.y = -1.15f;
        }
        else if (isClownfish)
        {
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(candidate.x, candidate.z) : _spawnCenter.y;
            // Swim freely around the reef outside the anemone (0.8m to 3.0m above seabed)
            float minHoverY = floorY + 0.8f;
            float maxHoverY = Mathf.Min(safeTop, floorY + 3.0f);
            candidate.y = Random.Range(minHoverY, Mathf.Max(minHoverY, maxHoverY));
        }
        else if (isBenthicFollower)
        {
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(candidate.x, candidate.z) : candidate.y;
            float offset = Data != null && Data.benthicSurfaceOffset > 0f ? Data.benthicSurfaceOffset : 0.15f;
            candidate.y = floorY + offset;
        }
        else
        {
            // Pelagic & swimming species: stay safely above floor and inside depth band (strictly below 1.5m depth, never above 0m)
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(candidate.x, candidate.z) : -200f;
            float clearance = isPelagicCruiser ? 4.0f : 2.5f;
            float minSafeY = Mathf.Max(safeBottom, floorY + clearance);
            float maxSafeY = Mathf.Min(safeTop, -1.8f);
            if (minSafeY > maxSafeY)
            {
                candidate.y = Mathf.Lerp(floorY + 0.8f, maxSafeY, 0.5f);
            }
            else
            {
                candidate.y = Mathf.Clamp(candidate.y, minSafeY, maxSafeY);
            }
        }

        _wanderTarget = candidate;
    }

    private bool IsValidPelagicBiome(Vector3 pos, TerrainGenerator tg)
    {
        if (tg == null || !tg.HasGenerated || Data == null) return true;

        BiomeBand biome = tg.GetBiomeAt(pos.x, pos.z);
        float floorY = tg.SampleHeight(pos.x, pos.z);

        // Species preferring OpenWater (Dolphins, Sharks, Squids, Jellyfish)
        if (Data.preferredBiome == BiomeBand.OpenWater)
        {
            // Must strictly avoid Hard (Coral Reef) and Rock
            if (biome == BiomeBand.Hard || biome == BiomeBand.Rock)
                return false;

            // In shallow zones (Sunlight Zone), coral reef plateaus rise up to Y = -10m to -25m.
            // Open water pelagic cruisers must have deep water below them (floor deeper than -30m)
            if (floorY > -30.0f)
                return false;

            return true;
        }

        // For species preferring other biomes (e.g. Soft or Hard)
        return biome == Data.preferredBiome || SpeciesSpawner.IsCompatibleBiome(biome, Data.preferredBiome);
    }

    private void SetFleeGoalAwayFrom(Vector3 threat)
    {
        Vector3 rawFleeDir = (transform.position - threat).normalized;
        if (rawFleeDir == Vector3.zero)
        {
            rawFleeDir = _steering != null ? _steering.TrueForward : transform.forward;
        }

        bool isBenthic = Data != null && Data.locomotionArchetype == LocomotionArchetype.BenthicFollower;
        bool isSurfaceDrifter = Data != null && Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;

        float safeHalfW  = _steering != null ? _steering.SafeHalfWidth  : 240f;
        float safeHalfL  = _steering != null ? _steering.SafeHalfLength : 240f;
        float safeTop    = _steering != null ? _steering.SafeTopY       : (isSurfaceDrifter ? -0.2f : -3.0f);
        float safeBottom = _steering != null ? _steering.SafeBottomY    : -180f;

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

        // Clamp goal to guarantee it remains inside player reach with safe margin from walls
        float maxFleeW = Mathf.Max(50f, safeHalfW - 35f);
        float maxFleeL = Mathf.Max(50f, safeHalfL - 35f);
        fleeGoal.x = Mathf.Clamp(fleeGoal.x, -maxFleeW, maxFleeW);
        fleeGoal.z = Mathf.Clamp(fleeGoal.z, -maxFleeL, maxFleeL);

        if (isSurfaceDrifter)
        {
            fleeGoal.y = -1.15f;
        }
        else if (isBenthic)
        {
            var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(fleeGoal.x, fleeGoal.z) : transform.position.y;
            float offset = Data != null && Data.benthicSurfaceOffset > 0f ? Data.benthicSurfaceOffset : 0.15f;
            fleeGoal.y = floorY + offset;
        }
        else
        {
            var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(fleeGoal.x, fleeGoal.z) : -200f;
            float clearance = (Data != null && Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser) ? 5.5f : 3.0f;
            float minSafeY = Mathf.Max(safeBottom, floorY + clearance);
            float maxSafeY = Mathf.Min(safeTop, -3.0f);
            fleeGoal.y = Mathf.Clamp(fleeGoal.y, minSafeY, maxSafeY);
        }

        if (_steering != null) _steering.SetGoal(fleeGoal);
    }

    private void EnterReturning()
    {
        _isWaitingAtWanderTarget = false;
        _state = AIState.Returning;
        if (_steering != null) _steering.MoveSpeed = (Data != null ? Data.moveSpeed : 2f) * _speedMultiplier;
    }

    private void EnterWandering()
    {
        _isWaitingAtWanderTarget = false;
        _state = AIState.Wandering;
        if (_steering != null) _steering.MoveSpeed = (Data != null ? Data.moveSpeed : 2f) * _speedMultiplier;
        PickNewWanderTarget();
    }

    private float _lastBoundaryTurnTime = -10f;

    /// <summary>
    /// Forces the creature to turn away from a boundary (e.g. surface top boundary or perimeter wall).
    /// </summary>
    public void TurnAwayFromBoundary(Vector3 boundaryNormal)
    {
        _isWaitingAtWanderTarget = false;
        if (Time.time - _lastBoundaryTurnTime < 1.2f) return;
        _lastBoundaryTurnTime = Time.time;

        bool isSurfaceDrifter = Data != null && Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;
        if (isSurfaceDrifter)
        {
            boundaryNormal.y = 0f;
            if (boundaryNormal.sqrMagnitude < 0.01f) return;
        }
        else if (boundaryNormal.sqrMagnitude < 0.01f)
        {
            boundaryNormal = Vector3.down;
        }
        boundaryNormal.Normalize();

        // 1. Determine a turn direction that points away from the boundary and back toward safe waters
        Vector3 trueFacing = _steering != null ? _steering.TrueForward : transform.forward;
        Vector3 turnDir = Vector3.Reflect(trueFacing, boundaryNormal).normalized;
        if (isSurfaceDrifter)
        {
            turnDir.y = 0f;
            if (turnDir.sqrMagnitude < 0.01f) turnDir = boundaryNormal;
            turnDir.Normalize();

            var locomotion = GetComponent<CreatureLocomotion>();
            if (locomotion != null)
            {
                locomotion.DeflectSurfaceDrifter(boundaryNormal);
            }
        }
        else
        {
            // Vertical boundary deflection: avoid straight up/down vectors that cause gimbal lock and violent 90° flips
            if (boundaryNormal.y < -0.5f)
            {
                // Ceiling/surface boundary: preserve horizontal swimming heading with gentle downward pitch (~18°)
                Vector3 flatHeading = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                if (flatHeading.sqrMagnitude < 0.01f) flatHeading = Vector3.forward;
                turnDir = (flatHeading * 0.95f + Vector3.down * 0.31f).normalized;
            }
            else if (boundaryNormal.y > 0.5f)
            {
                // Seabed boundary: preserve horizontal heading with gentle upward pitch (~18°)
                Vector3 flatHeading = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                if (flatHeading.sqrMagnitude < 0.01f) flatHeading = Vector3.forward;
                turnDir = (flatHeading * 0.95f + Vector3.up * 0.31f).normalized;
            }
            else
            {
                if (Vector3.Dot(turnDir, boundaryNormal) < 0.3f)
                {
                    turnDir = (boundaryNormal * 0.7f + turnDir * 0.3f).normalized;
                }

                // Bias toward spawn center so it loops safely inward
                Vector3 toCenter = (_spawnCenter - transform.position).normalized;
                if (toCenter.sqrMagnitude > 0.01f && Vector3.Dot(toCenter, boundaryNormal) > -0.2f)
                {
                    turnDir = (turnDir * 0.6f + toCenter * 0.4f).normalized;
                }
            }
        }

        if (turnDir.sqrMagnitude < 0.01f)
        {
            turnDir = boundaryNormal;
        }
        turnDir.Normalize();

        // 2. Smoothly steer creature:
        // For creatures driven by CreatureLocomotion (e.g. sharks, dolphins), DO NOT snap _rb.rotation!
        // Instant rotation snapping causes catastrophic angular spikes and severe banking roll jitter.
        // Instead, allow CreatureLocomotion to naturally rotate and bank smoothly towards the new inward goal.
        var locComp = GetComponent<CreatureLocomotion>();
        if (locComp == null)
        {
            Vector3 flatTurn = new Vector3(turnDir.x, 0f, turnDir.z);
            if (flatTurn.sqrMagnitude > 0.01f)
            {
                float pitch = Mathf.Clamp(-Mathf.Atan2(turnDir.y, flatTurn.magnitude) * Mathf.Rad2Deg, -30f, 30f);
                Quaternion safeRot = Quaternion.LookRotation(flatTurn.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
                if (_rb != null)
                {
                    _rb.rotation = safeRot;
                    _rb.linearVelocity = turnDir * (_steering != null ? _steering.MoveSpeed : 2.5f);
                }
                else
                {
                    transform.rotation = safeRot;
                }
            }
        }

        // 3. Establish a new wander goal well inside the playable volume
        float wanderDist = Mathf.Max(25f, Data != null ? Data.wanderRadius : 30f);
        Vector3 newGoal = transform.position + turnDir * wanderDist;

        // Strict clamp to safe bounds with safe margin so goal is never placed near perimeter
        float safeHalfW = _steering != null ? _steering.SafeHalfWidth : 240f;
        float safeHalfL = _steering != null ? _steering.SafeHalfLength : 240f;
        float safeTop = _steering != null ? _steering.SafeTopY : (isSurfaceDrifter ? -0.2f : -3.0f);
        float safeBottom = _steering != null ? _steering.SafeBottomY : -180f;

        float maxGoalHalfW = Mathf.Max(50f, safeHalfW - 35f);
        float maxGoalHalfL = Mathf.Max(50f, safeHalfL - 35f);
        newGoal.x = Mathf.Clamp(newGoal.x, -maxGoalHalfW, maxGoalHalfW);
        newGoal.z = Mathf.Clamp(newGoal.z, -maxGoalHalfL, maxGoalHalfL);

        // When deflecting from ceiling, set goal comfortably below safeTop by at least 2.5m
        if (boundaryNormal.y < -0.5f)
        {
            newGoal.y = Mathf.Min(newGoal.y, safeTop - 2.5f);
        }

        bool isBenthic = Data != null && (Data.locomotionArchetype == LocomotionArchetype.BenthicFollower || Data.isStationary);
        if (isBenthic)
        {
            var tg = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
            float floorY = tg != null && tg.HasGenerated ? tg.SampleHeight(newGoal.x, newGoal.z) : newGoal.y;
            float offset = Data != null && Data.benthicSurfaceOffset > 0f ? Data.benthicSurfaceOffset : 0.15f;
            newGoal.y = floorY + offset;
        }
        else
        {
            newGoal.y = isSurfaceDrifter ? -1.15f : Mathf.Clamp(newGoal.y, safeBottom, safeTop);
        }

        _wanderTarget = newGoal;
        if (_steering != null)
        {
            _steering.SetGoal(_wanderTarget);
        }

        // If spawn center was set too high near the surface, pull it deeper so creature doesn't gravitate back up
        if (!isSurfaceDrifter && _spawnCenter.y > -5.0f)
        {
            _spawnCenter.y = Mathf.Min(transform.position.y - 3f, -8f);
        }

        _state = AIState.Wandering;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleBoundaryCollision(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleBoundaryCollision(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            HandleBoundaryCollision(collision.collider);
    }

    private void HandleBoundaryCollision(Collider other)
    {
        if (other == null) return;
        if (Time.time - _lastBoundaryTurnTime < 0.35f) return;

        bool isBoundary = other.GetComponent<ZoneBoundaryTrigger>() != null ||
                          other.name.Contains("Boundary") ||
                          other.name.Contains("Wall_");

        if (!isBoundary) return;

        bool isSurfaceDrifter = Data != null && Data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;
        if (isSurfaceDrifter)
        {
            // Surface drifters naturally live at the surface (Y = -0.45m).
            // Do NOT treat the top water boundary as an obstacle to turn away from!
            if (other.name.Contains("TopBoundary") || other.transform.position.y > transform.position.y)
            {
                return;
            }
        }

        Vector3 normal = Vector3.zero;
        if (other.name.Contains("TopBoundary") || (other.transform.position.y > transform.position.y && Mathf.Abs(other.transform.position.y - transform.position.y) < 15f))
        {
            normal = Vector3.down;
        }
        else if (other.name.Contains("BottomBoundary") || other.transform.position.y < transform.position.y)
        {
            normal = Vector3.up;
        }
        else
        {
            try
            {
                Vector3 closest = other.ClosestPoint(transform.position);
                normal = (transform.position - closest).normalized;
            }
            catch
            {
                normal = (_spawnCenter - transform.position).normalized;
            }

            if (normal.sqrMagnitude < 0.01f)
            {
                normal = (_spawnCenter - transform.position).normalized;
            }
        }

        TurnAwayFromBoundary(normal);
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
