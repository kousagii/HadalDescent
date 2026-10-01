using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Procedural animated directional pointer and Sonar Contact Legend for the interactive tutorial.
/// Implements Section 5B of HadalDescent_Interactive_Tutorial_Story_Plan.md:
///   - Supports screen-space HUD targets and 3D world-space targets
///   - Vibrant Cyan (#00E5FF) procedural chevron/arrow
///   - Sine wave bouncing along pointing axis (amplitude 6px, 2Hz)
///   - Minimum 32px label, Normal Weight (no bold), Gameplay SDF
///   - Dark pill capsule background (0.04, 0.08, 0.16, 0.88)
///   - Side HUD Sonar Legend card during Step 3
/// </summary>
public class TutorialArrowUI : MonoBehaviour
{
    private static TutorialArrowUI _instance;
    public static TutorialArrowUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TutorialArrowUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    var go = new GameObject("TutorialArrowUI");
                    if (canvas != null)
                        go.transform.SetParent(canvas.transform, false);
                    _instance = go.AddComponent<TutorialArrowUI>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public enum ArrowDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private GameObject _arrowRoot;
    private RectTransform _arrowRootRect;
    private Image _arrowImage;
    private RectTransform _labelPillRect;
    private TextMeshProUGUI _labelText;
    private GameObject _sonarLegendRoot;

    // Target tracking
    private RectTransform _hudTargetRect;
    private Transform     _worldTarget;
    private Vector3       _worldOffset;
    private ArrowDirection _currentDirection = ArrowDirection.Left;
    private Vector2       _basePosition;
    private bool          _isPointing = false;

