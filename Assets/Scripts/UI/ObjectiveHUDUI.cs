using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dedicated Objective HUD Card positioned below the Sonar Range Box on the top-left of the screen.
/// Decoupled from the Radio Textbox so mission objectives do not crowd radio communications.
///
/// Post-Tutorial Progression Missions ("TO GO DEEPER:"):
///   - Mission 1: "- Upgrade submarine Hull"
///   - Mission 2: "- Discover at least half of the {zone} species ({discovered}/{total})"
///
/// Progression Behaviors:
///   - Hidden completely during opening/story cinematics.
///   - When Hull meets the requirement for the next zone, Mission 1 disappears.
///   - When at least half of the zone species are discovered, Mission 2 disappears.
///   - When both missions are completed, the entire mission objectives card disappears.
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
                    var canvas = FindFirstObjectByType<Canvas>();
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

    private RectTransform _panelRect;
    private CanvasGroup   _canvasGroup;
    private TMP_Text      _headerText;
    private TMP_Text      _objectiveBodyText;
    private string        _currentObjective = "";
    private bool          _isCustomTutorialObjective = false;
    private float         _pollTimer = 0f;

    public bool IsVisible => _panelRect != null && _panelRect.gameObject.activeSelf && _canvasGroup != null && _canvasGroup.alpha > 0.01f;

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
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
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
            if (_panelRect != null && _panelRect.gameObject.activeSelf)
            {
                _panelRect.gameObject.SetActive(false);
            }
            return;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (inTutorial && _isCustomTutorialObjective)
        {
            // Restore visibility after minigame or cinematic ends if we have an active objective
            if (_panelRect != null && !_panelRect.gameObject.activeSelf && !string.IsNullOrWhiteSpace(_currentObjective))
            {
                _panelRect.gameObject.SetActive(true);
                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            }
            return;
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

    public void EnsureUI()
    {
        if (_panelRect != null) return;

        var canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Container: Compact card anchored top-left cleanly below the Sonar Range box (y = -380)
        var rootGO = new GameObject("ObjectiveCard_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        _panelRect = rootGO.GetComponent<RectTransform>();
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();

        _panelRect.anchorMin = new Vector2(0f, 1f);
        _panelRect.anchorMax = new Vector2(0f, 1f);
        _panelRect.pivot     = new Vector2(0f, 1f);
        _panelRect.sizeDelta = new Vector2(420f, 220f);
        _panelRect.anchoredPosition = new Vector2(35f, -380f);

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

        // Header Label: "TO GO DEEPER:" (11px, Cyan, Normal Weight)
        var headerGO = new GameObject("HeaderLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGO.transform.SetParent(rootGO.transform, false);
        var hRect = headerGO.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot     = new Vector2(0f, 1f);
        hRect.sizeDelta = new Vector2(0f, 22f);
        hRect.anchoredPosition = new Vector2(0f, -30f);

        _headerText = headerGO.GetComponent<TextMeshProUGUI>();
        _headerText.font = font;
        _headerText.fontStyle = FontStyles.Normal;
        _headerText.fontSize = 9f;
        _headerText.color = new Color(0.00f, 0.90f, 1.00f, 0.95f);
        _headerText.alignment = TextAlignmentOptions.Center;
        _headerText.raycastTarget = false;
        _headerText.text = "TO GO DEEPER:";

        // Objective Body Text (13px, Mint Green #2DFFAA, Normal Weight, Auto-wrapping)
        var bodyGO = new GameObject("ObjectiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(rootGO.transform, false);
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot     = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(14f, 14f);
        bodyRect.offsetMax = new Vector2(-14f, -50f);

        _objectiveBodyText = bodyGO.GetComponent<TextMeshProUGUI>();
        _objectiveBodyText.font = font;
        _objectiveBodyText.fontStyle = FontStyles.Normal;
        _objectiveBodyText.fontSize = 13f;
        _objectiveBodyText.enableAutoSizing = true;
        _objectiveBodyText.fontSizeMin = 9f;
        _objectiveBodyText.fontSizeMax = 13f;
        _objectiveBodyText.color = new Color(0.18f, 1.00f, 0.66f, 1.00f);
        _objectiveBodyText.alignment = TextAlignmentOptions.Center;
        _objectiveBodyText.textWrappingMode = TextWrappingModes.Normal;
        _objectiveBodyText.lineSpacing = 1f;
        _objectiveBodyText.raycastTarget = false;

        rootGO.SetActive(true);
    }

    /// <summary>
    /// Evaluates current zone progression and updates the dual missions:
    ///   - "- Upgrade submarine Hull" (disappears when hull tier meets requirement)
    ///   - "- Discover at least half of the {zone} species ({discovered}/{total})" (disappears when count met)
    ///   - When both are completed: entire mission objectives card disappears.
    /// </summary>
    public void RefreshProgressionObjectives()
    {
        EnsureUI();

        if (IsCinematicActive() || IsMinigameActive())
        {
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
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
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
            return;
        }

        ZoneDefinition curZoneDef = ZoneConfig.Zones[currentZone];
        ZoneDefinition nextZoneDef = ZoneConfig.Zones[nextZone];

        // 1. Mission 1: Upgrade submarine Hull
        int currentHull = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;
        int requiredHull = nextZoneDef.requiredHullTier;
        bool hullCompleted = currentHull >= requiredHull;

        // 2. Mission 2: Discover at least half of the (zone) species (no. of discovered/15)
        int totalSpecies = curZoneDef.totalSpeciesCount > 0 ? curZoneDef.totalSpeciesCount : 15;
        int requiredSpecies = Mathf.CeilToInt(totalSpecies * curZoneDef.unlockThreshold);
        int discoveredSpecies = GameManager.Instance != null
            ? GameManager.Instance.GetDiscoveredCountInZone(currentZone)
            : 0;
        bool speciesCompleted = discoveredSpecies >= requiredSpecies;

        // 3. When both missions are completed, the entire mission objectives card disappears!
        if (hullCompleted && speciesCompleted)
        {
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
            return;
        }

        // At least one mission is still pending — display the card
        if (_panelRect != null && !_panelRect.gameObject.activeSelf)
        {
            _panelRect.gameObject.SetActive(true);
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        // Configure Header
        if (_headerText != null)
        {
            _headerText.text = "TO GO DEEPER:";
        }

        // Configure Body Text with '-' bullet markers and single line separation
        if (_objectiveBodyText != null)
        {
            _objectiveBodyText.alignment = TextAlignmentOptions.Center;

            string zoneName = curZoneDef.zoneName ?? "Sunlight";
            string cleanZoneName = zoneName.Replace("Zone", "").Trim();

            string bullet1 = "- Upgrade submarine Hull";
            string bullet2 = $"- Discover at least half of the {cleanZoneName} species ({discoveredSpecies}/{totalSpecies})";

            if (!hullCompleted && !speciesCompleted)
            {
                // Both missions active — single newline, no empty space
                _objectiveBodyText.text = $"{bullet1}\n{bullet2}";
            }
            else if (!hullCompleted && speciesCompleted)
            {
                // Species finished -> Bullet 2 disappears, only Bullet 1 remains
                _objectiveBodyText.text = bullet1;
            }
            else if (hullCompleted && !speciesCompleted)
            {
                // Hull finished -> Bullet 1 disappears, only Bullet 2 remains
                _objectiveBodyText.text = bullet2;
            }
        }
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
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
            return;
        }

        if (_headerText != null) _headerText.text = "MISSION OBJECTIVE";
        if (_objectiveBodyText != null)
        {
            _objectiveBodyText.alignment = TextAlignmentOptions.Center;
            _objectiveBodyText.text = clean.ToUpperInvariant();
        }

        if (_panelRect != null) _panelRect.gameObject.SetActive(true);
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    public void SetVisible(bool visible)
    {
        EnsureUI();

        if (!visible || IsCinematicActive() || IsMinigameActive())
        {
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
            return;
        }

        bool inTutorial = (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                          && !GameManager.IsTutorialCompleted();

        if (inTutorial && _isCustomTutorialObjective)
        {
            if (_panelRect != null) _panelRect.gameObject.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }
        else
        {
            RefreshProgressionObjectives();
        }
    }

    public void Hide()
    {
        if (_panelRect != null)
        {
            _panelRect.gameObject.SetActive(false);
        }
    }
}
