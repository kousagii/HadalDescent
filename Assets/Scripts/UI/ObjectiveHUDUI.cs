using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dedicated Objective HUD Card positioned below the Sonar Range Box on the top-left of the screen.
/// Decoupled from the Radio Textbox so mission objectives do not crowd radio communications.
///
/// Features:
///   - Collapsible header bar with mission logo on the left, title in the center, and chevron (v / ^) on the right.
///   - User can tap anywhere on the header to toggle between Collapsed and Expanded states.
///   - Mission Logo sprite can be assigned directly in the Unity Inspector (falls back to procedural cyan icon if unassigned).
///   - Fully supported in both Tutorial Mode and Post-Tutorial Exploration Mode.
///
/// Post-Tutorial Progression Missions ("TO GO DEEPER:"):
///   - "- Upgrade submarine Hull"
///   - "- Discover at least half of the {Zone} species ({discovered}/{total})"
/// </summary>
public class ObjectiveHUDUI : MonoBehaviour
{
    private static ObjectiveHUDUI _instance;
    public static ObjectiveHUDUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ObjectiveHUDUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    var canvas = UIManager.GetExplorationCanvas() ?? FindFirstObjectByType<Canvas>();
                    var go = new GameObject("ObjectiveHUDUI");
                    if (canvas != null)
                        go.transform.SetParent(canvas.transform, false);
                    _instance = go.AddComponent<ObjectiveHUDUI>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public const string PostTutorialDefaultObjective = "Upgrade submarine hull and discover half of the species to go deeper";

    public const float ExpandedWidth = 420f;
    public const float ExpandedHeight = 220f;
    public const float CollapsedLogoSize = 80f;
    public const float CollapsedMargin = 10f;
    public const float CollapsedWidth = CollapsedLogoSize + (CollapsedMargin * 2f);  // 140f
    public const float CollapsedHeight = CollapsedLogoSize + (CollapsedMargin * 2f); // 140f
    public const float ExpandedLogoSize = 26f;

    // -----------------------------------------------------------------------
    // Inspector Assignments
    // -----------------------------------------------------------------------

    [Header("Inspector Logo & Styling")]
    [Tooltip("Mission logo sprite displayed on the left side of the mission card. Assign in Inspector.")]
    [SerializeField] private Sprite missionLogo;

