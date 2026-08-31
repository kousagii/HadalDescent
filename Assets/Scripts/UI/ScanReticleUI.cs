using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Center-screen target reticle UI with floating status text and action button.
///
/// Features:
///   - 4-corner animated square viewfinder.
///   - State-driven floating text (Font: Poppins, Size: 36).
///   - Large readable "VIEW IN BESTIARY" button (Font: Poppins, Size: 36).
///   - Automatically clears text and hides button when reticle is Idle or Locked.
/// </summary>
public class ScanReticleUI : MonoBehaviour
{
    public static ScanReticleUI Instance { get; private set; }

    public enum ReticleState { Idle, Locked, TooFar, AlreadyScanned }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Square Reticle Frame Settings")]
    [Tooltip("Total side length of the square frame when Idle (in pixels).")]
    [SerializeField] private float idleSquareSize   = 84f;
    [Tooltip("Total side length of the square frame when Locked (in pixels).")]
    [SerializeField] private float lockedSquareSize = 56f;
    [Tooltip("Length of each bracket arm along the edge.")]
    [SerializeField] private float armLength        = 26f;
    [Tooltip("Thickness of the bracket lines.")]
    [SerializeField] private float lineThickness    = 3f;
    [Tooltip("Animation speed for smooth transition.")]
    [SerializeField] private float animSpeed        = 10f;

    [Header("Typography")]
    [Tooltip("Font asset for status text and buttons (defaults to Poppins-Regular SDF).")]
    [SerializeField] private TMP_FontAsset reticleFont;

    private static readonly Color IdleColor     = new Color(1f, 1f, 1f, 0.50f);
    private static readonly Color LockedColor   = new Color(0f, 0.93f, 0.85f, 1f);
    private static readonly Color TooFarColor   = new Color(1f, 0.78f, 0.18f, 1f);
    private static readonly Color ScannedColor  = new Color(0.35f, 0.88f, 1.0f, 0.95f);

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private ReticleState _currentState = ReticleState.Idle;
    private float        _currentSize;
    private Color        _currentColor;

    private RectTransform _rootRect;
    private RectTransform[] _corners = new RectTransform[4];
    private Image[] _allArmImages = new Image[8];

    // Status text and bestiary button
    private TMP_Text _statusLabel;
    private Button   _viewBestiaryBtn;
    private TMP_Text _viewBestiaryLabel;

    // Species ID for the "View in Bestiary" button
    private string _currentSpeciesId;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _currentSize  = idleSquareSize;
        _currentColor = IdleColor;

