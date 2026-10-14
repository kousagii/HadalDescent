using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Celebratory confirmation popup shown when the player clears all progression objectives for a zone:
///   - Submarine Hull upgraded to required tier for the next zone.
///   - At least 50% of the current zone's species discovered.
///
/// Features:
///   - Header: "OBJECTIVES CLEARED"
///   - Body: "You can now go to the next zone! Or complete the species on the {zoneName} ({discovered}/{total})."
///   - Buttons: "Stay" (continue exploring current zone) and "Go" (descend to next zone).
///   - Beautiful high-tech deep sea modal design with dark ocean glassmorphism and cyan/mint glow accents.
///   - Self-bootstrapping and self-contained: builds UI automatically if not assigned in Inspector.
/// </summary>
public class ObjectivesClearedPopupUI : MonoBehaviour
{
    private static ObjectivesClearedPopupUI _instance;
    public static ObjectivesClearedPopupUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ObjectivesClearedPopupUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    Canvas canvas = UIManager.GetExplorationCanvas() ?? FindFirstObjectByType<Canvas>();
                    var go = new GameObject("ObjectivesClearedPopupUI");
                    if (canvas != null)
                    {
                        go.transform.SetParent(canvas.transform, false);
                    }
                    _instance = go.AddComponent<ObjectivesClearedPopupUI>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Inspector References (Optional - Auto-built if null)")]
    [Tooltip("Root popup GameObject containing the modal card and overlay.")]
    [SerializeField] private GameObject popupPanel;

    [Tooltip("Header text displaying 'OBJECTIVES CLEARED'.")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("Body text describing the next zone and remaining species.")]
    [SerializeField] private TMP_Text bodyText;

    [Tooltip("Button to remain in the current zone and keep exploring.")]
    [SerializeField] private Button stayButton;

    [Tooltip("Button to proceed immediately to the next zone.")]
    [SerializeField] private Button goButton;

    private CanvasGroup   _canvasGroup;
    private RectTransform _cardRect;
    private bool          _isOpen;
    private int           _currentZoneIndex;
    private int           _nextZoneIndex;
    private Action        _onStayAction;
    private Action        _onGoAction;
    private Coroutine     _animateCoroutine;

    /// <summary>Returns true if the Objectives Cleared popup is currently open and visible.</summary>
    public bool IsOpen => _isOpen && popupPanel != null && popupPanel.activeSelf;

    public const string SaveKeyPrefix = "Save_ObjectivesCleared_Shown_";

    /// <summary>
    /// Checks whether the Objectives Cleared popup has already been shown for the given zone.
    /// </summary>
    public static bool HasBeenShown(int zoneIndex)
    {
        return PlayerPrefs.GetInt($"{SaveKeyPrefix}{zoneIndex}", 0) == 1;
    }

