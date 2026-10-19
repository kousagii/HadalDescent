using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Scrollable, touch-friendly research cards using the existing ocean UI theme.</summary>
public class ResearchVerificationUI : MonoBehaviour
{
    private GameObject _root;
    private RectTransform _safeArea;
    private RectTransform _content;
    private ScrollRect _scroll;
    private TMP_Text _title;
    private TMP_Text _progress;
    private Transform _footer;
    private const float ActionWidth = 420f;
    private const float ActionHeight = 86f;
    private readonly Color _cyan = new Color(0.25f, 0.95f, 0.90f);

    public void Show(string title, string progress, string body, Sprite photo = null)
    {
        EnsureUI();
        _root.SetActive(true);
        ClearChildren(_content);
        ClearChildren(_footer);
        _title.text = title;
        _progress.text = progress;
        Text(_content, "Body", body, 34f);
        if (photo != null) AddPhoto(_content, photo, 210f);
        _scroll.verticalNormalizedPosition = 1f;
    }

    public void AddAnswer(string label, Sprite photo, Action action, bool interactable = true,
        bool highlight = false)
    {
        var button = Button(_content, "Answer", label, action, highlight ? new Color(0.08f, 0.42f, 0.35f) :
            new Color(0.05f, 0.16f, 0.24f));
        button.interactable = interactable;
        if (photo != null) AddPhoto(button.transform, photo, 160f).transform.SetAsFirstSibling();
    }

    public void AddCameraAnswer(string label, Sprite modelPreview, Sprite photograph, Action action,
        bool interactable = true, bool highlight = false, SpeciesData rotatingSpecies = null)
    {
        var button = Button(_content, "CameraAnswer", label, action,
            highlight ? new Color(0.08f, 0.42f, 0.35f) : new Color(0.05f, 0.16f, 0.24f));
        button.interactable = interactable;
        var labelHolder = button.transform.Find("LabelContainer");
        var row = Rect("CameraAnswerRow", button.transform);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 260f;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;

        var visuals = Rect("CameraVisuals", row);
        var visualSize = visuals.gameObject.AddComponent<LayoutElement>();
        visualSize.minWidth = visualSize.preferredWidth = 0f;
        visualSize.flexibleWidth = 6f;
        var visualLayout = visuals.gameObject.AddComponent<HorizontalLayoutGroup>();
        visualLayout.spacing = 12f;
        visualLayout.childControlWidth = visualLayout.childControlHeight = true;
        visualLayout.childForceExpandWidth = visualLayout.childForceExpandHeight = true;
        AddVisualFrame(visuals, "ModelFrame", modelPreview, "Model not yet available", 230f, rotatingSpecies);
        AddVisualFrame(visuals, "PhotoFrame", photograph, "Photograph not yet available", 230f);

        labelHolder.SetParent(row, false);
        var labelSize = labelHolder.gameObject.AddComponent<LayoutElement>();
        labelSize.minWidth = labelSize.preferredWidth = 0f;
        labelSize.flexibleWidth = 4f;
    }

    public void AddFeedback(string message)
    {
        var text = Text(_content, "Body", message, 32f);
        text.color = _cyan;
    }

    public void AddAction(string label, Action action)
    {
        var button = Button(_footer, "Action", label, action, new Color(0.08f, 0.38f, 0.43f));
        var layout = button.GetComponent<LayoutElement>();
        layout.minWidth = 0f;
        layout.preferredWidth = ActionWidth;
        layout.flexibleWidth = 0f;
        layout.preferredHeight = ActionHeight;
    }

    /// <summary>The question footer always keeps Save on the left and Next on the right.</summary>
    public void SetQuestionActions(Action onSave, Action onNext = null)
    {
        ClearChildren(_footer);
        AddAction("SAVE & RETURN", onSave);
        if (onNext != null) AddAction("NEXT", onNext);
    }

    public void SetProceedAction(Action onProceed)
    {
        ClearChildren(_footer);
        AddAction("PROCEED", onProceed);
    }

    public void AddSpeciesVisuals(Sprite modelPreview, Sprite photograph, SpeciesData rotatingSpecies = null)
    {
        var row = Rect("SpeciesVisuals", _content);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 320f;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        AddVisualFrame(row, "ModelFrame", modelPreview, "Model not yet available", 290f, rotatingSpecies);
        AddVisualFrame(row, "PhotoFrame", photograph, "Photograph not yet available");
    }

    public void Hide() { if (_root != null) _root.SetActive(false); }

    public void Release()
    {
        if (_root != null)
        {
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
        }
        _root = null;
    }

    private void OnDestroy() => Release();

    private void Update()
    {
        if (_root == null || _safeArea == null || !_root.activeSelf) return;
        Rect safe = Screen.safeArea;
        _safeArea.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
        _safeArea.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
    }

