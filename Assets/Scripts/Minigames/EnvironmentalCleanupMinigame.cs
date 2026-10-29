using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Mini-game 3 – Environmental Cleanup (Debris Extraction & Sorting Minigame).
///
/// Features:
///   - Auto-discovers 'Minigame3HUD' in Canvas hierarchy and activates it.
///   - Automatically handles Canvas camera / CanvasGroup visibility.
///   - Auto-wires Timer, Score, DropButton, and MoveOuterRing/Move joystick.
///   - Seamless transition from Removal Phase to Sorting Phase on the same HUD.
/// </summary>
public class EnvironmentalCleanupMinigame : MonoBehaviour
{
    public enum DebrisCategory { Plastic, Metal, Hazardous }

    public enum DebrisItemType
    {
        WaterBottle,
        PlasticBag,
        SixPackRings,
        SodaCan,
        MetalPipe,
        MetalDrum,
        BatterySmall,
        BatteryLarge,
        ElectronicWaste,
        MetalTube // Append to preserve the existing serialized enum values.
    }

    [System.Serializable]
    public class DebrisInstance
    {
        public DebrisItemType itemType;
        public DebrisCategory category;
        public string displayName;
        public GameObject worldObject;
        public Color itemColor;
        public Vector3 restPosition;
        public float driftPhase;
        public bool carefulRecovery;
        public bool isFalling;
    }

    // -----------------------------------------------------------------------
    // Inspector: Custom User UI (Auto-wires 'Minigame3HUD'!)
    // -----------------------------------------------------------------------

    [Header("=== USER CUSTOM UI (Optional) ===")]
    public GameObject customUIRoot;

    [Header("Common Header UI Elements")]
    public TMP_Text   customTimerText;
    public Text       customTimerTextLegacy;
    public TMP_Text   customScoreText;
    public Text       customScoreTextLegacy;
    public TMP_Text   customStatusText;
    public Text       customStatusTextLegacy;

    [Header("Phase 1: Removal Controls (Can be on the same panel)")]
    public GameObject customExtractionContainer;
    public Button     customDropClawButton;
    public RectTransform customJoystickHandle;
    public RectTransform customJoystickBackground;

    [Header("Phase 2: Sorting Controls (Can be on the same panel)")]
    public GameObject customSortingContainer;
    public TMP_Text   customSortingItemText;
    public Text       customSortingItemTextLegacy;
    public Button     customBinPlastics;
    public Button     customBinMetals;
    public Button     customBinHazardous;

    [Header("Phase 3: Results Controls (Can be on the same panel)")]
    public GameObject customResultsContainer;
    public TMP_Text   customResultsSummaryText;
    public Text       customResultsSummaryTextLegacy;
    public Button     customReturnButton;

    // -----------------------------------------------------------------------
    // Inspector: 3D Model Prefab Overrides (Leave empty to use procedural shapes)
    // -----------------------------------------------------------------------

    private const float CollectionModelScale = 2f;
    private const float MinimumDebrisVisibleSize = 1.4f;
    private const float FishVisibleSize = 1.8f;

    [Header("=== 3D MODEL PREFABS (Optional) ===")]
    [Tooltip("Additional collection debris scale. Small items have a minimum visible size comparable to the claw.")]
    [Range(0.2f, 2.5f)]
    public float debrisModelScale = 1.0f;

    public GameObject customClawPrefab;
    public GameObject customTrolleyPrefab;

    [Header("Plastics 3D Prefabs")]
    public GameObject prefabWaterBottle;
    public GameObject prefabPlasticBag;
    public GameObject prefabSixPackRings;

    [Header("Metals 3D Prefabs")]
    public GameObject prefabSodaCan;
    public GameObject prefabMetalPipe;
    public GameObject prefabMetalTube;
    public GameObject prefabMetalDrum;

    [Header("Sorting Bin Prefabs (defaults from Resources/Minigames/CleanupModelLibrary)")]
    public GameObject prefabPlasticBin;
    public GameObject prefabMetalBin;
    public GameObject prefabHazardBin;

    [Header("Hazardous Waste 3D Prefabs")]
    public GameObject prefabBatterySmall;
    public GameObject prefabBatteryLarge;
    public GameObject prefabElectronicWaste;

    [Header("Obstacles 3D Prefabs")]
    public GameObject prefabCoralObstacle;
    public GameObject prefabFishObstacle;

    [Header("=== AUDIO (Optional — defaults to AudioManager) ===")]
    [Tooltip("Energetic & tropical ambient BGM for Minigame 3.")]
    [SerializeField] private AudioClip customBGM;
    [Tooltip("Mechanical winch claw dropping & reel-up sound.")]
    [SerializeField] private AudioClip customClawSFX;
    [Tooltip("Species bump sound (shared bump track).")]
    [SerializeField] private AudioClip customBumpSFX;

    // -----------------------------------------------------------------------
    // State & Callbacks
    // -----------------------------------------------------------------------

    private DebrisCluster _targetCluster;
    private Action        _onSuccess;
    private Action        _onFail;
    private int           _zoneIndex;
    private bool          _isActive;
    public bool IsActive => _isActive && gameObject.activeInHierarchy;

    // -----------------------------------------------------------------------
    // Stage & 3D Objects
    // -----------------------------------------------------------------------

    private GameObject _stageRoot;
    private Camera     _minigameCam;
    private readonly List<Camera> _disabledSceneCams = new List<Camera>();
    private Camera     _previousCanvasCam;
    private float      _previousPlaneDistance;
    private Transform  _originalUIParent;
    private GameObject _dedicatedCanvasGO;
    private Canvas     _parentCanvas;

    private Transform  _trolley;
    private Transform  _clawHead;
    private Transform  _leftJaw;
    private Transform  _rightJaw;
    private LineRenderer _cableRenderer;

    private readonly List<DebrisInstance> _spawnedDebris = new List<DebrisInstance>();
    private readonly List<DebrisInstance> _collectedDebris = new List<DebrisInstance>();
    private readonly List<GameObject>     _spawnedObstacles = new List<GameObject>();

    // -----------------------------------------------------------------------
    // Claw Physics & Motion
    // -----------------------------------------------------------------------

    private float _trolleyX = 0f;
    private float _trolleySpeed = 6.5f;
    private float _clawY = 3.6f;
    private float _clawRestY = 3.6f;
    private float _clawSeabedY = -2.2f;
    private float _clawDropSpeed = 7.0f;
    private float _clawReelSpeed = 8.0f;

    private enum ClawState { Idle, Dropping, Reeling }
    private ClawState _clawState = ClawState.Idle;
    private DebrisInstance _grabbedItem = null;

    // -----------------------------------------------------------------------
    // UI Runtime References
    // -----------------------------------------------------------------------

    private Canvas     _uiCanvas;
    private Button     _dropButton;

    // Virtual Joystick UI
    private RectTransform _joystickHandle;
    private RectTransform _joystickBackground;
    private float         _joystickHorizontalInput = 0f;
    private bool          _isDraggingJoystick = false;

    // Sorting Phase UI
    private GameObject _sortingPanel;
    private int        _correctSortCount = 0;
    private CleanupRoundRules.SortingScore _sortingScore;
    private CleanupRoundRules.Variation _variation;
    private static int _previousVariation = -1;
    private int _initialDebrisCount;
    private int _objectiveTarget;
    private int _objectiveProgress;
    private int _carefulRecoveries;
    private bool _sortingActive;
    private bool _resultsShown;
    private bool _roundSucceeded;
    private int _pendingRdpReward;
    private TMP_Text _roundObjectiveText;
    private TMP_Text _sortingProgressText;
    private TMP_Text _sortingFeedbackText;
    private bool _isPlaytest;
    public bool CanSort => _isActive && _sortingActive && !_resultsShown && _phaseTimeRemaining > 0f &&
        (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused);

    // Results UI
    private GameObject _resultsPanel;

    private float _phaseTimeRemaining;

    private static readonly Vector3 StageOrigin = new Vector3(0f, 1500f, 0f);

