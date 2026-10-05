using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the simplified opening cinematic sequence (~15 seconds).
/// Implements Section 1B of HadalDescent_Interactive_Tutorial_Story_Plan.md:
///   Beat 1: Sunlight Zone Orbit (5s, 3rd-person camera orbiting 3D submarine model)
///   Beat 2: Cockpit Camera Dolly Transition (5s, dollies into forward cockpit viewport glass)
///   Beat 3: Hand-off to First Radio Transmission (Step 0) & Player Control
///   Supports instant skip at any time via [SKIP TUTORIAL] button.
/// </summary>
public class CinematicSequencer : MonoBehaviour
{
    public static CinematicSequencer Instance { get; private set; }

    [Header("Cinematic Settings")]
    [SerializeField] private float orbitDuration   = 5.0f;
    [SerializeField] private float dollyDuration   = 5.0f;
    [SerializeField] private float orbitDistance   = 12.0f;
    [SerializeField] private float orbitHeight     = 1.6f;

    [Header("UI Overlays")]
    [SerializeField] private Canvas        cinematicCanvas;
    [SerializeField] private TMP_Text      titleOverlayText;
    [SerializeField] private TMP_Text      subTitleOverlayText;
    [SerializeField] private Button        skipTutorialButton;

    private Camera         _cam;
    private PlayerMovement _player;
    private Transform      _camTransform;
    private Vector3        _originalCamLocalPos;
    private Quaternion     _originalCamLocalRot;
    private Coroutine      _cinematicRoutine;
    private bool           _isPlaying = false;
    private Action         _onCinematicFinished;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Starts the opening cinematic sequence.
    /// </summary>
    public void PlayOpeningCinematic(Action onFinished = null)
    {
        if (_isPlaying) return;
        _onFinishedCallback(onFinished);

        _player = FindFirstObjectByType<PlayerMovement>();
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();

        if (_player == null || _cam == null)
        {
            Debug.LogWarning("[CinematicSequencer] Player or Camera missing. Skipping cinematic.");
            FinishCinematic(skipped: false);
            return;
        }

        _camTransform = _cam.transform;
        _originalCamLocalPos = _camTransform.localPosition;
        _originalCamLocalRot = _camTransform.localRotation;

        // Temporarily disable SubmarineCamera so it doesn't fight the cinematic camera orbit
        var subCam = _cam.GetComponent<SubmarineCamera>();
        if (subCam != null) subCam.enabled = false;

        // Ensure 3D submarine mesh is visible during 3rd-person cinematic
        _player.ShowSubmarineVisuals();
        _player.enabled = false;

        var dragZone = FindFirstObjectByType<TouchDragZone>(FindObjectsInactive.Include);
        if (dragZone != null) dragZone.enabled = false;

        UIManager.Instance?.SetExplorationHUDVisible(false);

        BuildCinematicUI();

        _isPlaying = true;
        _cinematicRoutine = StartCoroutine(CinematicRoutine());
    }

    private void _onFinishedCallback(Action cb)
    {
        _onCinematicFinished = cb;
    }

    private void BuildCinematicUI()
    {
        if (cinematicCanvas != null)
        {
            cinematicCanvas.gameObject.SetActive(true);
            return;
        }

        var cgo = new GameObject("CinematicCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cinematicCanvas = cgo.GetComponent<Canvas>();
        cinematicCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cinematicCanvas.sortingOrder = 35;

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Skip Tutorial Button (Positioned at MIDDLE TOP: anchorMin/Max 0.5, 1)
        var skipGO = new GameObject("CinematicSkipBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        skipGO.transform.SetParent(cgo.transform, false);
        var skipRect = skipGO.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 1f);
        skipRect.anchorMax = new Vector2(0.5f, 1f);
        skipRect.pivot     = new Vector2(0.5f, 1f);
        skipRect.sizeDelta = new Vector2(320f, 50f);
        skipRect.anchoredPosition = new Vector2(0f, -22f);

        var skipImg = skipGO.GetComponent<Image>();
        skipImg.color = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f); // #1f5f7d

        var skipOutline = skipGO.AddComponent<Outline>();
        skipOutline.effectColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);
        skipOutline.effectDistance = new Vector2(2f, -2f);

        skipTutorialButton = skipGO.GetComponent<Button>();
        skipTutorialButton.targetGraphic = skipImg;
        skipTutorialButton.onClick.AddListener(OnSkipClicked);

