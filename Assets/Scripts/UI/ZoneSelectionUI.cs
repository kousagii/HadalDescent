using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the Ocean Zone Selection Screen (Figures 2, 3, 7, and 8).
///
/// Features:
///   - Displays all 5 ocean zones with depths, research progress, and required hull tiers.
///   - Evaluates unlock status: Zone N requires 50% species in Zone N-1 + required Hull Tier.
///   - Clicking an unlocked zone loads the respective zone scene.
///   - Clicking a locked zone displays the "Pressure Threshold Exceeded" warning modal.
///   - Supports custom Inspector canvas hierarchy or automatic procedural 36pt Poppins SDF UI.
/// </summary>
public class ZoneSelectionUI : MonoBehaviour
{
    public static ZoneSelectionUI Instance { get; private set; }

    /// <summary>
    /// When set to true (e.g. upon completing or skipping the tutorial),
    /// the active zone card action button displays "PROCEED" instead of "◀ GO BACK".
    /// Automatically resets to false as soon as the user selects a zone or closes the menu.
    /// </summary>
    public static bool IsAfterTutorialFlow { get; set; } = false;

    [System.Serializable]
    public class ZoneCardBinding
    {
        public int zoneIndex;
        public GameObject cardRoot;
        public TMP_Text   titleText;
        public TMP_Text   depthText;
        public TMP_Text   descText;
        public Image      zoneScreenshotImage;
        public GameObject zoneScreenshotPlaceholder;
        public RectTransform infoContainer;
        public RectTransform accessSpan;
        public RectTransform progressSpan;
        public TMP_Text   speciesProgressText;
        public Slider     progressBar;
        public Image      progressFill;
        public TMP_Text   hullReqText;
        public Image      hullReqBg;
        public Button     enterButton;
        public TMP_Text   enterButtonText;
        public GameObject lockOverlay;
    }

    [Header("Typography")]
    [Tooltip("Aloha font asset (AlohaPop SDF) used across the entire Zone Selection UI.")]
    [SerializeField] private TMP_FontAsset alohaFontAsset;

    [Header("Zone Screenshots (Optional)")]
    [Tooltip("Optional screenshots for each zone (0 = Sunlight, 1 = Twilight, 2 = Midnight, 3 = Abyss, 4 = Hadal). If not assigned in Inspector, the UI will attempt to load from Resources/UI/ZoneScreenshots/")]
    [SerializeField] private Sprite[] zoneScreenshots = new Sprite[ZoneConfig.ZoneCount];

    [Header("Custom UI Elements (Optional)")]
    [SerializeField] private GameObject customRoot;
    [SerializeField] private Button     customBackButton;
    [SerializeField] private TMP_Text   customRdpText;
    [SerializeField] private TMP_Text   customHullTierText;
    [SerializeField] private List<ZoneCardBinding> customCards = new List<ZoneCardBinding>();

    [Header("Custom Pressure Warning Modal (Optional)")]
    [SerializeField] private GameObject customWarningModal;
    [SerializeField] private TMP_Text   customWarningTitleText;
    [SerializeField] private TMP_Text   customWarningDescText;
    [SerializeField] private Button     customWarningCloseButton;
    [SerializeField] private Button     customWarningShopButton;

    // Procedural UI References
    private GameObject _proceduralCanvasGO;
    private GameObject _proceduralRoot;
    private GameObject _proceduralWarningModal;
    private TMP_Text   _proceduralWarningTitle;
    private TMP_Text   _proceduralWarningDesc;
    private Button     _proceduralWarningShopBtn;
    private TMP_Text   _proceduralWarningShopBtnText;
    private TMP_Text   _proceduralRdpText;
    private TMP_Text   _proceduralHullText;
    private Button     _proceduralBackBtn;
    private TMP_Text   _proceduralBackBtnText;
    private List<ZoneCardBinding> _proceduralCards = new List<ZoneCardBinding>();

    private int _selectedLockedZone = -1;

    public TMP_FontAsset GetAntoneFont() => GetAlohaFont();
    public TMP_FontAsset GetAlohaFont()
    {
        if (alohaFontAsset != null) return alohaFontAsset;

        alohaFontAsset = UIThemeManager.AntoneFont;
        return alohaFontAsset;
    }

