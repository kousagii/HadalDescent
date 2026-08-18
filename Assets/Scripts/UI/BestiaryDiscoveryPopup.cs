using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Celebratory popup shown after a successful mobile species scan (minigame win).
/// Slides up from the bottom, shows species name + "Added to Bestiary!" + animated RDP counter.
///
/// Called by ScannerSystem.OnScanSuccess(). No prefab required.
/// </summary>
public class BestiaryDiscoveryPopup : MonoBehaviour
{
    public static BestiaryDiscoveryPopup Instance { get; private set; }

    private RectTransform _panel;
    private Image         _photo;
    private TMP_Text      _titleText;
    private TMP_Text      _nameText;
    private TMP_Text      _rdpText;
    private Button        _closeBtn;
    private Button        _viewBtn;

    private bool   _isOpen;
    private Canvas _canvas;

    private const float PanelW      = 320f;
    private const float PanelH      = 240f;
    private const float SlideSpeed  = 12f;

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
        float targetY       = _isOpen ? 20f : -PanelH - 20f;
        Vector2 pos         = _panel.anchoredPosition;
        pos.y               = Mathf.Lerp(pos.y, targetY, Time.deltaTime * SlideSpeed);
        _panel.anchoredPosition = pos;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Show(SpeciesData data, bool isNew)
    {
        if (_panel == null) BuildPanel();

        // Photo
        Sprite display = data.photo != null ? data.photo : data.fullImage;
        if (_photo != null) { _photo.sprite = display; _photo.color = display != null ? Color.white : data.placeholderColor; }
        if (_nameText  != null) _nameText.text  = data.commonName;
        if (_titleText != null) _titleText.text  = isNew ? "Added to Bestiary!" : "Species Observed";

        _isOpen = true;
        if (isNew && data != null) StartCoroutine(AnimateRDP(data.rdpReward));
    }

    public void Hide()
    {
        _isOpen = false;
    }

    // -----------------------------------------------------------------------
    // RDP counter animation
    // -----------------------------------------------------------------------

    private IEnumerator AnimateRDP(int target)
    {
        if (_rdpText == null) yield break;
        float elapsed = 0f;
        float duration = 1.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int current = Mathf.RoundToInt(Mathf.Lerp(0, target, elapsed / duration));
            _rdpText.text = $"+{current} RDP";
            yield return null;
        }
        _rdpText.text = $"+{target} RDP";
    }

    // -----------------------------------------------------------------------
    // UI construction
    // -----------------------------------------------------------------------

    private void BuildPanel()
    {
        if (_canvas == null) return;

        var panelGO     = new GameObject("DiscoveryPopup", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel           = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0f);
        _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot     = new Vector2(0.5f, 0f);
        _panel.sizeDelta = new Vector2(PanelW, PanelH);
        _panel.anchoredPosition = new Vector2(0f, -PanelH - 20f);

        panelGO.GetComponent<Image>().color = new Color(0.04f, 0.10f, 0.18f, 0.97f);

        // Top accent
        MakeRect(_panel, "Accent",
                 new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f),
                 Vector2.zero, new Vector2(0f, 4f), new Color(0.20f, 0.85f, 0.75f));

        // Photo strip (left side)
        var photoGO     = new GameObject("Photo", typeof(RectTransform), typeof(Image));
        photoGO.transform.SetParent(_panel, false);
        var photoRect   = photoGO.GetComponent<RectTransform>();
        photoRect.anchorMin = new Vector2(0f, 0.2f);
        photoRect.anchorMax = new Vector2(0.38f, 1f);
        photoRect.sizeDelta = Vector2.zero;
        photoRect.offsetMin = new Vector2(8f, 8f);
        photoRect.offsetMax = new Vector2(-4f, -8f);
        _photo = photoGO.GetComponent<Image>();
        _photo.color = new Color(0.08f, 0.18f, 0.28f, 1f);

        // Title "Added to Bestiary!"
        _titleText = MakeTMP(_panel, "Title", 14, FontStyles.Bold,
                             new Vector2(0.4f, 0.75f), new Vector2(1f, 1f),
                             new Color(0.20f, 0.90f, 0.78f));

        // Species name
        _nameText = MakeTMP(_panel, "SpeciesName", 16, FontStyles.Bold,
                            new Vector2(0.4f, 0.45f), new Vector2(1f, 0.75f),
                            new Color(0.90f, 0.95f, 1.0f));

        // RDP counter
        _rdpText = MakeTMP(_panel, "RDP", 18, FontStyles.Bold,
                           new Vector2(0.4f, 0.22f), new Vector2(1f, 0.48f),
                           new Color(1f, 0.88f, 0.30f));

        // Close / View buttons
        _closeBtn = MakeBtn(_panel, "Close", new Vector2(0.05f,0f), new Vector2(0.48f,0.22f),
                            new Color(0.12f,0.18f,0.26f), "Close");
        _viewBtn  = MakeBtn(_panel, "View",  new Vector2(0.52f,0f), new Vector2(0.95f,0.22f),
                            new Color(0.07f,0.50f,0.46f), "View Bestiary");

        _closeBtn.onClick.AddListener(Hide);
        _viewBtn.onClick.AddListener(() => { Hide(); BestiaryManager.Instance?.ShowBestiary(); });
    }

    // ---- Helpers -------------------------------------------------------------

    private static void MakeRect(RectTransform parent, string name,
                                  Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
                                  Vector2 anc, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = ancMin; r.anchorMax = ancMax; r.pivot = pivot;
        r.anchoredPosition = anc; r.sizeDelta = size;
        go.GetComponent<Image>().color = color;
    }

    private static TMP_Text MakeTMP(RectTransform parent, string name, float size,
                                     FontStyles style, Vector2 ancMin, Vector2 ancMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = ancMin; r.anchorMax = ancMax;
        r.offsetMin = new Vector2(4f, 2f); r.offsetMax = new Vector2(-4f, -2f);
        var tmp     = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    private static Button MakeBtn(RectTransform parent, string name,
                                   Vector2 ancMin, Vector2 ancMax, Color bg, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = ancMin; r.anchorMax = ancMax;
        r.offsetMin = new Vector2(4f, 4f); r.offsetMax = new Vector2(-4f, -4f);
        go.GetComponent<Image>().color = bg;
        var lbl = new GameObject("Lbl", typeof(RectTransform));
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.sizeDelta = Vector2.zero;
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 12; t.fontStyle = FontStyles.Bold;
        t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
        return go.GetComponent<Button>();
    }
}
