using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the Pause Menu and Settings Menu UI.
///
/// Features:
///   1. Main Pause Panel:
///      - RESUME (unpauses game)
///      - SAVE GAME (persists state with "GAME SAVED" banner)
///      - SETTINGS (opens settings sub-panel)
///      - EXIT GAME (returns to MainMenu scene)
///   2. Settings Panel:
///      - Audio: Master Volume, Music (BGM), SFX, Mute Toggle
///      - Controls & Navigation: Look Sensitivity (0.5x – 2.0x), Invert Y-Axis (Pitch Toggle)
///      - BACK button
///
/// Supports custom Inspector slots or builds procedural UI automatically.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("Custom UI Elements (Optional — procedural fallback created if null)")]
    [SerializeField] private GameObject customPauseRoot;
    [SerializeField] private GameObject customMainPausePanel;
    [SerializeField] private GameObject customSettingsPanel;

    [Header("Custom Main Pause Buttons")]
    [SerializeField] private Button customResumeButton;
    [SerializeField] private Button customSaveGameButton;
    [SerializeField] private Button customSettingsButton;
    [SerializeField] private Button customExitGameButton;

    [Header("Custom Settings Controls - Audio")]
    [SerializeField] private Slider customMasterVolumeSlider;
    [SerializeField] private Slider customMusicVolumeSlider;
    [SerializeField] private Slider customSfxVolumeSlider;
    [SerializeField] private Toggle customMuteToggle;

    [Header("Custom Settings Controls - Navigation")]
    [SerializeField] private Slider customSensitivitySlider;
    [SerializeField] private Toggle customInvertPitchToggle;
    [SerializeField] private Button customSettingsBackButton;

    [Header("Notification Text")]
    [SerializeField] private TMP_Text customSaveNotificationText;

    // Procedural UI References
    private GameObject _proceduralCanvasGO;
    private GameObject _proceduralRoot;
    private GameObject _mainPausePanel;
    private GameObject _settingsPanel;
    private TMP_Text   _saveNotification;
    private Coroutine  _saveNotificationCoroutine;

    // Setting Sliders / Toggles (Procedural)
    private Slider   _masterSlider;
    private TMP_Text _masterValText;
    private Slider   _musicSlider;
    private TMP_Text _musicValText;
    private Slider   _sfxSlider;
    private TMP_Text _sfxValText;
    private Toggle   _muteToggle;

    private Slider   _sensSlider;
    private TMP_Text _sensValText;
    private Toggle   _invertToggle;

    private bool _isPaused = false;
    public bool IsPaused => _isPaused;

    private bool _openedFromMainMenu = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // If another instance exists (e.g. from previous scene), copy custom fields if any
            if (customPauseRoot != null)
            {
                Instance.customPauseRoot = customPauseRoot;
                Instance.customMainPausePanel = customMainPausePanel;
                Instance.customSettingsPanel = customSettingsPanel;
                Instance.customResumeButton = customResumeButton;
                Instance.customSaveGameButton = customSaveGameButton;
                Instance.customSettingsButton = customSettingsButton;
                Instance.customExitGameButton = customExitGameButton;
                Instance.customMasterVolumeSlider = customMasterVolumeSlider;
                Instance.customMusicVolumeSlider = customMusicVolumeSlider;
                Instance.customSfxVolumeSlider = customSfxVolumeSlider;
                Instance.customMuteToggle = customMuteToggle;
                Instance.customSensitivitySlider = customSensitivitySlider;
                Instance.customInvertPitchToggle = customInvertPitchToggle;
                Instance.customSettingsBackButton = customSettingsBackButton;
                Instance.customSaveNotificationText = customSaveNotificationText;
                Instance.WireCustomControls();
            }
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isPaused = false;
        Time.timeScale = 1f;
        _openedFromMainMenu = false;

        // Clean up any procedural objects from old scene
        if (_proceduralCanvasGO != null)
        {
            Destroy(_proceduralCanvasGO);
            _proceduralCanvasGO = null;
        }
        _proceduralRoot = null;
        _mainPausePanel = null;
        _settingsPanel = null;
        _saveNotification = null;

        // Check if custom controls are present in the new scene
        if (customPauseRoot != null)
        {
            customPauseRoot.SetActive(false);
            WireCustomControls();
        }

        LoadSettingsToUI();
    }

    private void Start()
    {
        if (customPauseRoot != null)
        {
            customPauseRoot.SetActive(false);
            WireCustomControls();
        }

        LoadSettingsToUI();
    }

    private void Update()
    {
        // Don't allow pause toggle on Main Menu
        if (SceneManager.GetActiveScene().name == "MainMenu") return;

        // Toggle pause on Escape key (New Input System)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    // -----------------------------------------------------------------------
    // Pause / Resume Logic
    // -----------------------------------------------------------------------

    public void TogglePause()
    {
        if (_isPaused) ResumeGame();
        else           PauseGame();
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        if (customPauseRoot != null)
        {
            customPauseRoot.SetActive(true);
            if (customMainPausePanel != null) customMainPausePanel.SetActive(true);
            if (customSettingsPanel != null)  customSettingsPanel.SetActive(false);
        }
        else
        {
            if (_proceduralRoot == null) BuildProceduralUI();
            if (_proceduralRoot != null)
            {
                _proceduralRoot.SetActive(true);
                if (_mainPausePanel != null) _mainPausePanel.SetActive(true);
                if (_settingsPanel != null)  _settingsPanel.SetActive(false);
            }
        }

        LoadSettingsToUI();
        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("[PauseMenuUI] Game Paused.");
    }

    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;

        if (customPauseRoot != null)
        {
            customPauseRoot.SetActive(false);
        }

        if (_proceduralRoot != null)
        {
            _proceduralRoot.SetActive(false);
        }

        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("[PauseMenuUI] Game Resumed.");
    }

    // -----------------------------------------------------------------------
    // Button Action Handlers
    // -----------------------------------------------------------------------

    public void OnResumeClicked()
    {
        ResumeGame();
    }

    public void OnSaveGameClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveGame();
        }

        AudioManager.Instance?.PlayButtonClick();
        ShowSaveNotification("✓ GAME SAVED!");
    }

    public void OpenSettingsFromMainMenu()
    {
        _openedFromMainMenu = true;
        _isPaused = false;

        if (customPauseRoot != null)
        {
            customPauseRoot.SetActive(true);
            if (customMainPausePanel != null) customMainPausePanel.SetActive(false);
            if (customSettingsPanel != null)  customSettingsPanel.SetActive(true);
        }
        else
        {
            if (_proceduralRoot == null) BuildProceduralUI();
            if (_proceduralRoot != null)
            {
                _proceduralRoot.SetActive(true);
                if (_mainPausePanel != null) _mainPausePanel.SetActive(false);
                if (_settingsPanel != null)  _settingsPanel.SetActive(true);
            }
        }

        LoadSettingsToUI();
    }

    public void OnSettingsClicked()
    {
        _openedFromMainMenu = false;
        AudioManager.Instance?.PlayButtonClick();

        if (customPauseRoot != null)
        {
            if (customMainPausePanel != null) customMainPausePanel.SetActive(false);
            if (customSettingsPanel != null)  customSettingsPanel.SetActive(true);
        }
        else
        {
            if (_mainPausePanel != null) _mainPausePanel.SetActive(false);
            if (_settingsPanel != null)  _settingsPanel.SetActive(true);
        }

        LoadSettingsToUI();
    }

    public void OnSettingsBackClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (_openedFromMainMenu || SceneManager.GetActiveScene().name == "MainMenu")
        {
            _openedFromMainMenu = false;
            _isPaused = false;
            if (customPauseRoot != null)
            {
                customPauseRoot.SetActive(false);
                if (customSettingsPanel != null) customSettingsPanel.SetActive(false);
            }
            if (_proceduralRoot != null)
            {
                _proceduralRoot.SetActive(false);
                if (_settingsPanel != null) _settingsPanel.SetActive(false);
            }
            return;
        }

        if (customPauseRoot != null)
        {
            if (customSettingsPanel != null)  customSettingsPanel.SetActive(false);
            if (customMainPausePanel != null) customMainPausePanel.SetActive(true);
        }
        else
        {
            if (_settingsPanel != null)  _settingsPanel.SetActive(false);
            if (_mainPausePanel != null) _mainPausePanel.SetActive(true);
        }
    }

    public void OnExitGameClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        Time.timeScale = 1f;
        _isPaused = false;

        if (customPauseRoot != null) customPauseRoot.SetActive(false);
        if (_proceduralRoot != null) _proceduralRoot.SetActive(false);

        // Auto save on exit
        GameManager.Instance?.SaveGame();

        // Load Main Menu Scene
        SceneManager.LoadScene("MainMenu");
    }

    // -----------------------------------------------------------------------
    // Save Notification
    // -----------------------------------------------------------------------

    private void ShowSaveNotification(string message)
    {
        if (_saveNotificationCoroutine != null)
            StopCoroutine(_saveNotificationCoroutine);

        _saveNotificationCoroutine = StartCoroutine(AnimateSaveNotification(message));
    }

    private IEnumerator AnimateSaveNotification(string message)
    {
        TMP_Text targetText = customSaveNotificationText ?? _saveNotification;
        if (targetText == null) yield break;

        targetText.gameObject.SetActive(true);
        targetText.text = message;
        targetText.color = new Color(0.2f, 1f, 0.7f, 1f);

        float elapsed = 0f;
        float duration = 2.0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > 1.2f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (elapsed - 1.2f) / 0.8f);
                targetText.color = new Color(0.2f, 1f, 0.7f, alpha);
            }
            yield return null;
        }

        targetText.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Settings Binding & Persistence
    // -----------------------------------------------------------------------

    private void LoadSettingsToUI()
    {
        float master = AudioManager.Instance != null ? AudioManager.Instance.MasterVolume : PlayerPrefs.GetFloat("Settings_MasterVol", 1.0f);
        float music  = AudioManager.Instance != null ? AudioManager.Instance.BGMVolume    : PlayerPrefs.GetFloat("Settings_MusicVol", 1.0f);
        float sfx    = AudioManager.Instance != null ? AudioManager.Instance.SFXVolume    : PlayerPrefs.GetFloat("Settings_SFXVol", 1.0f);
        bool  isMute = AudioManager.Instance != null ? AudioManager.Instance.IsMuted       : PlayerPrefs.GetInt("Settings_Muted", 0) == 1;

        float sens   = SubmarineCamera.LookSensitivityMultiplier;
        bool  invert = SubmarineCamera.InvertPitch;

        // Custom UI Sync
        if (customMasterVolumeSlider != null) { customMasterVolumeSlider.value = master; }
        if (customMusicVolumeSlider != null)  { customMusicVolumeSlider.value  = music; }
        if (customSfxVolumeSlider != null)    { customSfxVolumeSlider.value    = sfx; }
        if (customMuteToggle != null)         { customMuteToggle.isOn          = isMute; }

        if (customSensitivitySlider != null)  { customSensitivitySlider.value  = sens; }
        if (customInvertPitchToggle != null)  { customInvertPitchToggle.isOn  = invert; }

        // Procedural UI Sync
        if (_masterSlider != null) { _masterSlider.value = master; UpdateMasterText(master); }
        if (_musicSlider != null)  { _musicSlider.value  = music;  UpdateMusicText(music); }
        if (_sfxSlider != null)    { _sfxSlider.value    = sfx;    UpdateSfxText(sfx); }
        if (_muteToggle != null)   { _muteToggle.isOn    = isMute; }

        if (_sensSlider != null)   { _sensSlider.value   = sens;   UpdateSensText(sens); }
        if (_invertToggle != null) { _invertToggle.isOn  = invert; }
    }

    private void WireCustomControls()
    {
        if (customResumeButton != null)
        {
            customResumeButton.onClick.RemoveListener(OnResumeClicked);
            customResumeButton.onClick.AddListener(OnResumeClicked);
        }
        if (customSaveGameButton != null)
        {
            customSaveGameButton.onClick.RemoveListener(OnSaveGameClicked);
            customSaveGameButton.onClick.AddListener(OnSaveGameClicked);
        }
        if (customSettingsButton != null)
        {
            customSettingsButton.onClick.RemoveListener(OnSettingsClicked);
            customSettingsButton.onClick.AddListener(OnSettingsClicked);
        }
        if (customExitGameButton != null)
        {
            customExitGameButton.onClick.RemoveListener(OnExitGameClicked);
            customExitGameButton.onClick.AddListener(OnExitGameClicked);
        }

        if (customSettingsBackButton != null)
        {
            customSettingsBackButton.onClick.RemoveListener(OnSettingsBackClicked);
            customSettingsBackButton.onClick.AddListener(OnSettingsBackClicked);
        }

        if (customMasterVolumeSlider != null)
        {
            customMasterVolumeSlider.onValueChanged.RemoveAllListeners();
            customMasterVolumeSlider.onValueChanged.AddListener(val => AudioManager.Instance?.SetMasterVolume(val));
        }

        if (customMusicVolumeSlider != null)
        {
            customMusicVolumeSlider.onValueChanged.RemoveAllListeners();
            customMusicVolumeSlider.onValueChanged.AddListener(val => AudioManager.Instance?.SetBGMVolume(val));
        }

        if (customSfxVolumeSlider != null)
        {
            customSfxVolumeSlider.onValueChanged.RemoveAllListeners();
            customSfxVolumeSlider.onValueChanged.AddListener(val => AudioManager.Instance?.SetSFXVolume(val));
        }

        if (customMuteToggle != null)
        {
            customMuteToggle.onValueChanged.RemoveAllListeners();
            customMuteToggle.onValueChanged.AddListener(mute => AudioManager.Instance?.SetMute(mute));
        }

        if (customSensitivitySlider != null)
        {
            customSensitivitySlider.onValueChanged.RemoveAllListeners();
            customSensitivitySlider.onValueChanged.AddListener(val =>
            {
                SubmarineCamera.LookSensitivityMultiplier = val;
                PlayerPrefs.SetFloat("Settings_LookSensitivity", val);
                PlayerPrefs.Save();
            });
        }

        if (customInvertPitchToggle != null)
        {
            customInvertPitchToggle.onValueChanged.RemoveAllListeners();
            customInvertPitchToggle.onValueChanged.AddListener(inv =>
            {
                SubmarineCamera.InvertPitch = inv;
                PlayerPrefs.SetInt("Settings_InvertPitch", inv ? 1 : 0);
                PlayerPrefs.Save();
            });
        }
    }

    private void UpdateMasterText(float val) { if (_masterValText != null) _masterValText.text = $"{Mathf.RoundToInt(val * 100f)}%"; }
    private void UpdateMusicText(float val)  { if (_musicValText != null)  _musicValText.text  = $"{Mathf.RoundToInt(val * 100f)}%"; }
    private void UpdateSfxText(float val)    { if (_sfxValText != null)    _sfxValText.text    = $"{Mathf.RoundToInt(val * 100f)}%"; }
    private void UpdateSensText(float val)   { if (_sensValText != null)   _sensValText.text   = $"{val:F1}x"; }

    // -----------------------------------------------------------------------
    // Procedural UI Construction (Dedicated 1920x1080 Overlay Canvas)
    // -----------------------------------------------------------------------

    private void BuildProceduralUI()
    {
        if (_proceduralRoot != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        // Dedicated Pause Menu Canvas with Top Sorting Order
        _proceduralCanvasGO = new GameObject("PauseMenu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _proceduralCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = _proceduralCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Root overlay
        _proceduralRoot = new GameObject("PauseMenu_Root", typeof(RectTransform), typeof(Image));
        _proceduralRoot.transform.SetParent(canvas.transform, false);

        var rootRect = _proceduralRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;
        _proceduralRoot.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.10f, 0.88f);

        // ── 1. Main Pause Panel ──────────────────────────────────────────
        _mainPausePanel = new GameObject("MainPausePanel", typeof(RectTransform), typeof(Image));
        _mainPausePanel.transform.SetParent(_proceduralRoot.transform, false);
        var mainRect = _mainPausePanel.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot     = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(560f, 620f);
        _mainPausePanel.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Title
        CreateText("Title", "PAUSE", new Vector2(0f, 230f), new Vector2(480f, 60f), 42, FontStyles.Bold, Color.white, _mainPausePanel.transform, font);

        // Save Notification Text
        var notifGO = new GameObject("SaveNotification", typeof(RectTransform));
        notifGO.transform.SetParent(_mainPausePanel.transform, false);
        var notifRect = notifGO.GetComponent<RectTransform>();
        notifRect.anchoredPosition = new Vector2(0f, 175f);
        notifRect.sizeDelta = new Vector2(460f, 40f);
        _saveNotification = notifGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _saveNotification.font = font;
        _saveNotification.fontSize = 28;
        _saveNotification.fontStyle = FontStyles.Bold;
        _saveNotification.alignment = TextAlignmentOptions.Center;
        _saveNotification.color = new Color(0.2f, 1f, 0.7f);
        _saveNotification.text = "";
        notifGO.SetActive(false);

        // Buttons: Resume, Save Game, Settings, Exit Game
        CreateMenuButton("ResumeBtn",   "RESUME",    new Vector2(0f, 90f),   new Color(0.08f, 0.55f, 0.65f), OnResumeClicked,   _mainPausePanel.transform, font);
        CreateMenuButton("SaveBtn",     "SAVE GAME", new Vector2(0f, 15f),   new Color(0.12f, 0.45f, 0.40f), OnSaveGameClicked, _mainPausePanel.transform, font);
        CreateMenuButton("SettingsBtn", "SETTINGS",  new Vector2(0f, -60f),  new Color(0.12f, 0.22f, 0.35f), OnSettingsClicked, _mainPausePanel.transform, font);
        CreateMenuButton("ExitBtn",     "EXIT GAME", new Vector2(0f, -145f), new Color(0.55f, 0.15f, 0.15f), OnExitGameClicked, _mainPausePanel.transform, font);

        // ── 2. Settings Panel ────────────────────────────────────────────
        _settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        _settingsPanel.transform.SetParent(_proceduralRoot.transform, false);
        var setRect = _settingsPanel.GetComponent<RectTransform>();
        setRect.anchorMin = new Vector2(0.5f, 0.5f);
        setRect.anchorMax = new Vector2(0.5f, 0.5f);
        setRect.pivot     = new Vector2(0.5f, 0.5f);
        setRect.sizeDelta = new Vector2(720f, 680f);
        _settingsPanel.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);
        _settingsPanel.SetActive(false);

        // Settings Header
        CreateText("SettingsTitle", "SETTINGS", new Vector2(0f, 290f), new Vector2(500f, 50f), 38, FontStyles.Bold, Color.white, _settingsPanel.transform, font);

        // ── Audio Section ──
        CreateText("AudioHeader", "AUDIO", new Vector2(0f, 240f), new Vector2(620f, 34f), 26, FontStyles.Bold, new Color(0.3f, 0.85f, 1f), _settingsPanel.transform, font, TextAlignmentOptions.Left);

        _masterSlider = CreateSettingSlider("MasterSlider", "Master Volume", new Vector2(0f, 195f), 0f, 1f, val => { AudioManager.Instance?.SetMasterVolume(val); UpdateMasterText(val); }, out _masterValText, _settingsPanel.transform, font);
        _musicSlider  = CreateSettingSlider("MusicSlider",  "Music (BGM)",   new Vector2(0f, 145f), 0f, 1f, val => { AudioManager.Instance?.SetBGMVolume(val);    UpdateMusicText(val); },  out _musicValText,  _settingsPanel.transform, font);
        _sfxSlider    = CreateSettingSlider("SfxSlider",    "SFX Volume",    new Vector2(0f, 95f),  0f, 1f, val => { AudioManager.Instance?.SetSFXVolume(val);    UpdateSfxText(val); },    out _sfxValText,    _settingsPanel.transform, font);
        _muteToggle   = CreateSettingToggle("MuteToggle",   "Mute All Audio", new Vector2(0f, 45f), mute => AudioManager.Instance?.SetMute(mute), _settingsPanel.transform, font);

        // ── Controls & Navigation Section ──
        CreateText("ControlsHeader", "CONTROLS & NAVIGATION", new Vector2(0f, -10f), new Vector2(620f, 34f), 26, FontStyles.Bold, new Color(0.3f, 0.85f, 1f), _settingsPanel.transform, font, TextAlignmentOptions.Left);

        _sensSlider   = CreateSettingSlider("SensSlider", "Look Sensitivity", new Vector2(0f, -55f), 0.5f, 2.0f, val =>
        {
            SubmarineCamera.LookSensitivityMultiplier = val;
            PlayerPrefs.SetFloat("Settings_LookSensitivity", val);
            PlayerPrefs.Save();
            UpdateSensText(val);
        }, out _sensValText, _settingsPanel.transform, font);

        _invertToggle = CreateSettingToggle("InvertToggle", "Invert Y-Axis (Pitch)", new Vector2(0f, -110f), inv =>
        {
            SubmarineCamera.InvertPitch = inv;
            PlayerPrefs.SetInt("Settings_InvertPitch", inv ? 1 : 0);
            PlayerPrefs.Save();
        }, _settingsPanel.transform, font);

        // Back Button
        CreateMenuButton("SettingsBackBtn", "BACK", new Vector2(0f, -240f), new Color(0.15f, 0.35f, 0.5f), OnSettingsBackClicked, _settingsPanel.transform, font, new Vector2(280f, 54f));
    }

    // -----------------------------------------------------------------------
    // UI Element Creation Helpers
    // -----------------------------------------------------------------------

    private void CreateText(string name, string text, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, Transform parent, TMP_FontAsset font, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.text = text;
    }

    private Button CreateMenuButton(string name, string label, Vector2 pos, Color btnColor, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, Vector2? customSize = null)
    {
        Vector2 size = customSize ?? new Vector2(400f, 58f);

        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var r = btnGO.GetComponent<RectTransform>();
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        var img = btnGO.GetComponent<Image>();
        img.color = btnColor;

        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(action);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.sizeDelta = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = label;

        return btn;
    }

    private Slider CreateSettingSlider(string name, string label, Vector2 pos, float min, float max, UnityEngine.Events.UnityAction<float> onValChanged, out TMP_Text valText, Transform parent, TMP_FontAsset font)
    {
        var rowGO = new GameObject(name + "_Row", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        var rowR = rowGO.GetComponent<RectTransform>();
        rowR.anchoredPosition = pos;
        rowR.sizeDelta = new Vector2(620f, 40f);

        // Label
        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(rowGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = new Vector2(0f, 0.5f);
        lblR.anchorMax = new Vector2(0f, 0.5f);
        lblR.pivot     = new Vector2(0f, 0.5f);
        lblR.anchoredPosition = new Vector2(10f, 0f);
        lblR.sizeDelta = new Vector2(230f, 36f);
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lblTMP.font = font;
        lblTMP.fontSize = 22;
        lblTMP.color = Color.white;
        lblTMP.text = label;

        // Slider Root
        var sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(rowGO.transform, false);
        var sliderR = sliderGO.GetComponent<RectTransform>();
        sliderR.anchorMin = new Vector2(0.5f, 0.5f);
        sliderR.anchorMax = new Vector2(0.5f, 0.5f);
        sliderR.pivot     = new Vector2(0.5f, 0.5f);
        sliderR.anchoredPosition = new Vector2(55f, 0f);
        sliderR.sizeDelta = new Vector2(240f, 22f);

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        // Background Track
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgR = bgGO.GetComponent<RectTransform>();
        bgR.anchorMin = new Vector2(0f, 0.35f);
        bgR.anchorMax = new Vector2(1f, 0.65f);
        bgR.sizeDelta = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0.12f, 0.20f, 0.30f);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faR = fillArea.GetComponent<RectTransform>();
        faR.anchorMin = new Vector2(0f, 0.35f);
        faR.anchorMax = new Vector2(1f, 0.65f);
        faR.sizeDelta = new Vector2(-20f, 0f);

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillArea.transform, false);
        var fillR = fillGO.GetComponent<RectTransform>();
        fillR.sizeDelta = Vector2.zero;
        var fillImg = fillGO.GetComponent<Image>();
        fillImg.color = new Color(0.08f, 0.75f, 0.68f);

        slider.fillRect = fillR;

        // Handle Area
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haR = handleArea.GetComponent<RectTransform>();
        haR.anchorMin = Vector2.zero;
        haR.anchorMax = Vector2.one;
        haR.sizeDelta = new Vector2(-20f, 0f);

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleArea.transform, false);
        var hR = handleGO.GetComponent<RectTransform>();
        hR.sizeDelta = new Vector2(24f, 24f);
        var hImg = handleGO.GetComponent<Image>();
        hImg.color = Color.white;

        slider.handleRect = hR;
        slider.targetGraphic = hImg;
        slider.onValueChanged.AddListener(onValChanged);

        // Value text
        var valGO = new GameObject("ValText", typeof(RectTransform));
        valGO.transform.SetParent(rowGO.transform, false);
        var valR = valGO.GetComponent<RectTransform>();
        valR.anchorMin = new Vector2(1f, 0.5f);
        valR.anchorMax = new Vector2(1f, 0.5f);
        valR.pivot     = new Vector2(1f, 0.5f);
        valR.anchoredPosition = new Vector2(-10f, 0f);
        valR.sizeDelta = new Vector2(70f, 36f);
        valText = valGO.AddComponent<TextMeshProUGUI>();
        if (font != null) valText.font = font;
        valText.fontSize = 20;
        valText.alignment = TextAlignmentOptions.Right;
        valText.color = new Color(0.3f, 0.85f, 1f);

        return slider;
    }

    private Toggle CreateSettingToggle(string name, string label, Vector2 pos, UnityEngine.Events.UnityAction<bool> onToggleChanged, Transform parent, TMP_FontAsset font)
    {
        var toggleGO = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        toggleGO.transform.SetParent(parent, false);
        var tr = toggleGO.GetComponent<RectTransform>();
        tr.anchoredPosition = pos;
        tr.sizeDelta = new Vector2(620f, 40f);

        var toggle = toggleGO.GetComponent<Toggle>();

        // Label
        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(toggleGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = new Vector2(0f, 0.5f);
        lblR.anchorMax = new Vector2(0f, 0.5f);
        lblR.pivot     = new Vector2(0f, 0.5f);
        lblR.anchoredPosition = new Vector2(10f, 0f);
        lblR.sizeDelta = new Vector2(300f, 36f);
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lblTMP.font = font;
        lblTMP.fontSize = 22;
        lblTMP.color = Color.white;
        lblTMP.text = label;

        // Background Checkbox
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(toggleGO.transform, false);
        var bgR = bgGO.GetComponent<RectTransform>();
        bgR.anchorMin = new Vector2(1f, 0.5f);
        bgR.anchorMax = new Vector2(1f, 0.5f);
        bgR.pivot     = new Vector2(1f, 0.5f);
        bgR.anchoredPosition = new Vector2(-20f, 0f);
        bgR.sizeDelta = new Vector2(32f, 32f);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.12f, 0.20f, 0.30f);

        // Checkmark
        var checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        var cR = checkGO.GetComponent<RectTransform>();
        cR.anchorMin = Vector2.zero;
        cR.anchorMax = Vector2.one;
        cR.sizeDelta = new Vector2(-8f, -8f);
        var cImg = checkGO.GetComponent<Image>();
        cImg.color = new Color(0.08f, 0.75f, 0.68f);

        toggle.graphic = cImg;
        toggle.targetGraphic = bgImg;
        toggle.onValueChanged.AddListener(onToggleChanged);

        return toggle;
    }
}