    public Sprite GetZoneScreenshot(int zoneIndex)
    {
        if (zoneScreenshots != null && zoneIndex >= 0 && zoneIndex < zoneScreenshots.Length && zoneScreenshots[zoneIndex] != null)
        {
            return zoneScreenshots[zoneIndex];
        }

        if (ZoneConfig.IsValidZone(zoneIndex))
        {
            string sceneName = ZoneConfig.Zones[zoneIndex].sceneName;
            // 1. Check Resources/UI/ZoneScreenshots/{sceneName} (e.g. SunlightZone)
            Sprite resSprite = Resources.Load<Sprite>($"UI/ZoneScreenshots/{sceneName}");
            if (resSprite != null) return resSprite;

            // 2. Check Resources/UI/ZoneScreenshots/Zone_{zoneIndex} (e.g. Zone_0)
            resSprite = Resources.Load<Sprite>($"UI/ZoneScreenshots/Zone_{zoneIndex}");
            if (resSprite != null) return resSprite;

            // 3. Check Resources/UI/ZoneScreenshots/{zoneName} without spaces (e.g. SunlightZone)
            string strippedName = ZoneConfig.Zones[zoneIndex].zoneName.Replace(" ", "");
            resSprite = Resources.Load<Sprite>($"UI/ZoneScreenshots/{strippedName}");
            if (resSprite != null) return resSprite;

            // 4. Check Resources/ZoneScreenshots/{sceneName}
            resSprite = Resources.Load<Sprite>($"ZoneScreenshots/{sceneName}");
            if (resSprite != null) return resSprite;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        TryLoadCustomPrefab();

        if (customRoot != null)
        {
            WireCustomUI();
            RefreshAllZoneCards();
        }
        else
        {
            BuildProceduralUI();
            RefreshAllZoneCards();
        }

        // Always ensure Zone Selection starts closed until explicitly opened
        CloseZoneSelection();
    }

    private void TryLoadCustomPrefab()
    {
        if (customRoot != null) return;

        var prefab = Resources.Load<GameObject>("UI/ZoneSelectionUI")
                  ?? Resources.Load<GameObject>("Prefabs/UI/ZoneSelectionUI")
                  ?? Resources.Load<GameObject>("UI/ZoneSelectionCanvas");

        if (prefab != null)
        {
            var spawned = Instantiate(prefab);
            var spawnedUI = spawned.GetComponent<ZoneSelectionUI>() ?? spawned.GetComponentInChildren<ZoneSelectionUI>();
            if (spawnedUI != null && spawnedUI != this)
            {
                Instance = spawnedUI;
                if (spawned.transform.parent == null)
                {
                    DontDestroyOnLoad(spawned);
                }
                Destroy(gameObject);
                return;
            }
            else
            {
                customRoot = spawned;
                if (spawned.transform.parent == null)
                {
                    DontDestroyOnLoad(spawned);
                }
                WireCustomUI();
            }
        }
    }

    private void OnEnable()
    {
        RefreshAllZoneCards();
    }

    // -----------------------------------------------------------------------
    // Public API: Open / Close
    // -----------------------------------------------------------------------

    public void OpenZoneSelection()
    {
        TryLoadCustomPrefab();

        if (customRoot != null)
        {
            customRoot.SetActive(true);
        }
        else
        {
            if (_proceduralRoot == null) BuildProceduralUI();
            if (_proceduralRoot != null) _proceduralRoot.SetActive(true);
        }

        if (customBackButton != null)
        {
            customBackButton.gameObject.SetActive(true);
        }

        if (_proceduralBackBtn != null)
        {
            _proceduralBackBtn.gameObject.SetActive(true);
            if (_proceduralBackBtnText != null)
            {
                _proceduralBackBtnText.text = "MAIN MENU";
                _proceduralBackBtnText.alignment = TextAlignmentOptions.Center;
                _proceduralBackBtnText.verticalAlignment = VerticalAlignmentOptions.Middle;
                _proceduralBackBtnText.margin = Vector4.zero;
            }
        }

        RefreshAllZoneCards();
        Time.timeScale = 0f;
    }

    public void CloseZoneSelection()
    {
        IsAfterTutorialFlow = false;
        if (customRoot != null) customRoot.SetActive(false);
        if (_proceduralRoot != null) _proceduralRoot.SetActive(false);
        if (customWarningModal != null) customWarningModal.SetActive(false);
        if (_proceduralWarningModal != null) _proceduralWarningModal.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    // -----------------------------------------------------------------------
    // Data Refresh & Unlock Logic
    // -----------------------------------------------------------------------

    public void RefreshAllZoneCards()
    {
        int rdp = GameManager.Instance != null ? GameManager.Instance.RDP : 0;
        int hullTier = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;

        if (customRdpText != null) customRdpText.text = $"RDP: {rdp}";
        if (customHullTierText != null) customHullTierText.text = $"HULL: TIER {hullTier}";
        if (_proceduralRdpText != null) _proceduralRdpText.text = $"RDP: {rdp}";
        if (_proceduralHullText != null) _proceduralHullText.text = $"HULL: TIER {hullTier}";

        for (int i = 0; i < ZoneConfig.ZoneCount; i++)
        {
            RefreshZoneCard(i);
        }
    }

    private void RefreshZoneCard(int zoneIndex)
    {
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];
        bool isUnlocked = GameManager.Instance != null && GameManager.Instance.IsZoneUnlocked(zoneIndex);

        int discovered = GameManager.Instance != null ? GameManager.Instance.GetDiscoveredCountInZone(zoneIndex) : 0;
        int total = zone.totalSpeciesCount;
        float progressFrac = (float)discovered / Mathf.Max(1, total);

        string curScene = SceneManager.GetActiveScene().name;
        bool isCurZone = curScene == zone.sceneName;

        string btnText;
        Color btnColor;
        if (isCurZone)
        {
            if (IsAfterTutorialFlow)
            {
                btnText = "PROCEED";
                btnColor = new Color(0.08f, 0.65f, 0.55f);
            }
            else
            {
                btnText = "GO BACK";
                btnColor = new Color(0.12f, 0.48f, 0.70f);
            }
        }
        else
        {
            btnText = isUnlocked ? "ENTER ZONE" : "LOCKED 🔒";
            btnColor = isUnlocked ? new Color(0.08f, 0.65f, 0.55f) : new Color(0.25f, 0.28f, 0.32f);
        }

        string reqText = zoneIndex == 0 ? "DEFAULT ACCESS" : (isUnlocked ? "HULL REQUIREMENT MET" : $"REQ: HULL TIER {zone.requiredHullTier}");
        Color reqColor = isUnlocked ? new Color(0.10f, 0.44f, 0.34f) : new Color(0.65f, 0.20f, 0.20f);

        var font = GetAlohaFont();

        // Update all bindings (both custom and procedural)
        List<ZoneCardBinding> allCards = new List<ZoneCardBinding>();
        allCards.AddRange(customCards);
        allCards.AddRange(_proceduralCards);

        foreach (var card in allCards)
        {
            if (card.zoneIndex != zoneIndex) continue;

            if (card.titleText != null)
            {
                card.titleText.font = font;
                card.titleText.fontSize = 30f;
                card.titleText.enableAutoSizing = true;
                card.titleText.fontSizeMin = 22f;
                card.titleText.fontSizeMax = 30f;
                card.titleText.textWrappingMode = TextWrappingModes.NoWrap;
                card.titleText.text = zone.zoneName.ToUpper();
                card.titleText.color = Color.white;
            }
            if (card.depthText != null)
            {
                card.depthText.font = font;
                card.depthText.fontSize = 28f;
                card.depthText.enableAutoSizing = true;
                card.depthText.fontSizeMin = 20f;
                card.depthText.fontSizeMax = 28f;
                card.depthText.textWrappingMode = TextWrappingModes.NoWrap;
                card.depthText.text = $"{zone.displayDepthMin:F0} - {zone.displayDepthMax:F0} M";
                card.depthText.color = isUnlocked ? new Color(0.35f, 0.90f, 1f) : new Color(0.6f, 0.72f, 0.8f);
            }
            if (card.zoneScreenshotImage != null)
            {
                Sprite screenshot = GetZoneScreenshot(zoneIndex);
                if (screenshot != null)
                {
                    card.zoneScreenshotImage.sprite = screenshot;
                    card.zoneScreenshotImage.color = isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);
                    if (card.zoneScreenshotPlaceholder != null)
                    {
                        card.zoneScreenshotPlaceholder.SetActive(false);
                    }
                }
                else
                {
                    card.zoneScreenshotImage.sprite = null;
                    Color atmosphericTint = isUnlocked 
                        ? Color.Lerp(zone.fogColor, new Color(0.05f, 0.12f, 0.22f), 0.5f) 
                        : new Color(0.05f, 0.08f, 0.12f, 0.8f);
                    card.zoneScreenshotImage.color = atmosphericTint;
                    if (card.zoneScreenshotPlaceholder != null)
                    {
                        card.zoneScreenshotPlaceholder.SetActive(true);
                    }
                }
            }
            if (card.descText != null)
            {
                // Remove / hide description as requested
                card.descText.gameObject.SetActive(false);
            }
            if (card.speciesProgressText != null)
            {
                card.speciesProgressText.font = font;
                card.speciesProgressText.fontSize = 30f;
                card.speciesProgressText.enableAutoSizing = true;
                card.speciesProgressText.fontSizeMin = 22f;
                card.speciesProgressText.fontSizeMax = 30f;
                card.speciesProgressText.text = $"SPECIES: {discovered} / {total}";
            }
            if (card.progressBar != null) card.progressBar.value = progressFrac;
            if (card.progressFill != null)
            {
                card.progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progressFrac), 1f);
            }
            if (card.hullReqText != null)
            {
                card.hullReqText.font = font;
                card.hullReqText.fontSize = 26f;
                card.hullReqText.enableAutoSizing = true;
                card.hullReqText.fontSizeMin = 20f;
                card.hullReqText.fontSizeMax = 26f;
                card.hullReqText.text = reqText;
            }
            if (card.hullReqBg != null) card.hullReqBg.color = reqColor;
            if (card.lockOverlay != null) card.lockOverlay.SetActive(!isUnlocked);

