using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fixed circular Sonar Map HUD display (250x250 pixels).
///
/// Features:
///   - Heading-Up polar coordinate projection relative to submarine camera yaw.
///   - 360-degree rotating radar sweep line.
///   - Color-coded blips for Species (cyan), Debris (amber), Hazards (red), Collectibles (gold).
///   - Automatic object pooling for zero GC allocation during exploration.
///   - Dynamic range rings and scale badge based on Sonar Tier (Tier 1: 50m ... Tier 5: 250m).
///   - Procedural vector sprite generator fallback if sprite assets are unassigned.
/// </summary>
public class SonarMapUI : MonoBehaviour
{
    public static SonarMapUI Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector: UI Hierarchy References
    // -----------------------------------------------------------------------

    [Header("UI Structure")]
    [Tooltip("Root RectTransform containing the radar circle.")]
    [SerializeField] private RectTransform radarContainer;
    [Tooltip("Transform of the rotating sweep line.")]
    [SerializeField] private RectTransform sweepLineRect;
    [Tooltip("Transform of the center player submarine indicator.")]
    [SerializeField] private RectTransform playerPointerRect;
    [Tooltip("Text label displaying current sonar detection range (e.g. 'RANGE 50 m').")]
    [SerializeField] private TMP_Text rangeLabel;

    [Header("Visual Sprites (Optional - auto-generates if empty)")]
    [SerializeField] private Sprite circleFrameSprite;
    [SerializeField] private Sprite sweepBeamSprite;
    [SerializeField] private Sprite gridRingsSprite;
    [SerializeField] private Sprite playerSubSprite;
    [SerializeField] private Sprite defaultBlipDotSprite;

    [Header("Category Blip Icons (Optional)")]
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
    [Tooltip("Usable radar radius inside the bezel in UI pixels (for 250x250 UI).")]
    [SerializeField] private float radarPixelRadius    = 115f;

    [Header("Submarine References (auto-found)")]
    [SerializeField] private Transform       submarineTransform;
    [SerializeField] private SubmarineCamera submarineCamera;

    // -----------------------------------------------------------------------
    // Private State & Pooling
    // -----------------------------------------------------------------------

    private readonly List<SonarBlipUI> _blipPool = new List<SonarBlipUI>();
    private RectTransform _blipContainer;
    private float _currentSweepAngle       = 0f;
    private float _currentDetectionRadius = 50f;
    private int   _lastKnownTier           = -1;

    // Cached procedural sprites
    private static Sprite _cachedBezelSprite;
    private static Sprite _cachedSweepSprite;
    private static Sprite _cachedGridSprite;
    private static Sprite _cachedSubSprite;
    private static Sprite _cachedDotSprite;
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
        int currentTier = GameManager.Instance != null ? GameManager.Instance.SonarTier : 1;
        if (currentTier != _lastKnownTier)
        {
            UpdateSonarTier(false);
        }

        UpdateSweep();
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
                var playerMove = FindFirstObjectByType<PlayerMovement>();
                if (playerMove != null) submarineTransform = playerMove.transform;
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

        _currentDetectionRadius = baseDetectionRadius + (tier - 1) * radiusPerTier;
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

            float horizDist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

            if (horizDist > _currentDetectionRadius) continue;

            float worldAngle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
            float relativeAngle = Mathf.DeltaAngle(camYaw, worldAngle);
            float rad = relativeAngle * Mathf.Deg2Rad;

            float normDist = Mathf.Clamp01(horizDist / _currentDetectionRadius);

            float uiX = normDist * radarPixelRadius * Mathf.Sin(rad);
            float uiY = normDist * radarPixelRadius * Mathf.Cos(rad);

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
        go.transform.SetParent(_blipContainer != null ? _blipContainer : (radarContainer != null ? radarContainer : transform), false);

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

        SetPrivateField(blip, "rectTransform", go.GetComponent<RectTransform>());
        SetPrivateField(blip, "iconImage", iconImg);
        SetPrivateField(blip, "depthIndicatorText", text);

