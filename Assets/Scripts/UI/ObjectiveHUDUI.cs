using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dedicated Objective HUD Card positioned below the Sonar Range Box on the top-left of the screen.
/// Decoupled from the Radio Textbox so mission objectives do not crowd radio communications.
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

    public const string PostTutorialDefaultObjective = "Upgrade submarine hull and discover species to go deeper";

    private RectTransform _panelRect;
    private CanvasGroup   _canvasGroup;
    private TMP_Text      _headerText;
    private TMP_Text      _objectiveBodyText;
    private string        _currentObjective = "";

    public bool IsVisible => _panelRect != null && _panelRect.gameObject.activeSelf && _canvasGroup != null && _canvasGroup.alpha > 0.01f;

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
        if (string.IsNullOrWhiteSpace(_currentObjective))
        {
            SetObjective(PostTutorialDefaultObjective);
        }
        SetVisible(true);
    }

    public void EnsureUI()
    {
        if (_panelRect != null) return;

        var canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Container: Anchored top-left cleanly below the Sonar Range box (y = -335)
        var rootGO = new GameObject("ObjectiveCard_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        _panelRect = rootGO.GetComponent<RectTransform>();
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();

        _panelRect.anchorMin = new Vector2(0f, 1f);
        _panelRect.anchorMax = new Vector2(0f, 1f);
        _panelRect.pivot     = new Vector2(0f, 1f);
        _panelRect.sizeDelta = new Vector2(420f, 200f);
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

        // Header Label: "MISSION OBJECTIVE" (20px, Cyan, Normal Weight)
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
        _headerText.fontSize = 16f;
        _headerText.color = new Color(0.00f, 0.90f, 1.00f, 0.95f);
        _headerText.alignment = TextAlignmentOptions.Center;
        _headerText.raycastTarget = false;
        _headerText.text = "MISSION OBJECTIVE";

        // Objective Body Text (22px, Mint Green #2DFFAA, Normal Weight, Auto-wrapping)
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
        _objectiveBodyText.fontSize = 18f;
        _objectiveBodyText.enableAutoSizing = true;
        _objectiveBodyText.fontSizeMin = 14f;
        _objectiveBodyText.fontSizeMax = 18f;
        _objectiveBodyText.color = new Color(0.18f, 1.00f, 0.66f, 1.00f);
        _objectiveBodyText.alignment = TextAlignmentOptions.Center;
        _objectiveBodyText.textWrappingMode = TextWrappingModes.Normal;
        _objectiveBodyText.lineSpacing = 2f;
        _objectiveBodyText.raycastTarget = false;

        rootGO.SetActive(true);
    }

    public void SetObjective(string objective)
    {
        EnsureUI();

        string clean = string.IsNullOrWhiteSpace(objective)
            ? PostTutorialDefaultObjective
            : objective.Replace("📋", "").Trim();

        if (clean.StartsWith("OBJECTIVE:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring("OBJECTIVE:".Length).Trim();
        }

        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = PostTutorialDefaultObjective;
        }

        _currentObjective = clean;

        if (_objectiveBodyText != null)
        {
            _objectiveBodyText.text = clean.ToUpperInvariant();
        }

        if (_panelRect != null)
        {
            _panelRect.gameObject.SetActive(true);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    public void SetVisible(bool visible)
    {
        EnsureUI();

        if (visible)
        {
            if (string.IsNullOrWhiteSpace(_currentObjective))
            {
                SetObjective(PostTutorialDefaultObjective);
            }

            if (_panelRect != null)
            {
                _panelRect.gameObject.SetActive(true);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }
        else
        {
            if (_panelRect != null)
            {
                _panelRect.gameObject.SetActive(false);
            }
        }
    }

    public void Hide()
    {
        // Fallback to default objective rather than blanking out completely
        SetObjective(PostTutorialDefaultObjective);
    }
}
