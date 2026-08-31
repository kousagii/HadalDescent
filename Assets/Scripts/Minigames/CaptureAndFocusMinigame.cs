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
    private static readonly float[] CreatureSpeed = { 0.16f, 0.26f, 0.38f, 0.52f, 0.70f };
    private static readonly float[] CreaturePower = { 0.36f, 0.50f, 0.64f, 0.78f, 0.90f };

    private const float RiseSpeed     = 0.75f;  // Snappy upward movement when holding
    private const float FallSpeed     = 0.58f;  // Smooth downward drop on release
    private const float BaseFillRate  = 0.30f;  // Lock-on fill speed
    private const float BaseDrainRate = 0.15f;  // Drain rate when outside zone

    private float _zonePos        = 0.5f;  // [halfZone..1-halfZone] strictly clamped inside black container
    private float _creaturePos    = 0.5f;  // [0..1] creature position
    private float _creatureVel    = 0f;
    private float _creatureTarget = 0.5f;
    private float _targetTimer    = 0f;
    private float _meter          = 0.20f; // [0..1] catch progress (starts at 20%)
    private float _startGraceTimer= 0.8f;  // Grace period at start
    private bool  _holding        = false;
    private bool  _finished       = false;

    // Static cached white sprite for uGUI Filled images (prevents solid quad bug)
    private static Sprite _cachedWhiteSprite;
    public static Sprite WhiteSprite
    {
        get
        {
            if (_cachedWhiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return _cachedWhiteSprite;
        }
    }

    // -- UI References --------------------------------------------------------
    private RectTransform _rootPanel;
    private RectTransform _mainBarRect;
    private RectTransform _greenZoneRect;
    private Image         _greenZoneImage;
    private RectTransform _creatureDotRect;
    private RectTransform _progressBarRect;
    private Image         _fluidFillImage;
    private RectTransform _meniscusLineRect;
    private Image         _meniscusLineImage;
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
        int tier = GetScannerTier();
        float zoneSize = GetScannerFocusWidth(tier);
        float halfZone = zoneSize * 0.5f;

        _zonePos         = 0.5f;
        _creaturePos     = 0.5f;
        _creatureVel     = 0f;
        _creatureTarget  = 0.5f;
        _targetTimer     = 0.5f;
        _meter           = 0.20f;
        _startGraceTimer = 0.8f;
        _holding         = false;
        _finished        = false;

        if (_resultText != null) _resultText.gameObject.SetActive(false);
        if (_rootPanel != null)  _rootPanel.gameObject.SetActive(true);

        UpdateVisuals(halfZone, true);
    }

    public void SetHolding(bool isHolding)
    {
        _holding = isHolding;
    }

    private int GetScannerTier()
    {
        if (GameManager.Instance == null) return 1;
        return Mathf.Clamp(GameManager.Instance.ScannerTier, 1, 5);
    }

    /// <summary>
    /// Scanner Spec focus bar width per tier:
    /// Tier 1: 15% (Base), Tier 2: 20%, Tier 3: 25%, Tier 4: 30%, Tier 5: 35%
    /// </summary>
    private float GetScannerFocusWidth(int tier)
    {
        return tier switch
        {
            1 => 0.15f,
            2 => 0.20f,
            3 => 0.25f,
            4 => 0.30f,
            5 => 0.35f,
            _ => 0.15f
        };
    }

    // -----------------------------------------------------------------------
    // Main Simulation Loop
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_finished || _rootPanel == null || !_rootPanel.gameObject.activeSelf) return;

        float dt   = Time.deltaTime;
        int   d    = _difficulty;
        int   tier = GetScannerTier();

        if (_startGraceTimer > 0f)
            _startGraceTimer -= dt;

        // 1. Universal input checking (New Input System, Pointer, Touchscreen, Mouse, Keyboard)
        bool pointerPress = Pointer.current != null && Pointer.current.press.isPressed;
        bool touchPress   = Touchscreen.current != null && Touchscreen.current.touches.Any(t => t.press.isPressed);
        bool mousePress   = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool keyPress     = Keyboard.current != null && (Keyboard.current.spaceKey.isPressed || Keyboard.current.fKey.isPressed);

        bool holding = _holding || pointerPress || touchPress || mousePress || keyPress;

        // 2. Focus bar dimensions & strict boundary clamping
        float zoneSize = GetScannerFocusWidth(tier);
        float halfZone = zoneSize * 0.5f;

        // Move green focus zone — strictly clamped so top and bottom edges never leave the black container
        _zonePos += holding ? RiseSpeed * dt : -FallSpeed * dt;
        _zonePos  = Mathf.Clamp(_zonePos, halfZone, 1.0f - halfZone);

        // 3. Creature autonomous movement
        _targetTimer -= dt;
        float halfDot = (_barH > 0f) ? (12f / _barH) : 0.03f;
        if (_targetTimer <= 0f)
        {
            float r = CreaturePower[d] * 0.5f;
            _creatureTarget = Mathf.Clamp(UnityEngine.Random.Range(0.5f - r, 0.5f + r), halfDot + 0.02f, 1.0f - halfDot - 0.02f);
            _targetTimer    = UnityEngine.Random.Range(0.8f, 1.8f);
        }

        // Scanner Perk: Tier >= 2 provides stabilization (reduces creature jerk / twitch speed by 25%)
        float stabMultiplier = (tier >= 2) ? 0.75f : 1.0f;
        float spring = CreatureSpeed[d] * 5.5f * stabMultiplier;
        _creatureVel += (_creatureTarget - _creaturePos) * spring * dt;
        _creatureVel -= _creatureVel * 4.0f * dt;
        _creaturePos += _creatureVel * dt;
        _creaturePos  = Mathf.Clamp(_creaturePos, halfDot, 1.0f - halfDot);

        // 4. Calculate zone alignment
        bool inZone = Mathf.Abs(_zonePos - _creaturePos) <= halfZone;

        // 5. Update lock-on meter with Tier Perks
        // Tier 1 & 2: Standard lock-on speed (1.0x)
        // Tier 3: +15% fill speed (1.15x)
        // Tier 4 & 5: +30% fill speed (1.30x)
        float fillSpeedMultiplier = 1.0f;
        if (tier == 3) fillSpeedMultiplier = 1.15f;
        else if (tier >= 4) fillSpeedMultiplier = 1.30f;

        // Tier 5: Halves lock-on progress decay when off-target
        float drainMultiplier = (tier >= 5) ? 0.50f : 1.0f;

        if (inZone)
        {
            _meter += (BaseFillRate * fillSpeedMultiplier) * dt;
        }
        else if (_startGraceTimer <= 0f)
        {
            _meter -= (BaseDrainRate * drainMultiplier) * dt;
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

        // Position green focus zone (strictly contained within black bar)
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

        // Dynamic Pouring Liquid Container Fill:
        // Fills from bottom to top (0% to 100%) like water being poured into a container.
        // Color is constant throughout the entire liquid volume based on progress threshold:
        //   - 0.00% - 33.33%: RED (Danger / initial charge)
        //   - 33.33% - 66.66%: ALL ORANGE from bottom to current level (Energy building)
        //   - 66.66% - 100.0%: ALL GREEN from bottom to current level (Locked-on / target scan)
        if (_fluidFillImage != null)
        {
            _fluidFillImage.fillAmount = _meter;

            Color liquidColor;
            if (_meter <= 0.3333f)
            {
                liquidColor = new Color(0.95f, 0.22f, 0.16f, 0.95f); // Red
            }
            else if (_meter <= 0.6666f)
            {
                liquidColor = new Color(0.96f, 0.60f, 0.10f, 0.95f); // All Orange from bottom up
            }
            else
            {
                liquidColor = new Color(0.15f, 0.92f, 0.45f, 0.95f); // All Green from bottom up
            }

            _fluidFillImage.color = liquidColor;

            // Update water surface meniscus cap / wave ripple line
            if (_meniscusLineRect != null && _meniscusLineImage != null)
            {
                if (_meter > 0.01f)
                {
                    _meniscusLineRect.gameObject.SetActive(true);
                    float yPos = (_meter - 0.5f) * _barH;
                    float waveOffset = inZone ? Mathf.Sin(Time.time * 8f) * 1.5f : 0f;
                    _meniscusLineRect.anchoredPosition = new Vector2(0f, yPos + waveOffset);

                    // Slightly brighter highlight tint for the surface meniscus
                    Color highlightCol = Color.Lerp(liquidColor, Color.white, 0.55f);
                    _meniscusLineImage.color = highlightCol;
                }
                else
                {
                    _meniscusLineRect.gameObject.SetActive(false);
                }
            }
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
        float progW    = 22f;
        float gap      = 10f;
        _barH          = canvasH * 0.72f;

        float totalW   = mainW + gap + progW;
        float startX   = -totalW * 0.5f;

        // Main Bar (Zone + Creature Dot)
        var mainBarGO = new GameObject("MainBar", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        mainBarGO.transform.SetParent(_rootPanel, false);
        _mainBarRect = mainBarGO.GetComponent<RectTransform>();
        _mainBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _mainBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _mainBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _mainBarRect.sizeDelta        = new Vector2(mainW, _barH);
        _mainBarRect.anchoredPosition = new Vector2(startX + mainW * 0.5f, 0f);
        mainBarGO.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.95f);
        mainBarGO.GetComponent<Image>().raycastTarget = false;

        // Main Bar subtle border
        var mainBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
        mainBorder.transform.SetParent(_mainBarRect, false);
        Stretch(mainBorder.GetComponent<RectTransform>());
        var mainBorderImg = mainBorder.GetComponent<Image>();
        mainBorderImg.sprite = WhiteSprite;
        mainBorderImg.color = new Color(0.12f, 0.35f, 0.45f, 0.30f);
        mainBorderImg.raycastTarget = false;

        // Green Zone
        int tier = GetScannerTier();
        float initZoneH = GetScannerFocusWidth(tier) * _barH;
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

        // -------------------------------------------------------------------
        // Graduated Liquid Container (Progress Bar)
        // -------------------------------------------------------------------
        var progBarGO = new GameObject("ProgressBarContainer", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        progBarGO.transform.SetParent(_rootPanel, false);
        _progressBarRect = progBarGO.GetComponent<RectTransform>();
        _progressBarRect.anchorMin        = new Vector2(0.88f, 0.5f);
        _progressBarRect.anchorMax        = new Vector2(0.88f, 0.5f);
        _progressBarRect.pivot            = new Vector2(0.5f, 0.5f);
        _progressBarRect.sizeDelta        = new Vector2(progW, _barH);
        _progressBarRect.anchoredPosition = new Vector2(startX + mainW + gap + progW * 0.5f, 0f);

        // Glass background of container
        var bgImg = progBarGO.GetComponent<Image>();
        bgImg.color = new Color(0.03f, 0.06f, 0.10f, 0.95f);
        bgImg.raycastTarget = false;

        // Dynamic Pouring Fluid Fill Image (Vertical bottom-up)
        var fluidGO = new GameObject("FluidFill", typeof(RectTransform), typeof(Image));
        fluidGO.transform.SetParent(_progressBarRect, false);
        var fluidRect = fluidGO.GetComponent<RectTransform>();
        fluidRect.anchorMin = Vector2.zero;
        fluidRect.anchorMax = Vector2.one;
        fluidRect.sizeDelta = Vector2.zero;

        _fluidFillImage = fluidGO.GetComponent<Image>();
        _fluidFillImage.sprite        = WhiteSprite; // Mandatory for uGUI Image.Type.Filled
        _fluidFillImage.type          = Image.Type.Filled;
        _fluidFillImage.fillMethod    = Image.FillMethod.Vertical;
        _fluidFillImage.fillOrigin    = (int)Image.OriginVertical.Bottom;
        _fluidFillImage.fillAmount    = 0.20f;
        _fluidFillImage.color         = new Color(0.95f, 0.22f, 0.16f, 0.95f);
        _fluidFillImage.raycastTarget = false;

        // Meniscus surface wave highlight line
        var meniscusGO = new GameObject("FluidMeniscus", typeof(RectTransform), typeof(Image));
        meniscusGO.transform.SetParent(_progressBarRect, false);
        _meniscusLineRect = meniscusGO.GetComponent<RectTransform>();
        _meniscusLineRect.anchorMin        = new Vector2(0f, 0.5f);
        _meniscusLineRect.anchorMax        = new Vector2(1f, 0.5f);
        _meniscusLineRect.pivot            = new Vector2(0.5f, 0.5f);
        _meniscusLineRect.sizeDelta        = new Vector2(0f, 3.5f);
        _meniscusLineImage                 = meniscusGO.GetComponent<Image>();
        _meniscusLineImage.sprite          = WhiteSprite;
        _meniscusLineImage.color           = Color.white;
        _meniscusLineImage.raycastTarget   = false;

        // 3-Segment Measurement Ticks / Divider Lines on the Container
        CreateContainerDivider("Tick_33", _progressBarRect, 0.3333f);
        CreateContainerDivider("Tick_66", _progressBarRect, 0.6666f);
        CreateContainerDivider("Tick_100", _progressBarRect, 1.0000f);

        // Glass Highlight Sheen overlay on container
        var sheenGO = new GameObject("GlassSheen", typeof(RectTransform), typeof(Image));
        sheenGO.transform.SetParent(_progressBarRect, false);
        var sheenRect = sheenGO.GetComponent<RectTransform>();
        sheenRect.anchorMin = new Vector2(0f, 0f);
        sheenRect.anchorMax = new Vector2(0.35f, 1f);
        sheenRect.sizeDelta = Vector2.zero;
        var sheenImg = sheenGO.GetComponent<Image>();
        sheenImg.sprite = WhiteSprite;
        sheenImg.color = new Color(1f, 1f, 1f, 0.10f);
        sheenImg.raycastTarget = false;

        // Instruction label
        var instrGO = new GameObject("Instr", typeof(RectTransform));
        instrGO.transform.SetParent(_rootPanel, false);
        var ir = instrGO.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.04f); ir.anchorMax = new Vector2(1.0f, 0.12f); ir.sizeDelta = Vector2.zero;
        var it = instrGO.AddComponent<TextMeshProUGUI>();
        it.text = "<b>HOLD SCREEN</b> to raise focus bar  •  Keep creature in the green";
        it.fontSize = 36; it.color = new Color(0.85f, 0.95f, 0.85f, 0.90f);
        it.alignment = TextAlignmentOptions.Center; it.raycastTarget = false;
        it.enableWordWrapping = false;
        it.overflowMode = TextOverflowModes.Overflow;

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

    private void CreateContainerDivider(string name, RectTransform parent, float yNormalized)
    {
        var tickGO = new GameObject(name, typeof(RectTransform), typeof(Image));
        tickGO.transform.SetParent(parent, false);

        var tickRect = tickGO.GetComponent<RectTransform>();
        tickRect.anchorMin        = new Vector2(0f, yNormalized);
        tickRect.anchorMax        = new Vector2(1f, yNormalized);
        tickRect.pivot            = new Vector2(0.5f, 0.5f);
        tickRect.sizeDelta        = new Vector2(0f, 2f);
        tickRect.anchoredPosition = Vector2.zero;

        var img = tickGO.GetComponent<Image>();
        img.sprite        = WhiteSprite;
        img.color         = new Color(1f, 1f, 1f, 0.45f); // Crisp semi-transparent tick line
        img.raycastTarget = false;
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