        go.SetActive(false);
        _blipPool.Add(blip);
        return blip;
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(obj, value);
    }

    // -----------------------------------------------------------------------
    // Procedural Graphics Construction (250x250)
    // -----------------------------------------------------------------------

    private void BuildSonarVisuals()
    {
        var rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(250f, 250f);

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
            radarContainer.sizeDelta = new Vector2(250f, 250f);
            radarContainer.anchoredPosition = Vector2.zero;
        }
        else
        {
            radarContainer.sizeDelta = new Vector2(250f, 250f);
        }

        // 1. Outer Bezel & Dark Ocean Background (250x250)
        var bgGO = new GameObject("SonarBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(radarContainer, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(250f, 250f);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.sprite = circleFrameSprite != null ? circleFrameSprite : _cachedBezelSprite;
        bgImg.color = new Color(0.02f, 0.08f, 0.13f, 0.92f);
        bgImg.raycastTarget = false;

        // 2. Concentric Range Rings & Crosshairs (230x230)
        var gridGO = new GameObject("SonarGrid", typeof(RectTransform), typeof(Image));
        gridGO.transform.SetParent(radarContainer, false);
        var gridRect = gridGO.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(230f, 230f);
        var gridImg = gridGO.GetComponent<Image>();
        gridImg.sprite = gridRingsSprite != null ? gridRingsSprite : _cachedGridSprite;
        gridImg.color = new Color(0.00f, 0.93f, 0.85f, 0.35f);
        gridImg.raycastTarget = false;

        // 3. Rotating Sweep Beam (230x230)
        if (sweepLineRect == null)
        {
            var sweepGO = new GameObject("SonarSweepBeam", typeof(RectTransform), typeof(Image));
            sweepGO.transform.SetParent(radarContainer, false);
            sweepLineRect = sweepGO.GetComponent<RectTransform>();
            sweepLineRect.anchorMin = sweepLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            sweepLineRect.pivot     = new Vector2(0.5f, 0.5f);
            sweepLineRect.sizeDelta = new Vector2(230f, 230f);
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
            playerPointerRect.sizeDelta = new Vector2(20f, 20f);
            var ptrImg = ptrGO.GetComponent<Image>();
            ptrImg.sprite = playerSubSprite != null ? playerSubSprite : _cachedSubSprite;
            ptrImg.color = new Color(0.00f, 1.00f, 0.90f, 1.00f);
            ptrImg.raycastTarget = false;
        }

        // 5. Dedicated Blip Layer (Placed on top of background & sweep beam)
        if (_blipContainer == null)
        {
            var blipsGO = new GameObject("BlipContainer", typeof(RectTransform));
            blipsGO.transform.SetParent(radarContainer, false);
            _blipContainer = blipsGO.GetComponent<RectTransform>();
            _blipContainer.anchorMin = _blipContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _blipContainer.pivot     = new Vector2(0.5f, 0.5f);
            _blipContainer.sizeDelta = new Vector2(230f, 230f);
            _blipContainer.anchoredPosition = Vector2.zero;
            _blipContainer.SetAsLastSibling();
        }

        // 6. Range / Status Label
        if (rangeLabel == null)
        {
            var badgeGO = new GameObject("RangeBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(radarContainer, false);
            var bRect = badgeGO.GetComponent<RectTransform>();
            bRect.anchorMin = bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot     = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = new Vector2(0f, -10f);
            bRect.sizeDelta = new Vector2(170f, 34f);

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
        if (_cachedBezelSprite == null)   _cachedBezelSprite   = GenerateBezelSprite(256);
        if (_cachedSweepSprite == null)   _cachedSweepSprite   = GenerateSweepSprite(256);
        if (_cachedGridSprite == null)    _cachedGridSprite    = GenerateGridSprite(256);
        if (_cachedSubSprite == null)     _cachedSubSprite     = GenerateSubPointerSprite(64);
        if (_cachedDotSprite == null)     _cachedDotSprite     = GenerateDotSprite(32);
        if (_cachedHazardSprite == null)  _cachedHazardSprite  = GenerateHazardSprite(48);
        if (_cachedDebrisSprite == null)  _cachedDebrisSprite  = GenerateDebrisSprite(48);
        if (_cachedFishSprite == null)    _cachedFishSprite    = GenerateFishSprite(48);
    }

    private static Sprite GenerateBezelSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d <= radius)
                {
                    float t = d / radius;
                    float ringEdge = Mathf.Abs(d - radius);
                    Color fill = Color.Lerp(new Color(0.01f, 0.05f, 0.09f, 0.95f), new Color(0.02f, 0.10f, 0.16f, 0.95f), t);
                    if (ringEdge < 3.5f)
                    {
                        fill = Color.Lerp(new Color(0.00f, 0.93f, 0.85f, 0.9f), fill, ringEdge / 3.5f);
                    }
                    tex.SetPixel(x, y, fill);
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

    private static Sprite GenerateSweepSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x - center, y - center);
                float dist = pos.magnitude;
                if (dist <= radius && dist > 1f)
                {
                    float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;

                    if (angle >= 0f && angle <= 60f)
                    {
                        float alpha = Mathf.Pow(1f - (angle / 60f), 2.2f) * 0.55f;
                        tex.SetPixel(x, y, new Color(0.00f, 0.93f, 0.85f, alpha));
                        continue;
                    }
                }
                tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateGridSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float maxR = center - 8f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d > maxR) { tex.SetPixel(x, y, Color.clear); continue; }

                float r1 = maxR * 0.33f;
                float r2 = maxR * 0.66f;
                float r3 = maxR * 1.00f;

                bool isRing = Mathf.Abs(d - r1) < 1.2f || Mathf.Abs(d - r2) < 1.2f || Mathf.Abs(d - r3) < 1.4f;
                bool isAxis = (Mathf.Abs(x - center) < 1.1f || Mathf.Abs(y - center) < 1.1f) && d <= maxR;

                if (isRing || isAxis)
                {
                    tex.SetPixel(x, y, new Color(0.00f, 0.93f, 0.85f, isRing ? 0.30f : 0.20f));
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

    private static Sprite GenerateSubPointerSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / (size * 0.5f);
                float ny = (y - size * 0.5f) / (size * 0.5f);
                bool inTriangle = ny >= -0.6f && ny <= 0.8f && Mathf.Abs(nx) <= (0.8f - ny) * 0.6f;
                tex.SetPixel(x, y, inTriangle ? new Color(0.00f, 1.00f, 0.90f, 1f) : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateDotSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float r = center - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateHazardSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / (size * 0.5f);
                float ny = (y - size * 0.5f) / (size * 0.5f);
                bool inTri = ny >= -0.6f && ny <= 0.7f && Mathf.Abs(nx) <= (0.7f - ny) * 0.75f;
                tex.SetPixel(x, y, inTri ? new Color(1.00f, 0.23f, 0.20f, 1f) : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateDebrisSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - size * 0.5f) / (size * 0.5f));
                float ny = Mathf.Abs((y - size * 0.5f) / (size * 0.5f));
                bool inDiamond = (nx + ny) <= 0.8f;
                tex.SetPixel(x, y, inDiamond ? new Color(1.00f, 0.72f, 0.15f, 1f) : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GenerateFishSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                tex.SetPixel(x, y, d <= center - 3f ? new Color(0.00f, 0.93f, 0.85f, 1f) : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
