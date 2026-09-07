using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Universal UI Theme and Font Enforcer for Hadal Descent.
/// Ensures all UI elements with text use the Antone font (Antone DEMO SDF)
/// and have a consistent, readable font size of at least 36px.
/// </summary>
public class UIThemeManager : MonoBehaviour
{
    public static UIThemeManager Instance { get; private set; }

    public const float MinFontSize = 32f;
    public const float DefaultLineSpacing = 10f;
    public const float DefaultParagraphSpacing = 10f;

    private static TMP_FontAsset _cachedAntoneFont;

    /// <summary>
    /// Retrieves the global Antone font asset (Antone DEMO SDF).
    /// </summary>
    public static TMP_FontAsset AntoneFont
    {
        get
        {
            if (_cachedAntoneFont != null) return _cachedAntoneFont;

            // 1. Check TMP_Settings default
            if (TMP_Settings.defaultFontAsset != null && TMP_Settings.defaultFontAsset.name.Contains("Antone"))
            {
                _cachedAntoneFont = TMP_Settings.defaultFontAsset;
                return _cachedAntoneFont;
            }

            // 2. Search all loaded TMP_FontAsset instances in memory
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in allFonts)
            {
                if (f != null && f.name.Contains("Antone"))
                {
                    _cachedAntoneFont = f;
                    return _cachedAntoneFont;
                }
            }

            // 3. Search in scene TextMeshPro components
            var inScene = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in inScene)
            {
                if (t != null && t.font != null && t.font.name.Contains("Antone"))
                {
                    _cachedAntoneFont = t.font;
                    return _cachedAntoneFont;
                }
            }

            // 4. Try Resources.Load fallbacks
            _cachedAntoneFont = Resources.Load<TMP_FontAsset>("Fonts/Antone DEMO SDF")
                            ?? Resources.Load<TMP_FontAsset>("Antone DEMO SDF")
                            ?? TMP_Settings.defaultFontAsset;

