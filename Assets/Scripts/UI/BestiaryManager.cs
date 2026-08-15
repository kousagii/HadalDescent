using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Bestiary UI panel.
/// 
/// Shows 5 zone tabs. For each zone, lists all species from SpeciesRegistry.
/// Each entry card shows:
///   Discovered   ? full info (common name, scientific name, habitat, description, fact)
///   Undiscovered ? silhouette tint + habitat hint only
///
/// Setup:
///   Attach to a UIManager child GameObject in each zone scene.
///   Assign: registry, bestiaryPanel, entryContainer (ScrollView Content), zoneTabs[5].
///   Leave entryCardPrefab null to use the auto-generated fallback cards.
///   Wire UIManager.OnBestiaryButtonPressed() ? BestiaryManager.Instance.ToggleBestiary().
/// </summary>
public class BestiaryManager : MonoBehaviour
{
    public static BestiaryManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Data")]
    [SerializeField] private SpeciesRegistry registry;

    [Header("UI")]
    [SerializeField] private GameObject bestiaryPanel;
    [SerializeField] private Transform  entryContainer;       // Scroll View ? Viewport ? Content
    [SerializeField] private GameObject entryCardPrefab;      // optional custom card prefab
    [SerializeField] private Button[]   zoneTabs = new Button[5];

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private int  _activeTab = 0;
    private bool _isOpen    = false;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (bestiaryPanel != null) bestiaryPanel.SetActive(false);

        for (int i = 0; i < zoneTabs.Length; i++)
        {
            int tab = i;
            if (zoneTabs[i] != null)
                zoneTabs[i].onClick.AddListener(() => SelectTab(tab));
        }

        // Auto-find registry in Resources if not assigned
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
        RefreshEntries(_activeTab);
    }

    public void HideBestiary()
    {
        _isOpen = false;
        if (bestiaryPanel != null) bestiaryPanel.SetActive(false);
    }

    public void ToggleBestiary()
    {
        if (_isOpen) HideBestiary();
        else         ShowBestiary();
    }

    // -----------------------------------------------------------------------
    // Tab selection
    // -----------------------------------------------------------------------

    private void SelectTab(int zoneIndex)
    {
        _activeTab = zoneIndex;
        RefreshEntries(zoneIndex);
    }

    // -----------------------------------------------------------------------
    // Entry rendering
    // -----------------------------------------------------------------------

    private void RefreshEntries(int zoneIndex)
    {
        if (entryContainer == null) return;
        ClearEntries();

        if (registry == null) { Debug.LogWarning("[BestiaryManager] No SpeciesRegistry assigned."); return; }

        var species = registry.GetSpeciesForZone(zoneIndex);
        foreach (var data in species)
        {
            bool found = GameManager.Instance != null
                ? GameManager.Instance.IsDiscovered(zoneIndex, data.speciesId)
                : false;

            if (entryCardPrefab != null) PopulatePrefabCard(data, found);
            else                         CreateFallbackCard(data, found);
        }
    }

    private void ClearEntries()
    {
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);
    }

    // -----------------------------------------------------------------------
    // Prefab card (if you build a proper prefab in UI Builder)
    // -----------------------------------------------------------------------

    private void PopulatePrefabCard(SpeciesData data, bool discovered)
    {
        var card   = Instantiate(entryCardPrefab, entryContainer);
        var labels = card.GetComponentsInChildren<TMP_Text>();
        var images = card.GetComponentsInChildren<Image>();

        if (discovered)
        {
            SetTextSafe(labels, 0, data.commonName);
            SetTextSafe(labels, 1, $"<i>{data.scientificName}</i>");
            SetTextSafe(labels, 2, $"Habitat: {data.habitat}");
            SetTextSafe(labels, 3, $"Characteristics: {data.characteristics}");
            SetTextSafe(labels, 4, $"Ecological Role: {data.ecologicalRole}");
            SetTextSafe(labels, 5, data.description);
            SetTextSafe(labels, 6, $"Did you know? {data.interestingFact}");

            if (images.Length > 0)
            {
                Sprite display = data.photo != null ? data.photo : data.fullImage;
                if (display != null) images[0].sprite = display;
            }

            // Trigger 3D model preview
            ModelPreviewSystem.Instance?.ShowPreview(data);
        }
        else
        {
            SetTextSafe(labels, 0, "???");
            SetTextSafe(labels, 1, "");
            SetTextSafe(labels, 2, $"Habitat Hint: {data.habitat}");
            SetTextSafe(labels, 3, "");
            SetTextSafe(labels, 4, "");
            SetTextSafe(labels, 5, "Scan this species to learn more.");
            SetTextSafe(labels, 6, "");
            if (images.Length > 0)
            {
                if (data.silhouette != null) images[0].sprite = data.silhouette;
                images[0].color = new Color(0.08f, 0.08f, 0.08f, 1f);
            }
        }
    }

    private static void SetTextSafe(TMP_Text[] arr, int idx, string text)
    {
        if (idx < arr.Length) arr[idx].text = text;
    }

    // -----------------------------------------------------------------------
    // Fallback card (auto-generated, no prefab required for prototype)
    // -----------------------------------------------------------------------

    private void CreateFallbackCard(SpeciesData data, bool discovered)
    {
        // Card root
        var cardGO = new GameObject($"Card_{data.speciesId}", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(entryContainer, false);

        var rect = cardGO.GetComponent<RectTransform>();
        rect.sizeDelta = discovered ? new Vector2(0f, 220f) : new Vector2(0f, 110f);
        StretchHorizontal(rect);

        var bg = cardGO.GetComponent<Image>();
        bg.color = discovered
            ? new Color(0.08f, 0.18f, 0.28f, 0.92f)
            : new Color(0.05f, 0.05f, 0.07f, 0.92f);

        // Text child
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(cardGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 8f);
        textRect.offsetMax = new Vector2(-14f, -8f);

        var tmp      = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.color    = Color.white;
        tmp.enableWordWrapping = true;

        if (discovered)
        {
            tmp.text =
                $"<b><size=16>{data.commonName}</size></b>  " +
                $"<color=#aaddff><i><size=12>{data.scientificName}</size></i></color>\n" +
                $"<color=#88ccaa>\u25A0 Habitat:</color> {data.habitat}\n" +
                $"<color=#88ccaa>\u25A0 Characteristics:</color> {data.characteristics}\n" +
                $"<color=#88ccaa>\u25A0 Ecological Role:</color> {data.ecologicalRole}\n" +
                $"<color=#dddddd>{data.description}</color>\n" +
                $"<color=#ffdd88>\u2736 {data.interestingFact}</color>";

            // Wire a button click to show the 3D model preview
            var btn = cardGO.AddComponent<UnityEngine.UI.Button>();
            var capturedData = data;
            btn.onClick.AddListener(() => ModelPreviewSystem.Instance?.ShowPreview(capturedData));
        }
        else
        {
            tmp.text =
                $"<b><size=15><color=#555555>???</color></size></b>\n" +
                $"<color=#666666>Undiscovered</color>\n" +
                $"<color=#888888>Habitat Hint: {data.habitat}</color>\n" +
                $"<color=#555555>Scan this entry to unlock it.</color>";
        }

        // Left accent bar
        var accentGO = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGO.transform.SetParent(cardGO.transform, false);
        var accentRect = accentGO.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentRect.anchoredPosition = new Vector2(2f, 0f);
        accentGO.GetComponent<Image>().color = discovered
            ? new Color(0.20f, 0.75f, 0.90f, 1f)
            : new Color(0.25f, 0.25f, 0.30f, 1f);
    }

    private static void StretchHorizontal(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
    }
}
