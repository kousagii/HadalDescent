using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Full-Screen Image Slideshow Briefing for minigames.
/// Implements Section 5C of HadalDescent_Interactive_Tutorial_Story_Plan.md:
///   - Displays one tutorial image at a time from Assets/Arts/Tutorial/
///   - NO overlay instruction text — all instructions are baked directly into the images
///   - 2px cyan border (#00E5FF) with deep navy container
///   - Page indicator (e.g. "1 / 3") in Gameplay SDF, 34px, normal weight
///   - ◀ PREV and NEXT ▶ touch navigation arrows
///   - Prominent [ CONTINUE ] button on final slide replaces NEXT ▶
///   - Top-right [SKIP ▶] button (32px) to skip directly to gameplay
///   - Natural swipe gesture navigation (mobile & desktop drag)
///   - Fully self-paced (no timer auto-advance)
/// </summary>
public class MinigameInstructionOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static MinigameInstructionOverlay _instance;
    public static MinigameInstructionOverlay Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MinigameInstructionOverlay>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    var canvas = UIManager.GetExplorationCanvas();
                    var go = new GameObject("MinigameInstructionOverlay");
                    if (canvas != null)
                        go.transform.SetParent(canvas.transform, false);
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MinigameInstructionOverlay>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Optional Custom UI Bindings")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private RawImage   slideDisplayImage;
    [SerializeField] private TMP_Text   pageIndicatorText;
    [SerializeField] private Button     prevButton;
    [SerializeField] private Button     nextButton;
    [SerializeField] private Button     continueButton;
    [SerializeField] private Button     skipButton;

    // Procedural UI references
    private RectTransform   _panelRect;
    private CanvasGroup     _canvasGroup;
    private RawImage        _procSlideImage;
    private RectTransform   _controlsRect;
    private TMP_Text        _procPageIndicator;
    private Button          _procPrevBtn;
    private Button          _procNextBtn;
    private Button          _procContinueBtn;
    private Button          _procSkipBtn;

    // Active Slideshow State
    private readonly List<Texture2D> _currentSlides = new List<Texture2D>();
    private int    _currentSlideIndex = 0;
    private Action _onFinishedCallback;
    private bool   _isOpen = false;

    // Swipe gesture tracking
    private Vector2 _dragStartPos;
    private bool    _isDragging = false;

    public bool IsOpen => _isOpen || (overlayRoot != null && overlayRoot.activeSelf) || (_panelRect != null && _panelRect.gameObject.activeSelf);

    // Static slide filename mappings
    private static readonly Dictionary<int, string[]> MinigameSlideFiles = new Dictionary<int, string[]>
    {
        { 1, new[] { "Minigame 1-1.png", "Minigame 1-2.png" } },
        { 2, new[] { "Minigame 2.png" } },
        { 3, new[] { "Minigame 3-1.png", "Minigame 3-2.png", "Minigame 3-3.png" } },
        { 4, new[] { "Minigame 4-1.png", "Minigame 4-2.png", "Minigame 4-3.png" } }
    };

    private static readonly Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<int> _shownTutorials = new HashSet<int>();

    /// <summary>
    /// Resets the history of shown minigame tutorials so instructions show again on a fresh playthrough.
    /// </summary>
    public static void ResetShownTutorials()
    {
        _shownTutorials.Clear();
        Debug.Log("[MinigameInstructionOverlay] Tutorial history reset for new game session.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnDomainReload()
    {
        _shownTutorials.Clear();
        _cachedTextures.Clear();
        _instance = null;
    }

    // Design Colors
    private static readonly Color ButtonFillColor = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f); // #1f5f7d
    private static readonly Color CyanOutlineColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);      // (0, 0.9, 1)

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        EnsureUI();
        HideInstant();
    }

    public void EnsureUI()
    {
        if (overlayRoot != null)
        {
            ApplyCustomBindingsStyling();
            return;
        }
        if (_panelRect != null) return;

        // Find or create Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.name.Contains("Skip") || canvas.name.Contains("Arrow") || canvas.name.Contains("Pointer") || canvas.name.Contains("Popup") || canvas.GetComponent<CanvasScaler>() == null)
        {
            canvas = UIManager.GetExplorationCanvas();
        }

        if (canvas == null)
        {
            var cgo = new GameObject("TutorialBriefingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28000;

            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(cgo);
        }

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Root fullscreen dimmer overlay
        var rootGO = new GameObject("MinigameInstructionOverlay_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        _panelRect = rootGO.GetComponent<RectTransform>();
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();

        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.one;
        _panelRect.sizeDelta = Vector2.zero;
        _panelRect.anchoredPosition = Vector2.zero;

        // Ensure instruction overlay renders above minigames (order 0) and bestiary (order 9999), below Skip button (32000)
        var overlayCanvas = rootGO.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 28000;
        var raycaster = rootGO.AddComponent<GraphicRaycaster>();
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Dark vignette background
        var bgImage = rootGO.GetComponent<Image>();
        bgImage.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);

        // Raw Image for Tutorial Slide (base 600 x 400, scaled 2 on all axes, with (0, 0.9, 1) outline)
        var imgGO = new GameObject("SlideImage", typeof(RectTransform), typeof(RawImage), typeof(Outline));
        imgGO.transform.SetParent(rootGO.transform, false);
        var imgRect = imgGO.GetComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.pivot     = new Vector2(0.5f, 0.5f);
        imgRect.sizeDelta = new Vector2(600f, 400f);
        imgRect.localScale = new Vector3(2f, 2f, 2f);
        imgRect.anchoredPosition = new Vector2(0f, 60f);

        _procSlideImage = imgGO.GetComponent<RawImage>();
        _procSlideImage.raycastTarget = false; // allows swipe gestures on slide to reach rootGO

        var imgOutline = imgGO.GetComponent<Outline>();
        imgOutline.effectColor = CyanOutlineColor;
        imgOutline.effectDistance = new Vector2(2f, -2f);
        imgOutline.useGraphicAlpha = false;

        // Bottom Controls Bar (Below Image Container)
        var controlsGO = new GameObject("BottomControlsBar", typeof(RectTransform));
        controlsGO.transform.SetParent(rootGO.transform, false);
        _controlsRect = controlsGO.GetComponent<RectTransform>();
        _controlsRect.anchorMin = new Vector2(0.5f, 0.5f);
        _controlsRect.anchorMax = new Vector2(0.5f, 0.5f);
        _controlsRect.pivot     = new Vector2(0.5f, 0.5f);
        _controlsRect.sizeDelta = new Vector2(1200f, 80f);
        _controlsRect.anchoredPosition = new Vector2(0f, -420f);

        // ◀ PREV Button (Left aligned, 300x68px, #1f5f7d fill, cyan outline, Gameplay SDF)
        var prevGO = CreateProceduralButton("PrevButton", controlsGO.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(300f, 68f), Vector2.zero, "◀ PREV", font, OnPrevClicked);
        _procPrevBtn = prevGO.GetComponent<Button>();

        // Page Indicator: "1 / 3" (Centered between buttons)
        var pageGO = new GameObject("PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        pageGO.transform.SetParent(controlsGO.transform, false);
        var pageRect = pageGO.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0.5f);
        pageRect.anchorMax = new Vector2(0.5f, 0.5f);
        pageRect.pivot     = new Vector2(0.5f, 0.5f);
        pageRect.sizeDelta = new Vector2(260f, 60f);
        pageRect.anchoredPosition = Vector2.zero;

        _procPageIndicator = pageGO.GetComponent<TextMeshProUGUI>();
        _procPageIndicator.font = font;
        _procPageIndicator.fontStyle = FontStyles.Normal;
        _procPageIndicator.fontSize = 34f;
        _procPageIndicator.color = new Color(0.93f, 0.95f, 0.97f, 1.00f);
        _procPageIndicator.alignment = TextAlignmentOptions.Center;
        _procPageIndicator.raycastTarget = false;
        _procPageIndicator.text = "1 / 3";

        // NEXT ▶ Button (Right aligned, 300x68px, #1f5f7d fill, cyan outline, Gameplay SDF)
        var nextGO = CreateProceduralButton("NextButton", controlsGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(300f, 68f), Vector2.zero, "NEXT ▶", font, OnNextClicked);
        _procNextBtn = nextGO.GetComponent<Button>();

        // [ CONTINUE ] Button (Right aligned, replaces NEXT on last slide)
        var contGO = CreateProceduralButton("ContinueButton", controlsGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(300f, 68f), Vector2.zero, "CONTINUE", font, OnContinueClicked);
        _procContinueBtn = contGO.GetComponent<Button>();
        contGO.SetActive(false);

        UpdateLayoutResponsiveness();
    }

    /// <summary>
    /// Builds a styled UI button retaining the cyan border outline, #1f5f7d background fill, and high-contrast text.
    /// </summary>
    private GameObject CreateProceduralButton(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 pos,
        string labelText,
        TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(parent, false);
        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot     = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        // Button background (color #1f5f7d)
        var img = btnGO.GetComponent<Image>();
        img.color = ButtonFillColor;

        // Button outline (cyan (0, 0.9, 1))
        var outline = btnGO.GetComponent<Outline>();
        outline.effectColor = CyanOutlineColor;
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        outline.useGraphicAlpha = false;

        // Inner subtle cyan border inset to ensure ultra-sharp border on all devices
        var innerGO = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
        innerGO.transform.SetParent(btnGO.transform, false);
        var innerRect = innerGO.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.sizeDelta = new Vector2(-4f, -4f);
        innerRect.anchoredPosition = Vector2.zero;
        var innerBg = innerGO.GetComponent<Image>();
        innerBg.color = ButtonFillColor;
        innerBg.raycastTarget = false;

        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors = btn.colors;
        colors.normalColor = ButtonFillColor;
        colors.highlightedColor = new Color(0.18f, 0.48f, 0.62f, 1.00f);
        colors.pressedColor = new Color(0.08f, 0.26f, 0.35f, 1.00f);
        colors.selectedColor = ButtonFillColor;
        colors.disabledColor = new Color(ButtonFillColor.r, ButtonFillColor.g, ButtonFillColor.b, 0.50f);
        btn.colors = colors;

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(btnGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.fontSize = 32f;
        text.color = new Color(0.93f, 0.95f, 0.97f, 1.00f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = labelText;

        return btnGO;
    }

    private void ApplyCustomBindingsStyling()
    {
        if (slideDisplayImage != null)
        {
            var outline = slideDisplayImage.GetComponent<Outline>();
            if (outline == null) outline = slideDisplayImage.gameObject.AddComponent<Outline>();
            outline.effectColor = CyanOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            slideDisplayImage.rectTransform.sizeDelta = new Vector2(600f, 400f);
            slideDisplayImage.rectTransform.localScale = new Vector3(2f, 2f, 2f);
        }

        StyleCustomButton(prevButton);
        StyleCustomButton(nextButton);
        StyleCustomButton(continueButton);
        StyleCustomButton(skipButton);
    }

    private void StyleCustomButton(Button btn)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = ButtonFillColor;

        var outline = btn.GetComponent<Outline>();
        if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
        outline.effectColor = CyanOutlineColor;
        outline.effectDistance = new Vector2(2.5f, -2.5f);
    }

    /// <summary>
    /// Dynamically scales and positions the tutorial slide and controls so it looks optimal
    /// on all mobile aspect ratios (16:9, 18:9, 19.5:9, 20:9, 4:3) without clipping or button collisions.
    /// </summary>
    public void UpdateLayoutResponsiveness()
    {
        if (_panelRect == null || _procSlideImage == null) return;

        float canvasW = _panelRect.rect.width > 0 ? _panelRect.rect.width : 1920f;
        float canvasH = _panelRect.rect.height > 0 ? _panelRect.rect.height : 1080f;

        // Base image is 600 x 400. Target scale is 2.0 (1200 x 800)
        float availableW = canvasW * 0.92f;
        float availableH = canvasH - 200f; // Leave space for controls bar and safe margins
        float maxScaleX = availableW / 600f;
        float maxScaleY = availableH / 400f;
        float scale = Mathf.Min(2.0f, Mathf.Min(maxScaleX, maxScaleY));
        if (scale < 1.0f) scale = 1.0f;

        var imgRect = _procSlideImage.rectTransform;
        imgRect.sizeDelta = new Vector2(600f, 400f);
        imgRect.localScale = new Vector3(scale, scale, scale);

        float scaledHalfH = (400f * scale) * 0.5f;
        float yPos = 60f * (scale / 2.0f);
        imgRect.anchoredPosition = new Vector2(0f, yPos);

        if (_controlsRect != null)
        {
            float barW = Mathf.Min(1200f, 600f * scale);
            _controlsRect.sizeDelta = new Vector2(barW, 80f);
            float btnY = yPos - scaledHalfH - 65f;
            _controlsRect.anchoredPosition = new Vector2(0f, btnY);
        }
    }

    /// <summary>
    /// Displays the full-screen tutorial slideshow for the specified minigame (1 to 4).
    /// Pauses time / input and invokes onFinished when [CONTINUE] or [SKIP] is pressed.
    /// </summary>
    public void ShowSlideshow(int minigameNumber, Action onFinished = null)
    {
        if (minigameNumber < 1 || minigameNumber > 4)
        {
            onFinished?.Invoke();
            return;
        }

        if (_shownTutorials.Contains(minigameNumber))
        {
            onFinished?.Invoke();
            return;
        }

        EnsureUI();
        UpdateLayoutResponsiveness();
        _shownTutorials.Add(minigameNumber);
        
        _onFinishedCallback = onFinished;
        _currentSlideIndex = 0;
        _currentSlides.Clear();

        if (MinigameSlideFiles.TryGetValue(minigameNumber, out string[] filenames))
        {
            foreach (var fn in filenames)
            {
                var tex = LoadTutorialTexture(fn);
                if (tex != null)
                {
                    _currentSlides.Add(tex);
                }
            }
        }

        if (_currentSlides.Count == 0)
        {
            Debug.LogWarning($"[MinigameInstructionOverlay] No slides found for Minigame {minigameNumber}. Proceeding immediately.");
            onFinished?.Invoke();
            return;
        }

        _isOpen = true;
        if (overlayRoot != null)
        {
            overlayRoot.transform.SetAsLastSibling();
            overlayRoot.SetActive(true);
        }
        if (_panelRect != null)
        {
            _panelRect.SetAsLastSibling();
            _panelRect.gameObject.SetActive(true);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        DisplaySlide(_currentSlideIndex);
    }

    private void DisplaySlide(int index)
    {
        if (index < 0 || index >= _currentSlides.Count) return;

        _currentSlideIndex = index;
        Texture2D slideTex = _currentSlides[index];

        // Assign to RawImage
        if (slideDisplayImage != null) slideDisplayImage.texture = slideTex;
        if (_procSlideImage != null)
        {
            _procSlideImage.texture = slideTex;
        }

        // Update Page Indicator ("1 / 3")
        string indicator = $"{_currentSlideIndex + 1} / {_currentSlides.Count}";
        if (pageIndicatorText != null) pageIndicatorText.text = indicator;
        if (_procPageIndicator != null) _procPageIndicator.text = indicator;

        // Update Prev Button (Hidden on first slide)
        bool showPrev = _currentSlideIndex > 0;
        if (prevButton != null) prevButton.gameObject.SetActive(showPrev);
        if (_procPrevBtn != null) _procPrevBtn.gameObject.SetActive(showPrev);

        // Update Next vs Continue Button
        bool isLastSlide = _currentSlideIndex == _currentSlides.Count - 1;

        if (nextButton != null) nextButton.gameObject.SetActive(!isLastSlide);
        if (_procNextBtn != null) _procNextBtn.gameObject.SetActive(!isLastSlide);

        if (continueButton != null) continueButton.gameObject.SetActive(isLastSlide);
        if (_procContinueBtn != null) _procContinueBtn.gameObject.SetActive(isLastSlide);
    }

    public void OnNextClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (_currentSlideIndex < _currentSlides.Count - 1)
        {
            DisplaySlide(_currentSlideIndex + 1);
        }
    }

    public void OnPrevClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (_currentSlideIndex > 0)
        {
            DisplaySlide(_currentSlideIndex - 1);
        }
    }

    public void OnContinueClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        CloseOverlay();
    }

    public void OnSkipClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        _isOpen = false;
        HideInstant();
        _onFinishedCallback?.Invoke();
        _onFinishedCallback = null;
    }

    public void HideInstant()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        if (_panelRect != null) _panelRect.gameObject.SetActive(false);
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    // -----------------------------------------------------------------------
    // Mobile Swipe Gesture Navigation
    // -----------------------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStartPos = eventData.position;
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Dragging in progress
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;

        float deltaX = eventData.position.x - _dragStartPos.x;
        const float swipeThreshold = 60f;

        if (deltaX < -swipeThreshold)
        {
            // Swiped Left -> Advance to next slide
            if (_currentSlideIndex < _currentSlides.Count - 1)
            {
                OnNextClicked();
            }
        }
        else if (deltaX > swipeThreshold)
        {
            // Swiped Right -> Go to prev slide
            if (_currentSlideIndex > 0)
            {
                OnPrevClicked();
            }
        }
    }

    // -----------------------------------------------------------------------
    // Texture Loading Helper (Works seamlessly on Android APK, iOS, PC, and Editor)
    // -----------------------------------------------------------------------

    private static Texture2D LoadTutorialTexture(string filename)
    {
        if (_cachedTextures.TryGetValue(filename, out var cached) && cached != null)
            return cached;

        string baseName = Path.GetFileNameWithoutExtension(filename);

        // 1. Primary: Load via Unity Resources (guaranteed to be bundled into APK build)
        Texture2D tex = Resources.Load<Texture2D>($"Tutorial/{baseName}");
        if (tex == null)
        {
            var sprite = Resources.Load<Sprite>($"Tutorial/{baseName}");
            if (sprite != null)
                tex = sprite.texture;
        }

        // Check without subfolder if placed at Resources root
        if (tex == null)
        {
            tex = Resources.Load<Texture2D>(baseName);
            if (tex == null)
            {
                var sprite = Resources.Load<Sprite>(baseName);
                if (sprite != null)
                    tex = sprite.texture;
            }
        }

        if (tex != null)
        {
            _cachedTextures[filename] = tex;
            return tex;
        }

        // 2. Fallback: Search in-memory objects
        var inMemory = Resources.FindObjectsOfTypeAll<Texture2D>();
        foreach (var t in inMemory)
        {
            if (t != null && (t.name.Equals(baseName, StringComparison.OrdinalIgnoreCase) || t.name.Equals(filename, StringComparison.OrdinalIgnoreCase)))
            {
                _cachedTextures[filename] = t;
                return t;
            }
        }

        // 3. Editor-only fallback for reading directly from disk
#if UNITY_EDITOR
        try
        {
            string diskPath = Path.Combine(Application.dataPath, "Resources", "Tutorial", filename);
            if (!File.Exists(diskPath))
            {
                diskPath = Path.Combine(Application.dataPath, "Arts", "Tutorial", filename);
            }

            if (File.Exists(diskPath))
            {
                byte[] bytes = File.ReadAllBytes(diskPath);
                var loadedTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (loadedTex.LoadImage(bytes))
                {
                    loadedTex.name = baseName;
                    _cachedTextures[filename] = loadedTex;
                    return loadedTex;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MinigameInstructionOverlay] Failed reading {filename} from disk: {ex.Message}");
        }
#endif

        return null;
    }
}
