using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private bool _cancelling;
    private int _activityVersion;
    public bool IsMinigameActive => _minigameActive || _cancelling;

    [Header("Minigame Component References (Auto-found if unassigned)")]
    [SerializeField] private CaptureAndFocusMinigame      _mg1;
    [SerializeField] private ReconstructionScanMinigame   _mg2;
    [SerializeField] private EnvironmentalCleanupMinigame _mg3;
    [SerializeField] private HazardDodgeMinigame          _mg4;

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

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureMinigameComponents();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CancelActiveMinigame();
    }

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        _touchDragZone  = FindFirstObjectByType<TouchDragZone>();

        EnsureMinigameComponents();
    }

    /// <summary>
    /// Forcibly aborts any active minigame, destroys minigame UI panels,
    /// re-enables submarine movement and camera drag, and restores normal time scale.
    /// </summary>
    public void CancelActiveMinigame()
    {
        if (_cancelling) return;
        _cancelling = true;
        _activityVersion++;
        try
        {
            FindFirstObjectByType<MinigameInstructionOverlay>(FindObjectsInactive.Include)?.CancelSlideshow();
            ResearchVerificationMinigame.Instance?.CancelMinigame();
            _minigameActive = false;
            if (_mg1 != null) _mg1.CancelMinigame();
            if (_mg2 != null) _mg2.CancelMinigame();
            if (_mg3 != null) _mg3.CancelMinigame();
            if (_mg4 != null) _mg4.CancelMinigame();
        }
        finally
        {
            _minigameActive = false;
            _cancelling = false;
            Time.timeScale = 1f;
            EnableControls();
            UIManager.Instance?.SetExplorationHUDVisible(!UIManager.IsMenuScene(SceneManager.GetActiveScene().name));
        }
    }

    private void EnsureMinigameComponents()
    {
        if (_mg1 == null)
        {
            _mg1 = GetComponent<CaptureAndFocusMinigame>()
                ?? FindFirstObjectByType<CaptureAndFocusMinigame>(FindObjectsInactive.Include)
                ?? gameObject.AddComponent<CaptureAndFocusMinigame>();
        }

        if (_mg2 == null)
        {
            _mg2 = GetComponent<ReconstructionScanMinigame>()
                ?? FindFirstObjectByType<ReconstructionScanMinigame>(FindObjectsInactive.Include)
                ?? gameObject.AddComponent<ReconstructionScanMinigame>();
        }

        if (_mg3 == null)
        {
            _mg3 = FindBestMG3();
        }

        if (_mg4 == null)
        {
            _mg4 = FindBestMG4();
        }
    }

    private EnvironmentalCleanupMinigame FindBestMG3()
    {
        var all = FindObjectsByType<EnvironmentalCleanupMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var candidate in all)
        {
            if (candidate.gameObject.name.Contains("Minigame3") || candidate.customUIRoot != null)
                return candidate;
        }
        return all.Length > 0 ? all[0] : (GetComponent<EnvironmentalCleanupMinigame>() ?? gameObject.AddComponent<EnvironmentalCleanupMinigame>());
    }

    private HazardDodgeMinigame FindBestMG4()
    {
        var all = FindObjectsByType<HazardDodgeMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var candidate in all)
        {
            if (candidate.gameObject.name.Contains("Minigame4") || candidate.customUIRoot != null)
                return candidate;
        }
        return all.Length > 0 ? all[0] : (GetComponent<HazardDodgeMinigame>() ?? gameObject.AddComponent<HazardDodgeMinigame>());
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
        if (IsMinigameActive || (ResearchVerificationMinigame.Instance != null && ResearchVerificationMinigame.Instance.IsOpen))
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring request.");
            return;
        }

        _minigameActive = true;
        int launchVersion = ++_activityVersion;
        DisableControls();
        Time.timeScale = 0f;
        UIManager.Instance?.SetExplorationHUDVisible(false);
        TutorialArrowUI.Instance?.Hide();
        RadioTextboxUI.Instance?.HideInstant();

        Action wrappedSuccess = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            Time.timeScale = 1f;
            _minigameActive = false;
            EnableControls();
            UIManager.Instance?.SetExplorationHUDVisible(true);
            AudioManager.Instance?.StopBarRisingSFX();
            onSuccess?.Invoke();
        };
        Action wrappedFail = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            Time.timeScale = 1f;
            _minigameActive = false;
            EnableControls();
            UIManager.Instance?.SetExplorationHUDVisible(true);
            AudioManager.Instance?.StopBarRisingSFX();
            onFail?.Invoke();
        };

        int zone = ZoneManager.CurrentZoneIndex;
        int pick = ChooseMinigame(zone);

        Action startMinigame = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            if ((pick == 1 || forceMG1) && !forceMG2)
            {
                _mg1.Initialize(data, zone, wrappedSuccess, wrappedFail);
                _mg1.Show();
            }
            else
            {
                _mg2.Initialize(data, zone, wrappedSuccess, wrappedFail);
                _mg2.Show();
            }
        };

        var overlay = MinigameInstructionOverlay.Instance ?? FindFirstObjectByType<MinigameInstructionOverlay>(FindObjectsInactive.Include);
        if (overlay != null)
        {
            overlay.ShowSlideshow(pick, startMinigame);
        }
        else
        {
            startMinigame();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Test Minigame 2 (Reconstruction Scan)")]
    public void DebugTestMinigame2()
    {
        EnsureMinigameComponents();
        SpeciesData dummy = ScriptableObject.CreateInstance<SpeciesData>();
        dummy.commonName = "Test Specimen";
        TriggerScanMinigame(dummy, () => Debug.Log("[Test] Won Minigame 2!"), () => Debug.Log("[Test] Failed Minigame 2!"));
    }

    [ContextMenu("Debug: Test Minigame 1 (Capture & Focus)")]
    public void DebugTestMinigame1()
    {
        EnsureMinigameComponents();
        SpeciesData dummy = ScriptableObject.CreateInstance<SpeciesData>();
        dummy.commonName = "Test Specimen";
        _mg1.Initialize(dummy, ZoneManager.CurrentZoneIndex, () => Debug.Log("[Test] Won Minigame 1!"), () => Debug.Log("[Test] Failed Minigame 1!"));
        _mg1.Show();
    }
#endif

    // -----------------------------------------------------------------------
    // Public API - Environmental Cleanup Minigame (MG3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch Mini-game 3 (Environmental Cleanup 2.5D Extraction & Sorting) for a DebrisCluster.
    /// </summary>
    public void TriggerDebrisCleanupMinigame(DebrisCluster cluster, Action onSuccess = null, Action onFail = null)
    {
        if (IsMinigameActive || (ResearchVerificationMinigame.Instance != null && ResearchVerificationMinigame.Instance.IsOpen))
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring cleanup request.");
            return;
        }

        EnsureMinigameComponents();

        if (_mg3 != null && !_mg3.gameObject.activeSelf)
        {
            _mg3.gameObject.SetActive(true);
        }

        _minigameActive = true;
        int launchVersion = ++_activityVersion;
        DisableControls();
        UIManager.Instance?.SetExplorationHUDVisible(false);
        TutorialArrowUI.Instance?.Hide();
        RadioTextboxUI.Instance?.HideInstant();

        Action wrappedSuccess = () => { if (!IsCurrentActivity(launchVersion)) return; _minigameActive = false; EnableControls(); UIManager.Instance?.SetExplorationHUDVisible(true); AudioManager.Instance?.StopClawSFX(); onSuccess?.Invoke(); };
        Action wrappedFail    = () => { if (!IsCurrentActivity(launchVersion)) return; _minigameActive = false; EnableControls(); UIManager.Instance?.SetExplorationHUDVisible(true); AudioManager.Instance?.StopClawSFX(); onFail?.Invoke(); };

        Action startMG3 = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            int zone = ZoneManager.CurrentZoneIndex;
            _mg3.Initialize(cluster, zone, wrappedSuccess, wrappedFail);
            _mg3.Show();
        };

        var overlay = MinigameInstructionOverlay.Instance ?? FindFirstObjectByType<MinigameInstructionOverlay>(FindObjectsInactive.Include);
        if (overlay != null)
        {
            overlay.ShowSlideshow(3, startMG3);
        }
        else
        {
            startMG3();
        }
    }

    // -----------------------------------------------------------------------
    // Public API - Hazard Dodge Minigame (MG4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch Mini-game 4 (Hazard Dodge 2.5D Top-Perspective 3-Lane Emergency Runner).
    /// </summary>
    public void TriggerHazardDodgeMinigame(int zoneIndex, Action<int> onSuccess = null, Action onFail = null)
    {
        if (IsMinigameActive || (ResearchVerificationMinigame.Instance != null && ResearchVerificationMinigame.Instance.IsOpen))
        {
            Debug.LogWarning("[MinigameManager] Minigame already active - ignoring hazard request.");
            return;
        }

        if (UIManager.IsAnyPanelOrModalOpen(includeMinigames: false))
        {
            bool isTutorialStep7 = TutorialManager.Instance != null && 
                                   TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Step7_HazardEncounter;
            if (!isTutorialStep7)
            {
                Debug.LogWarning("[MinigameManager] Major UI panel or modal is open - suppressing hazard request.");
                return;
            }
        }

        EnsureMinigameComponents();

        if (_mg4 != null && !_mg4.gameObject.activeSelf)
        {
            _mg4.gameObject.SetActive(true);
        }

        _minigameActive = true;
        int launchVersion = ++_activityVersion;
        DisableControls();
        UIManager.Instance?.SetExplorationHUDVisible(false);
        TutorialArrowUI.Instance?.Hide();
        RadioTextboxUI.Instance?.HideInstant();

        Action<int> wrappedSuccess = (reward) =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            _minigameActive = false;
            EnableControls();
            UIManager.Instance?.SetExplorationHUDVisible(true);
            onSuccess?.Invoke(reward);
        };

        Action wrappedFail = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            _minigameActive = false;
            EnableControls();
            UIManager.Instance?.SetExplorationHUDVisible(true);
            onFail?.Invoke();
        };

        Action startMG4 = () =>
        {
            if (!IsCurrentActivity(launchVersion)) return;
            _mg4.Initialize(zoneIndex, wrappedSuccess, wrappedFail);
            _mg4.Show();
        };

        var overlay = MinigameInstructionOverlay.Instance ?? FindFirstObjectByType<MinigameInstructionOverlay>(FindObjectsInactive.Include);
        if (overlay != null)
        {
            overlay.ShowSlideshow(4, startMG4);
        }
        else
        {
            startMG4();
        }
    }

    /// <summary>
    /// Call when scene is being unloaded or reset to cleanly abort any running minigame.
    /// </summary>
    public void AbortCurrentMinigame()
    {
        CancelActiveMinigame();
    }

    /// <summary>Reserve controls during Santos's introduction, then mark the quiz active.</summary>
    public void SetResearchReviewState(bool open, bool questionsActive)
    {
        _minigameActive = open && questionsActive;
        if (open)
        {
            DisableControls();
            Time.timeScale = 0f;
            UIManager.Instance?.SetExplorationHUDVisible(false);
            TutorialArrowUI.Instance?.Hide();
            ScanReticleUI.Instance?.SetVisible(false);
        }
        else
        {
            EnableControls();
            Time.timeScale = 1f;
            string scene = SceneManager.GetActiveScene().name;
            bool gameplay = !UIManager.IsMenuScene(scene);
            UIManager.Instance?.SetExplorationHUDVisible(gameplay);
            ScanReticleUI.Instance?.SetVisible(gameplay);
        }
    }

    // -----------------------------------------------------------------------
    // Minigame selection
    // -----------------------------------------------------------------------

    private int ChooseMinigame(int zoneIndex)
    {
        if (forceMG1) return 1;
        if (forceMG2) return 2;

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            int tutPick = TutorialManager.Instance.GetTutorialMinigamePick();
            if (tutPick == 1 || tutPick == 2) return tutPick;
        }

        return UnityEngine.Random.value < 0.5f ? 1 : 2;
    }

    // -----------------------------------------------------------------------
    // Controls management
    // -----------------------------------------------------------------------

    private bool IsCurrentActivity(int version) => version == _activityVersion && _minigameActive && !_cancelling;

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