            return _cachedAntoneFont;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for AntoneFont (redirects all callers to Antone font).
    /// </summary>
    public static TMP_FontAsset AlohaFont => AntoneFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("UIThemeManager");
        Instance = go.AddComponent<UIThemeManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(ContinuousFontEnforcerRoutine());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyThemeToAllOpenCanvases();
    }

    /// <summary>
    /// Coroutine that checks active canvases periodically to enforce Antone font and minimum 36px font size
    /// on any dynamically spawned, enabled, or toggled UI elements.
    /// </summary>
    private IEnumerator ContinuousFontEnforcerRoutine()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (true)
        {
            ApplyThemeToAllOpenCanvases();
            yield return wait;
        }
    }

    /// <summary>
    /// Scans all active and inactive TMP_Text components across all canvases and enforces Antone font and >= 36px font size.
    /// </summary>
    public static void ApplyThemeToAllOpenCanvases()
    {
        var font = AntoneFont;
        var allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var text in allTexts)
        {
            if (text == null) continue;
            ApplyToText(text, font);
        }
    }

    /// <summary>
    /// Recursively enforces the Antone font and minimum 36px font size on a specific hierarchy.
    /// </summary>
    public static void ApplyAntoneTheme(GameObject root)
    {
        if (root == null) return;
        var font = AntoneFont;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text == null) continue;
            ApplyToText(text, font);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for ApplyAntoneTheme.
    /// </summary>
    public static void ApplyAlohaTheme(GameObject root) => ApplyAntoneTheme(root);

    /// <summary>
    /// Enforces Antone font and minimum font size on an individual TMP_Text component.
    /// </summary>
    public static void ApplyToText(TMP_Text text, TMP_FontAsset font = null)
    {
        if (text == null) return;
        if (font == null) font = AntoneFont;

        if (font != null && text.font != font)
        {
            text.font = font;
        }

        // Replace missing unicode en-dash/em-dash glyphs with standard ASCII hyphen (-)
        if (text.text != null && (text.text.Contains('–') || text.text.Contains('—') || text.text.Contains('\u2013') || text.text.Contains('\u2014')))
        {
            text.text = text.text.Replace('–', '-').Replace('—', '-').Replace('\u2013', '-').Replace('\u2014', '-');
        }

        // Identify if this text is a multi-line card body / description / habitat / clue element
        var bestiaryCard = text.GetComponentInParent<BestiaryCardUI>();
        bool isBestiaryBody = bestiaryCard != null && (text == bestiaryCard.habitatOrClueText || text.name == "Habitat" || text.name == "Clue");
        bool isMultiLineContent = isBestiaryBody || text.name == "Description" || text.name == "Fact" || text.name == "Body" || text.name == "EcologicalSignificance";

        // Special case: Sonar map range badge must strictly remain 36px and not auto-size down
        if (text.name == "RangeText" || text.name == "RangeLabel" || (text.transform.parent != null && text.transform.parent.name == "RangeBadge"))
        {
            text.fontSize = 36f;
            text.fontSizeMin = 36f;
            text.fontSizeMax = 36f;
            text.enableAutoSizing = false;
        }
        else if (isBestiaryBody)
        {
            // Preserve original font size and settings from the prefab; only ensure text wraps normally
            text.textWrappingMode = TextWrappingModes.Normal;
        }
        else if (text.fontSize < MinFontSize)
        {
            text.fontSize = MinFontSize;
        }

        // Ensure dedicated UI button labels stay on one single line, but NEVER force NoWrap on multi-line cards/descriptions
        if (!isMultiLineContent && bestiaryCard == null)
        {
            // Only apply NoWrap if it is directly on a Button or direct child of a Button (standard button label)
            if (text.GetComponent<Button>() != null || (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null))
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
        else if (isMultiLineContent)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        if (!isBestiaryBody)
        {
            if (text.enableAutoSizing && text.fontSizeMin < 24f)
            {
                text.fontSizeMin = 24f;
            }

            if (text.lineSpacing < DefaultLineSpacing)
            {
                text.lineSpacing = DefaultLineSpacing;
            }

            if (text.paragraphSpacing < DefaultParagraphSpacing)
            {
                text.paragraphSpacing = DefaultParagraphSpacing;
            }
        }
    }

    /// <summary>
    /// Preserves the aspect ratio of the sprite and crops it to completely fill the placeholder
    /// with the absolute minimum cut off possible (Aspect Fill / Center Crop / object-fit: cover).
    /// Prevents real-life photos from ever being distorted, stretched, or squished into non-matching placeholder aspect ratios.
    /// </summary>
    public static void ApplyAspectFillCrop(Image targetImage, Sprite sprite)
    {
        if (targetImage == null) return;
        if (sprite == null)
        {
            targetImage.sprite = null;
            return;
        }

        targetImage.sprite = sprite;
        targetImage.preserveAspect = false;

        RectTransform imgRect = targetImage.rectTransform;
        RectTransform parentRect = imgRect.parent as RectTransform;
        if (parentRect == null) return;

        // Ensure targetImage is enclosed within a dedicated RectMask2D container
        RectTransform maskContainer;
        if (parentRect.name.EndsWith("_CropContainer"))
        {
            maskContainer = parentRect;
        }
        else
        {
            string containerName = targetImage.gameObject.name + "_CropContainer";
            Transform existing = parentRect.Find(containerName);
            if (existing != null)
            {
                maskContainer = existing as RectTransform;
            }
            else
            {
                var containerGO = new GameObject(containerName, typeof(RectTransform), typeof(RectMask2D));
                maskContainer = containerGO.GetComponent<RectTransform>();
                maskContainer.SetParent(parentRect, false);

                // Copy original layout transforms to container
                maskContainer.anchorMin = imgRect.anchorMin;
                maskContainer.anchorMax = imgRect.anchorMax;
                maskContainer.pivot = imgRect.pivot;
                maskContainer.anchoredPosition = imgRect.anchoredPosition;
                maskContainer.sizeDelta = imgRect.sizeDelta;

                maskContainer.SetSiblingIndex(imgRect.GetSiblingIndex());
                imgRect.SetParent(maskContainer, false);
            }
        }

        // Ensure mask component is active on container
        if (maskContainer.GetComponent<RectMask2D>() == null && maskContainer.GetComponent<Mask>() == null)
        {
            maskContainer.gameObject.AddComponent<RectMask2D>();
        }

        // Center image within the crop container
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.pivot = new Vector2(0.5f, 0.5f);
        imgRect.anchoredPosition = Vector2.zero;

        // Calculate exact dimensions for minimum cut off (Aspect Fill)
        float cW = maskContainer.rect.width > 0f ? maskContainer.rect.width : maskContainer.sizeDelta.x;
        float cH = maskContainer.rect.height > 0f ? maskContainer.rect.height : maskContainer.sizeDelta.y;
        if (cW <= 0f) cW = 300f;
        if (cH <= 0f) cH = 300f;

        float sW = sprite.rect.width;
        float sH = sprite.rect.height;
        if (sW <= 0f || sH <= 0f) return;

        float containerAspect = cW / cH;
        float spriteAspect = sW / sH;

        if (spriteAspect > containerAspect)
        {
            // Landscape/wider photo: match container height (0 vertical cut off), crop left and right equally
            float h = cH;
            float w = cH * spriteAspect;
            imgRect.sizeDelta = new Vector2(w, h);
        }
        else
        {
            // Portrait/taller photo: match container width (0 horizontal cut off), crop top and bottom equally
            float w = cW;
            float h = cW / spriteAspect;
            imgRect.sizeDelta = new Vector2(w, h);
        }

        // Add or update AspectRatioFitter in EnvelopeParent mode so dynamic layout changes remain aspect-filled
        var fitter = targetImage.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = targetImage.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = spriteAspect;
    }
}

