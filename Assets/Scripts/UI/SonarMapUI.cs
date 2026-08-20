using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fixed Sonar Map (Radar) for the cockpit HUD.
///
/// Features:
///   - Fixed display in the top-left HUD (no interactive buttons, non-blocking).
///   - Heading-Up Polar Projection: Up on the radar always matches the submarine camera direction.
///   - Concentric distance rings & cardinal crosshairs.
///   - Smooth 360-degree rotating radar sweep beam with trail.
///   - Target tracking for Species (Discovered/Undiscovered), Debris, Hazards, and Collectibles.
///   - Scales detection radius and features dynamically with GameManager.Instance.SonarTier (1 to 5).
///   - Procedural vector-style sprite generation fallback (runs out-of-the-box with zero missing assets).
///   - Zero-allocation blip object pooling for 60fps performance on mobile.
///
/// Setup:
///   Attach to the 'Map placeholder' GameObject (or any HUD Canvas child).
///   Assign optional custom sprites/references in the Inspector or let it auto-build.
/// </summary>
public class SonarMapUI : MonoBehaviour
{
    public static SonarMapUI Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector - Custom UI Configuration (Optional)
    // -----------------------------------------------------------------------

    [Header("Custom UI Elements (Optional)")]
    [Tooltip("Container where blips and grid are rendered.")]
    [SerializeField] private RectTransform radarContainer;
    [Tooltip("Rotating sweep line image.")]
    [SerializeField] private RectTransform sweepLineRect;
    [Tooltip("Center submarine icon pointer.")]
    [SerializeField] private RectTransform playerPointerRect;
    [Tooltip("Label displaying current sonar range or tier.")]
    [SerializeField] private TMP_Text rangeLabel;

    [Header("Custom Sprites (Optional)")]
    [SerializeField] private Sprite circleFrameSprite;
    [SerializeField] private Sprite gridRingsSprite;
    [SerializeField] private Sprite sweepBeamSprite;
    [SerializeField] private Sprite playerSubSprite;
    [SerializeField] private Sprite defaultBlipDotSprite;
    [SerializeField] private Sprite speciesIconSprite;
    [SerializeField] private Sprite debrisIconSprite;
    [SerializeField] private Sprite hazardIconSprite;
    [SerializeField] private Sprite collectibleIconSprite;

    [Header("Sonar Tuning")]
    [Tooltip("Base detection radius in metres at Sonar Tier 1.")]
    [SerializeField] private float baseDetectionRadius = 50f;
    [Tooltip("Additional detection radius per Sonar Tier upgrade.")]
    [SerializeField] private float radiusPerTier        = 50f;
    [Tooltip("Radar sweep rotation speed in degrees per second.")]
    [SerializeField] private float sweepSpeed          = 120f;
    [Tooltip("Usable radar radius inside the bezel in UI pixels.")]
    [SerializeField] private float radarPixelRadius     = 105f;

    [Header("References (Auto-found if null)")]
    [SerializeField] private Transform       submarineTransform;
    [SerializeField] private SubmarineCamera submarineCamera;

    // -----------------------------------------------------------------------
    // Object Pool & Internal State
    // -----------------------------------------------------------------------

    private readonly List<SonarBlipUI> _blipPool = new List<SonarBlipUI>();
    private float _currentSweepAngle;
    private float _currentDetectionRadius;
    private int   _lastKnownTier = -1;

