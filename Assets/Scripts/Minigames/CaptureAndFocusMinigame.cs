using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minigame 1 — Capture and Focus (Responsive mobile & desktop scanner).
///
/// Controls:
///   - Touch/Click & Hold anywhere on screen: Raises the green focus bar.
///   - Release: Gravity smoothly drops the green focus bar downward.
///   - Objective: Keep the moving creature dot inside the green bar to fill the Lock-On Meter.
/// </summary>
public class CaptureAndFocusMinigame : MonoBehaviour
{
    private SpeciesData _data;
    private int         _zoneIndex;
    private Action      _onSuccess;
    private Action      _onFail;
    private int         _difficulty;

    // Difficulty tables (creature movement agility per zone/difficulty)
    private static readonly float[] CreatureSpeed = { 0.08f, 0.15f, 0.24f, 0.35f, 0.50f };
    private static readonly float[] CreaturePower = { 0.22f, 0.35f, 0.48f, 0.62f, 0.78f };

    private const float BaseZoneSize = 0.26f;   // 26% of bar height
    private const float ZonePerTier  = 0.06f;   // +6% per Scanner Tier upgrade
    private const float RiseSpeed    = 0.88f;   // Snappy upward movement when holding
    private const float FallSpeed    = 0.58f;   // Smooth downward drop on release
    private const float FillRate     = 0.30f;   // Lock-on fill speed
    private const float DrainRate    = 0.14f;   // Balanced drain rate when outside zone

    private float _zonePos        = 0.5f;  // [0..1] position of green zone
    private float _creaturePos    = 0.5f;  // [0..1] creature position
    private float _creatureVel    = 0f;
    private float _creatureTarget = 0.5f;
    private float _targetTimer    = 0f;
    private float _meter          = 0.40f; // [0..1] catch progress (starts at 40%)
    private float _startGraceTimer= 0.8f;  // Grace period at start
    private bool  _holding        = false;
    private bool  _finished       = false;

    // -- UI References --------------------------------------------------------
    private RectTransform _rootPanel;
    private RectTransform _mainBarRect;
    private RectTransform _greenZoneRect;
    private Image         _greenZoneImage;
    private RectTransform _creatureDotRect;
    private RectTransform _progressBarRect;
    private Image         _progressFill;
    private Image         _redFlash;
    private TMP_Text      _resultText;
    private float         _barH;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Initialize(SpeciesData data, int zoneIndex, Action onSuccess, Action onFail)
    {
        _data       = data;
        _zoneIndex  = Mathf.Clamp(zoneIndex, 0, 4);
        _onSuccess  = onSuccess;
        _onFail     = onFail;
        _difficulty = (data != null && data.scanDifficulty > 0)
            ? Mathf.Clamp(data.scanDifficulty - 1, 0, 4)
            : _zoneIndex;
    }

    public void Show()
    {
        BuildUI();
        _zonePos         = 0.5f;
        _creaturePos     = 0.5f;
        _creatureVel     = 0f;
        _creatureTarget  = 0.5f;
        _targetTimer     = 0.5f;
        _meter           = 0.40f;
        _startGraceTimer = 0.8f;
        _holding         = false;
        _finished        = false;

        if (_resultText != null) _resultText.gameObject.SetActive(false);
        if (_rootPanel != null)  _rootPanel.gameObject.SetActive(true);

        int tier = GameManager.Instance != null ? GameManager.Instance.ScannerTier : 0;
        float zoneSize = BaseZoneSize + tier * ZonePerTier;
        UpdateVisuals(zoneSize * 0.5f, true);
    }

    public void SetHolding(bool isHolding)
    {
        _holding = isHolding;
    }

