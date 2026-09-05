using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Bestiary Encyclopedia UI.
///
/// Features:
///   - Continuous Ocean Descent view (0m to 11,000m) with zone dividers & progress meters.
///   - Zone quick-jump filter tabs (Sunlight, Twilight, Midnight, Abyss, Hadal, All Depths).
///   - Card Prefabs (ScrollView):
///       * Discovered: 3D model on the left + Common/Scientific names. Clicking opens Detail Modal.
///       * Undiscovered: Blackened 3D silhouette on the left + Search Clue & Class. Clicking is locked.
///   - Detail Modal: Displays the Real Biological Photograph and complete scientific research data.
/// </summary>
public class BestiaryManager : MonoBehaviour
{
    public static BestiaryManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Data")]
    [SerializeField] private SpeciesRegistry registry;

    [Header("UI Panels")]
    [Tooltip("Main Bestiary root panel (toggled on/off).")]
    [SerializeField] private GameObject bestiaryPanel;

    [Tooltip("Scroll Rect Content Transform where cards are populated.")]
    [SerializeField] private Transform entryContainer;

    [Tooltip("Close button on the main Bestiary panel (e.g. [X] or [Back]).")]
    [SerializeField] private Button bestiaryCloseButton;

    [Tooltip("Optional: Top zone quick-jump buttons (0=Sunlight, 1=Twilight, 2=Midnight, 3=Abyss, 4=Hadal, 5=All).")]
    [SerializeField] private Button[] zoneTabs = new Button[6];

    [Tooltip("Overall progress text (e.g. '18 / 50 Discovered (36%)').")]
    [SerializeField] private TMP_Text overallProgressText;

    [Header("Custom Card & Header Prefabs (Assign your UI Prefabs here)")]
    [Tooltip("Custom Zone Header prefab (attach ZoneHeaderUI to it for custom layout & typography).")]
    [SerializeField] private GameObject customZoneHeaderPrefab;

    [Tooltip("Prefab for discovered species (3D model thumbnail, cyan accent).")]
    [SerializeField] private GameObject discoveredCardPrefab;

    [Tooltip("Prefab for undiscovered species (3D silhouette thumbnail, search clue).")]
    [SerializeField] private GameObject undiscoveredCardPrefab;

    [Tooltip("Fallback single prefab if you only want one unified card prefab.")]
    [SerializeField] private GameObject entryCardPrefab;

    [Header("3D Model Inspection Modal")]
    [Tooltip("Custom 3D Model Inspection prefab (attach ModelInspectionModalUI). If null, a default programmatic modal is created.")]
    [SerializeField] private GameObject customModelInspectionPrefab;

    [Header("Detail Modal (Popup on Discovered Card Click)")]
    [Tooltip("Custom Detail Modal GameObject (opened ONLY for discovered species).")]
    [SerializeField] private GameObject detailModal;

    [Header("Detail Modal UI References")]
    [Tooltip("Displays the Real Biological Photograph of the species.")]
    [SerializeField] private Image    detailPhoto;
    [SerializeField] private TMP_Text detailCommonName;
    [SerializeField] private TMP_Text detailScientificName;
    [SerializeField] private TMP_Text detailClass;
    [SerializeField] private TMP_Text detailHabitat;
    [SerializeField] private TMP_Text detailCharacteristics;
    [SerializeField] private TMP_Text detailEcologicalRole;
    [SerializeField] private TMP_Text detailFact;
    [SerializeField] private TMP_Text detailReward;
    [SerializeField] private Button   detailCloseButton;

    [Header("Scroll")]
    [Tooltip("ScrollRect containing the entry list. Auto-found if null.")]
    [SerializeField] private ScrollRect scrollRect;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private int  _activeFilter = -1; // -1 = All Depths (Continuous Descent)
    private bool _isOpen       = false;
    public bool IsOpen => _isOpen || (bestiaryPanel != null && bestiaryPanel.activeSelf) || (detailModal != null && detailModal.activeSelf);

    // Maps speciesId -> the instantiated card RectTransform for scroll-to
    private readonly Dictionary<string, RectTransform> _cardMap = new Dictionary<string, RectTransform>();

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (!_isOpen && bestiaryPanel != null) bestiaryPanel.SetActive(false);
        if (detailModal != null)   detailModal.SetActive(false);

