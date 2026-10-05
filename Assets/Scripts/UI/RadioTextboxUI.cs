using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Procedural non-blocking radio speech box for the interactive tutorial.
/// Implements Section 5A of HadalDescent_Interactive_Tutorial_Story_Plan.md:
///   - Bottom-center docked (880x260, 60px above bottom margin)
///   - Gameplay SDF font, strictly NORMAL WEIGHT (no bold), >= 32px
///   - Speaker Header: 38px, Cyan (#4DDAFF)
///   - Body Text: 34px, Off-White (#EEF2F7) with smooth typewriter reveal (35 char/sec)
///   - Objective Text: 32px, Green (#33FF99)
///   - Integrated [SKIP TUTORIAL] and [SKIP STEP] buttons
///   - Audio: radio static burst on open, radio chirp on BOTH open and close
///   - Tap anywhere on the textbox to instantly skip the typewriter effect
///   - Non-blocking: Time.timeScale remains 1.0f
/// </summary>
public class RadioTextboxUI : MonoBehaviour
{
    private static RadioTextboxUI _instance;
    public static RadioTextboxUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RadioTextboxUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    var go = new GameObject("RadioTextboxUI");
                    if (canvas != null)
                        go.transform.SetParent(canvas.transform, false);
                    _instance = go.AddComponent<RadioTextboxUI>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Optional Custom UI Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text   speakerText;
    [SerializeField] private TMP_Text   bodyText;
    [SerializeField] private TMP_Text   objectiveText;
    [SerializeField] private Button     skipTutorialButton;
    [SerializeField] private Button     skipStepButton;
    [SerializeField] private Button     clickToSkipTypewriterButton;

    // Procedurally generated references
    private RectTransform _panelRect;
    private CanvasGroup   _canvasGroup;
    private TMP_Text      _procSpeakerText;
    private TMP_Text      _procBodyText;

    private Coroutine _typewriterCoroutine;
    private Coroutine _autoDismissCoroutine;
    private Coroutine _autoAdvanceCoroutine;
    private Coroutine _fadeOutCoroutine;

    public bool IsRadioText(TMP_Text text)
    {
        if (text == null) return false;
        return text == speakerText || text == bodyText || text == objectiveText || 
               text == _procSpeakerText || text == _procBodyText;
    }
    private string    _currentFullText = "";
    private bool      _isTyping = false;
    private Action    _onCompleteCallback;

    // Dialogue Paging State
    private readonly System.Collections.Generic.List<string> _dialogPages = new System.Collections.Generic.List<string>();
    private int   _currentDialogPageIndex = 0;
    private float _currentAutoDismissDelay = 3f;

    public bool IsVisible => (panelRoot != null && panelRoot.activeSelf) ||
                             (_panelRect != null && _panelRect.gameObject.activeSelf);

    public event Action OnSkipTutorialRequested = delegate { };
    public event Action OnSkipStepRequested;

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

    private void Update()
    {
        // Instantly hide radio box if a minigame is active to prevent disturbing the minigame
        if (IsVisible && MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            HideInstant();
        }
    }

    /// <summary>
    /// Builds procedural UI elements if custom bindings were not provided in Inspector.
    /// Reverted to original dimensions (880x260, 60px above bottom margin).
    /// </summary>
    public void EnsureUI()
    {
        TMP_FontAsset gameplayFont = UIThemeManager.GameplayFont;

        if (panelRoot != null)
        {
            var customTapBtn = panelRoot.GetComponent<Button>();
            if (customTapBtn == null) customTapBtn = panelRoot.AddComponent<Button>();
            customTapBtn.transition = Selectable.Transition.None;
            customTapBtn.onClick.RemoveListener(OnPanelTapped);
            customTapBtn.onClick.AddListener(OnPanelTapped);

            // Enforce gameplay font and correct wrapping rules on existing inspector-assigned UI
            if (speakerText != null)
            {
                speakerText.font = gameplayFont;
                speakerText.fontStyle = FontStyles.Normal;
                
                // Force anchors so it doesn't overflow
                var rt = speakerText.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-60f, 40f);
                rt.anchoredPosition = new Vector2(30f, -10f);
            }
            if (bodyText != null)
            {
                bodyText.font = gameplayFont;
                bodyText.fontStyle = FontStyles.Normal;
                bodyText.fontSize = 28f;
                bodyText.enableAutoSizing = false;
                bodyText.textWrappingMode = TextWrappingModes.Normal;
                bodyText.overflowMode = TextOverflowModes.Overflow;
                bodyText.lineSpacing = 2f;

                // Force anchors so it wraps cleanly inside the box
                var rt = bodyText.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(30f, 20f);
                rt.offsetMax = new Vector2(-30f, -50f);
            }
            return;
        }

