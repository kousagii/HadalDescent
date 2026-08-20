using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Celebratory popup shown after a successful mobile species scan (minigame win).
///
/// Features:
///   - Clean, compact notification without photo (Species Name + "Added to Bestiary!" + animated +RDP).
///   - Automatically builds default UI if none is assigned in Inspector.
///   - Supports custom UI panel designed in Canvas.
/// </summary>
public class BestiaryDiscoveryPopup : MonoBehaviour
{
    public static BestiaryDiscoveryPopup Instance { get; private set; }

    [Header("Custom UI Panel (Optional - assign if designed in Canvas)")]
    [Tooltip("Custom popup GameObject in Canvas.")]
    [SerializeField] private GameObject customPopupPanel;
    [SerializeField] private TMP_Text   customTitleText;
    [SerializeField] private TMP_Text   customNameText;
    [SerializeField] private TMP_Text   customRdpText;
    [SerializeField] private Button     customCloseButton;
    [SerializeField] private Button     customViewButton;

    // Runtime programmatic elements (used if customPopupPanel is null)
    private RectTransform _panel;
    private TMP_Text      _titleText;
    private TMP_Text      _nameText;
    private TMP_Text      _rdpText;
    private Button        _closeBtn;

    private bool   _isOpen;
    private Canvas _canvas;

    private const float PanelW     = 300f;
    private const float PanelH     = 160f;
    private const float SlideSpeed = 12f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();

        if (customPopupPanel != null)
        {
            customPopupPanel.SetActive(false);
            if (customCloseButton != null)
                customCloseButton.onClick.AddListener(Hide);
            if (customViewButton != null)
                customViewButton.onClick.AddListener(() =>
                {
                    Hide();
                    BestiaryManager.Instance?.ShowBestiary();
                });
        }
        else
        {
            BuildPanel();
        }
    }

    private void Update()
    {
        if (customPopupPanel == null && _panel != null)
        {
            float targetY = _isOpen ? 24f : -PanelH - 30f;
            Vector2 pos   = _panel.anchoredPosition;
            pos.y         = Mathf.Lerp(pos.y, targetY, Time.deltaTime * SlideSpeed);
            _panel.anchoredPosition = pos;
        }
    }

    public void Show(SpeciesData data, bool isNew)
    {
        if (data == null) return;

        if (customPopupPanel != null)
        {
            customPopupPanel.SetActive(true);
            if (customNameText != null)  customNameText.text  = data.commonName;
            if (customTitleText != null) customTitleText.text = isNew ? "Added to Bestiary!" : "Species Observed";
            if (isNew) StartCoroutine(AnimateRDP(data.rdpReward, customRdpText));
            else if (customRdpText != null) customRdpText.text = "";
        }
        else
        {
            if (_panel == null) BuildPanel();

            if (_nameText  != null) _nameText.text  = data.commonName;
            if (_titleText != null) _titleText.text = isNew ? "Added to Bestiary!" : "Species Observed";

            _isOpen = true;
            if (isNew) StartCoroutine(AnimateRDP(data.rdpReward, _rdpText));
            else if (_rdpText != null) _rdpText.text = "";
        }
    }

    public void Hide()
    {
        _isOpen = false;
        if (customPopupPanel != null) customPopupPanel.SetActive(false);
    }

    private IEnumerator AnimateRDP(int target, TMP_Text label)
    {
        if (label == null) yield break;
        float elapsed = 0f;
        float duration = 1.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int current = Mathf.RoundToInt(Mathf.Lerp(0, target, elapsed / duration));
            label.text = $"+{current} RDP";
            yield return null;
        }
        label.text = $"+{target} RDP";
    }

    private void BuildPanel()
    {
        if (_panel != null || _canvas == null) return;

        var go = new GameObject("DiscoveryPopup", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _panel = go.GetComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0f);
        _panel.anchorMax        = new Vector2(0.5f, 0f);
        _panel.pivot            = new Vector2(0.5f, 0f);
        _panel.sizeDelta        = new Vector2(PanelW, PanelH);
        _panel.anchoredPosition = new Vector2(0f, -PanelH - 30f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.14f, 0.96f);

        // Header Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(go.transform, false);
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f); tr.sizeDelta = new Vector2(0f, 28f);
        tr.anchoredPosition = new Vector2(0f, -10f);
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize = 13; _titleText.fontStyle = FontStyles.Bold;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color = new Color(0.12f, 0.90f, 0.50f, 1f);
        _titleText.text = "Added to Bestiary!";

        // Species Name
        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(go.transform, false);
        var nr = nameGO.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f); nr.sizeDelta = new Vector2(0f, 32f);
        nr.anchoredPosition = new Vector2(0f, -38f);
        _nameText = nameGO.AddComponent<TextMeshProUGUI>();
        _nameText.fontSize = 19; _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = Color.white;

        // RDP text
        var rdpGO = new GameObject("RDP", typeof(RectTransform));
        rdpGO.transform.SetParent(go.transform, false);
        var rr = rdpGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(1f, 0f);
        rr.pivot = new Vector2(0.5f, 0f); rr.sizeDelta = new Vector2(0f, 26f);
        rr.anchoredPosition = new Vector2(0f, 52f);
        _rdpText = rdpGO.AddComponent<TextMeshProUGUI>();
        _rdpText.fontSize = 15; _rdpText.fontStyle = FontStyles.Bold;
        _rdpText.alignment = TextAlignmentOptions.Center;
        _rdpText.color = new Color(1f, 0.85f, 0.15f, 1f);

        // Close Button
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(go.transform, false);
        var cr = closeGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f); cr.anchorMax = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(110f, 28f); cr.anchoredPosition = new Vector2(0f, 14f);
        closeGO.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.65f, 1f);
        _closeBtn = closeGO.GetComponent<Button>();
        _closeBtn.onClick.AddListener(Hide);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(closeGO.transform, false);
        var lr = lblGO.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.fontSize = 11; lbl.alignment = TextAlignmentOptions.Center;
        lbl.text = "CONTINUE"; lbl.color = Color.white;
    }
}