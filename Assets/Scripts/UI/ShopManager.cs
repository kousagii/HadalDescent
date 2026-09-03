using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum UpgradeCategory
{
    Hull,
    Sonar,
    Scanner,
    Engine,
    Lights,
    Utilities
}

[Serializable]
public class UpgradeTierInfo
{
    public int    tier;
    public int    costRDP;
    [TextArea(2, 3)]
    public string perkDescription;

    public UpgradeTierInfo(int tier, int costRDP, string perkDescription)
    {
        this.tier            = tier;
        this.costRDP         = costRDP;
        this.perkDescription = perkDescription;
    }
}

[Serializable]
public class UpgradeCategoryConfig
{
    public UpgradeCategory category;
    public string          displayName;
    public Sprite          icon;
    public UpgradeTierInfo[] tiers = new UpgradeTierInfo[5];
}

/// <summary>
/// Manages the Submarine Upgrade Shop UI (similar to BestiaryManager).
/// Wire up your custom panel, buttons, text, and card prefabs in the Inspector,
/// or let it auto-generate a sleek procedural interface at runtime.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector UI References
    // -----------------------------------------------------------------------

    [Header("Shop UI Root & Controls")]
    [Tooltip("Main Shop root panel (toggled on/off).")]
    [SerializeField] private GameObject shopPanel;

    [Tooltip("Close button on the shop panel (e.g. [X] or [Close]).")]
    [SerializeField] private Button closeButton;

    [Tooltip("Header text displaying player's available RDP (e.g. 'Current RDP: 850').")]
    [SerializeField] private TMP_Text currentRdpText;

    [Header("Dynamic ScrollView (Optional)")]
    [Tooltip("Scroll Rect Content Transform where upgrade cards are spawned.")]
    [SerializeField] private Transform entryContainer;

    [Tooltip("Prefab containing UpgradeCardUI for spawning each row.")]
    [SerializeField] private GameObject upgradeCardPrefab;

    [Header("Static Cards (Optional)")]
    [Tooltip("Direct references if you build static upgrade rows directly in the scene.")]
    [SerializeField] private UpgradeCardUI[] staticCards;

    [Header("Upgrade Categories & Perks")]
    [SerializeField] private List<UpgradeCategoryConfig> categoryConfigs = new List<UpgradeCategoryConfig>();

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    public event Action<UpgradeCategory, int> OnUpgradePurchased;

    private static Sprite _cachedWhiteSprite;
    private static Sprite WhiteSprite
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

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        InitializeDefaultConfigs();
    }

    private void Start()
    {
        if (!_isOpen && shopPanel != null)
            shopPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(HideShop);
    }

    // -----------------------------------------------------------------------
    // Exact Upgrade Configs (Hull, Sonar, Scanner, Engine, Lights, Utilities)
    // -----------------------------------------------------------------------

    private void InitializeDefaultConfigs()
    {
        if (categoryConfigs != null && categoryConfigs.Count > 0) return;

        categoryConfigs = new List<UpgradeCategoryConfig>
        {
            // 1. HULL UPGRADES (Total: 6,250 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Hull,
                displayName = "Reinforced Hull",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Sunlight Zone (0 m depth rated) • Standard pressure hull"),
                    new UpgradeTierInfo(2, 350,  "Twilight Zone (200 m depth rated) • Unlocks Zone 2 descent"),
                    new UpgradeTierInfo(3, 900,  "Midnight Zone (1,000 m depth rated) • Unlocks Zone 3 descent"),
                    new UpgradeTierInfo(4, 1800, "Abyssal Zone (4,000 m depth rated) • Unlocks Zone 4 descent"),
                    new UpgradeTierInfo(5, 3200, "Hadal Zone (6,000 m depth rated) • Maximum trench pressure rating")
                }
            },

            // 2. SONAR UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Sonar,
                displayName = "Active Sonar Array",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "50 m detection radius • Basic dot blips"),
                    new UpgradeTierInfo(2, 150,  "100 m detection radius • Category icons (fish, debris, hazard)"),
                    new UpgradeTierInfo(3, 350,  "150 m detection radius • Elevation indicators (▲ / ▼)"),
                    new UpgradeTierInfo(4, 750,  "200 m detection radius • Faster sweep & pulse glow"),
                    new UpgradeTierInfo(5, 1500, "250 m detection radius • Maximum range coverage")
                }
            },

            // 3. SCANNER UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Scanner,
                displayName = "Research Scanner",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Base focus bar width (15%) • Standard lock-on speed"),
                    new UpgradeTierInfo(2, 150,  "25% focus bar width • Minor stabilization against creature movement"),
                    new UpgradeTierInfo(3, 350,  "30% focus bar width • +15% lock-on meter fill speed from base"),
                    new UpgradeTierInfo(4, 750,  "30% focus bar width • +30% lock-on meter fill speed from base"),
                    new UpgradeTierInfo(5, 1500, "35% focus bar width • Halves lock-on progress decay when off-target")
                }
            },

            // 4. ENGINE UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Engine,
                displayName = "Propulsion Engine",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Base exploration speed • Standard turning response"),
                    new UpgradeTierInfo(2, 150,  "+20% speed • Faster vertical ascent and descent"),
                    new UpgradeTierInfo(3, 350,  "+40% speed • Improved current resistance"),
                    new UpgradeTierInfo(4, 750,  "+65% speed • +25% lane-shift speed in Hazard Dodge"),
                    new UpgradeTierInfo(5, 1500, "+100% speed • Boost burst against strong underwater currents")
                }
            },

            // 5. LIGHT UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Lights,
                displayName = "Submersible Floodlights",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "25 m illumination reach • Narrow halogen beam (Sunlight Zone)"),
                    new UpgradeTierInfo(2, 150,  "50 m illumination reach • High-intensity LED beam (45° angle)"),
                    new UpgradeTierInfo(3, 350,  "80 m illumination reach • Wide floodlight matrix (75° angle)"),
                    new UpgradeTierInfo(4, 750,  "120 m illumination reach • Deep-penetrating xenon spotlights (100° angle)"),
                    new UpgradeTierInfo(5, 1500, "170 m illumination reach • Full-field visibility")
                }
            },

            // 6. UTILITIES UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Utilities,
                displayName = "Utilities & Extraction Arm",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Mechanical claw • Single-slot debris collection"),
                    new UpgradeTierInfo(2, 150,  "Hydraulic suction • +25% claw drop and retrieval speed"),
                    new UpgradeTierInfo(3, 350,  "Multi-grip arm • Collects up to 2 items per drop"),
                    new UpgradeTierInfo(4, 750,  "Auto-sort scanner • Highlights correct bins in sorting UI"),
                    new UpgradeTierInfo(5, 1500, "Magnetic extraction • Cleans clusters of debris simultaneously")
                }
            }
        };
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void ShowShop()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
        if (shopPanel == null)
        {
            BuildProceduralShopUI();
        }

        _isOpen = true;
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            shopPanel.transform.SetAsLastSibling();
            shopPanel.transform.localPosition = new Vector3(shopPanel.transform.localPosition.x, shopPanel.transform.localPosition.y, 0f);

            // Ensure shop panel renders above all other canvases (e.g. ZoneSelectionUI canvas at 9998)
            var canvas = shopPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = shopPanel.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;

            var raycaster = shopPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                shopPanel.AddComponent<GraphicRaycaster>();
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideShop);
            closeButton.onClick.AddListener(HideShop);
        }

        if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(false);
        if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(false);

        RefreshShopUI();
    }

    public void HideShop()
    {
        _isOpen = false;
        if (shopPanel != null) shopPanel.SetActive(false);

        if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(true);
        if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(true);

        if (ZoneSelectionUI.Instance != null && ZoneSelectionUI.Instance.IsOpen)
        {
            ZoneSelectionUI.Instance.RefreshAllZoneCards();
            ZoneSelectionUI.Instance.transform.SetAsLastSibling();
        }
    }

    public void ToggleShop()
    {
        bool isActuallyOpen = _isOpen && shopPanel != null && shopPanel.activeInHierarchy;
        if (isActuallyOpen) HideShop();
        else                ShowShop();
    }

    public int GetCurrentTier(UpgradeCategory category)
    {
        if (GameManager.Instance == null) return 1;

        return category switch
        {
            UpgradeCategory.Hull      => GameManager.Instance.HullTier,
            UpgradeCategory.Sonar     => GameManager.Instance.SonarTier,
            UpgradeCategory.Scanner   => GameManager.Instance.ScannerTier,
            UpgradeCategory.Engine    => GameManager.Instance.EngineTier,
            UpgradeCategory.Lights    => GameManager.Instance.LightTier,
            UpgradeCategory.Utilities => GameManager.Instance.UtilitiesTier,
            _                         => 1
        };
    }

    public void SetTier(UpgradeCategory category, int tier)
    {
        if (GameManager.Instance == null) return;
        tier = Mathf.Clamp(tier, 1, 5);

        switch (category)
        {
            case UpgradeCategory.Hull:      GameManager.Instance.HullTier = tier; break;
            case UpgradeCategory.Sonar:     GameManager.Instance.SonarTier = tier; break;
            case UpgradeCategory.Scanner:   GameManager.Instance.ScannerTier = tier; break;
            case UpgradeCategory.Engine:    GameManager.Instance.EngineTier = tier; break;
            case UpgradeCategory.Lights:    GameManager.Instance.LightTier = tier; break;
            case UpgradeCategory.Utilities: GameManager.Instance.UtilitiesTier = tier; break;
        }
    }

    public UpgradeCategoryConfig GetConfig(UpgradeCategory category)
    {
        return categoryConfigs.Find(c => c.category == category);
    }

    public int GetNextTierCost(UpgradeCategory category)
    {
        int currentTier = GetCurrentTier(category);
        if (currentTier >= 5) return 0;

        var config = GetConfig(category);
        if (config != null && config.tiers != null && currentTier < config.tiers.Length)
        {
            return config.tiers[currentTier].costRDP; // Index currentTier is the next upgrade tier
        }
        return 0;
    }

    public bool TryPurchaseUpgrade(UpgradeCategory category)
    {
        int currentTier = GetCurrentTier(category);
        if (currentTier >= 5) return false;

        int cost = GetNextTierCost(category);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendRDP(cost))
        {
            Debug.Log($"[ShopManager] Cannot afford upgrade for {category} (Cost: {cost} RDP).");
            return false;
        }

        int newTier = currentTier + 1;
        SetTier(category, newTier);
        Debug.Log($"[ShopManager] Upgraded {category} to Tier {newTier} for {cost} RDP.");

        OnUpgradePurchased?.Invoke(category, newTier);
        RefreshShopUI();
        ZoneSelectionUI.Instance?.RefreshAllZoneCards();
        return true;
    }

    // -----------------------------------------------------------------------
    // UI Refreshing
    // -----------------------------------------------------------------------

    public void RefreshShopUI()
    {
        int currentRDP = GameManager.Instance != null ? GameManager.Instance.RDP : 0;

        if (currentRdpText != null)
            currentRdpText.text = $"RESEARCH DATA POINTS:  <color=#2EE8C6>{currentRDP:N0} RDP</color>";

        // 1. Dynamic card spawning in ScrollView
        if (entryContainer != null && upgradeCardPrefab != null)
        {
            for (int i = entryContainer.childCount - 1; i >= 0; i--)
                Destroy(entryContainer.GetChild(i).gameObject);

            foreach (var cfg in categoryConfigs)
            {
                int currentTier = GetCurrentTier(cfg.category);
                int nextCost    = GetNextTierCost(cfg.category);
                bool canAfford  = currentRDP >= nextCost;

                string perkDesc = (currentTier < cfg.tiers.Length)
                    ? cfg.tiers[currentTier < 5 ? currentTier : 4].perkDescription
                    : "";

                var cardGO = Instantiate(upgradeCardPrefab, entryContainer);
                var cardUI = cardGO.GetComponent<UpgradeCardUI>();
                if (cardUI != null)
                {
                    cardUI.Setup(
                        cfg.category,
                        cfg.icon,
                        cfg.displayName,
                        currentTier,
                        5,
                        nextCost,
                        perkDesc,
                        canAfford,
                        cat => TryPurchaseUpgrade(cat)
                    );
                }
            }
        }

        // 2. Static cards if manually placed in hierarchy
        if (staticCards != null && staticCards.Length > 0)
        {
            for (int i = 0; i < staticCards.Length; i++)
            {
                if (staticCards[i] == null || i >= categoryConfigs.Count) continue;
                var cfg = categoryConfigs[i];

                int currentTier = GetCurrentTier(cfg.category);
                int nextCost    = GetNextTierCost(cfg.category);
                bool canAfford  = currentRDP >= nextCost;

                string perkDesc = (currentTier < cfg.tiers.Length)
                    ? cfg.tiers[currentTier < 5 ? currentTier : 4].perkDescription
                    : "";

                staticCards[i].Setup(
                    cfg.category,
                    cfg.icon,
                    cfg.displayName,
                    currentTier,
                    5,
                    nextCost,
                    perkDesc,
                    canAfford,
                    cat => TryPurchaseUpgrade(cat)
                );
            }
        }
    }

    // -----------------------------------------------------------------------
    // Procedural Fallback UI Construction
    // -----------------------------------------------------------------------

    private void BuildProceduralShopUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Dark dim backdrop
        var rootGO = new GameObject("ShopModalRoot", typeof(RectTransform), typeof(Image), typeof(GraphicRaycaster));
        rootGO.transform.SetParent(canvas.transform, false);
        var rootCanvas = rootGO.AddComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 9999;

        var rootRect = rootGO.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.sizeDelta = Vector2.zero;
        var bg = rootGO.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        bg.raycastTarget = true;
        shopPanel = rootGO;

        // Main Window Frame (Glassmorphism dark-slate)
        var winGO = new GameObject("ShopWindow", typeof(RectTransform), typeof(Image));
        winGO.transform.SetParent(rootRect, false);
        var winRect = winGO.GetComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0.5f, 0.5f);
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot     = new Vector2(0.5f, 0.5f);
        winRect.sizeDelta = new Vector2(880f, 540f);
        var winImg = winGO.GetComponent<Image>();
        winImg.color = new Color(0.04f, 0.08f, 0.14f, 0.96f);

        // Window Border Outline
        var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(winRect, false);
        var br = borderGO.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;
        var bImg = borderGO.GetComponent<Image>();
        bImg.sprite = WhiteSprite;
        bImg.color = new Color(0.12f, 0.65f, 0.75f, 0.35f);
        bImg.raycastTarget = false;

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        // Title Header
        var titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(winRect, false);
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.04f, 0.86f); tr.anchorMax = new Vector2(0.55f, 0.98f); tr.sizeDelta = Vector2.zero;
        var tt = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tt.font = font;
        tt.text = "<b>SUBMARINE UPGRADE BAY</b>";
        tt.fontSize = 36; tt.color = new Color(0.20f, 0.92f, 0.82f, 1f);
        tt.alignment = TextAlignmentOptions.Left;

        // RDP Display Text
        var rdpGO = new GameObject("RdpText", typeof(RectTransform));
        rdpGO.transform.SetParent(winRect, false);
        var rr = rdpGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.55f, 0.86f); rr.anchorMax = new Vector2(0.88f, 0.98f); rr.sizeDelta = Vector2.zero;
        currentRdpText = rdpGO.AddComponent<TextMeshProUGUI>();
        if (font != null) currentRdpText.font = font;
        currentRdpText.fontSize = 36; currentRdpText.color = Color.white;
        currentRdpText.fontStyle = FontStyles.Bold;
        currentRdpText.alignment = TextAlignmentOptions.Right;

        // Close Button [X]
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(winRect, false);
        var cr = closeGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.91f, 0.88f); cr.anchorMax = new Vector2(0.98f, 0.97f); cr.sizeDelta = Vector2.zero;
        closeGO.GetComponent<Image>().color = new Color(0.85f, 0.20f, 0.20f, 0.85f);
        closeButton = closeGO.GetComponent<Button>();
        closeButton.onClick.AddListener(HideShop);

        var closeTxtGO = new GameObject("Txt", typeof(RectTransform));
        closeTxtGO.transform.SetParent(cr, false);
        var ctr = closeTxtGO.GetComponent<RectTransform>();
        ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one; ctr.sizeDelta = Vector2.zero;
        var ctxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        if (font != null) ctxt.font = font;
        ctxt.text = "✕"; ctxt.fontSize = 36; ctxt.alignment = TextAlignmentOptions.Center; ctxt.color = Color.white;

        // Scroll Area for Upgrade Rows
        var scrollGO = new GameObject("ShopScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGO.transform.SetParent(winRect, false);
        var sr = scrollGO.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.03f, 0.04f); sr.anchorMax = new Vector2(0.97f, 0.86f); sr.sizeDelta = Vector2.zero;
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scrollGO.GetComponent<Mask>().showMaskGraphic = false;

        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 25f;

        // Content container
        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(sr, false);
        var contr = contentGO.GetComponent<RectTransform>();
        contr.anchorMin = new Vector2(0f, 1f); contr.anchorMax = new Vector2(1f, 1f);
        contr.pivot = new Vector2(0.5f, 1f); contr.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contr;
        entryContainer = contr;

        // Procedurally construct Card rows for each category
        var cardList = new List<UpgradeCardUI>();
        foreach (var cfg in categoryConfigs)
        {
            var card = CreateProceduralCard(contr, cfg);
            cardList.Add(card);
        }
        staticCards = cardList.ToArray();
    }

    private UpgradeCardUI CreateProceduralCard(Transform parent, UpgradeCategoryConfig cfg)
    {
        var rowGO = new GameObject($"Card_{cfg.category}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var rRect = rowGO.GetComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0f, 68f);

        var le = rowGO.GetComponent<LayoutElement>();
        le.minHeight = 68f; le.preferredHeight = 68f;

        var rImg = rowGO.GetComponent<Image>();
        rImg.color = new Color(0.08f, 0.14f, 0.22f, 0.85f);

        var cardUI = rowGO.AddComponent<UpgradeCardUI>();

        // Row Inner Layout:
        // Left: Title (and tier pips)
        var leftGO = new GameObject("TitleArea", typeof(RectTransform));
        leftGO.transform.SetParent(rRect, false);
        var lr = leftGO.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.02f, 0.48f); lr.anchorMax = new Vector2(0.40f, 0.95f); lr.sizeDelta = Vector2.zero;
        var titleTxt = leftGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = cfg.displayName;
        titleTxt.fontSize = 18; titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = new Color(0.25f, 0.92f, 0.80f);

        // Perk Description (under title)
        var descGO = new GameObject("DescArea", typeof(RectTransform));
        descGO.transform.SetParent(rRect, false);
        var dr = descGO.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.02f, 0.08f); dr.anchorMax = new Vector2(0.68f, 0.48f); dr.sizeDelta = Vector2.zero;
        var descTxt = descGO.AddComponent<TextMeshProUGUI>();
        descTxt.fontSize = 13; descTxt.color = new Color(0.80f, 0.88f, 0.92f, 0.90f);
        descTxt.overflowMode = TextOverflowModes.Ellipsis;

        // Tier Pips container (5 pips)
        var pipsGO = new GameObject("Pips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        pipsGO.transform.SetParent(rRect, false);
        var pr = pipsGO.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.42f, 0.52f); pr.anchorMax = new Vector2(0.65f, 0.88f); pr.sizeDelta = Vector2.zero;
        var hlg = pipsGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false; hlg.childControlHeight = false;

        var pips = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var pip = new GameObject($"Pip_{i + 1}", typeof(RectTransform), typeof(Image));
            pip.transform.SetParent(pr, false);
            var pipR = pip.GetComponent<RectTransform>();
            pipR.sizeDelta = new Vector2(16f, 16f);
            var pipImg = pip.GetComponent<Image>();
            pipImg.sprite = WhiteSprite;
            pipImg.color = (i == 0) ? new Color(0.12f, 0.95f, 0.78f) : new Color(0.15f, 0.20f, 0.28f, 0.8f);
            pips[i] = pipImg;
        }

        // Cost Label
        var costGO = new GameObject("CostArea", typeof(RectTransform));
        costGO.transform.SetParent(rRect, false);
        var costr = costGO.GetComponent<RectTransform>();
        costr.anchorMin = new Vector2(0.68f, 0.20f); costr.anchorMax = new Vector2(0.82f, 0.80f); costr.sizeDelta = Vector2.zero;
        var costTxt = costGO.AddComponent<TextMeshProUGUI>();
        costTxt.fontSize = 15; costTxt.fontStyle = FontStyles.Bold;
        costTxt.alignment = TextAlignmentOptions.Center;

        // Upgrade Button
        var btnGO = new GameObject("UpgradeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(rRect, false);
        var btnr = btnGO.GetComponent<RectTransform>();
        btnr.anchorMin = new Vector2(0.84f, 0.15f); btnr.anchorMax = new Vector2(0.98f, 0.85f); btnr.sizeDelta = Vector2.zero;
        var btnImg = btnGO.GetComponent<Image>();
        btnImg.sprite = WhiteSprite;
        btnImg.color = new Color(0.08f, 0.75f, 0.68f, 1f);
        var btn = btnGO.GetComponent<Button>();

        var btnLblGO = new GameObject("Lbl", typeof(RectTransform));
        btnLblGO.transform.SetParent(btnr, false);
        var blr = btnLblGO.GetComponent<RectTransform>();
        blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one; blr.sizeDelta = Vector2.zero;
        var btnLbl = btnLblGO.AddComponent<TextMeshProUGUI>();
        btnLbl.text = "UPGRADE"; btnLbl.fontSize = 14; btnLbl.fontStyle = FontStyles.Bold;
        btnLbl.alignment = TextAlignmentOptions.Center; btnLbl.color = Color.white;

        // Wire serialized references on the card component
        cardUI.InjectReferences(titleTxt, costTxt, descTxt, btn, btnLbl, pips);

        return cardUI;
    }
}

