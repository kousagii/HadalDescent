using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls an individual blip on the Sonar Map.
/// Managed and recycled by SonarMapUI via object pooling.
///
/// Features:
///   - Dynamic color & icon by target category (Species, Debris, Hazard, Collectible).
///   - Discovered vs Undiscovered species styling (bright pulsing vs dim steady).
///   - Relative depth elevation indicator (▲ / ▼) for Sonar Tier >= 3.
///   - Sweep illumination fade when the radar beam passes over.
/// </summary>
public class SonarBlipUI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // UI References
    // -----------------------------------------------------------------------

    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image         iconImage;
    [SerializeField] private TMP_Text      depthIndicatorText;
    [SerializeField] private CanvasGroup   canvasGroup;

    // -----------------------------------------------------------------------
    // Color Palette
    // -----------------------------------------------------------------------

    public static readonly Color ColorUndiscoveredSpecies = new Color(0.00f, 0.93f, 0.85f, 1.00f); // Bright Cyan
    public static readonly Color ColorDiscoveredSpecies   = new Color(0.00f, 0.65f, 0.60f, 0.55f); // Dim Cyan
    public static readonly Color ColorDebris              = new Color(1.00f, 0.72f, 0.15f, 1.00f); // Amber Yellow
    public static readonly Color ColorHazard              = new Color(1.00f, 0.23f, 0.20f, 1.00f); // Danger Red
    public static readonly Color ColorCollectible         = new Color(1.00f, 0.85f, 0.20f, 1.00f); // Gold
    public static readonly Color ColorEnvironment         = new Color(0.25f, 0.80f, 1.00f, 0.80f); // Sea Blue

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private float _lastIlluminatedTime;
    private Color _baseColor;
    private bool  _isPulsing;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (rectTransform = GetComponent<RectTransform>());

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        // Undiscovered species or hazard subtle pulse animation
        if (_isPulsing)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            if (iconImage != null)
            {
                Color c = _baseColor;
                c.a *= pulse;
                iconImage.color = c;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Setup & Configuration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Update the blip's position, visuals, and elevation indicator.
    /// </summary>
    public void Configure(
        SonarTrackable trackable,
        Vector2 radarAnchoredPos,
        float verticalOffset,
        int sonarTier,
        Sprite defaultDotSprite,
        Sprite customCategoryIcon = null)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        rectTransform.anchoredPosition = radarAnchoredPos;

        // 1. Determine Base Color
        if (trackable.CustomBlipColor.a > 0f)
        {
            _baseColor = trackable.CustomBlipColor;
        }
        else
        {
            _baseColor = trackable.TargetType switch
            {
                SonarTrackable.SonarTargetType.Species     => trackable.IsDiscovered ? ColorDiscoveredSpecies : ColorUndiscoveredSpecies,
                SonarTrackable.SonarTargetType.Debris      => ColorDebris,
                SonarTrackable.SonarTargetType.Hazard      => ColorHazard,
                SonarTrackable.SonarTargetType.Collectible => ColorCollectible,
                SonarTrackable.SonarTargetType.Environment => ColorEnvironment,
                _                                          => ColorUndiscoveredSpecies
            };
        }

        _isPulsing = (!trackable.IsDiscovered && trackable.TargetType == SonarTrackable.SonarTargetType.Species)
                     || trackable.TargetType == SonarTrackable.SonarTargetType.Hazard;

        // 2. Icon Sprite & Size
        if (iconImage != null)
        {
            if (trackable.CustomBlipIcon != null)
            {
                iconImage.sprite = trackable.CustomBlipIcon;
                iconImage.rectTransform.sizeDelta = new Vector2(16f, 16f);
            }
            else if (customCategoryIcon != null && sonarTier >= 2)
            {
                iconImage.sprite = customCategoryIcon;
                iconImage.rectTransform.sizeDelta = new Vector2(14f, 14f);
            }
            else if (defaultDotSprite != null)
            {
                iconImage.sprite = defaultDotSprite;
                float dotSize = trackable.TargetType == SonarTrackable.SonarTargetType.Hazard ? 12f : 9f;
                iconImage.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
            }
            iconImage.color = _baseColor;
        }

        // 3. Elevation Indicator (Tier 3+)
        if (depthIndicatorText != null)
        {
            if (sonarTier >= 3 && Mathf.Abs(verticalOffset) > 4.5f)
            {
                depthIndicatorText.gameObject.SetActive(true);
                depthIndicatorText.text = verticalOffset > 0f ? "▲" : "▼";
                depthIndicatorText.color = _baseColor;
            }
            else
            {
                depthIndicatorText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Called when the rotating radar sweep beam passes over this blip.
    /// </summary>
    public void PingSweep()
    {
        _lastIlluminatedTime = Time.time;
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
