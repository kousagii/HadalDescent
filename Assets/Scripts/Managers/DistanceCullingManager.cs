using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based culling system that disables renderers, colliders, AI, and
/// physics components on objects beyond the camera's fog visibility range.
///
/// Architecture:
///   - Auto-discovers cullable objects from parent transforms (_EnvironmentProps,
///     _Creatures, _DebrisClusters) via RegisterParent().
///   - Caches component references per object (zero per-frame GetComponent).
///   - Uses squared distance checks (no sqrt per object).
///   - Staggered batch processing: ~batchSize objects per frame in round-robin.
///   - Hysteresis gap: cull at fogEnd+30m, re-enable at fogEnd+10m to prevent flicker.
///   - SonarTrackable is NEVER disabled so the radar always sees all blips.
///
/// Setup:
///   Added automatically by TerrainGenerator after zone generation.
///   Spawners call RegisterParent() after placing their objects.
/// </summary>
public class DistanceCullingManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    private static DistanceCullingManager _instance;
    public static DistanceCullingManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DistanceCullingManager>();
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------

    [Header("Culling Thresholds")]
    [Tooltip("Objects beyond this distance are culled. Auto-set from zone fog if 0.")]
    [SerializeField] private float cullDistance = 0f;

    [Tooltip("Culled objects closer than this are re-enabled. Must be < cullDistance.")]
    [SerializeField] private float activateDistance = 0f;

    [Header("Performance")]
    [Tooltip("How many objects to process per frame (spreads cost over multiple frames).")]
    [SerializeField] private int batchSize = 100;

    // -----------------------------------------------------------------------
    // Tracked object data (cache everything to avoid per-frame allocations)
    // -----------------------------------------------------------------------

    private struct CullableEntry
    {
        public Transform        transform;
        public Renderer[]       renderers;
        public Collider[]       colliders;
        public SpeciesAI        speciesAI;
        public ContextSteering  steering;
        public Rigidbody        rigidbody;
        public bool             isCulled;
        public bool             isMobileSpecies;
    }

    private readonly List<CullableEntry> _entries = new List<CullableEntry>(2048);
    private int _batchCursor = 0;

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private Transform _playerTransform;
    private float     _cullDistSqr;
    private float     _activateDistSqr;
    private bool      _initialized = false;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;
    }

    private void Start()
    {
        InitializeThresholds();
        FindPlayer();
    }

    private void Update()
    {
        if (!_initialized) InitializeThresholds();
        if (_playerTransform == null)
        {
            FindPlayer();
            if (_playerTransform == null) return;
        }

        ProcessBatch();
    }

    // -----------------------------------------------------------------------
    // Public API — called by spawners after placing objects
    // -----------------------------------------------------------------------

    /// <summary>
    /// Register all children of a parent transform as cullable objects.
    /// Call this after spawning/scattering completes.
    /// </summary>
    public void RegisterParent(Transform parent)
    {
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            RegisterSingle(child);
        }
    }

    /// <summary>
    /// Register a single object and all its nested children (for debris clusters
    /// where each child part has its own renderer).
    /// </summary>
    public void RegisterSingle(Transform obj)
    {
        if (obj == null) return;

        // Skip if already registered
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].transform == obj) return;
        }

        var entry = new CullableEntry
        {
            transform       = obj,
            renderers       = obj.GetComponentsInChildren<Renderer>(true),
            colliders       = obj.GetComponentsInChildren<Collider>(true),
            speciesAI       = obj.GetComponent<SpeciesAI>(),
            steering        = obj.GetComponent<ContextSteering>(),
            rigidbody       = obj.GetComponent<Rigidbody>(),
            isCulled        = false,
            isMobileSpecies = obj.GetComponent<SpeciesAI>() != null
        };

        _entries.Add(entry);
    }

    /// <summary>
    /// Clear all tracked entries. Call when changing zones before new objects spawn.
    /// </summary>
    public void ClearAll()
    {
        _entries.Clear();
        _batchCursor = 0;
    }

    /// <summary>
    /// Force re-initialization of cull thresholds (call after zone change).
    /// </summary>
    public void RefreshThresholds()
    {
        _initialized = false;
        InitializeThresholds();
    }

    // -----------------------------------------------------------------------
    // Core culling loop (staggered batch processing)
    // -----------------------------------------------------------------------

    private void ProcessBatch()
    {
        if (_entries.Count == 0) return;

        Vector3 playerPos = _playerTransform.position;
        int count = _entries.Count;
        int processed = 0;

        // Process batchSize entries starting from _batchCursor
        while (processed < batchSize && processed < count)
        {
            if (_batchCursor >= count)
                _batchCursor = 0;

            CullableEntry entry = _entries[_batchCursor];

            // Skip destroyed objects
            if (entry.transform == null)
            {
                _entries.RemoveAt(_batchCursor);
                count--;
                if (_batchCursor >= count) _batchCursor = 0;
                processed++;
                continue;
            }

            float sqrDist = (entry.transform.position - playerPos).sqrMagnitude;

            if (!entry.isCulled && sqrDist > _cullDistSqr)
            {
                // Cull this object
                CullEntry(ref entry);
                _entries[_batchCursor] = entry;
            }
            else if (entry.isCulled && sqrDist < _activateDistSqr)
            {
                // Re-enable this object
                ActivateEntry(ref entry);
                _entries[_batchCursor] = entry;
            }

            _batchCursor++;
            processed++;
        }
    }

    // -----------------------------------------------------------------------
    // Cull / Activate individual entries
    // -----------------------------------------------------------------------

    private void CullEntry(ref CullableEntry entry)
    {
        entry.isCulled = true;

        // Disable renderers
        if (entry.renderers != null)
        {
            for (int i = 0; i < entry.renderers.Length; i++)
            {
                if (entry.renderers[i] != null)
                    entry.renderers[i].enabled = false;
            }
        }

        // Disable colliders (removes from physics broadphase)
        if (entry.colliders != null)
        {
            for (int i = 0; i < entry.colliders.Length; i++)
            {
                if (entry.colliders[i] != null)
                    entry.colliders[i].enabled = false;
            }
        }

        // Disable AI and physics for mobile species
        if (entry.isMobileSpecies)
        {
            if (entry.speciesAI != null)
                entry.speciesAI.enabled = false;
            if (entry.steering != null)
                entry.steering.enabled = false;
            if (entry.rigidbody != null)
            {
                entry.rigidbody.linearVelocity = Vector3.zero;
                entry.rigidbody.angularVelocity = Vector3.zero;
                entry.rigidbody.Sleep();
            }
        }
    }

    private void ActivateEntry(ref CullableEntry entry)
    {
        entry.isCulled = false;

        // Re-enable renderers
        if (entry.renderers != null)
        {
            for (int i = 0; i < entry.renderers.Length; i++)
            {
                if (entry.renderers[i] != null)
                    entry.renderers[i].enabled = true;
            }
        }

        // Re-enable colliders
        if (entry.colliders != null)
        {
            for (int i = 0; i < entry.colliders.Length; i++)
            {
                if (entry.colliders[i] != null)
                    entry.colliders[i].enabled = true;
            }
        }

        // Re-enable AI and physics for mobile species
        if (entry.isMobileSpecies)
        {
            if (entry.speciesAI != null)
                entry.speciesAI.enabled = true;
            if (entry.steering != null)
                entry.steering.enabled = true;
            if (entry.rigidbody != null)
                entry.rigidbody.WakeUp();
        }
    }

    // -----------------------------------------------------------------------
    // Initialization helpers
    // -----------------------------------------------------------------------

    private void InitializeThresholds()
    {
        // Auto-set from zone fog distance if not manually configured in Inspector
        int zoneIdx = ZoneManager.CurrentZoneIndex;
        float fogEnd = 140f; // fallback

        if (ZoneConfig.IsValidZone(zoneIdx))
        {
            fogEnd = ZoneConfig.Zones[zoneIdx].fogEndDistance;
        }

        float effectiveCull = cullDistance > 0f ? cullDistance : (fogEnd + 30f);
        float effectiveActivate = activateDistance > 0f ? activateDistance : (fogEnd + 10f);

        // Ensure hysteresis gap
        if (effectiveActivate >= effectiveCull)
            effectiveActivate = Mathf.Max(10f, effectiveCull - 20f);

        _cullDistSqr     = effectiveCull * effectiveCull;
        _activateDistSqr = effectiveActivate * effectiveActivate;
        _initialized     = true;

        Debug.Log($"[DistanceCullingManager] Zone {zoneIdx} thresholds initialized: " +
                  $"cull={effectiveCull:F0}m, activate={effectiveActivate:F0}m (fogEnd={fogEnd:F0}m).");
    }

    private void FindPlayer()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            _playerTransform = pm.transform;
            return;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            _playerTransform = cam.transform;
        }
    }
}
