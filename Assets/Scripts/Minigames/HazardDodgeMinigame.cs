using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Mini-game 4 — Hazard Dodge (2.5D Top-Perspective 3-Lane Emergency Navigation Runner).
///
/// Gameplay Flow:
///   Phase 1 (Alert & Warning):
///     - Screen flashes red, warning siren sounds, "⚠️ ENTERING HAZARD ZONE ⚠️" alert banner.
///     - Transitions into high-angle top-down 2.5D perspective over 3 underwater navigation lanes.
///   Phase 2 (3-Lane Emergency Run):
///     - Player controls the submarine across 3 lanes: Left (-X), Center (0), Right (+X).
///     - Controls: Touch swipe left/right, on-screen tap, or Keyboard (A/D, Arrow keys).
///     - Hazards (Thermal Vents, Whirlpools, Falling Rocks) stream downward along lanes.
///     - Collectibles: Floating High-Pressure Data Pods (+10 to +25 RDP) rewarding risky lane shifts.
///     - Fixed Hull Integrity: 3 hits (2 in Abyss/Hadal) with NO mid-run health restoration.
///     - Distance countdown: 50m down to 0m.
///   Phase 3 (Outcome):
///     - Win (0m left): Awards high-yield Extreme Depth Survey Data (+Bonus RDP) + collected Pods.
///     - Fail (Hull hits depleted): -50 RDP penalty (clamped to 0 minimum), returns to exploration.
///
/// Features built-in procedural 3D primitives so it works out of the box, with Inspector slots
/// ready for custom models/prefabs.
/// </summary>
public class HazardDodgeMinigame : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector & Custom Prefab Slots
    // -----------------------------------------------------------------------

    [Header("Custom 3D Prefabs (Optional — procedural primitives used if null)")]
    [Tooltip("Custom top-view Submarine 3D model prefab.")]
    [SerializeField] private GameObject customSubmarinePrefab;

    [Tooltip("Custom Thermal Vent 3D prefab.")]
    [SerializeField] private GameObject customThermalVentPrefab;

    [Tooltip("Custom Whirlpool / Vortex 3D prefab.")]
    [SerializeField] private GameObject customWhirlpoolPrefab;

    [Tooltip("Custom Falling Rock / Boulder 3D prefab.")]
    [SerializeField] private GameObject customRockPrefab;

    [Tooltip("Custom High-Pressure Data Pod 3D prefab.")]
    [SerializeField] private GameObject customDataPodPrefab;

    [Header("Custom UI Canvas (Optional — procedural UI used if null)")]
    [Tooltip("Custom parent UI Panel in your Canvas.")]
    public GameObject customUIRoot;
    [Tooltip("Custom RawImage where the 2.5D ocean stage renders.")]
    [SerializeField] private RawImage   customViewportRawImage;
    [Tooltip("Custom red flash border Image.")]
    [SerializeField] private Image      customRedVignette;
    [Tooltip("TMP text for distance remaining (e.g. 100m).")]
    [SerializeField] private TMP_Text   customDistanceText;
    [Tooltip("TMP text for accumulated RDP bonus (e.g. +25 RDP).")]
    [SerializeField] private TMP_Text   customRdpBonusText;
    [Tooltip("TMP text for Hull Integrity (e.g. 3 / 3).")]
    [SerializeField] private TMP_Text   customHullText;
    [Tooltip("Array of 3 UI Images for Hull Integrity Pips / Shield Badges.")]
    [SerializeField] private Image[]    customHullPipImages;
    [Tooltip("Custom Phase 1 Warning Banner GameObject (ENTERING HAZARD ZONE).")]
    [SerializeField] private GameObject customWarningBanner;
    [Tooltip("Custom Win / Fail result banner TMP text.")]
    [SerializeField] private TMP_Text   customResultBanner;

    [Header("Lane & Movement Tuning")]
    [SerializeField] private float laneWidth        = 3.2f;
    [SerializeField] private float laneSwitchSpeed  = 14f;
    [SerializeField] private float baseScrollSpeed  = 18f;
    [SerializeField] private float swipeSensitivity = 35f;

    // -----------------------------------------------------------------------
    // Zone Difficulty Configuration
    // -----------------------------------------------------------------------

    private static readonly float[] ZoneDistances  = { 100f, 110f, 120f, 130f, 140f };
    private static readonly float[] ZoneSpeedMults = { 1.0f, 1.15f, 1.30f, 1.45f, 1.60f };
    private static readonly int[]   ZoneHullHits   = { 3, 3, 3, 2, 2 };
    private static readonly int[]   ZoneBaseBonus  = { 50, 100, 150, 200, 250 };

    private const float RunDurationSeconds = 20f; // 25 seconds total duration

    // -----------------------------------------------------------------------
    // Runtime State
    // -----------------------------------------------------------------------

    private int          _zoneIndex;
    private Action<int>  _onSuccess;
    private Action       _onFail;

    private int          _currentLane    = 1; // 0 = Left, 1 = Center, 2 = Right
    private float        _targetX        = 0f;
    private float        _currentX       = 0f;
    private int          _remainingHits  = 3;
    private float        _distanceLeft   = 100f;
    private float        _totalDistance  = 100f;
    private int          _collectedRdp   = 0;
    private bool         _isRunning      = false;
    private bool         _isFinished     = false;

    // Swipe tracking
    private Vector2      _touchStartPos;
    private bool         _touchActive;
    private bool         _hasSwipedThisTouch;

    // 2.5D World Stage
    private GameObject            _stageRoot;
    private Camera                _stageCamera;
    private readonly List<Camera> _disabledSceneCams = new List<Camera>();
    private RenderTexture         _stageRT;
    private RawImage              _stageViewport;
    private GameObject            _submarineObj;
    private Vector3               _stageOrigin = new Vector3(9000f, 9000f, 9000f);

    private void SwitchToMinigameCamera()
    {
        _disabledSceneCams.Clear();
        var allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in allCams)
        {
            if (c != _stageCamera && c.enabled)
            {
                c.enabled = false;
                _disabledSceneCams.Add(c);
            }
        }
        if (_stageCamera != null) _stageCamera.enabled = true;
    }

    private void RestoreSceneCamera()
    {
        foreach (var c in _disabledSceneCams)
        {
            if (c != null) c.enabled = true;
        }
        _disabledSceneCams.Clear();
    }

    private static Sprite _cachedPipSprite;
    private static Sprite GetPipSprite()
    {
        if (_cachedPipSprite != null) return _cachedPipSprite;

        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                if (dist > radius + 0.75f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float alpha = Mathf.Clamp01(radius + 0.75f - dist);
                    float highlight = Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(x, y), center + new Vector2(0f, 4f)) / radius));
                    Color col = Color.Lerp(new Color(0.85f, 0.95f, 1f), Color.white, highlight);
                    col.a = alpha;
                    tex.SetPixel(x, y, col);
                }
            }
        }
        tex.Apply();
        _cachedPipSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _cachedPipSprite;
    }

    private void SetExplorationHUDActive(bool active)
    {
        UIManager.Instance?.SetExplorationHUDVisible(active);

        var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (customUIRoot != null && c.transform.IsChildOf(customUIRoot.transform)) continue;
            if (c.gameObject == gameObject) continue;

            var hudTr = c.transform.Find("HUD");
            if (hudTr != null) hudTr.gameObject.SetActive(active);

            var mc = c.transform.Find("MobileControls");
            if (mc != null) mc.gameObject.SetActive(active);
        }

        var hudDirect = GameObject.Find("HUD");
        if (hudDirect != null) hudDirect.SetActive(active);

        var mcDirect = GameObject.Find("MobileControls");
        if (mcDirect != null) mcDirect.SetActive(active);
    }

    // Active Spawns
    private readonly List<ActiveHazardItem> _activeItems = new List<ActiveHazardItem>();
    private float _spawnTimer = 0f;

    // UI Elements
    private RectTransform _rootUI;
    private Image         _explorationVignette;
    private Image         _redVignette;
    private TMP_Text      _distanceLabel;
    private TMP_Text      _rdpBonusLabel;
    private TMP_Text      _hullLabel;
    private Image[]       _hullPipImages;

    // Rich Aesthetic Warning & Finish Banners
    private GameObject _warningBannerRoot;
    private Image      _warningBannerBg;
    private Image      _warningBannerBorder;
    private TMP_Text   _warningTitleTMP;
    private TMP_Text   _warningDescTMP;
    private TMP_Text   _warningHintTMP;
    private Coroutine  _warningPulseCoroutine;

    private GameObject _finishBannerRoot;
    private Image      _finishBannerBg;
    private Image      _finishBannerBorder;
    private Image      _finishTopStripe;
    private Image      _finishBottomStripe;
    private TMP_Text   _finishTitleTMP;
    private TMP_Text   _finishStatusTMP;
    private TMP_Text   _finishRewardTMP;
    private Coroutine  _finishPunchCoroutine;

    private bool         _isActive       = false;

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
    // Hazard Item Representation
    // -----------------------------------------------------------------------

    public enum HazardType { ThermalVent, Whirlpool, FallingRock, DataPod }

    private class ActiveHazardItem
    {
        public GameObject GameObject;
        public HazardType Type;
        public int        Lane;
        public float      ZPos;
        public bool       CollectedOrHit;
    }

    // -----------------------------------------------------------------------
    // Public Entry Point
    // -----------------------------------------------------------------------

    public void Initialize(int zoneIndex, Action<int> onSuccess, Action onFail)
    {
        _zoneIndex  = Mathf.Clamp(zoneIndex, 0, 4);
        _onSuccess  = onSuccess;
        _onFail     = onFail;
    }

    public void Show()
    {
        StopAllCoroutines();
        CleanupStage();

        SetExplorationHUDActive(false);

        _totalDistance = ZoneDistances[_zoneIndex];
        _distanceLeft  = _totalDistance;
        _remainingHits = ZoneHullHits[_zoneIndex];
        _collectedRdp  = 0;
        _currentLane   = 1;
        _targetX       = 0f;
        _currentX      = 0f;
        _isRunning     = false;
        _isFinished    = false;
        _touchActive   = false;
        _hasSwipedThisTouch = false;
        _isActive      = true;

        gameObject.SetActive(true);

        BuildStage();
        BuildUI();

        StartCoroutine(Phase1AlertSequence());
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (customUIRoot == null) return null;
        var cg = customUIRoot.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = customUIRoot.AddComponent<CanvasGroup>();
        }
        return cg;
    }

    private void EnsureExplorationVignette()
    {
        if (_explorationVignette != null) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var vigGO = new GameObject("ExplorationRedVignette", typeof(RectTransform), typeof(Image));
        vigGO.transform.SetParent(canvas.transform, false);
        vigGO.transform.SetAsLastSibling();
        var vigRect = vigGO.GetComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.sizeDelta = Vector2.zero;
        _explorationVignette = vigGO.GetComponent<Image>();
        _explorationVignette.color = new Color(0.85f, 0.20f, 0.12f, 0f);
        _explorationVignette.raycastTarget = false;
    }

    // -----------------------------------------------------------------------
    // Phase 1: Alert & Warning Transition Sequence (3.0s)
    // -----------------------------------------------------------------------

    private IEnumerator Phase1AlertSequence()
    {
        _isRunning = false;
        EnsureExplorationVignette();

        // Keep Minigame HUD / 2.5D viewport hidden during first-person exploration alert
        if (customUIRoot != null)
        {
            if (customUIRoot == gameObject)
            {
                var cg = GetCanvasGroup();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }
            }
            else
            {
                customUIRoot.SetActive(false);
            }
        }
        if (_rootUI != null) _rootUI.gameObject.SetActive(false);
        HideAllBanners();

        // 1. Gentle ambient warning on exploration screen with Warning Banner & Alert chime
        ShowWarningBanner(true);

        float elapsed = 0f;
        float exploreAlertDuration = 1.8f;
        while (elapsed < exploreAlertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Smooth, calming 0.5 Hz breathing pulse (max alpha 0.16f - no strobing or eye strain)
            float pulse = (Mathf.Sin(elapsed * Mathf.PI * 1.0f) * 0.5f + 0.5f) * 0.16f;
            Color flashCol = new Color(0.85f, 0.20f, 0.12f, pulse);
            if (_explorationVignette != null) _explorationVignette.color = flashCol;
            yield return null;
        }

        // 2. Transition into Hazard Dodge Minigame 3D stage
        SwitchToMinigameCamera();
        SetExplorationHUDActive(false);

        if (customUIRoot != null)
        {
            customUIRoot.SetActive(true);
            var cg = GetCanvasGroup();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
            if (customViewportRawImage != null)
            {
                customViewportRawImage.gameObject.SetActive(false); // Direct-to-screen camera, no RawImage needed!
            }
        }
        else
        {
            if (_rootUI != null) _rootUI.gameObject.SetActive(true);
            if (_stageViewport != null) _stageViewport.gameObject.SetActive(false);
        }

        // Zero out exploration vignette so it doesn't double-tint with minigame vignette
        if (_explorationVignette != null) _explorationVignette.color = new Color(0f, 0f, 0f, 0f);

        UpdateHUD();

        // 3. 1.2 second warning transition countdown (Warning banner remains steady and readable)
        float transitionElapsed = 0f;
        float transitionDuration = 1.2f;
        while (transitionElapsed < transitionDuration)
        {
            transitionElapsed += Time.unscaledDeltaTime;
            float pulse = (Mathf.Sin((exploreAlertDuration + transitionElapsed) * Mathf.PI * 1.0f) * 0.5f + 0.5f) * 0.16f;
            Color flashCol = new Color(0.85f, 0.20f, 0.12f, pulse);
            if (_redVignette != null)          _redVignette.color = flashCol;
            if (customRedVignette != null)     customRedVignette.color = flashCol;
            yield return null;
        }

        // 4. Deactivate warning banner & turn off ambient tint
        ShowWarningBanner(false);
        if (_explorationVignette != null) _explorationVignette.color = new Color(0f, 0f, 0f, 0f);
        if (_redVignette != null)          _redVignette.color = new Color(0f, 0f, 0f, 0f);
        if (customRedVignette != null)     customRedVignette.color = new Color(0f, 0f, 0f, 0f);

        // Phase 2 Begins: Minigame is live!
        _isRunning = true;
    }

    // -----------------------------------------------------------------------
    // Main Simulation Loop (Phase 2)
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_isFinished) return;

        HandleInput();

        if (!_isRunning) return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.1f) dt = 0.016f;

        float speed = baseScrollSpeed * ZoneSpeedMults[_zoneIndex];

        // 1. Update Distance over 25 seconds
        float distStep = (_totalDistance / RunDurationSeconds) * dt;
        _distanceLeft -= distStep;
        if (_distanceLeft <= 0f)
        {
            _distanceLeft = 0f;
            FinishRun(true);
            return;
        }

        // 2. Smooth Submarine Lane Movement & Bank Tilt
        _currentX = Mathf.Lerp(_currentX, _targetX, dt * laneSwitchSpeed);
        if (_submarineObj != null)
        {
            float tiltNorm = laneWidth > 0.01f ? Mathf.Clamp((_targetX - _currentX) / laneWidth, -1f, 1f) : 0f;
            float tilt = tiltNorm * -28.0f; // Snappy, sleek banking roll clamped to 28 degrees
            _submarineObj.transform.localPosition = new Vector3(_currentX, 0f, -2.2f);
            _submarineObj.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // 3. Spawn Obstacles & Data Pods
        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            SpawnHazardWave();
            _spawnTimer = UnityEngine.Random.Range(1.0f, 1.6f) / ZoneSpeedMults[_zoneIndex];
        }

        // 4. Update & Scroll Active Items
        UpdateActiveItems(speed, dt);

        // 5. Update HUD Text
        UpdateHUD();
    }

    // -----------------------------------------------------------------------
    // Input Handling (Mobile Swipes + Keyboard)
    // -----------------------------------------------------------------------

    private void HandleInput()
    {
        // 1. Keyboard (A/D, Left/Right)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) ShiftLane(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) ShiftLane(1);
        }

        // 2. Mobile Touch & Pointer Swipe (Swipe ONLY - exactly one lane move per swipe)
        var ts = Touchscreen.current;
        if (ts != null)
        {
            if (ts.primaryTouch.press.wasPressedThisFrame)
            {
                _touchActive = true;
                _hasSwipedThisTouch = false;
                _touchStartPos = ts.primaryTouch.position.ReadValue();
            }
            else if (ts.primaryTouch.press.isPressed && _touchActive && !_hasSwipedThisTouch)
            {
                Vector2 touchPos = ts.primaryTouch.position.ReadValue();
                float deltaX = touchPos.x - _touchStartPos.x;
                if (Mathf.Abs(deltaX) > swipeSensitivity)
                {
                    ShiftLane(deltaX > 0 ? 1 : -1);
                    _hasSwipedThisTouch = true; // Consumed: only 1 lane move per swipe gesture!
                }
            }
            else if (ts.primaryTouch.press.wasReleasedThisFrame)
            {
                _touchActive = false;
                _hasSwipedThisTouch = false;
            }
        }
        else
        {
            var ptr = Pointer.current;
            if (ptr != null)
            {
                if (ptr.press.wasPressedThisFrame)
                {
                    _touchActive = true;
                    _hasSwipedThisTouch = false;
                    _touchStartPos = ptr.position.ReadValue();
                }
                else if (ptr.press.isPressed && _touchActive && !_hasSwipedThisTouch)
                {
                    Vector2 ptrPos = ptr.position.ReadValue();
                    float deltaX = ptrPos.x - _touchStartPos.x;
                    if (Mathf.Abs(deltaX) > swipeSensitivity)
                    {
                        ShiftLane(deltaX > 0 ? 1 : -1);
                        _hasSwipedThisTouch = true; // Consumed: only 1 lane move per swipe gesture!
                    }
                }
                else if (ptr.press.wasReleasedThisFrame)
                {
                    _touchActive = false;
                    _hasSwipedThisTouch = false;
                }
            }
        }
    }

    public void SetLane(int targetLane)
    {
        if (!_isRunning || _isFinished) return;

        int clamped = Mathf.Clamp(targetLane, 0, 2);
        if (clamped != _currentLane)
        {
            _currentLane = clamped;
            _targetX = (_currentLane - 1) * laneWidth;
        }
    }

    public void ShiftLane(int direction)
    {
        if (!_isRunning || _isFinished) return;

        int newLane = Mathf.Clamp(_currentLane + direction, 0, 2);
        if (newLane != _currentLane)
        {
            _currentLane = newLane;
            _targetX = (_currentLane - 1) * laneWidth;
        }
    }

    // -----------------------------------------------------------------------
    // Procedural Hazard & Data Pod Spawning
    // -----------------------------------------------------------------------

    private void SpawnHazardWave()
    {
        // Pick 1 or 2 lanes to block, leaving at least 1 lane open
        int openLane = UnityEngine.Random.Range(0, 3);

        for (int lane = 0; lane < 3; lane++)
        {
            if (lane == openLane)
            {
                // 35% chance to spawn a glowing High-Pressure Data Pod in the open lane (Risk vs Reward!)
                if (UnityEngine.Random.value < 0.35f)
                {
                    SpawnItem(HazardType.DataPod, lane, 32f);
                }
                continue;
            }

            // 65% chance to spawn a hazard in blocked lanes
            if (UnityEngine.Random.value < 0.70f)
            {
                HazardType hType = PickHazardTypeForZone(_zoneIndex);
                SpawnItem(hType, lane, 32f);
            }
        }
    }

    private HazardType PickHazardTypeForZone(int zone)
    {
        float roll = UnityEngine.Random.value;
        if (zone <= 1)
        {
            return roll < 0.5f ? HazardType.ThermalVent : HazardType.Whirlpool;
        }
        else if (zone == 2)
        {
            if (roll < 0.4f) return HazardType.ThermalVent;
            if (roll < 0.7f) return HazardType.Whirlpool;
            return HazardType.FallingRock;
        }
        else
        {
            if (roll < 0.35f) return HazardType.FallingRock;
            if (roll < 0.70f) return HazardType.ThermalVent;
            return HazardType.Whirlpool;
        }
    }

    private void SpawnItem(HazardType type, int lane, float zPos)
    {
        GameObject go = null;
        float xPos = (lane - 1) * laneWidth;
        Vector3 worldPos = _stageOrigin + new Vector3(xPos, 0f, zPos);

        switch (type)
        {
            case HazardType.ThermalVent:
                go = customThermalVentPrefab != null
                    ? Instantiate(customThermalVentPrefab, worldPos, Quaternion.identity, _stageRoot.transform)
                    : CreateProceduralThermalVent(worldPos);
                break;

            case HazardType.Whirlpool:
                go = customWhirlpoolPrefab != null
                    ? Instantiate(customWhirlpoolPrefab, worldPos, Quaternion.identity, _stageRoot.transform)
                    : CreateProceduralWhirlpool(worldPos);
                break;

            case HazardType.FallingRock:
                go = customRockPrefab != null
                    ? Instantiate(customRockPrefab, worldPos, Quaternion.identity, _stageRoot.transform)
                    : CreateProceduralRock(worldPos);
                break;

            case HazardType.DataPod:
                go = customDataPodPrefab != null
                    ? Instantiate(customDataPodPrefab, worldPos, Quaternion.identity, _stageRoot.transform)
                    : CreateProceduralDataPod(worldPos);
                break;
        }

        if (go != null)
        {
            SetLayerRecursive(go, 0);
            _activeItems.Add(new ActiveHazardItem
            {
                GameObject     = go,
                Type           = type,
                Lane           = lane,
                ZPos           = zPos,
                CollectedOrHit = false
            });
        }
    }

    // -----------------------------------------------------------------------
    // Scroll & Collision Detection
    // -----------------------------------------------------------------------

    private void UpdateActiveItems(float speed, float dt)
    {
        for (int i = _activeItems.Count - 1; i >= 0; i--)
        {
            var item = _activeItems[i];
            if (item == null || item.GameObject == null)
            {
                _activeItems.RemoveAt(i);
                continue;
            }

            // Scroll item downward
            item.ZPos -= speed * dt;
            float xPos = (item.Lane - 1) * laneWidth;
            item.GameObject.transform.position = _stageOrigin + new Vector3(xPos, 0f, item.ZPos);

            // Animate spin/rotation
            if (item.Type == HazardType.Whirlpool)
                item.GameObject.transform.Rotate(Vector3.forward, 180f * dt);
            else if (item.Type == HazardType.DataPod)
                item.GameObject.transform.Rotate(Vector3.up, 120f * dt);

            // Collision check with submarine (submarine is located at Z = -2.2f)
            float subZ = -2.2f;
            if (!item.CollectedOrHit && Mathf.Abs(item.ZPos - subZ) < 2.0f)
            {
                // Lane alignment check (scaled to laneWidth)
                float hitThreshold = Mathf.Max(1.4f, laneWidth * 0.38f);
                if (item.Lane == _currentLane && Mathf.Abs(_currentX - xPos) < hitThreshold)
                {
                    item.CollectedOrHit = true;

                    if (item.Type == HazardType.DataPod)
                    {
                        // Collect Data Pod (+5 RDP bonus)
                        _collectedRdp += 5;
                        StartCoroutine(FlashItemCollection(item.GameObject));
                    }
                    else
                    {
                        // Hit Hazard!
                        OnHazardHit();
                    }
                }
            }

            // Despawn if scrolled past camera
            if (item.ZPos < -10f)
            {
                Destroy(item.GameObject);
                _activeItems.RemoveAt(i);
            }
        }
    }

    private void OnHazardHit()
    {
        _remainingHits--;
        StartCoroutine(ScreenShakeAndImpactFlash());

        if (_remainingHits <= 0)
        {
            FinishRun(false);
        }
    }

    private IEnumerator FlashItemCollection(GameObject go)
    {
        if (go == null) yield break;
        var rend = go.GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = Color.white;
        go.transform.localScale *= 1.3f;
        yield return new WaitForSecondsRealtime(0.12f);
        Destroy(go);
    }

    private IEnumerator ScreenShakeAndImpactFlash()
    {
        if (_redVignette != null)      _redVignette.color = new Color(0.85f, 0.15f, 0.12f, 0.28f);
        if (customRedVignette != null) customRedVignette.color = new Color(0.85f, 0.15f, 0.12f, 0.28f);

        Vector3 origCamPos = _stageCamera != null ? _stageCamera.transform.localPosition : Vector3.zero;
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_stageCamera != null)
            {
                Vector2 shake = UnityEngine.Random.insideUnitCircle * 0.35f;
                _stageCamera.transform.localPosition = origCamPos + new Vector3(shake.x, 0f, shake.y);
            }
            yield return null;
        }

        if (_stageCamera != null)      _stageCamera.transform.localPosition = origCamPos;
        if (_redVignette != null)      _redVignette.color = new Color(0f, 0f, 0f, 0f);
        if (customRedVignette != null) customRedVignette.color = new Color(0f, 0f, 0f, 0f);
    }

    // -----------------------------------------------------------------------
    // Outcome & Resolution (Phase 3)
    // -----------------------------------------------------------------------

    private void FinishRun(bool success)
    {
        if (_isFinished) return;
        _isFinished = true;
        _isRunning  = false;

        if (success)
        {
            int baseBonus = ZoneBaseBonus[_zoneIndex];
            int totalBonus = baseBonus + _collectedRdp;

            ShowFinishBanner(true, totalBonus);
            GameManager.Instance?.AddRDP(totalBonus);
            StartCoroutine(DelayedClose(true, totalBonus));
        }
        else
        {
            ShowFinishBanner(false, 0);
            GameManager.Instance?.DeductRDP(50);
            StartCoroutine(DelayedClose(false, 0));
        }
    }

    private IEnumerator DelayedClose(bool success, int totalReward)
    {
        yield return new WaitForSecondsRealtime(2.8f);

        RestoreSceneCamera();
        SetExplorationHUDActive(true);

        CleanupStage();
        if (_rootUI != null) _rootUI.gameObject.SetActive(false);
        if (customUIRoot != null) customUIRoot.SetActive(false);
        HideAllBanners();

        _isActive = false;
        if (success) _onSuccess?.Invoke(totalReward);
        else         _onFail?.Invoke();
    }

    // -----------------------------------------------------------------------
    // HUD & UI Updates
    // -----------------------------------------------------------------------

    private void UpdateHUD()
    {
        int distInt = Mathf.Max(0, Mathf.CeilToInt(_distanceLeft));
        int maxHits = ZoneHullHits[_zoneIndex];

        string distStr = $"DISTANCE LEFT: {distInt}m";
        string rdpStr  = $"RDP BONUS: +{_collectedRdp}";
        string hullStr = $"HULL INTEGRITY: {_remainingHits}/{maxHits}";

        // Procedural UI Binds
        if (_distanceLabel != null)
            _distanceLabel.text = distStr;

        if (_rdpBonusLabel != null)
            _rdpBonusLabel.text = rdpStr;

        if (_hullLabel != null)
        {
            _hullLabel.text = hullStr;
            _hullLabel.color = _remainingHits <= 1 ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 1f);
        }

        if (_hullPipImages != null)
        {
            for (int i = 0; i < _hullPipImages.Length; i++)
            {
                if (_hullPipImages[i] == null) continue;
                if (_hullPipImages[i].sprite == null) _hullPipImages[i].sprite = GetPipSprite();
                _hullPipImages[i].gameObject.SetActive(true);
                bool isIntact = i < _remainingHits;
                _hullPipImages[i].color = isIntact ? new Color(0.2f, 0.95f, 1f, 0.95f) : new Color(0.35f, 0.1f, 0.1f, 0.35f);
            }
        }

        // Custom UI Binds (Exact User Formatting)
        if (customDistanceText != null) customDistanceText.text = distStr;
        if (customRdpBonusText != null) customRdpBonusText.text = rdpStr;
        if (customHullText != null)     customHullText.text     = hullStr;

        if (customHullPipImages != null)
        {
            for (int i = 0; i < customHullPipImages.Length; i++)
            {
                if (customHullPipImages[i] == null) continue;
                if (customHullPipImages[i].sprite == null)
                {
                    customHullPipImages[i].sprite = GetPipSprite();
                }
                customHullPipImages[i].gameObject.SetActive(true);
                bool isIntact = i < _remainingHits;
                customHullPipImages[i].color = isIntact 
                    ? new Color(0.20f, 0.95f, 1.0f, 1.0f) // Bright glowing cyan badge
                    : new Color(0.35f, 0.10f, 0.10f, 0.35f); // Depleted dark red badge
            }
        }
    }

    // -----------------------------------------------------------------------
    // 2.5D Stage Construction & Procedural Fallbacks
    // -----------------------------------------------------------------------

    private void BuildStage()
    {
        if (_stageRoot != null) return;

        _stageRoot = new GameObject("HazardDodgeStage");
        _stageRoot.transform.position = _stageOrigin;

        // 1. Direct-to-Screen Minigame Camera (Framed for lower-third submarine view with 3 equal full-screen lanes)
        var camGO = new GameObject("StageCamera", typeof(Camera));
        camGO.transform.SetParent(_stageRoot.transform, false);
        _stageCamera = camGO.GetComponent<Camera>();
        _stageCamera.transform.localPosition = new Vector3(0f, 14.0f, -7.5f);
        _stageCamera.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
        _stageCamera.clearFlags = CameraClearFlags.SolidColor;
        _stageCamera.backgroundColor = new Color(0.015f, 0.05f, 0.12f, 1f);
        _stageCamera.fieldOfView = 48f;
        _stageCamera.nearClipPlane = 0.3f;
        _stageCamera.farClipPlane = 160f;
        _stageCamera.depth = 100;
        _stageCamera.targetTexture = null; // Direct full-screen backbuffer rendering!
        _stageCamera.enabled = false; // Enabled during transition by SwitchToMinigameCamera()

        var camData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (camData != null)
        {
            camData.renderPostProcessing = false;
            camData.renderShadows = false;
        }

        // Dynamically compute laneWidth based on screen aspect ratio so the 3 lanes ALWAYS span the entire screen!
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (aspect < 1.2f) aspect = 16f / 9f; // Mobile landscape safeguard

        float subZ = -2.2f;
        float camY = 14.0f;
        float camZ = -7.5f;
        float pitchRad = 52.0f * Mathf.Deg2Rad;
        float dz = camY * Mathf.Sin(pitchRad) + (subZ - camZ) * Mathf.Cos(pitchRad);
        float vFovRad = 48.0f * Mathf.Deg2Rad;
        float wSub = 2.0f * dz * aspect * Mathf.Tan(vFovRad * 0.5f);

        // Divide total visible width into 3 equal lanes with slim outer boundary bezel margins
        laneWidth = wSub / 3.08f;

        // 2. Stage Lighting (Directional Key Light + Fill Light for guaranteed mobile URP visibility)
        var lightGO = new GameObject("StageKeyLight", typeof(Light));
        lightGO.transform.SetParent(_stageRoot.transform, false);
        lightGO.transform.localRotation = Quaternion.Euler(50f, -25f, 0f);
        var keyLight = lightGO.GetComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.3f;
        keyLight.color = new Color(0.85f, 0.95f, 1.0f);

        var fillLightGO = new GameObject("StageFillLight", typeof(Light));
        fillLightGO.transform.SetParent(_stageRoot.transform, false);
        fillLightGO.transform.localRotation = Quaternion.Euler(-35f, 155f, 0f);
        var fillLight = fillLightGO.GetComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.6f;
        fillLight.color = new Color(0.35f, 0.65f, 0.95f);

        // 3. Lane Track / Seafloor Trench Plane (Wide enough to fill all device aspects without gaps)
        var trenchGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        trenchGO.name = "TrenchFloor";
        trenchGO.transform.SetParent(_stageRoot.transform, false);
        trenchGO.transform.localPosition = new Vector3(0f, -0.6f, 25f);
        trenchGO.transform.localScale = new Vector3(8.0f, 1f, 14.0f);
        var trenchRend = trenchGO.GetComponent<Renderer>();
        trenchRend.material = CreateLitMaterial(new Color(0.03f, 0.08f, 0.14f));
        Destroy(trenchGO.GetComponent<Collider>());

        // 3 Equal Lanes taking up the entire screen:
        // Lane 0 = -laneWidth (Left 1/3) | Lane 1 = 0.0f (Center 1/3) | Lane 2 = +laneWidth (Right 1/3)
        // Glowing cyan lane divider lines
        CreateLaneLine(-laneWidth * 0.5f, new Color(0.20f, 0.90f, 1.0f, 0.85f));
        CreateLaneLine(laneWidth * 0.5f, new Color(0.20f, 0.90f, 1.0f, 0.85f));

        // Glowing outer boundary warning lines right at the screen edges
        CreateBoundaryWall(-laneWidth * 1.5f);
        CreateBoundaryWall(laneWidth * 1.5f);

        // Outer abyss seabed ledges
        CreateSideLedge(-laneWidth * 1.5f - 8f);
        CreateSideLedge(laneWidth * 1.5f + 8f);

        // 4. Submarine Player
        if (customSubmarinePrefab != null)
        {
            _submarineObj = Instantiate(customSubmarinePrefab, _stageOrigin + new Vector3(0f, 0f, -2.2f), Quaternion.identity, _stageRoot.transform);
        }
        else
        {
            _submarineObj = CreateProceduralSubmarine();
        }
    }

    private void CreateLaneLine(float xOffset, Color glowCol)
    {
        var lineGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineGO.name = "LaneLine";
        lineGO.transform.SetParent(_stageRoot.transform, false);
        lineGO.transform.localPosition = new Vector3(xOffset, -0.55f, 25f);
        lineGO.transform.localScale = new Vector3(0.14f, 0.06f, 90f);
        var rend = lineGO.GetComponent<Renderer>();
        rend.material = CreateUnlitMaterial(glowCol);
        Destroy(lineGO.GetComponent<Collider>());
    }

    private void CreateBoundaryWall(float xOffset)
    {
        var wallGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallGO.name = "BoundaryMarker";
        wallGO.transform.SetParent(_stageRoot.transform, false);
        wallGO.transform.localPosition = new Vector3(xOffset, -0.42f, 25f);
        wallGO.transform.localScale = new Vector3(0.25f, 0.38f, 90f);
        var rend = wallGO.GetComponent<Renderer>();
        rend.material = CreateUnlitMaterial(new Color(1.0f, 0.45f, 0.10f, 0.90f));
        Destroy(wallGO.GetComponent<Collider>());
    }

    private void CreateSideLedge(float xCenter)
    {
        var ledgeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledgeGO.name = "SideLedge";
        ledgeGO.transform.SetParent(_stageRoot.transform, false);
        ledgeGO.transform.localPosition = new Vector3(xCenter, -0.35f, 25f);
        ledgeGO.transform.localScale = new Vector3(16f, 0.5f, 90f);
        var rend = ledgeGO.GetComponent<Renderer>();
        rend.material = CreateLitMaterial(new Color(0.015f, 0.04f, 0.08f));
        Destroy(ledgeGO.GetComponent<Collider>());
    }

    private GameObject CreateProceduralSubmarine()
    {
        var subRoot = new GameObject("ProceduralSubmarine");
        subRoot.transform.SetParent(_stageRoot.transform, false);
        subRoot.transform.localPosition = new Vector3(0f, 0f, -2.2f);

        // Main Hull (Capsule)
        var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hull.transform.SetParent(subRoot.transform, false);
        hull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hull.transform.localScale = new Vector3(2.2f, 3.4f, 2.2f);
        hull.GetComponent<Renderer>().material = CreateLitMaterial(new Color(1.0f, 0.80f, 0.05f)); // Bright Exploration Yellow
        Destroy(hull.GetComponent<Collider>());

        // Conning Tower / Cockpit Dome
        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.transform.SetParent(subRoot.transform, false);
        dome.transform.localPosition = new Vector3(0f, 0.8f, 0.6f);
        dome.transform.localScale = new Vector3(1.4f, 1.1f, 1.7f);
        dome.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.1f, 0.9f, 1.0f)); // Glowing Cyan Glass
        Destroy(dome.GetComponent<Collider>());

        // Left & Right Stabilizer Fins
        CreateFin(subRoot.transform, new Vector3(-1.8f, 0f, -0.9f), new Vector3(1.4f, 0.16f, 0.9f));
        CreateFin(subRoot.transform, new Vector3(1.8f, 0f, -0.9f), new Vector3(1.4f, 0.16f, 0.9f));

        // Twin Thruster Glow Cones (Cyan Engine Glow)
        CreateEngineGlow(subRoot.transform, new Vector3(-0.7f, 0f, -2.0f));
        CreateEngineGlow(subRoot.transform, new Vector3(0.7f, 0f, -2.0f));

        return subRoot;
    }

    private void CreateFin(Transform parent, Vector3 localPos, Vector3 scale)
    {
        var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.transform.SetParent(parent, false);
        fin.transform.localPosition = localPos;
        fin.transform.localScale = scale;
        fin.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.2f, 0.3f, 0.4f));
        Destroy(fin.GetComponent<Collider>());
    }

    private void CreateEngineGlow(Transform parent, Vector3 localPos)
    {
        var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.transform.SetParent(parent, false);
        glow.transform.localPosition = localPos;
        glow.transform.localScale = new Vector3(0.5f, 0.5f, 0.7f);
        glow.GetComponent<Renderer>().material = CreateUnlitMaterial(new Color(0.2f, 0.95f, 1.0f, 0.9f));
        Destroy(glow.GetComponent<Collider>());
    }

    private GameObject CreateProceduralThermalVent(Vector3 worldPos)
    {
        var go = new GameObject("ThermalVent");
        go.transform.position = worldPos;

        // Rock Chimney Cone (Cylinder)
        var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cone.transform.SetParent(go.transform, false);
        cone.transform.localScale = new Vector3(2.8f, 1.6f, 2.8f);
        cone.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.18f, 0.12f, 0.08f));
        Destroy(cone.GetComponent<Collider>());

        // Glowing Core Plume (Sphere)
        var plume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        plume.transform.SetParent(go.transform, false);
        plume.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        plume.transform.localScale = new Vector3(2.2f, 2.6f, 2.2f);
        plume.GetComponent<Renderer>().material = CreateUnlitMaterial(new Color(1.0f, 0.35f, 0.05f, 0.90f));
        Destroy(plume.GetComponent<Collider>());

        return go;
    }

    private GameObject CreateProceduralWhirlpool(Vector3 worldPos)
    {
        var go = new GameObject("Whirlpool");
        go.transform.position = worldPos;

        // Outer Vortex Ring (Cylinder)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(go.transform, false);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(4.2f, 0.15f, 4.2f);
        ring.GetComponent<Renderer>().material = CreateUnlitMaterial(new Color(0.10f, 0.85f, 0.95f, 0.65f));
        Destroy(ring.GetComponent<Collider>());

        return go;
    }

    private GameObject CreateProceduralRock(Vector3 worldPos)
    {
        var go = new GameObject("FallingRock");
        go.transform.position = worldPos;

        // Jagged Rock Sphere
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.transform.SetParent(go.transform, false);
        rock.transform.localScale = new Vector3(2.8f, 2.4f, 2.8f);
        rock.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.25f, 0.22f, 0.20f));
        Destroy(rock.GetComponent<Collider>());

        return go;
    }

    private GameObject CreateProceduralDataPod(Vector3 worldPos)
    {
        var go = new GameObject("DataPod");
        go.transform.position = worldPos;

        // Golden/Cyan Spinning Diamond (Cube rotated 45 deg)
        var diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diamond.transform.SetParent(go.transform, false);
        diamond.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        diamond.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        diamond.GetComponent<Renderer>().material = CreateUnlitMaterial(new Color(1.0f, 0.82f, 0.15f, 0.95f));
        Destroy(diamond.GetComponent<Collider>());

        return go;
    }

    private Material CreateLitMaterial(Color col)
    {
        return MaterialUtils.CreateColoredMaterial(col);
    }

    private Material CreateUnlitMaterial(Color col)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (s != null)
        {
            var m = new Material(s);
            MaterialUtils.SetMaterialColor(m, col);
            return m;
        }
        return MaterialUtils.CreateColoredMaterial(col);
    }

    private void CleanupStage()
    {
        RestoreSceneCamera();
        SetExplorationHUDActive(true);

        foreach (var item in _activeItems)
        {
            if (item != null && item.GameObject != null)
                Destroy(item.GameObject);
        }
        _activeItems.Clear();

        if (_stageRoot != null)
        {
            Destroy(_stageRoot);
            _stageRoot = null;
        }

        if (_stageRT != null)
        {
            _stageRT.Release();
            Destroy(_stageRT);
            _stageRT = null;
        }
    }

    // -----------------------------------------------------------------------
    // UI Construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        // 1. Auto-discover custom UI panel in Canvas if unassigned
        if (customUIRoot == null)
        {
            var found = GameObject.Find("Minigame4HUD") ?? GameObject.Find("Minigame4_HUD") ?? GameObject.Find("Minigame4");
            if (found == null)
            {
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    for (int i = 0; i < c.transform.childCount; i++)
                    {
                        var child = c.transform.GetChild(i);
                        if (child.name.Equals("Minigame4HUD", StringComparison.OrdinalIgnoreCase) ||
                            child.name.Equals("Minigame4", StringComparison.OrdinalIgnoreCase))
                        {
                            found = child.gameObject;
                            break;
                        }
                    }
                    if (found != null) break;
                }
            }
            if (found != null)
            {
                customUIRoot = found;
                if (customViewportRawImage == null) customViewportRawImage = customUIRoot.GetComponentInChildren<RawImage>(true);
                if (customDistanceText == null) customDistanceText = customUIRoot.transform.Find("DistanceText")?.GetComponent<TMP_Text>() ?? customUIRoot.transform.Find("Distance")?.GetComponent<TMP_Text>();
                if (customRdpBonusText == null) customRdpBonusText = customUIRoot.transform.Find("RdpText")?.GetComponent<TMP_Text>() ?? customUIRoot.transform.Find("RDP")?.GetComponent<TMP_Text>();
                if (customHullText == null)     customHullText     = customUIRoot.transform.Find("HullIntegrity")?.GetComponentInChildren<TMP_Text>() ?? customUIRoot.transform.Find("Hull")?.GetComponent<TMP_Text>();
                if (customRedVignette == null)
                {
                    var redTransform = customUIRoot.transform.Find("RedImage") ?? customUIRoot.transform.Find("RedVignette");
                    if (redTransform != null) customRedVignette = redTransform.GetComponent<Image>();
                }
            }
        }

        if (customUIRoot != null)
        {
            if (customViewportRawImage == null)
                customViewportRawImage = customUIRoot.GetComponentInChildren<RawImage>(true);

            // Deactivate RawImage so it never blocks the direct-to-screen minigame camera
            if (customViewportRawImage != null)
            {
                customViewportRawImage.gameObject.SetActive(false);
            }

            // Auto-find hull pips if not wired in Inspector
            if (customHullPipImages == null || customHullPipImages.Length == 0)
            {
                var customHullContainer = customUIRoot.transform.Find("HullIntegrity") ?? customUIRoot.transform.Find("Hull");
                if (customHullContainer != null)
                {
                    var pips = new List<Image>();
                    for (int i = 1; i <= 3; i++)
                    {
                        var p = customHullContainer.Find($"Pip{i}")?.GetComponent<Image>() ?? customHullContainer.Find($"Pip_{i}")?.GetComponent<Image>();
                        if (p != null) pips.Add(p);
                    }
                    if (pips.Count > 0) customHullPipImages = pips.ToArray();
                }
            }

            // Configure HorizontalLayoutGroup so pips stay crisp, fixed-size badges next to Hull text
            var customHlg = customHullText != null ? customHullText.GetComponentInParent<HorizontalLayoutGroup>() : null;
            if (customHlg != null)
            {
                customHlg.childForceExpandWidth = false;
                customHlg.childControlWidth = false;
                customHlg.spacing = 10f;
                customHlg.childAlignment = TextAnchor.MiddleRight;
            }

            if (customHullPipImages != null)
            {
                for (int i = 0; i < customHullPipImages.Length; i++)
                {
                    if (customHullPipImages[i] == null) continue;
                    if (customHullPipImages[i].sprite == null)
                    {
                        customHullPipImages[i].sprite = GetPipSprite();
                    }
                    var le = customHullPipImages[i].GetComponent<LayoutElement>() ?? customHullPipImages[i].gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 24f;
                    le.preferredHeight = 24f;
                    le.minWidth = 24f;
                    le.minHeight = 24f;
                    le.flexibleWidth = 0f;
                    le.flexibleHeight = 0f;
                    customHullPipImages[i].rectTransform.sizeDelta = new Vector2(24f, 24f);
                    customHullPipImages[i].gameObject.SetActive(true);
                }
            }

            if (customRedVignette == null)
            {
                var redTransform = customUIRoot.transform.Find("RedImage") ?? customUIRoot.transform.Find("RedVignette");
                if (redTransform != null) customRedVignette = redTransform.GetComponent<Image>();
            }

            if (customRedVignette != null)
            {
                customRedVignette.color = new Color(1f, 0.05f, 0.05f, 0f);
            }

            Canvas customCanvas = customUIRoot.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            EnsureBanners(customCanvas, UIThemeManager.AlohaFont);

            if (customWarningBanner != null)
            {
                customWarningBanner.SetActive(false);
            }

            return;
        }
        if (_rootUI != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var font = UIThemeManager.AlohaFont;

        // Root Container
        var rootGO = new GameObject("HazardDodgeUI", typeof(RectTransform));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootUI = rootGO.GetComponent<RectTransform>();
        _rootUI.anchorMin = Vector2.zero;
        _rootUI.anchorMax = Vector2.one;
        _rootUI.sizeDelta = Vector2.zero;

        // 2.5D Viewport RawImage
        var vpGO = new GameObject("StageViewport", typeof(RectTransform), typeof(RawImage));
        vpGO.transform.SetParent(_rootUI, false);
        var vpRect = vpGO.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        _stageViewport = vpGO.GetComponent<RawImage>();
        _stageViewport.texture = _stageRT;
        _stageViewport.raycastTarget = false;

        // Red Vignette Flash
        var vigGO = new GameObject("RedVignette", typeof(RectTransform), typeof(Image));
        vigGO.transform.SetParent(_rootUI, false);
        var vigRect = vigGO.GetComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.sizeDelta = Vector2.zero;
        _redVignette = vigGO.GetComponent<Image>();
        _redVignette.color = new Color(1f, 0.05f, 0.05f, 0f);
        _redVignette.raycastTarget = false;

        // Top Navigation Bar (Expanded to accommodate 36pt Poppins without overlapping)
        var topBarGO = new GameObject("TopNavBar", typeof(RectTransform), typeof(Image));
        topBarGO.transform.SetParent(_rootUI, false);
        var topRect = topBarGO.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0.5f, 1f);
        topRect.anchorMax = new Vector2(0.5f, 1f);
        topRect.pivot     = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(780f, 90f);
        topRect.anchoredPosition = new Vector2(0f, -20f);
        topBarGO.GetComponent<Image>().color = new Color(0.03f, 0.08f, 0.15f, 0.90f);

        // Distance Label (Top Left)
        var distGO = new GameObject("DistanceText", typeof(RectTransform));
        distGO.transform.SetParent(topBarGO.transform, false);
        var distRect = distGO.GetComponent<RectTransform>();
        distRect.anchorMin = new Vector2(0.03f, 0.5f);
        distRect.anchorMax = new Vector2(0.48f, 0.5f);
        distRect.pivot = new Vector2(0f, 0.5f);
        distRect.sizeDelta = new Vector2(0f, 54f);
        _distanceLabel = distGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _distanceLabel.font = font;
        _distanceLabel.fontSize = 36;
        _distanceLabel.fontStyle = FontStyles.Bold;
        _distanceLabel.color = Color.white;
        _distanceLabel.text = "DISTANCE: 100m";

        // RDP Bonus Label (Top Right)
        var rdpGO = new GameObject("RdpText", typeof(RectTransform));
        rdpGO.transform.SetParent(topBarGO.transform, false);
        var rdpRect = rdpGO.GetComponent<RectTransform>();
        rdpRect.anchorMin = new Vector2(0.52f, 0.5f);
        rdpRect.anchorMax = new Vector2(0.97f, 0.5f);
        rdpRect.pivot = new Vector2(1f, 0.5f);
        rdpRect.sizeDelta = new Vector2(0f, 54f);
        _rdpBonusLabel = rdpGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _rdpBonusLabel.font = font;
        _rdpBonusLabel.fontSize = 36;
        _rdpBonusLabel.fontStyle = FontStyles.Bold;
        _rdpBonusLabel.alignment = TextAlignmentOptions.Right;
        _rdpBonusLabel.color = new Color(1f, 0.85f, 0.2f);
        _rdpBonusLabel.text = "RDP: +0";

        // Hull Integrity Container (Below Top Bar with Auto-Centering Layout)
        var hullContainer = new GameObject("HullContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        hullContainer.transform.SetParent(_rootUI, false);
        var hullRect = hullContainer.GetComponent<RectTransform>();
        hullRect.anchorMin = new Vector2(0.5f, 1f);
        hullRect.anchorMax = new Vector2(0.5f, 1f);
        hullRect.pivot = new Vector2(0.5f, 1f);
        hullRect.sizeDelta = new Vector2(580f, 50f);
        hullRect.anchoredPosition = new Vector2(0f, -120f);

        var hlg = hullContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 12f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var hTextGO = new GameObject("HullText", typeof(RectTransform));
        hTextGO.transform.SetParent(hullContainer.transform, false);
        var htRect = hTextGO.GetComponent<RectTransform>();
        htRect.sizeDelta = new Vector2(320f, 48f);
        _hullLabel = hTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _hullLabel.font = font;
        _hullLabel.fontSize = 36;
        _hullLabel.fontStyle = FontStyles.Bold;
        _hullLabel.alignment = TextAlignmentOptions.MidlineRight;
        _hullLabel.color = new Color(0.3f, 0.9f, 1f);
        _hullLabel.text = "HULL INTEGRITY:";

        // 3 Physical Badge Pips
        int maxHits = 3;
        _hullPipImages = new Image[maxHits];
        for (int i = 0; i < maxHits; i++)
        {
            var pipGO = new GameObject($"HullPip_{i}", typeof(RectTransform), typeof(Image));
            pipGO.transform.SetParent(hullContainer.transform, false);
            var pr = pipGO.GetComponent<RectTransform>();
            pr.sizeDelta = new Vector2(30f, 24f);
            _hullPipImages[i] = pipGO.GetComponent<Image>();
            _hullPipImages[i].color = new Color(0.2f, 0.95f, 1f, 0.95f);
        }

        // Ensure banners exist for procedural HUD
        EnsureBanners(canvas, font);

        _rootUI.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Warning & Finish Banner Management & Builders
    // -----------------------------------------------------------------------

    private void EnsureBanners(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null) return;

        // Auto-link if present in customUIRoot
        if (customWarningBanner == null && customUIRoot != null)
        {
            var foundWarn = customUIRoot.transform.Find("WarningBanner") ?? customUIRoot.transform.Find("AlertBanner");
            if (foundWarn != null) customWarningBanner = foundWarn.gameObject;
        }

        if (customResultBanner == null && customUIRoot != null)
        {
            var foundRes = customUIRoot.transform.Find("FinishBanner") ?? customUIRoot.transform.Find("ResultBanner");
            if (foundRes != null) customResultBanner = foundRes.GetComponent<TMP_Text>() ?? foundRes.GetComponentInChildren<TMP_Text>();
        }

        // Build procedural Warning Banner if not assigned
        if (customWarningBanner == null && _warningBannerRoot == null)
        {
            BuildProceduralWarningBanner(canvas.transform, font);
        }

        // Build procedural Finish Banner if not assigned
        if (customResultBanner == null && _finishBannerRoot == null)
        {
            BuildProceduralFinishBanner(canvas.transform, font);
        }
    }

    private void BuildProceduralWarningBanner(Transform parent, TMP_FontAsset font)
    {
        if (font == null) font = UIThemeManager.AntoneFont;

        // 1. Root Container with Scrim Dimmer (fills parent canvas)
        _warningBannerRoot = new GameObject("HazardWarningBanner", typeof(RectTransform));
        _warningBannerRoot.transform.SetParent(parent, false);
        _warningBannerRoot.transform.SetAsLastSibling();

        var r = _warningBannerRoot.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = Vector2.zero;
        r.anchoredPosition = Vector2.zero;

        // Dark ambient background scrim to dim scene behind the banner
        var scrimGO = new GameObject("DimmerScrim", typeof(RectTransform), typeof(Image));
        scrimGO.transform.SetParent(_warningBannerRoot.transform, false);
        var scrimRect = scrimGO.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.sizeDelta = Vector2.zero;
        var scrimImg = scrimGO.GetComponent<Image>();
        scrimImg.color = new Color(0f, 0f, 0f, 0.88f); // Deep dark dimmer scrim
        scrimImg.raycastTarget = false;

        // 2. Outer Warning Border Frame (1120 x 260) - Vivid Alert Red Glowing Border Outline
        var borderFrameGO = new GameObject("AlertBorderFrame", typeof(RectTransform), typeof(Image));
        borderFrameGO.transform.SetParent(_warningBannerRoot.transform, false);
        var borderRect = borderFrameGO.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.52f);
        borderRect.anchorMax = new Vector2(0.5f, 0.52f);
        borderRect.pivot     = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(1120f, 260f);
        _warningBannerBorder = borderFrameGO.GetComponent<Image>();
        _warningBannerBorder.color = new Color(0.95f, 0.20f, 0.22f, 0.95f); // Vivid alert red
        _warningBannerBorder.raycastTarget = false;

        // 3. Inner Dark Alert Card (inset 4px inside border frame) - Deep Dark Crimson-Black
        var cardGO = new GameObject("AlertCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(borderFrameGO.transform, false);
        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = new Vector2(4f, 4f);
        cardRect.offsetMax = new Vector2(-4f, -4f);
        _warningBannerBg = cardGO.GetComponent<Image>();
        _warningBannerBg.color = new Color(0.04f, 0.01f, 0.02f, 0.98f); // Deep dark crimson-black (like minigame 3 dark banners)
        _warningBannerBg.raycastTarget = false;

        // Top hazard accent line
        var topStripe = new GameObject("TopStripe", typeof(RectTransform), typeof(Image));
        topStripe.transform.SetParent(cardGO.transform, false);
        var tsR = topStripe.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0f, 1f);
        tsR.anchorMax = new Vector2(1f, 1f);
        tsR.pivot     = new Vector2(0.5f, 1f);
        tsR.sizeDelta = new Vector2(0f, 5f);
        var topStripeImg = topStripe.GetComponent<Image>();
        topStripeImg.color = new Color(1f, 0.30f, 0.30f, 0.9f);
        topStripeImg.raycastTarget = false;

        // Bottom hazard accent line
        var botStripe = new GameObject("BottomStripe", typeof(RectTransform), typeof(Image));
        botStripe.transform.SetParent(cardGO.transform, false);
        var bsR = botStripe.GetComponent<RectTransform>();
        bsR.anchorMin = new Vector2(0f, 0f);
        bsR.anchorMax = new Vector2(1f, 0f);
        bsR.pivot     = new Vector2(0.5f, 0f);
        bsR.sizeDelta = new Vector2(0f, 5f);
        var botStripeImg = botStripe.GetComponent<Image>();
        botStripeImg.color = new Color(1f, 0.30f, 0.30f, 0.9f);
        botStripeImg.raycastTarget = false;

        // 3. Warning Title (Vibrant Alert Red, 44px Bold, Clean Text)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        var tR = titleGO.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0f, 1f);
        tR.anchorMax = new Vector2(1f, 1f);
        tR.pivot     = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -22f);
        tR.sizeDelta = new Vector2(0f, 58f);
        _warningTitleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _warningTitleTMP.font = font;
        _warningTitleTMP.enableAutoSizing = false;
        _warningTitleTMP.fontSize = 44f;
        _warningTitleTMP.fontStyle = FontStyles.Bold;
        _warningTitleTMP.alignment = TextAlignmentOptions.Center;
        _warningTitleTMP.color = new Color(1f, 0.35f, 0.35f, 1f); // Vibrant bright alert red
        _warningTitleTMP.text = "HAZARD ZONE DETECTED";
        _warningTitleTMP.raycastTarget = false;

        // 4. Instruction Subtitle (Crisp Pure White, 36px Bold)
        var descGO = new GameObject("Instruction", typeof(RectTransform));
        descGO.transform.SetParent(cardGO.transform, false);
        var dR = descGO.GetComponent<RectTransform>();
        dR.anchorMin = new Vector2(0f, 1f);
        dR.anchorMax = new Vector2(1f, 1f);
        dR.pivot     = new Vector2(0.5f, 1f);
        dR.anchoredPosition = new Vector2(0f, -96f);
        dR.sizeDelta = new Vector2(0f, 52f);
        _warningDescTMP = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _warningDescTMP.font = font;
        _warningDescTMP.enableAutoSizing = false;
        _warningDescTMP.fontSize = 36f;
        _warningDescTMP.fontStyle = FontStyles.Bold;
        _warningDescTMP.alignment = TextAlignmentOptions.Center;
        _warningDescTMP.color = Color.white; // Pure crisp white for 100% readability
        _warningDescTMP.text = "SWIPE TO EVADE INCOMING HAZARDS";
        _warningDescTMP.raycastTarget = false;

        // 5. Tip Line (Soft Cyan, 32px Bold)
        var hintGO = new GameObject("Hint", typeof(RectTransform));
        hintGO.transform.SetParent(cardGO.transform, false);
        var hR = hintGO.GetComponent<RectTransform>();
        hR.anchorMin = new Vector2(0f, 1f);
        hR.anchorMax = new Vector2(1f, 1f);
        hR.pivot     = new Vector2(0.5f, 1f);
        hR.anchoredPosition = new Vector2(0f, -162f);
        hR.sizeDelta = new Vector2(0f, 48f);
        _warningHintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _warningHintTMP.font = font;
        _warningHintTMP.enableAutoSizing = false;
        _warningHintTMP.fontSize = 32f;
        _warningHintTMP.fontStyle = FontStyles.Bold;
        _warningHintTMP.alignment = TextAlignmentOptions.Center;
        _warningHintTMP.color = new Color(0.40f, 0.92f, 1f, 1f); // Soft cyan
        _warningHintTMP.text = "COLLECT DATA PODS FOR BONUS RDP";
        _warningHintTMP.raycastTarget = false;

        _warningBannerRoot.SetActive(false);
    }

    private void BuildProceduralFinishBanner(Transform parent, TMP_FontAsset font)
    {
        _finishBannerRoot = new GameObject("HazardFinishBanner", typeof(RectTransform));
        _finishBannerRoot.transform.SetParent(parent, false);
        _finishBannerRoot.transform.SetAsLastSibling();

        var r = _finishBannerRoot.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = Vector2.zero;
        r.anchoredPosition = Vector2.zero;

        // Dark ambient background scrim to dim scene behind the finish banner
        var scrimGO = new GameObject("DimmerScrim", typeof(RectTransform), typeof(Image));
        scrimGO.transform.SetParent(_finishBannerRoot.transform, false);
        var scrimRect = scrimGO.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.sizeDelta = Vector2.zero;
        var scrimImg = scrimGO.GetComponent<Image>();
        scrimImg.color = new Color(0f, 0f, 0f, 0.88f); // Deep dark dimmer scrim
        scrimImg.raycastTarget = false;

        // 2. Outer Finish Border Frame (1120 x 260)
        var borderFrameGO = new GameObject("FinishBorderFrame", typeof(RectTransform), typeof(Image));
        borderFrameGO.transform.SetParent(_finishBannerRoot.transform, false);
        var borderRect = borderFrameGO.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot     = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(1120f, 260f);
        _finishBannerBorder = borderFrameGO.GetComponent<Image>();
        _finishBannerBorder.color = new Color(0.95f, 0.20f, 0.22f, 0.95f);
        _finishBannerBorder.raycastTarget = false;

        // 3. Inner Dark Finish Card (inset 4px inside border frame) - Deep Darkened Crimson-Black
        var cardGO = new GameObject("FinishCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(borderFrameGO.transform, false);
        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = new Vector2(4f, 4f);
        cardRect.offsetMax = new Vector2(-4f, -4f);
        _finishBannerBg = cardGO.GetComponent<Image>();
        _finishBannerBg.color = new Color(0.04f, 0.01f, 0.02f, 0.98f);
        _finishBannerBg.raycastTarget = false;

        // Top accent line
        var topStripe = new GameObject("TopStripe", typeof(RectTransform), typeof(Image));
        topStripe.transform.SetParent(cardGO.transform, false);
        var tsR = topStripe.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0f, 1f);
        tsR.anchorMax = new Vector2(1f, 1f);
        tsR.pivot     = new Vector2(0.5f, 1f);
        tsR.sizeDelta = new Vector2(0f, 5f);
        _finishTopStripe = topStripe.GetComponent<Image>();
        _finishTopStripe.color = new Color(1f, 0.30f, 0.30f, 0.9f);
        _finishTopStripe.raycastTarget = false;

        // Bottom accent line
        var botStripe = new GameObject("BottomStripe", typeof(RectTransform), typeof(Image));
        botStripe.transform.SetParent(cardGO.transform, false);
        var bsR = botStripe.GetComponent<RectTransform>();
        bsR.anchorMin = new Vector2(0f, 0f);
        bsR.anchorMax = new Vector2(1f, 0f);
        bsR.pivot     = new Vector2(0.5f, 0f);
        bsR.sizeDelta = new Vector2(0f, 5f);
        _finishBottomStripe = botStripe.GetComponent<Image>();
        _finishBottomStripe.color = new Color(1f, 0.30f, 0.30f, 0.9f);
        _finishBottomStripe.raycastTarget = false;

        // Finish Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        var tR = titleGO.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0f, 1f);
        tR.anchorMax = new Vector2(1f, 1f);
        tR.pivot     = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -24f);
        tR.sizeDelta = new Vector2(0f, 62f);
        _finishTitleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _finishTitleTMP.font = font;
        _finishTitleTMP.fontSize = 48f;
        _finishTitleTMP.fontStyle = FontStyles.Bold;
        _finishTitleTMP.alignment = TextAlignmentOptions.Center;

        // Finish Status
        var statusGO = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(cardGO.transform, false);
        var sR = statusGO.GetComponent<RectTransform>();
        sR.anchorMin = new Vector2(0f, 1f);
        sR.anchorMax = new Vector2(1f, 1f);
        sR.pivot     = new Vector2(0.5f, 1f);
        sR.anchoredPosition = new Vector2(0f, -96f);
        sR.sizeDelta = new Vector2(0f, 48f);
        _finishStatusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _finishStatusTMP.font = font;
        _finishStatusTMP.fontSize = 36f;
        _finishStatusTMP.fontStyle = FontStyles.Bold;
        _finishStatusTMP.alignment = TextAlignmentOptions.Center;

        // Finish Reward / Penalty
        var rewGO = new GameObject("Reward", typeof(RectTransform));
        rewGO.transform.SetParent(cardGO.transform, false);
        var rR = rewGO.GetComponent<RectTransform>();
        rR.anchorMin = new Vector2(0f, 1f);
        rR.anchorMax = new Vector2(1f, 1f);
        rR.pivot     = new Vector2(0.5f, 1f);
        rR.anchoredPosition = new Vector2(0f, -158f);
        rR.sizeDelta = new Vector2(0f, 54f);
        _finishRewardTMP = rewGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _finishRewardTMP.font = font;
        _finishRewardTMP.fontSize = 40f;
        _finishRewardTMP.fontStyle = FontStyles.Bold;
        _finishRewardTMP.alignment = TextAlignmentOptions.Center;

        _finishBannerRoot.SetActive(false);
    }

    private void ShowWarningBanner(bool show)
    {
        if (_warningPulseCoroutine != null)
        {
            StopCoroutine(_warningPulseCoroutine);
            _warningPulseCoroutine = null;
        }

        if (customWarningBanner != null)
        {
            customWarningBanner.SetActive(show);
        }

        if (_warningBannerRoot != null)
        {
            _warningBannerRoot.SetActive(show);
            if (show)
            {
                _warningBannerRoot.transform.SetAsLastSibling();
                _warningPulseCoroutine = StartCoroutine(AnimateWarningBannerPulse());
            }
        }

        if (show)
        {
            AudioManager.Instance?.PlayAlert();
        }
    }

    private IEnumerator AnimateWarningBannerPulse()
    {
        if (_warningBannerRoot == null) yield break;
        float elapsed = 0f;
        _warningBannerRoot.transform.localScale = Vector3.one;

        while (_warningBannerRoot.activeSelf)
        {
            elapsed += Time.unscaledDeltaTime;
            // Rock-solid stationary scale so text never shakes or vibrates while reading
            _warningBannerRoot.transform.localScale = Vector3.one;

            // Gentle breathing border glow (vivid alarm red outline)
            if (_warningBannerBorder != null)
            {
                float borderAlpha = Mathf.Lerp(0.70f, 1.0f, Mathf.Sin(elapsed * 2.0f) * 0.5f + 0.5f);
                _warningBannerBorder.color = new Color(0.95f, 0.20f, 0.22f, borderAlpha);
            }
            yield return null;
        }
        _warningBannerRoot.transform.localScale = Vector3.one;
    }

    private void ShowFinishBanner(bool success, int totalBonus)
    {
        if (customResultBanner != null)
        {
            customResultBanner.gameObject.SetActive(true);
            customResultBanner.text = success
                ? $"HAZARD ZONE CLEARED!\n+{totalBonus} RDP"
                : "HULL COMPROMISED!\n-50 RDP";
            if (!success)
            {
                var pImg = customResultBanner.GetComponentInParent<Image>();
                if (pImg != null) pImg.color = new Color(0.18f, 0.03f, 0.04f, 0.98f);
            }
        }

        if (_finishBannerRoot != null)
        {
            _finishBannerRoot.SetActive(true);
            _finishBannerRoot.transform.SetAsLastSibling();

            if (success)
            {
                // Victory Theme (Cyan & Emerald)
                if (_finishBannerBg != null)
                    _finishBannerBg.color = new Color(0.01f, 0.04f, 0.08f, 0.98f); // Deep dark oceanic navy
                if (_finishBannerBorder != null)
                    _finishBannerBorder.color = new Color(0f, 0.92f, 0.95f, 0.95f); // Neon cyan outline
                if (_finishTopStripe != null)
                    _finishTopStripe.color = new Color(0.1f, 0.95f, 0.7f, 1f);
                if (_finishBottomStripe != null)
                    _finishBottomStripe.color = new Color(0.1f, 0.95f, 0.7f, 1f);

                if (_finishTitleTMP != null)
                {
                    _finishTitleTMP.color = new Color(0.3f, 1f, 0.9f);
                    _finishTitleTMP.text = "HAZARD ZONE CLEARED!";
                }
                if (_finishStatusTMP != null)
                {
                    _finishStatusTMP.color = new Color(0.92f, 0.96f, 1f);
                    _finishStatusTMP.text = "EXTREME DEPTH SURVEY DATA RECOVERED";
                }
                if (_finishRewardTMP != null)
                {
                    _finishRewardTMP.color = new Color(1f, 0.88f, 0.15f); // Gold
                    _finishRewardTMP.text = $"+{totalBonus} RDP REWARD AWARDED";
                }
            }
            else
            {
                // Defeat Theme (Deep Darkened Crimson-Black with high-contrast text)
                if (_finishBannerBg != null)
                    _finishBannerBg.color = new Color(0.04f, 0.01f, 0.02f, 0.98f); // Deep dark crimson-black
                if (_finishBannerBorder != null)
                    _finishBannerBorder.color = new Color(0.95f, 0.20f, 0.22f, 0.95f); // Vivid alarm red outline
                if (_finishTopStripe != null)
                    _finishTopStripe.color = new Color(1f, 0.30f, 0.30f, 1f);
                if (_finishBottomStripe != null)
                    _finishBottomStripe.color = new Color(1f, 0.30f, 0.30f, 1f);

                if (_finishTitleTMP != null)
                {
                    _finishTitleTMP.color = new Color(1f, 0.35f, 0.35f); // Vibrant bright alarm red
                    _finishTitleTMP.text = "HULL COMPROMISED!";
                }
                if (_finishStatusTMP != null)
                {
                    _finishStatusTMP.color = Color.white; // Crisp pure white for 100% legibility
                    _finishStatusTMP.text = "EMERGENCY SURFACE RETREAT INITIATED";
                }
                if (_finishRewardTMP != null)
                {
                    _finishRewardTMP.color = new Color(1f, 0.45f, 0.45f); // High-contrast bright coral penalty text
                    _finishRewardTMP.text = "-50 RDP REPAIR PENALTY";
                }
            }

            if (_finishPunchCoroutine != null)
            {
                StopCoroutine(_finishPunchCoroutine);
            }
            _finishPunchCoroutine = StartCoroutine(AnimateFinishBannerPunch());
        }
    }

    private IEnumerator AnimateFinishBannerPunch()
    {
        if (_finishBannerRoot == null) yield break;
        float elapsed = 0f;
        float duration = 0.35f;
        Vector3 baseScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = Mathf.LerpUnclamped(0.82f, 1.0f, EaseOutBack(t));
            _finishBannerRoot.transform.localScale = baseScale * scale;
            yield return null;
        }
        _finishBannerRoot.transform.localScale = baseScale;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private void HideAllBanners()
    {
        if (_warningPulseCoroutine != null)
        {
            StopCoroutine(_warningPulseCoroutine);
            _warningPulseCoroutine = null;
        }

        if (_finishPunchCoroutine != null)
        {
            StopCoroutine(_finishPunchCoroutine);
            _finishPunchCoroutine = null;
        }

        if (_warningBannerRoot != null)  _warningBannerRoot.SetActive(false);
        if (customWarningBanner != null) customWarningBanner.SetActive(false);

        if (_finishBannerRoot != null)   _finishBannerRoot.SetActive(false);
        if (customResultBanner != null)  customResultBanner.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreSceneCamera();
        SetExplorationHUDActive(true);
        HideAllBanners();
        if (_warningBannerRoot != null) Destroy(_warningBannerRoot);
        if (_finishBannerRoot != null)  Destroy(_finishBannerRoot);
        if (_explorationVignette != null) Destroy(_explorationVignette.gameObject);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