    [Tooltip("Color tint for the mission logo (defaults to HUD Cyan #00E5FF).")]
    [SerializeField] private Color logoColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);

    [Tooltip("Start in collapsed state.")]
    [SerializeField] private bool startCollapsed = false;

    [Header("Optional Component Overrides (Auto-wired if null)")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup   canvasGroup;
    [SerializeField] private Image         logoImage;
    [SerializeField] private Button        headerButton;
    [SerializeField] private TMP_Text      headerText;
    [SerializeField] private TMP_Text      chevronText;
    [SerializeField] private GameObject    bodyContainer;
    [SerializeField] private CanvasGroup   bodyCanvasGroup;
    [SerializeField] private TMP_Text      objectiveBodyText;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private bool      _isCollapsed = false;
    private string    _currentObjective = "";
    private bool      _isCustomTutorialObjective = false;
    private float     _pollTimer = 0f;
    private float     _startupGraceTimer = 1.5f;
    private Coroutine _animCoroutine;

    public bool IsVisible => panelRect != null && panelRect.gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.01f;
    public bool IsCollapsed => _isCollapsed;

    public Sprite MissionLogo
    {
        get => missionLogo;
        set
        {
            missionLogo = value;
            UpdateLogoVisual();
        }
    }

    private static bool IsCinematicActive()
    {
        return CinematicSequencer.Instance != null && CinematicSequencer.Instance.IsPlaying;
    }

    private static bool IsMinigameActive()
    {
        return (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
            || (MinigameInstructionOverlay.Instance != null && MinigameInstructionOverlay.Instance.IsOpen);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _isCollapsed = startCollapsed;
    }

    private void Start()
    {
        EnsureUI();

        // Subscribe to scan and upgrade events for instant reactive UI updates
        ScannerSystem.OnSpeciesScanned += OnSpeciesScanned;
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (IsCinematicActive() || IsMinigameActive())
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
        }
        else
        {
            if (!inTutorial)
            {
                RefreshProgressionObjectives();
            }
            else if (string.IsNullOrWhiteSpace(_currentObjective))
            {
                SetObjective(PostTutorialDefaultObjective);
            }

            SetVisible(true);
        }

        UpdateCollapseVisuals(animate: false);
    }

    private void OnDestroy()
    {
        ScannerSystem.OnSpeciesScanned -= OnSpeciesScanned;
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        // 1. Never display Objective HUD during cinematic camera fly-bys or minigames
        if (IsCinematicActive() || IsMinigameActive())
        {
            if (panelRect != null && panelRect.gameObject.activeSelf)
            {
                panelRect.gameObject.SetActive(false);
            }
            return;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (inTutorial && _isCustomTutorialObjective)
        {
            // Restore visibility after minigame or cinematic ends if we have an active objective
            if (panelRect != null && !panelRect.gameObject.activeSelf && !string.IsNullOrWhiteSpace(_currentObjective))
            {
                panelRect.gameObject.SetActive(true);
                if (canvasGroup != null) canvasGroup.alpha = 1f;
            }
            return;
        }

        if (_startupGraceTimer > 0f)
        {
            _startupGraceTimer -= Time.unscaledDeltaTime;
        }

        // Periodic refresh to catch zone transitions, save loads, or external progression changes
        _pollTimer -= Time.deltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = 0.25f;
            RefreshProgressionObjectives();
        }
    }

    private void OnSpeciesScanned(SpeciesData species)
    {
        if (IsCinematicActive() || IsMinigameActive()) return;
        RefreshProgressionObjectives();
    }

    private void OnUpgradePurchased(UpgradeCategory category, int tier)
    {
        if (IsCinematicActive() || IsMinigameActive()) return;
        if (category == UpgradeCategory.Hull)
        {
            RefreshProgressionObjectives();
        }
    }

    // -----------------------------------------------------------------------
    // UI Construction & Setup
    // -----------------------------------------------------------------------

    public void EnsureUI()
    {
        if (panelRect != null)
        {
            UpdateLogoVisual();
            return;
        }

        var canvas = UIManager.GetExplorationCanvas()
                  ?? GetComponentInParent<Canvas>()
                  ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Container: Compact card anchored top-left cleanly below the Sonar Range box (y = -380)
        var rootGO = new GameObject("ObjectiveCard_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        panelRect = rootGO.GetComponent<RectTransform>();
        canvasGroup = rootGO.GetComponent<CanvasGroup>();

        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot     = new Vector2(0f, 1f);
        panelRect.sizeDelta = _isCollapsed
            ? new Vector2(CollapsedWidth, CollapsedHeight)
            : new Vector2(ExpandedWidth, ExpandedHeight);
        panelRect.anchoredPosition = new Vector2(35f, -380f);

        var bg = rootGO.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.07f, 0.14f, 0.92f);
        bg.raycastTarget = false;

        // 2px Top Cyan Border Line (#00E5FF)
        var borderGO = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(rootGO.transform, false);
        var bRect = borderGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0f, 1f);
        bRect.anchorMax = new Vector2(1f, 1f);
        bRect.pivot     = new Vector2(0.5f, 1f);
        bRect.sizeDelta = new Vector2(0f, 2f);
        bRect.anchoredPosition = Vector2.zero;
        var bImg = borderGO.GetComponent<Image>();
        bImg.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        bImg.raycastTarget = false;

        // ── Header Row (Clickable Button for Collapsing) ───────────────────────
        var headerGO = new GameObject("HeaderRow_Button", typeof(RectTransform), typeof(Image), typeof(Button));
        headerGO.transform.SetParent(rootGO.transform, false);
        var hRect = headerGO.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot     = new Vector2(0.5f, 1f);
        hRect.sizeDelta = new Vector2(0f, 38f);
        hRect.anchoredPosition = new Vector2(0f, -15f); // 5f margin on top of "TO GO DEEPER:"

        var hImg = headerGO.GetComponent<Image>();
        hImg.color = Color.clear;
        hImg.raycastTarget = true;

        headerButton = headerGO.GetComponent<Button>();
        headerButton.transition = Selectable.Transition.ColorTint;
        var colors = headerButton.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(0.00f, 0.90f, 1.00f, 0.08f);
        colors.pressedColor = new Color(0.00f, 0.90f, 1.00f, 0.16f);
        headerButton.colors = colors;
        headerButton.onClick.AddListener(ToggleCollapse);

        // Mission Logo Image (Left side)
        var logoGO = new GameObject("MissionLogo", typeof(RectTransform), typeof(Image));
        logoGO.transform.SetParent(headerGO.transform, false);
        var logoRect = logoGO.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 0.5f);
        logoRect.anchorMax = new Vector2(0f, 0.5f);
        logoRect.pivot     = new Vector2(0f, 0.5f);
        logoRect.sizeDelta = new Vector2(26f, 26f);
        logoRect.anchoredPosition = new Vector2(14f, 0f);

        logoImage = logoGO.GetComponent<Image>();
        logoImage.raycastTarget = false;
        logoImage.preserveAspect = true;
        UpdateLogoVisual();

        // Chevron Text Indicator (Right side: 'v' rotated for identical size & weight in both states)
        var chevronGO = new GameObject("ChevronText", typeof(RectTransform), typeof(TextMeshProUGUI));
        chevronGO.transform.SetParent(headerGO.transform, false);
        var cRect = chevronGO.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(1f, 0.5f);
        cRect.anchorMax = new Vector2(1f, 0.5f);
        cRect.pivot     = new Vector2(0.5f, 0.5f);
        cRect.sizeDelta = new Vector2(24f, 24f);
        cRect.anchoredPosition = new Vector2(-26f, 0f);

        chevronText = chevronGO.GetComponent<TextMeshProUGUI>();
        chevronText.font = font;
        chevronText.fontStyle = FontStyles.Bold;
        chevronText.fontSize = 14f;
        chevronText.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        chevronText.alignment = TextAlignmentOptions.Center;
        chevronText.raycastTarget = false;
        chevronText.text = "v";
        cRect.localEulerAngles = new Vector3(0f, 0f, _isCollapsed ? 0f : 180f);

        // Header Title Label ("MISSION" when collapsed, "TO GO DEEPER:" when expanded)
        var labelGO = new GameObject("HeaderLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(headerGO.transform, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot     = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(44f, 0f);
        labelRect.offsetMax = new Vector2(-44f, 0f);

        headerText = labelGO.GetComponent<TextMeshProUGUI>();
        headerText.font = font;
        headerText.fontStyle = FontStyles.Normal;
        headerText.fontSize = 9f; // Exact original font size
        headerText.color = new Color(0.00f, 0.90f, 1.00f, 0.95f);
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.raycastTarget = false;
        headerText.text = _isCollapsed ? "MISSION" : "TO GO DEEPER:";

        // Objective Body Text (Exact original font 13px, Mint Green #2DFFAA, Normal Weight, Auto-wrapping)
        var bodyGO = new GameObject("ObjectiveText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        bodyGO.transform.SetParent(rootGO.transform, false);
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot     = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(14f, 15f);
        bodyRect.offsetMax = new Vector2(-14f, -58f); // Clean clearance below header

        objectiveBodyText = bodyGO.GetComponent<TextMeshProUGUI>();
        objectiveBodyText.font = font;
        objectiveBodyText.fontStyle = FontStyles.Normal;
        objectiveBodyText.fontSize = 13f;
        objectiveBodyText.enableAutoSizing = true;
        objectiveBodyText.fontSizeMin = 9f;
        objectiveBodyText.fontSizeMax = 13f;
        objectiveBodyText.color = new Color(0.18f, 1.00f, 0.66f, 1.00f);
        objectiveBodyText.alignment = TextAlignmentOptions.Center;
        objectiveBodyText.textWrappingMode = TextWrappingModes.Normal;
        objectiveBodyText.lineSpacing = 1f;
        objectiveBodyText.raycastTarget = false;

        bodyCanvasGroup = bodyGO.GetComponent<CanvasGroup>();
        bodyContainer = bodyGO;

        rootGO.SetActive(true);
        UpdateCollapseVisuals(animate: false);
    }

    private void UpdateLogoVisual()
    {
        if (logoImage == null) return;

        Sprite spriteToUse = missionLogo;

#if UNITY_EDITOR
        if (spriteToUse == null)
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/UI/mission-button.png");
            if (assets != null)
            {
                foreach (var a in assets)
                {
                    if (a is Sprite s && (s.name == "mission-button_4" || s.name.Contains("mission")))
                    {
                        spriteToUse = s;
                        missionLogo = s;
                        break;
                    }
                }
            }
        }
#endif

        if (spriteToUse == null)
        {
            var resSprites = Resources.LoadAll<Sprite>("UI/mission-button");
            if (resSprites != null && resSprites.Length > 0)
            {
                foreach (var s in resSprites)
                {
                    if (s.name == "mission-button_4")
                    {
                        spriteToUse = s;
                        break;
                    }
                }
                if (spriteToUse == null) spriteToUse = resSprites[0];
            }
            if (spriteToUse == null)
            {
                spriteToUse = Resources.Load<Sprite>("UI/mission-button");
            }
        }

        if (spriteToUse != null)
        {
            logoImage.sprite = spriteToUse;
            logoImage.color = logoColor;
            logoImage.enabled = true;
            logoImage.gameObject.SetActive(true);
        }
        else
        {
            logoImage.sprite = GetOrCreateDefaultLogoSprite();
            logoImage.color = logoColor;
            logoImage.enabled = true;
            logoImage.gameObject.SetActive(true);
        }
    }

    private static Sprite _defaultLogoSprite;
    private static Sprite GetOrCreateDefaultLogoSprite()
    {
        if (_defaultLogoSprite != null) return _defaultLogoSprite;

        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0, 0, 0, 0);
        Color cyan = new Color(0.00f, 0.90f, 1.00f, 1.00f);

        float center = (size - 1) * 0.5f;
        float outerRadius = size * 0.44f;
        float ringInnerRadius = size * 0.32f;
        float dotRadius = size * 0.17f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float ringAlpha = Mathf.Clamp01(outerRadius - dist + 0.5f) * Mathf.Clamp01(dist - ringInnerRadius + 0.5f);
                float dotAlpha = Mathf.Clamp01(dotRadius - dist + 0.5f);

                float alpha = Mathf.Max(ringAlpha, dotAlpha);
                if (alpha > 0.01f)
                    tex.SetPixel(x, y, new Color(cyan.r, cyan.g, cyan.b, cyan.a * alpha));
                else
                    tex.SetPixel(x, y, clear);
            }
        }
        tex.Apply();
        _defaultLogoSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _defaultLogoSprite;
    }

    // -----------------------------------------------------------------------
    // Collapse / Expand Logic
    // -----------------------------------------------------------------------

    public void ToggleCollapse()
    {
        SetCollapsed(!_isCollapsed, animate: true);
    }

    public void SetCollapsed(bool collapsed, bool animate = true)
    {
        if (_isCollapsed == collapsed) return;
        _isCollapsed = collapsed;
        AudioManager.Instance?.PlayButtonClick();

        // Disappear INSTANTLY the moment the player taps to close
        if (_isCollapsed)
        {
            SetBodyActive(false);
            if (headerText != null) headerText.gameObject.SetActive(false);
            if (chevronText != null) chevronText.gameObject.SetActive(false);
        }

        UpdateCollapseVisuals(animate);
    }

    private void UpdateCollapseVisuals(bool animate)
    {
        if (panelRect == null) return;

        Vector2 targetSize = _isCollapsed
            ? new Vector2(CollapsedWidth, CollapsedHeight)
            : new Vector2(ExpandedWidth, ExpandedHeight);

        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }

        // Ensure body, header text, and chevron are hidden immediately if collapsed
        if (_isCollapsed)
        {
            SetBodyActive(false);
            if (headerText != null) headerText.gameObject.SetActive(false);
            if (chevronText != null) chevronText.gameObject.SetActive(false);
        }

        if (animate && gameObject.activeInHierarchy)
        {
            _animCoroutine = StartCoroutine(AnimateCollapseRoutine(targetSize, _isCollapsed));
        }
        else
        {
            ApplyLayoutForState(_isCollapsed);
        }
    }

    private void ApplyLayoutForState(bool collapsed)
    {
        if (panelRect == null) return;

        Vector2 targetSize = collapsed
            ? new Vector2(CollapsedWidth, CollapsedHeight)
            : new Vector2(ExpandedWidth, ExpandedHeight);
        panelRect.sizeDelta = targetSize;

        if (headerButton != null)
        {
            var hRect = headerButton.GetComponent<RectTransform>();
            if (hRect != null)
            {
                if (collapsed)
                {
                    // Full button area for the collapsed badge (140x140 box)
                    hRect.anchorMin = Vector2.zero;
                    hRect.anchorMax = Vector2.one;
                    hRect.pivot     = new Vector2(0.5f, 0.5f);
                    hRect.sizeDelta = Vector2.zero;
                    hRect.anchoredPosition = Vector2.zero;
                }
                else
                {
                    // Top header row for the expanded card
                    hRect.anchorMin = new Vector2(0f, 1f);
                    hRect.anchorMax = new Vector2(1f, 1f);
                    hRect.pivot     = new Vector2(0.5f, 1f);
                    hRect.sizeDelta = new Vector2(0f, 38f);
                    hRect.anchoredPosition = new Vector2(0f, -15f);
                }
            }
        }

        if (logoImage != null)
        {
            var lRect = logoImage.rectTransform;
            if (collapsed)
            {
                // Centered 64x64 badge icon with 20f margins on all sides (No text)
                lRect.anchorMin = new Vector2(0.5f, 0.5f);
                lRect.anchorMax = new Vector2(0.5f, 0.5f);
                lRect.pivot     = new Vector2(0.5f, 0.5f);
                lRect.sizeDelta = new Vector2(CollapsedLogoSize, CollapsedLogoSize);
                lRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Compact header icon on left
                lRect.anchorMin = new Vector2(0f, 0.5f);
                lRect.anchorMax = new Vector2(0f, 0.5f);
                lRect.pivot     = new Vector2(0f, 0.5f);
                lRect.sizeDelta = new Vector2(ExpandedLogoSize, ExpandedLogoSize);
                lRect.anchoredPosition = new Vector2(14f, 0f);
            }
        }

        if (headerText != null)
        {
            headerText.gameObject.SetActive(!collapsed);
            if (!collapsed)
            {
                bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                                  && !GameManager.IsTutorialCompleted();
                headerText.text = (inTutorial && _isCustomTutorialObjective) ? "OBJECTIVE" : "TO GO DEEPER";
            }
        }

        if (chevronText != null)
        {
            chevronText.gameObject.SetActive(!collapsed);
            if (!collapsed)
            {
                chevronText.text = "v";
                chevronText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            }
        }

        SetBodyActive(!collapsed);
    }

    private void SetBodyActive(bool active)
    {
        if (bodyContainer != null)
        {
            bodyContainer.SetActive(active);
        }
        else if (objectiveBodyText != null)
        {
            objectiveBodyText.gameObject.SetActive(active);
        }

        if (bodyCanvasGroup != null)
        {
            bodyCanvasGroup.alpha = active ? 1f : 0f;
        }
    }

    private IEnumerator AnimateCollapseRoutine(Vector2 targetSize, bool collapsing)
    {
        Vector2 startSize = panelRect.sizeDelta;
        float duration = 0.18f;
        float elapsed = 0f;

        Vector2 startLogoSize = logoImage != null ? logoImage.rectTransform.sizeDelta : Vector2.one * (collapsing ? ExpandedLogoSize : CollapsedLogoSize);
        Vector2 targetLogoSize = Vector2.one * (collapsing ? CollapsedLogoSize : ExpandedLogoSize);

        if (collapsing)
        {
            SetBodyActive(false);
            if (headerText != null) headerText.gameObject.SetActive(false);
            if (chevronText != null) chevronText.gameObject.SetActive(false);

            if (headerButton != null)
            {
                var hRect = headerButton.GetComponent<RectTransform>();
                if (hRect != null)
                {
                    hRect.anchorMin = Vector2.zero;
                    hRect.anchorMax = Vector2.one;
                    hRect.pivot     = new Vector2(0.5f, 0.5f);
                    hRect.sizeDelta = Vector2.zero;
                    hRect.anchoredPosition = Vector2.zero;
                }
            }

            if (logoImage != null)
            {
                var lRect = logoImage.rectTransform;
                lRect.anchorMin = new Vector2(0.5f, 0.5f);
                lRect.anchorMax = new Vector2(0.5f, 0.5f);
                lRect.pivot     = new Vector2(0.5f, 0.5f);
                lRect.anchoredPosition = Vector2.zero;
            }
        }
        else
        {
            ApplyLayoutForState(false);
            panelRect.sizeDelta = startSize;
            if (logoImage != null) logoImage.rectTransform.sizeDelta = startLogoSize;
            if (headerText != null) headerText.gameObject.SetActive(false);
            if (chevronText != null) chevronText.gameObject.SetActive(false);
            SetBodyActive(false);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            panelRect.sizeDelta = Vector2.Lerp(startSize, targetSize, smoothT);
            if (logoImage != null)
            {
                logoImage.rectTransform.sizeDelta = Vector2.Lerp(startLogoSize, targetLogoSize, smoothT);
            }

            yield return null;
        }

        ApplyLayoutForState(collapsing);

        _animCoroutine = null;
    }

    // -----------------------------------------------------------------------
    // Objective Management
    // -----------------------------------------------------------------------

    /// <summary>
    /// Evaluates current zone progression and updates the dual missions:
    ///   - "- Upgrade submarine Hull" (disappears when hull tier meets requirement)
    ///   - "- Discover at least half of the {zone} species ({discovered}/{total})"
    ///   - When both are completed: entire mission objectives card disappears.
    /// </summary>
    public void RefreshProgressionObjectives()
    {
        EnsureUI();

        if (IsCinematicActive() || IsMinigameActive())
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
            return;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (inTutorial && _isCustomTutorialObjective)
        {
            return;
        }

        int currentZone = ZoneManager.CurrentZoneIndex;
        int nextZone = currentZone + 1;

        // If in deepest zone (Zone 4 / Hadal), there is no deeper zone to unlock
        if (!ZoneConfig.IsValidZone(nextZone))
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
            return;
        }

        ZoneDefinition curZoneDef = ZoneConfig.Zones[currentZone];
        ZoneDefinition nextZoneDef = ZoneConfig.Zones[nextZone];

        // 1. Mission 1: Upgrade submarine Hull
        int currentHull = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;
        int requiredHull = nextZoneDef.requiredHullTier;
        bool hullCompleted = currentHull >= requiredHull;

        // 2. Mission 2: Discover at least half of the (zone) species (no. of discovered/total)
        int totalSpecies = curZoneDef.totalSpeciesCount > 0 ? curZoneDef.totalSpeciesCount : 15;
        int requiredSpecies = Mathf.CeilToInt(totalSpecies * curZoneDef.unlockThreshold);
        int discoveredSpecies = GameManager.Instance != null
            ? GameManager.Instance.GetDiscoveredCountInZone(currentZone)
            : 0;
        bool speciesCompleted = discoveredSpecies >= requiredSpecies;

        // 3. When both missions are completed, the entire mission objectives card disappears!
        if (hullCompleted && speciesCompleted)
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
            CheckAndTriggerObjectivesCleared(currentZone, nextZone, curZoneDef, discoveredSpecies, totalSpecies);
            return;
        }

        // At least one mission is still pending — display the card
        if (panelRect != null && !panelRect.gameObject.activeSelf)
        {
            panelRect.gameObject.SetActive(true);
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // Configure Header
        if (headerText != null)
        {
            headerText.text = _isCollapsed ? "MISSION" : "TO GO DEEPER:";
        }

        // Configure Body Text with exact original sentence casing and clean single newline
        if (objectiveBodyText != null)
        {
            objectiveBodyText.alignment = TextAlignmentOptions.Center;

            string zoneName = curZoneDef.zoneName ?? "Sunlight";
            string cleanZoneName = zoneName.Replace("Zone", "").Trim();

            string bullet1 = "- Upgrade submarine Hull";
            string bullet2 = $"- Discover at least half of the {cleanZoneName} species ({discoveredSpecies}/{totalSpecies})";

            if (!hullCompleted && !speciesCompleted)
            {
                objectiveBodyText.text = $"{bullet1}\n{bullet2}";
            }
            else if (!hullCompleted && speciesCompleted)
            {
                objectiveBodyText.text = bullet1;
            }
            else if (hullCompleted && !speciesCompleted)
            {
                objectiveBodyText.text = bullet2;
            }
        }

        if (panelRect != null)
        {
            panelRect.sizeDelta = _isCollapsed
                ? new Vector2(CollapsedWidth, CollapsedHeight)
                : new Vector2(ExpandedWidth, ExpandedHeight);
        }
    }

    private void CheckAndTriggerObjectivesCleared(int currentZone, int nextZone, ZoneDefinition curZoneDef, int discoveredSpecies, int totalSpecies)
    {
        if (_startupGraceTimer > 0f) return;
        if (ObjectivesClearedPopupUI.HasBeenShown(currentZone)) return;

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();
        if (inTutorial) return;

        if (IsCinematicActive() || IsMinigameActive()) return;
        if (BestiaryDiscoveryPopup.Instance != null && BestiaryDiscoveryPopup.Instance.IsOpen) return;
        if (ShopManager.Instance != null && ShopManager.Instance.IsOpen) return;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;
        if (ZoneSelectionUI.Instance != null && ZoneSelectionUI.Instance.IsOpen) return;
        if (ZoneBoundaryPopupUI.Instance != null && ZoneBoundaryPopupUI.Instance.IsOpen) return;

        ObjectivesClearedPopupUI.Instance.Show(currentZone, nextZone, discoveredSpecies, totalSpecies);
    }

    public void SetObjective(string objective)
    {
        EnsureUI();

        bool isPostTutorialDefault = string.IsNullOrWhiteSpace(objective)
            || objective.Equals(PostTutorialDefaultObjective, StringComparison.OrdinalIgnoreCase)
            || objective.IndexOf("go deeper", StringComparison.OrdinalIgnoreCase) >= 0;

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (isPostTutorialDefault || !inTutorial)
        {
            _isCustomTutorialObjective = false;
            RefreshProgressionObjectives();
            return;
        }

        // Custom tutorial objective
        _isCustomTutorialObjective = true;
        string clean = objective.Replace("📋", "").Trim();
        if (clean.StartsWith("OBJECTIVE:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring("OBJECTIVE:".Length).Trim();
        }

        _currentObjective = clean;

        if (IsCinematicActive() || IsMinigameActive())
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
            return;
        }

        if (headerText != null)
        {
            headerText.text = _isCollapsed ? "OBJECTIVE" : "OBJECTIVE";
        }

        if (objectiveBodyText != null)
        {
            objectiveBodyText.alignment = TextAlignmentOptions.Center;
            objectiveBodyText.text = clean.ToUpperInvariant();
        }

        // When a new tutorial objective arrives, expand it so the player clearly sees what to do
        if (_isCollapsed)
        {
            SetCollapsed(false, animate: true);
        }
        else if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(ExpandedWidth, ExpandedHeight);
        }

        if (panelRect != null) panelRect.gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    public void SetVisible(bool visible)
    {
        EnsureUI();

        if (!visible || IsCinematicActive() || IsMinigameActive())
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
            return;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (inTutorial && _isCustomTutorialObjective)
        {
            if (panelRect != null) panelRect.gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }
        else
        {
            RefreshProgressionObjectives();
        }
    }

    public void Hide()
    {
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (logoImage != null)
        {
            UpdateLogoVisual();
        }
    }
#endif
}