    private void Awake()
    {
        if (!_isActive)
        {
            if (customUIRoot != null)
                customUIRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (!_isActive)
        {
            if (customUIRoot != null)
                customUIRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }

    // -----------------------------------------------------------------------
    // Entry Point
    // -----------------------------------------------------------------------

    public void Initialize(DebrisCluster cluster, int zoneIndex, Action onSuccess, Action onFail)
    {
        _targetCluster = cluster;
        _zoneIndex     = zoneIndex;
        _onSuccess     = onSuccess;
        _onFail        = onFail;
        _isPlaytest = false;
    }

    public void Show()
    {
        _isActive = true;
        gameObject.SetActive(true);
        StopAllCoroutines();

        if (customBGM != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(customBGM);
        else
            AudioManager.Instance?.PlayMinigame3BGM();

        if (customUIRoot == null && (gameObject.name.Contains("Minigame3") || GetComponent<RectTransform>() != null))
        {
            customUIRoot = gameObject;
        }

        if (_resultsPanel != null) { Destroy(_resultsPanel); _resultsPanel = null; }
        if (_sortingPanel != null) { Destroy(_sortingPanel); _sortingPanel = null; }
        if (_dedicatedCanvasGO != null) { Destroy(_dedicatedCanvasGO); _dedicatedCanvasGO = null; }
        if (_stageRoot != null) { Destroy(_stageRoot); _stageRoot = null; }

        if (customUIRoot != null)
        {
            for (int i = customUIRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = customUIRoot.transform.GetChild(i);
                if (child.name.StartsWith("ResultsPanel_Auto") || child.name.StartsWith("SortingPanel_Auto") || child.name.StartsWith("StatusBanner_Auto") || child.name.StartsWith("TimerText_Auto") || child.name.StartsWith("ScoreText_Auto"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // Thoroughly purge any leftover procedural panels across all canvases in the scene
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && (t.name.StartsWith("ResultsPanel_Auto") || t.name.StartsWith("SortingPanel_Auto")))
            {
                Destroy(t.gameObject);
            }
        }

        _collectedDebris.Clear();
        _spawnedDebris.Clear();
        _spawnedObstacles.Clear();
        _sortingActive = false;
        _resultsShown = false;
        _roundSucceeded = false;
        _pendingRdpReward = 0;
        _carefulRecoveries = 0;
        _objectiveProgress = 0;
        _correctSortCount = 0;
        _sortingScore = new CleanupRoundRules.SortingScore();
        _clawState   = ClawState.Idle;
        _grabbedItem = null;
        _trolleyX    = 0f;
        _clawY       = _clawRestY;
        _joystickHorizontalInput = 0f;

        if (customStatusText != null) customStatusText.gameObject.SetActive(false);
        if (customStatusTextLegacy != null) customStatusTextLegacy.gameObject.SetActive(false);

        // Utilities Tier 2 Perk: +25% claw drop and retrieval speed
        int utilTier = GameManager.Instance != null ? GameManager.Instance.UtilitiesTier : 1;
        float speedBonus = (utilTier >= 2) ? 1.25f : 1.0f;
        _clawDropSpeed = 7.0f * speedBonus;
        _clawReelSpeed = 8.0f * speedBonus;

        // Cleanly hide main exploration HUD
        UIManager.Instance?.SetExplorationHUDVisible(false);

        ResolveModelDefaults();
        Build3DStage();
        SwitchToMinigameCamera();
        BuildUI();
        SpawnDebrisAndObstacles();

        gameObject.SetActive(true);
        StartCoroutine(ExtractionPhaseRoutine());
    }

#if UNITY_EDITOR
    /// <summary>Disposable editor preview; never awards RDP or consumes a world cluster.</summary>
    public void OpenForTesting(int zoneIndex, bool sortingOnly)
    {
        Initialize(null, zoneIndex, null, null);
        _isPlaytest = true;
        Show();
        if (!sortingOnly) return;
        StopAllCoroutines();
        foreach (var item in _spawnedDebris)
            if (item.worldObject != null) Destroy(item.worldObject);
        _spawnedDebris.Clear();
        foreach (DebrisItemType type in Enum.GetValues(typeof(DebrisItemType)))
            SpawnSingleDebris(type, new Vector3(0f, -2.05f, 0f));
        foreach (var item in _spawnedDebris)
        {
            item.worldObject.SetActive(false);
            _collectedDebris.Add(item);
        }
        _initialDebrisCount = _collectedDebris.Count;
        _objectiveProgress = _objectiveTarget;
        _spawnedDebris.Clear();
        StartCoroutine(SortingPhaseRoutine());
    }
#endif

    private void SwitchToMinigameCamera()
    {
        _disabledSceneCams.Clear();
        var allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in allCams)
        {
            if (c != _minigameCam && c.enabled && c.targetTexture == null)
            {
                c.enabled = false;
                _disabledSceneCams.Add(c);
            }
        }
        if (_minigameCam != null) _minigameCam.enabled = true;
    }

    private void RestoreSceneCamera()
    {
        if (_minigameCam != null) _minigameCam.enabled = false;
        foreach (var c in _disabledSceneCams)
        {
            if (c != null) c.enabled = true;
        }
        _disabledSceneCams.Clear();

        // No need to restore canvas properties since we now use a dedicated canvas that gets destroyed.
    }

    // -----------------------------------------------------------------------
    // 3D Stage Construction
    // -----------------------------------------------------------------------

    private void Build3DStage()
    {
        if (_stageRoot != null) Destroy(_stageRoot);

        _stageRoot = new GameObject("CleanupMinigameStage");
        _stageRoot.transform.position = StageOrigin;

        // 1. Minigame Camera
        var camGO = new GameObject("MinigameCamera", typeof(Camera));
        camGO.transform.SetParent(_stageRoot.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.4f, -11.0f);
        camGO.transform.localRotation = Quaternion.identity;

        _minigameCam = camGO.GetComponent<Camera>();
        _minigameCam.clearFlags      = CameraClearFlags.SolidColor;
        _minigameCam.backgroundColor = new Color(0.08f, 0.35f, 0.52f, 1f);
        _minigameCam.fieldOfView     = 46f;
        _minigameCam.nearClipPlane   = 0.3f;
        _minigameCam.farClipPlane    = 50f;
        _minigameCam.depth           = 100;

        var camData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (camData != null)
        {
            camData.renderPostProcessing = false;
        }

        // 2. Underwater Lighting (Key Light + Fill Light to prevent dark murky scenes)
        var lightGO = new GameObject("StageLight", typeof(Light));
        lightGO.transform.SetParent(_stageRoot.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 6f, -4f);
        var l = lightGO.GetComponent<Light>();
        l.type      = LightType.Directional;
        l.color     = new Color(0.85f, 0.95f, 1.0f);
        l.intensity = 0.3f;
        lightGO.transform.localRotation = Quaternion.Euler(50f, -15f, 0f);

        var fillGO = new GameObject("StageFillLight", typeof(Light));
        fillGO.transform.SetParent(_stageRoot.transform, false);
        var fill = fillGO.GetComponent<Light>();
        fill.type      = LightType.Directional;
        fill.color     = new Color(0.35f, 0.65f, 0.85f);
        fill.intensity = 0.5f;
        fillGO.transform.localRotation = Quaternion.Euler(-35f, 165f, 0f);

        // 3. Seabed Sand Floor
        var floorGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorGO.name = "SeabedFloor";
        floorGO.transform.SetParent(_stageRoot.transform, false);
        floorGO.transform.localPosition = new Vector3(0f, -3.2f, 0f);
        floorGO.transform.localScale    = new Vector3(22f, 1.2f, 8f);
        floorGO.GetComponent<Renderer>().material = CreateMaterial(new Color(0.72f, 0.65f, 0.48f));

        // 4. Overhead Gantry Track
        var trackGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trackGO.name = "GantryTrack";
        trackGO.transform.SetParent(_stageRoot.transform, false);
        trackGO.transform.localPosition = new Vector3(0f, 4.3f, 0f);
        trackGO.transform.localScale    = new Vector3(18f, 0.35f, 0.8f);
        trackGO.GetComponent<Renderer>().material = CreateMaterial(new Color(0.2f, 0.26f, 0.34f));

        // 5. Trolley Carriage
        if (customTrolleyPrefab != null)
        {
            var tObj = Instantiate(customTrolleyPrefab, _stageRoot.transform);
            tObj.name = "TrolleyCarriage";
            tObj.transform.localPosition = new Vector3(0f, 4.1f, 0f);
            _trolley = tObj.transform;
        }
        else
        {
            var trolleyGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trolleyGO.name = "TrolleyCarriage";
            trolleyGO.transform.SetParent(_stageRoot.transform, false);
            trolleyGO.transform.localPosition = new Vector3(0f, 4.1f, 0f);
            trolleyGO.transform.localScale    = new Vector3(1.3f, 0.5f, 0.9f);
            trolleyGO.GetComponent<Renderer>().material = CreateMaterial(new Color(1.0f, 0.70f, 0.0f));
            _trolley = trolleyGO.transform;

            var spotGO = new GameObject("WorkLight", typeof(Light));
            spotGO.transform.SetParent(_trolley, false);
            spotGO.transform.localPosition = Vector3.down * 0.3f;
            spotGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var spot = spotGO.GetComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 65f;
            spot.range = 10f;
            spot.intensity = 3.5f;
            spot.color = new Color(0.85f, 0.95f, 1f);
        }

        // 6. Claw Head
        if (customClawPrefab != null)
        {
            var cObj = Instantiate(customClawPrefab, _stageRoot.transform);
            cObj.name = "ClawHead";
            cObj.transform.localPosition = new Vector3(0f, _clawRestY, 0f);
            _clawHead = cObj.transform;
        }
        else
        {
            var clawGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clawGO.name = "ClawHead";
            clawGO.transform.SetParent(_stageRoot.transform, false);
            clawGO.transform.localPosition = new Vector3(0f, _clawRestY, 0f);
            clawGO.transform.localScale    = new Vector3(1.1f, 0.85f, 1.1f);
            clawGO.GetComponent<Renderer>().material = CreateMaterial(new Color(0.35f, 0.40f, 0.48f));
            _clawHead = clawGO.transform;

            var lj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lj.name = "LeftJaw";
            lj.transform.SetParent(_clawHead, false);
            lj.transform.localPosition = new Vector3(-0.38f, -0.38f, 0f);
            lj.transform.localScale    = new Vector3(0.18f, 0.65f, 0.25f);
            lj.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            lj.GetComponent<Renderer>().material = CreateMaterial(new Color(0.9f, 0.25f, 0.25f));
            _leftJaw = lj.transform;

            var rj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rj.name = "RightJaw";
            rj.transform.SetParent(_clawHead, false);
            rj.transform.localPosition = new Vector3(0.38f, -0.38f, 0f);
            rj.transform.localScale    = new Vector3(0.18f, 0.65f, 0.25f);
            rj.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            rj.GetComponent<Renderer>().material = CreateMaterial(new Color(0.9f, 0.25f, 0.25f));
            _rightJaw = rj.transform;
        }

        // 7. Cable LineRenderer
        _cableRenderer = _trolley.GetComponent<LineRenderer>();
        if (_cableRenderer == null)
        {
            _cableRenderer = _trolley.gameObject.AddComponent<LineRenderer>();
        }
        _cableRenderer.positionCount = 2;
        _cableRenderer.startWidth    = 0.08f;
        _cableRenderer.endWidth      = 0.08f;
        _cableRenderer.material      = CreateMaterial(new Color(0.15f, 0.15f, 0.15f));
    }

    // -----------------------------------------------------------------------
    // Spawning 3D Debris & Obstacles
    // -----------------------------------------------------------------------

    private void SpawnDebrisAndObstacles()
    {
        int plastics = _targetCluster != null ? _targetCluster.PlasticCount : 2;
        int metals = _targetCluster != null ? _targetCluster.MetalCount : 2;
        int hazards = _targetCluster != null ? _targetCluster.HazardCount : 1;
        var variations = new List<CleanupRoundRules.Variation> { CleanupRoundRules.Variation.MixedCleanup };
        if (plastics > 0) variations.Add(CleanupRoundRules.Variation.CurrentSweep);
        if (metals > 0) variations.Add(CleanupRoundRules.Variation.HeavyRecovery);
        if (hazards > 0) variations.Add(CleanupRoundRules.Variation.PriorityRecovery);
        if (variations.Count > 1) variations.Remove((CleanupRoundRules.Variation)_previousVariation);
        _variation = variations[UnityEngine.Random.Range(0, variations.Count)];
        _previousVariation = (int)_variation;
        var itemsToSpawn = CleanupRoundRules.BuildManifest(_zoneIndex, plastics, metals, hazards,
            _variation, new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));
        _initialDebrisCount = itemsToSpawn.Count;
        for (int i = 0; i < itemsToSpawn.Count; i++)
        {
            SpawnSingleDebris(itemsToSpawn[i], new Vector3(0f, -2.05f, 0f));
        }
        // Measure the enlarged models before distributing the available seabed space.
        var widths = new List<float>();
        foreach (var item in _spawnedDebris) widths.Add(MeasureCollectionModel(item.worldObject).size.x);
        float[] positions = CleanupRoundRules.CollectionPositions(widths, 6f);
        for (int i = 0; i < _spawnedDebris.Count; i++)
        {
            var item = _spawnedDebris[i];
            Vector3 position = item.worldObject.transform.localPosition;
            position.x += positions[i] - (MeasureCollectionModel(item.worldObject).center.x - _stageRoot.transform.position.x);
            item.worldObject.transform.localPosition = position;
            item.restPosition = position;
        }
        _objectiveTarget = _variation == CleanupRoundRules.Variation.MixedCleanup
            ? Mathf.Max(1, Mathf.CeilToInt(_initialDebrisCount * 0.7f))
            : _spawnedDebris.FindAll(item => item.category == ObjectiveCategory()).Count;
        _roundObjectiveText = CreateUIText(_dedicatedCanvasGO.transform, "CleanupObjective", "", 19f,
            new Vector2(0f, 1f), TextAlignmentOptions.TopLeft, new Color(0.2f, 1f, 0.9f), new Vector2(700f, 60f));
        _roundObjectiveText.rectTransform.pivot = new Vector2(0f, 1f);
        _roundObjectiveText.rectTransform.anchoredPosition = new Vector2(75f, -12f);
        UpdateRoundObjective();
        SpawnObstacles();
    }

    private void ResolveModelDefaults()
    {
        var models = Resources.Load<CleanupModelLibrary>("Minigames/CleanupModelLibrary");
        if (models == null) return;
        if (prefabWaterBottle == null) prefabWaterBottle = models.waterBottle;
        if (prefabPlasticBag == null) prefabPlasticBag = models.plasticBag;
        if (prefabSixPackRings == null) prefabSixPackRings = models.sixPackRings;
        if (prefabSodaCan == null) prefabSodaCan = models.sodaCan;
        if (prefabMetalPipe == null) prefabMetalPipe = models.metalPipe;
        if (prefabMetalTube == null) prefabMetalTube = models.metalTube;
        if (prefabMetalDrum == null) prefabMetalDrum = models.metalDrum;
        if (prefabBatterySmall == null) prefabBatterySmall = models.aaBattery;
        if (prefabBatteryLarge == null) prefabBatteryLarge = models.submarineBatteryCell;
        if (prefabElectronicWaste == null) prefabElectronicWaste = models.electronicBoard;
        if (prefabPlasticBin == null) prefabPlasticBin = models.plasticBin;
        if (prefabMetalBin == null) prefabMetalBin = models.metalBin;
        if (prefabHazardBin == null) prefabHazardBin = models.hazardBin;
    }

    private DebrisCategory ObjectiveCategory() => _variation == CleanupRoundRules.Variation.CurrentSweep
        ? DebrisCategory.Plastic : _variation == CleanupRoundRules.Variation.HeavyRecovery
            ? DebrisCategory.Metal : DebrisCategory.Hazardous;

    private string ObjectiveName() => _variation == CleanupRoundRules.Variation.CurrentSweep ? "CURRENT SWEEP" :
        _variation == CleanupRoundRules.Variation.HeavyRecovery ? "HEAVY RECOVERY" :
        _variation == CleanupRoundRules.Variation.PriorityRecovery ? "PRIORITY RECOVERY" : "MIXED CLEANUP";

    private void UpdateRoundObjective()
    {
        if (_roundObjectiveText == null) return;
        string target = _variation == CleanupRoundRules.Variation.MixedCleanup ? "items" :
            _variation == CleanupRoundRules.Variation.CurrentSweep ? "plastics" :
            _variation == CleanupRoundRules.Variation.HeavyRecovery ? "metals" : "hazardous items";
        _roundObjectiveText.text = $"Bonus Mission: {ObjectiveName()}\nRecover {target}: {_objectiveProgress}/{_objectiveTarget} (+15 RDP)";
        _roundObjectiveText.raycastTarget = false;
    }

    private void UpdateDebrisDrift()
    {
        bool currentSweep = _variation == CleanupRoundRules.Variation.CurrentSweep;
        float amplitude = currentSweep ? 0.8f : 0.45f;
        float rise = currentSweep ? 0.6f : 0.35f;
        float speed = 0.9f + Mathf.Clamp(_zoneIndex, 0, 4) * 0.15f;
        foreach (var item in _spawnedDebris)
        {
            if (item.category != DebrisCategory.Plastic || item.worldObject == null || item.isFalling) continue;
            float phase = Time.time * speed + item.driftPhase;
            // Buoyant motion stays above the resting height and within claw reach.
            Vector3 position = item.restPosition + new Vector3(Mathf.Sin(phase) * amplitude,
                (Mathf.Sin(phase * 0.75f) + 1f) * 0.5f * rise, 0f);
            Bounds bounds = GetDebrisBounds(item);
            float centerOffset = bounds.center.x - item.worldObject.transform.position.x;
            float edge = Mathf.Max(0f, 6f - bounds.extents.x);
            position.x = Mathf.Clamp(position.x + centerOffset, -edge, edge) - centerOffset;
            item.worldObject.transform.localPosition = position;
        }
    }

    private void SpawnSingleDebris(DebrisItemType type, Vector3 localPos)
    {
        var deb = new DebrisInstance { itemType = type };
        GameObject go = null;
        bool isCustom = false;
        float targetDim = 0.70f;

        switch (type)
        {
            case DebrisItemType.WaterBottle:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Plastic Water Bottle";
                deb.itemColor = new Color(0.2f, 0.75f, 1.0f, 0.9f);
                isCustom = prefabWaterBottle != null;
                go = isCustom ? Instantiate(prefabWaterBottle, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom) go.transform.localScale = new Vector3(0.30f, 0.50f, 0.30f);
                targetDim = 0.50f;
                break;

            case DebrisItemType.PlasticBag:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Plastic Shopping Bag";
                deb.itemColor = new Color(0.92f, 0.92f, 0.98f, 0.85f);
                isCustom = prefabPlasticBag != null;
                go = isCustom ? Instantiate(prefabPlasticBag, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!isCustom) go.transform.localScale = new Vector3(0.55f, 0.48f, 0.18f);
                targetDim = 0.55f;
                break;

            case DebrisItemType.SixPackRings:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Six-Pack Plastic Rings";
                deb.itemColor = new Color(0.65f, 0.88f, 1.0f, 0.85f);
                isCustom = prefabSixPackRings != null;
                go = isCustom ? Instantiate(prefabSixPackRings, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom) go.transform.localScale = new Vector3(0.48f, 0.08f, 0.36f);
                targetDim = 0.48f;
                break;

            case DebrisItemType.SodaCan:
                deb.category = DebrisCategory.Metal;
                deb.displayName = "Aluminum Soda Can";
                deb.itemColor = new Color(0.90f, 0.18f, 0.18f);
                isCustom = prefabSodaCan != null;
                go = isCustom ? Instantiate(prefabSodaCan, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom) go.transform.localScale = new Vector3(0.32f, 0.40f, 0.32f);
                targetDim = 0.40f;
                break;

            case DebrisItemType.MetalPipe:
            case DebrisItemType.MetalTube:
                deb.category = DebrisCategory.Metal;
                deb.displayName = type == DebrisItemType.MetalTube ? "Metal Tube" : "Rusted Metal Pipes";
                deb.itemColor = new Color(0.60f, 0.48f, 0.36f);
                var pipePrefab = type == DebrisItemType.MetalTube ? prefabMetalTube : prefabMetalPipe;
                isCustom = pipePrefab != null;
                go = isCustom ? Instantiate(pipePrefab, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom)
                {
                    go.transform.localScale = new Vector3(0.50f, 1.15f, 0.50f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, 75f);
                }
                targetDim = 1.15f;
                break;

            case DebrisItemType.MetalDrum:
                deb.category = DebrisCategory.Metal;
                deb.displayName = "Rusted Oil Drum";
                deb.itemColor = new Color(0.40f, 0.32f, 0.24f);
                isCustom = prefabMetalDrum != null;
                go = isCustom ? Instantiate(prefabMetalDrum, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom) go.transform.localScale = new Vector3(1.05f, 1.40f, 1.05f);
                targetDim = 1.40f;
                break;

            case DebrisItemType.BatterySmall:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Discarded AA Battery";
                deb.itemColor = new Color(1.0f, 0.85f, 0.1f);
                isCustom = prefabBatterySmall != null;
                go = isCustom ? Instantiate(prefabBatterySmall, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (!isCustom) go.transform.localScale = new Vector3(0.24f, 0.35f, 0.24f);
                targetDim = 0.25f;
                break;

            case DebrisItemType.BatteryLarge:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Submarine Battery Cell";
                deb.itemColor = new Color(0.2f, 0.85f, 0.35f);
                isCustom = prefabBatteryLarge != null;
                go = isCustom ? Instantiate(prefabBatteryLarge, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!isCustom) go.transform.localScale = new Vector3(0.95f, 0.78f, 0.68f);
                targetDim = 0.95f;
                break;

            case DebrisItemType.ElectronicWaste:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Electronic Circuit Board";
                deb.itemColor = new Color(0.12f, 0.65f, 0.25f);
                isCustom = prefabElectronicWaste != null;
                go = isCustom ? Instantiate(prefabElectronicWaste, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!isCustom) go.transform.localScale = new Vector3(0.55f, 0.10f, 0.48f);
                targetDim = 0.55f;
                break;
        }

        if (go != null)
        {
            go.name = deb.displayName;
            go.transform.SetParent(_stageRoot.transform, false);
            go.transform.localPosition = localPos;

            if (!isCustom)
            {
                // Procedural primitive fallback
                var rend = go.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    rend.material = CreateMaterial(deb.itemColor);
                }
            }
            float minimumVisibleSize = type == DebrisItemType.BatterySmall ? 0.5f : MinimumDebrisVisibleSize;
            NormalizeCollectionDebris(go, targetDim, localPos, minimumVisibleSize);

            deb.worldObject = go;
            deb.restPosition = go.transform.localPosition;
            deb.driftPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _spawnedDebris.Add(deb);
        }
    }

    private void NormalizeCollectionDebris(GameObject go, float targetMaxDim, Vector3 targetLocalPos,
        float minimumVisibleSize)
    {
        if (go == null) return;

        // Size the silhouette seen by the collection camera, rather than hidden model depth.
        // Measure geometry directly so spawn-time scaling does not depend on cached renderer bounds.
        Bounds bounds = MeasureCollectionModel(go);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y);
        if (maxDim > 0.001f)
        {
            float desiredDim = Mathf.Max(minimumVisibleSize, targetMaxDim * CollectionModelScale)
                * debrisModelScale;
            float scaleMultiplier = desiredDim / maxDim;
            go.transform.localScale = go.transform.localScale * scaleMultiplier;
        }

        Bounds postBounds = MeasureCollectionModel(go);
        // Ground both imported and fallback models without changing their internal mesh offsets.
        float floorWorldY = _stageRoot.transform.position.y - 2.60f;
        float yCorrection = floorWorldY - postBounds.min.y;
        float xCorrection = (_stageRoot.transform.position.x + targetLocalPos.x) - postBounds.center.x;
        float zCorrection = (_stageRoot.transform.position.z + 0f) - postBounds.center.z;

        go.transform.position += new Vector3(xCorrection, yCorrection, zCorrection);
    }

    private static Bounds MeasureCollectionModel(GameObject model)
    {
        if (CreatureGeometry.TryMeasure(model.transform, model.transform.rotation, out Bounds bounds))
        {
            bounds.center += model.transform.position;
            return bounds;
        }
        return GetModelBounds(model);
    }

    private void SpawnObstacles()
    {
        if (prefabCoralObstacle != null)
        {
            for (int i = 0; i < 2; i++)
            {
                float cx = i == 0 ? -2.4f : 2.4f;
                GameObject coralGO = Instantiate(prefabCoralObstacle, _stageRoot.transform);

                coralGO.name = "Obstacle_Coral";
                coralGO.transform.SetParent(_stageRoot.transform, false);
                coralGO.transform.localPosition = new Vector3(cx, -2.1f, 0f);
                coralGO.transform.localScale    = Vector3.one * 1.6f;

                // Make sure colliders are triggers so they don't push physics bodies
                foreach (var col in coralGO.GetComponentsInChildren<Collider>())
                    col.isTrigger = true;

                // Note: Coral is decorative scenery, NOT an obstacle with eco disturbance penalty.
                // Do not add to _spawnedObstacles so dropping onto empty seabed incurs 0 penalty.
            }
        }

        for (int i = 0; i < 2; i++)
        {
            // Keep the swimming path on a centered root; imported mesh pivots may be offset.
            var fishGO = new GameObject($"Obstacle_Fish_{i}");
            fishGO.transform.SetParent(_stageRoot.transform, false);
            float fy = i == 0 ? 0.6f : -0.7f;
            float fx = i == 0 ? -6.5f : 6.5f;
            fishGO.transform.localPosition = new Vector3(fx, fy, 0f);
            fishGO.transform.localRotation = Quaternion.Euler(0f, i == 0 ? -90f : 90f, 0f);
            GameObject fishModel = prefabFishObstacle != null
                ? Instantiate(prefabFishObstacle, fishGO.transform)
                : CreateProceduralFish();
            fishModel.transform.SetParent(fishGO.transform, false);
            fishModel.transform.localPosition = Vector3.zero;
            fishModel.transform.localRotation = Quaternion.identity;
            NormalizeFishScale(fishModel, FishVisibleSize);

            // Lock all physics constraints so claw hits cannot displace fish forward or backward in Z
            foreach (var col in fishGO.GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            var rbs = fishGO.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            StartCoroutine(FishSwimRoutine(fishGO, i == 0 ? 1f : -1f, fy));
            _spawnedObstacles.Add(fishGO);
        }
    }

    private static void NormalizeFishScale(GameObject fishGO, float targetMaxDim)
    {
        if (fishGO == null) return;
        fishGO.transform.localScale = Vector3.one;
        Vector3 center = fishGO.transform.position;
        Bounds bounds = MeasureCollectionModel(fishGO);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y);
        if (maxDim > 0.001f)
        {
            fishGO.transform.localScale = Vector3.one * (targetMaxDim / maxDim);
            fishGO.transform.position += center - MeasureCollectionModel(fishGO).center;
        }
    }

    private IEnumerator FishSwimRoutine(GameObject fish, float direction, float fixedY)
    {
        float speed = 2.6f;
        while (_isActive && fish != null)
        {
            Vector3 pos = fish.transform.localPosition;
            pos.x += direction * speed * Time.deltaTime;

            if (direction > 0 && pos.x > 7.5f)
            {
                pos.x = -7.5f;
            }
            else if (direction < 0 && pos.x < -7.5f)
            {
                pos.x = 7.5f;
            }

            pos.y = fixedY + Mathf.Sin(Time.time * 2.8f + pos.x) * 0.25f;
            pos.z = 0f; // Strictly lock Z so fish never moves forward or backward in 3D depth

            fish.transform.localPosition = pos;

            // Flip yaw 180 degrees so the fish faces forward into its movement direction (head-first)
            float yaw = direction > 0 ? -90f : 90f;
            fish.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    // UI Construction & Auto-wiring
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        // 1. Auto-discover custom UI panel in Canvas if unassigned
        if (customUIRoot == null)
        {
            if (gameObject.name.IndexOf("Minigame3", StringComparison.OrdinalIgnoreCase) >= 0 || GetComponent<RectTransform>() != null)
            {
                customUIRoot = gameObject;
            }
            else
            {
                var found = GameObject.Find("Minigame3HUD") ?? GameObject.Find("Minigame3_HUD") ?? GameObject.Find("Minigame3");
                if (found == null)
                {
                    var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var c in allCanvases)
                    {
                        for (int i = 0; i < c.transform.childCount; i++)
                        {
                            var child = c.transform.GetChild(i);
                            if (child.name.IndexOf("Minigame3", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                found = child.gameObject;
                                break;
                            }
                        }
                        if (found != null) break;
                    }
                }
                if (found != null) customUIRoot = found;
            }
        }

        // 2. Setup Dedicated Canvas for Minigame
        if (_dedicatedCanvasGO != null) Destroy(_dedicatedCanvasGO);

        _dedicatedCanvasGO = new GameObject("Minigame3_DedicatedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _dedicatedCanvasGO.layer = LayerMask.NameToLayer("UI");
        _parentCanvas = _dedicatedCanvasGO.GetComponent<Canvas>();
        _parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _parentCanvas.sortingOrder = 100;

        var scaler = _dedicatedCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 3. If Custom UI Root exists, reparent it and wire it up
        if (customUIRoot != null)
        {
            _originalUIParent = customUIRoot.transform.parent;
            customUIRoot.transform.SetParent(_dedicatedCanvasGO.transform, false);

            var rect = customUIRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            customUIRoot.SetActive(true);

            // Disable the full-screen 78% black tint on Minigame3HUD so the 3D underwater scene is bright and not darkened
            var rootImg = customUIRoot.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.enabled = false;
                rootImg.color = new Color(0f, 0f, 0f, 0f);
                rootImg.raycastTarget = false;
            }

            // Auto-wire Header
            if (customTimerText == null) customTimerText = FindDeepChild<TMP_Text>(customUIRoot, "Timer", "TimerText", "Time");
            if (customTimerTextLegacy == null && customTimerText == null) customTimerTextLegacy = FindDeepChild<Text>(customUIRoot, "Timer", "TimerText", "Time");

            if (customScoreText == null) customScoreText = FindDeepChild<TMP_Text>(customUIRoot, "Score", "ScoreText", "Collect");
            if (customScoreTextLegacy == null && customScoreText == null) customScoreTextLegacy = FindDeepChild<Text>(customUIRoot, "Score", "ScoreText", "Collect");

            // Ensure timer text is correctly positioned and anchored, leaving room for pause button (-50 to -200)
            if (customTimerText != null)
            {
                var tRect = customTimerText.GetComponent<RectTransform>();
                if (tRect != null)
                {
                    tRect.anchorMin = new Vector2(1f, 1f);
                    tRect.anchorMax = new Vector2(1f, 1f);
                    tRect.pivot     = new Vector2(1f, 1f);
                    tRect.anchoredPosition = new Vector2(-230f, -20f);
                    tRect.sizeDelta = new Vector2(200f, 50f);
                    customTimerText.alignment = TextAlignmentOptions.Right;
                    customTimerText.verticalAlignment = VerticalAlignmentOptions.Middle;
                }
            }

            // Ensure score text is placed cleanly to the left of the timer without overlapping
            if (customScoreText != null)
            {
                var sRect = customScoreText.GetComponent<RectTransform>();
                if (sRect != null)
                {
                    sRect.anchorMin = new Vector2(1f, 1f);
                    sRect.anchorMax = new Vector2(1f, 1f);
                    sRect.pivot     = new Vector2(1f, 1f);
                    sRect.anchoredPosition = new Vector2(-460f, -20f);
                    sRect.sizeDelta = new Vector2(220f, 50f);
                    customScoreText.alignment = TextAlignmentOptions.Right;
                    customScoreText.verticalAlignment = VerticalAlignmentOptions.Middle;
                }
            }

            // Immediately initialize labels with uppercase formatted text to avoid unstyled placeholder flash
            SetText(customTimerText, customTimerTextLegacy, $"TIME: 0:{CleanupRoundRules.PhaseDurationSeconds:00}");
            SetText(customScoreText, customScoreTextLegacy, "COLLECT: 0");

            // Fallbacks in case HUD does not contain Timer/Score
            if (customTimerText == null && customTimerTextLegacy == null)
            {
                customTimerText = CreateUIText(_dedicatedCanvasGO.transform, "TimerText_Auto", $"TIME: 0:{CleanupRoundRules.PhaseDurationSeconds:00}", 28, new Vector2(0.85f, 0.94f), TextAlignmentOptions.Right, Color.white);
            }
            if (customScoreText == null && customScoreTextLegacy == null)
            {
                customScoreText = CreateUIText(_dedicatedCanvasGO.transform, "ScoreText_Auto", "COLLECT: 0", 28, new Vector2(0.55f, 0.94f), TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.2f));
            }

            if (customStatusText == null) customStatusText = FindDeepChild<TMP_Text>(customUIRoot, "Status", "StatusText", "Banner");
            if (customStatusTextLegacy == null && customStatusText == null) customStatusTextLegacy = FindDeepChild<Text>(customUIRoot, "Status", "StatusText", "Banner");
            if (customStatusText != null) customStatusText.gameObject.SetActive(false);
            if (customStatusTextLegacy != null) customStatusTextLegacy.gameObject.SetActive(false);

            // Auto-wire Phase 1 Controls
            if (customDropClawButton == null) customDropClawButton = FindDeepChild<Button>(customUIRoot, "DropButton", "DropClaw", "Drop", "BtnDrop");
            if (customJoystickBackground == null) customJoystickBackground = FindDeepChild<RectTransform>(customUIRoot, "MoveOuterRing", "Joystick", "MoveRing");
            if (customJoystickHandle == null) customJoystickHandle = FindDeepChild<RectTransform>(customUIRoot, "Move", "Handle", "Thumb");

            // Auto-wire Phase 2 Controls
            if (customBinPlastics == null) customBinPlastics = FindDeepChild<Button>(customUIRoot, "BinPlastics", "Plastics", "Plastic");
            if (customBinMetals == null) customBinMetals = FindDeepChild<Button>(customUIRoot, "BinMetals", "Metals", "Metal");
            if (customBinHazardous == null) customBinHazardous = FindDeepChild<Button>(customUIRoot, "BinHazardous", "Hazardous", "Hazard");
            if (customSortingItemText == null) customSortingItemText = FindDeepChild<TMP_Text>(customUIRoot, "ItemName", "SortingText", "Item");
            if (customSortingItemTextLegacy == null && customSortingItemText == null) customSortingItemTextLegacy = FindDeepChild<Text>(customUIRoot, "ItemName", "SortingText", "Item");

            // Auto-wire Phase 3 Controls
            if (customReturnButton == null) customReturnButton = FindDeepChild<Button>(customUIRoot, "ReturnButton", "BtnReturn", "Return");
            if (customResultsSummaryText == null) customResultsSummaryText = FindDeepChild<TMP_Text>(customUIRoot, "ResultsSummary", "Summary", "ResultsText");
            if (customResultsSummaryTextLegacy == null && customResultsSummaryText == null) customResultsSummaryTextLegacy = FindDeepChild<Text>(customUIRoot, "ResultsSummary", "Summary", "ResultsText");

            // Show Phase 1 elements
            if (customExtractionContainer != null) customExtractionContainer.SetActive(true);
            if (customDropClawButton != null)
            {
                customDropClawButton.gameObject.SetActive(true);
                customDropClawButton.interactable = true;
                customDropClawButton.onClick.RemoveAllListeners();
                customDropClawButton.onClick.AddListener(OnDropClawPressed);
            }
            if (customJoystickBackground != null)
            {
                customJoystickBackground.gameObject.SetActive(true);
                var img = customJoystickBackground.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                WireJoystickEvents(customJoystickBackground.gameObject);
            }
            if (customJoystickHandle != null)
            {
                var hImg = customJoystickHandle.GetComponent<Image>();
                if (hImg != null) hImg.raycastTarget = false;
            }

            // Style buttons and joystick on Minigame3HUD to match main HUD (dark oceanic base + cyan inner glow)
            StyleMinigame3ButtonsLikeHUD();

            // Hide Phase 2 & 3 elements on start
            if (customSortingContainer != null) customSortingContainer.SetActive(false);
            if (customBinPlastics != null) customBinPlastics.gameObject.SetActive(false);
            if (customBinMetals != null) customBinMetals.gameObject.SetActive(false);
            if (customBinHazardous != null) customBinHazardous.gameObject.SetActive(false);
            if (customSortingItemText != null) customSortingItemText.gameObject.SetActive(false);
            if (customSortingItemTextLegacy != null) customSortingItemTextLegacy.gameObject.SetActive(false);

            if (customResultsContainer != null) customResultsContainer.SetActive(false);
            if (customResultsSummaryText != null) customResultsSummaryText.gameObject.SetActive(false);
            if (customResultsSummaryTextLegacy != null) customResultsSummaryTextLegacy.gameObject.SetActive(false);
            if (customReturnButton != null) customReturnButton.gameObject.SetActive(false);

            _dropButton = customDropClawButton;
            _joystickHandle = customJoystickHandle;
            _joystickBackground = customJoystickBackground;

            UIManager.CreateMinigamePauseButton(_dedicatedCanvasGO != null ? _dedicatedCanvasGO.transform : customUIRoot.transform);
            return;
        }

        // 4. Fallback: Procedural UI
        var topBar = CreateUIPanel(_dedicatedCanvasGO.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(850f, 80f), new Color(0.02f, 0.08f, 0.15f, 0.92f));
        customScoreText = CreateUIText(topBar.transform, "CollectText", "COLLECT: 0", 26, new Vector2(0.20f, 0.5f), TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f));
        CreateUIText(topBar.transform, "PhaseTitle", "REMOVAL PHASE", 36, new Vector2(0.50f, 0.5f), TextAlignmentOptions.Center, new Color(0f, 0.93f, 0.85f));
        customTimerText = CreateUIText(topBar.transform, "TimerText", $"TIME: 0:{CleanupRoundRules.PhaseDurationSeconds:00}", 26, new Vector2(0.80f, 0.5f), TextAlignmentOptions.Center, Color.white);

        BuildVirtualJoystick();

        _dropButton = CreateUIButton(_dedicatedCanvasGO.transform, "BtnDropClaw", "⬇ TAP TO\nDROP CLAW", new Vector2(0.85f, 0.18f), new Vector2(230f, 120f), OnDropClawPressed, new Color(0f, 0.72f, 0.65f, 0.95f));

        UIManager.CreateMinigamePauseButton(_dedicatedCanvasGO != null ? _dedicatedCanvasGO.transform : (customUIRoot != null ? customUIRoot.transform : transform));
    }

    private void BuildVirtualJoystick()
    {
        var joyRoot = CreateUIPanel(_dedicatedCanvasGO.transform, "MinigameJoystick", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(180f, 180f), new Vector2(200f, 200f), new Color(0.05f, 0.15f, 0.25f, 0.65f));
        _joystickBackground = joyRoot.GetComponent<RectTransform>();

        var handleGO = CreateUIPanel(joyRoot.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(85f, 85f), new Color(0f, 0.93f, 0.85f, 0.9f));
        _joystickHandle = handleGO.GetComponent<RectTransform>();

        var lbl = CreateUIText(joyRoot.transform, "JoyLabel", "MOVE CLAW", 15, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, Color.white);
        lbl.raycastTarget = false;

        WireJoystickEvents(joyRoot);
    }

    private void WireJoystickEvents(GameObject joyRoot)
    {
        var trigger = joyRoot.GetComponent<EventTrigger>() ?? joyRoot.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) => OnJoystickPointerDown((PointerEventData)data));
        trigger.triggers.Add(downEntry);

        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => OnJoystickDrag((PointerEventData)data));
        trigger.triggers.Add(dragEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((_) => OnJoystickPointerUp());
        trigger.triggers.Add(upEntry);
    }

    private void OnJoystickPointerDown(PointerEventData eventData)
    {
        _isDraggingJoystick = true;
        OnJoystickDrag(eventData);
    }

    private void OnJoystickDrag(PointerEventData eventData)
    {
        if (_joystickBackground == null || _joystickHandle == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        // Subtract the visual center of _joystickBackground so offset is 0 at center regardless of pivot!
        Vector2 centerOffset = localPoint - _joystickBackground.rect.center;

        // Radius constraint: keep handle nicely within outer ring (40% of ring width)
        float bgWidth = _joystickBackground.rect.width > 10f ? _joystickBackground.rect.width : 100f;
        float maxRadius = bgWidth * 0.40f;

        float clampedX = Mathf.Clamp(centerOffset.x, -maxRadius, maxRadius);
        _joystickHandle.anchoredPosition = new Vector2(clampedX, 0f);

        _joystickHorizontalInput = Mathf.Clamp(clampedX / maxRadius, -1f, 1f);
    }

    private void OnJoystickPointerUp()
    {
        _isDraggingJoystick = false;
        _joystickHorizontalInput = 0f;
        if (_joystickHandle != null) _joystickHandle.anchoredPosition = Vector2.zero;
    }

    private void StyleMinigame3ButtonsLikeHUD()
    {
        Color btnDark = UIManager.BtnDefault;
        Color innerGlowColor = UIManager.InnerGlowIdle;

        // 1. MoveOuterRing (Joystick Base Ring - identical to Exploration HUD)
        if (customJoystickBackground != null)
        {
            customJoystickBackground.anchoredPosition = new Vector2(75f, 75f);

            var ringImg = customJoystickBackground.GetComponent<Image>();
            if (ringImg != null)
            {
                ringImg.color = btnDark;
            }
            var glow = UIManager.EnsureInnerGlow(customJoystickBackground.gameObject, 0.85f, innerGlowColor);
            if (glow != null) glow.transform.SetSiblingIndex(0);
        }

        // 2. Move (Joystick Handle Thumb - solid dark circle like in HUD picture 1, NO inner glow)
        if (customJoystickHandle != null)
        {
            // Ensure proper center anchoring & zero initial offset
            customJoystickHandle.anchorMin = new Vector2(0.5f, 0.5f);
            customJoystickHandle.anchorMax = new Vector2(0.5f, 0.5f);
            customJoystickHandle.pivot     = new Vector2(0.5f, 0.5f);
            customJoystickHandle.anchoredPosition = Vector2.zero;

            // Disable conflicting OnScreenStick so it doesn't fight our drag handler
            var onScreenStick = customJoystickHandle.GetComponent<UnityEngine.InputSystem.OnScreen.OnScreenStick>();
            if (onScreenStick != null) onScreenStick.enabled = false;

            var oldGlow = customJoystickHandle.transform.Find("InnerGlow");
            if (oldGlow != null)
            {
                if (Application.isPlaying) Destroy(oldGlow.gameObject);
                else DestroyImmediate(oldGlow.gameObject);
            }

            var handleImg = customJoystickHandle.GetComponent<Image>();
            if (handleImg != null)
            {
                handleImg.color = btnDark;
            }
        }

        // 3. DropButton (Claw Drop Button with Hand Icon)
        if (customDropClawButton != null)
        {
            var dropRt = customDropClawButton.GetComponent<RectTransform>();
            if (dropRt != null)
            {
                dropRt.anchoredPosition = new Vector2(-75f, 75f);
            }

            var dropImg = customDropClawButton.GetComponent<Image>();
            if (dropImg != null)
            {
                // Ensure child Icon exists so InnerGlow sits behind the hand icon
                var iconTr = customDropClawButton.transform.Find("Icon");
                Image iconImg = null;
                if (iconTr != null)
                {
                    iconImg = iconTr.GetComponent<Image>();
                }
                else
                {
                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGO.transform.SetParent(customDropClawButton.transform, false);
                    var irt = iconGO.GetComponent<RectTransform>();
                    irt.anchorMin = Vector2.zero;
                    irt.anchorMax = Vector2.one;
                    irt.sizeDelta = Vector2.zero;
                    irt.anchoredPosition = Vector2.zero;

                    iconImg = iconGO.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;
                    if (dropImg.sprite != null) iconImg.sprite = dropImg.sprite;
                }

                // Set parent image to outer ring sprite
                var ringSprite = UIManager.GetOuterRingSprite();
                if (ringSprite != null)
                {
                    dropImg.sprite = ringSprite;
                    dropImg.preserveAspect = true;
                }
                dropImg.color = btnDark;

                // Inner radial glow
                var dropGlow = UIManager.EnsureInnerGlow(customDropClawButton.gameObject, 0.85f, innerGlowColor);
                if (dropGlow != null) dropGlow.transform.SetSiblingIndex(0);

                if (iconImg != null)
                {
                    iconImg.color = btnDark;
                    iconImg.transform.SetAsLastSibling();
                }

                // Add pointer down / up glow illumination
                var trigger = customDropClawButton.GetComponent<EventTrigger>() ?? customDropClawButton.gameObject.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                var pDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                pDown.callback.AddListener((_) =>
                {
                    if (dropGlow != null) dropGlow.color = UIManager.InnerGlowActive;
                    if (dropImg != null) dropImg.color = UIManager.BtnActiveRing;
                    if (iconImg != null) iconImg.color = UIManager.BtnActiveIcon;
                });
                trigger.triggers.Add(pDown);

                var pUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                pUp.callback.AddListener((_) =>
                {
                    if (dropGlow != null) dropGlow.color = innerGlowColor;
                    if (dropImg != null) dropImg.color = btnDark;
                    if (iconImg != null) iconImg.color = btnDark;
                });
                trigger.triggers.Add(pUp);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Phase 1: Extraction Game Loop
    // -----------------------------------------------------------------------

    private IEnumerator ExtractionPhaseRoutine()
    {
        _phaseTimeRemaining = CleanupRoundRules.PhaseDurationSeconds;

        // Finish an in-flight drop/reel, including the last item, before changing phases.
        while ((_phaseTimeRemaining > 0f && _spawnedDebris.Count > 0) || _clawState != ClawState.Idle)
        {
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            _phaseTimeRemaining = Mathf.Max(0f, _phaseTimeRemaining - Time.deltaTime);
            int seconds = Mathf.CeilToInt(_phaseTimeRemaining);
            SetText(customTimerText, customTimerTextLegacy, $"TIME: 0:{seconds:00}");
            SetText(customScoreText, customScoreTextLegacy, $"COLLECT: {_collectedDebris.Count}");

            UpdateDebrisDrift();
            HandleInputAndMotion();
            yield return null;
        }

        StartCoroutine(SortingPhaseRoutine());
    }

    private void HandleInputAndMotion()
    {
        float moveAxis = _joystickHorizontalInput;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  moveAxis = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveAxis = 1f;

            if (kb.spaceKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                OnDropClawPressed();
            }
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            float padX = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(padX) > 0.1f && Mathf.Abs(padX) > Mathf.Abs(moveAxis))
                moveAxis = padX;

            if (pad.buttonSouth.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame)
            {
                OnDropClawPressed();
            }
        }

        if (_clawState == ClawState.Idle)
        {
            _trolleyX += moveAxis * _trolleySpeed * Time.deltaTime;
            _trolleyX = Mathf.Clamp(_trolleyX, -6.2f, 6.2f);
            if (_trolley != null) _trolley.localPosition = new Vector3(_trolleyX, 4.1f, 0f);
            if (_clawHead != null) _clawHead.localPosition = new Vector3(_trolleyX, _clawRestY, 0f);
        }

        if (_clawState == ClawState.Dropping)
        {
            _clawY -= _clawDropSpeed * Time.deltaTime;
            if (_clawHead != null) _clawHead.localPosition = new Vector3(_trolleyX, _clawY, 0f);

            if (_leftJaw != null) _leftJaw.localRotation = Quaternion.Euler(0f, 0f, -35f);
            if (_rightJaw != null) _rightJaw.localRotation = Quaternion.Euler(0f, 0f, 35f);

            if (CheckObstacleCollision())
            {
                TriggerEcoDisturbance();
                return;
            }

            DebrisInstance hitItem = CheckDebrisCollision();
            if (hitItem != null)
            {
                _grabbedItem = hitItem;
                _spawnedDebris.Remove(hitItem);
                hitItem.worldObject.transform.SetParent(_clawHead, true);
                hitItem.carefulRecovery = hitItem.category == DebrisCategory.Hazardous &&
                    Mathf.Abs(_clawHead.position.x - DebrisCenter(hitItem).x) <= 0.22f;
                _clawState = ClawState.Reeling;

                if (_leftJaw != null) _leftJaw.localRotation = Quaternion.Euler(0f, 0f, -10f);
                if (_rightJaw != null) _rightJaw.localRotation = Quaternion.Euler(0f, 0f, 10f);
            }
            else if (_clawY <= _clawSeabedY)
            {
                _clawState = ClawState.Reeling;
            }
        }
        else if (_clawState == ClawState.Reeling)
        {
            // Metals reel at half normal speed, so their return takes twice as long.
            float reelMultiplier = _grabbedItem != null && _grabbedItem.category == DebrisCategory.Metal ? 0.5f : 1f;
            _clawY += _clawReelSpeed * reelMultiplier * Time.deltaTime;
            if (_clawHead != null) _clawHead.localPosition = new Vector3(_trolleyX, _clawY, 0f);
            // Check both the claw and its cargo before registering a completed retrieval.
            if (_grabbedItem != null && CheckObstacleCollision()) TriggerEcoDisturbance();
            if (_clawY >= _clawRestY)
            {
                _clawY = _clawRestY;
                _clawState = ClawState.Idle;

                if (_grabbedItem != null)
                {
                    _collectedDebris.Add(_grabbedItem);
                    if (_grabbedItem.carefulRecovery) _carefulRecoveries++;
                    if (_variation == CleanupRoundRules.Variation.MixedCleanup || _grabbedItem.category == ObjectiveCategory())
                        _objectiveProgress = Mathf.Min(_objectiveTarget, _objectiveProgress + 1);
                    UpdateRoundObjective();
                    // Retain the exact model for the sorting thumbnail, including pipe variants.
                    _grabbedItem.worldObject.transform.SetParent(_stageRoot.transform, true);
                    _grabbedItem.worldObject.SetActive(false);
                    _grabbedItem = null;
                }

                if (_leftJaw != null) _leftJaw.localRotation = Quaternion.Euler(0f, 0f, -25f);
                if (_rightJaw != null) _rightJaw.localRotation = Quaternion.Euler(0f, 0f, 25f);

                if (_dropButton != null) _dropButton.interactable = true;
            }
            if (_clawHead != null) _clawHead.localPosition = new Vector3(_trolleyX, _clawY, 0f);
        }

        if (_cableRenderer != null && _trolley != null && _clawHead != null)
        {
            _cableRenderer.SetPosition(0, _trolley.position);
            _cableRenderer.SetPosition(1, _clawHead.position + Vector3.up * 0.35f);
        }

        bool isClawMoving = (_clawState == ClawState.Dropping || _clawState == ClawState.Reeling);
        AudioManager.Instance?.SetClawMoving(isClawMoving);
    }

    private void OnDropClawPressed()
    {
        if (_isActive && !_sortingActive && !_resultsShown && _phaseTimeRemaining > 0f && _clawState == ClawState.Idle)
        {
            _clawState = ClawState.Dropping;
            if (_dropButton != null) _dropButton.interactable = false;
        }
    }

    private DebrisInstance CheckDebrisCollision()
    {
        Vector3 clawPos = _clawHead != null ? _clawHead.position : Vector3.zero;
        DebrisInstance nearest = null;
        float nearestDistance = 0.45f * 0.45f;
        for (int i = 0; i < _spawnedDebris.Count; i++)
        {
            var deb = _spawnedDebris[i];
            if (deb.worldObject == null || deb.isFalling) continue;
            Bounds bounds = GetDebrisBounds(deb);
            float dist = CleanupRoundRules.DistanceToDebrisSquared(clawPos.x, clawPos.y,
                bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y);
            if (dist < nearestDistance)
            {
                nearest = deb;
                nearestDistance = dist;
            }
        }
        return nearest;
    }

    private static Vector3 DebrisCenter(DebrisInstance item)
    {
        return GetDebrisBounds(item).center;
    }

    private static Bounds GetDebrisBounds(DebrisInstance item)
    {
        return GetModelBounds(item.worldObject);
    }

    private static Bounds GetModelBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private bool CheckObstacleCollision()
    {
        Vector3 clawPos = _clawHead != null ? _clawHead.position : Vector3.zero;
        Bounds? cargoBounds = _grabbedItem != null && _grabbedItem.worldObject != null
            ? GetDebrisBounds(_grabbedItem) : (Bounds?)null;
        foreach (var obs in _spawnedObstacles)
        {
            if (obs == null) continue;
            // Only swimming live species (fish) trigger eco disturbance
            if (!obs.name.StartsWith("Obstacle_Fish", StringComparison.OrdinalIgnoreCase)) continue;

            Bounds fishBounds = GetModelBounds(obs);
            Vector3 fishPos = fishBounds.center;
            // Follow the larger fish while excluding empty corners around its fins.
            float fishHalfWidth = fishBounds.extents.x * 0.85f;
            float fishHalfHeight = fishBounds.extents.y * 0.85f;
            if (CleanupRoundRules.TouchesFish(clawPos.x, clawPos.y, 0f, 0f,
                fishPos.x, fishPos.y, fishHalfWidth, fishHalfHeight))
            {
                return true;
            }
            if (cargoBounds.HasValue)
            {
                Bounds cargo = cargoBounds.Value;
                // This stage is 2.5D: compare X/Y while ignoring model depth offsets.
                if (CleanupRoundRules.TouchesFish(cargo.center.x, cargo.center.y, cargo.extents.x,
                    cargo.extents.y, fishPos.x, fishPos.y, fishHalfWidth, fishHalfHeight))
                    return true;
            }
        }
        return false;
    }

    private void TriggerEcoDisturbance()
    {
        DropHeldDebris();
        _clawState = ClawState.Reeling;
        _phaseTimeRemaining = Mathf.Max(0f, _phaseTimeRemaining - 3f);

        if (customBumpSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(customBumpSFX);
        else
            AudioManager.Instance?.PlayBumpSFX();

    }

    private void DropHeldDebris()
    {
        if (_grabbedItem == null) return;
        var item = _grabbedItem;
        _grabbedItem = null;
        item.carefulRecovery = false;
        if (item.worldObject == null) return;
        item.worldObject.transform.SetParent(_stageRoot.transform, true);
        item.isFalling = true;
        if (!_spawnedDebris.Contains(item)) _spawnedDebris.Add(item);
        if (_leftJaw != null) _leftJaw.localRotation = Quaternion.Euler(0f, 0f, -35f);
        if (_rightJaw != null) _rightJaw.localRotation = Quaternion.Euler(0f, 0f, 35f);
        StartCoroutine(FallBackToSeabed(item));
    }

    private IEnumerator FallBackToSeabed(DebrisInstance item)
    {
        float velocity = 0f;
        float landingY = item.restPosition.y;
        while (item.worldObject != null && item.worldObject.transform.localPosition.y > landingY)
        {
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
            {
                yield return null;
                continue;
            }
            velocity += 9.81f * Time.deltaTime;
            Vector3 position = item.worldObject.transform.localPosition;
            position.y = Mathf.Max(landingY, position.y - velocity * Time.deltaTime);
            item.worldObject.transform.localPosition = position;
            yield return null;
        }
        if (item.worldObject != null)
        {
            Vector3 position = item.worldObject.transform.localPosition;
            position.y = landingY;
            item.worldObject.transform.localPosition = position;
            item.restPosition = position;
        }
        item.isFalling = false;
    }

    // -----------------------------------------------------------------------
    // Phase 2: Recycler Sorting Game Loop
    // -----------------------------------------------------------------------

    private IEnumerator SortingPhaseRoutine()
    {
        AudioManager.Instance?.StopClawSFX();
        if (customExtractionContainer != null) customExtractionContainer.SetActive(false);
        if (_dropButton != null) _dropButton.gameObject.SetActive(false);
        if (_joystickBackground != null) _joystickBackground.gameObject.SetActive(false);
        if (_roundObjectiveText != null) _roundObjectiveText.gameObject.SetActive(false);
        if (_collectedDebris.Count == 0)
        {
            ShowResultsPhase();
            yield break;
        }

        _phaseTimeRemaining = CleanupRoundRules.SortingSeconds(_collectedDebris.Count);
        _sortingActive = true;
        BuildSortingUI();
        while (_phaseTimeRemaining > 0f && _correctSortCount < _collectedDebris.Count)
        {
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
            {
                yield return null;
                continue;
            }
            _phaseTimeRemaining = Mathf.Max(0f, _phaseTimeRemaining - Time.deltaTime);
            int seconds = Mathf.CeilToInt(_phaseTimeRemaining);
            SetText(customTimerText, customTimerTextLegacy, $"TIME: {seconds / 60}:{seconds % 60:00}");
            yield return null;
        }
        // Allow the final item and its bin to finish their feedback animation.
        if (_correctSortCount == _collectedDebris.Count)
        {
            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                if (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused) elapsed += Time.deltaTime;
                yield return null;
            }
        }
        _sortingActive = false;
        ShowResultsPhase();
    }

    private void BuildSortingUI()
    {
        // Replace the old sequential tap controls while retaining the surrounding HUD.
        if (customSortingContainer != null) customSortingContainer.SetActive(false);
        if (customBinPlastics != null) customBinPlastics.gameObject.SetActive(false);
        if (customBinMetals != null) customBinMetals.gameObject.SetActive(false);
        if (customBinHazardous != null) customBinHazardous.gameObject.SetActive(false);
        if (customSortingItemText != null) customSortingItemText.gameObject.SetActive(false);
        if (customSortingItemTextLegacy != null) customSortingItemTextLegacy.gameObject.SetActive(false);

        _sortingPanel = CreateUIPanel(_dedicatedCanvasGO.transform, "SortingPanel_Auto", new Vector2(0.04f, 0.1f),
            new Vector2(0.96f, 0.84f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.07f, 0.14f, 0.98f));
        var title = CreateUIText(_sortingPanel.transform, "Title", "SORT THE RECOVERED DEBRIS", 30,
            new Vector2(0.5f, 0.95f), TextAlignmentOptions.Center, new Color(0f, 0.93f, 0.85f), new Vector2(1200f, 45f));
        title.raycastTarget = false;
        _sortingProgressText = CreateUIText(_sortingPanel.transform, "Progress", "", 23,
            new Vector2(0.5f, 0.89f), TextAlignmentOptions.Center, Color.white, new Vector2(1200f, 42f));
        _sortingProgressText.raycastTarget = false;
        _sortingFeedbackText = CreateUIText(_sortingPanel.transform, "Feedback", "Drag collected debris from the tray into a bin.", 23,
            new Vector2(0.5f, 0.055f), TextAlignmentOptions.Center, new Color(0.85f, 0.95f, 1f), new Vector2(1400f, 48f));
        _sortingFeedbackText.raycastTarget = false;
        _sortingFeedbackText.enableAutoSizing = true;
        _sortingFeedbackText.fontSizeMin = 16;
        _sortingFeedbackText.fontSizeMax = 23;

        var body = CreateUIPanel(_sortingPanel.transform, "SortingBody", new Vector2(0.025f, 0.13f),
            new Vector2(0.975f, 0.82f), Vector2.zero, Vector2.zero, Color.clear);
        body.GetComponent<Image>().raycastTarget = false;
        // The working area follows the sketch: 70% bins, 30% collected tray.
        var binArea = CreateUIPanel(body.transform, "Bins", Vector2.zero, new Vector2(0.7f, 1f),
            Vector2.zero, Vector2.zero, new Color(0.025f, 0.09f, 0.15f));
        binArea.GetComponent<Image>().raycastTarget = false;
        BuildSortingBin(binArea.transform, DebrisCategory.Plastic, "PLASTICS", prefabPlasticBin, 0.02f);
        BuildSortingBin(binArea.transform, DebrisCategory.Metal, "METALS", prefabMetalBin, 0.35f);
        BuildSortingBin(binArea.transform, DebrisCategory.Hazardous, "HAZARDS", prefabHazardBin, 0.68f);
        var divider = CreateUIPanel(body.transform, "Divider", new Vector2(0.7f, 0f), new Vector2(0.7f, 1f),
            Vector2.zero, new Vector2(2f, 0f), new Color(0.15f, 0.6f, 0.63f));
        divider.GetComponent<Image>().raycastTarget = false;
        var tray = CreateUIPanel(body.transform, "CollectedTray", new Vector2(0.7f, 0f), Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.035f, 0.12f, 0.18f));
        var trayTitle = CreateUIText(tray.transform, "Title", "COLLECTED", 25,
            new Vector2(0.5f, 0.94f), TextAlignmentOptions.Center, new Color(0.3f, 1f, 0.93f));
        trayTitle.rectTransform.anchorMin = new Vector2(0.03f, 0.89f);
        trayTitle.rectTransform.anchorMax = new Vector2(0.97f, 0.99f);
        trayTitle.rectTransform.offsetMin = trayTitle.rectTransform.offsetMax = Vector2.zero;
        trayTitle.raycastTarget = false;

        var scroll = tray.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        var viewport = CreateUIPanel(tray.transform, "Viewport", new Vector2(0.025f, 0.025f), new Vector2(0.91f, 0.87f),
            Vector2.zero, Vector2.zero, new Color(0.025f, 0.085f, 0.13f));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = (RectTransform)viewport.transform;
        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(viewport.transform, false);
        var content = (RectTransform)contentGO.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        scroll.content = content;
        var grid = contentGO.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.spacing = new Vector2(10f, 10f);
        grid.childAlignment = TextAnchor.UpperLeft;
        contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollTrack = CreateUIPanel(tray.transform, "ScrollTrack", new Vector2(0.925f, 0.025f), new Vector2(0.99f, 0.87f),
            Vector2.zero, Vector2.zero, new Color(0.03f, 0.18f, 0.23f));
        var handle = CreateUIPanel(scrollTrack.transform, "Handle", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.15f, 0.62f, 0.65f));
        var scrollbar = scrollTrack.AddComponent<Scrollbar>();
        scrollbar.handleRect = (RectTransform)handle.transform;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        tray.AddComponent<CleanupSortingTrayUI>().Initialize(this, scroll, grid);

        var dragLayerGO = new GameObject("DragLayer", typeof(RectTransform));
        dragLayerGO.transform.SetParent(_sortingPanel.transform, false);
        var dragLayer = (RectTransform)dragLayerGO.transform;
        dragLayer.anchorMin = Vector2.zero;
        dragLayer.anchorMax = Vector2.one;
        dragLayer.sizeDelta = Vector2.zero;

        for (int i = 0; i < _collectedDebris.Count; i++)
        {
            var slot = CreateUIPanel(content, "TraySlot_" + i, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.035f, 0.1f, 0.16f));
            slot.GetComponent<Image>().raycastTarget = false;
            var card = CreateUIPanel(slot.transform, "Debris_" + i, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.07f, 0.17f, 0.23f));
            var item = _collectedDebris[i];
            AddModelIcon(card.transform, item.worldObject, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.98f));
            var label = CreateUIText(card.transform, "Name", item.displayName, 20, new Vector2(0.5f, 0.12f),
                TextAlignmentOptions.Center, Color.white);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.03f, 0.01f);
            labelRect.anchorMax = new Vector2(0.97f, 0.24f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = 20;
            label.raycastTarget = false;
            card.AddComponent<CleanupSortingItemUI>().Initialize(this, i, dragLayer);
        }
        // Keep held cards above all slots, with raycasts passing through to the bins.
        dragLayer.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        UpdateSortingProgress();
    }

    private void BuildSortingBin(Transform parent, DebrisCategory category, string label, GameObject model, float left)
    {
        var bin = CreateUIPanel(parent, "SortingBin_" + category,
            new Vector2(left, 0.06f), new Vector2(left + 0.3f, 0.94f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.22f, 0.28f));
        AddModelIcon(bin.transform, model, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.98f));
        var text = CreateUIText(bin.transform, "Category", label, 26, new Vector2(0.5f, 0.11f),
            TextAlignmentOptions.Center, new Color(0.3f, 1f, 0.93f));
        text.rectTransform.anchorMin = new Vector2(0.03f, 0.04f);
        text.rectTransform.anchorMax = new Vector2(0.97f, 0.18f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = 26;
        text.raycastTarget = false;
        bin.AddComponent<CleanupSortingBinUI>().Initialize(this, category);
    }

    private static void AddModelIcon(Transform parent, GameObject model, Vector2 min, Vector2 max)
    {
        if (model == null) return;
        var area = new GameObject("ModelArea", typeof(RectTransform));
        area.transform.SetParent(parent, false);
        var areaRect = (RectTransform)area.transform;
        areaRect.anchorMin = min;
        areaRect.anchorMax = max;
        areaRect.sizeDelta = Vector2.zero;
        var icon = new GameObject("Model", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        icon.transform.SetParent(area.transform, false);
        icon.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        icon.GetComponent<AspectRatioFitter>().aspectRatio = 1f;
        icon.AddComponent<CleanupModelIconUI>().SetModel(model);
    }

    private void UpdateSortingProgress()
    {
        if (_sortingProgressText != null)
            _sortingProgressText.text = $"Sorted: {_correctSortCount}/{_collectedDebris.Count}    Streak: {_sortingScore.Streak}    Bonus: +{_sortingScore.Bonus} RDP";
        SetText(customScoreText, customScoreTextLegacy, $"SORTED: {_correctSortCount}/{_collectedDebris.Count}");
    }

    public bool TrySortItem(int index, DebrisCategory category)
    {
        if (!CanSort || index < 0 || index >= _collectedDebris.Count || _sortingScore.IsSorted(index)) return false;
        var item = _collectedDebris[index];
        int previousBonus = _sortingScore.Bonus;
        bool correct = _sortingScore.Submit(index, item.category, category);
        _correctSortCount = _sortingScore.SortedCount;
        if (correct)
        {
            _sortingFeedbackText.color = new Color(0.3f, 1f, 0.75f);
            _sortingFeedbackText.text = _sortingScore.Bonus > previousBonus
                ? "Three in a row! +5 RDP" : $"{item.displayName} sorted. +15 RDP";
        }
        else
        {
            string bin = item.category == DebrisCategory.Plastic ? "Plastics" : item.category == DebrisCategory.Metal ? "Metals" : "Hazards";
            _sortingFeedbackText.color = new Color(1f, 0.75f, 0.4f);
            _sortingFeedbackText.text = $"{item.displayName} belongs in {bin}. Try that item again.";
        }
        UpdateSortingProgress();
        return correct;
    }

    // -----------------------------------------------------------------------
    // Phase 3: Results & Ecosystem Restoration
    // -----------------------------------------------------------------------

    private void ShowResultsPhase()
    {
        if (_resultsShown) return;
        _resultsShown = true;
        _sortingActive = false;
        if (_roundObjectiveText != null) _roundObjectiveText.gameObject.SetActive(false);
        if (_sortingPanel != null) _sortingPanel.SetActive(false);
        if (customSortingContainer != null) customSortingContainer.SetActive(false);
        if (customBinPlastics != null) customBinPlastics.gameObject.SetActive(false);
        if (customBinMetals != null) customBinMetals.gameObject.SetActive(false);
        if (customBinHazardous != null) customBinHazardous.gameObject.SetActive(false);
        if (customSortingItemText != null) customSortingItemText.gameObject.SetActive(false);
        if (customSortingItemTextLegacy != null) customSortingItemTextLegacy.gameObject.SetActive(false);

        if (customStatusText != null) customStatusText.gameObject.SetActive(false);
        if (customStatusTextLegacy != null) customStatusTextLegacy.gameObject.SetActive(false);

        int totalCleaned = _collectedDebris.Count;
        int accuracy = totalCleaned > 0 ? Mathf.RoundToInt((float)_sortingScore.FirstTryCount / totalCleaned * 100f) : 0;
        _roundSucceeded = totalCleaned > 0 && _correctSortCount == totalCleaned;
        int objectiveBonus = _objectiveTarget > 0 && _objectiveProgress >= _objectiveTarget ? 15 : 0;
        int cleanSweepBonus = _sortingScore.IsCleanSweep(totalCleaned) ? 10 : 0;
        int bonus = objectiveBonus + cleanSweepBonus + _carefulRecoveries * 3 + _sortingScore.Bonus;
        _pendingRdpReward = _roundSucceeded ? 30 + totalCleaned * 10 + _correctSortCount * 15 + bonus : 0;
        string outcome = _roundSucceeded ? "Cleanup complete. The area is ready for marine life." :
            totalCleaned == 0 ? "No debris recovered. Return to this cluster to try again." :
            "Some debris still needs sorting. Return to this cluster to try again.";

        string summary =
            $"Debris Retrieved: {totalCleaned}/{_initialDebrisCount}\n" +
            $"Sorted: {_correctSortCount}/{totalCleaned} | First-try Accuracy: {accuracy}%\n" +
            $"Recovery Objective: {_objectiveProgress}/{_objectiveTarget}\n" +
            $"Bonuses: +{(_roundSucceeded ? bonus : 0)} RDP" + (cleanSweepBonus > 0 ? " (includes Clean Sweep)" : "") + "\n" +
            $"RDP Earned: +{_pendingRdpReward}" + (_isPlaytest ? " (preview only)" : "") + $"\n\n{outcome}";

        // 1. If user provided results elements in custom UI
        if (customReturnButton != null && (customResultsSummaryText != null || customResultsSummaryTextLegacy != null))
        {
            if (customResultsContainer != null) customResultsContainer.SetActive(true);
            SetText(customResultsSummaryText, customResultsSummaryTextLegacy, summary);
            if (customReturnButton != null)
            {
                customReturnButton.gameObject.SetActive(true);
                customReturnButton.onClick.RemoveAllListeners();
                customReturnButton.onClick.AddListener(OnFinishMinigame);
            }
            return;
        }

        // 2. Safe Fallback: Auto-generate results panel strictly on dedicated canvas
        Transform parentTransform = _dedicatedCanvasGO != null ? _dedicatedCanvasGO.transform : transform;
        _resultsPanel = CreateUIPanel(parentTransform, "ResultsPanel_Auto", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 620f), new Color(0.02f, 0.07f, 0.14f, 0.98f));

        CreateUIText(_resultsPanel.transform, "Title", _roundSucceeded ? "CLEANUP COMPLETE" : "CLEANUP REPORT", 32, new Vector2(0.5f, 0.88f), TextAlignmentOptions.Center, new Color(0f, 1f, 0.75f), new Vector2(900f, 45f));
        CreateUIText(_resultsPanel.transform, "Summary", summary, 22, new Vector2(0.5f, 0.52f), TextAlignmentOptions.Center, Color.white, new Vector2(860f, 300f));

        CreateUIButton(_resultsPanel.transform, "BtnReturn", "RETURN TO EXPLORATION", new Vector2(0.5f, 0.15f), new Vector2(380f, 75f), OnFinishMinigame, new Color(0f, 0.75f, 0.65f), Vector2.zero);
    }

    private void OnFinishMinigame()
    {
        if (!_isActive || !_resultsShown) return;
        _isActive = false;
        StopAllCoroutines();

        if (_roundSucceeded && !_isPlaytest)
        {
            GameManager.Instance?.AddRDP(_pendingRdpReward);
            if (_targetCluster != null) _targetCluster.CleanUp();
        }
        _pendingRdpReward = 0;
        _targetCluster = null;
        Action callback = _roundSucceeded ? _onSuccess : _onFail;
        CancelMinigame();
        callback?.Invoke();
    }

    public void CancelMinigame()
    {
        StopAllCoroutines();
        _isActive = false;
        _sortingActive = false;

        if (customUIRoot != null) 
        {
            customUIRoot.SetActive(false);
            if (_originalUIParent != null)
            {
                customUIRoot.transform.SetParent(_originalUIParent, true);
                _originalUIParent = null;
            }
        }
        if (customExtractionContainer != null) customExtractionContainer.SetActive(false);
        if (customSortingContainer != null) customSortingContainer.SetActive(false);
        if (customResultsContainer != null) customResultsContainer.SetActive(false);

        if (_sortingPanel != null) Destroy(_sortingPanel);
        if (_resultsPanel != null) Destroy(_resultsPanel);
        if (_stageRoot != null) Destroy(_stageRoot);
        if (_dedicatedCanvasGO != null) Destroy(_dedicatedCanvasGO);

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && (t.name.StartsWith("ResultsPanel_Auto") || t.name.StartsWith("SortingPanel_Auto")))
            {
                Destroy(t.gameObject);
            }
        }

        RestoreSceneCamera();
        UIManager.Instance?.SetExplorationHUDVisible(true);

        AudioManager.Instance?.StopClawSFX();
        AudioManager.Instance?.RestoreZoneBGM();
    }

    private void OnDisable()
    {
        if (_isActive) CancelMinigame();
    }

    private void OnDestroy()
    {
        CancelMinigame();
    }

    // -----------------------------------------------------------------------
    // UI Helpers & Deep Child Finders
    // -----------------------------------------------------------------------

    private static void SetText(TMP_Text tmp, Text legacy, string text, Color? col = null)
    {
        if (tmp != null)
        {
            tmp.gameObject.SetActive(true);
            tmp.text = text;
            if (col.HasValue) tmp.color = col.Value;
        }
        if (legacy != null)
        {
            legacy.gameObject.SetActive(true);
            legacy.text = text;
            if (col.HasValue) legacy.color = col.Value;
        }
    }

    private static T FindDeepChild<T>(GameObject root, params string[] possibleNames) where T : Component
    {
        if (root == null) return null;
        foreach (var name in possibleNames)
        {
            var comp = FindComponentRecursive<T>(root.transform, name);
            if (comp != null) return comp;
        }
        return null;
    }

    private static T FindComponentRecursive<T>(Transform parent, string targetName) where T : Component
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                var comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }
            var deeper = FindComponentRecursive<T>(child, targetName);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private static Material CreateMaterial(Color col)
    {
        return MaterialUtils.CreateColoredMaterial(col);
    }

    private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        go.GetComponent<Image>().color = col;
        return go;
    }

    private static TMP_Text CreateUIText(Transform parent, string name, string text, float fontSize, Vector2 anchorPos, TextAlignmentOptions align, Color col, Vector2? size = null, Vector2? pos = null)
    {
        var font = UIThemeManager.AlohaFont;

        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorPos;
        r.anchoredPosition = pos ?? Vector2.zero;
        r.sizeDelta = size ?? new Vector2(720f, 50f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = col;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchorPos, Vector2 size, Action onClick, Color? btnColor = null, Vector2? pos = null)
    {
        var font = UIThemeManager.AlohaFont;

        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorPos;
        r.anchoredPosition = pos ?? Vector2.zero;
        r.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = btnColor ?? new Color(0.15f, 0.22f, 0.32f);

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8f, 6f);
        tr.offsetMax = new Vector2(-8f, -6f);
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16;
        tmp.fontSizeMax = 28;
        tmp.raycastTarget = false;

        return btn;
    }

    private static GameObject CreateProceduralFish()
    {
        var root = new GameObject("ProceduralFish");

        // Body: horizontal capsule pointing forward along Z
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.35f, 0.60f, 0.25f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.GetComponent<Renderer>().material = CreateMaterial(new Color(1f, 0.55f, 0.1f));

        // Tail fin: back at -Z
        var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "TailFin";
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0f, -0.45f);
        tail.transform.localScale = new Vector3(0.06f, 0.35f, 0.22f);
        tail.GetComponent<Renderer>().material = CreateMaterial(new Color(1f, 0.35f, 0.05f));

        // Eye marker: front at +Z
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(root.transform, false);
        eye.transform.localPosition = new Vector3(0f, 0.06f, 0.30f);
        eye.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        eye.GetComponent<Renderer>().material = CreateMaterial(new Color(0.1f, 0.1f, 0.1f));

        // Set colliders as triggers
        foreach (var col in root.GetComponentsInChildren<Collider>())
            col.isTrigger = true;

        return root;
    }
}
