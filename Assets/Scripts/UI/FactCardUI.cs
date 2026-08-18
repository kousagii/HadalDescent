using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fact Card popup shown when the player interacts with a stationary species
/// (coral, sponge, bivalve). Slides up from the bottom of the screen.
/// Displays all 6 educational fields + "Added to Bestiary!" on first discovery.
///
/// Setup: Add FactCardUI to the Canvas. Singleton — auto-found at runtime.
/// No prefab required — all UI is built from code.
/// Called by ScannerSystem.StaticScanSequence() after the sonar pulse VFX.
/// </summary>
public class FactCardUI : MonoBehaviour
{
    public static FactCardUI Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Runtime UI elements (built in code)
    // -----------------------------------------------------------------------

    private RectTransform _panel;
    private Image         _photo;
    private TMP_Text      _nameText;
    private TMP_Text      _sciNameText;
    private TMP_Text      _bodyText;
    private TMP_Text      _rdpText;
    private Button        _closeButton;
    private Button        _viewButton;

    private bool   _isOpen;
    private Canvas _canvas;

    // Panel slide animation
    private const float PanelHeight   = 520f;
    private const float SlideSpeed    = 10f;
    private const float PanelWidth    = 380f;

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
        _canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        BuildPanel();
    }

    private void Update()
    {
        if (_panel == null) return;

        float targetY = _isOpen ? 0f : -PanelHeight - 20f;
        Vector2 pos   = _panel.anchoredPosition;
        pos.y         = Mathf.Lerp(pos.y, targetY, Time.deltaTime * SlideSpeed);
        _panel.anchoredPosition = pos;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Show(SpeciesData data, bool isNewDiscovery)
    {
        if (_panel == null) BuildPanel();
        PopulateCard(data, isNewDiscovery);
        _isOpen = true;
    }

    public void Hide()
    {
        _isOpen = false;
    }

    // -----------------------------------------------------------------------
    // Content population
    // -----------------------------------------------------------------------

    private void PopulateCard(SpeciesData data, bool isNew)
    {
        // Photo
        if (_photo != null)
        {
            Sprite display = data.photo != null ? data.photo
                           : (data.fullImage != null ? data.fullImage : data.silhouette);
            _photo.sprite  = display;
            _photo.color   = (display != null) ? Color.white : data.placeholderColor;
        }

        if (_nameText    != null) _nameText.text    = data.commonName;
        if (_sciNameText != null) _sciNameText.text = $"<i>{data.scientificName}</i>";

        if (_bodyText != null)
        {
            _bodyText.text =
                $"<color=#88ccaa>\u25A0 Habitat:</color> {data.habitat}\n\n" +
                $"<color=#88ccaa>\u25A0 Characteristics:</color>\n{data.characteristics}\n\n" +
                $"<color=#88ccaa>\u25A0 Ecological Role:</color>\n{data.ecologicalRole}\n\n" +
                $"<color=#ffdd88>\u2736 {data.interestingFact}</color>";
        }

        if (_rdpText != null)
        {
            _rdpText.gameObject.SetActive(isNew);
            if (isNew) _rdpText.text = $"\u2605 Added to Bestiary!  +{data.rdpReward} RDP";
        }
    }

    // -----------------------------------------------------------------------
    // UI construction (all code — no prefab required)
    // -----------------------------------------------------------------------

    private void BuildPanel()
    {
        if (_canvas == null) return;

        // -- Root panel ---------------------------------------------------
        var panelGO = new GameObject("FactCard", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel               = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin     = new Vector2(0.5f, 0f);
        _panel.anchorMax     = new Vector2(0.5f, 0f);
        _panel.pivot         = new Vector2(0.5f, 0f);
        _panel.sizeDelta     = new Vector2(PanelWidth, PanelHeight);
        _panel.anchoredPosition = new Vector2(0f, -PanelHeight - 20f);

        var panelImg       = panelGO.GetComponent<Image>();
        panelImg.color     = new Color(0.04f, 0.10f, 0.18f, 0.97f);

        // Top accent bar
        MakeAccentBar(_panel, new Color(0.08f, 0.75f, 0.70f, 1f));

        float yPos = PanelHeight - 18f;

        // Photo placeholder
        var photoGO = new GameObject("Photo", typeof(RectTransform), typeof(Image));
        photoGO.transform.SetParent(_panel, false);
        var photoRect = photoGO.GetComponent<RectTransform>();
        photoRect.anchorMin     = new Vector2(0f, 1f);
        photoRect.anchorMax     = new Vector2(1f, 1f);
        photoRect.pivot         = new Vector2(0.5f, 1f);
        photoRect.anchoredPosition = new Vector2(0f, -6f);
        photoRect.sizeDelta     = new Vector2(0f, 120f);
        _photo = photoGO.GetComponent<Image>();
        _photo.color            = new Color(0.08f, 0.18f, 0.28f, 1f);
        yPos -= 126f;

        // Common name
        _nameText    = MakeText(_panel, "CommonName", 18, FontStyles.Bold,
                               new Vector2(14f, -yPos), new Vector2(PanelWidth - 28f, 26f),
                               new Color(0.85f, 0.95f, 1.0f, 1f));
        yPos += 30f;

        // Scientific name
        _sciNameText = MakeText(_panel, "SciName", 13, FontStyles.Italic,
                               new Vector2(14f, -yPos), new Vector2(PanelWidth - 28f, 22f),
                               new Color(0.55f, 0.75f, 0.95f, 1f));
        yPos += 26f;

        // Divider
        MakeDivider(_panel, -yPos);
        yPos += 8f;

        // Body text (all 5 info fields)
        _bodyText = MakeText(_panel, "Body", 11, FontStyles.Normal,
                            new Vector2(14f, -yPos), new Vector2(PanelWidth - 28f, 230f),
                            Color.white);
        _bodyText.enableWordWrapping = true;
        _bodyText.overflowMode       = TextOverflowModes.ScrollRect;
        yPos += 234f;

        // RDP badge (shown only on first discover)
        _rdpText = MakeText(_panel, "RDP", 13, FontStyles.Bold,
                           new Vector2(14f, -yPos), new Vector2(PanelWidth - 28f, 24f),
                           new Color(1f, 0.87f, 0.30f, 1f));
        _rdpText.gameObject.SetActive(false);
        yPos += 28f;

        // Buttons row
        float btnY = -yPos;
        _closeButton = MakeButton(_panel, "Close",  new Vector2(-48f, btnY), 88f, 36f,
                                  new Color(0.15f, 0.20f, 0.28f, 1f), "Close");
        _viewButton  = MakeButton(_panel, "View",   new Vector2( 48f, btnY), 88f, 36f,
                                  new Color(0.08f, 0.55f, 0.52f, 1f), "View");

        _closeButton.onClick.AddListener(Hide);
        _viewButton.onClick.AddListener(() =>
        {
            Hide();
            BestiaryManager.Instance?.ShowBestiary();
        });
    }

    // ---- Helpers ------------------------------------------------------------

    private static TMP_Text MakeText(RectTransform parent, string name, float size,
                                     FontStyles style, Vector2 anchoredPos, Vector2 sizeDelta,
                                     Color color)
    {
        var go   = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(0f, 1f);
        rect.pivot            = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;
        var tmp         = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize    = size;
        tmp.fontStyle   = style;
        tmp.color       = color;
        tmp.alignment   = TextAlignmentOptions.TopLeft;
        return tmp;
    }

    private static void MakeAccentBar(RectTransform parent, Color color)
    {
        var go   = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = new Vector2(0f, 4f);
        go.GetComponent<Image>().color = color;
    }

    private static void MakeDivider(RectTransform parent, float y)
    {
        var go   = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta        = new Vector2(-28f, 1f);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
    }

    private static Button MakeButton(RectTransform parent, string name,
                                      Vector2 anchoredPos, float w, float h,
                                      Color bgColor, string label)
    {
        var go   = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(w, h);
        go.GetComponent<Image>().color = bgColor;

        var lblGO   = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblRect = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero;
        var tmp            = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text           = label;
        tmp.fontSize       = 13;
        tmp.fontStyle      = FontStyles.Bold;
        tmp.color          = Color.white;
        tmp.alignment      = TextAlignmentOptions.Center;

        return go.GetComponent<Button>();
    }
}