    /// <summary>
    /// Marks the Objectives Cleared popup as having been shown for the given zone.
    /// </summary>
    public static void MarkAsShown(int zoneIndex)
    {
        PlayerPrefs.SetInt($"{SaveKeyPrefix}{zoneIndex}", 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Resets the shown flag for a specific zone (e.g. for testing or new game).
    /// </summary>
    public static void ResetShownForZone(int zoneIndex)
    {
        PlayerPrefs.DeleteKey($"{SaveKeyPrefix}{zoneIndex}");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Resets the shown flag for all zones.
    /// </summary>
    public static void ResetAllZones()
    {
        for (int i = 0; i < 5; i++)
        {
            PlayerPrefs.DeleteKey($"{SaveKeyPrefix}{i}");
        }
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        AutoWireOrBuildUI();
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void Start()
    {
        AutoWireOrBuildUI();
        if (!_isOpen && popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_isOpen)
        {
            Time.timeScale = 1f;
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Displays the Objectives Cleared popup with zone progression context.
    /// </summary>
    /// <param name="currentZone">Current zone index (0=Sunlight, 1=Twilight, etc.)</param>
    /// <param name="nextZone">Next zone index</param>
    /// <param name="discoveredSpecies">Current discovered count in zone</param>
    /// <param name="totalSpecies">Total species in zone</param>
    public void Show(int currentZone, int nextZone, int discoveredSpecies = -1, int totalSpecies = -1)
    {
        _currentZoneIndex = currentZone;
        _nextZoneIndex    = nextZone;

        // Auto-resolve counts if not provided
        if (curZoneData(currentZone, out ZoneDefinition curZoneDef))
        {
            if (totalSpecies < 0)
                totalSpecies = curZoneDef.totalSpeciesCount > 0 ? curZoneDef.totalSpeciesCount : 15;
            if (discoveredSpecies < 0)
                discoveredSpecies = GameManager.Instance != null
                    ? GameManager.Instance.GetDiscoveredCountInZone(currentZone)
                    : 0;
        }

        string zoneName = curZoneDef != null && !string.IsNullOrEmpty(curZoneDef.zoneName)
            ? curZoneDef.zoneName
            : "Sunlight Zone";

        string bodyMsg;
        if (discoveredSpecies < totalSpecies)
        {
            bodyMsg = $"You can now go to the next zone! Or complete the species on the {zoneName} ({discoveredSpecies}/{totalSpecies}).";
        }
        else
        {
            bodyMsg = $"You can now go to the next zone! All species on the {zoneName} have been discovered ({discoveredSpecies}/{totalSpecies}).";
        }

        ShowCustom("OBJECTIVES CLEARED", bodyMsg,
            onStay: () => {
                Debug.Log($"[ObjectivesClearedPopup] Player chose to stay in {zoneName}.");
            },
            onGo: () => {
                Debug.Log($"[ObjectivesClearedPopup] Player proceeding to Zone {nextZone}!");
                if (ZoneManager.Instance != null)
                {
                    ZoneManager.Instance.LoadZone(nextZone, enteredFromAbove: true);
                }
                else if (ZoneConfig.IsValidZone(nextZone))
                {
                    LoadingScreenUI.LoadScene(ZoneConfig.Zones[nextZone].sceneName);
                }
            }
        );

        MarkAsShown(currentZone);
    }

    private static bool curZoneData(int zoneIdx, out ZoneDefinition def)
    {
        if (ZoneConfig.IsValidZone(zoneIdx))
        {
            def = ZoneConfig.Zones[zoneIdx];
            return true;
        }
        def = null;
        return false;
    }

    /// <summary>
    /// Displays the Objectives Cleared popup with custom header, body, and callbacks.
    /// </summary>
    public void ShowCustom(string header, string body, Action onStay, Action onGo)
    {
        AutoWireOrBuildUI();

        _onStayAction = onStay;
        _onGoAction   = onGo;

        if (headerText != null)
        {
            headerText.text = header;
        }

        if (bodyText != null)
        {
            bodyText.text = body;
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling();
        }

        _isOpen = true;

        // Play celebration audio feedback
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySpeciesScannedSuccessSFX();
        }

        // Animate entrance smoothly
        if (_animateCoroutine != null) StopCoroutine(_animateCoroutine);
        _animateCoroutine = StartCoroutine(AnimateEntrance());

        // Pause world gameplay while deciding
        Time.timeScale = 0f;
    }

    private IEnumerator AnimateEntrance()
    {
        float duration = 0.22f;
        float elapsed  = 0f;

        Vector3 startScale = new Vector3(0.92f, 0.92f, 1f);
        Vector3 endScale   = Vector3.one;

        if (_cardRect != null) _cardRect.localScale = startScale;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth ease out cubic
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            if (_cardRect != null)
                _cardRect.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, ease);

            yield return null;
        }

        if (_cardRect != null) _cardRect.localScale = endScale;
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        _animateCoroutine = null;
    }

    /// <summary>
    /// Hides the popup and unpauses gameplay.
    /// </summary>
    public void Hide()
    {
        _isOpen = false;
        if (_animateCoroutine != null)
        {
            StopCoroutine(_animateCoroutine);
            _animateCoroutine = null;
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    private void OnStayClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
        Hide();
        _onStayAction?.Invoke();
    }

    private void OnGoClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
        Hide();
        _onGoAction?.Invoke();
    }

    private void AutoWireOrBuildUI()
    {
        if (popupPanel != null && headerText != null && bodyText != null && stayButton != null && goButton != null)
        {
            return;
        }

        // 1. Try finding existing panel under Canvas
        Canvas canvas = GetComponentInParent<Canvas>() 
                     ?? UIManager.GetExplorationCanvas() 
                     ?? FindFirstObjectByType<Canvas>();

        if (canvas == null) return;

        if (popupPanel == null)
        {
            Transform existing = canvas.transform.Find("ObjectivesClearedPopup_Root");
            if (existing != null)
            {
                popupPanel = existing.gameObject;
            }
        }

        if (popupPanel == null)
        {
            BuildDefaultUI(canvas);
        }
        else
        {
            WireExistingUI();
        }
    }

    private void WireExistingUI()
    {
        if (popupPanel == null) return;

        _cardRect = popupPanel.GetComponent<RectTransform>();
        _canvasGroup = popupPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = popupPanel.AddComponent<CanvasGroup>();

        if (headerText == null)
            headerText = popupPanel.transform.Find("Card/HeaderLabel")?.GetComponent<TMP_Text>()
                      ?? popupPanel.transform.Find("HeaderLabel")?.GetComponent<TMP_Text>();

        if (bodyText == null)
            bodyText = popupPanel.transform.Find("Card/BodyText")?.GetComponent<TMP_Text>()
                    ?? popupPanel.transform.Find("BodyText")?.GetComponent<TMP_Text>();

        if (stayButton == null)
        {
            var btnTr = popupPanel.transform.Find("Card/StayBtn") ?? popupPanel.transform.Find("StayBtn");
            if (btnTr != null) stayButton = btnTr.GetComponent<Button>();
        }

        if (goButton == null)
        {
            var btnTr = popupPanel.transform.Find("Card/GoBtn") ?? popupPanel.transform.Find("GoBtn");
            if (btnTr != null) goButton = btnTr.GetComponent<Button>();
        }

        if (stayButton != null)
        {
            stayButton.onClick.RemoveAllListeners();
            stayButton.onClick.AddListener(OnStayClicked);
        }

        if (goButton != null)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(OnGoClicked);
        }
    }

    private void BuildDefaultUI(Canvas canvas)
    {
        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // 1. Root Overlay GameObject
        var rootGO = new GameObject("ObjectivesClearedPopup_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();

        popupPanel = rootGO;
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();

        var rootRect = rootGO.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;

        // Overlay backdrop tint: semi-transparent dark ocean tint blocking world clicks
        var overlayImg = rootGO.GetComponent<Image>();
        overlayImg.color = new Color(0.02f, 0.04f, 0.08f, 0.78f);
        overlayImg.raycastTarget = true;

        // Optional separate Canvas on the modal to guarantee topmost rendering order
        var subCanvas = rootGO.AddComponent<Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = 32000;
        rootGO.AddComponent<GraphicRaycaster>();

        // 2. Central Card Panel (780x430)
        var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(rootGO.transform, false);
        _cardRect = cardGO.GetComponent<RectTransform>();
        _cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        _cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        _cardRect.pivot     = new Vector2(0.5f, 0.5f);
        _cardRect.sizeDelta = new Vector2(780f, 430f);
        _cardRect.anchoredPosition = Vector2.zero;

        var cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.04f, 0.08f, 0.15f, 0.98f);
        cardImg.raycastTarget = true;

        // 3. Top Glowing Cyan Border Line (#00E5FF, 3px)
        var topBorderGO = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        topBorderGO.transform.SetParent(cardGO.transform, false);
        var tbRect = topBorderGO.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0f, 1f);
        tbRect.anchorMax = new Vector2(1f, 1f);
        tbRect.pivot     = new Vector2(0.5f, 1f);
        tbRect.sizeDelta = new Vector2(0f, 3f);
        tbRect.anchoredPosition = Vector2.zero;

        var tbImg = topBorderGO.GetComponent<Image>();
        tbImg.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        tbImg.raycastTarget = false;

        // 4. Header Label: "OBJECTIVES CLEARED" (38px, Mint Green #2DFFAA, Bold)
        var headerGO = new GameObject("HeaderLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGO.transform.SetParent(cardGO.transform, false);
        var hRect = headerGO.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot     = new Vector2(0.5f, 1f);
        hRect.sizeDelta = new Vector2(0f, 50f);
        hRect.anchoredPosition = new Vector2(0f, -40f);

        headerText = headerGO.GetComponent<TextMeshProUGUI>();
        headerText.font = font;
        headerText.fontSize = 38f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = new Color(0.18f, 1.00f, 0.66f, 1.00f); // Vibrant Mint Green
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.text = "OBJECTIVES CLEARED";
        headerText.raycastTarget = false;

        // 5. Decorative Divider Line (680px x 1.5px, subtle cyan glow)
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(cardGO.transform, false);
        var divRect = divGO.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 1f);
        divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot     = new Vector2(0.5f, 1f);
        divRect.sizeDelta = new Vector2(680f, 1.5f);
        divRect.anchoredPosition = new Vector2(0f, -96f);

        var divImg = divGO.GetComponent<Image>();
        divImg.color = new Color(0.00f, 0.90f, 1.00f, 0.35f);
        divImg.raycastTarget = false;

        // 6. Body Text
        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(cardGO.transform, false);
        var bRect = bodyGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot     = new Vector2(0.5f, 0.5f);
        bRect.sizeDelta = new Vector2(680f, 140f);
        bRect.anchoredPosition = new Vector2(0f, 15f);

        bodyText = bodyGO.GetComponent<TextMeshProUGUI>();
        bodyText.font = font;
        bodyText.fontSize = 26f;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 20f;
        bodyText.fontSizeMax = 28f;
        bodyText.color = new Color(0.92f, 0.97f, 1.00f, 1.00f); // Ice ocean white
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 6f;
        bodyText.paragraphSpacing = 6f;
        bodyText.raycastTarget = false;

        // 7. Buttons: "Stay" and "Go"
        const float btnWidth  = 300f;
        const float btnHeight = 68f;
        const float btnY      = -140f;

        // Stay Button (Left, Slate Navy)
        stayButton = CreateButton(
            cardGO.transform,
            name: "StayBtn",
            label: "Stay",
            position: new Vector2(-170f, btnY),
            size: new Vector2(btnWidth, btnHeight),
            bgColor: new Color(0.10f, 0.18f, 0.28f, 1.00f),
            hoverColor: new Color(0.16f, 0.28f, 0.42f, 1.00f),
            textColor: new Color(0.72f, 0.88f, 1.00f, 1.00f),
            outlineColor: new Color(0.20f, 0.50f, 0.70f, 0.80f),
            onClick: OnStayClicked,
            font: font
        );

        // Go Button (Right, Radiant Cyan/Blue)
        goButton = CreateButton(
            cardGO.transform,
            name: "GoBtn",
            label: "Go",
            position: new Vector2(170f, btnY),
            size: new Vector2(btnWidth, btnHeight),
            bgColor: new Color(0.00f, 0.58f, 0.90f, 1.00f),
            hoverColor: new Color(0.15f, 0.70f, 1.00f, 1.00f),
            textColor: Color.white,
            outlineColor: new Color(0.00f, 0.90f, 1.00f, 0.90f),
            onClick: OnGoClicked,
            font: font
        );

        rootGO.SetActive(false);
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Color bgColor,
        Color hoverColor,
        Color textColor,
        Color outlineColor,
        UnityEngine.Events.UnityAction onClick,
        TMP_FontAsset font)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var img = btnGO.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        // Subtle 1.5px Outline image border
        var outlineGO = new GameObject("Outline", typeof(RectTransform), typeof(Outline));
        outlineGO.transform.SetParent(btnGO.transform, false);
        var ol = outlineGO.GetComponent<Outline>();
        ol.effectColor = outlineColor;
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = hoverColor;
        cb.pressedColor     = bgColor * 0.75f;
        cb.selectedColor    = bgColor;
        btn.colors = cb;

        btn.onClick.AddListener(onClick);

        // Button Text
        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(btnGO.transform, false);

        var tRect = textGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        return btn;
    }
}