        if (_panelRect != null) return;

        // Find or create Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            var cgo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        TMP_FontAsset font = UIThemeManager.GameplayFont;

        // Root container for Radio Box (880px width x 260px height, anchored at bottom center)
        var rootGO = new GameObject("RadioTextbox_Root", typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvas.transform, false);
        _panelRect = rootGO.GetComponent<RectTransform>();
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();

        _panelRect.anchorMin = new Vector2(0.5f, 0f);
        _panelRect.anchorMax = new Vector2(0.5f, 0f);
        _panelRect.pivot     = new Vector2(0.5f, 0f);
        _panelRect.sizeDelta = new Vector2(880f, 260f);
        _panelRect.anchoredPosition = new Vector2(0f, 60f);

        // Background panel (Deep navy semi-transparent: 0.03, 0.07, 0.14, 0.94)
        var bgImage = rootGO.AddComponent<Image>();
        bgImage.color = new Color(0.03f, 0.07f, 0.14f, 0.94f);
        bgImage.raycastTarget = true;

        // Tap-to-complete-typewriter / tap-to-advance overlay button directly on background
        var tapBtn = rootGO.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        tapBtn.onClick.AddListener(OnPanelTapped);

        // Top border line: 2px cyan line (#00E5FF)
        var borderGO = new GameObject("TopCyanBorder", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(rootGO.transform, false);
        var borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 1f);
        borderRect.anchorMax = new Vector2(1f, 1f);
        borderRect.pivot     = new Vector2(0.5f, 1f);
        borderRect.sizeDelta = new Vector2(0f, 2f);
        borderRect.anchoredPosition = Vector2.zero;
        var bImg = borderGO.GetComponent<Image>();
        bImg.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        bImg.raycastTarget = false;

        // 1. Header Row (38px height)
        var headerGO = new GameObject("HeaderRow", typeof(RectTransform));
        headerGO.transform.SetParent(rootGO.transform, false);
        var headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot     = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(-48f, 38f);
        headerRect.anchoredPosition = new Vector2(0f, -8f);

        // Speaker Title Text (28px, Normal Weight, Cyan #4DDAFF, Strictly "DR. SANTOS" only)
        var speakerGO = new GameObject("SpeakerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        speakerGO.transform.SetParent(headerGO.transform, false);
        var speakerRect = speakerGO.GetComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0f, 0f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot     = new Vector2(0f, 0.5f);
        speakerRect.sizeDelta = Vector2.zero;
        speakerRect.anchoredPosition = Vector2.zero;

        _procSpeakerText = speakerGO.GetComponent<TextMeshProUGUI>();
        _procSpeakerText.font = font;
        _procSpeakerText.fontStyle = FontStyles.Normal; // Strictly no bold
        _procSpeakerText.fontSize = 28f;
        _procSpeakerText.color = new Color(0.30f, 0.85f, 1.00f, 1.00f);
        _procSpeakerText.alignment = TextAlignmentOptions.MidlineLeft;
        _procSpeakerText.raycastTarget = false;
        _procSpeakerText.text = "DR. SANTOS";

        // Divider Line 1
        var div1 = new GameObject("Divider1", typeof(RectTransform), typeof(Image));
        div1.transform.SetParent(rootGO.transform, false);
        var div1Rect = div1.GetComponent<RectTransform>();
        div1Rect.anchorMin = new Vector2(0f, 1f);
        div1Rect.anchorMax = new Vector2(1f, 1f);
        div1Rect.sizeDelta = new Vector2(-48f, 1f);
        div1Rect.anchoredPosition = new Vector2(0f, -46f);
        var d1Img = div1.GetComponent<Image>();
        d1Img.color = new Color(0.00f, 0.90f, 1.00f, 0.20f);
        d1Img.raycastTarget = false;

        // 2. Body Text Area (28px, Normal Weight, Off-White #EEF2F7, Full-word Wrapping)
        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(rootGO.transform, false);
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot     = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(30f, 20f);
        bodyRect.offsetMax = new Vector2(-30f, -50f); // Below header/divider

        _procBodyText = bodyGO.GetComponent<TextMeshProUGUI>();
        _procBodyText.font = font;
        _procBodyText.fontStyle = FontStyles.Normal;
        _procBodyText.fontSize = 28f;
        _procBodyText.enableAutoSizing = false;
        _procBodyText.color = new Color(0.93f, 0.95f, 0.97f, 1.00f);
        _procBodyText.alignment = TextAlignmentOptions.TopLeft;
        _procBodyText.textWrappingMode = TextWrappingModes.Normal;
        _procBodyText.overflowMode = TextOverflowModes.Overflow;
        _procBodyText.lineSpacing = 2f;
        _procBodyText.raycastTarget = false;
    }

