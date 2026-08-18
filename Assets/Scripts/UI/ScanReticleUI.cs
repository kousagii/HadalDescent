using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Center-screen target reticle UI.
/// Forms a square camera viewfinder reticle made of 4 L-shaped corner brackets
/// with clean gaps in the middle of each side (top, bottom, left, right).
///
/// Animates inward and glows bright cyan-teal when a target is locked in focus.
/// Setup: Add ScanReticleUI to Canvas or UIManager child.
/// ScannerSystem calls SetState() every frame.
/// </summary>
public class ScanReticleUI : MonoBehaviour
{
    public static ScanReticleUI Instance { get; private set; }

    public enum ReticleState { Idle, Locked }

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

    private static readonly Color IdleColor   = new Color(1f, 1f, 1f, 0.50f);
    private static readonly Color LockedColor = new Color(0f, 0.93f, 0.85f, 1f);

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private ReticleState _currentState = ReticleState.Idle;
    private float        _currentSize;
    private Color        _currentColor;

    private RectTransform _rootRect;
    // 4 Corner RectTransforms [0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight]
    private RectTransform[] _corners = new RectTransform[4];
    private Image[] _allArmImages = new Image[8];

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
        float targetSize  = _currentState == ReticleState.Locked ? lockedSquareSize : idleSquareSize;
        Color targetColor = _currentState == ReticleState.Locked ? LockedColor      : IdleColor;

        _currentSize  = Mathf.Lerp(_currentSize,  targetSize,  Time.deltaTime * animSpeed);
        _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * animSpeed);

        UpdateReticleLayout();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void SetState(ReticleState state) => _currentState = state;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    private void BuildCornerReticle()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

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

            // Each corner gets 1 Horizontal arm + 1 Vertical arm
            _allArmImages[imgIdx++] = CreateArmImage(cornerGO.transform, $"H_Arm_{i}");
            _allArmImages[imgIdx++] = CreateArmImage(cornerGO.transform, $"V_Arm_{i}");
        }

        UpdateReticleLayout();
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

    private void UpdateReticleLayout()
    {
        if (_rootRect == null) return;

        _rootRect.sizeDelta = new Vector2(_currentSize, _currentSize);
        float halfS = _currentSize * 0.5f;
        float armL  = Mathf.Min(armLength, halfS);
        float t     = lineThickness;

        // Offsets for the 4 corners of the square: TL, TR, BL, BR
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

        // Top-Left (0): H arm extends right (+X), V arm extends down (-Y)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(armL * 0.5f, -t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(t * 0.5f, -armL * 0.5f), new Vector2(t, armL));

        // Top-Right (1): H arm extends left (-X), V arm extends down (-Y)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-armL * 0.5f, -t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-t * 0.5f, -armL * 0.5f), new Vector2(t, armL));

        // Bottom-Left (2): H arm extends right (+X), V arm extends up (+Y)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(armL * 0.5f, t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(t * 0.5f, armL * 0.5f), new Vector2(t, armL));

        // Bottom-Right (3): H arm extends left (-X), V arm extends up (+Y)
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-armL * 0.5f, t * 0.5f), new Vector2(armL, t));
        SetArmRect(_allArmImages[imgIdx++], new Vector2(-t * 0.5f, armL * 0.5f), new Vector2(t, armL));

        // Apply color to all arms
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
