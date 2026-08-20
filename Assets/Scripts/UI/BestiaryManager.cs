using System;
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

    [Header("Custom Card Prefabs (Assign your UI Prefabs here)")]
    [Tooltip("Prefab for discovered species (3D model thumbnail, cyan accent).")]
    [SerializeField] private GameObject discoveredCardPrefab;

    [Tooltip("Prefab for undiscovered species (3D silhouette thumbnail, search clue).")]
    [SerializeField] private GameObject undiscoveredCardPrefab;

    [Tooltip("Fallback single prefab if you only want one unified card prefab.")]
    [SerializeField] private GameObject entryCardPrefab;

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

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private int  _activeFilter = -1; // -1 = All Depths (Continuous Descent)
    private bool _isOpen       = false;

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
        if (bestiaryPanel != null) bestiaryPanel.SetActive(false);
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

    public void ShowBestiary()
    {
        _isOpen = true;
        if (bestiaryPanel != null) bestiaryPanel.SetActive(true);
        RefreshBestiary();
    }

    public void HideBestiary()
    {
        _isOpen = false;
        if (bestiaryPanel != null) bestiaryPanel.SetActive(false);
        CloseDetailModal();
    }

    public void ToggleBestiary()
    {
        if (_isOpen) HideBestiary();
        else         ShowBestiary();
    }

    public void SelectTab(int filterIndex)
    {
        _activeFilter = filterIndex;
        RefreshBestiary();
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

        if (discovered)
        {
            SetTextSafe(labels, 0, data.commonName);
            SetTextSafe(labels, 1, $"<i>{data.scientificName}</i>");
            SetTextSafe(labels, 2, $"Habitat: {data.habitat}");
            SetTextSafe(labels, 3, $"Depth: {data.depthRangeText}");

            if (images.Length > 0 && data.photo != null)
                images[0].sprite = data.photo;
        }
        else
        {
            SetTextSafe(labels, 0, "??? [Uncataloged]");
            SetTextSafe(labels, 1, $"Class: {data.taxonomicClass}");
            SetTextSafe(labels, 2, $"Habitat Hint: {data.habitat}");
            SetTextSafe(labels, 3, !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : "Scan to unlock.");

            if (images.Length > 0 && data.silhouette != null)
            {
                images[0].sprite = data.silhouette;
                images[0].color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            }
        }
    }

    private void CreateFallbackCard(SpeciesData data, bool discovered)
    {
        var cardGO = new GameObject($"Card_{data.speciesId}", typeof(RectTransform), typeof(Image), typeof(Button));
        cardGO.transform.SetParent(entryContainer, false);

        var rect = cardGO.GetComponent<RectTransform>();
        rect.sizeDelta = discovered ? new Vector2(0f, 120f) : new Vector2(0f, 95f);
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

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(cardGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.color    = Color.white;
        tmp.enableWordWrapping = true;

        if (discovered)
        {
            tmp.text =
                $"<b><size=16><color=#ffffff>{data.commonName}</color></size></b>   " +
                $"<color=#88ccff><i><size=12>{data.scientificName}</size></i></color>  " +
                $"<color=#ffcc00><size=11>[+{data.rdpReward} RDP]</size></color>\n" +
                $"<color=#77ccee>📍 Depth:</color> {data.depthRangeText}   <color=#77ccee>■ Class:</color> {data.taxonomicClass}\n" +
                $"<color=#aaaaaa>{data.habitat}</color>\n" +
                $"<color=#ffdd88>✦ {data.interestingFact}</color>";
        }
        else
        {
            string clue = !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : data.habitat;
            tmp.text =
                $"<b><size=15><color=#778899>??? [Uncataloged Specimen]</color></size></b>   " +
                $"<color=#556677><size=12>Class: {data.taxonomicClass}</size></color>\n" +
                $"<color=#667788>📍 Expected Depth:</color> {data.depthRangeText}\n" +
                $"<color=#8899aa>🔍 Clue:</color> <color=#aaccee>{clue}</color>";
        }

        var accentGO = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGO.transform.SetParent(cardGO.transform, false);
        var accentRect = accentGO.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(5f, 0f);
        accentRect.anchoredPosition = new Vector2(2.5f, 0f);
        accentGO.GetComponent<Image>().color = discovered
            ? new Color(0.20f, 0.85f, 0.95f, 1f)
            : new Color(0.25f, 0.30f, 0.35f, 0.8f);
    }

    private void CreateZoneHeader(ZoneDefinition zone, int zoneIndex, int discovered, int total)
    {
        var headerGO = new GameObject($"Header_Zone_{zoneIndex}", typeof(RectTransform), typeof(Image));
        headerGO.transform.SetParent(entryContainer, false);

        var rect = headerGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 48f);
        StretchHorizontal(rect);

        var img = headerGO.GetComponent<Image>();
        img.color = new Color(zone.fogColor.r * 0.4f, zone.fogColor.g * 0.4f, zone.fogColor.b * 0.4f, 0.95f);

        var textGO = new GameObject("Title", typeof(RectTransform));
        textGO.transform.SetParent(headerGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;

        float pct = total > 0 ? (float)discovered / total * 100f : 0f;
        string unlockBadge = pct >= 50f ? "<color=#88ffaa>✓ 50% Met</color>" : $"<color=#ffcc66>{50 - (int)pct}% needed</color>";

        tmp.text = $"<b>{zone.zoneName.ToUpper()}</b>  <size=12><color=#a0d8ef>({zone.displayDepthMin:0}–{zone.displayDepthMax:0} m)</color></size>  " +
                   $"<size=12>• {discovered}/{total} ({pct:0}%)  {unlockBadge}</size>";
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
        if (detailReward != null)           detailReward.text           = $"Research Reward: +{data.rdpReward} RDP";
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
        tmp.enableWordWrapping = true;

        tmp.text =
            $"<b><size=22><color=#ffffff>{data.commonName}</color></size></b>\n" +
            $"<size=15><color=#88ccff><i>{data.scientificName}</i></color>  •  <color=#aaccee>Class: {data.taxonomicClass}</color></size>\n\n" +
            $"<color=#77ddbb><b>HABITAT:</b></color> {data.habitat}\n" +
            $"<color=#77ddbb><b>CHARACTERISTICS:</b></color> {data.characteristics}\n" +
            $"<color=#77ddbb><b>ECOLOGICAL ROLE:</b></color> {data.ecologicalRole}\n\n" +
            $"<color=#ffcc00><b>✦ INTERESTING FACT:</b></color>\n{data.interestingFact}\n\n" +
            $"<size=12><color=#888888>Research Reward: +{data.rdpReward} RDP</color></size>";

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