    private struct QueuedRadioMessage
    {
        public string Speaker;
        public string Dialogue;
        public string Objective;
        public Action OnComplete;
        public bool ShowSkipStep;
        public float AutoDismissDelay;
    }

    private readonly System.Collections.Generic.Queue<QueuedRadioMessage> _messageQueue = 
        new System.Collections.Generic.Queue<QueuedRadioMessage>();

    private string _currentRawDialogue = "";

    public bool IsBusy => IsVisible && (_isTyping || _autoAdvanceCoroutine != null || _autoDismissCoroutine != null);
    public bool IsActiveAndTyping => IsVisible && _isTyping;

    /// <summary>
    /// Displays a new radio transmission with typewriter effect.
    /// If another transmission is already playing, queues it so the previous text completes cleanly.
    /// Long dialogue is cleanly paginated to prevent overflow and auto-advances.
    /// Objective text is forwarded to the dedicated ObjectiveHUDUI below Sonar Range box.
    /// Once the final line finishes typing, it stays visible for autoDismissDelay before dismissing and calling onComplete.
    /// </summary>
    public void DisplayMessage(string speaker, string dialogue, string objective = "", Action onComplete = null, bool showSkipStep = false, float autoDismissDelay = 1.5f, bool forceImmediate = false)
    {
        if (string.IsNullOrEmpty(dialogue)) return;

        // If this exact dialogue is already actively being displayed, don't restart it (prevents Update loop re-triggering)
        if (IsVisible && _currentRawDialogue == dialogue)
        {
            return;
        }

        // If busy and not forced immediate, queue this message so the previous radio box finishes completely!
        if (IsBusy && !forceImmediate)
        {
            foreach (var q in _messageQueue)
            {
                if (q.Dialogue == dialogue) return;
            }

            _messageQueue.Enqueue(new QueuedRadioMessage
            {
                Speaker = speaker,
                Dialogue = dialogue,
                Objective = objective,
                OnComplete = onComplete,
                ShowSkipStep = showSkipStep,
                AutoDismissDelay = autoDismissDelay
            });
            return;
        }

        if (forceImmediate)
        {
            _messageQueue.Clear();
        }

        DisplayMessageInternal(speaker, dialogue, objective, onComplete, showSkipStep, autoDismissDelay);
    }

    private void DisplayMessageInternal(string speaker, string dialogue, string objective, Action onComplete, bool showSkipStep, float autoDismissDelay)
    {
        EnsureUI();

        if (_typewriterCoroutine != null) { StopCoroutine(_typewriterCoroutine); _typewriterCoroutine = null; }
        if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }
        if (_autoAdvanceCoroutine != null) { StopCoroutine(_autoAdvanceCoroutine); _autoAdvanceCoroutine = null; }
        if (_fadeOutCoroutine != null) { StopCoroutine(_fadeOutCoroutine); _fadeOutCoroutine = null; }

        _currentRawDialogue = dialogue;
        _onCompleteCallback = onComplete;
        _currentAutoDismissDelay = autoDismissDelay > 0f ? autoDismissDelay : 1.5f;

        // Apply Speaker: strictly "DR. SANTOS" only
        string spk = string.IsNullOrEmpty(speaker) || speaker.IndexOf("Santos", StringComparison.OrdinalIgnoreCase) >= 0
            ? "DR. SANTOS"
            : speaker;
        if (speakerText != null) speakerText.text = spk;
        if (_procSpeakerText != null) _procSpeakerText.text = spk;

        // Forward Objective to dedicated ObjectiveHUDUI below Sonar Range box
        if (!string.IsNullOrEmpty(objective))
        {
            ObjectiveHUDUI.Instance?.SetObjective(objective);
        }

        // Hide legacy embedded objective fields if any
        if (objectiveText != null) objectiveText.gameObject.SetActive(false);

        // Show panel
        if (panelRoot != null) panelRoot.SetActive(true);
        if (_panelRect != null) _panelRect.gameObject.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        // Dialogue Paging: Split into natural sentence-boundary pages that fit comfortably in the textbox
        _dialogPages.Clear();
        var pages = SplitIntoPages(dialogue, 150);
        _dialogPages.AddRange(pages);

