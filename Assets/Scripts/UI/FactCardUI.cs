using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Environmental Fact Card popup shown when the player interacts with a natural
/// habitat feature, landmark, coral reef formation, hydrothermal vent, or rock outcrop.
///
/// Features:
///   - Displays habitat name, geological/ecological category, ocean zone, depth range,
///     habitat description, ecological significance, and fascinating biological facts.
///   - Supports custom UI panel designed in Canvas (just drag references into Inspector).
///   - Automatically builds default UI if none is assigned in Inspector.
///   - Awards Research Data Points (RDP) on first-time survey.
/// </summary>
public class FactCardUI : MonoBehaviour
{
    public static FactCardUI Instance { get; private set; }

    [Header("Custom UI Panel (Optional - assign if designed in Canvas)")]
    [Tooltip("Custom Fact Card GameObject in Canvas.")]
    [SerializeField] private GameObject customCardPanel;

    [Header("Custom UI - Content Bindings (Drag your TextMeshPro & Image elements here)")]
    [Tooltip("Photo or Illustration Image component for the habitat / prop.")]
    [SerializeField] private Image      customPhoto;

    [Tooltip("Title text component (e.g. 'Tropical Coral Reef Matrix').")]
    [SerializeField] private TMP_Text   customHabitatName;

    [Tooltip("Category / Subtitle text component (e.g. 'Biogenic Marine Habitat').")]
    [SerializeField] private TMP_Text   customCategory;

    [Tooltip("Zone text component (e.g. 'Zone: Sunlight Zone (0–200 m)').")]
    [SerializeField] private TMP_Text   customZone;

    [Tooltip("Depth Range text component (e.g. 'Depth: 10–30 m').")]
    [SerializeField] private TMP_Text   customDepth;

    [Tooltip("Habitat Description text component.")]
    [SerializeField] private TMP_Text   customDescription;

    [Tooltip("Ecological Significance text component.")]
    [SerializeField] private TMP_Text   customEcologicalSignificance;

    [Tooltip("Interesting Fact / 'Did you know?' text component.")]
    [SerializeField] private TMP_Text   customFact;

    [Tooltip("RDP Survey Reward text component (e.g. '+30 RDP (Habitat Surveyed!)').")]
    [SerializeField] private TMP_Text   customReward;

    [Tooltip("Close Button component.")]
    [SerializeField] private Button     customCloseButton;

    // Runtime programmatic elements (used if customCardPanel is null)
    private RectTransform _panel;
    private Image         _photo;
    private TMP_Text      _commonNameText;
    private TMP_Text      _sciNameText;
    private TMP_Text      _classText;
    private TMP_Text      _bodyText;
    private TMP_Text      _rewardText;
    private Button        _closeButton;

    private bool   _isOpen;
    public bool IsOpen => _isOpen || (customCardPanel != null && customCardPanel.activeSelf);
    private Canvas _canvas;

    private const float PanelWidth  = 560f;
    private const float PanelHeight = 840f;
    private const float SlideSpeed  = 12f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (customCloseButton != null)
        {
            customCloseButton.onClick.RemoveListener(Hide);
            customCloseButton.onClick.AddListener(Hide);
        }
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();

        if (customCloseButton != null)
        {
            customCloseButton.onClick.RemoveListener(Hide);
            customCloseButton.onClick.AddListener(Hide);
        }

