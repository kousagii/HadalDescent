using System;
using UnityEngine;
/// <summary>
/// Singleton that orchestrates species scan minigames.
/// Picks randomly between Minigame 1 (Capture & Focus) and Minigame 2 (Reconstruction Scan).
/// Disables PlayerMovement + TouchDragZone while a minigame is active.
///
/// Setup: Add MinigameManager to a persistent or per-scene GameObject.
/// ScannerSystem calls TriggerScanMinigame() � no direct setup needed.
/// </summary>
public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

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

    private CaptureAndFocusMinigame    _mg1;
    private ReconstructionScanMinigame _mg2;

    // Components to disable during minigame
    private PlayerMovement  _playerMovement;
    private TouchDragZone   _touchDragZone;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        _touchDragZone  = FindFirstObjectByType<TouchDragZone>();

        // Lazily create minigame components on this GameObject
        _mg1 = gameObject.AddComponent<CaptureAndFocusMinigame>();
        _mg2 = gameObject.AddComponent<ReconstructionScanMinigame>();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Launch a random scan minigame for the given species.
    /// On win  ? <paramref name="onSuccess"/> is invoked.
    /// On fail ? <paramref name="onFail"/> is invoked.
    /// </summary>
    public void TriggerScanMinigame(SpeciesData data, Action onSuccess, Action onFail)
    {
        if (_minigameActive)
        {
            Debug.LogWarning("[MinigameManager] Minigame already active � ignoring request.");
            return;
        }

        _minigameActive = true;
        DisableControls();

        Action wrappedSuccess = () => { _minigameActive = false; EnableControls(); onSuccess?.Invoke(); };
        Action wrappedFail    = () => { _minigameActive = false; EnableControls(); onFail?.Invoke(); };

        int zone = ZoneManager.CurrentZoneIndex;
        int pick  = ChooseMinigame(zone);

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

    /// <summary>
    /// Call when scene is being unloaded to cleanly abort any running minigame.
    /// </summary>
    public void AbortCurrentMinigame()
    {
        if (!_minigameActive) return;
        _minigameActive = false;
        EnableControls();
        // Minigame panels will be destroyed with the scene
    }

    public bool IsMinigameActive => _minigameActive;

    // -----------------------------------------------------------------------
    // Minigame selection
    // -----------------------------------------------------------------------

    private int ChooseMinigame(int zoneIndex)
    {
        if (forceMG1) return 1;
        if (forceMG2) return 2;

        // 50/50 for all zones in Phase 4 � tune later
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