            if (card.enterButton != null)
            {
                if (card.enterButtonText != null)
                {
                    card.enterButtonText.font = font;
                    card.enterButtonText.fontSize = 32f;
                    card.enterButtonText.enableAutoSizing = true;
                    card.enterButtonText.fontSizeMin = 24f;
                    card.enterButtonText.fontSizeMax = 32f;
                    card.enterButtonText.text = btnText;
                    card.enterButtonText.alignment = TextAlignmentOptions.Center;
                    card.enterButtonText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    card.enterButtonText.verticalAlignment = VerticalAlignmentOptions.Middle;
                    card.enterButtonText.margin = Vector4.zero;

                    var textRt = card.enterButtonText.rectTransform;
                    if (textRt != null)
                    {
                        textRt.anchorMin = Vector2.zero;
                        textRt.anchorMax = Vector2.one;
                        textRt.offsetMin = Vector2.zero;
                        textRt.offsetMax = Vector2.zero;
                        textRt.anchoredPosition = Vector2.zero;
                        textRt.sizeDelta = Vector2.zero;
                    }
                }
                var img = card.enterButton.GetComponent<Image>();
                if (img != null) img.color = btnColor;

                card.enterButton.onClick.RemoveAllListeners();
                int idx = zoneIndex;
                card.enterButton.onClick.AddListener(() => OnZoneCardClicked(idx));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Zone Selection Actions
    // -----------------------------------------------------------------------

    public void OnZoneCardClicked(int zoneIndex)
    {
        AudioManager.Instance?.PlayButtonClick();

        string currentScene = SceneManager.GetActiveScene().name;
        if (ZoneConfig.IsValidZone(zoneIndex) && currentScene == ZoneConfig.Zones[zoneIndex].sceneName)
        {
            IsAfterTutorialFlow = false;
            // Player chose "GO BACK" or "PROCEED" to resume dive
            Time.timeScale = 1f;
            CloseZoneSelection();

            // Push back the player safely and dismiss any boundary popups
            ZoneBoundaryTrigger.PushPlayerFromActiveBoundary();
            return;
        }

        bool isUnlocked = GameManager.Instance != null && GameManager.Instance.IsZoneUnlocked(zoneIndex);

        if (isUnlocked)
        {
            EnterZone(zoneIndex);
        }
        else
        {
            ShowPressureWarningModal(zoneIndex);
        }
    }

    private void EnterZone(int zoneIndex)
    {
        IsAfterTutorialFlow = false;
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;

        MinigameManager.Instance?.CancelActiveMinigame();

        ZoneManager.SetCurrentZone(zoneIndex);

        string sceneName = ZoneConfig.Zones[zoneIndex].sceneName;
        Debug.Log($"[ZoneSelectionUI] Entering Zone {zoneIndex} ('{sceneName}')...");

        AudioManager.Instance?.PlayZoneTransition();
        Time.timeScale = 1f;
        CloseZoneSelection();
        ZoneBoundaryTrigger.PushPlayerFromActiveBoundary();

        if (SceneManager.GetActiveScene().name != sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void ShowPressureWarningModal(int zoneIndex)
    {
        _selectedLockedZone = zoneIndex;
        GameManager.ZoneUnlockDetails details = GameManager.Instance != null
            ? GameManager.Instance.GetZoneUnlockDetails(zoneIndex)
            : new GameManager.ZoneUnlockDetails { reqHull = 2, reqSpecies = 8, currentSpecies = 0, prevZoneName = "Previous Zone" };

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];
        int currentHull = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;

        bool hullMet = currentHull >= details.reqHull;
        bool speciesMet = details.currentSpecies >= details.reqSpecies;
        bool onlySpeciesMissing = hullMet && !speciesMet;

        string title = onlySpeciesMissing ? "🔍 SPECIES DISCOVERY REQUIRED" : "⚠️ PRESSURE THRESHOLD EXCEEDED";
        string desc = onlySpeciesMissing
            ? $"To access <b>{zone.zoneName}</b>, you must document at least 50% of the species in {details.prevZoneName}.\n\n" +
              $"<color=#ffcc00><b>Research Progress:</b></color>\n" +
              $"• {details.prevZoneName}: <b>{details.currentSpecies} / {details.reqSpecies} species documented</b>\n\n" +
              $"Consult your <b>Bestiary</b> to track and locate missing species."
            : $"Your submarine cannot withstand the deep-sea pressure of <b>{zone.zoneName}</b> ({zone.displayDepthMin:F0}m - {zone.displayDepthMax:F0}m).\n\n" +
              $"<color=#ffcc00><b>Unlock Requirements:</b></color>\n" +
              $"• Hull Resistance: <b>Tier {details.reqHull}</b> required (Current: Tier {currentHull})\n" +
              $"• Research Progress: Document at least <b>50% species</b> in {details.prevZoneName} ({details.currentSpecies}/{details.reqSpecies} found)\n\n" +
              $"Upgrade your Hull in the <b>Shop</b> to proceed.";

        if (customWarningModal != null)
        {
            UIThemeManager.ApplyAlohaTheme(customWarningModal);
            if (customWarningTitleText != null)
            {
                customWarningTitleText.text = title;
                customWarningTitleText.fontSize = Mathf.Max(32f, customWarningTitleText.fontSize);
            }
            if (customWarningDescText != null)
            {
                customWarningDescText.text = desc;
                customWarningDescText.fontSize = Mathf.Max(32f, customWarningDescText.fontSize);
                customWarningDescText.alignment = TextAlignmentOptions.Top;
                customWarningDescText.lineSpacing = 10f;
                customWarningDescText.paragraphSpacing = 10f;
            }
            if (customWarningShopButton != null)
            {
                var lbl = customWarningShopButton.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = onlySpeciesMissing ? "BESTIARY 📖" : "GO TO SHOP 🛠️";
                var leg = customWarningShopButton.GetComponentInChildren<Text>(true);
                if (leg != null) leg.text = onlySpeciesMissing ? "BESTIARY" : "GO TO SHOP";

                customWarningShopButton.onClick.RemoveAllListeners();
                if (onlySpeciesMissing)
                {
                    customWarningShopButton.onClick.AddListener(() => {
                        OnWarningCloseClicked();
                        BestiaryManager.OpenBestiary();
                    });
                }
                else
                {
                    customWarningShopButton.onClick.AddListener(OnWarningShopClicked);
                }
            }
            customWarningModal.SetActive(true);
            return;
        }

        if (_proceduralWarningModal != null)
        {
            if (_proceduralWarningTitle != null) _proceduralWarningTitle.text = title;
            if (_proceduralWarningDesc != null)
            {
                _proceduralWarningDesc.text = desc;
                _proceduralWarningDesc.lineSpacing = 10f;
                _proceduralWarningDesc.paragraphSpacing = 10f;
            }
            if (_proceduralWarningShopBtn != null)
            {
                if (_proceduralWarningShopBtnText != null)
                    _proceduralWarningShopBtnText.text = onlySpeciesMissing ? "BESTIARY 📖" : "GO TO SHOP 🛠️";

                _proceduralWarningShopBtn.onClick.RemoveAllListeners();
                if (onlySpeciesMissing)
                {
                    _proceduralWarningShopBtn.onClick.AddListener(() => {
                        OnWarningCloseClicked();
                        BestiaryManager.OpenBestiary();
                    });
                }
                else
                {
                    _proceduralWarningShopBtn.onClick.AddListener(OnWarningShopClicked);
                }
            }
            _proceduralWarningModal.SetActive(true);
        }
    }

    public bool IsOpen => (customRoot != null && customRoot.activeInHierarchy) || (_proceduralRoot != null && _proceduralRoot.activeInHierarchy);

    public void OnWarningShopClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customWarningModal != null) customWarningModal.SetActive(false);
        if (_proceduralWarningModal != null) _proceduralWarningModal.SetActive(false);

        // Open Shop and bring it to the front
        var sm = ShopManager.Instance ?? FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
        if (sm == null)
        {
            var pCanvas = Resources.Load<GameObject>("UI/PersistentCanvasUI");
            if (pCanvas != null)
            {
                var go = Instantiate(pCanvas);
                go.name = "PersistentCanvasUI";
                DontDestroyOnLoad(go);
                sm = go.GetComponentInChildren<ShopManager>(true);
            }
        }

        if (sm != null)
        {
            sm.gameObject.SetActive(true);
            sm.transform.SetAsLastSibling();
            sm.ShowShop();
        }
        else
        {
            var shopGO = new GameObject("ShopManager");
            var newSm = shopGO.AddComponent<ShopManager>();
            newSm.transform.SetAsLastSibling();
            newSm.ShowShop();
        }
    }