    // Procedurally generated sprites (cached to avoid duplicate textures)
    private static Sprite _cachedBezelSprite;
    private static Sprite _cachedGridSprite;
    private static Sprite _cachedDotSprite;
    private static Sprite _cachedSweepSprite;
    private static Sprite _cachedSubSprite;
    private static Sprite _cachedHazardSprite;
    private static Sprite _cachedDebrisSprite;
    private static Sprite _cachedFishSprite;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureSprites();
        BuildSonarVisuals();
        PrewarmBlipPool(32);
    }

    private void Start()
    {
        FindReferences();
        UpdateSonarTier(true);
    }

    private void Update()
    {
        // Check for Sonar Tier upgrade changes
        int currentTier = GameManager.Instance != null ? GameManager.Instance.SonarTier : 1;
        if (currentTier != _lastKnownTier)
        {
            UpdateSonarTier(false);
        }

        // Animate radar sweep beam
        UpdateSweep();

        // Update all blip positions
        UpdateBlips();
    }

    // -----------------------------------------------------------------------
    // Reference Finding
    // -----------------------------------------------------------------------

    private void FindReferences()
    {
        if (submarineTransform == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) submarineTransform = playerGO.transform;
            else
            {
                var pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) submarineTransform = pm.transform;
            }
        }

        if (submarineCamera == null)
        {
            submarineCamera = FindFirstObjectByType<SubmarineCamera>();
        }
    }

    // -----------------------------------------------------------------------
    // Tier & Range Configuration
    // -----------------------------------------------------------------------

    private void UpdateSonarTier(bool force)
    {
        int tier = GameManager.Instance != null ? GameManager.Instance.SonarTier : 1;
        tier = Mathf.Clamp(tier, 1, 5);
        _lastKnownTier = tier;

        // Tier 1: 50m, Tier 2: 100m, Tier 3: 150m, Tier 4: 200m, Tier 5: 250m
        _currentDetectionRadius = baseDetectionRadius + (tier - 1) * radiusPerTier;

        // Higher tiers have slightly faster sonar sweep
        sweepSpeed = 110f + (tier - 1) * 20f;

        if (rangeLabel != null)
        {
            rangeLabel.text = $"RANGE  {_currentDetectionRadius:0} m";
        }
    }

    // -----------------------------------------------------------------------
    // Sweep Animation
    // -----------------------------------------------------------------------

    private void UpdateSweep()
    {
        _currentSweepAngle = (_currentSweepAngle + sweepSpeed * Time.deltaTime) % 360f;

        if (sweepLineRect != null)
        {
            sweepLineRect.localRotation = Quaternion.Euler(0f, 0f, -_currentSweepAngle);
        }
    }

    // -----------------------------------------------------------------------
    // Blip Positioning & Polar Projection
    // -----------------------------------------------------------------------

    private void UpdateBlips()
    {
        if (submarineTransform == null)
        {
            FindReferences();
            if (submarineTransform == null) return;
        }

        Vector3 subPos = submarineTransform.position;
        float camYaw = submarineCamera != null ? submarineCamera.CameraYaw : submarineTransform.eulerAngles.y;

        int tier = _lastKnownTier;
        var trackables = SonarTrackable.AllTrackables;
        int activeBlipCount = 0;

        for (int i = 0; i < trackables.Count; i++)
        {
            var target = trackables[i];
            if (target == null || !target.gameObject.activeInHierarchy) continue;

            Vector3 targetPos = target.WorldPosition;
            Vector3 diff = targetPos - subPos;

            // Horizontal planar distance (X/Z plane)
            float horizDist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

            if (horizDist > _currentDetectionRadius) continue; // Out of sonar range

            // Angle in world space (0 deg = North / +Z, 90 deg = East / +X)
            float worldAngle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;

            // Relative angle to submarine camera heading (0 deg = straight ahead)
            float relativeAngle = Mathf.DeltaAngle(camYaw, worldAngle);
            float rad = relativeAngle * Mathf.Deg2Rad;

            // Normalized distance (0 at center, 1 at edge)
            float normDist = Mathf.Clamp01(horizDist / _currentDetectionRadius);

            // Radar UI position (Heading-Up: +Y is straight ahead, +X is right)
            float uiX = normDist * radarPixelRadius * Mathf.Sin(rad);
            float uiY = normDist * radarPixelRadius * Mathf.Cos(rad);

            // Fetch and configure pooled blip
            SonarBlipUI blip = GetBlipFromPool(activeBlipCount);
            Sprite catIcon = GetCategoryIcon(target.TargetType);

            blip.Configure(
                target,
                new Vector2(uiX, uiY),
                diff.y,
                tier,
                defaultBlipDotSprite != null ? defaultBlipDotSprite : _cachedDotSprite,
                catIcon);

            blip.SetActive(true);
            activeBlipCount++;
        }

        // Hide remaining unused pooled blips
        for (int i = activeBlipCount; i < _blipPool.Count; i++)
        {
            _blipPool[i].SetActive(false);
        }
    }

    private Sprite GetCategoryIcon(SonarTrackable.SonarTargetType type)
    {
        return type switch
        {
            SonarTrackable.SonarTargetType.Species     => speciesIconSprite     != null ? speciesIconSprite     : _cachedFishSprite,
            SonarTrackable.SonarTargetType.Debris      => debrisIconSprite      != null ? debrisIconSprite      : _cachedDebrisSprite,
            SonarTrackable.SonarTargetType.Hazard      => hazardIconSprite      != null ? hazardIconSprite      : _cachedHazardSprite,
            SonarTrackable.SonarTargetType.Collectible => collectibleIconSprite != null ? collectibleIconSprite : _cachedDotSprite,
            _                                          => null
        };
    }

    // -----------------------------------------------------------------------
    // Object Pooling
    // -----------------------------------------------------------------------

    private void PrewarmBlipPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CreatePooledBlip();
        }
    }

    private SonarBlipUI GetBlipFromPool(int index)
    {
        while (index >= _blipPool.Count)
        {
            CreatePooledBlip();
        }
        return _blipPool[index];
    }

    private SonarBlipUI CreatePooledBlip()
    {
        var go = new GameObject($"SonarBlip_{_blipPool.Count}", typeof(RectTransform));
        go.transform.SetParent(radarContainer != null ? radarContainer : transform, false);

        var blip = go.AddComponent<SonarBlipUI>();

        // Icon child
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.sprite = defaultBlipDotSprite != null ? defaultBlipDotSprite : _cachedDotSprite;

        // Depth indicator text child
        var textGO = new GameObject("DepthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.fontSize = 11;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var tr = textGO.GetComponent<RectTransform>();
        tr.anchoredPosition = new Vector2(0f, 10f);
        tr.sizeDelta = new Vector2(16f, 14f);

        // Reflection wire up
        SetPrivateField(blip, "rectTransform", go.GetComponent<RectTransform>());
        SetPrivateField(blip, "iconImage", iconImg);
        SetPrivateField(blip, "depthIndicatorText", text);

        go.SetActive(false);
        _blipPool.Add(blip);
        return blip;
    }

    // -----------------------------------------------------------------------
    // Procedural UI Construction (Fallback if no custom Canvas setup)
    // -----------------------------------------------------------------------

    private void BuildSonarVisuals()
    {
        var rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

        // Remove or disable default placeholder white Image if attached
        var existingImg = GetComponent<Image>();
        if (existingImg != null)
        {
            existingImg.enabled = false;
        }

        if (radarContainer == null)
        {
            var containerGO = new GameObject("RadarContainer", typeof(RectTransform));
            containerGO.transform.SetParent(transform, false);
            radarContainer = containerGO.GetComponent<RectTransform>();
            radarContainer.anchorMin = radarContainer.anchorMax = new Vector2(0.5f, 0.5f);
            radarContainer.pivot     = new Vector2(0.5f, 0.5f);
            radarContainer.sizeDelta = new Vector2(radarPixelRadius * 2f + 20f, radarPixelRadius * 2f + 20f);
            radarContainer.anchoredPosition = Vector2.zero;
        }

        // 1. Outer Bezel & Dark Ocean Background
        var bgGO = new GameObject("SonarBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(radarContainer, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(radarPixelRadius * 2f + 16f, radarPixelRadius * 2f + 16f);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.sprite = circleFrameSprite != null ? circleFrameSprite : _cachedBezelSprite;
        bgImg.color = new Color(0.02f, 0.08f, 0.13f, 0.92f);
        bgImg.raycastTarget = false;

        // 2. Concentric Range Rings & Crosshairs
        var gridGO = new GameObject("SonarGrid", typeof(RectTransform), typeof(Image));
        gridGO.transform.SetParent(radarContainer, false);
        var gridRect = gridGO.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(radarPixelRadius * 2f, radarPixelRadius * 2f);
        var gridImg = gridGO.GetComponent<Image>();
        gridImg.sprite = gridRingsSprite != null ? gridRingsSprite : _cachedGridSprite;
        gridImg.color = new Color(0.00f, 0.93f, 0.85f, 0.35f); // Cyan grid lines
        gridImg.raycastTarget = false;

        // 3. Rotating Sweep Beam
        if (sweepLineRect == null)
        {
            var sweepGO = new GameObject("SonarSweepBeam", typeof(RectTransform), typeof(Image));
            sweepGO.transform.SetParent(radarContainer, false);
            sweepLineRect = sweepGO.GetComponent<RectTransform>();
            sweepLineRect.anchorMin = sweepLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            sweepLineRect.pivot     = new Vector2(0.5f, 0.5f);
            sweepLineRect.sizeDelta = new Vector2(radarPixelRadius * 2f, radarPixelRadius * 2f);
            var sweepImg = sweepGO.GetComponent<Image>();
            sweepImg.sprite = sweepBeamSprite != null ? sweepBeamSprite : _cachedSweepSprite;
            sweepImg.color = new Color(0.00f, 0.93f, 0.85f, 0.40f);
            sweepImg.raycastTarget = false;
        }

        // 4. Center Submarine Pointer
        if (playerPointerRect == null)
        {
            var ptrGO = new GameObject("PlayerPointer", typeof(RectTransform), typeof(Image));
            ptrGO.transform.SetParent(radarContainer, false);
            playerPointerRect = ptrGO.GetComponent<RectTransform>();
            playerPointerRect.anchorMin = playerPointerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerPointerRect.sizeDelta = new Vector2(16f, 16f);
            var ptrImg = ptrGO.GetComponent<Image>();
            ptrImg.sprite = playerSubSprite != null ? playerSubSprite : _cachedSubSprite;
            ptrImg.color = new Color(0.00f, 1.00f, 0.90f, 1.00f);
            ptrImg.raycastTarget = false;
        }

        // 5. Range / Status Label with Sleek Pill Badge
        if (rangeLabel == null)
        {
            var badgeGO = new GameObject("RangeBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(radarContainer, false);
            var bRect = badgeGO.GetComponent<RectTransform>();
            bRect.anchorMin = bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot     = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = new Vector2(0f, -8f);
            bRect.sizeDelta = new Vector2(160f, 32f);

            var badgeImg = badgeGO.GetComponent<Image>();
            badgeImg.color = new Color(0.02f, 0.08f, 0.14f, 0.85f);
            badgeImg.raycastTarget = false;

            var labelGO = new GameObject("RangeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(badgeGO.transform, false);
            var lRect = labelGO.GetComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.sizeDelta = Vector2.zero;
            lRect.anchoredPosition = Vector2.zero;

            rangeLabel = labelGO.GetComponent<TextMeshProUGUI>();
            rangeLabel.fontSize = 20f;
            rangeLabel.fontStyle = FontStyles.Bold;
            rangeLabel.alignment = TextAlignmentOptions.Center;
            rangeLabel.color = new Color(0.00f, 0.93f, 0.85f, 1.00f);
            rangeLabel.text = "RANGE  50 m";
            rangeLabel.raycastTarget = false;
        }
    }

    // -----------------------------------------------------------------------
    // Procedural Vector Sprite Generators
    // -----------------------------------------------------------------------

    private static void EnsureSprites()
    {
        if (_cachedBezelSprite == null) _cachedBezelSprite = GenerateBezelSprite();
        if (_cachedGridSprite == null)  _cachedGridSprite  = GenerateGridSprite();
        if (_cachedDotSprite == null)   _cachedDotSprite   = GenerateDotSprite();
        if (_cachedSweepSprite == null) _cachedSweepSprite = GenerateSweepSprite();
        if (_cachedSubSprite == null)   _cachedSubSprite   = GenerateSubmarineSprite();
        if (_cachedFishSprite == null)  _cachedFishSprite  = GenerateFishIcon();
        if (_cachedDebrisSprite == null)_cachedDebrisSprite= GenerateDebrisIcon();
        if (_cachedHazardSprite == null)_cachedHazardSprite= GenerateHazardIcon();
    }

    private static Sprite GenerateBezelSprite()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d <= radius - 4f)
                {
                    // Dark ocean body
                    tex.SetPixel(x, y, new Color(0.03f, 0.08f, 0.14f, 0.94f));
                }
                else if (d <= radius)
                {
                    // Glowing cyan outer bezel
                    float t = 1f - (radius - d) / 4f;
                    tex.SetPixel(x, y, new Color(0.00f, 0.93f, 0.85f, Mathf.Lerp(0.9f, 0.4f, t)));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateGridSprite()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float maxR = center - 6f;

        // Clear transparent
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // 3 Concentric Rings (25%, 50%, 75%, 100%)
        float[] ringFractions = { 0.33f, 0.66f, 0.99f };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d > maxR) continue;

                // Concentric rings
                foreach (var rf in ringFractions)
                {
                    float targetR = maxR * rf;
                    if (Mathf.Abs(d - targetR) < 1.2f)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.55f));
                    }
                }

                // Crosshairs
                if (Mathf.Abs(x - center) < 1f || Mathf.Abs(y - center) < 1f)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.40f));
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateDotSprite()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d <= radius)
                {
                    float alpha = Mathf.SmoothStep(1f, 0.2f, d / radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateSweepSprite()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float maxR = center - 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d > maxR) { tex.SetPixel(x, y, Color.clear); continue; }

                float angle = Mathf.Atan2(x - center, y - center) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                // 60-degree trailing sweep gradient
                if (angle <= 60f)
                {
                    float t = 1f - (angle / 60f);
                    float alpha = t * t * 0.65f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateSubmarineSprite()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Draw forward-facing triangle pointer
        float center = size * 0.5f;
        for (int y = 6; y <= 26; y++)
        {
            float halfWidth = (y - 6) * 0.45f;
            int minX = Mathf.RoundToInt(center - halfWidth);
            int maxX = Mathf.RoundToInt(center + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateFishIcon()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Stylized fish ellipse + tail
        float cx = 14f, cy = 16f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / 8f;
                float dy = (y - cy) / 5f;
                if (dx * dx + dy * dy <= 1f)
                    tex.SetPixel(x, y, Color.white);

                // Tail triangle
                if (x >= 22 && x <= 28 && Mathf.Abs(y - cy) <= (x - 22) * 0.8f)
                    tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateDebrisIcon()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Stylized square / box icon
        for (int y = 8; y <= 24; y++)
        {
            for (int x = 8; x <= 24; x++)
            {
                if (x == 8 || x == 24 || y == 8 || y == 24 || (x >= 12 && x <= 20 && y >= 12 && y <= 20))
                    tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateHazardIcon()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Warning triangle
        float center = size * 0.5f;
        for (int y = 6; y <= 26; y++)
        {
            float halfWidth = (26 - y) * 0.55f;
            int minX = Mathf.RoundToInt(center - halfWidth);
            int maxX = Mathf.RoundToInt(center + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                if (y >= 24 || x == minX || x == maxX || (x == (int)center && y >= 12 && y <= 18) || (x == (int)center && y == 8))
                    tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // -----------------------------------------------------------------------
    // Helper Reflection
    // -----------------------------------------------------------------------

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }
}