        if (customCardPanel != null)
        {
            if (!_isOpen)
                customCardPanel.SetActive(false);
        }
        else
        {
            if (!_isOpen)
                BuildPanel();
        }
    }

    private void Update()
    {
        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (_isOpen && escapePressed)
        {
            Hide();
            return;
        }

        if (customCardPanel == null && _panel != null)
        {
            float targetY = _isOpen ? 0f : -PanelHeight - 40f;
            Vector2 pos   = _panel.anchoredPosition;
            pos.y         = Mathf.Lerp(pos.y, targetY, Time.deltaTime * SlideSpeed);
            _panel.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// Safely finds or instantiates FactCardUI and displays the environmental habitat fact card.
    /// </summary>
    public static FactCardUI ShowEnvironmentFact(EnvironmentFactTarget target)
    {
        if (target == null) return null;

        var fc = Instance ?? FindFirstObjectByType<FactCardUI>(FindObjectsInactive.Include);
        if (fc == null)
        {
            var pCanvas = FindFirstObjectByType<Canvas>();
            var prefab = Resources.Load<GameObject>("UI/FactCardUI")
                      ?? Resources.Load<GameObject>("Prefabs/UI/FactCardUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab, pCanvas != null ? pCanvas.transform : null);
                go.name = "FactCardUI";
                fc = go.GetComponentInChildren<FactCardUI>(true);
            }
        }

        if (fc != null)
        {
            fc.gameObject.SetActive(true);
            fc.Show(target);
        }
        else
        {
            Debug.LogError("[FactCardUI] Failed to locate or load FactCardUI!");
        }
        return fc;
    }

    public void Show(EnvironmentFactTarget target)
    {
        if (target == null) return;

        gameObject.SetActive(true);
        _isOpen = true;

        bool isNew = !target.IsSurveyed;
        if (isNew)
        {
            target.IsSurveyed = true;
            GameManager.Instance?.AddRDP(target.RdpReward);
            AudioManager.Instance?.PlaySpeciesFound();
        }

        string zoneName = ZoneConfig.Zones != null && target.ZoneIndex >= 0 && target.ZoneIndex < ZoneConfig.Zones.Length
            ? ZoneConfig.Zones[target.ZoneIndex].zoneName
            : "Ocean Ecosystem";

        if (customCardPanel != null)
        {
            customCardPanel.SetActive(true);
            customCardPanel.transform.SetAsLastSibling();
            customCardPanel.transform.localPosition = new Vector3(customCardPanel.transform.localPosition.x, customCardPanel.transform.localPosition.y, 0f);

            var canvas = customCardPanel.GetComponent<Canvas>();
            if (canvas == null) canvas = customCardPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;

            var raycaster = customCardPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null) customCardPanel.AddComponent<GraphicRaycaster>();

            if (customPhoto != null)
            {
                UIThemeManager.ApplyAspectFillCrop(customPhoto, target.HabitatPhoto);
                customPhoto.gameObject.SetActive(target.HabitatPhoto != null);
                customPhoto.color = Color.white;
            }

            if (customHabitatName != null)            customHabitatName.text            = target.HabitatName;
            if (customCategory != null)               customCategory.text               = target.Category;
            if (customZone != null)                   customZone.text                   = $"Zone: {zoneName}";
            if (customDepth != null)                  customDepth.text                  = $"Depth: {target.DepthRangeText}";
            if (customDescription != null)            customDescription.text            = target.HabitatDescription;
            if (customEcologicalSignificance != null) customEcologicalSignificance.text = $"Significance: {target.EcologicalSignificance}";
            if (customFact != null)                   customFact.text                   = $"Did you know? {target.InterestingFact}";
            if (customReward != null)                 customReward.text                 = isNew ? $"+{target.RdpReward} RDP (Habitat Surveyed!)" : "Habitat Already Surveyed";
        }
        else
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (_panel == null) BuildPanel();
            if (_panel != null)
            {
                _panel.gameObject.SetActive(true);
                _panel.SetAsLastSibling();
            }

            PopulateProgrammaticCard(target, zoneName, isNew);
        }

        UIThemeManager.ApplyAntoneTheme(customCardPanel != null ? customCardPanel : (_panel != null ? _panel.gameObject : gameObject));
    }

    public void Hide()
    {
        _isOpen = false;
        if (customCardPanel != null)
        {
            customCardPanel.SetActive(false);
        }
        if (_panel != null)
        {
            _panel.gameObject.SetActive(false);
        }
        if (customCardPanel == gameObject)
        {
            gameObject.SetActive(false);
        }
    }

    private void PopulateProgrammaticCard(EnvironmentFactTarget target, string zoneName, bool isNew)
    {
        if (_photo != null)
        {
            UIThemeManager.ApplyAspectFillCrop(_photo, target.HabitatPhoto);
            _photo.gameObject.SetActive(target.HabitatPhoto != null);
            _photo.color = Color.white;
        }

        if (_commonNameText != null) _commonNameText.text = target.HabitatName;
        if (_sciNameText != null)    _sciNameText.text    = $"{target.Category}  •  {zoneName} ({target.DepthRangeText})";

        if (_bodyText != null)
        {
            _bodyText.text =
                $"<color=#77ddbb><b>HABITAT OVERVIEW:</b></color>\n{target.HabitatDescription}\n\n" +
                $"<color=#77ddbb><b>ECOLOGICAL SIGNIFICANCE:</b></color>\n{target.EcologicalSignificance}\n\n" +
                $"<color=#ffcc00><b>✦ DID YOU KNOW?</b></color>\n{target.InterestingFact}";
        }

        if (_rewardText != null)
        {
            _rewardText.text = isNew ? $"<color=#ffcc00>+{target.RdpReward} RDP</color>  Habitat Surveyed!" : "<color=#88ccff>Habitat Previously Surveyed</color>";
        }
    }

    private void BuildPanel()
    {
        if (_panel != null || _canvas == null) return;

        var font = UIThemeManager.AntoneFont;

        var go = new GameObject("FactCard", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _panel = go.GetComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0.5f);
        _panel.anchorMax        = new Vector2(0.5f, 0.5f);
        _panel.pivot            = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta        = new Vector2(PanelWidth, PanelHeight);
        _panel.anchoredPosition = new Vector2(0f, 0f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);

        // Photo Image
        var photoGO = new GameObject("Photo", typeof(RectTransform), typeof(Image));
        photoGO.transform.SetParent(go.transform, false);
        var pr = photoGO.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 1f); pr.anchorMax = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(480f, 200f); pr.anchoredPosition = new Vector2(0f, -120f);
        _photo = photoGO.GetComponent<Image>();

        // Habitat Name
        var nameGO = new GameObject("HabitatName", typeof(RectTransform));
        nameGO.transform.SetParent(go.transform, false);
        var nr = nameGO.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.sizeDelta = new Vector2(-40f, 48f); nr.anchoredPosition = new Vector2(0f, -245f);
        _commonNameText = nameGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _commonNameText.font = font;
        _commonNameText.fontSize = 36; _commonNameText.fontStyle = FontStyles.Bold;
        _commonNameText.alignment = TextAlignmentOptions.Center; _commonNameText.color = Color.white;

        // Category & Zone
        var sciGO = new GameObject("CategoryAndZone", typeof(RectTransform));
        sciGO.transform.SetParent(go.transform, false);
        var sr = sciGO.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f);
        sr.sizeDelta = new Vector2(-40f, 40f); sr.anchoredPosition = new Vector2(0f, -295f);
        _sciNameText = sciGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _sciNameText.font = font;
        _sciNameText.fontSize = 32; _sciNameText.alignment = TextAlignmentOptions.Center;
        _sciNameText.color = new Color(0.6f, 0.8f, 1f, 1f);

        // Body Text (Overview, Significance, Fact)
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(go.transform, false);
        var br = bodyGO.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 1f);
        br.offsetMin = new Vector2(28f, 110f); br.offsetMax = new Vector2(-28f, -340f);
        _bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _bodyText.font = font;
        _bodyText.fontSize = 32; _bodyText.color = Color.white;
        _bodyText.lineSpacing = 10f;
        _bodyText.paragraphSpacing = 10f;
        _bodyText.textWrappingMode = TextWrappingModes.Normal;

        // Reward Text
        var rdpGO = new GameObject("Reward", typeof(RectTransform));
        rdpGO.transform.SetParent(go.transform, false);
        var rr = rdpGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(1f, 0f);
        rr.sizeDelta = new Vector2(0f, 44f); rr.anchoredPosition = new Vector2(0f, 72f);
        _rewardText = rdpGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _rewardText.font = font;
        _rewardText.fontSize = 32; _rewardText.fontStyle = FontStyles.Bold;
        _rewardText.alignment = TextAlignmentOptions.Center;

        // Close button
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(go.transform, false);
        var cr = closeGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f); cr.anchorMax = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(220f, 54f); cr.anchoredPosition = new Vector2(0f, 16f);
        closeGO.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.65f, 1f);
        _closeButton = closeGO.GetComponent<Button>();
        _closeButton.onClick.AddListener(Hide);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(closeGO.transform, false);
        var lr = lblGO.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lbl.font = font;
        lbl.fontSize = 32; lbl.alignment = TextAlignmentOptions.Center;
        lbl.fontStyle = FontStyles.Bold;
        lbl.text = "CLOSE"; lbl.color = Color.white;
    }
}