        if (bestiaryCloseButton != null)
            bestiaryCloseButton.onClick.AddListener(HideBestiary);

        if (detailCloseButton != null)
            detailCloseButton.onClick.AddListener(CloseDetailModal);

        for (int i = 0; i < zoneTabs.Length; i++)
        {
            if (zoneTabs[i] == null) continue;
            int tabIndex = i < 5 ? i : -1;
            zoneTabs[i].onClick.AddListener(() => SelectTab(tabIndex));
        }

        if (registry == null)
            registry = Resources.Load<SpeciesRegistry>("SpeciesRegistry");
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Safely finds or instantiates BestiaryManager and opens the Bestiary modal.
    /// Handles inactive GameObjects and missing instances cleanly.
    /// If speciesId is provided, scrolls directly to that species card.
    /// </summary>
    public static BestiaryManager OpenBestiary(string speciesId = null)
    {
        var bm = Instance ?? FindFirstObjectByType<BestiaryManager>(FindObjectsInactive.Include);
        if (bm == null)
        {
            var pCanvas = FindFirstObjectByType<Canvas>();
            var prefab = Resources.Load<GameObject>("UI/BestiaryUI")
                      ?? Resources.Load<GameObject>("Prefabs/UI/BestiaryUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab, pCanvas != null ? pCanvas.transform : null);
                go.name = "BestiaryUI";
                bm = go.GetComponentInChildren<BestiaryManager>(true);
            }
        }

        if (bm != null)
        {
            bm.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(speciesId))
                bm.ShowBestiaryAndScrollTo(speciesId);
            else
                bm.ShowBestiary();
        }
        else
        {
            Debug.LogError("[BestiaryManager] Failed to locate or load BestiaryManager / BestiaryUI!");
        }
        return bm;
    }

    public void ShowBestiary()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
        _isOpen = true;
        if (bestiaryPanel != null)
        {
            bestiaryPanel.SetActive(true);
            bestiaryPanel.transform.SetAsLastSibling();
            bestiaryPanel.transform.localPosition = new Vector3(bestiaryPanel.transform.localPosition.x, bestiaryPanel.transform.localPosition.y, 0f);

            var canvas = bestiaryPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = bestiaryPanel.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;

            var raycaster = bestiaryPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                bestiaryPanel.AddComponent<GraphicRaycaster>();
            }
        }
        else
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(false);
        if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(false);
        RefreshBestiary();
    }

    public void HideBestiary()
    {
        _isOpen = false;
        if (bestiaryPanel != null) bestiaryPanel.SetActive(false);
        if (UIManager.Instance != null) UIManager.Instance.SetExplorationHUDVisible(true);
        if (ScanReticleUI.Instance != null) ScanReticleUI.Instance.SetVisible(true);
        CloseDetailModal();
        CloseModelInspectionModal();
    }

    public void ToggleBestiary()
    {
        bool isActuallyOpen = _isOpen && bestiaryPanel != null && bestiaryPanel.activeInHierarchy;
        if (isActuallyOpen) HideBestiary();
        else                ShowBestiary();
    }

    public void SelectTab(int filterIndex)
    {
        _activeFilter = filterIndex;
        RefreshBestiary();
    }

    /// <summary>
    /// Opens the dedicated 3D Model Inspection Modal in the center of the screen
    /// with interactive 360° touch/mouse drag rotation.
    /// Works for both discovered species (full color) and undiscovered species (3D silhouette).
    /// </summary>
    public void OpenModelInspectionModal(SpeciesData data, bool isDiscovered)
    {
        if (data == null) return;
        CloseDetailModal();
        CloseModelInspectionModal();

        var canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // ── Custom Prefab Path ──
        if (customModelInspectionPrefab != null)
        {
            var modalGO = Instantiate(customModelInspectionPrefab, canvas.transform);
            modalGO.name = "_3DModelInspectionModal";
            var modalUI = modalGO.GetComponent<ModelInspectionModalUI>()
                       ?? modalGO.AddComponent<ModelInspectionModalUI>();
            modalUI.Setup(data, isDiscovered);
            return;
        }

        // ── Default Programmatic Path ──
        var defaultModalGO = new GameObject("_3DModelInspectionModal", typeof(RectTransform), typeof(Image));
        defaultModalGO.transform.SetParent(canvas.transform, false);

        var rect = defaultModalGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Semi-transparent dark ocean backdrop
        var bgImg = defaultModalGO.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);

        var font = UIThemeManager.AlohaFont;

        var cardGO = new GameObject("InspectionModalCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(defaultModalGO.transform, false);
        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot     = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(720f, 680f);
        cardRect.anchoredPosition = Vector2.zero;

        var bg = cardGO.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.15f, 0.98f);

        // Header Title (Discovered Common Name or Undiscovered Class)
        var titleGO = new GameObject("ModalTitle", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);
        titleRect.sizeDelta = new Vector2(-40f, 110f);

        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTMP.font = font;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        titleTMP.fontSize = 38;
        titleTMP.fontStyle = FontStyles.Bold;

        if (isDiscovered)
        {
            titleTMP.text = $"<b>{data.commonName.ToUpper()}</b>\n<size=36><color=#88ccff><i>{data.scientificName}</i></color>  •  <color=#a0d8ef>Depth: {data.depthRangeText}</color></size>";
        }
        else
        {
            titleTMP.text = $"<b><color=#8899aa>??? UNCATALOGED</color></b>\n<size=36><color=#667788>Class: {data.taxonomicClass}</color>  •  <color=#88aacc>Depth: {data.depthRangeText}</color></size>";
        }

        // Center 3D Viewport Box
        var viewGO = new GameObject("ViewportBox", typeof(RectTransform), typeof(Image));
        viewGO.transform.SetParent(cardGO.transform, false);
        var viewRect = viewGO.GetComponent<RectTransform>();
        viewRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewRect.pivot     = new Vector2(0.5f, 0.5f);
        viewRect.sizeDelta = new Vector2(460f, 360f);
        viewRect.anchoredPosition = new Vector2(0f, 6f);

        var viewImg = viewGO.GetComponent<Image>();
        viewImg.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);

        // RawImage for live 3D rotating model
        var rawGO = new GameObject("PreviewRaw", typeof(RectTransform), typeof(RawImage));
        rawGO.transform.SetParent(viewGO.transform, false);
        var rawRect = rawGO.GetComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.sizeDelta = Vector2.zero;

        var rawImg = rawGO.GetComponent<RawImage>();
        if (ModelPreviewSystem.Instance != null)
        {
            ModelPreviewSystem.Instance.ShowPreview(data, isSilhouette: !isDiscovered, rawImg);
            rawGO.AddComponent<ThumbnailDragRotator>();
        }

        // Subtitle / Instruction Hint
        var hintGO = new GameObject("HintText", typeof(RectTransform));
        hintGO.transform.SetParent(cardGO.transform, false);
        var hintRect = hintGO.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot     = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 85f);
        hintRect.sizeDelta = new Vector2(-40f, 45f);

        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        if (font != null) hintTMP.font = font;
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.fontSize = 36;
        hintTMP.color = new Color(0.5f, 0.8f, 1f, 0.85f);
        hintTMP.text = "✦ Touch and drag to spin 360°";

        // Close Button
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(cardGO.transform, false);
        var closeRect = closeGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot     = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(260f, 65f);
        closeRect.anchoredPosition = new Vector2(0f, 14f);

        closeGO.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.55f, 1f);
        closeGO.GetComponent<Button>().onClick.AddListener(CloseModelInspectionModal);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(closeGO.transform, false);
        var lblRect = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lblTmp.font = font;
        lblTmp.fontSize = 36;
        lblTmp.fontStyle = FontStyles.Bold;
        lblTmp.alignment = TextAlignmentOptions.Center;
        lblTmp.color = Color.white;
        lblTmp.text = "CLOSE";
    }

    public void CloseModelInspectionModal()
    {
        if (ModelPreviewSystem.Instance != null)
            ModelPreviewSystem.Instance.ClearPreview();

        var modal = GameObject.Find("_3DModelInspectionModal");
        if (modal != null) Destroy(modal);
    }

    // -----------------------------------------------------------------------
    // Bestiary Scroll-To-Species
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens the Bestiary and auto-scrolls to the card matching the given speciesId.
    /// Used by BestiaryDiscoveryPopup "View in Bestiary" and ScanReticleUI "View in Bestiary".
    /// </summary>
    public void ShowBestiaryAndScrollTo(string speciesId)
    {
        _activeFilter = -1;
        ShowBestiary();
        StartCoroutine(ScrollToSpeciesNextFrame(speciesId));
    }

    private IEnumerator ScrollToSpeciesNextFrame(string speciesId)
    {
        // Wait one frame for layout to rebuild after RefreshBestiary
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (scrollRect == null)
            scrollRect = entryContainer?.GetComponentInParent<ScrollRect>();

        if (scrollRect == null || string.IsNullOrEmpty(speciesId)) yield break;

        if (!_cardMap.TryGetValue(speciesId, out RectTransform cardRect) || cardRect == null)
            yield break;

        // Calculate normalized scroll position
        RectTransform content = scrollRect.content ?? entryContainer as RectTransform;
        RectTransform viewport = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();

        if (content == null || viewport == null) yield break;

        float contentHeight  = content.rect.height;
        float viewportHeight = viewport.rect.height;

        if (contentHeight <= viewportHeight) yield break; // No scrolling needed

        // Get the card's Y position within the content
        float cardLocalY = -cardRect.anchoredPosition.y;

        // Walk up the hierarchy to accumulate Y offsets if the card is nested
        Transform current = cardRect.parent;
        while (current != null && current != content.transform)
        {
            var rt = current as RectTransform;
            if (rt != null) cardLocalY -= rt.anchoredPosition.y;
            current = current.parent;
        }

        // Center the card in the viewport
        float scrollableRange = contentHeight - viewportHeight;
        float targetScroll = cardLocalY - (viewportHeight * 0.35f);
        float normalizedY = 1f - Mathf.Clamp01(targetScroll / scrollableRange);

        scrollRect.verticalNormalizedPosition = normalizedY;
    }

    /// <summary>
    /// Opens the Detail Modal displaying the Real Biological Photo and full scientific data.
    /// Only accessible for discovered species.
    /// </summary>
    public void OpenDetailModal(SpeciesData data)
    {
        if (data == null) return;

        bool isDiscovered = GameManager.Instance != null && (GameManager.Instance.IsDiscovered(data.speciesId) || GameManager.Instance.IsDiscovered(data.zoneIndex, data.speciesId));
        if (!isDiscovered) return; // Locked for undiscovered entries

        if (detailModal != null)
        {
            detailModal.SetActive(true);
            PopulateCustomDetailModal(data);
        }
        else
        {
            ShowDefaultDetailModal(data);
        }
    }

    public void CloseDetailModal()
    {
        if (detailModal != null) detailModal.SetActive(false);
        var defaultModal = GameObject.Find("_DefaultBestiaryDetailModal");
        if (defaultModal != null) Destroy(defaultModal);
    }

    // -----------------------------------------------------------------------
    // Entry Rendering & Continuous Descent Layout
    // -----------------------------------------------------------------------

    public void RefreshBestiary()
    {
        if (entryContainer == null) return;
        ClearEntries();
        _cardMap.Clear();

        if (registry == null)
            registry = Resources.Load<SpeciesRegistry>("SpeciesRegistry");

        if (registry == null)
        {
            Debug.LogWarning("[BestiaryManager] SpeciesRegistry not found.");
            return;
        }

        int totalDiscovered = 0;
        int totalSpecies = registry.TotalCount;
        for (int z = 0; z < ZoneConfig.ZoneCount; z++)
        {
            var zoneSpecies = registry.GetSpeciesForZone(z);
            foreach (var s in zoneSpecies)
            {
                if (GameManager.Instance != null && (GameManager.Instance.IsDiscovered(s.speciesId) || GameManager.Instance.IsDiscovered(z, s.speciesId)))
                    totalDiscovered++;
            }
        }

        if (overallProgressText != null && totalSpecies > 0)
        {
            float pct = (float)totalDiscovered / totalSpecies * 100f;
            overallProgressText.text = $"Research Bestiary: {totalDiscovered} / {totalSpecies} ({pct:0}%)";
        }

        int startZone = _activeFilter >= 0 ? _activeFilter : 0;
        int endZone   = _activeFilter >= 0 ? _activeFilter : ZoneConfig.ZoneCount - 1;

        for (int z = startZone; z <= endZone; z++)
        {
            ZoneDefinition zoneDef = ZoneConfig.Zones[z];
            var speciesList = registry.GetSpeciesForZone(z);

            int zoneDiscovered = 0;
            foreach (var s in speciesList)
            {
                if (GameManager.Instance != null && (GameManager.Instance.IsDiscovered(s.speciesId) || GameManager.Instance.IsDiscovered(z, s.speciesId)))
                    zoneDiscovered++;
            }

            CreateZoneHeader(zoneDef, z, zoneDiscovered, speciesList.Count);

            foreach (var data in speciesList)
            {
                bool discovered = GameManager.Instance != null && (GameManager.Instance.IsDiscovered(data.speciesId) || GameManager.Instance.IsDiscovered(z, data.speciesId));
                InstantiateSpeciesCard(data, discovered);
            }
        }
    }

    private void ClearEntries()
    {
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);
    }

    private void InstantiateSpeciesCard(SpeciesData data, bool discovered)
    {
        GameObject prefabToUse = null;
        if (discovered && discoveredCardPrefab != null) prefabToUse = discoveredCardPrefab;
        else if (!discovered && undiscoveredCardPrefab != null) prefabToUse = undiscoveredCardPrefab;
        else if (entryCardPrefab != null) prefabToUse = entryCardPrefab;

        if (prefabToUse != null)
        {
            var card = Instantiate(prefabToUse, entryContainer);
            var cardUI = card.GetComponent<BestiaryCardUI>();
            var capturedData = data;

            if (cardUI != null)
            {
                cardUI.Setup(data, discovered, () => OpenDetailModal(capturedData));
            }
            else
            {
                var btn = card.GetComponent<Button>() ?? card.AddComponent<Button>();
                if (discovered)
                {
                    btn.interactable = true;
                    btn.onClick.AddListener(() => OpenDetailModal(capturedData));
                }
                else
                {
                    btn.interactable = false;
                }
                PopulateGenericPrefabLabels(card, data, discovered);
            }

            // Register card for scroll-to-species
            if (data != null && !string.IsNullOrEmpty(data.speciesId))
                _cardMap[data.speciesId] = card.GetComponent<RectTransform>();
        }
        else
        {
            CreateFallbackCard(data, discovered);
        }
    }

    private void PopulateGenericPrefabLabels(GameObject card, SpeciesData data, bool discovered)
    {
        var labels = card.GetComponentsInChildren<TMP_Text>();
        var images = card.GetComponentsInChildren<Image>();
        var rawImages = card.GetComponentsInChildren<RawImage>();

        if (discovered)
        {
            SetTextSafe(labels, 0, data.commonName);
            SetTextSafe(labels, 1, $"<i>{data.scientificName}</i>");
            SetTextSafe(labels, 2, $"Habitat: {data.habitat}");
            SetTextSafe(labels, 3, $"Depth: {data.depthRangeText}");
        }
        else
        {
            SetTextSafe(labels, 0, "??? [Uncataloged]");
            SetTextSafe(labels, 1, $"Class: {data.taxonomicClass}");
            SetTextSafe(labels, 2, $"Habitat Hint: {data.habitat}");
            SetTextSafe(labels, 3, !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : "Scan to unlock.");
        }

        Sprite thumb = ModelPreviewSystem.Instance != null
            ? ModelPreviewSystem.Instance.GetOrRenderThumbnail(data, isSilhouette: !discovered)
            : (discovered ? data.photo : data.silhouette);

        // Find child thumbnail Image (skipping the root card background Image!)
        Image thumbImg = null;
        foreach (var img in images)
        {
            if (img.gameObject == card) continue; // Skip root background
            string n = img.gameObject.name.ToLower();
            if (n.Contains("thumb") || n.Contains("photo") || n.Contains("icon") || n.Contains("image") || n.Contains("preview"))
            {
                thumbImg = img;
                break;
            }
        }
        if (thumbImg == null && images.Length > 1) thumbImg = images[1];

        if (thumbImg != null)
        {
            if (thumb != null)
            {
                thumbImg.sprite = thumb;
                thumbImg.color  = Color.white;
            }
            else if (data.photo != null)
            {
                thumbImg.sprite = discovered ? data.photo : (data.silhouette != null ? data.silhouette : data.photo);
                thumbImg.color  = discovered ? Color.white : new Color(0.12f, 0.15f, 0.20f, 1f);
            }
        }

        // Also populate any child RawImage thumbnail
        if (rawImages.Length > 0 && thumb != null)
        {
            rawImages[0].texture = thumb.texture;
            rawImages[0].color   = Color.white;
        }
    }

    private void CreateFallbackCard(SpeciesData data, bool discovered)
    {
        var cardGO = new GameObject($"Card_{data.speciesId}", typeof(RectTransform), typeof(Image), typeof(Button));
        cardGO.transform.SetParent(entryContainer, false);

        var rect = cardGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 125f);
        StretchHorizontal(rect);

        var bg = cardGO.GetComponent<Image>();
        bg.color = discovered
            ? new Color(0.06f, 0.16f, 0.26f, 0.95f)
            : new Color(0.03f, 0.05f, 0.08f, 0.95f);

        var btn = cardGO.GetComponent<Button>();
        var captured = data;
        if (discovered)
        {
            btn.interactable = true;
            btn.onClick.AddListener(() => OpenDetailModal(captured));
        }
        else
        {
            btn.interactable = false;
        }

        // Register card for scroll-to-species
        if (!string.IsNullOrEmpty(data.speciesId))
            _cardMap[data.speciesId] = rect;

        // Left Accent Strip
        var accentGO = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGO.transform.SetParent(cardGO.transform, false);
        var accentRect = accentGO.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(6f, 0f);
        accentRect.anchoredPosition = new Vector2(3f, 0f);
        accentGO.GetComponent<Image>().color = discovered
            ? new Color(0.0f, 0.9f, 1.0f, 1f)
            : new Color(0.25f, 0.30f, 0.35f, 0.8f);

        // 3D Model Thumbnail Box on Left
        var thumbBoxGO = new GameObject("ThumbnailBox", typeof(RectTransform), typeof(Image));
        thumbBoxGO.transform.SetParent(cardGO.transform, false);
        var thumbBoxRect = thumbBoxGO.GetComponent<RectTransform>();
        thumbBoxRect.anchorMin = new Vector2(0f, 0.5f);
        thumbBoxRect.anchorMax = new Vector2(0f, 0.5f);
        thumbBoxRect.pivot     = new Vector2(0f, 0.5f);
        thumbBoxRect.sizeDelta = new Vector2(105f, 105f);
        thumbBoxRect.anchoredPosition = new Vector2(16f, 0f);
        thumbBoxGO.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.09f, 0.8f);

        // Thumbnail Image for 3D model render
        var thumbImgGO = new GameObject("ThumbnailImage", typeof(RectTransform), typeof(Image));
        thumbImgGO.transform.SetParent(thumbBoxGO.transform, false);
        var thumbImgRect = thumbImgGO.GetComponent<RectTransform>();
        thumbImgRect.anchorMin = Vector2.zero;
        thumbImgRect.anchorMax = Vector2.one;
        thumbImgRect.offsetMin = Vector2.zero;
        thumbImgRect.offsetMax = Vector2.zero;

        var thumbImg = thumbImgGO.GetComponent<Image>();
        thumbImg.preserveAspect = true;

        Sprite thumb = ModelPreviewSystem.Instance != null
            ? ModelPreviewSystem.Instance.GetOrRenderThumbnail(data, isSilhouette: !discovered)
            : (discovered ? data.photo : data.silhouette);

        if (thumb != null)
        {
            thumbImg.sprite = thumb;
            thumbImg.color  = Color.white;
        }
        else if (data.photo != null)
        {
            thumbImg.sprite = discovered ? data.photo : (data.silhouette != null ? data.silhouette : data.photo);
            thumbImg.color  = discovered ? Color.white : new Color(0.1f, 0.1f, 0.15f, 0.9f);
        }

        // Make 3D thumbnail clickable on all cards to open interactive 3D inspection modal
        var thumbBtn = thumbBoxGO.AddComponent<Button>();
        thumbBtn.onClick.AddListener(() => OpenModelInspectionModal(captured, discovered));

        // Text details (offset to the right of thumbnail box)
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(cardGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(132f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.color    = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        if (discovered)
        {
            tmp.text =
                $"<b><size=17><color=#ffffff>{data.commonName}</color></size></b>   " +
                $"<color=#88ccff><i><size=13>{data.scientificName}</size></i></color>  " +
                $"<color=#ffcc00><size=12>[+{data.rdpReward} RDP]</size></color>\n" +
                $"<color=#77ccee>Depth:</color> {data.depthRangeText}   <color=#77ccee>■ Class:</color> {data.taxonomicClass}\n" +
                $"<color=#bbbbbb>{data.habitat}</color>\n" +
                $"<color=#ffdd88>✦ {data.interestingFact}</color>";
        }
        else
        {
            string clue = !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : data.habitat;
            tmp.text =
                $"<b><size=16><color=#8899aa>??? [Uncataloged Specimen]</color></size></b>   " +
                $"<color=#667788><size=13>Class: {data.taxonomicClass}</size></color>\n" +
                $"<color=#778899>Expected Depth:</color> {data.depthRangeText}\n" +
                $"<color=#8899aa>Clue:</color> <color=#aaccee>{clue}</color>";
        }
    }

    private void CreateZoneHeader(ZoneDefinition zone, int zoneIndex, int discovered, int total)
    {
        if (customZoneHeaderPrefab != null)
        {
            var headerGO = Instantiate(customZoneHeaderPrefab, entryContainer);
            headerGO.name = $"Header_Zone_{zoneIndex}";
            var zoneUI = headerGO.GetComponent<ZoneHeaderUI>() ?? headerGO.AddComponent<ZoneHeaderUI>();
            zoneUI.Setup(zone, discovered, total);
            return;
        }

        // Fallback programmatic zone header with large, highly-readable typography
        var defaultHeaderGO = new GameObject($"Header_Zone_{zoneIndex}", typeof(RectTransform), typeof(Image));
        defaultHeaderGO.transform.SetParent(entryContainer, false);

        var rect = defaultHeaderGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 72f);
        StretchHorizontal(rect);

        var img = defaultHeaderGO.GetComponent<Image>();
        img.color = new Color(zone.fogColor.r * 0.35f, zone.fogColor.g * 0.35f, zone.fogColor.b * 0.35f, 0.95f);

        // Left accent strip
        var accentGO = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGO.transform.SetParent(defaultHeaderGO.transform, false);
        var accRect = accentGO.GetComponent<RectTransform>();
        accRect.anchorMin = new Vector2(0f, 0f);
        accRect.anchorMax = new Vector2(0f, 1f);
        accRect.sizeDelta = new Vector2(8f, 0f);
        accRect.anchoredPosition = new Vector2(4f, 0f);
        accentGO.GetComponent<Image>().color = zone.fogColor;

        // Top Row: Zone Title + Depth
        var titleGO = new GameObject("ZoneTitle", typeof(RectTransform));
        titleGO.transform.SetParent(defaultHeaderGO.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(24f, 0f);
        titleRect.offsetMax = new Vector2(-20f, -6f);

        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.fontSize = 20;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.color = Color.white;
        titleTMP.text = $"<b>{zone.zoneName.ToUpper()}</b>  <size=15><color=#a0d8ef>({zone.displayDepthMin:0} - {zone.displayDepthMax:0} m)</color></size>";

        // Bottom Row: Progress + Large Unlock Badge
        var subGO = new GameObject("ProgressSubtitle", typeof(RectTransform));
        subGO.transform.SetParent(defaultHeaderGO.transform, false);
        var subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.offsetMin = new Vector2(24f, 6f);
        subRect.offsetMax = new Vector2(-20f, 0f);

        var subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.fontSize = 15;
        subTMP.alignment = TextAlignmentOptions.MidlineLeft;
        subTMP.color = Color.white;

        float pct = total > 0 ? (float)discovered / total * 100f : 0f;
        int needed = Mathf.Max(0, 50 - Mathf.RoundToInt(pct));
        string badge = pct >= 50f
            ? "<color=#66ffbb><b>[ ✓ 50% UNLOCKED ]</b></color>"
            : $"<color=#ffcc44><b>[ {needed}% MORE NEEDED TO UNLOCK NEXT ZONE ]</b></color>";

        subTMP.text = $"<color=#e0e0e0><b>{discovered} / {total}</b> Discovered ({pct:0}%)</color>   {badge}";
    }

    private void PopulateCustomDetailModal(SpeciesData data)
    {
        if (detailPhoto != null)
        {
            Sprite display = data.photo != null ? data.photo : data.fullImage;
            if (display != null)
            {
                detailPhoto.sprite = display;
                detailPhoto.color  = Color.white;
            }
        }

        if (detailCommonName != null)       detailCommonName.text       = data.commonName;
        if (detailScientificName != null)   detailScientificName.text   = $"<i>{data.scientificName}</i>";
        if (detailClass != null)            detailClass.text            = $"Class: {data.taxonomicClass}";
        if (detailHabitat != null)          detailHabitat.text          = $"Habitat: {data.habitat}";
        if (detailCharacteristics != null)  detailCharacteristics.text  = $"Characteristics: {data.characteristics}";
        if (detailEcologicalRole != null)   detailEcologicalRole.text   = $"Ecological Role: {data.ecologicalRole}";
        if (detailFact != null)             detailFact.text             = $"Did you know? {data.interestingFact}";
        if (detailReward != null)           detailReward.text           = $"+{data.rdpReward} RDP";
    }

    private void ShowDefaultDetailModal(SpeciesData data)
    {
        CloseDetailModal();

        var modalGO = new GameObject("_DefaultBestiaryDetailModal", typeof(RectTransform), typeof(Image));
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) modalGO.transform.SetParent(canvas.transform, false);

        var rect = modalGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.10f);
        rect.anchorMax = new Vector2(0.85f, 0.90f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = modalGO.GetComponent<Image>();
        img.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);

        // Photo Image
        var photoGO = new GameObject("BiologicalPhoto", typeof(RectTransform), typeof(Image));
        photoGO.transform.SetParent(modalGO.transform, false);
        var photoRect = photoGO.GetComponent<RectTransform>();
        photoRect.anchorMin = new Vector2(0.5f, 1f);
        photoRect.anchorMax = new Vector2(0.5f, 1f);
        photoRect.sizeDelta = new Vector2(300f, 160f);
        photoRect.anchoredPosition = new Vector2(0f, -100f);

        var photoImg = photoGO.GetComponent<Image>();
        Sprite display = data.photo != null ? data.photo : data.fullImage;
        if (display != null) photoImg.sprite = display;
        else photoImg.color = new Color(0.1f, 0.2f, 0.3f, 1f);

        // Text
        var textGO = new GameObject("DetailText", typeof(RectTransform));
        textGO.transform.SetParent(modalGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 60f);
        textRect.offsetMax = new Vector2(-24f, -190f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 10f;
        tmp.paragraphSpacing = 10f;

        tmp.text =
            $"<b><size=22><color=#ffffff>{data.commonName}</color></size></b>\n" +
            $"<size=15><color=#88ccff><i>{data.scientificName}</i></color>  •  <color=#aaccee>Class: {data.taxonomicClass}</color></size>\n\n" +
            $"<color=#77ddbb><b>HABITAT:</b></color> {data.habitat}\n" +
            $"<color=#77ddbb><b>CHARACTERISTICS:</b></color> {data.characteristics}\n" +
            $"<color=#77ddbb><b>ECOLOGICAL ROLE:</b></color> {data.ecologicalRole}\n\n" +
            $"<color=#ffcc00><b>✦ INTERESTING FACT:</b></color>\n{data.interestingFact}\n\n" +
            $"<size=12><color=#888888>+{data.rdpReward} RDP</color></size>";

        // Close button
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(modalGO.transform, false);
        var closeRect = closeGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(160f, 40f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);

        closeGO.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.65f, 1f);
        closeGO.GetComponent<Button>().onClick.AddListener(CloseDetailModal);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(closeGO.transform, false);
        var lblRect = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
        lblTmp.fontSize = 15;
        lblTmp.fontStyle = FontStyles.Bold;
        lblTmp.alignment = TextAlignmentOptions.Center;
        lblTmp.text = "CLOSE";
    }

    private static void SetTextSafe(TMP_Text[] arr, int idx, string text)
    {
        if (arr != null && idx < arr.Length && arr[idx] != null)
            arr[idx].text = text;
    }

    private static void StretchHorizontal(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
    }
}