        _currentDialogPageIndex = 0;

        // SFX: Static burst on appearance, chirp on open
        AudioManager.Instance?.PlayRadioStatic();
        AudioManager.Instance?.PlayRadioChirp();

        ShowCurrentPage();
    }

    private void ShowCurrentPage()
    {
        if (_currentDialogPageIndex < 0 || _currentDialogPageIndex >= _dialogPages.Count) return;

        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        if (_autoDismissCoroutine != null) StopCoroutine(_autoDismissCoroutine);
        if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);

        _currentFullText = _dialogPages[_currentDialogPageIndex];
        bool hasNextPage = _currentDialogPageIndex < _dialogPages.Count - 1;

        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(_currentFullText, hasNextPage));
    }

    public static bool IsGamePaused => (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) || Time.timeScale <= 0f;

    private IEnumerator WaitWhileUnpaused(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (!IsGamePaused)
            {
                timer += Time.unscaledDeltaTime;
            }
            yield return null;
        }
    }

    private IEnumerator TypewriterRoutine(string fullText, bool hasNextPage)
    {
        _isTyping = true;
        SetBodyText("");

        const float charsPerSecond = 35f;
        float delay = 1f / charsPerSecond;

        for (int i = 0; i <= fullText.Length; i++)
        {
            while (IsGamePaused)
            {
                yield return null;
            }

            SetBodyText(fullText.Substring(0, i));

            float t = 0f;
            while (t < delay)
            {
                if (!IsGamePaused)
                {
                    t += Time.unscaledDeltaTime;
                }
                yield return null;
            }
        }

        _isTyping = false;
        _typewriterCoroutine = null;

        // The radio box remains on screen until the player taps on it.
        // No timer auto-advance or auto-dismiss.
    }

    private IEnumerator AutoAdvanceRoutine(float delay)
    {
        yield return StartCoroutine(WaitWhileUnpaused(delay));
        _autoAdvanceCoroutine = null;
        AdvanceToNextPage();
    }

    private void AdvanceToNextPage()
    {
        if (_autoAdvanceCoroutine != null)
        {
            StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
        }

        if (_currentDialogPageIndex < _dialogPages.Count - 1)
        {
            _currentDialogPageIndex++;
            ShowCurrentPage();
        }
    }

    private void SetBodyText(string text)
    {
        if (bodyText != null) bodyText.text = text;
        if (_procBodyText != null) _procBodyText.text = text;
    }

    private void OnPanelTapped()
    {
        if (IsGamePaused) return;

        if (_isTyping)
        {
            // Instantly complete typewriter for the active page
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;
            SetBodyText(_currentFullText);

            if (_autoAdvanceCoroutine != null) { StopCoroutine(_autoAdvanceCoroutine); _autoAdvanceCoroutine = null; }
            if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }
            // Stays on screen waiting for the player's next tap to advance or dismiss
        }
        else
        {
            if (_autoDismissCoroutine != null)
            {
                StopCoroutine(_autoDismissCoroutine);
                _autoDismissCoroutine = null;
            }
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }

            // If already done typing and more pages exist, tap advances to next page
            if (_currentDialogPageIndex < _dialogPages.Count - 1)
            {
                AdvanceToNextPage();
            }
            else
            {
                // Final page: tap dismisses the radio box and invokes callback or advances to next queued message
                var cb = _onCompleteCallback;
                _onCompleteCallback = null;

                if (_messageQueue.Count > 0)
                {
                    cb?.Invoke();
                    var next = _messageQueue.Dequeue();
                    DisplayMessageInternal(next.Speaker, next.Dialogue, next.Objective, next.OnComplete, next.ShowSkipStep, next.AutoDismissDelay);
                }
                else
                {
                    Hide();
                    cb?.Invoke();
                }
            }
        }
    }

    private IEnumerator AutoDismissRoutine(float delay)
    {
        yield return StartCoroutine(WaitWhileUnpaused(delay));
        _autoDismissCoroutine = null;

        var cb = _onCompleteCallback;
        _onCompleteCallback = null;

        if (_messageQueue.Count > 0)
        {
            cb?.Invoke();
            var next = _messageQueue.Dequeue();
            DisplayMessageInternal(next.Speaker, next.Dialogue, next.Objective, next.OnComplete, next.ShowSkipStep, next.AutoDismissDelay);
        }
        else
        {
            var callback = cb;
            Hide();
            callback?.Invoke();
        }
    }

    /// <summary>
    /// Smoothly dismisses the radio box with a radio chirp sound on close.
    /// </summary>
    public void Hide()
    {
        if (!IsVisible) return;

        // Play chirp sound on close
        AudioManager.Instance?.PlayRadioChirp();

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        if (_autoDismissCoroutine != null)
        {
            StopCoroutine(_autoDismissCoroutine);
            _autoDismissCoroutine = null;
        }

        if (_autoAdvanceCoroutine != null)
        {
            StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
        }

        if (_fadeOutCoroutine != null)
        {
            StopCoroutine(_fadeOutCoroutine);
            _fadeOutCoroutine = null;
        }

        _fadeOutCoroutine = StartCoroutine(FadeOutRoutine());
    }

    public void HideInstant()
    {
        if (_typewriterCoroutine != null) { StopCoroutine(_typewriterCoroutine); _typewriterCoroutine = null; }
        if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }
        if (_autoAdvanceCoroutine != null) { StopCoroutine(_autoAdvanceCoroutine); _autoAdvanceCoroutine = null; }
        if (_fadeOutCoroutine != null) { StopCoroutine(_fadeOutCoroutine); _fadeOutCoroutine = null; }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (_panelRect != null) _panelRect.gameObject.SetActive(false);
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    private IEnumerator FadeOutRoutine()
    {
        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            const float duration = 0.25f;
            while (elapsed < duration)
            {
                if (!IsGamePaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                }
                yield return null;
            }
        }

        _fadeOutCoroutine = null;
        HideInstant();
    }

    /// <summary>
    /// Toggles the [SKIP STEP] button visibility for Tier 3 non-compliance safeguard (60s+).
    /// </summary>
    public void SetSkipStepVisible(bool visible)
    {
        if (skipStepButton != null) skipStepButton.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Splits dialogue into natural sentence-boundary pages that fit comfortably in the textbox without overflowing.
    /// </summary>
    private static System.Collections.Generic.List<string> SplitIntoPages(string text, int maxCharsPerPage = 150)
    {
        var pages = new System.Collections.Generic.List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            pages.Add("");
            return pages;
        }

        string trimmed = text.Trim();
        if (trimmed.Length <= maxCharsPerPage)
        {
            pages.Add(trimmed);
            return pages;
        }

        // Split by sentences ending with . ! ? followed by space
        string[] sentences = System.Text.RegularExpressions.Regex.Split(trimmed, @"(?<=[.!?])\s+");
        var currentPage = new System.Text.StringBuilder();

        foreach (string s in sentences)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;

            if (currentPage.Length == 0)
            {
                if (s.Length <= maxCharsPerPage)
                {
                    currentPage.Append(s);
                }
                else
                {
                    var wordPages = SplitByWords(s, maxCharsPerPage);
                    for (int i = 0; i < wordPages.Count - 1; i++)
                        pages.Add(wordPages[i]);
                    currentPage.Append(wordPages[wordPages.Count - 1]);
                }
            }
            else
            {
                if (currentPage.Length + 1 + s.Length <= maxCharsPerPage)
                {
                    currentPage.Append(" ").Append(s);
                }
                else
                {
                    pages.Add(currentPage.ToString());
                    currentPage.Clear();
                    if (s.Length <= maxCharsPerPage)
                    {
                        currentPage.Append(s);
                    }
                    else
                    {
                        var wordPages = SplitByWords(s, maxCharsPerPage);
                        for (int i = 0; i < wordPages.Count - 1; i++)
                            pages.Add(wordPages[i]);
                        currentPage.Append(wordPages[wordPages.Count - 1]);
                    }
                }
            }
        }

        if (currentPage.Length > 0)
        {
            pages.Add(currentPage.ToString());
        }

        return pages.Count > 0 ? pages : new System.Collections.Generic.List<string> { trimmed };
    }

    private static System.Collections.Generic.List<string> SplitByWords(string sentence, int maxChars)
    {
        var result = new System.Collections.Generic.List<string>();
        string[] words = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();

        foreach (var w in words)
        {
            if (sb.Length == 0)
            {
                sb.Append(w);
            }
            else if (sb.Length + 1 + w.Length <= maxChars)
            {
                sb.Append(" ").Append(w);
            }
            else
            {
                result.Add(sb.ToString());
                sb.Clear();
                sb.Append(w);
            }
        }

        if (sb.Length > 0)
        {
            result.Add(sb.ToString());
        }

        return result;
    }
}