    public void OnWarningCloseClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (customWarningModal != null) customWarningModal.SetActive(false);
        if (_proceduralWarningModal != null) _proceduralWarningModal.SetActive(false);
    }

    public void OnBackClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        Time.timeScale = 1f;
        CloseZoneSelection();

        string curScene = SceneManager.GetActiveScene().name;
        if (curScene != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void WireCustomUI()
    {
        if (customBackButton != null)
        {
            customBackButton.onClick.RemoveListener(OnBackClicked);
            customBackButton.onClick.AddListener(OnBackClicked);
        }

        if (customWarningCloseButton != null)
        {
            customWarningCloseButton.onClick.RemoveListener(OnWarningCloseClicked);
            customWarningCloseButton.onClick.AddListener(OnWarningCloseClicked);
        }

        if (customWarningShopButton != null)
        {
            customWarningShopButton.onClick.RemoveListener(OnWarningShopClicked);
            customWarningShopButton.onClick.AddListener(OnWarningShopClicked);
        }
    }

    // -----------------------------------------------------------------------
    // Procedural UI Construction (Aloha Font >= 36px Dark Sci-Fi Canvas)
    // -----------------------------------------------------------------------

    private void BuildProceduralUI()
    {
        if (_proceduralRoot != null) return;

        var font = GetAlohaFont();

        _proceduralCanvasGO = new GameObject("ZoneSelect_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _proceduralCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;

        var scaler = _proceduralCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Root Background
        _proceduralRoot = new GameObject("ZoneSelect_Root", typeof(RectTransform), typeof(Image));
        _proceduralRoot.transform.SetParent(canvas.transform, false);
        var rootRect = _proceduralRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        _proceduralRoot.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

        // ── Top Header Bar ──────────────────────────────────────────────
        var headerGO = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerGO.transform.SetParent(_proceduralRoot.transform, false);
        var headR = headerGO.GetComponent<RectTransform>();
        headR.anchorMin = new Vector2(0f, 1f);
        headR.anchorMax = new Vector2(1f, 1f);
        headR.pivot     = new Vector2(0.5f, 1f);
        headR.anchoredPosition = Vector2.zero;
        headR.sizeDelta = new Vector2(0f, 110f);
        headerGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Back Button (MAIN MENU, 32px Bold, centered in box, vertically aligned with badges)
        _proceduralBackBtn = CreateButton("BackBtn", "MAIN MENU", new Vector2(45f, 0f), new Vector2(280f, 66f), new Color(0.12f, 0.22f, 0.35f), OnBackClicked, headerGO.transform, font, 32f, out _proceduralBackBtnText, new Vector2(0f, 0.5f));
        string activeScene = SceneManager.GetActiveScene().name;
        bool isMenuScene = activeScene == "MainMenu" || activeScene == "ZoneSelect";
        _proceduralBackBtn.gameObject.SetActive(isMenuScene);

        // Header Title (Aloha, 44px Bold)
        CreateText("Title", "OCEAN ZONE SELECTION", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 70f), 44f, FontStyles.Bold, Color.white, headerGO.transform, font);

        // Badges: RDP & Hull
        var badgeContainer = new GameObject("Badges", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        badgeContainer.transform.SetParent(headerGO.transform, false);
        var bcR = badgeContainer.GetComponent<RectTransform>();
        bcR.anchorMin = new Vector2(1f, 0.5f);
        bcR.anchorMax = new Vector2(1f, 0.5f);
        bcR.pivot     = new Vector2(1f, 0.5f);
        bcR.anchoredPosition = new Vector2(-35f, 0f);
        bcR.sizeDelta = new Vector2(600f, 70f);

        var hlg = badgeContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.spacing = 20f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _proceduralRdpText = CreateBadge(badgeContainer.transform, "RDP: 0", new Vector2(240f, 66f), new Color(0.08f, 0.55f, 0.65f), font, 32f);
        _proceduralHullText = CreateBadge(badgeContainer.transform, "HULL: TIER 1", new Vector2(330f, 66f), new Color(0.15f, 0.45f, 0.35f), font, 32f);

        // ── Scrollable Zone Cards Container ──────────────────────────────
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(_proceduralRoot.transform, false);
        var sRect = scrollGO.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.02f, 0.03f);
        sRect.anchorMax = new Vector2(0.98f, 0.86f);
        sRect.sizeDelta = Vector2.zero;
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical   = false;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scrollGO.transform, false);
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vpRect;

        var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var cRect = content.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0f, 0f);
        cRect.anchorMax = new Vector2(0f, 1f);
        cRect.pivot     = new Vector2(0f, 0.5f);
        cRect.sizeDelta = Vector2.zero;

        var chlg = content.GetComponent<HorizontalLayoutGroup>();
        chlg.padding = new RectOffset(30, 30, 20, 20);
        chlg.spacing = 30f;
        chlg.childAlignment = TextAnchor.MiddleCenter;
        chlg.childControlWidth = false;
        chlg.childControlHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cRect;

        // Build 5 Cards
        _proceduralCards.Clear();
        for (int i = 0; i < ZoneConfig.ZoneCount; i++)
        {
            BuildProceduralZoneCard(i, content.transform, font);
        }

        // Build Procedural Warning Modal
        BuildProceduralWarningModal(canvas.transform, font);
    }

    private void BuildProceduralZoneCard(int zoneIndex, Transform parent, TMP_FontAsset font)
    {
        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];
        bool isUnlocked = GameManager.Instance != null && GameManager.Instance.IsZoneUnlocked(zoneIndex);

        var cardGO = new GameObject($"ZoneCard_{zoneIndex}", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(parent, false);
        var r = cardGO.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(380f, 760f);

        Color cardBg = isUnlocked ? new Color(0.04f, 0.10f, 0.18f, 0.98f) : new Color(0.02f, 0.05f, 0.08f, 0.85f);
        cardGO.GetComponent<Image>().color = cardBg;

        var binding = new ZoneCardBinding
        {
            zoneIndex = zoneIndex,
            cardRoot = cardGO
        };

        // 1. Zone Title Banner (Top: Y 0 to -80, Aloha, 30px Bold)
        var topBanner = new GameObject("TopBanner", typeof(RectTransform), typeof(Image));
        topBanner.transform.SetParent(cardGO.transform, false);
        var tbR = topBanner.GetComponent<RectTransform>();
        tbR.anchorMin = new Vector2(0f, 1f);
        tbR.anchorMax = new Vector2(1f, 1f);
        tbR.pivot     = new Vector2(0.5f, 1f);
        tbR.anchoredPosition = Vector2.zero;
        tbR.sizeDelta = new Vector2(0f, 80f);
        topBanner.GetComponent<Image>().color = isUnlocked ? new Color(0.08f, 0.38f, 0.48f) : new Color(0.14f, 0.17f, 0.22f);

        binding.titleText = CreateText("Title", zone.zoneName.ToUpper(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 60f), 30f, FontStyles.Bold, Color.white, topBanner.transform, font);
        binding.titleText.enableAutoSizing = true;
        binding.titleText.fontSizeMin = 22f;
        binding.titleText.fontSizeMax = 30f;
        binding.titleText.textWrappingMode = TextWrappingModes.NoWrap;

        // 2. Depth Range (Below Title Banner, Y: -88f, 28px Bold)
        Color depthCol = isUnlocked ? new Color(0.35f, 0.90f, 1f) : new Color(0.6f, 0.72f, 0.8f);
        binding.depthText = CreateText("Depth", $"{zone.displayDepthMin:F0} - {zone.displayDepthMax:F0} M", new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(340f, 40f), 28f, FontStyles.Bold, depthCol, cardGO.transform, font);
        binding.depthText.enableAutoSizing = true;
        binding.depthText.fontSizeMin = 20f;
        binding.depthText.fontSizeMax = 28f;
        binding.depthText.textWrappingMode = TextWrappingModes.NoWrap;

        // 3. Screenshot Preview Container (Y: -138f, size 344x230)
        var ssFrame = new GameObject("ScreenshotFrame", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        ssFrame.transform.SetParent(cardGO.transform, false);
        var ssFrameR = ssFrame.GetComponent<RectTransform>();
        ssFrameR.anchorMin = new Vector2(0.5f, 1f);
        ssFrameR.anchorMax = new Vector2(0.5f, 1f);
        ssFrameR.pivot     = new Vector2(0.5f, 1f);
        ssFrameR.anchoredPosition = new Vector2(0f, -138f);
        ssFrameR.sizeDelta = new Vector2(344f, 230f);
        ssFrame.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.09f, 0.95f);

        var ssImgGO = new GameObject("ScreenshotImage", typeof(RectTransform), typeof(Image));
        ssImgGO.transform.SetParent(ssFrame.transform, false);
        var ssImgR = ssImgGO.GetComponent<RectTransform>();
        ssImgR.anchorMin = Vector2.zero;
        ssImgR.anchorMax = Vector2.one;
        ssImgR.sizeDelta = Vector2.zero;
        var ssImg = ssImgGO.GetComponent<Image>();
        ssImg.preserveAspect = false;

        Sprite zoneSprite = GetZoneScreenshot(zoneIndex);
        if (zoneSprite != null)
        {
            ssImg.sprite = zoneSprite;
            ssImg.color = isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
        else
        {
            ssImg.sprite = null;
            Color atmosphericTint = isUnlocked 
                ? Color.Lerp(zone.fogColor, new Color(0.05f, 0.12f, 0.22f), 0.5f) 
                : new Color(0.05f, 0.08f, 0.12f, 0.8f);
            ssImg.color = atmosphericTint;
        }
        binding.zoneScreenshotImage = ssImg;

        // Placeholder text if no sprite assigned
        var ssPlaceholder = new GameObject("PlaceholderText", typeof(RectTransform));
        ssPlaceholder.transform.SetParent(ssFrame.transform, false);
        var phR = ssPlaceholder.GetComponent<RectTransform>();
        phR.anchorMin = Vector2.zero;
        phR.anchorMax = Vector2.one;
        phR.sizeDelta = Vector2.zero;
        var phText = ssPlaceholder.AddComponent<TextMeshProUGUI>();
        if (font != null) phText.font = font;
        phText.fontSize = 20f;
        phText.fontStyle = FontStyles.Bold;
        phText.alignment = TextAlignmentOptions.Center;
        phText.color = new Color(0.4f, 0.65f, 0.8f, 0.5f);
        phText.text = "ZONE PREVIEW";
        ssPlaceholder.SetActive(zoneSprite == null);
        binding.zoneScreenshotPlaceholder = ssPlaceholder;

        // 4. Info & Progress Container (Code-only non-visual container spanning between screenshot and action button)
        var infoContainer = new GameObject("InfoContainer", typeof(RectTransform));
        infoContainer.transform.SetParent(cardGO.transform, false);
        var icR = infoContainer.GetComponent<RectTransform>();
        icR.anchorMin = new Vector2(0.5f, 1f);
        icR.anchorMax = new Vector2(0.5f, 1f);
        icR.pivot     = new Vector2(0.5f, 1f);
        icR.anchoredPosition = new Vector2(0f, -374f);
        icR.sizeDelta = new Vector2(344f, 266f);
        binding.infoContainer = icR;

        // 4a. Upper Span: Access State Display (Spans top 50% of container)
        var accessSpan = new GameObject("AccessSpan", typeof(RectTransform));
        accessSpan.transform.SetParent(infoContainer.transform, false);
        var asR = accessSpan.GetComponent<RectTransform>();
        asR.anchorMin = new Vector2(0f, 0.5f);
        asR.anchorMax = new Vector2(1f, 1f);
        asR.pivot     = new Vector2(0.5f, 0.5f);
        asR.anchoredPosition = Vector2.zero;
        asR.sizeDelta = Vector2.zero;
        binding.accessSpan = asR;

        string reqText = zoneIndex == 0 ? "DEFAULT ACCESS" : (isUnlocked ? "HULL REQUIREMENT MET" : $"REQ: HULL TIER {zone.requiredHullTier}");
        Color reqColor = isUnlocked ? new Color(0.10f, 0.44f, 0.34f) : new Color(0.65f, 0.20f, 0.20f);
        binding.hullReqText = CreateBadgeCard(accessSpan.transform, reqText, Vector2.zero, reqColor, font, 26f, out binding.hullReqBg, new Vector2(0f, 68f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        binding.hullReqText.enableAutoSizing = true;
        binding.hullReqText.fontSizeMin = 20f;
        binding.hullReqText.fontSizeMax = 26f;

        // 4b. Lower Span: Species Progress Section (Spans bottom 50% of container)
        var progressSpan = new GameObject("ProgressSpan", typeof(RectTransform));
        progressSpan.transform.SetParent(infoContainer.transform, false);
        var psR = progressSpan.GetComponent<RectTransform>();
        psR.anchorMin = new Vector2(0f, 0f);
        psR.anchorMax = new Vector2(1f, 0.5f);
        psR.pivot     = new Vector2(0.5f, 0.5f);
        psR.anchoredPosition = Vector2.zero;
        psR.sizeDelta = Vector2.zero;
        binding.progressSpan = psR;

        int discovered = GameManager.Instance != null ? GameManager.Instance.GetDiscoveredCountInZone(zoneIndex) : 0;
        int total = zone.totalSpeciesCount;
        float progressFrac = (float)discovered / Mathf.Max(1, total);

        // Species Progress Label (Top half of ProgressSpan)
        var spGO = new GameObject("SpeciesLbl", typeof(RectTransform));
        spGO.transform.SetParent(progressSpan.transform, false);
        var spR = spGO.GetComponent<RectTransform>();
        spR.anchorMin = new Vector2(0f, 0.48f);
        spR.anchorMax = new Vector2(1f, 1f);
        spR.pivot     = new Vector2(0.5f, 0.5f);
        spR.anchoredPosition = Vector2.zero;
        spR.sizeDelta = Vector2.zero;
        var spTmp = spGO.AddComponent<TextMeshProUGUI>();
        if (font != null) spTmp.font = font;
        spTmp.fontSize = 30f;
        spTmp.fontStyle = FontStyles.Bold;
        spTmp.alignment = TextAlignmentOptions.Center;
        spTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        spTmp.color = Color.white;
        spTmp.text = $"SPECIES: {discovered} / {total}";
        spTmp.enableAutoSizing = true;
        spTmp.fontSizeMin = 22f;
        spTmp.fontSizeMax = 30f;
        binding.speciesProgressText = spTmp;

        // Progress Bar Track & Fill (Bottom half of ProgressSpan, spans full width with 6px padding)
        var barTrackGO = new GameObject("ProgressBarTrack", typeof(RectTransform), typeof(Image));
        barTrackGO.transform.SetParent(progressSpan.transform, false);
        var btR = barTrackGO.GetComponent<RectTransform>();
        btR.anchorMin = new Vector2(0f, 0.12f);
        btR.anchorMax = new Vector2(1f, 0.38f);
        btR.pivot     = new Vector2(0.5f, 0.5f);
        btR.anchoredPosition = Vector2.zero;
        btR.sizeDelta = new Vector2(-12f, 0f);
        barTrackGO.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.18f, 0.9f);

        var barFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        barFillGO.transform.SetParent(barTrackGO.transform, false);
        var bfR = barFillGO.GetComponent<RectTransform>();
        bfR.anchorMin = Vector2.zero;
        bfR.anchorMax = new Vector2(Mathf.Clamp01(progressFrac), 1f);
        bfR.sizeDelta = Vector2.zero;
        var fillImg = barFillGO.GetComponent<Image>();
        fillImg.color = new Color(0.15f, 0.85f, 0.70f);
        binding.progressFill = fillImg;

        // 5. Action Button (Anchored Bottom: Y +35f, size 330x76, 32px Bold)
        string currentScene = SceneManager.GetActiveScene().name;
        bool isCurrentZone = (currentScene == zone.sceneName);

        string btnText;
        Color btnColor;
        if (isCurrentZone)
        {
            if (IsAfterTutorialFlow)
            {
                btnText = "PROCEED";
                btnColor = new Color(0.08f, 0.65f, 0.55f);
            }
            else
            {
                btnText = "GO BACK";
                btnColor = new Color(0.12f, 0.48f, 0.70f);
            }
        }
        else
        {
            btnText = isUnlocked ? "ENTER ZONE" : "LOCKED 🔒";
            btnColor = isUnlocked ? new Color(0.08f, 0.65f, 0.55f) : new Color(0.25f, 0.28f, 0.32f);
        }

        int idx = zoneIndex;
        binding.enterButton = CreateButton("ActionBtn", btnText, new Vector2(0f, 35f), new Vector2(330f, 76f), btnColor, () => OnZoneCardClicked(idx), cardGO.transform, font, 32f, out binding.enterButtonText, new Vector2(0.5f, 0f));
        binding.enterButtonText.enableAutoSizing = true;
        binding.enterButtonText.fontSizeMin = 24f;
        binding.enterButtonText.fontSizeMax = 32f;
        binding.enterButtonText.alignment = TextAlignmentOptions.Center;
        binding.enterButtonText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        binding.enterButtonText.verticalAlignment = VerticalAlignmentOptions.Middle;
        binding.enterButtonText.margin = Vector4.zero;

        _proceduralCards.Add(binding);
    }

    private void BuildProceduralWarningModal(Transform canvasParent, TMP_FontAsset font)
    {
        _proceduralWarningModal = new GameObject("PressureWarningModal", typeof(RectTransform), typeof(Image));
        _proceduralWarningModal.transform.SetParent(canvasParent, false);
        _proceduralWarningModal.transform.SetAsLastSibling();

        var bgR = _proceduralWarningModal.GetComponent<RectTransform>();
        bgR.anchorMin = Vector2.zero;
        bgR.anchorMax = Vector2.one;
        bgR.sizeDelta = Vector2.zero;
        _proceduralWarningModal.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.90f);

        // Dialog Box (enlarged to 1200x780 to comfortably fit Aloha 36px font with generous margins and no overlap)
        var boxGO = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        boxGO.transform.SetParent(_proceduralWarningModal.transform, false);
        var bRect = boxGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot     = new Vector2(0.5f, 0.5f);
        bRect.sizeDelta = new Vector2(1200f, 780f);
        boxGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Title (Top-anchored: Y = -40f, height 70f, Aloha, 40px Bold)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(boxGO.transform, false);
        var tR = titleGO.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.5f, 1f);
        tR.anchorMax = new Vector2(0.5f, 1f);
        tR.pivot     = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -40f);
        tR.sizeDelta = new Vector2(1120f, 70f);
        _proceduralWarningTitle = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralWarningTitle.font = font;
        _proceduralWarningTitle.fontSize = 40f;
        _proceduralWarningTitle.fontStyle = FontStyles.Bold;
        _proceduralWarningTitle.alignment = TextAlignmentOptions.Center;
        _proceduralWarningTitle.color = new Color(1f, 0.35f, 0.35f);
        _proceduralWarningTitle.text = "⚠️ PRESSURE THRESHOLD EXCEEDED";

        // Description (Top-anchored starting at Y = -125f, height 500f, Aloha, 36px, strictly top-aligned to eliminate overlap)
        var descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(boxGO.transform, false);
        var dR = descGO.GetComponent<RectTransform>();
        dR.anchorMin = new Vector2(0.5f, 1f);
        dR.anchorMax = new Vector2(0.5f, 1f);
        dR.pivot     = new Vector2(0.5f, 1f);
        dR.anchoredPosition = new Vector2(0f, -125f);
        dR.sizeDelta = new Vector2(1100f, 500f);
        _proceduralWarningDesc = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralWarningDesc.font = font;
        _proceduralWarningDesc.fontSize = 32f;
        _proceduralWarningDesc.alignment = TextAlignmentOptions.Top;
        _proceduralWarningDesc.lineSpacing = 10f;
        _proceduralWarningDesc.paragraphSpacing = 10f;
        _proceduralWarningDesc.color = new Color(0.9f, 0.92f, 0.95f);
        _proceduralWarningDesc.textWrappingMode = TextWrappingModes.Normal;

        // Button Container (Bottom-anchored at Y = 40f, height 85f, buttons 420x80)
        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(boxGO.transform, false);
        var brR = btnRow.GetComponent<RectTransform>();
        brR.anchorMin = new Vector2(0.5f, 0f);
        brR.anchorMax = new Vector2(0.5f, 0f);
        brR.pivot     = new Vector2(0.5f, 0f);
        brR.anchoredPosition = new Vector2(0f, 40f);
        brR.sizeDelta = new Vector2(920f, 85f);

        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 35f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _proceduralWarningShopBtn = CreateButton("ShopBtn", "GO TO SHOP", Vector2.zero, new Vector2(420f, 80f), new Color(0.08f, 0.65f, 0.55f), OnWarningShopClicked, btnRow.transform, font, 32f, out _proceduralWarningShopBtnText);
        CreateButton("CloseBtn", "CANCEL", Vector2.zero, new Vector2(420f, 80f), new Color(0.18f, 0.25f, 0.35f), OnWarningCloseClicked, btnRow.transform, font, 32f);

        _proceduralWarningModal.SetActive(false);
        _proceduralRoot.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // UI Helpers
    // -----------------------------------------------------------------------

    private TMP_Text CreateBadge(Transform parent, string text, Vector2 size, Color color, TMP_FontAsset font, float fontSize = 32f)
    {
        var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(parent, false);
        var r = badgeGO.GetComponent<RectTransform>();
        r.sizeDelta = size;
        badgeGO.GetComponent<Image>().color = color;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(badgeGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;

        return tmp;
    }

    private TMP_Text CreateBadgeCard(Transform parent, string text, Vector2 pos, Color color, TMP_FontAsset font, float fontSize, out Image bgImage, Vector2? size = null, Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        var badgeGO = new GameObject("ReqBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(parent, false);
        var r = badgeGO.GetComponent<RectTransform>();
        r.anchorMin = anchorMin ?? new Vector2(0.5f, 1f);
        r.anchorMax = anchorMax ?? new Vector2(0.5f, 1f);
        r.pivot     = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size ?? new Vector2(340f, 58f);
        bgImage = badgeGO.GetComponent<Image>();
        bgImage.color = color;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(badgeGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = Color.white;
        tmp.text = text;

        return tmp;
    }

    private TextMeshProUGUI CreateText(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot     = anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.text = text;

        return tmp;
    }

    private Button CreateButton(string name, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, float fontSize = 32f, Vector2? anchor = null)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var r = btnGO.GetComponent<RectTransform>();
        Vector2 anc = anchor ?? new Vector2(0.5f, 0.5f);
        r.anchorMin = anc;
        r.anchorMax = anc;
        r.pivot     = anc;
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        btnGO.GetComponent<Image>().color = color;
        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(action);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.pivot = new Vector2(0.5f, 0.5f);
        lblR.anchoredPosition = Vector2.zero;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.margin = Vector4.zero;
        tmp.color = Color.white;
        tmp.text = label;

        return btn;
    }

    private Button CreateButton(string name, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, float fontSize, out TMP_Text labelText, Vector2? anchor = null)
    {
        var btn = CreateButton(name, label, pos, size, color, action, parent, font, fontSize, anchor);
        labelText = btn.GetComponentInChildren<TMP_Text>();
        return btn;
    }
}