        var colors = skipTutorialButton.colors;
        colors.normalColor = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f);
        colors.highlightedColor = new Color(0.18f, 0.48f, 0.62f, 1.00f);
        colors.pressedColor = new Color(0.08f, 0.26f, 0.35f, 1.00f);
        colors.selectedColor = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f);
        skipTutorialButton.colors = colors;

        // Child Text for button label
        var skipTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        skipTxtGO.transform.SetParent(skipGO.transform, false);
        var stRect = skipTxtGO.GetComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.sizeDelta = Vector2.zero;
        stRect.anchoredPosition = Vector2.zero;

        var skipTxt = skipTxtGO.GetComponent<TextMeshProUGUI>();
        skipTxt.font = font;
        skipTxt.fontStyle = FontStyles.Normal; // Strictly no bold
        skipTxt.fontSize = 28f;
        skipTxt.color = Color.white; // Pure white text
        skipTxt.alignment = TextAlignmentOptions.Center;
        skipTxt.raycastTarget = false;
        skipTxt.text = "SKIP TUTORIAL";

        // Title Overlay (Center Screen)
        var titleGO = new GameObject("ExpeditionTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cgo.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot     = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1400f, 140f);
        titleRect.anchoredPosition = new Vector2(0f, 60f);

        titleOverlayText = titleGO.GetComponent<TextMeshProUGUI>();
        titleOverlayText.font = font;
        titleOverlayText.fontStyle = FontStyles.Normal;
        titleOverlayText.fontSize = 36f;
        titleOverlayText.color = new Color(0.92f, 0.96f, 1f, 1f);
        titleOverlayText.alignment = TextAlignmentOptions.Center;
        titleOverlayText.raycastTarget = false;
        titleOverlayText.text = "HADAL DESCENT — MARINE EXPEDITION\n<size=30><color=#00E5FF>BIODIVERSITY RESEARCH SUBMARINE 'HADAL EXPLORER'</color></size>";
    }

    private IEnumerator CinematicRoutine()
    {
        Transform sub = _player.transform;

        // Calculate visual bounds of submarine model to accurately focus the camera
        Bounds subBounds = new Bounds(sub.position, Vector3.one);
        bool hasBounds = false;
        var renderers = sub.GetComponentsInChildren<Renderer>(false);
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null) continue;
            if (!hasBounds) { subBounds = r.bounds; hasBounds = true; }
            else subBounds.Encapsulate(r.bounds);
        }
        Vector3 subCenter = hasBounds ? subBounds.center : (sub.position + sub.forward * 2.0f + Vector3.up * 0.3f);

        // ── BEAT 1: Sunlight Zone Starboard Orbit (5s) ──────────────
        // Camera moves along starboard (right) side of the submarine so the full hull is clearly seen
        float elapsed = 0f;

        while (elapsed < orbitDuration)
        {
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / orbitDuration;

            // Fade title text in and then out
            if (titleOverlayText != null)
            {
                float alpha = t < 0.3f ? (t / 0.3f) : (t > 0.8f ? (1f - (t - 0.8f) / 0.2f) : 1f);
                titleOverlayText.alpha = alpha;
            }

            // Starboard diagonal orbit: angles 25° to 60° relative to starboard
            float angleDeg = Mathf.Lerp(25f, 60f, t);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 offset = (sub.right * Mathf.Cos(rad) + sub.forward * Mathf.Sin(rad)) * orbitDistance + sub.up * orbitHeight;
            _camTransform.position = subCenter + offset;
            _camTransform.LookAt(subCenter);

            yield return null;
        }

        if (titleOverlayText != null) titleOverlayText.alpha = 0f;

        // ── BEAT 2: Cockpit Camera Dolly Transition (5s) ────────────
        elapsed = 0f;
        Vector3 dollyStartPos = _camTransform.position;
        Quaternion dollyStartRot = _camTransform.rotation;

        while (elapsed < dollyDuration)
        {
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dollyDuration);

            Vector3 targetWorldPos = sub.TransformPoint(_originalCamLocalPos);
            Quaternion targetWorldRot = sub.rotation * _originalCamLocalRot;

            _camTransform.position = Vector3.Lerp(dollyStartPos, targetWorldPos, t);
            _camTransform.rotation = Quaternion.Slerp(dollyStartRot, targetWorldRot, t);

            yield return null;
        }

        FinishCinematic(skipped: false);
    }

    private void OnSkipClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        SkipCinematic();
    }

    public void SkipCinematic()
    {
        if (!_isPlaying) return;

        if (_cinematicRoutine != null)
        {
            StopCoroutine(_cinematicRoutine);
            _cinematicRoutine = null;
        }

        FinishCinematic(skipped: true);
    }

    private void FinishCinematic(bool skipped)
    {
        _isPlaying = false;

        if (cinematicCanvas != null)
            cinematicCanvas.gameObject.SetActive(false);

        if (_camTransform != null && _player != null)
        {
            _camTransform.localPosition = _originalCamLocalPos;
            _camTransform.localRotation = _originalCamLocalRot;
        }

        // Hide 3D submarine mesh for first-person cockpit gameplay
        if (_player != null)
        {
            _player.HideSubmarineVisuals();
            _player.enabled = true;
        }

        var dragZone = FindFirstObjectByType<TouchDragZone>(FindObjectsInactive.Include);
        if (dragZone != null)
        {
            dragZone.gameObject.SetActive(true);
            dragZone.enabled = true;
        }

        if (_cam != null)
        {
            var subCam = _cam.GetComponent<SubmarineCamera>();
            if (subCam != null)
            {
                subCam.enabled = true;
                subCam.SnapYawToBody();
            }
        }

        UIManager.Instance?.SetExplorationHUDVisible(true);

        _onCinematicFinished?.Invoke();
        _onCinematicFinished = null;
    }
}
