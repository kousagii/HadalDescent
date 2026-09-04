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
    public bool IsOpen => _isOpen || (customPopupPanel != null && customPopupPanel.activeSelf);
    private Canvas _canvas;
    private string _lastSpeciesId;

    private const float PanelW     = 460f;
    private const float PanelH     = 280f;
    private const float SlideSpeed = 12f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (customCloseButton != null)
        {
            customCloseButton.onClick.RemoveListener(Hide);
            customCloseButton.onClick.AddListener(Hide);
        }
        if (customViewButton != null)
        {
            customViewButton.onClick.RemoveListener(OnViewInBestiaryClicked);
            customViewButton.onClick.AddListener(OnViewInBestiaryClicked);
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
        if (customViewButton != null)
        {
            customViewButton.onClick.RemoveListener(OnViewInBestiaryClicked);
            customViewButton.onClick.AddListener(OnViewInBestiaryClicked);
        }

        if (customPopupPanel != null)
        {
            if (!_isOpen)
                customPopupPanel.SetActive(false);
        }
        else
        {
            if (!_isOpen)
                BuildPanel();
        }
    }

    private void OnViewInBestiaryClicked()
    {
        string speciesToView = _lastSpeciesId;
        Hide();
        BestiaryManager.OpenBestiary(speciesToView);
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

    private Coroutine _autoCloseCoroutine;

    /// <summary>
    /// Safely finds or instantiates BestiaryDiscoveryPopup and displays it.
    /// </summary>
    public static BestiaryDiscoveryPopup ShowPopup(SpeciesData data, bool isNew)
    {
        var popup = Instance ?? FindFirstObjectByType<BestiaryDiscoveryPopup>(FindObjectsInactive.Include);
        if (popup == null)
        {
            var pCanvas = FindFirstObjectByType<Canvas>();
            var prefab = Resources.Load<GameObject>("UI/DiscoveryPopup")
                      ?? Resources.Load<GameObject>("Prefabs/UI/DiscoveryPopup");
            if (prefab != null)
            {
                var go = Instantiate(prefab, pCanvas != null ? pCanvas.transform : null);
                go.name = "DiscoveryPopup";
                popup = go.GetComponentInChildren<BestiaryDiscoveryPopup>(true);
            }
        }

        if (popup != null)
        {
            popup.gameObject.SetActive(true);
            popup.Show(data, isNew);
        }
        else
        {
            Debug.LogError("[BestiaryDiscoveryPopup] Failed to locate or load BestiaryDiscoveryPopup!");
        }
        return popup;
    }

    public void Show(SpeciesData data, bool isNew)
    {
        if (data == null) return;
        gameObject.SetActive(true);
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
        transform.SetAsLastSibling();
        _isOpen = true;
        _lastSpeciesId = data.speciesId;

        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);

        if (customPopupPanel != null)
        {
            customPopupPanel.SetActive(true);
            customPopupPanel.transform.SetAsLastSibling();
            customPopupPanel.transform.localPosition = new Vector3(customPopupPanel.transform.localPosition.x, customPopupPanel.transform.localPosition.y, 0f);

            var canvas = customPopupPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = customPopupPanel.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9998;

            var raycaster = customPopupPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                customPopupPanel.AddComponent<GraphicRaycaster>();
            }

            if (customNameText != null)  customNameText.text  = data.commonName;
            if (customTitleText != null) customTitleText.text = isNew ? "Added to Bestiary!" : "Species Observed";
            if (isNew) StartCoroutine(AnimateRDP(data.rdpReward, customRdpText));
            else if (customRdpText != null) customRdpText.text = "";
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

            if (_nameText  != null) _nameText.text  = data.commonName;
            if (_titleText != null) _titleText.text = isNew ? "Added to Bestiary!" : "Species Observed";

            if (isNew) StartCoroutine(AnimateRDP(data.rdpReward, _rdpText));
            else if (_rdpText != null) _rdpText.text = "";
        }

        // Automatically dismiss discovery popup after 5 seconds
        _autoCloseCoroutine = StartCoroutine(AutoCloseCountdown(5f));
    }

    private IEnumerator AutoCloseCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);
        Hide();
    }

    public void Hide()
    {
        if (_autoCloseCoroutine != null)
        {
            StopCoroutine(_autoCloseCoroutine);
            _autoCloseCoroutine = null;
        }
        _isOpen = false;
        if (customPopupPanel != null)
        {
            customPopupPanel.SetActive(false);
        }
        if (_panel != null)
        {
            _panel.gameObject.SetActive(false);
        }
        if (customPopupPanel == gameObject)
        {
            gameObject.SetActive(false);
        }
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

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

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
        tr.pivot = new Vector2(0.5f, 1f); tr.sizeDelta = new Vector2(0f, 44f);
        tr.anchoredPosition = new Vector2(0f, -12f);
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _titleText.font = font;
        _titleText.fontSize = 36; _titleText.fontStyle = FontStyles.Bold;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color = new Color(0.12f, 0.90f, 0.50f, 1f);
        _titleText.text = "Added to Bestiary!";

        // Species Name
        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(go.transform, false);
        var nr = nameGO.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f); nr.sizeDelta = new Vector2(0f, 48f);
        nr.anchoredPosition = new Vector2(0f, -60f);
        _nameText = nameGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _nameText.font = font;
        _nameText.fontSize = 36; _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = Color.white;

        // RDP text
        var rdpGO = new GameObject("RDP", typeof(RectTransform));
        rdpGO.transform.SetParent(go.transform, false);
        var rr = rdpGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(1f, 0f);
        rr.pivot = new Vector2(0.5f, 0f); rr.sizeDelta = new Vector2(0f, 46f);
        rr.anchoredPosition = new Vector2(0f, 80f);
        _rdpText = rdpGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _rdpText.font = font;
        _rdpText.fontSize = 36; _rdpText.fontStyle = FontStyles.Bold;
        _rdpText.alignment = TextAlignmentOptions.Center;
        _rdpText.color = new Color(1f, 0.85f, 0.15f, 1f);

        // Continue Button
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(go.transform, false);
        var cr = closeGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f); cr.anchorMax = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(220f, 54f); cr.anchoredPosition = new Vector2(0f, 16f);
        closeGO.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.65f, 1f);
        _closeBtn = closeGO.GetComponent<Button>();
        _closeBtn.onClick.AddListener(Hide);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(closeGO.transform, false);
        var lr = lblGO.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lbl.font = font;
        lbl.fontSize = 36; lbl.alignment = TextAlignmentOptions.Center;
        lbl.fontStyle = FontStyles.Bold;
        lbl.text = "CONTINUE"; lbl.color = Color.white;
    }
}