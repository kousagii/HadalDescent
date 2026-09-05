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

        // Special case: Sonar map range badge must strictly remain 36px and not auto-size down
        if (text.name == "RangeText" || text.name == "RangeLabel" || (text.transform.parent != null && text.transform.parent.name == "RangeBadge"))
        {
            text.fontSize = 36f;
            text.fontSizeMin = 36f;
            text.fontSizeMax = 36f;
            text.enableAutoSizing = false;
        }
        else if (text.fontSize < MinFontSize)
        {
            text.fontSize = MinFontSize;
        }

        // Ensure all UI button labels strictly stay on one single line
        if (text.GetComponentInParent<Button>() != null)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

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
