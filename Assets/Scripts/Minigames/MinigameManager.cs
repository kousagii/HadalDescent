using System;
using UnityEngine;

/// <summary>
/// Singleton that orchestrates species scan minigames and environmental cleanup minigames.
/// Disables PlayerMovement + TouchDragZone while a minigame is active.
///
/// Setup: Auto-creates if missing.
/// ScannerSystem calls TriggerScanMinigame() or TriggerDebrisCleanupMinigame().
/// </summary>
public class MinigameManager : MonoBehaviour
{
    private static MinigameManager _instance;
    public static MinigameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MinigameManager>();
                if (_instance == null)
                {
                    var go = new GameObject("MinigameManager");
                    _instance = go.AddComponent<MinigameManager>();
                }
            }
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Tooltip("If true, always launch Minigame 1 for testing. False = random.")]
    [SerializeField] private bool forceMG1 = false;
    [Tooltip("If true, always launch Minigame 2 for testing. False = random.")]
    [SerializeField] private bool forceMG2 = false;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private bool _minigameActive = false;

    private CaptureAndFocusMinigame      _mg1;
    private ReconstructionScanMinigame   _mg2;
    private EnvironmentalCleanupMinigame _mg3;
    private HazardDodgeMinigame          _mg4;

    // Components to disable during minigame
    private PlayerMovement  _playerMovement;
    private TouchDragZone   _touchDragZone;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        EnsureMinigameComponents();
    }

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        _touchDragZone  = FindFirstObjectByType<TouchDragZone>();

        EnsureMinigameComponents();
    }

    private void EnsureMinigameComponents()
    {
        if (_mg1 == null) _mg1 = GetComponent<CaptureAndFocusMinigame>() ?? gameObject.AddComponent<CaptureAndFocusMinigame>();
        if (_mg2 == null) _mg2 = GetComponent<ReconstructionScanMinigame>() ?? gameObject.AddComponent<ReconstructionScanMinigame>();
        if (_mg3 == null) _mg3 = GetComponent<EnvironmentalCleanupMinigame>() ?? gameObject.AddComponent<EnvironmentalCleanupMinigame>();
        if (_mg4 == null) _mg4 = GetComponent<HazardDodgeMinigame>() ?? gameObject.AddComponent<HazardDodgeMinigame>();
    }

    // -----------------------------------------------------------------------
    // Public API - Species Scanning Minigames (MG1 & MG2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch a random scan minigame for the given species.
    /// On win  -> onSuccess is invoked.
    /// On fail -> onFail is invoked.
    /// </summary>
    public void TriggerScanMinigame(SpeciesData data, Action onSuccess, Action onFail)
    {
        if (_minigameActive)
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring request.");
            return;
        }

        _minigameActive = true;
        DisableControls();
        Time.timeScale = 0f;

        Action wrappedSuccess = () =>
        {
            Time.timeScale = 1f;
            _minigameActive = false;
            EnableControls();
            onSuccess?.Invoke();
        };
        Action wrappedFail = () =>
        {
            Time.timeScale = 1f;
            _minigameActive = false;
            EnableControls();
            onFail?.Invoke();
        };

        int zone = ZoneManager.CurrentZoneIndex;
        int pick = ChooseMinigame(zone);

        if (pick == 1 || forceMG1)
        {
            _mg1.Initialize(data, zone, wrappedSuccess, wrappedFail);
            _mg1.Show();
        }
        else
        {
            _mg2.Initialize(data, zone, wrappedSuccess, wrappedFail);
            _mg2.Show();
        }
    }

    // -----------------------------------------------------------------------
    // Public API - Environmental Cleanup Minigame (MG3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch Mini-game 3 (Environmental Cleanup 2.5D Extraction & Sorting) for a DebrisCluster.
    /// </summary>
    public void TriggerDebrisCleanupMinigame(DebrisCluster cluster, Action onSuccess = null, Action onFail = null)
    {
        if (_minigameActive)
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring cleanup request.");
            return;
        }

        EnsureMinigameComponents();

        _minigameActive = true;
        DisableControls();

        Action wrappedSuccess = () => { _minigameActive = false; EnableControls(); onSuccess?.Invoke(); };
        Action wrappedFail    = () => { _minigameActive = false; EnableControls(); onFail?.Invoke(); };

        int zone = ZoneManager.CurrentZoneIndex;
        _mg3.Initialize(cluster, zone, wrappedSuccess, wrappedFail);
        _mg3.Show();
    }

    // -----------------------------------------------------------------------
    // Public API - Hazard Dodge Minigame (MG4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch Mini-game 4 (Hazard Dodge 2.5D Top-Perspective 3-Lane Emergency Runner).
    /// </summary>
    public void TriggerHazardDodgeMinigame(int zoneIndex, Action<int> onSuccess = null, Action onFail = null)
    {
        if (_minigameActive)
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring hazard request.");
            return;
        }

        EnsureMinigameComponents();

        _minigameActive = true;
        DisableControls();

        Action<int> wrappedSuccess = (reward) =>
        {
            _minigameActive = false;
            EnableControls();
            onSuccess?.Invoke(reward);
        };

        Action wrappedFail = () =>
        {
            _minigameActive = false;
            EnableControls();
            onFail?.Invoke();
        };

        _mg4.Initialize(zoneIndex, wrappedSuccess, wrappedFail);
        _mg4.Show();
    }

    /// <summary>
    /// Call when scene is being unloaded to cleanly abort any running minigame.
    /// </summary>
    public void AbortCurrentMinigame()
    {
        if (!_minigameActive) return;
        Time.timeScale = 1f;
        _minigameActive = false;
        EnableControls();
    }

    public bool IsMinigameActive => _minigameActive;

    // -----------------------------------------------------------------------
    // Minigame selection
    // -----------------------------------------------------------------------

    private int ChooseMinigame(int zoneIndex)
    {
        if (forceMG1) return 1;
        if (forceMG2) return 2;

        return UnityEngine.Random.value < 0.5f ? 1 : 2;
    }

    // -----------------------------------------------------------------------
    // Controls management
    // -----------------------------------------------------------------------

    private void DisableControls()
    {
        if (_playerMovement == null) _playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (_touchDragZone  == null) _touchDragZone  = FindFirstObjectByType<TouchDragZone>();

        if (_playerMovement != null) _playerMovement.enabled = false;
        if (_touchDragZone  != null) _touchDragZone.enabled  = false;
    }

    private void EnableControls()
    {
        if (_playerMovement != null) _playerMovement.enabled = true;
        if (_touchDragZone  != null) _touchDragZone.enabled  = true;
    }
}