        BuildCornerReticle();
    }

    private void Update()
    {
        bool hideReticle = (FactCardUI.Instance != null && FactCardUI.Instance.IsOpen);
        if (hideReticle)
        {
            if (_rootRect != null && _rootRect.gameObject.activeSelf)
                _rootRect.gameObject.SetActive(false);
            return;
        }
        else
        {
            if (_rootRect != null && !_rootRect.gameObject.activeSelf)
                _rootRect.gameObject.SetActive(true);
        }

        float targetSize;
        Color targetColor;

        switch (_currentState)
        {
            case ReticleState.Locked:
                targetSize  = lockedSquareSize;
                targetColor = LockedColor;
                break;
            case ReticleState.TooFar:
                targetSize  = idleSquareSize * 0.95f;
                targetColor = TooFarColor;
                break;
            case ReticleState.AlreadyScanned:
                targetSize  = lockedSquareSize;
                targetColor = ScannedColor;
                break;
            default: // Idle
                targetSize  = idleSquareSize;
                targetColor = IdleColor;
                break;
        }

        _currentSize  = Mathf.Lerp(_currentSize,  targetSize,  Time.deltaTime * animSpeed);
        _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * animSpeed);

        UpdateReticleLayout();
        UpdateStatusText();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void SetState(ReticleState state)
    {
        _currentState = state;
        if (state == ReticleState.Idle || state == ReticleState.Locked)
            _currentSpeciesId = null;
    }

    /// <summary>Sets the state and stores the species ID for "View in Bestiary".</summary>
    public void SetState(ReticleState state, string speciesId)
    {
        _currentState = state;
        _currentSpeciesId = speciesId;
    }

    /// <summary>Show or hide the square viewfinder reticle.</summary>
    public void SetVisible(bool visible)
    {
        if (_rootRect != null)
            _rootRect.gameObject.SetActive(visible);
    }

    private void OnEnable()
    {
        if (_rootRect != null) _rootRect.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_rootRect != null) _rootRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_rootRect != null) Destroy(_rootRect.gameObject);
    }

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    private void BuildCornerReticle()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Auto-load Poppins-Regular SDF font if not assigned in inspector
        if (reticleFont == null)
        {
            reticleFont = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                       ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                       ?? TMP_Settings.defaultFontAsset;
        }

        var rootGO = new GameObject("SquareCornerReticle", typeof(RectTransform));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootRect = rootGO.GetComponent<RectTransform>();
        _rootRect.anchorMin = _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _rootRect.pivot     = new Vector2(0.5f, 0.5f);
        _rootRect.sizeDelta = new Vector2(_currentSize, _currentSize);

        int imgIdx = 0;
        // 4 corners: TL (0), TR (1), BL (2), BR (3)
        for (int i = 0; i < 4; i++)
        {
            var cornerGO = new GameObject($"Corner_{i}", typeof(RectTransform));
            cornerGO.transform.SetParent(_rootRect, false);
            _corners[i] = cornerGO.GetComponent<RectTransform>();
            _corners[i].anchorMin = _corners[i].anchorMax = new Vector2(0.5f, 0.5f);
            _corners[i].sizeDelta = Vector2.zero;

            _allArmImages[imgIdx++] = CreateArmImage(cornerGO.transform, $"H_Arm_{i}");
            _allArmImages[imgIdx++] = CreateArmImage(cornerGO.transform, $"V_Arm_{i}");
        }

        // Status Label (below reticle, Font Size 36)
        var statusGO = new GameObject("StatusLabel", typeof(RectTransform));
        statusGO.transform.SetParent(_rootRect, false);
        var statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(700f, 60f);
        statusRect.anchoredPosition = new Vector2(0f, -70f);

        _statusLabel = statusGO.AddComponent<TextMeshProUGUI>();
        if (reticleFont != null) _statusLabel.font = reticleFont;
        _statusLabel.fontSize = 36f;
        _statusLabel.fontStyle = FontStyles.Bold;
        _statusLabel.alignment = TextAlignmentOptions.Center;
        _statusLabel.color = TooFarColor;
        _statusLabel.outlineWidth = 0.22f;
        _statusLabel.outlineColor = new Color32(0, 0, 0, 230);
        _statusLabel.text = "";
        _statusLabel.raycastTarget = false;

        // "View in Bestiary" button (below status label, Font Size 36)
        var viewBtnGO = new GameObject("ViewBestiaryBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        viewBtnGO.transform.SetParent(_rootRect, false);
        var viewBtnRect = viewBtnGO.GetComponent<RectTransform>();
        viewBtnRect.anchorMin = viewBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewBtnRect.pivot = new Vector2(0.5f, 1f);
        viewBtnRect.sizeDelta = new Vector2(360f, 62f);
        viewBtnRect.anchoredPosition = new Vector2(0f, -140f);

        var btnImg = viewBtnGO.GetComponent<Image>();
        btnImg.color = new Color(0.04f, 0.18f, 0.32f, 0.95f);
        _viewBestiaryBtn = viewBtnGO.GetComponent<Button>();
        _viewBestiaryBtn.onClick.AddListener(OnViewBestiaryPressed);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(viewBtnGO.transform, false);
        var lblRect = lblGO.GetComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = new Vector2(10f, 4f);
        lblRect.offsetMax = new Vector2(-10f, -4f);

        _viewBestiaryLabel = lblGO.AddComponent<TextMeshProUGUI>();
        if (reticleFont != null) _viewBestiaryLabel.font = reticleFont;
        _viewBestiaryLabel.fontSize = 36f;
        _viewBestiaryLabel.fontStyle = FontStyles.Bold;
        _viewBestiaryLabel.alignment = TextAlignmentOptions.Center;
        _viewBestiaryLabel.color = Color.white;
        _viewBestiaryLabel.outlineWidth = 0.20f;
        _viewBestiaryLabel.outlineColor = new Color32(0, 0, 0, 200);
        _viewBestiaryLabel.text = "VIEW IN BESTIARY";
        _viewBestiaryLabel.raycastTarget = false;

        viewBtnGO.SetActive(false);

        UpdateReticleLayout();
    }

    private void OnViewBestiaryPressed()
    {
        if (BestiaryManager.Instance != null && !string.IsNullOrEmpty(_currentSpeciesId))
            BestiaryManager.Instance.ShowBestiaryAndScrollTo(_currentSpeciesId);
    }

    private Image CreateArmImage(Transform parent, string name)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = IdleColor;
        img.raycastTarget = false;
        return img;
    }

    // -----------------------------------------------------------------------
    // Status Text Updates
    // -----------------------------------------------------------------------

    private void UpdateStatusText()
    {
        if (_statusLabel == null) return;

        if (ScannerSystem.Instance != null && ScannerSystem.Instance.IsAnyPopupOpen())
        {
            _statusLabel.text = "";
            if (_viewBestiaryBtn != null) _viewBestiaryBtn.gameObject.SetActive(false);
            return;
        }

        switch (_currentState)
        {
            case ReticleState.TooFar:
                // Pulsing alpha
                float alpha = 0.70f + 0.30f * Mathf.Sin(Time.time * 4.5f);
                _statusLabel.color = new Color(TooFarColor.r, TooFarColor.g, TooFarColor.b, alpha);
                _statusLabel.text = "Get closer to scan";
                if (_viewBestiaryBtn != null) _viewBestiaryBtn.gameObject.SetActive(false);
                break;

            case ReticleState.AlreadyScanned:
                _statusLabel.color = ScannedColor;
                _statusLabel.text = "Already cataloged";
                if (_viewBestiaryBtn != null) _viewBestiaryBtn.gameObject.SetActive(true);
                break;

            default: // Idle, Locked
                _statusLabel.text = "";
                if (_viewBestiaryBtn != null) _viewBestiaryBtn.gameObject.SetActive(false);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Layout Update
    // -----------------------------------------------------------------------

    private void UpdateReticleLayout()
    {
        if (_rootRect == null) return;

        _rootRect.sizeDelta = new Vector2(_currentSize, _currentSize);
        float halfS = _currentSize * 0.5f;
        float armL  = Mathf.Min(armLength, halfS);
        float t     = lineThickness;

        Vector2[] cornerPositions = {
            new Vector2(-halfS,  halfS), // Top-Left
            new Vector2( halfS,  halfS), // Top-Right
            new Vector2(-halfS, -halfS), // Bottom-Left
            new Vector2( halfS, -halfS)  // Bottom-Right
        };

        for (int i = 0; i < 4; i++)
        {
            _corners[i].anchoredPosition = cornerPositions[i];
        }

        int imgIdx = 0;

        // Top-Left (0)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(armL * 0.5f, -t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(t * 0.5f, -armL * 0.5f), new Vector2(t, armL));

        // Top-Right (1)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-armL * 0.5f, -t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-t * 0.5f, -armL * 0.5f), new Vector2(t, armL));

        // Bottom-Left (2)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(armL * 0.5f, t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(t * 0.5f, armL * 0.5f), new Vector2(t, armL));

        // Bottom-Right (3)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-armL * 0.5f, t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-t * 0.5f, armL * 0.5f), new Vector2(t, armL));

        foreach (var img in _allArmImages)
        {
            if (img != null) img.color = _currentColor;
        }
    }

    private void SetArmRect(Image img, Vector2 pos, Vector2 size)
    {
        if (img == null) return;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta        = size;
    }
}
