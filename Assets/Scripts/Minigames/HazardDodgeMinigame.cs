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
    [SerializeField] private GameObject customUIRoot;
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

    // 2.5D World Stage
    private GameObject   _stageRoot;
    private Camera       _stageCamera;
    private RenderTexture _stageRT;
    private RawImage     _stageViewport;
    private GameObject   _submarineObj;
    private Vector3      _stageOrigin = new Vector3(9000f, 9000f, 9000f);

    // Active Spawns
    private readonly List<ActiveHazardItem> _activeItems = new List<ActiveHazardItem>();
    private float _spawnTimer = 0f;

    // UI Elements
    private RectTransform _rootUI;
    private Image         _explorationVignette;
    private Image         _redVignette;
    private GameObject    _warningBanner;
    private TMP_Text      _distanceLabel;
    private TMP_Text      _rdpBonusLabel;
    private TMP_Text      _hullLabel;
    private Image[]       _hullPipImages;
    private TMP_Text      _resultBanner;

    private void Awake()
    {
        if (customUIRoot != null)
            customUIRoot.SetActive(false);
    }

    private void Start()
    {
        if (customUIRoot != null)
            customUIRoot.SetActive(false);
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

        BuildStage();
        BuildUI();

        // Ensure Minigame HUD is hidden at the start of Show()
        if (customUIRoot != null) customUIRoot.SetActive(false);
        if (_rootUI != null)      _rootUI.gameObject.SetActive(false);

        // Begin Phase 1 Alert Sequence
        StartCoroutine(Phase1AlertSequence());
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
        _explorationVignette.color = new Color(1f, 0.05f, 0.05f, 0f);
        _explorationVignette.raycastTarget = false;
    }

    // -----------------------------------------------------------------------
    // Phase 1: Alert & Warning Transition Sequence (2.5s)
    // -----------------------------------------------------------------------

    private IEnumerator Phase1AlertSequence()
    {
        _isRunning = false;
        EnsureExplorationVignette();

        // Keep Minigame HUD / 2.5D viewport hidden during first-person exploration alert
        if (customUIRoot != null) customUIRoot.SetActive(false);
        if (_rootUI != null)      _rootUI.gameObject.SetActive(false);
        if (_warningBanner != null)       _warningBanner.SetActive(false);
        if (customWarningBanner != null) customWarningBanner.SetActive(false);
        if (_resultBanner != null)       _resultBanner.gameObject.SetActive(false);
        if (customResultBanner != null)  customResultBanner.gameObject.SetActive(false);

        // 1. Red flashing light on exploration screen (First-Person view) for 1.5 seconds
        float elapsed = 0f;
        float exploreAlertDuration = 1.5f;
        while (elapsed < exploreAlertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.PingPong(elapsed * 4.5f, 1f) * 0.45f;
            Color flashCol = new Color(1f, 0.08f, 0.08f, pulse);
            if (_explorationVignette != null) _explorationVignette.color = flashCol;
            yield return null;
        }

        // 2. Transition into Hazard Dodge Minigame 1 sec before start of minigame!
        if (customUIRoot != null)
        {
            customUIRoot.SetActive(true);
            if (customViewportRawImage != null)
            {
                customViewportRawImage.texture = _stageRT;
                customViewportRawImage.gameObject.SetActive(true);
            }
        }
        else
        {
            if (_rootUI != null) _rootUI.gameObject.SetActive(true);
            if (_stageViewport != null) _stageViewport.gameObject.SetActive(true);
        }

        UpdateHUD();

        // Show Warning Banner (Fallback if custom not assigned)
        if (customWarningBanner != null)
            customWarningBanner.SetActive(true);
        else if (_warningBanner != null)
            _warningBanner.SetActive(true);

        // 3. 1.0 second warning transition countdown
        float transitionElapsed = 0f;
        float transitionDuration = 1.0f;
        while (transitionElapsed < transitionDuration)
        {
            transitionElapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.PingPong((exploreAlertDuration + transitionElapsed) * 4.5f, 1f) * 0.40f;
            Color flashCol = new Color(1f, 0.08f, 0.08f, pulse);
            if (_explorationVignette != null) _explorationVignette.color = flashCol;
            if (_redVignette != null)          _redVignette.color = flashCol;
            if (customRedVignette != null)     customRedVignette.color = flashCol;
            yield return null;
        }

        // 4. Deactivate warning banners & turn off flash
        if (_warningBanner != null)       _warningBanner.SetActive(false);
        if (customWarningBanner != null) customWarningBanner.SetActive(false);
        if (_explorationVignette != null) _explorationVignette.color = new Color(1f, 0.08f, 0.08f, 0f);
        if (_redVignette != null)          _redVignette.color = new Color(1f, 0.08f, 0.08f, 0f);
        if (customRedVignette != null)     customRedVignette.color = new Color(1f, 0.08f, 0.08f, 0f);

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
            float tilt = (_targetX - _currentX) * -12.0f;
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
    // Input Handling (Swipes + Keys + Screen Taps)
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

        // 2. Mobile Touch & Pointer Swipe
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
        {
            Vector2 touchPos = ts.primaryTouch.position.ReadValue();
            if (!_touchActive)
            {
                _touchActive = true;
                _touchStartPos = touchPos;
            }
            else
            {
                float deltaX = touchPos.x - _touchStartPos.x;
                if (Mathf.Abs(deltaX) > swipeSensitivity)
                {
                    ShiftLane(deltaX > 0 ? 1 : -1);
                    _touchStartPos = touchPos; // Reset baseline for continuous swiping
                }
            }
        }
        else
        {
            var ptr = Pointer.current;
            if (ptr != null && ptr.press.isPressed)
            {
                Vector2 ptrPos = ptr.position.ReadValue();
                if (!_touchActive)
                {
                    _touchActive = true;
                    _touchStartPos = ptrPos;
                }
                else
                {
                    float deltaX = ptrPos.x - _touchStartPos.x;
                    if (Mathf.Abs(deltaX) > swipeSensitivity)
                    {
                        ShiftLane(deltaX > 0 ? 1 : -1);
                        _touchStartPos = ptrPos;
                    }
                }
            }
            else
            {
                _touchActive = false;
            }
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
            SetLayerRecursive(go, LayerMask.NameToLayer("UI"));
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
            if (!item.CollectedOrHit && Mathf.Abs(item.ZPos - subZ) < 1.6f)
            {
                // Lane alignment check
                if (item.Lane == _currentLane && Mathf.Abs(_currentX - xPos) < 1.4f)
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
        if (_redVignette != null)      _redVignette.color = new Color(1f, 0.05f, 0.05f, 0.55f);
        if (customRedVignette != null) customRedVignette.color = new Color(1f, 0.05f, 0.05f, 0.55f);

        Vector3 origCamPos = _stageCamera != null ? _stageCamera.transform.localPosition : Vector3.zero;
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_stageCamera != null)
            {
                Vector2 shake = UnityEngine.Random.insideUnitCircle * 0.45f;
                _stageCamera.transform.localPosition = origCamPos + new Vector3(shake.x, 0f, shake.y);
            }
            yield return null;
        }

        if (_stageCamera != null)      _stageCamera.transform.localPosition = origCamPos;
        if (_redVignette != null)      _redVignette.color = new Color(1f, 0.05f, 0.05f, 0f);
        if (customRedVignette != null) customRedVignette.color = new Color(1f, 0.05f, 0.05f, 0f);
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

            if (customResultBanner != null)
            {
                customResultBanner.gameObject.SetActive(true);
                customResultBanner.text = $"HAZARD ZONE CLEARED!\n+{totalBonus} RDP";
            }
            else if (_resultBanner != null)
            {
                _resultBanner.gameObject.SetActive(true);
                _resultBanner.text = $"<b>HAZARD ZONE CLEARED!</b>\n<color=#ffdd00>+{totalBonus} RDP</color>";
                _resultBanner.color = new Color(0.2f, 0.95f, 0.85f);
            }

            GameManager.Instance?.AddRDP(totalBonus);
            StartCoroutine(DelayedClose(true, totalBonus));
        }
        else
        {
            if (customResultBanner != null)
            {
                customResultBanner.gameObject.SetActive(true);
                customResultBanner.text = "HULL COMPROMISED!\n-50 RDP";
            }
            else if (_resultBanner != null)
            {
                _resultBanner.gameObject.SetActive(true);
                _resultBanner.text = "<b>HULL COMPROMISED!</b>\n<color=#ff4444>-50 RDP</color>";
                _resultBanner.color = new Color(1f, 0.3f, 0.3f);
            }

            // Deduct 50 RDP (clamped to 0 minimum)
            GameManager.Instance?.DeductRDP(50);
            StartCoroutine(DelayedClose(false, 0));
        }
    }

    private IEnumerator DelayedClose(bool success, int totalReward)
    {
        yield return new WaitForSecondsRealtime(2.2f);

        CleanupStage();
        if (_rootUI != null) _rootUI.gameObject.SetActive(false);
        if (customUIRoot != null) customUIRoot.SetActive(false);
        if (_warningBanner != null) _warningBanner.SetActive(false);
        if (customWarningBanner != null) customWarningBanner.SetActive(false);
        if (_resultBanner != null) _resultBanner.gameObject.SetActive(false);
        if (customResultBanner != null) customResultBanner.gameObject.SetActive(false);

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
                bool isIntact = i < _remainingHits;
                _hullPipImages[i].color = isIntact ? new Color(0.2f, 0.95f, 1f, 0.95f) : new Color(0.6f, 0.1f, 0.1f, 0.35f);
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
                bool isIntact = i < _remainingHits;
                customHullPipImages[i].color = isIntact ? new Color(0.2f, 0.95f, 1.0f, 1.0f) : new Color(0.35f, 0.1f, 0.1f, 0.3f);
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

        // 1. RenderTexture & Camera (Framed for lower-third submarine view)
        if (_stageRT == null)
        {
            _stageRT = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32)
            {
                name = "HazardDodge_RT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            _stageRT.Create();
        }

        var camGO = new GameObject("StageCamera", typeof(Camera));
        camGO.transform.SetParent(_stageRoot.transform, false);
        _stageCamera = camGO.GetComponent<Camera>();
        _stageCamera.transform.localPosition = new Vector3(0f, 16f, -10f);
        _stageCamera.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);
        _stageCamera.clearFlags = CameraClearFlags.SolidColor;
        _stageCamera.backgroundColor = new Color(0.02f, 0.08f, 0.16f, 1f);
        _stageCamera.fieldOfView = 48f;
        _stageCamera.targetTexture = _stageRT;

        // 2. Directional Key Light
        var lightGO = new GameObject("StageLight", typeof(Light));
        lightGO.transform.SetParent(_stageRoot.transform, false);
        lightGO.transform.localRotation = Quaternion.Euler(50f, -25f, 0f);
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.color = new Color(0.8f, 0.95f, 1f);

        // 3. Lane Track / Seafloor Trench Plane
        var trenchGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        trenchGO.name = "TrenchFloor";
        trenchGO.transform.SetParent(_stageRoot.transform, false);
        trenchGO.transform.localPosition = new Vector3(0f, -0.6f, 16f);
        trenchGO.transform.localScale = new Vector3(2.5f, 1f, 7.0f);
        var trenchRend = trenchGO.GetComponent<Renderer>();
        trenchRend.material = CreateLitMaterial(new Color(0.03f, 0.09f, 0.15f));
        Destroy(trenchGO.GetComponent<Collider>());

        // Glowing lane divider lines
        CreateLaneLine(-laneWidth * 0.5f);
        CreateLaneLine(laneWidth * 0.5f);

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

    private void CreateLaneLine(float xOffset)
    {
        var lineGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineGO.name = "LaneLine";
        lineGO.transform.SetParent(_stageRoot.transform, false);
        lineGO.transform.localPosition = new Vector3(xOffset, -0.55f, 16f);
        lineGO.transform.localScale = new Vector3(0.08f, 0.05f, 60f);
        var rend = lineGO.GetComponent<Renderer>();
        rend.material = CreateUnlitMaterial(new Color(0.15f, 0.70f, 0.95f, 0.40f));
        Destroy(lineGO.GetComponent<Collider>());
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
        hull.transform.localScale = new Vector3(1.4f, 2.2f, 1.4f);
        hull.GetComponent<Renderer>().material = CreateLitMaterial(new Color(1.0f, 0.80f, 0.05f)); // Bright Exploration Yellow
        Destroy(hull.GetComponent<Collider>());

        // Conning Tower / Cockpit Dome
        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.transform.SetParent(subRoot.transform, false);
        dome.transform.localPosition = new Vector3(0f, 0.55f, 0.4f);
        dome.transform.localScale = new Vector3(0.9f, 0.7f, 1.1f);
        dome.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.1f, 0.9f, 1.0f)); // Glowing Cyan Glass
        Destroy(dome.GetComponent<Collider>());

        // Left & Right Stabilizer Fins
        CreateFin(subRoot.transform, new Vector3(-1.15f, 0f, -0.6f), new Vector3(0.9f, 0.1f, 0.6f));
        CreateFin(subRoot.transform, new Vector3(1.15f, 0f, -0.6f), new Vector3(0.9f, 0.1f, 0.6f));

        // Twin Thruster Glow Cones (Cyan Engine Glow)
        CreateEngineGlow(subRoot.transform, new Vector3(-0.45f, 0f, -1.3f));
        CreateEngineGlow(subRoot.transform, new Vector3(0.45f, 0f, -1.3f));

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
        glow.transform.localScale = new Vector3(0.35f, 0.35f, 0.5f);
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
        cone.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
        cone.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.18f, 0.12f, 0.08f));
        Destroy(cone.GetComponent<Collider>());

        // Glowing Core Plume (Sphere)
        var plume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        plume.transform.SetParent(go.transform, false);
        plume.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        plume.transform.localScale = new Vector3(1.2f, 1.6f, 1.2f);
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
        ring.transform.localScale = new Vector3(2.2f, 0.05f, 2.2f);
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
        rock.transform.localScale = new Vector3(1.6f, 1.3f, 1.5f);
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
        diamond.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        diamond.GetComponent<Renderer>().material = CreateUnlitMaterial(new Color(1.0f, 0.82f, 0.15f, 0.95f));
        Destroy(diamond.GetComponent<Collider>());

        return go;
    }

    private Material CreateLitMaterial(Color col)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(s);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        return m;
    }

    private Material CreateUnlitMaterial(Color col)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var m = new Material(s);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        return m;
    }

    private void CleanupStage()
    {
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
    }

    // -----------------------------------------------------------------------
    // UI Construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        if (_rootUI != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

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

        // Phase 1 Warning Banner (Fallback overlay on Canvas)
        _warningBanner = new GameObject("WarningBanner", typeof(RectTransform), typeof(Image));
        _warningBanner.transform.SetParent(canvas.transform, false);
        _warningBanner.transform.SetAsLastSibling();
        var warnRect = _warningBanner.GetComponent<RectTransform>();
        warnRect.anchorMin = new Vector2(0.5f, 0.55f);
        warnRect.anchorMax = new Vector2(0.5f, 0.55f);
        warnRect.pivot = new Vector2(0.5f, 0.5f);
        warnRect.sizeDelta = new Vector2(740f, 120f);
        _warningBanner.GetComponent<Image>().color = new Color(0.85f, 0.12f, 0.12f, 0.90f);

        var warnTextGO = new GameObject("WarnText", typeof(RectTransform));
        warnTextGO.transform.SetParent(_warningBanner.transform, false);
        var wtRect = warnTextGO.GetComponent<RectTransform>();
        wtRect.anchorMin = Vector2.zero;
        wtRect.anchorMax = Vector2.one;
        var wt = warnTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) wt.font = font;
        wt.fontSize = 36;
        wt.fontStyle = FontStyles.Bold;
        wt.alignment = TextAlignmentOptions.Center;
        wt.color = Color.white;
        wt.text = "⚠️ ENTERING HAZARD ZONE ⚠️\n<size=24>SWIPE TO EVADE INCOMING OBSTACLES</size>";
        _warningBanner.SetActive(false);

        // Result Banner (Win / Fail) (Fallback overlay on Canvas)
        var resGO = new GameObject("ResultBanner", typeof(RectTransform));
        resGO.transform.SetParent(canvas.transform, false);
        resGO.transform.SetAsLastSibling();
        var resRect = resGO.GetComponent<RectTransform>();
        resRect.anchorMin = new Vector2(0.5f, 0.5f);
        resRect.anchorMax = new Vector2(0.5f, 0.5f);
        resRect.pivot = new Vector2(0.5f, 0.5f);
        resRect.sizeDelta = new Vector2(740f, 140f);
        _resultBanner = resGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _resultBanner.font = font;
        _resultBanner.fontSize = 36;
        _resultBanner.fontStyle = FontStyles.Bold;
        _resultBanner.alignment = TextAlignmentOptions.Center;
        _resultBanner.text = "";
        _resultBanner.gameObject.SetActive(false);

        // Left/Right Touch Tap Buttons (for direct clicking/tapping)
        CreateLaneButton("LeftBtn", new Vector2(0f, 0f), new Vector2(0.5f, 0.6f), -1);
        CreateLaneButton("RightBtn", new Vector2(0.5f, 0f), new Vector2(1f, 0.6f), 1);

        _rootUI.gameObject.SetActive(false);
    }

    private void CreateLaneButton(string name, Vector2 minAnchor, Vector2 maxAnchor, int dir)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(_rootUI, false);
        var r = btnGO.GetComponent<RectTransform>();
        r.anchorMin = minAnchor;
        r.anchorMax = maxAnchor;
        r.sizeDelta = Vector2.zero;
        btnGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // Invisible touch detector
        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(() => ShiftLane(dir));
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