    private void LateUpdate()
    {
        if (_footer == null || _root == null || !_root.activeSelf) return;
        int count = 0;
        foreach (Transform child in _footer) if (child.gameObject.activeSelf) count++;
        if (count == 0) return;
        float width = Mathf.Min(ActionWidth,
            Mathf.Max(0f, (((RectTransform)_footer).rect.width - 16f * (count - 1)) / count));
        foreach (Transform child in _footer)
            if (child.gameObject.activeSelf) child.GetComponent<LayoutElement>().preferredWidth = width;
    }

    private void EnsureUI()
    {
        if (_root != null) return;
        _root = new GameObject("ResearchVerificationCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12000;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var backdrop = Rect("Backdrop", _root.transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.03f, 0.06f, 0.96f);
        _safeArea = Rect("SafeArea", _root.transform);
        Stretch(_safeArea);
        var card = Rect("ResearchCard", _safeArea);
        card.anchorMin = new Vector2(0.06f, 0.05f);
        card.anchorMax = new Vector2(0.94f, 0.95f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.075f, 0.13f, 1f);
        var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(32, 32, 22, 22);
        stack.spacing = 14;
        stack.childControlWidth = stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        _title = Text(card, "Title", "RESEARCH VERIFICATION", 42f);
        _title.color = _cyan;
        _title.margin = new Vector4(0f, 0f, 160f, 0f);
        _progress = Text(card, "Progress", "", 32f);
        _progress.color = new Color(0.65f, 0.82f, 0.88f);

        var scrollArea = Rect("ScrollArea", card);
        scrollArea.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        _scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 45f;
        var viewport = Rect("Viewport", scrollArea);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.06f, 0.11f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _content = Rect("Content", viewport);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1);
        _content.sizeDelta = Vector2.zero;
        var contentLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(16, 16, 16, 16);
        contentLayout.spacing = 14;
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _footer = Rect("Actions", card);
        var footerLayout = _footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 16;
        footerLayout.childControlWidth = footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = false;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        _footer.gameObject.AddComponent<LayoutElement>().preferredHeight = ActionHeight;
        if (Application.isPlaying) UIManager.CreateMinigamePauseButton(_safeArea);
    }

    private TMP_Text Text(Transform parent, string name, string value, float size)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = UIThemeManager.AntoneFont;
        text.fontSize = size;
        text.text = value;
        text.color = Color.white;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.lineSpacing = UIThemeManager.DefaultLineSpacing;
        text.paragraphSpacing = UIThemeManager.DefaultParagraphSpacing;
        return text;
    }

    private Button Button(Transform parent, string name, string label, Action action, Color color)
    {
        var rect = Rect(name, parent);
        rect.gameObject.AddComponent<Image>().color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); action?.Invoke(); });
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = ActionHeight;
        element.minWidth = 0f;
        var stack = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(20, 20, 14, 14);
        stack.spacing = 8;
        stack.childControlWidth = stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.childAlignment = TextAnchor.MiddleCenter;
        // Keep multiline choice text outside a Button's immediate children so the global
        // theme enforcer preserves wrapping, just like other Body content.
        var holder = Rect("LabelContainer", rect);
        var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        var text = Text(holder, "Body", label, 32f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void AddVisualFrame(Transform parent, string name, Sprite sprite, string missingMessage,
        float imageHeight = 290f, SpeciesData rotatingSpecies = null)
    {
        var frame = Rect(name, parent);
        frame.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.10f, 0.16f);
        var element = frame.gameObject.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = 0f;
        element.flexibleWidth = 1f;
        var layout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        if (Application.isPlaying && rotatingSpecies != null && rotatingSpecies.modelPrefab != null)
        {
            var live = Rect("RotatingModel", frame);
            live.gameObject.AddComponent<LayoutElement>().preferredHeight = imageHeight;
            live.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var preview = live.gameObject.AddComponent<ResearchModelPreviewUI>();
            try
            {
                if (preview.Initialize(rotatingSpecies)) return;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[ResearchPreview] Using a snapshot for {rotatingSpecies.commonName}: {error.Message}");
            }
            live.gameObject.SetActive(false);
            Destroy(live.gameObject);
        }
        if (sprite != null) AddPhoto(frame, sprite, imageHeight);
        else
        {
            var missing = Text(frame, "Body", missingMessage, 32f);
            missing.alignment = TextAlignmentOptions.Center;
            missing.gameObject.AddComponent<LayoutElement>().preferredHeight = imageHeight;
        }
    }

    private Image AddPhoto(Transform parent, Sprite sprite, float height)
    {
        var rect = Rect("SpeciesPhoto", parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
