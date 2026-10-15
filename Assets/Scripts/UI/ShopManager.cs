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
    public UpgradeTierInfo[] tiers = new UpgradeTierInfo[0];
    public int MaxTier => (tiers != null && tiers.Length > 0) ? tiers.Length : ((category == UpgradeCategory.Utilities) ? 3 : 5);
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

    [Header("3D Submarine Viewport (Right Column ~40%)")]
    [Tooltip("Studio manager for rendering the 3D submarine.")]
    [SerializeField] private SubmarineShopPreview submarineShopPreview;

    [Tooltip("The RawImage displaying the 3D submarine in the right column.")]
    [SerializeField] private RawImage submarineViewport;

    [Tooltip("Optional Submarine 3D visual prefab. If null, auto-loads Resources/Submarine_ShopVisual.")]
    [SerializeField] private GameObject submarinePreviewPrefab;

    [Header("Dynamic ScrollView (Optional)")]
    [Tooltip("Scroll Rect Content Transform where upgrade cards are spawned.")]
    [SerializeField] private Transform entryContainer;

    [Tooltip("Prefab containing UpgradeCardUI for spawning each row.")]
    [SerializeField] private GameObject upgradeCardPrefab;

    [Header("Static Cards (Optional)")]
    [Tooltip("Direct references if you build static upgrade rows directly in the scene.")]
    [SerializeField] private UpgradeCardUI[] staticCards;

    [Header("Category Logos (Assign in Inspector)")]
    [Tooltip("Logo icon for Reinforced Hull")]
    [SerializeField] private Sprite hullLogo;

    [Tooltip("Logo icon for Sonar")]
    [SerializeField] private Sprite sonarLogo;

    [Tooltip("Logo icon for Research Scanner")]
    [SerializeField] private Sprite scannerLogo;

    [Tooltip("Logo icon for Engine")]
    [SerializeField] private Sprite engineLogo;

    [Tooltip("Logo icon for Lights")]
    [SerializeField] private Sprite lightsLogo;

    [Tooltip("Logo icon for Utilities")]
    [SerializeField] private Sprite utilitiesLogo;

    [Header("Audio (Optional — defaults to AudioManager)")]
    [Tooltip("Sound played upon purchasing an upgrade (buying an upgrade.mp3).")]
    [SerializeField] private AudioClip buyUpgradeSFX;

    [Header("Upgrade Categories & Perks")]
    [SerializeField] private List<UpgradeCategoryConfig> categoryConfigs = new List<UpgradeCategoryConfig>();

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private bool _isOpen = false;
    public bool IsOpen => _isOpen || (shopPanel != null && shopPanel.activeInHierarchy);

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

    /// <summary>
    /// Returns the assigned logo sprite for a category.
    /// Priority: Inspector field -> Category config icon -> Resources/UI/UpgradeLogos/
    /// </summary>
    public Sprite GetCategoryIcon(UpgradeCategory category)
    {
        Sprite sprite = category switch
        {
            UpgradeCategory.Hull      => hullLogo,
            UpgradeCategory.Sonar     => sonarLogo,
            UpgradeCategory.Scanner   => scannerLogo,
            UpgradeCategory.Engine    => engineLogo,
            UpgradeCategory.Lights    => lightsLogo,
            UpgradeCategory.Utilities => utilitiesLogo,
            _                         => null
        };

        if (sprite != null) return sprite;

        var cfg = GetConfig(category);
        if (cfg != null && cfg.icon != null) return cfg.icon;

        // Auto-load fallback from Resources/UI/UpgradeLogos/{category}
        return Resources.Load<Sprite>($"UI/UpgradeLogos/{category}");
    }

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        InitializeDefaultConfigs();

        EnsureSubmarinePreviewConfigured();
    }

    private void OnValidate()
    {
        if (categoryConfigs == null || categoryConfigs.Count == 0)
        {
            InitializeDefaultConfigs();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Populate / Reset Default Configs into Inspector")]
    public void PopulateDefaultConfigsIntoInspector()
    {
        categoryConfigs.Clear();
        InitializeDefaultConfigs();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ShopManager] Successfully populated default upgrade configs into Inspector!");
    }
#endif

    private void Start()
    {
        if (!_isOpen && shopPanel != null)
            shopPanel.SetActive(false);

        if (submarineShopPreview != null)
            submarineShopPreview.SetVisible(_isOpen);

        if (closeButton != null)
            closeButton.onClick.AddListener(HideShop);
    }

    // -----------------------------------------------------------------------
    // Exact Upgrade Configs (Hull, Sonar, Scanner, Engine, Lights, Utilities)
    // -----------------------------------------------------------------------

    private void InitializeDefaultConfigs()
    {
        if (categoryConfigs != null && categoryConfigs.Count > 0)
        {
            foreach (var cfg in categoryConfigs)
            {
                cfg.displayName = cfg.category switch
                {
                    UpgradeCategory.Hull      => "Reinforced Hull",
                    UpgradeCategory.Sonar     => "Sonar",
                    UpgradeCategory.Scanner   => "Research Scanner",
                    UpgradeCategory.Engine    => "Engine",
                    UpgradeCategory.Lights    => "Lights",
                    UpgradeCategory.Utilities => "Utilities",
                    _                         => cfg.displayName
                };
            }

            var utilCfg = categoryConfigs.Find(c => c.category == UpgradeCategory.Utilities);
            if (utilCfg != null && utilCfg.tiers != null && utilCfg.tiers.Length > 3)
            {
                Array.Resize(ref utilCfg.tiers, 3);
            }
            return;
        }

        categoryConfigs = new List<UpgradeCategoryConfig>
        {
            // 1. HULL UPGRADES (Total: 6,250 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Hull,
                displayName = "Reinforced Hull",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Sunlight Zone (0 m depth rated) \nStandard pressure hull"),
                    new UpgradeTierInfo(2, 350,  "Twilight Zone (200 m depth rated) \nUnlocks Zone 2 descent"),
                    new UpgradeTierInfo(3, 900,  "Midnight Zone (1,000 m depth rated) \nUnlocks Zone 3 descent"),
                    new UpgradeTierInfo(4, 1800, "Abyssal Zone (4,000 m depth rated) \nUnlocks Zone 4 descent"),
                    new UpgradeTierInfo(5, 3200, "Hadal Zone (6,000 m depth rated) \nMaximum trench pressure rating")
                }
            },

            // 2. SONAR UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Sonar,
                displayName = "Sonar",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "50 m detection radius \nBasic dot blips"),
                    new UpgradeTierInfo(2, 150,  "100 m detection radius"),
                    new UpgradeTierInfo(3, 350,  "150 m detection radius \nElevation indicators"),
                    new UpgradeTierInfo(4, 750,  "200 m detection radius \nFaster sweep & pulse glow"),
                    new UpgradeTierInfo(5, 1500, "250 m detection radius \nMaximum range coverage")
                }
            },

            // 3. SCANNER UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Scanner,
                displayName = "Research Scanner",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "15% Focus Bar (MG1) \nStandard puzzle countdown timer (MG2)"),
                    new UpgradeTierInfo(2, 150,  "20% Focus Bar (MG1) \n+10s extra puzzle reconstruction time (MG2)"),
                    new UpgradeTierInfo(3, 350,  "30% Focus Bar (MG1) \n+20s extra puzzle reconstruction time (MG2)"),
                    new UpgradeTierInfo(4, 750,  "40% Focus Bar (MG1) \n1x Direct Tile Swap perk (MG2)"),
                    new UpgradeTierInfo(5, 1500, "50% Focus Bar (MG1) \n2x Direct Tile Swaps & +15s bonus time (MG2)")
                }
            },

            // 4. ENGINE UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Engine,
                displayName = "Engine",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Base exploration speed \nStandard turning response"),
                    new UpgradeTierInfo(2, 150,  "+20% speed \nFaster vertical ascent and descent"),
                    new UpgradeTierInfo(3, 350,  "+40% speed \nImproved current resistance"),
                    new UpgradeTierInfo(4, 750,  "+65% speed \n+25% lane-shift speed in Hazard Dodge"),
                    new UpgradeTierInfo(5, 1500, "+100% speed \nBoost burst against strong underwater currents")
                }
            },

            // 5. LIGHT UPGRADES (Total: 2,750 RDP)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Lights,
                displayName = "Lights",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "25 m illumination reach \nNarrow halogen beam (Sunlight Zone)"),
                    new UpgradeTierInfo(2, 150,  "50 m illumination reach \nHigh-intensity LED beam (45° angle)"),
                    new UpgradeTierInfo(3, 350,  "80 m illumination reach \nWide floodlight matrix (75° angle)"),
                    new UpgradeTierInfo(4, 750,  "120 m illumination reach \nDeep-penetrating xenon spotlights (100° angle)"),
                    new UpgradeTierInfo(5, 1500, "170 m illumination reach \nFull-field visibility")
                }
            },

            // 6. UTILITIES UPGRADES (Total: 500 RDP, Tiers 1-3)
            new UpgradeCategoryConfig
            {
                category    = UpgradeCategory.Utilities,
                displayName = "Utilities",
                tiers       = new UpgradeTierInfo[]
                {
                    new UpgradeTierInfo(1, 0,    "Mechanical claw \nSingle-slot debris collection"),
                    new UpgradeTierInfo(2, 150,  "Hydraulic suction \n+25% claw drop and retrieval speed"),
                    new UpgradeTierInfo(3, 350,  "Multi-grip arm \nCollects up to 2 items per drop")
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

        EnsureSubmarinePreviewConfigured();

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

        if (submarineShopPreview != null)
        {
            submarineShopPreview.SetVisible(true);
        }

        if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(false);
        if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(false);

        RefreshShopUI();
    }

    public void HideShop()
    {
        _isOpen = false;
        if (shopPanel != null) shopPanel.SetActive(false);

        if (submarineShopPreview != null)
        {
            submarineShopPreview.SetVisible(false);
        }

        if (ZoneSelectionUI.Instance != null && ZoneSelectionUI.Instance.IsOpen)
        {
            if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(false);
            if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(false);
            ZoneSelectionUI.Instance.RefreshAllZoneCards();
            ZoneSelectionUI.Instance.transform.SetAsLastSibling();
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(true);
            if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(true);
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

    public int GetMaxTier(UpgradeCategory category)
    {
        var config = GetConfig(category);
        if (config != null && config.tiers != null && config.tiers.Length > 0)
        {
            return config.tiers.Length;
        }
        return (category == UpgradeCategory.Utilities) ? 3 : 5;
    }

    public void SetTier(UpgradeCategory category, int tier)
    {
        if (GameManager.Instance == null) return;
        int maxTier = GetMaxTier(category);
        tier = Mathf.Clamp(tier, 1, maxTier);

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
        int maxTier = GetMaxTier(category);
        if (currentTier >= maxTier) return 0;

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
        int maxTier = GetMaxTier(category);
        if (currentTier >= maxTier) return false;

        int cost = GetNextTierCost(category);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendRDP(cost))
        {
            Debug.Log($"[ShopManager] Cannot afford upgrade for {category} (Cost: {cost} RDP).");
            return false;
        }

        int newTier = currentTier + 1;
        SetTier(category, newTier);
        Debug.Log($"[ShopManager] Upgraded {category} to Tier {newTier} for {cost} RDP.");

        if (buyUpgradeSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(buyUpgradeSFX);
        else
            AudioManager.Instance?.PlayBuyUpgradeSFX();

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
                int maxTier     = GetMaxTier(cfg.category);
                int nextCost    = GetNextTierCost(cfg.category);
                bool canAfford  = currentRDP >= nextCost;

                string perkDesc = (currentTier < maxTier && cfg.tiers != null && currentTier < cfg.tiers.Length)
                    ? cfg.tiers[currentTier].perkDescription
                    : (cfg.tiers != null && cfg.tiers.Length > 0 ? cfg.tiers[cfg.tiers.Length - 1].perkDescription : "");

                var cardGO = Instantiate(upgradeCardPrefab, entryContainer);
                var cardUI = cardGO.GetComponent<UpgradeCardUI>();
                if (cardUI != null)
                {
                    cardUI.Setup(
                        cfg.category,
                        GetCategoryIcon(cfg.category),
                        cfg.displayName,
                        currentTier,
                        maxTier,
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
                int maxTier     = GetMaxTier(cfg.category);
                int nextCost    = GetNextTierCost(cfg.category);
                bool canAfford  = currentRDP >= nextCost;

                string perkDesc = (currentTier < maxTier && cfg.tiers != null && currentTier < cfg.tiers.Length)
                    ? cfg.tiers[currentTier].perkDescription
                    : (cfg.tiers != null && cfg.tiers.Length > 0 ? cfg.tiers[cfg.tiers.Length - 1].perkDescription : "");

                staticCards[i].Setup(
                    cfg.category,
                    GetCategoryIcon(cfg.category),
                    cfg.displayName,
                    currentTier,
                    maxTier,
                    nextCost,
                    perkDesc,
                    canAfford,
                    cat => TryPurchaseUpgrade(cat)
                );
            }
        }
    }

    // -----------------------------------------------------------------------
    // Submarine 3D Viewport Setup for Prefab / Custom UI
    // -----------------------------------------------------------------------

    private void InitSubmarineShopPreview()
    {
        if (submarineShopPreview == null)
        {
            submarineShopPreview = GetComponent<SubmarineShopPreview>() ?? gameObject.AddComponent<SubmarineShopPreview>();
        }
        if (submarinePreviewPrefab != null)
        {
            submarineShopPreview.SubmarinePrefab = submarinePreviewPrefab;
        }
        submarineShopPreview.BindToRawImage(submarineViewport);
        submarineShopPreview.EnsureStudioReady();
    }

    /// <summary>
    /// Ensures that the 40% right-column 3D submarine preview is properly configured.
    /// If using ShopUI prefab, it moves the Scroll View to the left (~58%) without altering
    /// its style, and dynamically creates/binds the 3D submarine preview viewport on the right (~40%).
    /// </summary>
    public void EnsureSubmarinePreviewConfigured()
    {
        if (shopPanel == null) return;

        // 1. If viewport is already assigned, ensure studio is running
        if (submarineViewport != null)
        {
            InitSubmarineShopPreview();
            return;
        }

        // 2. Search hierarchy for existing viewport
        var existingVp = shopPanel.transform.Find("TopHeader/SubmarinePreviewColumn/SubmarineViewport")
                      ?? shopPanel.transform.Find("SubmarinePreviewColumn/SubmarineViewport")
                      ?? shopPanel.transform.Find("TopHeader/SubmarineViewport")
                      ?? shopPanel.transform.Find("SubmarineViewport");
        if (existingVp != null)
        {
            submarineViewport = existingVp.GetComponent<RawImage>();
            if (submarineViewport != null)
            {
                InitSubmarineShopPreview();
                return;
            }
        }

        // 3. Locate ScrollRect in shopPanel
        ScrollRect scrollRect = entryContainer != null 
            ? entryContainer.GetComponentInParent<ScrollRect>() 
            : shopPanel.GetComponentInChildren<ScrollRect>();

        if (scrollRect == null) return;

        var scrollTransform = scrollRect.GetComponent<RectTransform>();
        Transform parentTransform = scrollTransform.parent;

        // 4. Move Scroll View to the left column (~58% width) while keeping style identical
        scrollTransform.anchorMin = new Vector2(0f, scrollTransform.anchorMin.y);
        scrollTransform.anchorMax = new Vector2(0.585f, scrollTransform.anchorMax.y);
        scrollTransform.pivot = new Vector2(0.5f, scrollTransform.pivot.y);
        scrollTransform.anchoredPosition = new Vector2(0f, scrollTransform.anchoredPosition.y);
        scrollTransform.sizeDelta = new Vector2(-20f, scrollTransform.sizeDelta.y);

        // 5. Create Right Column (~40% width) matching the shop's style
        var rightColGO = new GameObject("SubmarinePreviewColumn", typeof(RectTransform), typeof(Image));
        rightColGO.transform.SetParent(parentTransform, false);
        var rcr = rightColGO.GetComponent<RectTransform>();

        // Align vertically with Scroll View
        rcr.anchorMin = new Vector2(0.60f, scrollTransform.anchorMin.y);
        rcr.anchorMax = new Vector2(1f, scrollTransform.anchorMax.y);
        rcr.pivot     = scrollTransform.pivot;
        rcr.anchoredPosition = new Vector2(0f, scrollTransform.anchoredPosition.y);
        rcr.sizeDelta = new Vector2(-25f, scrollTransform.sizeDelta.y);

        // Background styling matching ShopUI dark underwater theme
        var rcrImg = rightColGO.GetComponent<Image>();
        rcrImg.color = new Color(0.041f, 0.069f, 0.201f, 0.941f);

        // Subtle glowing border
        var rcBorderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        rcBorderGO.transform.SetParent(rcr, false);
        var rcBr = rcBorderGO.GetComponent<RectTransform>();
        rcBr.anchorMin = Vector2.zero; rcBr.anchorMax = Vector2.one; rcBr.sizeDelta = Vector2.zero;
        var rcBImg = rcBorderGO.GetComponent<Image>();
        rcBImg.sprite = WhiteSprite;
        rcBImg.color = new Color(0.12f, 0.65f, 0.75f, 0.35f);
        rcBImg.raycastTarget = false;

        TMP_FontAsset font = (currentRdpText != null) ? currentRdpText.font : UIThemeManager.AlohaFont;

        // Header Title
        var pHeaderGO = new GameObject("Header", typeof(RectTransform));
        pHeaderGO.transform.SetParent(rcr, false);
        var phr = pHeaderGO.GetComponent<RectTransform>();
        phr.anchorMin = new Vector2(0.04f, 0.91f); phr.anchorMax = new Vector2(0.96f, 0.985f); phr.sizeDelta = Vector2.zero;
        var pHeaderTxt = pHeaderGO.AddComponent<TextMeshProUGUI>();
        if (font != null) pHeaderTxt.font = font;
        pHeaderTxt.text = "<b>Submarine Preview</b>";
        pHeaderTxt.fontSize = 28;
        pHeaderTxt.color = new Color(0.25f, 0.92f, 0.80f);
        pHeaderTxt.alignment = TextAlignmentOptions.Center;

        // Viewport RawImage (expanded to full column height)
        var vpGO = new GameObject("SubmarineViewport", typeof(RectTransform), typeof(RawImage));
        vpGO.transform.SetParent(rcr, false);
        var vpr = vpGO.GetComponent<RectTransform>();
        vpr.anchorMin = new Vector2(0.02f, 0.02f); vpr.anchorMax = new Vector2(0.98f, 0.90f); vpr.sizeDelta = Vector2.zero;
        submarineViewport = vpGO.GetComponent<RawImage>();
        submarineViewport.color = Color.white;
        submarineViewport.raycastTarget = true;

        // Attach SubmarinePreviewRotator
        vpGO.AddComponent<SubmarinePreviewRotator>();

        // 6. Connect SubmarineShopPreview
        InitSubmarineShopPreview();
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

        // Main Window Frame (Glassmorphism dark-slate widescreen)
        var winGO = new GameObject("ShopWindow", typeof(RectTransform), typeof(Image));
        winGO.transform.SetParent(rootRect, false);
        var winRect = winGO.GetComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0.5f, 0.5f);
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot     = new Vector2(0.5f, 0.5f);
        winRect.sizeDelta = new Vector2(1080f, 560f);
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
        var font = UIThemeManager.AlohaFont;

        // Title Header
        var titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(winRect, false);
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.03f, 0.88f); tr.anchorMax = new Vector2(0.48f, 0.98f); tr.sizeDelta = Vector2.zero;
        var tt = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tt.font = font;
        tt.text = "<b>SUBMARINE UPGRADE BAY</b>";
        tt.fontSize = 24; tt.color = new Color(0.20f, 0.92f, 0.82f, 1f);
        tt.alignment = TextAlignmentOptions.Left;

        // RDP Display Text
        var rdpGO = new GameObject("RdpText", typeof(RectTransform));
        rdpGO.transform.SetParent(winRect, false);
        var rr = rdpGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.49f, 0.88f); rr.anchorMax = new Vector2(0.91f, 0.98f); rr.sizeDelta = Vector2.zero;
        currentRdpText = rdpGO.AddComponent<TextMeshProUGUI>();
        if (font != null) currentRdpText.font = font;
        currentRdpText.fontSize = 22; currentRdpText.color = Color.white;
        currentRdpText.fontStyle = FontStyles.Bold;
        currentRdpText.alignment = TextAlignmentOptions.Right;

        // Close Button [X]
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(winRect, false);
        var cr = closeGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.93f, 0.89f); cr.anchorMax = new Vector2(0.98f, 0.97f); cr.sizeDelta = Vector2.zero;
        closeGO.GetComponent<Image>().color = new Color(0.85f, 0.20f, 0.20f, 0.85f);
        closeButton = closeGO.GetComponent<Button>();
        closeButton.onClick.AddListener(HideShop);

        var closeTxtGO = new GameObject("Txt", typeof(RectTransform));
        closeTxtGO.transform.SetParent(cr, false);
        var ctr = closeTxtGO.GetComponent<RectTransform>();
        ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one; ctr.sizeDelta = Vector2.zero;
        var ctxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        if (font != null) ctxt.font = font;
        ctxt.text = "✕"; ctxt.fontSize = 24; ctxt.alignment = TextAlignmentOptions.Center; ctxt.color = Color.white;

        // =======================================================================
        // LEFT COLUMN: Upgrade Rows Scroll Area (~58-60% width)
        // =======================================================================
        var scrollGO = new GameObject("ShopScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGO.transform.SetParent(winRect, false);
        var sr = scrollGO.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.025f, 0.035f); sr.anchorMax = new Vector2(0.585f, 0.865f); sr.sizeDelta = Vector2.zero;
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scrollGO.GetComponent<Mask>().showMaskGraphic = false;

        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vr = viewportGO.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.sizeDelta = Vector2.zero;
        scrollRect.viewport = vr;

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contr = contentGO.GetComponent<RectTransform>();
        contr.anchorMin = new Vector2(0f, 1f); contr.anchorMax = new Vector2(1f, 1f);
        contr.pivot = new Vector2(0.5f, 1f); contr.sizeDelta = Vector2.zero;
        scrollRect.content = contr;

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Populate upgrade rows
        var cardList = new List<UpgradeCardUI>();
        foreach (var cfg in categoryConfigs)
        {
            var card = CreateProceduralCard(contr, cfg);
            cardList.Add(card);
        }
        staticCards = cardList.ToArray();

        // =======================================================================
        // RIGHT COLUMN: 3D Submarine Viewport (~40% width)
        // =======================================================================
        var rightColGO = new GameObject("SubmarinePreviewColumn", typeof(RectTransform), typeof(Image));
        rightColGO.transform.SetParent(winRect, false);
        var rcr = rightColGO.GetComponent<RectTransform>();
        rcr.anchorMin = new Vector2(0.595f, 0.035f);
        rcr.anchorMax = new Vector2(0.975f, 0.865f);
        rcr.sizeDelta = Vector2.zero;
        var rcrImg = rightColGO.GetComponent<Image>();
        rcrImg.color = new Color(0.03f, 0.06f, 0.10f, 0.92f);

        // Right Column Border
        var rcBorderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        rcBorderGO.transform.SetParent(rcr, false);
        var rcBr = rcBorderGO.GetComponent<RectTransform>();
        rcBr.anchorMin = Vector2.zero; rcBr.anchorMax = Vector2.one; rcBr.sizeDelta = Vector2.zero;
        var rcBImg = rcBorderGO.GetComponent<Image>();
        rcBImg.sprite = WhiteSprite;
        rcBImg.color = new Color(0.12f, 0.65f, 0.75f, 0.35f);
        rcBImg.raycastTarget = false;

        // Preview Header
        var pHeaderGO = new GameObject("Header", typeof(RectTransform));
        pHeaderGO.transform.SetParent(rcr, false);
        var phr = pHeaderGO.GetComponent<RectTransform>();
        phr.anchorMin = new Vector2(0.04f, 0.91f); phr.anchorMax = new Vector2(0.96f, 0.985f); phr.sizeDelta = Vector2.zero;
        var pHeaderTxt = pHeaderGO.AddComponent<TextMeshProUGUI>();
        if (font != null) pHeaderTxt.font = font;
        pHeaderTxt.text = "<b>Submarine Preview</b>";
        pHeaderTxt.fontSize = 20;
        pHeaderTxt.color = new Color(0.25f, 0.92f, 0.80f);
        pHeaderTxt.alignment = TextAlignmentOptions.Center;

        // Viewport RawImage (expanded to full column height)
        var vpGO = new GameObject("SubmarineViewport", typeof(RectTransform), typeof(RawImage));
        vpGO.transform.SetParent(rcr, false);
        var vpr = vpGO.GetComponent<RectTransform>();
        vpr.anchorMin = new Vector2(0.02f, 0.02f); vpr.anchorMax = new Vector2(0.98f, 0.90f); vpr.sizeDelta = Vector2.zero;
        submarineViewport = vpGO.GetComponent<RawImage>();
        submarineViewport.color = Color.white;
        submarineViewport.raycastTarget = true; // Crucial for drag rotation

        // Attach SubmarinePreviewRotator
        vpGO.AddComponent<SubmarinePreviewRotator>();

        // Instantiate / Initialize SubmarineShopPreview
        InitSubmarineShopPreview();
    }

    private UpgradeCardUI CreateProceduralCard(Transform parent, UpgradeCategoryConfig cfg)
    {
        var font = UIThemeManager.AlohaFont;
        var rowGO = new GameObject($"Card_{cfg.category}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var rRect = rowGO.GetComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0f, 96f);

        var le = rowGO.GetComponent<LayoutElement>();
        le.minHeight = 96f; le.preferredHeight = 96f;

        var rImg = rowGO.GetComponent<Image>();
        rImg.color = new Color(0.08f, 0.14f, 0.22f, 0.85f);

        var cardUI = rowGO.AddComponent<UpgradeCardUI>();

        // Row Inner Layout:
        // Left: Category Logo Icon (remains constant per category)
        var iconGO = new GameObject("CategoryIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(rRect, false);
        var ir = iconGO.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.02f, 0.12f); ir.anchorMax = new Vector2(0.12f, 0.88f); ir.sizeDelta = Vector2.zero;
        var iconImg = iconGO.GetComponent<Image>();
        var logoSprite = GetCategoryIcon(cfg.category);
        if (logoSprite != null)
        {
            iconImg.sprite = logoSprite;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
        }

        // Title Area (shows category name and current tier)
        var leftGO = new GameObject("TitleArea", typeof(RectTransform));
        leftGO.transform.SetParent(rRect, false);
        var lr = leftGO.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.14f, 0.52f); lr.anchorMax = new Vector2(0.64f, 0.95f); lr.sizeDelta = Vector2.zero;
        var titleTxt = leftGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTxt.font = font;
        titleTxt.text = cfg.displayName;
        titleTxt.fontSize = 19; titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = new Color(0.25f, 0.92f, 0.80f);

        // Perk Description (under title)
        var descGO = new GameObject("DescArea", typeof(RectTransform));
        descGO.transform.SetParent(rRect, false);
        var dr = descGO.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.14f, 0.05f); dr.anchorMax = new Vector2(0.64f, 0.50f); dr.sizeDelta = Vector2.zero;
        var descTxt = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) descTxt.font = font;
        descTxt.fontSize = 13; descTxt.color = new Color(0.80f, 0.88f, 0.92f, 0.90f);
        descTxt.overflowMode = TextOverflowModes.Ellipsis;

        // Cost Label
        var costGO = new GameObject("CostArea", typeof(RectTransform));
        costGO.transform.SetParent(rRect, false);
        var costr = costGO.GetComponent<RectTransform>();
        costr.anchorMin = new Vector2(0.65f, 0.12f); costr.anchorMax = new Vector2(0.80f, 0.88f); costr.sizeDelta = Vector2.zero;
        var costTxt = costGO.AddComponent<TextMeshProUGUI>();
        if (font != null) costTxt.font = font;
        costTxt.fontSize = 16; costTxt.fontStyle = FontStyles.Bold;
        costTxt.alignment = TextAlignmentOptions.Center;

        // Upgrade Button
        var btnGO = new GameObject("UpgradeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(rRect, false);
        var btnr = btnGO.GetComponent<RectTransform>();
        btnr.anchorMin = new Vector2(0.81f, 0.14f); btnr.anchorMax = new Vector2(0.98f, 0.86f); btnr.sizeDelta = Vector2.zero;
        var btnImg = btnGO.GetComponent<Image>();
        btnImg.sprite = WhiteSprite;
        btnImg.color = new Color(0.08f, 0.75f, 0.68f, 1f);
        var btn = btnGO.GetComponent<Button>();

        var btnLblGO = new GameObject("Lbl", typeof(RectTransform));
        btnLblGO.transform.SetParent(btnr, false);
        var blr = btnLblGO.GetComponent<RectTransform>();
        blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one; blr.sizeDelta = Vector2.zero;
        var btnLbl = btnLblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) btnLbl.font = font;
        btnLbl.text = "UPGRADE"; btnLbl.fontSize = 15; btnLbl.fontStyle = FontStyles.Bold;
        btnLbl.alignment = TextAlignmentOptions.Center; btnLbl.color = Color.white;

        // Wire serialized references on the card component
        cardUI.InjectReferences(titleTxt, costTxt, descTxt, btn, btnLbl, iconImg);

        return cardUI;
    }
}
