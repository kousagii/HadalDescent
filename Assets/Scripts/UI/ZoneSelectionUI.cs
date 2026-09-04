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

    [System.Serializable]
    public class ZoneCardBinding
    {
        public int zoneIndex;
        public GameObject cardRoot;
        public TMP_Text   titleText;
        public TMP_Text   depthText;
        public TMP_Text   speciesProgressText;
        public Slider     progressBar;
        public TMP_Text   hullReqText;
        public Button     enterButton;
        public GameObject lockOverlay;
    }

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

    private int _selectedLockedZone = -1;

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
            if (_proceduralBackBtnText != null) _proceduralBackBtnText.text = "◀ MAIN MENU";
        }

        RefreshAllZoneCards();
        Time.timeScale = 0f;
    }

    public void CloseZoneSelection()
    {
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

        // Update custom card if bound
        var custom = customCards.Find(c => c.zoneIndex == zoneIndex);
        if (custom != null)
        {
            if (custom.titleText != null) custom.titleText.text = zone.zoneName.ToUpper();
            if (custom.depthText != null) custom.depthText.text = $"{zone.displayDepthMin:F0} – {zone.displayDepthMax:F0} m";
            if (custom.speciesProgressText != null) custom.speciesProgressText.text = $"Species: {discovered} / {total} ({Mathf.RoundToInt(progressFrac * 100f)}%)";
            if (custom.progressBar != null) custom.progressBar.value = progressFrac;
            if (custom.hullReqText != null) custom.hullReqText.text = $"Req: Hull Tier {zone.requiredHullTier}";

            if (custom.lockOverlay != null) custom.lockOverlay.SetActive(!isUnlocked);
            if (custom.enterButton != null)
            {
                string curScene = SceneManager.GetActiveScene().name;
                bool isCurZone = curScene == zone.sceneName;
                var btnTxt = custom.enterButton.GetComponentInChildren<TMP_Text>();
                if (btnTxt != null)
                {
                    btnTxt.text = isCurZone ? "◀ GO BACK" : (isUnlocked ? "ENTER ZONE" : "LOCKED 🔒");
                }

                custom.enterButton.onClick.RemoveAllListeners();
                int idx = zoneIndex;
                custom.enterButton.onClick.AddListener(() => OnZoneCardClicked(idx));
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
            // Player chose "GO BACK" to resume current dive
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
        if (!ZoneConfig.IsValidZone(zoneIndex)) return;

        string sceneName = ZoneConfig.Zones[zoneIndex].sceneName;
        Debug.Log($"[ZoneSelectionUI] Entering Zone {zoneIndex} ('{sceneName}')...");

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
            : $"Your submarine cannot withstand the deep-sea pressure of <b>{zone.zoneName}</b> ({zone.displayDepthMin:F0}m – {zone.displayDepthMax:F0}m).\n\n" +
              $"<color=#ffcc00><b>Unlock Requirements:</b></color>\n" +
              $"• Hull Resistance: <b>Tier {details.reqHull}</b> required (Current: Tier {currentHull})\n" +
              $"• Research Progress: Document at least <b>50% species</b> in {details.prevZoneName} ({details.currentSpecies}/{details.reqSpecies} found)\n\n" +
              $"Upgrade your Hull in the <b>Shop</b> to proceed.";

        if (customWarningModal != null)
        {
            if (customWarningTitleText != null) customWarningTitleText.text = title;
            if (customWarningDescText != null) customWarningDescText.text = desc;
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
            if (_proceduralWarningDesc != null) _proceduralWarningDesc.text = desc;
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
    // Procedural UI Construction (36pt Poppins SDF Dark Sci-Fi Canvas)
    // -----------------------------------------------------------------------

    private void BuildProceduralUI()
    {
        if (_proceduralRoot != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

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
        headR.sizeDelta = new Vector2(0f, 90f);
        headerGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Back Button (hidden during exploration / after tutorial, visible in menu scenes)
        _proceduralBackBtn = CreateButton("BackBtn", "◀ BACK", new Vector2(30f, -45f), new Vector2(200f, 50f), new Color(0.12f, 0.22f, 0.35f), OnBackClicked, headerGO.transform, font, 20, out _proceduralBackBtnText);
        string activeScene = SceneManager.GetActiveScene().name;
        bool isMenuScene = activeScene == "MainMenu" || activeScene == "ZoneSelect";
        _proceduralBackBtn.gameObject.SetActive(isMenuScene);

        // Title
        CreateText("Title", "OCEAN ZONE SELECTION", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(600f, 50f), 36, FontStyles.Bold, Color.white, headerGO.transform, font);

        // Badges: RDP & Hull
        var badgeContainer = new GameObject("Badges", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        badgeContainer.transform.SetParent(headerGO.transform, false);
        var bcR = badgeContainer.GetComponent<RectTransform>();
        bcR.anchorMin = new Vector2(1f, 0.5f);
        bcR.anchorMax = new Vector2(1f, 0.5f);
        bcR.pivot     = new Vector2(1f, 0.5f);
        bcR.anchoredPosition = new Vector2(-30f, 0f);
        bcR.sizeDelta = new Vector2(380f, 50f);

        var hlg = badgeContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.spacing = 16f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _proceduralRdpText = CreateBadge(badgeContainer.transform, "RDP: 0", new Color(0.08f, 0.55f, 0.65f), font);
        _proceduralHullText = CreateBadge(badgeContainer.transform, "HULL: TIER 1", new Color(0.15f, 0.45f, 0.35f), font);

        // ── Scrollable Zone Cards Container ──────────────────────────────
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(_proceduralRoot.transform, false);
        var sRect = scrollGO.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.05f, 0.05f);
        sRect.anchorMax = new Vector2(0.95f, 0.88f);
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
        chlg.padding = new RectOffset(20, 20, 20, 20);
        chlg.spacing = 24f;
        chlg.childAlignment = TextAnchor.MiddleCenter;
        chlg.childControlWidth = false;
        chlg.childControlHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cRect;

        // Build 5 Cards
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
        r.sizeDelta = new Vector2(320f, 720f);

        Color cardBg = isUnlocked ? new Color(0.04f, 0.10f, 0.18f, 0.98f) : new Color(0.02f, 0.05f, 0.08f, 0.85f);
        cardGO.GetComponent<Image>().color = cardBg;

        // Zone Index & Depth Banner
        var topBanner = new GameObject("TopBanner", typeof(RectTransform), typeof(Image));
        topBanner.transform.SetParent(cardGO.transform, false);
        var tbR = topBanner.GetComponent<RectTransform>();
        tbR.anchorMin = new Vector2(0f, 1f);
        tbR.anchorMax = new Vector2(1f, 1f);
        tbR.pivot     = new Vector2(0.5f, 1f);
        tbR.anchoredPosition = Vector2.zero;
        tbR.sizeDelta = new Vector2(0f, 70f);
        topBanner.GetComponent<Image>().color = isUnlocked ? new Color(0.08f, 0.40f, 0.50f) : new Color(0.15f, 0.18f, 0.22f);

        CreateText("Depth", $"{zone.displayDepthMin:F0} – {zone.displayDepthMax:F0} m", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f), 24, FontStyles.Bold, Color.white, topBanner.transform, font);

        // Title
        CreateText("Title", zone.zoneName.ToUpper(), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(300f, 44f), 28, FontStyles.Bold, isUnlocked ? new Color(0.3f, 0.9f, 1f) : new Color(0.6f, 0.65f, 0.7f), cardGO.transform, font);

        // Subtitle / Characteristics
        string desc = zoneIndex switch
        {
            0 => "Bright surface waters teeming with abundant marine life. Beginner expedition zone.",
            1 => "Dim mesopelagic waters with bioluminescent species and increasing ocean currents.",
            2 => "Pitch-black bathypelagic realm. High water pressure with rare hydrothermal vents.",
            3 => "Abyssal plain with deep-sea trenches, cold seeps, and crushing extreme pressure.",
            4 => "The deepest hadal trench on Earth. Ultra-rare creatures and extreme conditions.",
            _ => "Deep sea expedition zone."
        };
        CreateText("Desc", desc, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(280f, 140f), 18, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f), cardGO.transform, font);

        // Requirement Badge
        string reqText = zoneIndex == 0 ? "Default Access" : $"Req: Hull Tier {zone.requiredHullTier}";
        Color reqColor = isUnlocked ? new Color(0.12f, 0.55f, 0.45f) : new Color(0.65f, 0.22f, 0.22f);
        CreateBadgeCard(cardGO.transform, reqText, new Vector2(0f, -310f), reqColor, font);

        // Species Progress Bar
        int discovered = GameManager.Instance != null ? GameManager.Instance.GetDiscoveredCountInZone(zoneIndex) : 0;
        int total = zone.totalSpeciesCount;
        float progressFrac = (float)discovered / Mathf.Max(1, total);

        CreateText("SpeciesLbl", $"Species Cataloged: {discovered} / {total}", new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(280f, 30f), 18, FontStyles.Bold, Color.white, cardGO.transform, font);

        // Enter / Locked / Current Zone Action Button
        string currentScene = SceneManager.GetActiveScene().name;
        bool isCurrentZone = (currentScene == zone.sceneName);

        string btnText;
        Color btnColor;

        if (isCurrentZone)
        {
            btnText = "◀ GO BACK";
            btnColor = new Color(0.12f, 0.48f, 0.70f);
        }
        else if (isUnlocked)
        {
            btnText = "ENTER ZONE";
            btnColor = new Color(0.08f, 0.65f, 0.55f);
        }
        else
        {
            btnText = "LOCKED 🔒";
            btnColor = new Color(0.25f, 0.28f, 0.32f);
        }

        int idx = zoneIndex;
        CreateButton("ActionBtn", btnText, new Vector2(0f, 40f), new Vector2(260f, 54f), btnColor, () => OnZoneCardClicked(idx), cardGO.transform, font, 22, new Vector2(0.5f, 0f));
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

        // Dialog Box
        var boxGO = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        boxGO.transform.SetParent(_proceduralWarningModal.transform, false);
        var bRect = boxGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot     = new Vector2(0.5f, 0.5f);
        bRect.sizeDelta = new Vector2(680f, 480f);
        boxGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(boxGO.transform, false);
        var tR = titleGO.GetComponent<RectTransform>();
        tR.anchoredPosition = new Vector2(0f, 180f);
        tR.sizeDelta = new Vector2(620f, 50f);
        _proceduralWarningTitle = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralWarningTitle.font = font;
        _proceduralWarningTitle.fontSize = 32;
        _proceduralWarningTitle.fontStyle = FontStyles.Bold;
        _proceduralWarningTitle.alignment = TextAlignmentOptions.Center;
        _proceduralWarningTitle.color = new Color(1f, 0.35f, 0.35f);
        _proceduralWarningTitle.text = "⚠️ PRESSURE THRESHOLD EXCEEDED";

        // Description
        var descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(boxGO.transform, false);
        var dR = descGO.GetComponent<RectTransform>();
        dR.anchoredPosition = new Vector2(0f, 40f);
        dR.sizeDelta = new Vector2(600f, 200f);
        _proceduralWarningDesc = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralWarningDesc.font = font;
        _proceduralWarningDesc.fontSize = 22;
        _proceduralWarningDesc.alignment = TextAlignmentOptions.Center;
        _proceduralWarningDesc.lineSpacing = 8f;
        _proceduralWarningDesc.color = new Color(0.9f, 0.92f, 0.95f);

        // Button Container
        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(boxGO.transform, false);
        var brR = btnRow.GetComponent<RectTransform>();
        brR.anchoredPosition = new Vector2(0f, -170f);
        brR.sizeDelta = new Vector2(560f, 60f);

        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 24f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _proceduralWarningShopBtn = CreateButton("ShopBtn", "GO TO SHOP", Vector2.zero, new Vector2(250f, 54f), new Color(0.08f, 0.65f, 0.55f), OnWarningShopClicked, btnRow.transform, font, 22, out _proceduralWarningShopBtnText);
        CreateButton("CloseBtn", "CANCEL", Vector2.zero, new Vector2(250f, 54f), new Color(0.18f, 0.25f, 0.35f), OnWarningCloseClicked, btnRow.transform, font, 22);

        _proceduralWarningModal.SetActive(false);
        _proceduralRoot.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // UI Helpers
    // -----------------------------------------------------------------------

    private TMP_Text CreateBadge(Transform parent, string text, Color color, TMP_FontAsset font)
    {
        var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(parent, false);
        var r = badgeGO.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(170f, 44f);
        badgeGO.GetComponent<Image>().color = color;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(badgeGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;

        return tmp;
    }

    private void CreateBadgeCard(Transform parent, string text, Vector2 pos, Color color, TMP_FontAsset font)
    {
        var badgeGO = new GameObject("ReqBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(parent, false);
        var r = badgeGO.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 1f);
        r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot     = new Vector2(0.5f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(260f, 38f);
        badgeGO.GetComponent<Image>().color = color;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(badgeGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;
    }

    private void CreateText(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, Transform parent, TMP_FontAsset font)
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
    }

    private Button CreateButton(string name, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, float fontSize = 24, Vector2? anchor = null)
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
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
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
