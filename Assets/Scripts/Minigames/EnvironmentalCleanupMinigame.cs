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
        ElectronicWaste
    }

    [System.Serializable]
    public class DebrisInstance
    {
        public DebrisItemType itemType;
        public DebrisCategory category;
        public string displayName;
        public GameObject worldObject;
        public Color itemColor;
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

    [Header("=== 3D MODEL PREFABS (Optional) ===")]
    public GameObject customClawPrefab;
    public GameObject customTrolleyPrefab;

    [Header("Plastics 3D Prefabs")]
    public GameObject prefabWaterBottle;
    public GameObject prefabPlasticBag;
    public GameObject prefabSixPackRings;

    [Header("Metals 3D Prefabs")]
    public GameObject prefabSodaCan;
    public GameObject prefabMetalPipe;
    public GameObject prefabMetalDrum;

    [Header("Hazardous Waste 3D Prefabs")]
    public GameObject prefabBatterySmall;
    public GameObject prefabBatteryLarge;
    public GameObject prefabElectronicWaste;

    [Header("Obstacles 3D Prefabs")]
    public GameObject prefabCoralObstacle;
    public GameObject prefabFishObstacle;

    // -----------------------------------------------------------------------
    // State & Callbacks
    // -----------------------------------------------------------------------

    private DebrisCluster _targetCluster;
    private Action        _onSuccess;
    private Action        _onFail;
    private int           _zoneIndex;
    private bool          _isActive;

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
    private Button     _btnPlastics;
    private Button     _btnMetals;
    private Button     _btnHazardous;
    private int        _sortingIndex = 0;
    private int        _correctSortCount = 0;

    // Results UI
    private GameObject _resultsPanel;

    private float _phaseTimeRemaining;
    private float _totalPhaseTime = 25f;

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
    }

    public void Show()
    {
        _isActive = true;
        gameObject.SetActive(true);
        StopAllCoroutines();

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
        _clawState   = ClawState.Idle;
        _grabbedItem = null;
        _trolleyX    = 0f;
        _clawY       = _clawRestY;
        _joystickHorizontalInput = 0f;

        // Utilities Tier 2 Perk: +25% claw drop and retrieval speed
        int utilTier = GameManager.Instance != null ? GameManager.Instance.UtilitiesTier : 1;
        float speedBonus = (utilTier >= 2) ? 1.25f : 1.0f;
        _clawDropSpeed = 7.0f * speedBonus;
        _clawReelSpeed = 8.0f * speedBonus;

        // Cleanly hide main exploration HUD
        UIManager.Instance?.SetExplorationHUDVisible(false);

        Build3DStage();
        SwitchToMinigameCamera();
        BuildUI();
        SpawnDebrisAndObstacles();

        gameObject.SetActive(true);
        StartCoroutine(ExtractionPhaseRoutine());
    }

    private void SwitchToMinigameCamera()
    {
        _disabledSceneCams.Clear();
        var allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in allCams)
        {
            if (c != _minigameCam && c.enabled)
            {
                c.enabled = false;
                _disabledSceneCams.Add(c);
            }
        }
        if (_minigameCam != null) _minigameCam.enabled = true;
    }

    private void RestoreSceneCamera()
    {
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
            clawGO.transform.localScale    = new Vector3(0.9f, 0.7f, 0.9f);
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
        var itemsToSpawn = new List<DebrisItemType>
        {
            DebrisItemType.WaterBottle,
            DebrisItemType.PlasticBag,
            DebrisItemType.SixPackRings,
            DebrisItemType.SodaCan,
            DebrisItemType.MetalPipe,
            DebrisItemType.MetalDrum,
            DebrisItemType.BatterySmall,
            DebrisItemType.BatteryLarge,
            DebrisItemType.ElectronicWaste
        };

        float minX = -5.8f;
        float maxX = 5.8f;
        float step = (maxX - minX) / (itemsToSpawn.Count - 1);

        for (int i = 0; i < itemsToSpawn.Count; i++)
        {
            float posX = minX + i * step + UnityEngine.Random.Range(-0.15f, 0.15f);
            float posY = -2.05f;
            SpawnSingleDebris(itemsToSpawn[i], new Vector3(posX, posY, 0f));
        }

        SpawnObstacles();
    }

    private void SpawnSingleDebris(DebrisItemType type, Vector3 localPos)
    {
        var deb = new DebrisInstance { itemType = type };
        GameObject go = null;

        switch (type)
        {
            case DebrisItemType.WaterBottle:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Plastic Water Bottle";
                deb.itemColor = new Color(0.2f, 0.75f, 1.0f, 0.9f);
                go = prefabWaterBottle != null ? Instantiate(prefabWaterBottle, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabWaterBottle == null) go.transform.localScale = new Vector3(0.40f, 0.65f, 0.40f);
                break;

            case DebrisItemType.PlasticBag:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Plastic Shopping Bag";
                deb.itemColor = new Color(0.92f, 0.92f, 0.98f, 0.85f);
                go = prefabPlasticBag != null ? Instantiate(prefabPlasticBag, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (prefabPlasticBag == null) go.transform.localScale = new Vector3(0.65f, 0.55f, 0.20f);
                break;

            case DebrisItemType.SixPackRings:
                deb.category = DebrisCategory.Plastic;
                deb.displayName = "Six-Pack Plastic Rings";
                deb.itemColor = new Color(0.65f, 0.88f, 1.0f, 0.85f);
                go = prefabSixPackRings != null ? Instantiate(prefabSixPackRings, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabSixPackRings == null) go.transform.localScale = new Vector3(0.75f, 0.10f, 0.55f);
                break;

            case DebrisItemType.SodaCan:
                deb.category = DebrisCategory.Metal;
                deb.displayName = "Aluminum Soda Can";
                deb.itemColor = new Color(0.90f, 0.18f, 0.18f);
                go = prefabSodaCan != null ? Instantiate(prefabSodaCan, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabSodaCan == null) go.transform.localScale = new Vector3(0.45f, 0.50f, 0.45f);
                break;

            case DebrisItemType.MetalPipe:
                deb.category = DebrisCategory.Metal;
                deb.displayName = "Rusted Metal Pipe";
                deb.itemColor = new Color(0.60f, 0.48f, 0.36f);
                go = prefabMetalPipe != null ? Instantiate(prefabMetalPipe, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabMetalPipe == null)
                {
                    go.transform.localScale = new Vector3(0.45f, 0.95f, 0.45f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, 75f);
                }
                break;

            case DebrisItemType.MetalDrum:
                deb.category = DebrisCategory.Metal;
                deb.displayName = "Rusted Oil Drum";
                deb.itemColor = new Color(0.40f, 0.32f, 0.24f);
                go = prefabMetalDrum != null ? Instantiate(prefabMetalDrum, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabMetalDrum == null) go.transform.localScale = new Vector3(0.75f, 0.85f, 0.75f);
                break;

            case DebrisItemType.BatterySmall:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Discarded AA Battery";
                deb.itemColor = new Color(1.0f, 0.85f, 0.1f);
                go = prefabBatterySmall != null ? Instantiate(prefabBatterySmall, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (prefabBatterySmall == null) go.transform.localScale = new Vector3(0.30f, 0.45f, 0.30f);
                break;

            case DebrisItemType.BatteryLarge:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Submarine Battery Cell";
                deb.itemColor = new Color(0.2f, 0.85f, 0.35f);
                go = prefabBatteryLarge != null ? Instantiate(prefabBatteryLarge, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (prefabBatteryLarge == null) go.transform.localScale = new Vector3(0.75f, 0.65f, 0.55f);
                break;

            case DebrisItemType.ElectronicWaste:
                deb.category = DebrisCategory.Hazardous;
                deb.displayName = "Electronic Circuit Board";
                deb.itemColor = new Color(0.12f, 0.65f, 0.25f);
                go = prefabElectronicWaste != null ? Instantiate(prefabElectronicWaste, _stageRoot.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (prefabElectronicWaste == null) go.transform.localScale = new Vector3(0.70f, 0.12f, 0.60f);
                break;
        }

        if (go != null)
        {
            go.name = deb.displayName;
            go.transform.SetParent(_stageRoot.transform, false);
            go.transform.localPosition = localPos;

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                rend.material = CreateMaterial(deb.itemColor);
            }
            deb.worldObject = go;
            _spawnedDebris.Add(deb);
        }
    }

    private void SpawnObstacles()
    {
        for (int i = 0; i < 2; i++)
        {
            float cx = i == 0 ? -2.4f : 2.4f;
            GameObject coralGO = prefabCoralObstacle != null 
                ? Instantiate(prefabCoralObstacle, _stageRoot.transform) 
                : CreateProceduralCoral();

            coralGO.name = "Obstacle_Coral";
            coralGO.transform.localPosition = new Vector3(cx, -2.1f, 0f);
            coralGO.transform.localScale    = Vector3.one * 1.6f;

            // Make sure colliders are triggers so they don't push physics bodies
            foreach (var col in coralGO.GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            _spawnedObstacles.Add(coralGO);
        }

        for (int i = 0; i < 2; i++)
        {
            GameObject fishGO = prefabFishObstacle != null 
                ? Instantiate(prefabFishObstacle, _stageRoot.transform) 
                : CreateProceduralFish();

            fishGO.name = $"Obstacle_Fish_{i}";
            float fy = i == 0 ? 0.6f : -0.7f;
            float fx = i == 0 ? -6.5f : 6.5f;
            fishGO.transform.localPosition = new Vector3(fx, fy, 0f);
            fishGO.transform.localScale    = Vector3.one * 1.4f;

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

            // Ensure timer text is correctly anchored & pivoted so it is never cut off on wide aspect ratios
            if (customTimerText != null)
            {
                var tRect = customTimerText.GetComponent<RectTransform>();
                if (tRect != null && tRect.anchorMin.x >= 0.8f)
                {
                    tRect.pivot = new Vector2(1f, tRect.pivot.y);
                    if (tRect.anchoredPosition.x > -50f)
                        tRect.anchoredPosition = new Vector2(-50f, tRect.anchoredPosition.y);
                    customTimerText.alignment = TextAlignmentOptions.Right;
                }
            }

            if (customScoreText == null) customScoreText = FindDeepChild<TMP_Text>(customUIRoot, "Score", "ScoreText", "Collect");
            if (customScoreTextLegacy == null && customScoreText == null) customScoreTextLegacy = FindDeepChild<Text>(customUIRoot, "Score", "ScoreText", "Collect");

            // Fallbacks in case HUD does not contain Timer/Score
            if (customTimerText == null && customTimerTextLegacy == null)
            {
                customTimerText = CreateUIText(_dedicatedCanvasGO.transform, "TimerText_Auto", "Time: 0:25", 28, new Vector2(0.85f, 0.94f), TextAlignmentOptions.Right, Color.white);
            }
            if (customScoreText == null && customScoreTextLegacy == null)
            {
                customScoreText = CreateUIText(_dedicatedCanvasGO.transform, "ScoreText_Auto", "COLLECT: 0", 28, new Vector2(0.55f, 0.94f), TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.2f));
            }

            if (customStatusText == null) customStatusText = FindDeepChild<TMP_Text>(customUIRoot, "Status", "StatusText", "Banner");
            if (customStatusTextLegacy == null && customStatusText == null) customStatusTextLegacy = FindDeepChild<Text>(customUIRoot, "Status", "StatusText", "Banner");
            if (customStatusText == null && customStatusTextLegacy == null)
            {
                var autoBannerGO = CreateUIPanel(_dedicatedCanvasGO.transform, "StatusBanner_Auto", new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(720f, 45f), new Color(0f, 0f, 0f, 0.65f));
                customStatusText = CreateUIText(autoBannerGO.transform, "StatusText", "Move claw with Joystick [◄ ►] | Tap button to drop claw", 19, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, Color.white);
            }

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
            return;
        }

        // 4. Fallback: Procedural UI
        var topBar = CreateUIPanel(_dedicatedCanvasGO.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(850f, 80f), new Color(0.02f, 0.08f, 0.15f, 0.92f));
        customScoreText = CreateUIText(topBar.transform, "CollectText", "COLLECT: 0", 26, new Vector2(0.20f, 0.5f), TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f));
        CreateUIText(topBar.transform, "PhaseTitle", "REMOVAL PHASE", 36, new Vector2(0.50f, 0.5f), TextAlignmentOptions.Center, new Color(0f, 0.93f, 0.85f));
        customTimerText = CreateUIText(topBar.transform, "TimerText", "Time: 0:25", 26, new Vector2(0.80f, 0.5f), TextAlignmentOptions.Center, Color.white);

        var bannerGO = CreateUIPanel(_dedicatedCanvasGO.transform, "StatusBanner", new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(720f, 45f), new Color(0f, 0f, 0f, 0.65f));
        customStatusText = CreateUIText(bannerGO.transform, "StatusText", "Move claw with Joystick [◄ ►] | Tap button to drop claw", 19, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, Color.white);

        BuildVirtualJoystick();

        _dropButton = CreateUIButton(_dedicatedCanvasGO.transform, "BtnDropClaw", "⬇ TAP TO\nDROP CLAW", new Vector2(0.85f, 0.18f), new Vector2(230f, 120f), OnDropClawPressed, new Color(0f, 0.72f, 0.65f, 0.95f));
    }

    private void BuildVirtualJoystick()
    {
        var joyRoot = CreateUIPanel(_dedicatedCanvasGO.transform, "MinigameJoystick", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(180f, 180f), new Vector2(200f, 200f), new Color(0.05f, 0.15f, 0.25f, 0.65f));
        _joystickBackground = joyRoot.GetComponent<RectTransform>();

        var handleGO = CreateUIPanel(joyRoot.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(85f, 85f), new Color(0f, 0.93f, 0.85f, 0.9f));
        _joystickHandle = handleGO.GetComponent<RectTransform>();

        var lbl = CreateUIText(joyRoot.transform, "JoyLabel", "MOVE CLAW\n◄  ►", 15, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, Color.white);
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

        float maxRadius = 70f;
        localPoint = Vector2.ClampMagnitude(localPoint, maxRadius);
        _joystickHandle.anchoredPosition = new Vector2(localPoint.x, 0f);

        _joystickHorizontalInput = Mathf.Clamp(localPoint.x / maxRadius, -1f, 1f);
    }

    private void OnJoystickPointerUp()
    {
        _isDraggingJoystick = false;
        _joystickHorizontalInput = 0f;
        if (_joystickHandle != null) _joystickHandle.anchoredPosition = Vector2.zero;
    }

    // -----------------------------------------------------------------------
    // Phase 1: Extraction Game Loop
    // -----------------------------------------------------------------------

    private IEnumerator ExtractionPhaseRoutine()
    {
        _phaseTimeRemaining = _totalPhaseTime;

        while (_phaseTimeRemaining > 0f && _spawnedDebris.Count > 0)
        {
            _phaseTimeRemaining -= Time.deltaTime;
            int seconds = Mathf.CeilToInt(_phaseTimeRemaining);
            SetText(customTimerText, customTimerTextLegacy, $"Time: 0:{seconds:00}");
            SetText(customScoreText, customScoreTextLegacy, $"COLLECT: {_collectedDebris.Count}");

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
                ShowStatusBanner($"GRABBED: {hitItem.displayName}!", new Color(0f, 1f, 0.8f));
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
            _clawY += _clawReelSpeed * Time.deltaTime;
            if (_clawY >= _clawRestY)
            {
                _clawY = _clawRestY;
                _clawState = ClawState.Idle;

                if (_grabbedItem != null)
                {
                    _collectedDebris.Add(_grabbedItem);
                    Destroy(_grabbedItem.worldObject);
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
    }

    private void OnDropClawPressed()
    {
        if (_clawState == ClawState.Idle)
        {
            _clawState = ClawState.Dropping;
            if (_dropButton != null) _dropButton.interactable = false;
        }
    }

    private DebrisInstance CheckDebrisCollision()
    {
        Vector3 clawPos = _clawHead != null ? _clawHead.localPosition : Vector3.zero;
        for (int i = 0; i < _spawnedDebris.Count; i++)
        {
            var deb = _spawnedDebris[i];
            if (deb.worldObject == null) continue;
            float dist = Vector3.Distance(clawPos, deb.worldObject.transform.localPosition);
            if (dist < 1.15f)
            {
                return deb;
            }
        }
        return null;
    }

    private bool CheckObstacleCollision()
    {
        Vector3 clawPos = _clawHead != null ? _clawHead.localPosition : Vector3.zero;
        foreach (var obs in _spawnedObstacles)
        {
            if (obs == null) continue;
            float dist = Vector3.Distance(clawPos, obs.transform.localPosition);
            if (dist < 1.1f)
            {
                return true;
            }
        }
        return false;
    }

    private void TriggerEcoDisturbance()
    {
        _clawState = ClawState.Reeling;
        _phaseTimeRemaining = Mathf.Max(0f, _phaseTimeRemaining - 3f);
        ShowStatusBanner("⚠ ECO-DISTURBANCE! AVOID LIVE SPECIES (-3s)", new Color(1f, 0.2f, 0.2f));
    }

    private void ShowStatusBanner(string msg, Color col)
    {
        SetText(customStatusText, customStatusTextLegacy, msg, col);
    }

    // -----------------------------------------------------------------------
    // Phase 2: Recycler Sorting Game Loop
    // -----------------------------------------------------------------------

    private IEnumerator SortingPhaseRoutine()
    {
        // 1. Hide Extraction Controls
        if (customExtractionContainer != null) customExtractionContainer.SetActive(false);
        if (_dropButton != null) _dropButton.gameObject.SetActive(false);
        if (_joystickBackground != null) _joystickBackground.gameObject.SetActive(false);

        if (_collectedDebris.Count == 0)
        {
            _collectedDebris.Add(new DebrisInstance { displayName = "Plastic Water Bottle", category = DebrisCategory.Plastic });
        }

        BuildSortingUI();

        _sortingIndex = 0;
        _correctSortCount = 0;
        float sortTime = 20f;

        while (sortTime > 0f && _sortingIndex < _collectedDebris.Count)
        {
            sortTime -= Time.deltaTime;
            SetText(customTimerText, customTimerTextLegacy, $"Time: 0:{Mathf.CeilToInt(sortTime):00}");

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) OnBinSelected(DebrisCategory.Plastic);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) OnBinSelected(DebrisCategory.Metal);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) OnBinSelected(DebrisCategory.Hazardous);
            }

            yield return null;
        }

        ShowResultsPhase();
    }

    private void BuildSortingUI()
    {
        // 1. If user provided sorting elements inside their HUD
        if (customBinPlastics != null || customBinMetals != null || customBinHazardous != null || customSortingContainer != null)
        {
            if (customSortingContainer != null) customSortingContainer.SetActive(true);

            if (customBinPlastics != null)
            {
                customBinPlastics.gameObject.SetActive(true);
                customBinPlastics.onClick.RemoveAllListeners();
                customBinPlastics.onClick.AddListener(() => OnBinSelected(DebrisCategory.Plastic));
                _btnPlastics = customBinPlastics;
            }
            if (customBinMetals != null)
            {
                customBinMetals.gameObject.SetActive(true);
                customBinMetals.onClick.RemoveAllListeners();
                customBinMetals.onClick.AddListener(() => OnBinSelected(DebrisCategory.Metal));
                _btnMetals = customBinMetals;
            }
            if (customBinHazardous != null)
            {
                customBinHazardous.gameObject.SetActive(true);
                customBinHazardous.onClick.RemoveAllListeners();
                customBinHazardous.onClick.AddListener(() => OnBinSelected(DebrisCategory.Hazardous));
                _btnHazardous = customBinHazardous;
            }

            if (customSortingItemText != null) customSortingItemText.gameObject.SetActive(true);
            if (customSortingItemTextLegacy != null) customSortingItemTextLegacy.gameObject.SetActive(true);

            UpdateSortingItemDisplay();
            return;
        }

        // 2. Safe Fallback: Auto-generate the 3 sorting buttons right on the active panel
        Transform parentTransform = _dedicatedCanvasGO != null ? _dedicatedCanvasGO.transform : (customUIRoot != null ? customUIRoot.transform : transform);

        if (_sortingPanel == null)
        {
            _sortingPanel = CreateUIPanel(parentTransform, "SortingPanel_Auto", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 580f), new Color(0.03f, 0.09f, 0.16f, 0.96f));

            CreateUIText(_sortingPanel.transform, "Title", "SORTING PHASE", 32, new Vector2(0.5f, 0.92f), TextAlignmentOptions.Center, new Color(0f, 0.93f, 0.85f));
            CreateUIText(_sortingPanel.transform, "Subtitle", "Sort retrieved waste into the correct category (+RDP):", 18, new Vector2(0.5f, 0.83f), TextAlignmentOptions.Center, Color.white);

            _btnPlastics = CreateUIButton(_sortingPanel.transform, "BinPlastics", "♻ PLASTICS\n(Bottles/Bags/Rings)", new Vector2(0.18f, 0.35f), new Vector2(200f, 160f), () => OnBinSelected(DebrisCategory.Plastic), new Color(0f, 0.45f, 0.9f));
            _btnMetals = CreateUIButton(_sortingPanel.transform, "BinMetals", "⚙ METALS\n(Cans/Pipes/Drums)", new Vector2(0.42f, 0.35f), new Vector2(200f, 160f), () => OnBinSelected(DebrisCategory.Metal), new Color(0.9f, 0.65f, 0f));
            _btnHazardous = CreateUIButton(_sortingPanel.transform, "BinHazardous", "☣ HAZARDOUS\n(Batteries/E-Waste)", new Vector2(0.66f, 0.35f), new Vector2(200f, 160f), () => OnBinSelected(DebrisCategory.Hazardous), new Color(0.85f, 0.15f, 0.15f));

            customSortingItemText = CreateUIText(_sortingPanel.transform, "ItemName", "", 24, new Vector2(0.5f, 0.68f), TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f));
        }
        else
        {
            _sortingPanel.SetActive(true);
        }

        UpdateSortingItemDisplay();
    }

    private void UpdateSortingItemDisplay()
    {
        if (_sortingIndex < _collectedDebris.Count)
        {
            var item = _collectedDebris[_sortingIndex];
            string msg = $"ITEM {_sortingIndex + 1}/{_collectedDebris.Count}: <b>{item.displayName}</b> ➔ [Choose Bin]";
            if (customSortingItemText != null || customSortingItemTextLegacy != null)
            {
                SetText(customSortingItemText, customSortingItemTextLegacy, msg);
            }
        }
    }

    private void OnBinSelected(DebrisCategory selectedCategory)
    {
        if (_sortingIndex >= _collectedDebris.Count) return;

        var currentItem = _collectedDebris[_sortingIndex];
        bool isCorrect = currentItem.category == selectedCategory;

        if (isCorrect)
        {
            _correctSortCount++;
            ShowStatusBanner($"✓ CORRECT! +15 RDP ({selectedCategory.ToString().ToUpper()})", Color.green);
        }
        else
        {
            ShowStatusBanner($"✗ WRONG BIN! {currentItem.displayName} is {currentItem.category.ToString().ToUpper()}.", new Color(1f, 0.5f, 0f));
        }

        _sortingIndex++;
        if (_sortingIndex < _collectedDebris.Count)
        {
            UpdateSortingItemDisplay();
        }
        else
        {
            ShowResultsPhase();
        }
    }

    // -----------------------------------------------------------------------
    // Phase 3: Results & Ecosystem Restoration
    // -----------------------------------------------------------------------

    private void ShowResultsPhase()
    {
        if (_sortingPanel != null) _sortingPanel.SetActive(false);
        if (customSortingContainer != null) customSortingContainer.SetActive(false);
        if (customBinPlastics != null) customBinPlastics.gameObject.SetActive(false);
        if (customBinMetals != null) customBinMetals.gameObject.SetActive(false);
        if (customBinHazardous != null) customBinHazardous.gameObject.SetActive(false);
        if (customSortingItemText != null) customSortingItemText.gameObject.SetActive(false);
        if (customSortingItemTextLegacy != null) customSortingItemTextLegacy.gameObject.SetActive(false);

        int totalCleaned = _collectedDebris.Count;
        int accuracy = totalCleaned > 0 ? Mathf.RoundToInt(((float)_correctSortCount / totalCleaned) * 100f) : 100;
        int rdpReward = 30 + totalCleaned * 10 + _correctSortCount * 15;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddRDP(rdpReward);
        }

        string summary =
            $"Debris Retrieved: {totalCleaned}\n" +
            $"Sorting Accuracy: {accuracy}%\n" +
            $"RDP Earned: +{rdpReward}\n\n" +
            $"Ecosystem Boost: +20% spawn rate (45s)";

        // 1. If user provided results elements in custom UI
        if (customResultsContainer != null || customReturnButton != null || customResultsSummaryText != null || customResultsSummaryTextLegacy != null)
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
        _resultsPanel = CreateUIPanel(parentTransform, "ResultsPanel_Auto", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 480f), new Color(0.03f, 0.08f, 0.15f, 0.98f));

        CreateUIText(_resultsPanel.transform, "Title", "ECOSYSTEM RESTORED", 30, new Vector2(0.5f, 0.84f), TextAlignmentOptions.Center, new Color(0f, 1f, 0.75f));

        string richSummary =
            $"<b>Debris Retrieved:</b> {totalCleaned} items\n" +
            $"<b>Sorting Accuracy:</b> {accuracy}%\n" +
            $"<b>Research Points Earned:</b> <color=#ffdd00>+{rdpReward} RDP</color>\n\n" +
            $"<color=#00e0ff><b>✦ OBJECTIVES COMPLETE</b></color>\n" +
            $"Increased species spawn rate by 20% (45s)";

        CreateUIText(_resultsPanel.transform, "Summary", richSummary, 20, new Vector2(0.5f, 0.50f), TextAlignmentOptions.Center, Color.white, new Vector2(680f, 220f));

        CreateUIButton(_resultsPanel.transform, "BtnReturn", "RETURN TO EXPLORATION", new Vector2(0.5f, 0.15f), new Vector2(340f, 65f), OnFinishMinigame, new Color(0f, 0.75f, 0.65f));
    }

    private void OnFinishMinigame()
    {
        _isActive = false;
        StopAllCoroutines();

        if (_targetCluster != null)
        {
            _targetCluster.CleanUp();
            _targetCluster = null;
        }

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

        // Restore main exploration HUD
        UIManager.Instance?.SetExplorationHUDVisible(true);

        _onSuccess?.Invoke();
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
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");
        var mat = new Material(shader);
        mat.color = col;
        return mat;
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

    private static TMP_Text CreateUIText(Transform parent, string name, string text, float fontSize, Vector2 anchorPos, TextAlignmentOptions align, Color col, Vector2? size = null)
    {
        var font = UIThemeManager.AlohaFont;

        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorPos;
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size ?? new Vector2(720f, 150f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = Mathf.Max(36f, fontSize);
        tmp.alignment = align;
        tmp.color = col;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchorPos, Vector2 size, Action onClick, Color? btnColor = null)
    {
        var font = UIThemeManager.AlohaFont;

        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchorPos;
        r.anchoredPosition = Vector2.zero;
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
        tr.sizeDelta = Vector2.zero;
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 36;
        tmp.fontSizeMax = 24;
        tmp.raycastTarget = false;

        return btn;
    }

    private static GameObject CreateProceduralCoral()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
        go.GetComponent<Renderer>().material = CreateMaterial(new Color(0.95f, 0.35f, 0.5f));
        return go;
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