    // -----------------------------------------------------------------------
    // Main Simulation Loop
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_finished || _rootPanel == null || !_rootPanel.gameObject.activeSelf) return;

        float dt = Time.deltaTime;
        int   d  = _difficulty;

        if (_startGraceTimer > 0f)
            _startGraceTimer -= dt;

        // 1. Universal input checking (New Input System, Pointer, Touchscreen, Mouse, Keyboard)
        bool pointerPress = Pointer.current != null && Pointer.current.press.isPressed;
        bool touchPress   = Touchscreen.current != null && Touchscreen.current.touches.Any(t => t.press.isPressed);
        bool mousePress   = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool keyPress     = Keyboard.current != null && (Keyboard.current.spaceKey.isPressed || Keyboard.current.fKey.isPressed);

        bool holding = _holding || pointerPress || touchPress || mousePress || keyPress;

        // 2. Move green focus zone
        _zonePos += holding ? RiseSpeed * dt : -FallSpeed * dt;
        _zonePos  = Mathf.Clamp01(_zonePos);

        // 3. Creature autonomous movement
        _targetTimer -= dt;
        if (_targetTimer <= 0f)
        {
            float r = CreaturePower[d] * 0.5f;
            _creatureTarget = Mathf.Clamp(UnityEngine.Random.Range(0.5f - r, 0.5f + r), 0.08f, 0.92f);
            _targetTimer    = UnityEngine.Random.Range(1.2f, 2.8f);
        }

        float spring = CreatureSpeed[d] * 5.5f;
        _creatureVel += (_creatureTarget - _creaturePos) * spring * dt;
        _creatureVel -= _creatureVel * 4.0f * dt;
        _creaturePos += _creatureVel * dt;
        _creaturePos  = Mathf.Clamp01(_creaturePos);

        // 4. Calculate zone alignment
        int   tier     = GameManager.Instance != null ? GameManager.Instance.ScannerTier : 0;
        float zoneSize = BaseZoneSize + tier * ZonePerTier;
        float halfZone = zoneSize * 0.5f;
        bool  inZone   = Mathf.Abs(_zonePos - _creaturePos) <= halfZone;

        // 5. Update lock-on meter
        if (inZone)
        {
            _meter += FillRate * dt;
        }
        else if (_startGraceTimer <= 0f)
        {
            _meter -= DrainRate * dt;
        }
        _meter = Mathf.Clamp01(_meter);

        UpdateVisuals(halfZone, inZone);

        // 6. Win / Loss triggers
        if (_meter >= 1f)
        {
            Finish(true);
        }
        else if (_meter <= 0f && _startGraceTimer <= 0f)
        {
            Finish(false);
        }
    }

    private void UpdateVisuals(float halfZone, bool inZone)
    {
        if (_barH <= 0f) return;

        // Position green focus zone
        if (_greenZoneRect != null)
        {
            float cy = (_zonePos - 0.5f) * _barH;
            float h  = halfZone * 2f * _barH;
            _greenZoneRect.anchoredPosition = new Vector2(0f, cy);
            _greenZoneRect.sizeDelta        = new Vector2(_greenZoneRect.sizeDelta.x, Mathf.Max(h, 24f));
        }

        // Position creature dot
        if (_creatureDotRect != null)
            _creatureDotRect.anchoredPosition = new Vector2(0f, (_creaturePos - 0.5f) * _barH);

        // Zone glow
        if (_greenZoneImage != null)
            _greenZoneImage.color = inZone
                ? new Color(0.12f, 0.95f, 0.48f, 0.90f)
                : new Color(0.14f, 0.62f, 0.32f, 0.60f);

        // Progress bar fill
        if (_progressFill != null)
        {
            _progressFill.fillAmount = _meter;
            if      (_meter > 0.66f) _progressFill.color = new Color(0.15f, 0.92f, 0.42f, 1f);
            else if (_meter > 0.33f) _progressFill.color = new Color(0.95f, 0.85f, 0.12f, 1f);
            else                     _progressFill.color = new Color(0.95f, 0.25f, 0.15f, 1f);
        }

        // Red disturbance warning flash when meter is low
        if (_redFlash != null)
        {
            float a = _meter < 0.25f ? Mathf.PingPong(Time.time * 4f, 1f) * 0.28f : 0f;
            _redFlash.color = new Color(1f, 0.08f, 0.08f, a);
        }
    }

    private void Finish(bool success)
    {
        if (_finished) return;
        _finished = true;

        if (_resultText != null)
        {
            _resultText.gameObject.SetActive(true);
            _resultText.text  = success ? "<b>RESEARCH SCAN COMPLETE!</b>" : "<b>TARGET LOST</b>";
            _resultText.color = success ? new Color(0.20f, 0.92f, 0.80f) : new Color(1f, 0.30f, 0.30f);
        }

        StartCoroutine(DelayedClose(success));
    }

    private IEnumerator DelayedClose(bool success)
    {
        yield return new WaitForSeconds(1.2f);
        if (_rootPanel != null) _rootPanel.gameObject.SetActive(false);

        if (success) _onSuccess?.Invoke();
        else         _onFail?.Invoke();
    }

    // -----------------------------------------------------------------------
    // UI Construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        if (_rootPanel != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Fullscreen touch capture panel
        var rootGO = new GameObject("CaptureMinigame",
            typeof(RectTransform), typeof(Image), typeof(GraphicRaycaster));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootPanel = rootGO.GetComponent<RectTransform>();
        _rootPanel.anchorMin = Vector2.zero; _rootPanel.anchorMax = Vector2.one; _rootPanel.sizeDelta = Vector2.zero;

        var rootImage = rootGO.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.45f);
        rootImage.raycastTarget = true;

        // Direct Touch & Click Event Listener on the panel
        var touchHandler = rootGO.AddComponent<CapturePointerReceiver>();
        touchHandler.Initialize(this);

        // Red flash border
        var flashGO = new GameObject("RedFlash", typeof(RectTransform), typeof(Image));
        flashGO.transform.SetParent(_rootPanel, false);
        Stretch(flashGO.GetComponent<RectTransform>());
        _redFlash = flashGO.GetComponent<Image>();
        _redFlash.color = new Color(1f, 0.08f, 0.08f, 0f);
        _redFlash.raycastTarget = false;

        // Dimensions
        var cr = canvas.GetComponent<RectTransform>();
        float canvasH  = cr != null ? cr.rect.height : 600f;
        float mainW    = 52f;
        float progW    = 18f;
        float gap      = 8f;
        _barH          = canvasH * 0.72f;

        float totalW   = mainW + gap + progW;
        float startX   = -totalW * 0.5f;

        // Main Bar (Zone + Creature Dot)
        var mainBarGO = new GameObject("MainBar", typeof(RectTransform), typeof(Image));
        mainBarGO.transform.SetParent(_rootPanel, false);
        _mainBarRect = mainBarGO.GetComponent<RectTransform>();
        _mainBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _mainBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _mainBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _mainBarRect.sizeDelta        = new Vector2(mainW, _barH);
        _mainBarRect.anchoredPosition = new Vector2(startX + mainW * 0.5f, 0f);
        mainBarGO.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.95f);
        mainBarGO.GetComponent<Image>().raycastTarget = false;

        // Green Zone
        float initZoneH = BaseZoneSize * _barH;
        var zoneGO = new GameObject("GreenZone", typeof(RectTransform), typeof(Image));
        zoneGO.transform.SetParent(_mainBarRect, false);
        _greenZoneRect = zoneGO.GetComponent<RectTransform>();
        _greenZoneRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _greenZoneRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _greenZoneRect.pivot            = new Vector2(0.5f, 0.5f);
        _greenZoneRect.sizeDelta        = new Vector2(mainW * 0.90f, initZoneH);
        _greenZoneRect.anchoredPosition = Vector2.zero;
        _greenZoneImage = zoneGO.GetComponent<Image>();
        _greenZoneImage.color = new Color(0.12f, 0.95f, 0.48f, 0.85f);
        _greenZoneImage.raycastTarget = false;

        // Creature Dot
        var dotGO = new GameObject("CreatureDot", typeof(RectTransform), typeof(Image));
        dotGO.transform.SetParent(_mainBarRect, false);
        _creatureDotRect = dotGO.GetComponent<RectTransform>();
        _creatureDotRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _creatureDotRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _creatureDotRect.pivot            = new Vector2(0.5f, 0.5f);
        _creatureDotRect.sizeDelta        = new Vector2(24f, 24f);
        _creatureDotRect.anchoredPosition = Vector2.zero;
        Color dotCol = _data != null ? _data.placeholderColor : new Color(1f, 0.40f, 0.15f);
        dotGO.GetComponent<Image>().color = dotCol;
        dotGO.GetComponent<Image>().raycastTarget = false;

        // Progress Bar
        var progBarGO = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
        progBarGO.transform.SetParent(_rootPanel, false);
        _progressBarRect = progBarGO.GetComponent<RectTransform>();
        _progressBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _progressBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _progressBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _progressBarRect.sizeDelta        = new Vector2(progW, _barH);
        _progressBarRect.anchoredPosition = new Vector2(startX + mainW + gap + progW * 0.5f, 0f);
        progBarGO.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.95f);
        progBarGO.GetComponent<Image>().raycastTarget = false;

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(_progressBarRect, false);
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
        _progressFill = fillGO.GetComponent<Image>();
        _progressFill.type       = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Vertical;
        _progressFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        _progressFill.fillAmount = 0.40f;
        _progressFill.color      = new Color(0.15f, 0.92f, 0.42f, 1f);
        _progressFill.raycastTarget = false;

        // Instruction label
        var instrGO = new GameObject("Instr", typeof(RectTransform));
        instrGO.transform.SetParent(_rootPanel, false);
        var ir = instrGO.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.04f); ir.anchorMax = new Vector2(0.80f, 0.12f); ir.sizeDelta = Vector2.zero;
        var it = instrGO.AddComponent<TextMeshProUGUI>();
        it.text = "<b>HOLD SCREEN</b> to raise focus bar  •  Keep creature in the green";
        it.fontSize = 13; it.color = new Color(0.85f, 0.95f, 0.85f, 0.90f);
        it.alignment = TextAlignmentOptions.Center; it.raycastTarget = false;

        // Result label
        var resultGO = new GameObject("Result", typeof(RectTransform));
        resultGO.transform.SetParent(_rootPanel, false);
        var rr = resultGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.1f, 0.44f); rr.anchorMax = new Vector2(0.80f, 0.58f); rr.sizeDelta = Vector2.zero;
        _resultText = resultGO.AddComponent<TextMeshProUGUI>();
        _resultText.fontSize = 24; _resultText.alignment = TextAlignmentOptions.Center;
        _resultText.raycastTarget = false;
        _resultText.gameObject.SetActive(false);

        _rootPanel.gameObject.SetActive(false);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
    }
}

/// <summary>
/// Touch receiver attached to the fullscreen capture panel.
/// </summary>
public class CapturePointerReceiver : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private CaptureAndFocusMinigame _minigame;

    public void Initialize(CaptureAndFocusMinigame minigame)
    {
        _minigame = minigame;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_minigame != null) _minigame.SetHolding(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_minigame != null) _minigame.SetHolding(false);
    }
}