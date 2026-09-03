using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Controls Main Menu interactions:
///   - New Game (Prompts overwrite warning if save data exists)
///   - Continue Game (Disabled / greyed out if no save data exists)
///   - Settings (Opens settings sub-panel)
///   - Exit / Quit Game
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Exact name of the Sunlight Zone scene to load when starting a new game")]
    [SerializeField] private string startingZoneSceneName = "SunlightZone";

    [Header("Main Menu Buttons (Auto-found if unassigned)")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Custom Overwrite Confirmation Popup (Optional)")]
    [Tooltip("Custom popup root GameObject to show when confirming save overwrite.")]
    [SerializeField] private GameObject customConfirmPopupRoot;
    [Tooltip("Button in custom popup to confirm starting a new game.")]
    [SerializeField] private Button     customConfirmButton;
    [Tooltip("Button in custom popup to cancel.")]
    [SerializeField] private Button     customCancelButton;

    // Procedural Confirmation Modal Fallback
    private GameObject _proceduralConfirmModal;

    private void Awake()
    {
        AutoFindButtons();
        if (customConfirmPopupRoot != null)
            customConfirmPopupRoot.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshMenuButtons();
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainMenuBGM();
        }

        RefreshMenuButtons();
    }

    private void AutoFindButtons()
    {
        if (newGameButton == null)
        {
            var go = GameObject.Find("NewGame") ?? GameObject.Find("New Game") ?? GameObject.Find("NewGameButton") ?? GameObject.Find("New Game Button");
            if (go != null) newGameButton = go.GetComponent<Button>();
        }

        if (continueButton == null)
        {
            var go = GameObject.Find("Continue") ?? GameObject.Find("ContinueGame") ?? GameObject.Find("ContinueButton") ?? GameObject.Find("Continue Button");
            if (go != null) continueButton = go.GetComponent<Button>();
        }

        if (settingsButton == null)
        {
            var go = GameObject.Find("Settings") ?? GameObject.Find("SettingsButton") ?? GameObject.Find("Settings Button");
            if (go != null) settingsButton = go.GetComponent<Button>();
        }

        if (exitButton == null)
        {
            var go = GameObject.Find("Exit") ?? GameObject.Find("ExitButton") ?? GameObject.Find("Exit Button") ?? GameObject.Find("Quit") ?? GameObject.Find("QuitButton");
            if (go != null) exitButton = go.GetComponent<Button>();
        }

        // Wire click listeners
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            newGameButton.onClick.AddListener(OnNewGameClicked);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnQuitClicked);
            exitButton.onClick.AddListener(OnQuitClicked);
        }

        if (customConfirmButton != null)
        {
            customConfirmButton.onClick.RemoveListener(OnConfirmNewGame);
            customConfirmButton.onClick.AddListener(OnConfirmNewGame);
        }

        if (customCancelButton != null)
        {
            customCancelButton.onClick.RemoveListener(OnCancelNewGame);
            customCancelButton.onClick.AddListener(OnCancelNewGame);
        }
    }

    /// <summary>
    /// Refreshes button states, specifically disabling / greying out Continue if no save data exists.
    /// </summary>
    public void RefreshMenuButtons()
    {
        bool hasSave = GameManager.HasSaveData();
        Debug.Log($"[MainMenuController] RefreshMenuButtons → HasSaveData = {hasSave}");

        if (continueButton != null)
        {
            continueButton.interactable = hasSave;

            // Apply CanvasGroup to visually dim and prevent all raycast clicks
            var cg = continueButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = continueButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = hasSave ? 1.0f : 0.35f;
            cg.blocksRaycasts = hasSave;
            cg.interactable = hasSave;

            // Update TMP text color if present
            var tmp = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = hasSave ? Color.black : new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }
        }
        else
        {
            Debug.LogWarning("[MainMenuController] continueButton reference is null — cannot disable it.");
        }
    }

    // -----------------------------------------------------------------------
    // Button Handlers
    // -----------------------------------------------------------------------

    public void OnNewGameClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        // If save data exists, show confirmation warning before deleting!
        if (GameManager.HasSaveData())
        {
            ShowOverwriteConfirmation();
        }
        else
        {
            StartFreshNewGame();
        }
    }

    [Header("Zone Selection Navigation (Figure 2)")]
    [Tooltip("If true, clicking Continue opens the Zone Selection screen. If false, loads saved zone directly.")]
    [SerializeField] private bool continueOpensZoneSelection = true;
    [SerializeField] private ZoneSelectionUI customZoneSelectionUI;

    public void OnContinueClicked()
    {
        if (!GameManager.HasSaveData())
        {
            Debug.Log("[MainMenuController] No save file found to continue.");
            return;
        }

        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance.LoadGame();

        if (continueOpensZoneSelection)
        {
            if (customZoneSelectionUI != null)
            {
                customZoneSelectionUI.OpenZoneSelection();
            }
            else if (ZoneSelectionUI.Instance != null)
            {
                ZoneSelectionUI.Instance.OpenZoneSelection();
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenZoneSelection();
            }
            else
            {
                var prefab = Resources.Load<GameObject>("UI/ZoneSelectionUI")
                          ?? Resources.Load<GameObject>("UI/ZoneSelectionCanvas");
                if (prefab != null)
                {
                    var spawned = Instantiate(prefab);
                    var ui = spawned.GetComponent<ZoneSelectionUI>() ?? spawned.GetComponentInChildren<ZoneSelectionUI>();
                    if (ui != null)
                    {
                        ui.OpenZoneSelection();
                        return;
                    }
                }

                var zsGO = new GameObject("ZoneSelectionUI");
                var zs = zsGO.AddComponent<ZoneSelectionUI>();
                zs.OpenZoneSelection();
            }
            return;
        }

        int savedZone = PlayerPrefs.GetInt("Save_CurrentZone", 0);
        string zoneScene = savedZone switch
        {
            0 => "SunlightZone",
            1 => "TwilightZone",
            2 => "MidnightZone",
            3 => "AbyssZone",
            4 => "HadalZone",
            _ => startingZoneSceneName
        };

        SceneManager.LoadScene(zoneScene);

        // Apply saved position after the zone scene finishes loading
        SceneManager.sceneLoaded += OnZoneSceneLoaded;
    }

    private void OnZoneSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnZoneSceneLoaded;
        StartCoroutine(ApplyPositionNextFrame());
    }

    private IEnumerator ApplyPositionNextFrame()
    {
        // Wait one frame so all Awake/Start calls finish
        yield return null;
        GameManager.Instance?.ApplySavedPosition();
    }

    private void StartFreshNewGame()
    {
        GameManager.DeleteSaveData();
        GameManager.Instance?.ResetState();
        GameManager.SetTutorialCompleted(false);
        SceneManager.LoadScene(startingZoneSceneName);
    }

    public void OnSettingsClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (PauseMenuUI.Instance != null)
        {
            PauseMenuUI.Instance.OpenSettingsFromMainMenu();
        }
        else
        {
            var pauseGO = new GameObject("PauseMenuUI");
            var pm = pauseGO.AddComponent<PauseMenuUI>();
            pm.OpenSettingsFromMainMenu();
        }
    }

    public void OnQuitClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        Debug.Log("[MainMenuController] Quit Game Requested.");
        Application.Quit();
    }

    // -----------------------------------------------------------------------
    // New Game & Overwrite Confirmation
    // -----------------------------------------------------------------------

    private void ShowOverwriteConfirmation()
    {
        if (customConfirmPopupRoot != null)
        {
            customConfirmPopupRoot.SetActive(true);
            return;
        }

        if (_proceduralConfirmModal == null)
        {
            BuildProceduralConfirmModal();
        }

        if (_proceduralConfirmModal != null)
        {
            _proceduralConfirmModal.SetActive(true);
        }
    }

    public void OnConfirmNewGame()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customConfirmPopupRoot != null) customConfirmPopupRoot.SetActive(false);
        if (_proceduralConfirmModal != null) _proceduralConfirmModal.SetActive(false);

        StartFreshNewGame();
    }

    public void OnCancelNewGame()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customConfirmPopupRoot != null) customConfirmPopupRoot.SetActive(false);
        if (_proceduralConfirmModal != null) _proceduralConfirmModal.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Procedural Confirmation Modal Fallback (Non-overlapping 36pt layout)
    // -----------------------------------------------------------------------

    private void BuildProceduralConfirmModal()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        // Dark background overlay
        _proceduralConfirmModal = new GameObject("OverwriteConfirmModal", typeof(RectTransform), typeof(Image));
        _proceduralConfirmModal.transform.SetParent(canvas.transform, false);
        _proceduralConfirmModal.transform.SetAsLastSibling();

        var bgRect = _proceduralConfirmModal.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        _proceduralConfirmModal.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

        // Dialog Card
        var cardGO = new GameObject("DialogCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(_proceduralConfirmModal.transform, false);
        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot     = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(680f, 440f);
        cardGO.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.15f, 0.98f);

        // Title (Top positioned with zero overlap)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(620f, 54f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTMP.font = font;
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(1f, 0.35f, 0.35f);
        titleTMP.text = "⚠️ OVERWRITE SAVE DATA?";

        // Description Lore (Center positioned with zero overlap)
        var descGO = new GameObject("Description", typeof(RectTransform));
        descGO.transform.SetParent(cardGO.transform, false);
        var descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.5f, 0.5f);
        descRect.anchorMax = new Vector2(0.5f, 0.5f);
        descRect.pivot     = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, 10f);
        descRect.sizeDelta = new Vector2(600f, 180f);
        var descTMP = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) descTMP.font = font;
        descTMP.fontSize = 24;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.lineSpacing = 10f;
        descTMP.color = new Color(0.9f, 0.92f, 0.95f);
        descTMP.text = "An existing expedition save file was found.\n\nStarting a new game will <color=#ff5555><b>permanently delete</b></color> your saved RDP, upgrades, and research progress.\n\nDo you wish to proceed?";

        // Buttons Container (Bottom positioned with zero overlap)
        var btnContainer = new GameObject("BtnContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnContainer.transform.SetParent(cardGO.transform, false);
        var bcRect = btnContainer.GetComponent<RectTransform>();
        bcRect.anchorMin = new Vector2(0.5f, 0f);
        bcRect.anchorMax = new Vector2(0.5f, 0f);
        bcRect.pivot     = new Vector2(0.5f, 0f);
        bcRect.anchoredPosition = new Vector2(0f, 30f);
        bcRect.sizeDelta = new Vector2(560f, 60f);

        var hlg = btnContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 24f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Yes Button (Delete & Start New Game)
        CreateDialogButton("ConfirmBtn", "START NEW GAME", new Color(0.75f, 0.18f, 0.18f), OnConfirmNewGame, btnContainer.transform, font);

        // Cancel Button (Keep Save)
        CreateDialogButton("CancelBtn", "CANCEL", new Color(0.15f, 0.35f, 0.50f), OnCancelNewGame, btnContainer.transform, font);
    }

    private void CreateDialogButton(string name, string label, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var r = btnGO.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(250f, 54f);

        btnGO.GetComponent<Image>().color = color;
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
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = label;
    }

    // -----------------------------------------------------------------------
    // Debug & Context Menu Helpers
    // -----------------------------------------------------------------------

    [ContextMenu("Delete Save Data (Debug)")]
    public void DebugDeleteSave()
    {
        GameManager.DeleteSaveData();
        RefreshMenuButtons();
        Debug.Log("[MainMenuController] Save data deleted for testing.");
    }
}
