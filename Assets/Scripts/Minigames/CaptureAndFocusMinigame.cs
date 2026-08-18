using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minigame 1 - Capture and Focus (Stardew Valley fishing rod style).
///
/// Layout (two bars side by side):
///   LEFT bar (wide)  - MAIN BAR:
///       Green floating zone + creature dot both live here.
///       Hold screen -> zone rises. Release -> zone falls.
///       Creature bounces autonomously (smooth spring).
///       In-zone when creature dot is inside the green zone.
///   RIGHT bar (thin) - PROGRESS BAR:
///       Fills from bottom as the meter rises (creature in zone).
///       Drains when creature is outside.
///       Like the catch progress bar in Stardew Valley.
///
/// Zone size: 20% base + 5% per Scanner Tier upgrade.
/// Per-species difficulty: SpeciesData.scanDifficulty 0=auto, 1-5 override.
/// </summary>
public class CaptureAndFocusMinigame : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private SpeciesData _data;
    private int         _zoneIndex;
    private Action      _onSuccess;
    private Action      _onFail;
    private int         _difficulty;

    // Difficulty tables (creature movement only � zone size is controlled by upgrades)
    private static readonly float[] CreatureSpeed = { 0.10f, 0.18f, 0.28f, 0.40f, 0.58f };
    private static readonly float[] CreaturePower = { 0.28f, 0.42f, 0.58f, 0.70f, 0.85f };

    private const float BaseZoneSize = 0.20f;   // 20% of bar height
    private const float ZonePerTier  = 0.05f;   // +5% per Scanner Tier
    private const float RiseSpeed    = 0.68f;
    private const float FallSpeed    = 0.48f;
    private const float FillRate     = 0.28f;
    private const float DrainRate    = 0.18f;

    private float _zonePos       = 0.5f;  // [0..1] centre of green zone (player-controlled)
    private float _creaturePos   = 0.5f;  // [0..1] creature autonomous position
    private float _creatureVel   = 0f;
    private float _creatureTarget= 0.5f;
    private float _targetTimer   = 0f;
    private float _meter         = 0.5f;  // [0..1] catch progress
    private bool  _holding       = false;
    private bool  _finished      = false;

    // -- UI refs --------------------------------------------------------------
    private RectTransform _rootPanel;

    // LEFT bar (main)
    private RectTransform _mainBarRect;
    private RectTransform _greenZoneRect;    // floating green zone
    private Image         _greenZoneImage;
    private RectTransform _creatureDotRect;  // creature bouncing dot

    // RIGHT bar (progress fill)
    private RectTransform _progressBarRect;
    private Image         _progressFill;     // fills from bottom

    // Result overlay
    private Image    _redFlash;
    private TMP_Text _resultText;
    private float    _barH;

    // -----------------------------------------------------------------------
    public void Initialize(SpeciesData data, int zoneIndex, Action onSuccess, Action onFail)
    {
        _data      = data;
        _zoneIndex = Mathf.Clamp(zoneIndex, 0, 4);
        _onSuccess = onSuccess;
        _onFail    = onFail;
        _difficulty = (data != null && data.scanDifficulty > 0)
            ? Mathf.Clamp(data.scanDifficulty - 1, 0, 4)
            : _zoneIndex;
    }

    public void Show()
    {
        BuildUI();
        _zonePos        = 0.5f;
        _creaturePos    = 0.5f;
        _creatureVel    = 0f;
        _creatureTarget = 0.5f;
        _targetTimer    = 0f;
        _meter          = 0.5f;
        _holding        = false;
        _finished       = false;
        if (_rootPanel != null) _rootPanel.gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData e) => _holding = true;
    public void OnPointerUp  (PointerEventData e) => _holding = false;

    // -----------------------------------------------------------------------
    private void Update()
    {
        if (_finished || _rootPanel == null || !_rootPanel.gameObject.activeSelf) return;

        float dt = Time.deltaTime;
        int   d  = _difficulty;

        // Player raises/lowers zone
        bool holdMouse = Mouse.current      != null && Mouse.current.leftButton.isPressed;
        bool holdTouch = Touchscreen.current!= null && Touchscreen.current.primaryTouch.press.isPressed;
        bool holding   = _holding || holdMouse || holdTouch;
        _zonePos += holding ? RiseSpeed * dt : -FallSpeed * dt;
        _zonePos  = Mathf.Clamp01(_zonePos);

        // Creature spring movement
        _targetTimer -= dt;
        if (_targetTimer <= 0f)
        {
            float r = CreaturePower[d] * 0.5f;
            _creatureTarget = Mathf.Clamp(UnityEngine.Random.Range(0.5f - r, 0.5f + r), 0.06f, 0.94f);
            _targetTimer    = UnityEngine.Random.Range(1.4f, 3.2f);
        }
        float spring = CreatureSpeed[d] * 6f;
        _creatureVel += (_creatureTarget - _creaturePos) * spring * dt;
        _creatureVel -= _creatureVel * 4.5f * dt;
        _creaturePos += _creatureVel * dt;
        _creaturePos  = Mathf.Clamp01(_creaturePos);

        // Zone size
        int   tier     = GameManager.Instance != null ? GameManager.Instance.ScannerTier : 0;
        float zoneSize = BaseZoneSize + tier * ZonePerTier;
        float halfZone = zoneSize * 0.5f;
        bool  inZone   = Mathf.Abs(_zonePos - _creaturePos) <= halfZone;

        // Meter
        _meter += (inZone ? FillRate : -DrainRate) * dt;
        _meter  = Mathf.Clamp01(_meter);

        UpdateVisuals(halfZone, inZone);

        if      (_meter >= 1f) Finish(true);
        else if (_meter <= 0f) Finish(false);
    }

    // -----------------------------------------------------------------------
    private void UpdateVisuals(float halfZone, bool inZone)
    {
        if (_barH <= 0f) return;

        // Green zone: floats up/down on main bar � centred at _zonePos
        if (_greenZoneRect != null)
        {
            float cy = (_zonePos - 0.5f) * _barH;
            float h  = halfZone * 2f * _barH;
            _greenZoneRect.anchoredPosition = new Vector2(0f, cy);
            _greenZoneRect.sizeDelta        = new Vector2(_greenZoneRect.sizeDelta.x, Mathf.Max(h, 18f));
        }

        // Creature dot: moves on main bar
        if (_creatureDotRect != null)
            _creatureDotRect.anchoredPosition = new Vector2(0f, (_creaturePos - 0.5f) * _barH);

        // Green zone color: bright when in zone, dim when not
        if (_greenZoneImage != null)
            _greenZoneImage.color = inZone
                ? new Color(0.10f, 0.92f, 0.45f, 0.90f)
                : new Color(0.14f, 0.62f, 0.32f, 0.65f);

        // Progress bar: fills from bottom
        if (_progressFill != null)
            _progressFill.fillAmount = _meter;

        // Color progress bar: green when high, yellow when mid, red when low
        if (_progressFill != null)
        {
            Color progressCol;
            if      (_meter > 0.66f) progressCol = new Color(0.15f, 0.90f, 0.40f, 1f);
            else if (_meter > 0.33f) progressCol = new Color(0.95f, 0.85f, 0.10f, 1f);
            else                     progressCol = new Color(0.95f, 0.25f, 0.15f, 1f);
            _progressFill.color = progressCol;
        }

        // Red flash
        if (_redFlash != null)
        {
            float a = _meter < 0.25f ? Mathf.PingPong(Time.time * 4f, 1f) * 0.28f : 0f;
            _redFlash.color = new Color(1f, 0.08f, 0.08f, a);
        }
    }

    // -----------------------------------------------------------------------
    private void Finish(bool success)
    {
        if (_finished) return;
        _finished = true;
        if (_resultText != null)
        {
            _resultText.gameObject.SetActive(true);
            _resultText.text  = success ? "<b>LOCKED ON!</b>" : "<b>LOST TARGET</b>";
            _resultText.color = success ? new Color(0.20f, 0.90f, 0.78f) : new Color(1f, 0.30f, 0.30f);
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
    private void BuildUI()
    {
        if (_rootPanel != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Root fullscreen dim (tap zone)
        var rootGO = new GameObject("CaptureMinigame",
            typeof(RectTransform), typeof(Image), typeof(GraphicRaycaster));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootPanel = rootGO.GetComponent<RectTransform>();
        _rootPanel.anchorMin = Vector2.zero; _rootPanel.anchorMax = Vector2.one; _rootPanel.sizeDelta = Vector2.zero;
        rootGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.48f);
        rootGO.GetComponent<Image>().raycastTarget = true;

        // Red flash
        var flashGO = new GameObject("RedFlash", typeof(RectTransform), typeof(Image));
        flashGO.transform.SetParent(_rootPanel, false);
        Stretch(flashGO.GetComponent<RectTransform>());
        _redFlash = flashGO.GetComponent<Image>();
        _redFlash.color = new Color(1f, 0.08f, 0.08f, 0f);
        _redFlash.raycastTarget = false;

        // Dimensions
        var cr = canvas.GetComponent<RectTransform>();
        float canvasH  = cr != null ? cr.rect.height : 600f;
        float mainW    = 50f;   // main bar width
        float progW    = 18f;   // progress bar width (thin)
        float gap      = 6f;
        _barH          = canvasH * 0.72f;

        float totalW   = mainW + gap + progW;
        float startX   = -totalW * 0.5f;  // pivot at centre

        // -- LEFT (MAIN) BAR � zone + creature -------------------------------
        var mainBarGO = new GameObject("MainBar", typeof(RectTransform), typeof(Image));
        mainBarGO.transform.SetParent(_rootPanel, false);
        _mainBarRect = mainBarGO.GetComponent<RectTransform>();
        _mainBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _mainBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _mainBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _mainBarRect.sizeDelta        = new Vector2(mainW, _barH);
        _mainBarRect.anchoredPosition = new Vector2(startX + mainW * 0.5f, 0f);
        mainBarGO.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 0.96f);
        mainBarGO.GetComponent<Image>().raycastTarget = false;

        // Green zone rect (floating, positioned by UpdateVisuals)
        float initZoneH = BaseZoneSize * _barH;
        var zoneGO = new GameObject("GreenZone", typeof(RectTransform), typeof(Image));
        zoneGO.transform.SetParent(_mainBarRect, false);
        _greenZoneRect = zoneGO.GetComponent<RectTransform>();
        _greenZoneRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _greenZoneRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _greenZoneRect.pivot            = new Vector2(0.5f, 0.5f);
        _greenZoneRect.sizeDelta        = new Vector2(mainW * 0.88f, initZoneH);
        _greenZoneRect.anchoredPosition = Vector2.zero;
        _greenZoneImage = zoneGO.GetComponent<Image>();
        _greenZoneImage.color = new Color(0.14f, 0.62f, 0.32f, 0.65f);
        _greenZoneImage.raycastTarget = false;

        // Creature dot (on top of zone so always visible)
        var dotGO = new GameObject("CreatureDot", typeof(RectTransform), typeof(Image));
        dotGO.transform.SetParent(_mainBarRect, false);
        _creatureDotRect = dotGO.GetComponent<RectTransform>();
        _creatureDotRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _creatureDotRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _creatureDotRect.pivot            = new Vector2(0.5f, 0.5f);
        _creatureDotRect.sizeDelta        = new Vector2(22f, 22f);
        _creatureDotRect.anchoredPosition = Vector2.zero;
        Color dotCol = _data != null ? _data.placeholderColor : new Color(1f, 0.35f, 0.15f);
        dotGO.GetComponent<Image>().color = dotCol;
        dotGO.GetComponent<Image>().raycastTarget = false;

        // -- RIGHT (PROGRESS) BAR � catch meter fills from bottom -------------
        var progBarGO = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
        progBarGO.transform.SetParent(_rootPanel, false);
        _progressBarRect = progBarGO.GetComponent<RectTransform>();
        _progressBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _progressBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _progressBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _progressBarRect.sizeDelta        = new Vector2(progW, _barH);
        _progressBarRect.anchoredPosition = new Vector2(startX + mainW + gap + progW * 0.5f, 0f);
        progBarGO.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 0.96f);
        progBarGO.GetComponent<Image>().raycastTarget = false;

        // Fill image inside progress bar
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(_progressBarRect, false);
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
        _progressFill = fillGO.GetComponent<Image>();
        _progressFill.type       = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Vertical;
        _progressFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        _progressFill.fillAmount = 0.5f;
        _progressFill.color      = new Color(0.15f, 0.90f, 0.40f, 1f);
        _progressFill.raycastTarget = false;

        // Instruction label
        var instrGO = new GameObject("Instr", typeof(RectTransform));
        instrGO.transform.SetParent(_rootPanel, false);
        var ir = instrGO.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.04f); ir.anchorMax = new Vector2(0.82f, 0.12f); ir.sizeDelta = Vector2.zero;
        var it = instrGO.AddComponent<TextMeshProUGUI>();
        it.text = "Hold screen to raise zone  -  Keep dot inside the green";
        it.fontSize = 11; it.color = new Color(0.8f, 0.9f, 0.8f, 0.85f);
        it.alignment = TextAlignmentOptions.Center; it.raycastTarget = false;

        // Result label
        var resultGO = new GameObject("Result", typeof(RectTransform));
        resultGO.transform.SetParent(_rootPanel, false);
        var rr = resultGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.1f, 0.44f); rr.anchorMax = new Vector2(0.82f, 0.58f); rr.sizeDelta = Vector2.zero;
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
