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
                    var canvas = FindFirstObjectByType<Canvas>();
                    var go = new GameObject("MinigameInstructionOverlay");
                    if (canvas != null)
                        go.transform.SetParent(canvas.transform, false);
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
    private AspectRatioFitter _procAspectRatioFitter;
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
        if (overlayRoot != null) return;
        if (_panelRect != null) return;

        // Find or create Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            var cgo = new GameObject("TutorialBriefingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
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

        // Dark vignette background (0.02, 0.04, 0.08, 0.94)
        var bgImage = rootGO.GetComponent<Image>();
        bgImage.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);

        // Skip button removed entirely per user request; handled by universal HUD button.

        // Center Container for the Tutorial Slide
        var containerGO = new GameObject("ImageContainer", typeof(RectTransform), typeof(Image));
        containerGO.transform.SetParent(rootGO.transform, false);
        var containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot     = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(1100f, 620f);
        containerRect.anchoredPosition = new Vector2(0f, 40f);

        var containerBg = containerGO.GetComponent<Image>();
        containerBg.color = new Color(0.04f, 0.08f, 0.16f, 0.95f);

        // 2px Solid Cyan Border around Image Container
        var borderGO = new GameObject("CyanBorder", typeof(RectTransform), typeof(Outline));
        borderGO.transform.SetParent(containerGO.transform, false);
        var borderOutline = containerGO.AddComponent<Outline>();
        borderOutline.effectColor = new Color(0.00f, 0.90f, 1.00f, 0.90f);
        borderOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // The Image inside container (RawImage with AspectRatioFitter)
        var imgGO = new GameObject("SlideImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        imgGO.transform.SetParent(containerGO.transform, false);
        var imgRect = imgGO.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = new Vector2(-12f, -12f);
        imgRect.anchoredPosition = Vector2.zero;

        _procSlideImage = imgGO.GetComponent<RawImage>();
        _procAspectRatioFitter = imgGO.GetComponent<AspectRatioFitter>();
        _procAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _procAspectRatioFitter.aspectRatio = 16f / 9f;

        // Bottom Controls Bar (Below Image Container)
        var controlsGO = new GameObject("BottomControlsBar", typeof(RectTransform));
        controlsGO.transform.SetParent(rootGO.transform, false);
        var controlsRect = controlsGO.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0.5f, 0.5f);
        controlsRect.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRect.pivot     = new Vector2(0.5f, 0.5f);
        controlsRect.sizeDelta = new Vector2(1100f, 90f);
        controlsRect.anchoredPosition = new Vector2(0f, -330f);

        // ◀ PREV Button (Box container: 260x64px, 2px Cyan Border, Gameplay SDF 32px)
        var prevGO = new GameObject("PrevButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        prevGO.transform.SetParent(controlsGO.transform, false);
        var prevRect = prevGO.GetComponent<RectTransform>();
        prevRect.anchorMin = new Vector2(0f, 0.5f);
        prevRect.anchorMax = new Vector2(0f, 0.5f);
        prevRect.pivot     = new Vector2(0f, 0.5f);
        prevRect.sizeDelta = new Vector2(260f, 64f);
        prevRect.anchoredPosition = new Vector2(20f, 0f);

        var prevBg = prevGO.GetComponent<Image>();
        prevBg.color = new Color(0.03f, 0.12f, 0.22f, 0.95f);

        var prevOutline = prevGO.GetComponent<Outline>();
        prevOutline.effectColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        prevOutline.effectDistance = new Vector2(2f, -2f);

        _procPrevBtn = prevGO.GetComponent<Button>();
        _procPrevBtn.targetGraphic = prevBg;
        _procPrevBtn.onClick.AddListener(OnPrevClicked);

        var prevTextGO = new GameObject("PrevLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        prevTextGO.transform.SetParent(prevGO.transform, false);
        var prevTextRect = prevTextGO.GetComponent<RectTransform>();
        prevTextRect.anchorMin = Vector2.zero;
        prevTextRect.anchorMax = Vector2.one;
        prevTextRect.sizeDelta = Vector2.zero;
        prevTextRect.anchoredPosition = Vector2.zero;

        var prevText = prevTextGO.GetComponent<TextMeshProUGUI>();
        prevText.font = font;
        prevText.fontStyle = FontStyles.Normal;
        prevText.fontSize = 32f;
        prevText.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        prevText.alignment = TextAlignmentOptions.Center;
        prevText.raycastTarget = false;
        prevText.text = "◀ PREV";

        // Page Indicator: "1 / 3" (Gameplay SDF, 34px, Off-White)
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
        _procPageIndicator.text = "1 / 3";

        // NEXT ▶ Button (Box container: 260x64px, 2px Cyan Border, Gameplay SDF 32px)
        var nextGO = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        nextGO.transform.SetParent(controlsGO.transform, false);
        var nextRect = nextGO.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0.5f);
        nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot     = new Vector2(1f, 0.5f);
        nextRect.sizeDelta = new Vector2(260f, 64f);
        nextRect.anchoredPosition = new Vector2(-20f, 0f);

        var nextBg = nextGO.GetComponent<Image>();
        nextBg.color = new Color(0.03f, 0.12f, 0.22f, 0.95f);

        var nextOutline = nextGO.GetComponent<Outline>();
        nextOutline.effectColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        nextOutline.effectDistance = new Vector2(2f, -2f);

        _procNextBtn = nextGO.GetComponent<Button>();
        _procNextBtn.targetGraphic = nextBg;
        _procNextBtn.onClick.AddListener(OnNextClicked);

        var nextTextGO = new GameObject("NextLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        nextTextGO.transform.SetParent(nextGO.transform, false);
        var nextTextRect = nextTextGO.GetComponent<RectTransform>();
        nextTextRect.anchorMin = Vector2.zero;
        nextTextRect.anchorMax = Vector2.one;
        nextTextRect.sizeDelta = Vector2.zero;
        nextTextRect.anchoredPosition = Vector2.zero;

        var nextText = nextTextGO.GetComponent<TextMeshProUGUI>();
        nextText.font = font;
        nextText.fontStyle = FontStyles.Normal;
        nextText.fontSize = 32f;
        nextText.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        nextText.alignment = TextAlignmentOptions.Center;
        nextText.raycastTarget = false;
        nextText.text = "NEXT ▶";

        // [ CONTINUE ] Button (Replaces NEXT on last slide: 260x64px, 2px Cyan Border, Gameplay SDF 32px)
        var contGO = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        contGO.transform.SetParent(controlsGO.transform, false);
        var contRect = contGO.GetComponent<RectTransform>();
        contRect.anchorMin = new Vector2(1f, 0.5f);
        contRect.anchorMax = new Vector2(1f, 0.5f);
        contRect.pivot     = new Vector2(1f, 0.5f);
        contRect.sizeDelta = new Vector2(260f, 64f);
        contRect.anchoredPosition = new Vector2(-20f, 0f);

        var contBg = contGO.GetComponent<Image>();
        contBg.color = new Color(0.03f, 0.12f, 0.22f, 0.95f);

        var contOutline = contGO.GetComponent<Outline>();
        contOutline.effectColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        contOutline.effectDistance = new Vector2(2f, -2f);

        _procContinueBtn = contGO.GetComponent<Button>();
        _procContinueBtn.targetGraphic = contBg;
        _procContinueBtn.onClick.AddListener(OnContinueClicked);

        var contTextGO = new GameObject("ContinueLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        contTextGO.transform.SetParent(contGO.transform, false);
        var contTextRect = contTextGO.GetComponent<RectTransform>();
        contTextRect.anchorMin = Vector2.zero;
        contTextRect.anchorMax = Vector2.one;
        contTextRect.sizeDelta = Vector2.zero;
        contTextRect.anchoredPosition = Vector2.zero;

        var contText = contTextGO.GetComponent<TextMeshProUGUI>();
        contText.font = font;
        contText.fontStyle = FontStyles.Normal;
        contText.fontSize = 32f;
        contText.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        contText.alignment = TextAlignmentOptions.Center;
        contText.raycastTarget = false;
        contText.text = "CONTINUE";

        contGO.SetActive(false); // Hidden until last slide
    }

    /// <summary>
    /// Displays the full-screen tutorial slideshow for the specified minigame (1 to 4).
    /// Pauses time / input and invokes onFinished when [CONTINUE] or [SKIP] is pressed.
    /// </summary>
    public void ShowSlideshow(int minigameNumber, Action onFinished = null)
    {
        if (_shownTutorials.Contains(minigameNumber))
        {
            onFinished?.Invoke();
            return;
        }

        EnsureUI();
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
        if (overlayRoot != null) overlayRoot.SetActive(true);
        if (_panelRect != null) _panelRect.gameObject.SetActive(true);

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
            if (_procAspectRatioFitter != null && slideTex != null && slideTex.height > 0)
            {
                _procAspectRatioFitter.aspectRatio = (float)slideTex.width / slideTex.height;
            }
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
    // Texture Loading Helper
    // -----------------------------------------------------------------------

    private static Texture2D LoadTutorialTexture(string filename)
    {
        if (_cachedTextures.TryGetValue(filename, out var cached) && cached != null)
            return cached;

        // 1. Search in memory
        string baseName = Path.GetFileNameWithoutExtension(filename);
        var inMemory = Resources.FindObjectsOfTypeAll<Texture2D>();
        foreach (var t in inMemory)
        {
            if (t != null && (t.name.Equals(baseName, StringComparison.OrdinalIgnoreCase) || t.name.Equals(filename, StringComparison.OrdinalIgnoreCase)))
            {
                _cachedTextures[filename] = t;
                return t;
            }
        }

        // 2. Read from disk in Assets/Arts/Tutorial/
        try
        {
            string diskPath = Path.Combine(Application.dataPath, "Arts", "Tutorial", filename);
            if (File.Exists(diskPath))
            {
                byte[] bytes = File.ReadAllBytes(diskPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = baseName;
                    _cachedTextures[filename] = tex;
                    return tex;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MinigameInstructionOverlay] Failed reading {filename} from disk: {ex.Message}");
        }

        return null;
    }
}
