using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Universal Asynchronous Loading Screen for Hadal Descent.
///
/// Features:
///   - Smoothly loads scenes in the background via SceneManager.LoadSceneAsync.
///   - Prevents game freeze during zone transitions and new game starts.
///   - Displays center game logo and atmospheric background (from Resources/UI/Loading/).
///   - Live animated progress bar with percentage readout (0% to 100%).
///   - Displays a random educational marine biodiversity fact from LoadingScreenFacts.
///   - Exposes serialized RectTransform fields so layout and positions can be tuned in the Unity Inspector.
///   - Persistent singleton across scenes (DontDestroyOnLoad, sorting order 9999).
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    private static LoadingScreenUI _instance;
    public static LoadingScreenUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    _instance = CreateInstance();
                }
            }
            return _instance;
        }
    }

    // -----------------------------------------------------------------------
    // Inspector Bindings (Positions & Visuals can be customized in Unity)
    // -----------------------------------------------------------------------

    [Header("UI Canvas & Root")]
    [SerializeField] private Canvas      loadingCanvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("UI Element Transforms (Adjust in Inspector)")]
    [Tooltip("Center logo RectTransform (adjust anchoredPosition & sizeDelta in Inspector).")]
    [SerializeField] private RectTransform logoTransform;

    [Tooltip("Progress bar container RectTransform (adjust anchoredPosition & sizeDelta in Inspector).")]
    [SerializeField] private RectTransform progressBarContainer;

    [Tooltip("Filled Image component that scales with loading progress.")]
    [SerializeField] private Image progressBarFill;

    [Tooltip("Text displaying percentage (e.g. '90%'), positioned to the right of the bar.")]
    [SerializeField] private TMP_Text progressPercentageText;

    [Tooltip("Text displaying the educational ocean fact directly underneath the bar.")]
    [SerializeField] private TMP_Text factText;

    [Tooltip("Fullscreen background image.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Center logo Image.")]
    [SerializeField] private Image logoImage;

    [Header("Visual Settings")]
    [SerializeField] private float fadeInDuration  = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.30f;
    [SerializeField] private float completionPause = 0.35f;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private bool _isLoading = false;
    public bool IsLoading => _isLoading;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (loadingCanvas == null)
            loadingCanvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);

        EnsureWhiteSprite();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Unity UGUI requires a valid Sprite for Image.Type.Filled to function.
    /// If sprite is null, Unity ignores fillAmount and renders a 100% solid full quad.
    /// </summary>
    private void EnsureWhiteSprite()
    {
        if (progressBarFill != null && progressBarFill.sprite == null)
        {
            Texture2D whiteTex = Texture2D.whiteTexture;
            progressBarFill.sprite = Sprite.Create(whiteTex, new Rect(0f, 0f, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f));
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillAmount = 0f;
        }
    }

    // -----------------------------------------------------------------------
    // Public Static API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Smoothly loads any scene asynchronously while displaying the loading screen.
    /// </summary>
    public static void LoadScene(string sceneName, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LoadingScreenUI] Cannot load null or empty scene name!");
            return;
        }

        Instance.StartLoadRoutine(sceneName, onComplete);
    }

    /// <summary>
    /// Loads a depth zone by index (0 = Sunlight, 1 = Twilight, 2 = Midnight, 3 = Abyss, 4 = Hadal).
    /// </summary>
    public static void LoadZone(int zoneIndex, Action onComplete = null)
    {
        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            Debug.LogWarning($"[LoadingScreenUI] Invalid zone index: {zoneIndex}");
            return;
        }

        ZoneManager.SetCurrentZone(zoneIndex);
        string sceneName = ZoneConfig.Zones[zoneIndex].sceneName;
        Instance.StartLoadRoutine(sceneName, onComplete, zoneIndex);
    }

    // -----------------------------------------------------------------------
    // Internal Loading Flow
    // -----------------------------------------------------------------------

    private void StartLoadRoutine(string sceneName, Action onComplete, int targetZoneIndex = -1)
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[LoadingScreenUI] Already loading a scene! Ignoring request for '{sceneName}'.");
            return;
        }

        gameObject.SetActive(true);
        if (loadingCanvas != null) loadingCanvas.gameObject.SetActive(true);

        StartCoroutine(LoadSceneCoroutine(sceneName, onComplete, targetZoneIndex));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, Action onComplete, int targetZoneIndex)
    {
        _isLoading = true;
        EnsureWhiteSprite();

        // 1. Select educational marine fact
        string fact = (targetZoneIndex >= 0)
            ? LoadingScreenFacts.GetFactForZone(targetZoneIndex)
            : LoadingScreenFacts.GetRandomFact();

        if (factText != null)
            factText.text = fact;

        // 2. Reset progress visuals immediately to 0
        float displayedProgress = 0f;
        UpdateProgressDisplay(0f);

        // 3. Fade In Loading Screen
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // 4. Begin Asynchronous Scene Load
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp == null)
        {
            Debug.LogError($"[LoadingScreenUI] Scene '{sceneName}' could not be loaded async! Check Build Settings.");
            _isLoading = false;
            yield break;
        }

        asyncOp.allowSceneActivation = false;

        // 5. Phase 1: Background Asset Loading (0% -> 75%)
        // Smoothly advances progress as Unity streams the scene data from disk.
        float phase1Target = 0.75f;
        while (asyncOp.progress < 0.9f)
        {
            float normalized = Mathf.Clamp01(asyncOp.progress / 0.9f);
            float target = normalized * phase1Target;

            displayedProgress = Mathf.MoveTowards(displayedProgress, target, Time.unscaledDeltaTime * 0.9f);
            UpdateProgressDisplay(displayedProgress);
            yield return null;
        }

        // Ensure smooth progress reaches 75% once asyncOp reaches 0.9
        while (displayedProgress < phase1Target)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, phase1Target, Time.unscaledDeltaTime * 1.5f);
            UpdateProgressDisplay(displayedProgress);
            yield return null;
        }

        // 6. Phase 2: Scene Activation & Object Instantiation (75% -> 90%)
        // Allow scene activation so Unity instantiates hierarchy, lighting, and materials.
        asyncOp.allowSceneActivation = true;

        while (!asyncOp.isDone)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 0.90f, Time.unscaledDeltaTime * 0.8f);
            UpdateProgressDisplay(displayedProgress);
            yield return null;
        }

        // 7. Phase 3: In-Scene Initialization & Terrain Generation (90% -> 100%)
        // Allow newly activated scene 1 frame to run Awake/Start/OnSceneLoaded
        yield return null;

        float terrainWaitTimeout = 3.5f;
        while (terrainWaitTimeout > 0f)
        {
            var terrain = FindFirstObjectByType<TerrainGenerator>();
            if (terrain == null || terrain.HasGenerated)
                break;

            terrainWaitTimeout -= Time.unscaledDeltaTime;
            displayedProgress = Mathf.MoveTowards(displayedProgress, 0.96f, Time.unscaledDeltaTime * 0.4f);
            UpdateProgressDisplay(displayedProgress);
            yield return null;
        }

        // Smoothly complete to 100%
        while (displayedProgress < 1.0f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1.0f, Time.unscaledDeltaTime * 2.5f);
            UpdateProgressDisplay(displayedProgress);
            yield return null;
        }

        UpdateProgressDisplay(1.0f);

        // Brief pause so player can observe completion and finish reading fact
        if (completionPause > 0f)
            yield return new WaitForSecondsRealtime(completionPause);

        // 8. Fade Out
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        _isLoading = false;
        onComplete?.Invoke();

        if (loadingCanvas != null)
            loadingCanvas.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }


    private void UpdateProgressDisplay(float progress)
    {
        EnsureWhiteSprite();
        progress = Mathf.Clamp01(progress);

        if (progressBarFill != null)
            progressBarFill.fillAmount = progress;

        if (progressPercentageText != null)
            progressPercentageText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    // -----------------------------------------------------------------------
    // Procedural Construction (Used if no custom prefab is assigned)
    // -----------------------------------------------------------------------

    private static LoadingScreenUI CreateInstance()
    {
        // Try loading custom prefab if created in Resources
        var prefab = Resources.Load<GameObject>("UI/Loading/LoadingScreenUI");
        if (prefab != null)
        {
            var spawned = Instantiate(prefab);
            spawned.name = "LoadingScreenUI";
            var ui = spawned.GetComponent<LoadingScreenUI>();
            if (ui != null) return ui;
        }

        // Build procedural loading screen
        var rootGO = new GameObject("LoadingScreenUI");
        var loadingUI = rootGO.AddComponent<LoadingScreenUI>();
        loadingUI.BuildProceduralUI();
        return loadingUI;
    }

    private void BuildProceduralUI()
    {
        // Canvas
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        loadingCanvas = canvas;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        TMP_FontAsset font = UIThemeManager.AntoneFont ?? TMP_Settings.defaultFontAsset;

        // 1. Background Image
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(transform, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        backgroundImage = bgGO.GetComponent<Image>();
        Sprite bgSprite = LoadSpriteSafe("UI/Loading/background");
        if (bgSprite != null)
        {
            backgroundImage.sprite = bgSprite;
            backgroundImage.color  = Color.white;
        }
        else
        {
            backgroundImage.color = new Color(0.015f, 0.035f, 0.075f, 1f); // Deep oceanic dark blue
        }

        // 2. Logo / Title (Matched to Splash Screen scale: 1500x1000)
        var logoGO = new GameObject("Logo", typeof(RectTransform), typeof(Image));
        logoGO.transform.SetParent(transform, false);
        logoTransform = logoGO.GetComponent<RectTransform>();
        logoTransform.anchorMin = new Vector2(0.5f, 0.5f);
        logoTransform.anchorMax = new Vector2(0.5f, 0.5f);
        logoTransform.pivot     = new Vector2(0.5f, 0.5f);
        logoTransform.anchoredPosition = new Vector2(0f, 90f);
        logoTransform.sizeDelta        = new Vector2(1500f, 1000f);

        logoImage = logoGO.GetComponent<Image>();
        Sprite logoSprite = LoadSpriteSafe("UI/Loading/logo") ?? LoadSpriteSafe("Splash/logo") ?? LoadSpriteSafe("logo");
        if (logoSprite != null)
        {
            logoImage.sprite = logoSprite;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
        }
        else
        {
            logoImage.color = Color.clear;
            // Fallback Title text
            var titleTextGO = new GameObject("FallbackTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleTextGO.transform.SetParent(logoGO.transform, false);
            var ttRect = titleTextGO.GetComponent<RectTransform>();
            ttRect.anchorMin = Vector2.zero;
            ttRect.anchorMax = Vector2.one;
            ttRect.sizeDelta = Vector2.zero;
            var tt = titleTextGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tt.font = font;
            tt.text = "HADAL DESCENT";
            tt.fontSize = 90;
            tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center;
            tt.color = new Color(0f, 0.92f, 1f, 1f);
        }

        // 3. Progress Bar Track / Container (Positioned in bottom region)
        var barContainerGO = new GameObject("ProgressBarContainer", typeof(RectTransform), typeof(Image));
        barContainerGO.transform.SetParent(transform, false);
        progressBarContainer = barContainerGO.GetComponent<RectTransform>();
        progressBarContainer.anchorMin = new Vector2(0.5f, 0.16f);
        progressBarContainer.anchorMax = new Vector2(0.5f, 0.16f);
        progressBarContainer.pivot     = new Vector2(0.5f, 0.5f);
        progressBarContainer.anchoredPosition = new Vector2(-45f, 0f);
        progressBarContainer.sizeDelta        = new Vector2(1200f, 26f);

        var trackImg = barContainerGO.GetComponent<Image>();
        trackImg.color = new Color(0.04f, 0.09f, 0.16f, 0.95f);

        // Progress Bar Fill
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barContainerGO.transform, false);
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        progressBarFill = fillGO.GetComponent<Image>();
        var fillTex = Texture2D.whiteTexture;
        progressBarFill.sprite = Sprite.Create(fillTex, new Rect(0f, 0f, fillTex.width, fillTex.height), new Vector2(0.5f, 0.5f));
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBarFill.fillAmount = 0f;
        progressBarFill.color = new Color(0f, 0.92f, 1f, 1f); // Cyan fill

        // 4. Percentage Text (Right of the progress bar)
        var percentGO = new GameObject("PercentageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        percentGO.transform.SetParent(transform, false);
        var percentRect = percentGO.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(0.5f, 0.16f);
        percentRect.anchorMax = new Vector2(0.5f, 0.16f);
        percentRect.pivot     = new Vector2(0f, 0.5f);
        percentRect.anchoredPosition = new Vector2(575f, 0f);
        percentRect.sizeDelta        = new Vector2(120f, 40f);

        progressPercentageText = percentGO.GetComponent<TextMeshProUGUI>();
        if (font != null) progressPercentageText.font = font;
        progressPercentageText.text = "0%";
        progressPercentageText.fontSize = 28;
        progressPercentageText.fontStyle = FontStyles.Bold;
        progressPercentageText.alignment = TextAlignmentOptions.Left;
        progressPercentageText.color = Color.white;

        // 5. Ocean Fact Text (Directly below progress bar)
        var factGO = new GameObject("FactText", typeof(RectTransform), typeof(TextMeshProUGUI));
        factGO.transform.SetParent(transform, false);
        var factRect = factGO.GetComponent<RectTransform>();
        factRect.anchorMin = new Vector2(0.5f, 0.16f);
        factRect.anchorMax = new Vector2(0.5f, 0.16f);
        factRect.pivot     = new Vector2(0.5f, 1f);
        factRect.anchoredPosition = new Vector2(0f, -28f);
        factRect.sizeDelta        = new Vector2(1350f, 85f);

        factText = factGO.GetComponent<TextMeshProUGUI>();
        if (font != null) factText.font = font;
        factText.text = "";
        factText.fontSize = 25;
        factText.enableAutoSizing = true;
        factText.fontSizeMin = 20;
        factText.fontSizeMax = 26;
        factText.alignment = TextAlignmentOptions.Top;
        factText.textWrappingMode = TextWrappingModes.Normal;
        factText.lineSpacing = 6f;
        factText.color = new Color(0.86f, 0.93f, 1f, 0.95f); // Crisp ice blue

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Helper to safely load a Sprite from Resources whether imported as Sprite or Texture2D.
    /// </summary>
    private static Sprite LoadSpriteSafe(string resourcePath)
    {
        Sprite s = Resources.Load<Sprite>(resourcePath);
        if (s != null) return s;

        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        return null;
    }
}