    private static Sprite _cachedArrowSprite;

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
        Hide();
    }

    private void EnsureUI()
    {
        if (_arrowRoot != null) return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            _canvas = FindFirstObjectByType<Canvas>();
        }

        if (_canvas == null)
        {
            var cgo = new GameObject("TutorialArrowCanvas", typeof(Canvas), typeof(CanvasScaler));
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 45;

            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        _canvasRect = _canvas.GetComponent<RectTransform>();
        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Pointer Arrow Root
        _arrowRoot = new GameObject("TutorialPointer_Root", typeof(RectTransform));
        _arrowRoot.transform.SetParent(_canvas.transform, false);
        _arrowRootRect = _arrowRoot.GetComponent<RectTransform>();
        _arrowRootRect.anchorMin = new Vector2(0.5f, 0.5f);
        _arrowRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _arrowRootRect.pivot     = new Vector2(0.5f, 0.5f);
        _arrowRootRect.sizeDelta = new Vector2(100f, 100f);

        // Arrow Graphic (Cyan Chevron)
        var arrowImgGO = new GameObject("ArrowGraphic", typeof(RectTransform), typeof(Image));
        arrowImgGO.transform.SetParent(_arrowRoot.transform, false);
        var arrowRect = arrowImgGO.GetComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(54f, 54f);
        arrowRect.anchoredPosition = Vector2.zero;

        _arrowImage = arrowImgGO.GetComponent<Image>();
        _arrowImage.sprite = GetOrCreateChevronSprite();
        _arrowImage.color = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        _arrowImage.raycastTarget = false;

        // Label Pill Capsule Background (0.04, 0.08, 0.16, 0.88)
        var pillGO = new GameObject("LabelPill", typeof(RectTransform), typeof(Image));
        pillGO.transform.SetParent(_arrowRoot.transform, false);
        _labelPillRect = pillGO.GetComponent<RectTransform>();
        _labelPillRect.sizeDelta = new Vector2(260f, 50f);

        var pillImg = pillGO.GetComponent<Image>();
        pillImg.color = new Color(0.04f, 0.08f, 0.16f, 0.90f);
        pillImg.raycastTarget = false;

        // Label Text (32px minimum, Normal Weight, Gameplay SDF, White)
        var labelTextGO = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelTextGO.transform.SetParent(pillGO.transform, false);
        var labelTextRect = labelTextGO.GetComponent<RectTransform>();
        labelTextRect.anchorMin = Vector2.zero;
        labelTextRect.anchorMax = Vector2.one;
        labelTextRect.sizeDelta = new Vector2(-20f, 0f);
        labelTextRect.anchoredPosition = Vector2.zero;

        _labelText = labelTextGO.GetComponent<TextMeshProUGUI>();
        _labelText.font = font;
        _labelText.fontStyle = FontStyles.Normal; // Strictly no bold
        _labelText.fontSize = 32f;
        _labelText.color = Color.white;
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.textWrappingMode = TextWrappingModes.NoWrap;
        _labelText.raycastTarget = false;

        BuildSonarLegendCard();
    }

    private void BuildSonarLegendCard()
    {
        if (_sonarLegendRoot != null) return;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        _sonarLegendRoot = new GameObject("SonarContactLegend_Card", typeof(RectTransform), typeof(Image));
        _sonarLegendRoot.transform.SetParent(_canvas.transform, false);
        var legendRect = _sonarLegendRoot.GetComponent<RectTransform>();

        // Anchored top-right below the Pause/Book buttons (150px pause + 20px margin = y = -170f)
        legendRect.anchorMin = new Vector2(1f, 1f);
        legendRect.anchorMax = new Vector2(1f, 1f);
        legendRect.pivot     = new Vector2(1f, 1f);
        legendRect.sizeDelta = new Vector2(500f, 260f); // 40px longer horizontally
        legendRect.anchoredPosition = new Vector2(-35f, -220f);

        var bg = _sonarLegendRoot.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.07f, 0.14f, 0.94f);
        bg.raycastTarget = false;

        // Top cyan line
        var topBorder = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        topBorder.transform.SetParent(_sonarLegendRoot.transform, false);
        var tbRect = topBorder.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0f, 1f);
        tbRect.anchorMax = new Vector2(1f, 1f);
        tbRect.sizeDelta = new Vector2(0f, 2f);
        tbRect.anchoredPosition = Vector2.zero;
        var tbImg = topBorder.GetComponent<Image>();
        tbImg.color = new Color(0f, 0.9f, 1f, 1f);
        tbImg.raycastTarget = false;

        // Title: SONAR CONTACT LEGEND
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(_sonarLegendRoot.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-20f, 32f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);

        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.font = font;
        titleText.fontStyle = FontStyles.Normal;
        titleText.fontSize = 22f;
        titleText.color = new Color(0.3f, 0.85f, 1f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.raycastTarget = false;
        titleText.text = "SONAR CONTACT LEGEND";

        // Entries container
        var entries = new (string label, Color color)[]
        {
            ("CYAN: MARINE SPECIES", new Color(0.00f, 0.93f, 0.88f)),
            ("YELLOW: LANDMARKS", new Color(1.00f, 0.90f, 0.13f)),
            ("ORANGE: COLLECTIBLE DEBRIS", new Color(1.00f, 0.72f, 0.15f)),
            ("RED: HAZARD ZONES", new Color(1.00f, 0.23f, 0.20f))
        };

        for (int i = 0; i < entries.Length; i++)
        {
            var rowGO = new GameObject($"Row_{i}", typeof(RectTransform), typeof(TextMeshProUGUI));
            rowGO.transform.SetParent(_sonarLegendRoot.transform, false);
            var rowRect = rowGO.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot     = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(-20f, 34f);
            rowRect.anchoredPosition = new Vector2(0f, -48f - (i * 46f));

            var rowText = rowGO.GetComponent<TextMeshProUGUI>();
            rowText.font = font;
            rowText.fontStyle = FontStyles.Normal;
            rowText.fontSize = 20f;
            rowText.color = entries[i].color;
            rowText.alignment = TextAlignmentOptions.Center;
            rowText.textWrappingMode = TextWrappingModes.NoWrap;
            rowText.enableAutoSizing = true;
            rowText.fontSizeMin = 16f;
            rowText.fontSizeMax = 20f;
            rowText.raycastTarget = false;
            rowText.text = entries[i].label;
        }

        _sonarLegendRoot.SetActive(false);
    }

    private void Update()
    {
        // Suppress all tutorial arrows while player is playing a minigame
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            if (_arrowRoot != null && _arrowRoot.activeSelf) _arrowRoot.SetActive(false);
            return;
        }

        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
        {
            return;
        }

        if (!_isPointing || _arrowRoot == null || !_arrowRoot.activeSelf) return;

        Vector2 screenPoint = Vector2.zero;
        float halfW = 0f;
        float halfH = 0f;

        Camera canvasCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;

        if (_hudTargetRect != null)
        {
            // Accurately compute physical bounding box and center of target in screen space
            Vector3[] corners = new Vector3[4];
            _hudTargetRect.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;

            Canvas targetCanvas = _hudTargetRect.GetComponentInParent<Canvas>();
            Camera targetCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? targetCanvas.worldCamera : null;

            screenPoint = RectTransformUtility.WorldToScreenPoint(targetCam, center);
            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);
            halfW = Mathf.Max(Mathf.Abs(screenMax.x - screenMin.x) * 0.5f, 50f);
            halfH = Mathf.Max(Mathf.Abs(screenMax.y - screenMin.y) * 0.5f, 45f);
        }
        else if (_worldTarget != null)
        {
            // Project 3D World target to screen
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 wpos = _worldTarget.position + _worldOffset;
                Vector3 sp = cam.WorldToScreenPoint(wpos);
                if (sp.z > 0f)
                {
                    screenPoint = new Vector2(sp.x, sp.y);
                }
                else
                {
                    // Target is behind camera - project it to edges by flipping
                    screenPoint = new Vector2(-sp.x, -sp.y);
                }

                // Clamp to screen bounds with padding so arrow always stays visible
                float padX = 120f;
                float padY = 120f;
                screenPoint.x = Mathf.Clamp(screenPoint.x, padX, Screen.width - padX);
                screenPoint.y = Mathf.Clamp(screenPoint.y, padY, Screen.height - padY);
            }
        }
        else
        {
            screenPoint = _basePosition;
        }

        // Apply sine wave bounce along pointing direction (amplitude 6px, 2Hz)
        float bounce = Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) * 6f;

        Vector2 offset = Vector2.zero;
        switch (_currentDirection)
        {
            case ArrowDirection.Left:
                offset = new Vector2(halfW + 48f + bounce, 0f);
                break;
            case ArrowDirection.Right:
                offset = new Vector2(-halfW - 48f - bounce, 0f);
                break;
            case ArrowDirection.Up:
                offset = new Vector2(0f, -halfH - 48f - bounce);
                break;
            case ArrowDirection.Down:
                offset = new Vector2(0f, halfH + 48f + bounce);
                break;
        }

        if (_canvasRect != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint + offset, canvasCam, out Vector2 localPoint))
            {
                _arrowRootRect.anchoredPosition = localPoint;
            }
            else
            {
                _arrowRootRect.position = screenPoint + offset;
            }
        }
        else
        {
            _arrowRootRect.position = screenPoint + offset;
        }
    }

    /// <summary>
    /// Points arrow at a 2D HUD RectTransform with a descriptive label.
    /// </summary>
    public void PointAtHUD(RectTransform target, string label, ArrowDirection direction = ArrowDirection.Left)
    {
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            Hide();
            return;
        }

        EnsureUI();
        if (target == null) return;

        _hudTargetRect = target;
        _worldTarget = null;
        _currentDirection = direction;

        ConfigureDirectionLayout(direction);
        SetLabel(label);

        _isPointing = true;
        _arrowRoot.SetActive(true);
    }

    /// <summary>
    /// Points arrow at a 3D world-space target (e.g. marine creature or debris cluster).
    /// </summary>
    public void PointAtWorld(Transform target, string label, Vector3 offset = default, ArrowDirection direction = ArrowDirection.Down)
    {
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            Hide();
            return;
        }

        EnsureUI();
        if (target == null) return;

        _hudTargetRect = null;
        _worldTarget = target;
        _worldOffset = offset == default ? Vector3.up * 1.5f : offset;
        _currentDirection = direction;

        ConfigureDirectionLayout(direction);
        SetLabel(label);

        _isPointing = true;
        _arrowRoot.SetActive(true);
    }

    /// <summary>
    /// Points arrow at an absolute screen position.
    /// </summary>
    public void PointAtScreenPosition(Vector2 screenPos, string label, ArrowDirection direction = ArrowDirection.Down)
    {
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive)
        {
            Hide();
            return;
        }

        EnsureUI();

        _hudTargetRect = null;
        _worldTarget = null;
        _basePosition = screenPos;
        _currentDirection = direction;

        ConfigureDirectionLayout(direction);
        SetLabel(label);

        _isPointing = true;
        _arrowRoot.SetActive(true);
    }

    private void ConfigureDirectionLayout(ArrowDirection direction)
    {
        float rotZ = 0f;
        Vector2 pillPos = Vector2.zero;
        Vector2 pillPivot = new Vector2(0.5f, 0.5f);

        switch (direction)
        {
            case ArrowDirection.Left: // Points left towards target on the left
                rotZ = 0f;
                pillPos = new Vector2(40f, 0f);
                pillPivot = new Vector2(0f, 0.5f);
                break;
            case ArrowDirection.Right: // Points right towards target on the right
                rotZ = 180f;
                pillPos = new Vector2(-40f, 0f);
                pillPivot = new Vector2(1f, 0.5f);
                break;
            case ArrowDirection.Up: // Points up towards target above
                rotZ = -90f;
                pillPos = new Vector2(0f, -45f);
                pillPivot = new Vector2(0.5f, 1f);
                break;
            case ArrowDirection.Down: // Points down towards target below
                rotZ = 90f;
                pillPos = new Vector2(0f, 45f);
                pillPivot = new Vector2(0.5f, 0f);
                break;
        }

        if (_arrowImage != null)
        {
            _arrowImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotZ);
        }

        if (_labelPillRect != null)
        {
            _labelPillRect.pivot = pillPivot;
            _labelPillRect.anchoredPosition = pillPos;
        }
    }

    private void SetLabel(string label)
    {
        if (_labelText != null)
        {
            _labelText.text = label;
            float textWidth = _labelText.GetPreferredValues(label).x;
            if (_labelPillRect != null)
            {
                _labelPillRect.sizeDelta = new Vector2(Mathf.Max(220f, textWidth + 40f), 52f);
            }
        }
    }

    private Coroutine _legendHideCoroutine;
    private float     _legendElapsedUnpaused = 999f;
    private const float LegendFixedDuration = 7.0f;

    /// <summary>
    /// Toggles the Sonar Radar Contact Legend card (shown during Step 3).
    /// Stays for 7 seconds fixed no matter what unless forceHide is explicitly specified.
    /// </summary>
    public void SetSonarLegendVisible(bool visible, bool forceHide = false)
    {
        EnsureUI();
        if (_sonarLegendRoot == null) return;

        if (visible)
        {
            _legendElapsedUnpaused = 0f;
            _sonarLegendRoot.SetActive(true);
            if (_legendHideCoroutine != null) StopCoroutine(_legendHideCoroutine);
            _legendHideCoroutine = StartCoroutine(AutoHideLegendRoutine(LegendFixedDuration));
        }
        else
        {
            // If caller asks to hide, but 7.0s haven't elapsed yet, do NOT hide early unless forceHide is true!
            if (!forceHide && (_legendElapsedUnpaused < LegendFixedDuration))
            {
                return; // Strictly keep visible for 7 seconds no matter what!
            }

            if (_legendHideCoroutine != null)
            {
                StopCoroutine(_legendHideCoroutine);
                _legendHideCoroutine = null;
            }
            _sonarLegendRoot.SetActive(false);
        }
    }

    private System.Collections.IEnumerator AutoHideLegendRoutine(float delay)
    {
        while (_legendElapsedUnpaused < delay)
        {
            if (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused)
            {
                _legendElapsedUnpaused += Time.unscaledDeltaTime;
            }
            yield return null;
        }

        if (_sonarLegendRoot != null)
        {
            _sonarLegendRoot.SetActive(false);
        }
        _legendHideCoroutine = null;
    }

    /// <summary>
    /// Hides the directional arrow pointer and Sonar legend.
    /// </summary>
    public void Hide()
    {
        _isPointing = false;
        _hudTargetRect = null;
        _worldTarget = null;

        if (_arrowRoot != null) _arrowRoot.SetActive(false);

        // Only hide legend if 7 seconds have elapsed
        if (_legendElapsedUnpaused >= LegendFixedDuration)
        {
            if (_sonarLegendRoot != null) _sonarLegendRoot.SetActive(false);
        }
    }

    public void HideInstant()
    {
        _isPointing = false;
        _hudTargetRect = null;
        _worldTarget = null;

        if (_legendHideCoroutine != null) { StopCoroutine(_legendHideCoroutine); _legendHideCoroutine = null; }
        if (_arrowRoot != null) _arrowRoot.SetActive(false);
        if (_sonarLegendRoot != null) _sonarLegendRoot.SetActive(false);
    }

    private static Sprite GetOrCreateChevronSprite()
    {
        if (_cachedArrowSprite != null) return _cachedArrowSprite;

        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Draw a sleek sharp chevron pointing left (towards x=0)
                float ny = Mathf.Abs((y - size / 2f) / (size / 2f)); // 0 at center, 1 at edge
                float tipX = ny * (size * 0.55f); // chevron shape
                float distFromChevron = Mathf.Abs(x - tipX);

                if (distFromChevron <= 6f && x < size * 0.8f)
                {
                    float alpha = 1f - (distFromChevron / 6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        _cachedArrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _cachedArrowSprite;
    }
}
