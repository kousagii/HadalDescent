using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the step-by-step interactive tutorial workflow (Figures 3, 4, 5, 6, 7).
///
/// Features:
///   - Initial "Start Tutorial?" prompt upon starting a New Game.
///   - 8 sequential tutorial modules:
///       0. Mission Overview
///       1. HUD Orientation (Depth, RDP, Sonar, Bestiary, Objectives)
///       2. Submarine Navigation (Look Drag, Joystick movement)
///       3. Sonar System (Sonar blips for species, debris, hazards)
///       4. Species Scanning (Lock-on, Capture & Focus / Reconstruction minigames, RDP)
///       5. Bestiary Encyclopedia (3D models, facts, completion tracking)
///       6. Environmental Cleanup (Robotic claw, bin sorting)
///       7. Hazard Encounter (3-lane dodging, data pods)
///       8. Submarine Upgrades (Hull pressure levels, shop progression)
///   - "Next", "Skip", and "Replay" options.
///   - Awards +100 Bonus RDP on completion and saves tutorial state.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [System.Serializable]
    public class TutorialStep
    {
        public string stepTitle;
        [TextArea(3, 8)]
        public string stepDescription;
        public string objectivePrompt;
    }

    [Header("Custom UI Bindings (Optional)")]
    [SerializeField] private GameObject customTutorialRoot;
    [SerializeField] private TMP_Text   customStepNumberText;
    [SerializeField] private TMP_Text   customTitleText;
    [SerializeField] private TMP_Text   customDescText;
    [SerializeField] private TMP_Text   customObjectiveText;
    [SerializeField] private Button     customNextButton;
    [SerializeField] private Button     customSkipButton;

    [Header("Custom Prompt Dialog (Optional)")]
    [SerializeField] private GameObject customPromptModal;
    [SerializeField] private Button     customPromptStartBtn;
    [SerializeField] private Button     customPromptSkipBtn;

    [Header("Custom Completion Dialog (Optional)")]
    [SerializeField] private GameObject customCompletionModal;
    [SerializeField] private Button     customCompletionProceedBtn;
    [SerializeField] private Button     customCompletionReplayBtn;

    // Procedural UI References
    private GameObject _proceduralCanvasGO;
    private GameObject _proceduralPromptModal;
    private GameObject _proceduralStepCard;
    private GameObject _proceduralCompletionModal;
    private TMP_Text   _proceduralStepNumText;
    private TMP_Text   _proceduralTitleText;
    private TMP_Text   _proceduralDescText;
    private TMP_Text   _proceduralObjectiveText;
    private Button     _proceduralNextBtn;
    private TMP_Text   _proceduralNextBtnText;

    private int _currentStepIndex = 0;
    private bool _isTutorialActive = false;

    private readonly TutorialStep[] _steps = new TutorialStep[]
    {
        new TutorialStep
        {
            stepTitle = "EXPEDITION MISSION OVERVIEW",
            stepDescription = "Welcome, Marine Researcher!\n\nYou have been deployed to the Philippine Sea aboard a state-of-the-art exploration submersible.\n\nYour mission is to explore all 5 ocean depth zones, research endemic marine species, collect underwater pollutants, survive environmental hazards, and upgrade your submarine.",
            objectivePrompt = "Tap 'NEXT' to begin HUD & Controls Orientation."
        },
        new TutorialStep
        {
            stepTitle = "1. HUD & GAUGES ORIENTATION",
            stepDescription = "• <b>Top-Left HUD:</b> Depth Gauge displays your current depth in metres. The circular Sonar Map tracks nearby entities in real time.\n• <b>Top-Right HUD:</b> RDP Counter tracks earned research currency. The Shop and Bestiary buttons manage your upgrades and biological records.\n• <b>Bottom HUD:</b> Steering Joystick, Scanner, and Interact buttons control your submersible.",
            objectivePrompt = "Observe your HUD indicators, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "2. SUBMARINE NAVIGATION",
            stepDescription = "• <b>Cockpit Look & Aim:</b> Drag anywhere across the exploration viewport to rotate your camera yaw and pitch.\n• <b>Propulsion:</b> Use the left joystick (or WASD / Arrow keys on keyboard) to steer forward, backward, left, and right.\n• Moving forward while aiming up or down controls your vertical ascent and descent.",
            objectivePrompt = "Practice looking and steering, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "3. SONAR SYSTEM & MAPPING",
            stepDescription = "• The Sonar Map in the top-left continuously emits acoustic pulses to detect surroundings:\n  - <color=#00e5ff><b>Cyan Markers:</b></color> Marine species swimming nearby.\n  - <color=#ffd700><b>Yellow Markers:</b></color> Collectible environmental debris.\n  - <color=#ff4444><b>Red Markers:</b></color> Dangerous thermal vent plumes & hazard zones.\n• Upgrading Sonar in the Shop extends detection radius and adds elevation indicators.",
            objectivePrompt = "Check your Sonar Map for nearby blips, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "4. SPECIES ENCOUNTER & SCANNING",
            stepDescription = "• When approaching marine life, center the creature within your HUD <b>Scan Reticle</b>.\n• When locked on, tap <b>SCAN</b> (or press 'F') to initiate research.\n• Complete the scanning mini-game (keep target inside the focus zone or solve the reconstruction puzzle) to document the species and earn <b>Research Data Points (RDP)</b>!",
            objectivePrompt = "Aim at a species and practice scan targeting, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "5. BESTIARY MARINE ENCYCLOPEDIA",
            stepDescription = "• Tap the <b>Bestiary</b> button (top-right book icon) at any time to open your field research encyclopedia.\n• Inspect interactive 3D models, scientific taxonomy, ecological roles, and fun facts for every discovered creature.\n• Undiscovered organisms remain as silhouettes with habitat clues to guide your search.",
            objectivePrompt = "Review the Bestiary catalog, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "6. ENVIRONMENTAL DEBRIS CLEANUP",
            stepDescription = "• Throughout the seabed, you will encounter marine debris clusters that threaten ocean ecosystems.\n• Approach debris and tap <b>INTERACT</b> (or press 'E') to deploy the robotic collection claw.\n• Classify and sort retrieved trash into <b>Plastics, Metals, and Hazardous Waste</b> bins to earn bonus RDP and increase species spawn rates!",
            objectivePrompt = "Learn debris retrieval mechanics, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "7. HAZARD ENCOUNTER & SURVIVAL",
            stepDescription = "• High-velocity hydrothermal plumes and rockfalls occasionally trigger an emergency <b>Hazard Dodge</b> sequence.\n• The camera transitions to a 3-lane top-down view. Swipe left/right to dodge incoming obstacles and collect bonus data pods!\n• Surviving awards high-yield survey data points without damaging your hull.",
            objectivePrompt = "Prepare for hazard encounters, then tap 'NEXT'."
        },
        new TutorialStep
        {
            stepTitle = "8. SUBMARINE UPGRADE SHOP",
            stepDescription = "• Open the <b>Shop</b> (top-right wrench icon) to spend your hard-earned RDP on submarine enhancements:\n  - <b>Hull Resistance (Tiers 1-5):</b> Withstand crushing pressure to unlock deeper ocean zones!\n  - <b>Engine, Sonar, Scanner, Lights:</b> Boost speed, scan area, focus size, and underwater visibility.\n• Reaching deeper zones requires both Hull upgrades and 50% species cataloged.",
            objectivePrompt = "Review upgrade paths, then tap 'NEXT' to complete the tutorial."
        }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        bool isMenu = scene.name == "MainMenu" || scene.name == "SplashScreen" || scene.name == "ZoneSelect";
        if (isMenu)
        {
            HideAllTutorialModals();
            return;
        }

        if (scene.name == "SunlightZone" && !GameManager.IsTutorialCompleted())
        {
            StartCoroutine(PromptTutorialAfterSceneReady());
        }
    }

    public void HideAllTutorialModals()
    {
        if (customTutorialRoot != null) customTutorialRoot.SetActive(false);
        if (customPromptModal != null) customPromptModal.SetActive(false);
        if (customCompletionModal != null) customCompletionModal.SetActive(false);

        if (_proceduralPromptModal != null) _proceduralPromptModal.SetActive(false);
        if (_proceduralStepCard != null) _proceduralStepCard.SetActive(false);
        if (_proceduralCompletionModal != null) _proceduralCompletionModal.SetActive(false);
    }

    private void Start()
    {
        if (customTutorialRoot != null)
        {
            WireCustomUI();
        }

        // Check if we should prompt the tutorial (e.g. New Game in SunlightZone)
        if (!GameManager.IsTutorialCompleted() && SceneManager.GetActiveScene().name == "SunlightZone")
        {
            StartCoroutine(PromptTutorialAfterSceneReady());
        }
    }

    private IEnumerator PromptTutorialAfterSceneReady()
    {
        yield return new WaitForSeconds(0.5f);
        if (!GameManager.IsTutorialCompleted() && SceneManager.GetActiveScene().name == "SunlightZone")
        {
            ShowTutorialPrompt();
        }
    }

    // -----------------------------------------------------------------------
    // Tutorial Prompt Dialog (Figure 3)
    // -----------------------------------------------------------------------

    public void ShowTutorialPrompt()
    {
        if (customPromptModal != null)
        {
            customPromptModal.SetActive(true);
            return;
        }

        if (_proceduralPromptModal == null)
        {
            BuildProceduralUI();
        }

        if (_proceduralPromptModal != null)
        {
            _proceduralPromptModal.transform.SetAsLastSibling();
            _proceduralPromptModal.SetActive(true);
        }
    }

    public void OnPromptStartTutorial()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customPromptModal != null) customPromptModal.SetActive(false);
        if (_proceduralPromptModal != null) _proceduralPromptModal.SetActive(false);

        StartTutorial();
    }

    public void OnPromptSkipTutorial()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customPromptModal != null) customPromptModal.SetActive(false);
        if (_proceduralPromptModal != null) _proceduralPromptModal.SetActive(false);

        GameManager.SetTutorialCompleted(true);
        Debug.Log("[TutorialManager] Tutorial skipped by player — opening Zone Selection.");

        if (ZoneSelectionUI.Instance != null)
        {
            ZoneSelectionUI.Instance.OpenZoneSelection();
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenZoneSelection();
        }
    }

    // -----------------------------------------------------------------------
    // Tutorial Step Progression (Figure 4)
    // -----------------------------------------------------------------------

    public void StartTutorial()
    {
        _isTutorialActive = true;
        _currentStepIndex = 0;

        if (customTutorialRoot != null)
        {
            customTutorialRoot.SetActive(true);
        }
        else
        {
            if (_proceduralStepCard == null) BuildProceduralUI();
            if (_proceduralStepCard != null) _proceduralStepCard.SetActive(true);
        }

        DisplayCurrentStep();
    }

    private void DisplayCurrentStep()
    {
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length)
        {
            CompleteTutorial();
            return;
        }

        TutorialStep step = _steps[_currentStepIndex];
        string stepNum = $"TUTORIAL STEP {_currentStepIndex + 1} / {_steps.Length}";

        // Update Custom UI
        if (customStepNumberText != null) customStepNumberText.text = stepNum;
        if (customTitleText != null) customTitleText.text = step.stepTitle;
        if (customDescText != null) customDescText.text = step.stepDescription;
        if (customObjectiveText != null) customObjectiveText.text = step.objectivePrompt;

        // Update Procedural UI
        if (_proceduralStepNumText != null) _proceduralStepNumText.text = stepNum;
        if (_proceduralTitleText != null) _proceduralTitleText.text = step.stepTitle;
        if (_proceduralDescText != null) _proceduralDescText.text = step.stepDescription;
        if (_proceduralObjectiveText != null) _proceduralObjectiveText.text = step.objectivePrompt;

        string nextLabel = (_currentStepIndex == _steps.Length - 1) ? "FINISH TUTORIAL" : "NEXT STEP";
        if (_proceduralNextBtnText != null) _proceduralNextBtnText.text = nextLabel;
    }

    public void OnNextStepClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        _currentStepIndex++;
        if (_currentStepIndex < _steps.Length)
        {
            DisplayCurrentStep();
        }
        else
        {
            CompleteTutorial();
        }
    }

    public void OnSkipTutorialClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customTutorialRoot != null) customTutorialRoot.SetActive(false);
        if (_proceduralStepCard != null) _proceduralStepCard.SetActive(false);

        _isTutorialActive = false;
        GameManager.SetTutorialCompleted(true);
        Debug.Log("[TutorialManager] Tutorial skipped — opening Zone Selection.");

        if (ZoneSelectionUI.Instance != null)
        {
            ZoneSelectionUI.Instance.OpenZoneSelection();
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenZoneSelection();
        }
    }

    // -----------------------------------------------------------------------
    // Tutorial Completion & Rewards
    // -----------------------------------------------------------------------

    private void CompleteTutorial()
    {
        _isTutorialActive = false;

        if (customTutorialRoot != null) customTutorialRoot.SetActive(false);
        if (_proceduralStepCard != null) _proceduralStepCard.SetActive(false);

        // Mark completed and award bonus RDP
        GameManager.SetTutorialCompleted(true);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddRDP(100);
            GameManager.Instance.SaveGame();
        }

        // Show Completion Modal
        if (customCompletionModal != null)
        {
            customCompletionModal.SetActive(true);
            return;
        }

        if (_proceduralCompletionModal != null)
        {
            _proceduralCompletionModal.SetActive(true);
        }
    }

    public void OnCompletionProceedClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customCompletionModal != null) customCompletionModal.SetActive(false);
        if (_proceduralCompletionModal != null) _proceduralCompletionModal.SetActive(false);

        Debug.Log("[TutorialManager] Tutorial completed — opening Zone Selection.");

        if (ZoneSelectionUI.Instance != null)
        {
            ZoneSelectionUI.Instance.OpenZoneSelection();
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenZoneSelection();
        }
    }

    public void OnCompletionReplayClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        if (customCompletionModal != null) customCompletionModal.SetActive(false);
        if (_proceduralCompletionModal != null) _proceduralCompletionModal.SetActive(false);

        StartTutorial();
    }

    private void WireCustomUI()
    {
        if (customNextButton != null)
        {
            customNextButton.onClick.RemoveListener(OnNextStepClicked);
            customNextButton.onClick.AddListener(OnNextStepClicked);
        }

        if (customSkipButton != null)
        {
            customSkipButton.onClick.RemoveListener(OnSkipTutorialClicked);
            customSkipButton.onClick.AddListener(OnSkipTutorialClicked);
        }

        if (customPromptStartBtn != null)
        {
            customPromptStartBtn.onClick.RemoveListener(OnPromptStartTutorial);
            customPromptStartBtn.onClick.AddListener(OnPromptStartTutorial);
        }

        if (customPromptSkipBtn != null)
        {
            customPromptSkipBtn.onClick.RemoveListener(OnPromptSkipTutorial);
            customPromptSkipBtn.onClick.AddListener(OnPromptSkipTutorial);
        }

        if (customCompletionProceedBtn != null)
        {
            customCompletionProceedBtn.onClick.RemoveListener(OnCompletionProceedClicked);
            customCompletionProceedBtn.onClick.AddListener(OnCompletionProceedClicked);
        }

        if (customCompletionReplayBtn != null)
        {
            customCompletionReplayBtn.onClick.RemoveListener(OnCompletionReplayClicked);
            customCompletionReplayBtn.onClick.AddListener(OnCompletionReplayClicked);
        }
    }

    // -----------------------------------------------------------------------
    // Procedural UI Construction (Aloha SDF Dark Sci-Fi Design, Min 36px)
    // -----------------------------------------------------------------------

    private void BuildProceduralUI()
    {
        if (_proceduralCanvasGO != null) return;

        var font = UIThemeManager.AlohaFont;

        _proceduralCanvasGO = new GameObject("Tutorial_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_proceduralCanvasGO);
        var canvas = _proceduralCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9997;

        var scaler = _proceduralCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ── 1. Initial Prompt Modal ──────────────────────────────────────
        BuildProceduralPromptModal(_proceduralCanvasGO.transform, font);

        // ── 2. Active Tutorial Step Card ─────────────────────────────────
        BuildProceduralStepCard(_proceduralCanvasGO.transform, font);

        // ── 3. Completion Modal ──────────────────────────────────────────
        BuildProceduralCompletionModal(_proceduralCanvasGO.transform, font);
    }

    private void BuildProceduralPromptModal(Transform parent, TMP_FontAsset font)
    {
        _proceduralPromptModal = new GameObject("TutorialPromptModal", typeof(RectTransform), typeof(Image));
        _proceduralPromptModal.transform.SetParent(parent, false);

        var bgR = _proceduralPromptModal.GetComponent<RectTransform>();
        bgR.anchorMin = Vector2.zero;
        bgR.anchorMax = Vector2.one;
        bgR.sizeDelta = Vector2.zero;
        _proceduralPromptModal.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

        var cardGO = new GameObject("PromptCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(_proceduralPromptModal.transform, false);
        var cR = cardGO.GetComponent<RectTransform>();
        cR.anchorMin = new Vector2(0.5f, 0.5f);
        cR.anchorMax = new Vector2(0.5f, 0.5f);
        cR.pivot     = new Vector2(0.5f, 0.5f);
        cR.sizeDelta = new Vector2(1240f, 680f);
        cardGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Top accent line
        var topStripe = new GameObject("TopStripe", typeof(RectTransform), typeof(Image));
        topStripe.transform.SetParent(cardGO.transform, false);
        var tsR = topStripe.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0f, 1f);
        tsR.anchorMax = new Vector2(1f, 1f);
        tsR.pivot     = new Vector2(0.5f, 1f);
        tsR.sizeDelta = new Vector2(0f, 5f);
        topStripe.GetComponent<Image>().color = new Color(0.2f, 0.85f, 1f, 1f);

        // Title (Top-anchored)
        CreateText("Title", "SUBMARINE SYSTEMS TUTORIAL", new Vector2(0f, -36f), new Vector2(1160f, 50f), 36, FontStyles.Bold, new Color(0.3f, 0.9f, 1f), cardGO.transform, font, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));

        // Subtitle (Top-anchored)
        CreateText("Subtitle", "WELCOME, MARINE RESEARCHER!", new Vector2(0f, -94f), new Vector2(1160f, 44f), 32, FontStyles.Bold, Color.white, cardGO.transform, font, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));

        // Desc (Top-anchored, top-aligned)
        var descTMP = CreateText("Desc", "Would you like to complete the guided tutorial covering submarine navigation, sonar mapping, species scanning, debris cleanup, and upgrade systems?", new Vector2(0f, -156f), new Vector2(1100f, 350f), 32, FontStyles.Normal, new Color(0.92f, 0.94f, 0.97f), cardGO.transform, font, TextAlignmentOptions.Top, new Vector2(0.5f, 1f));
        descTMP.enableAutoSizing = true;
        descTMP.fontSizeMin = 26f;
        descTMP.fontSizeMax = 32f;
        descTMP.lineSpacing = 6f;
        descTMP.paragraphSpacing = 10f;

        // Button Row (Bottom-anchored)
        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(cardGO.transform, false);
        var brR = btnRow.GetComponent<RectTransform>();
        brR.anchorMin = new Vector2(0.5f, 0f);
        brR.anchorMax = new Vector2(0.5f, 0f);
        brR.pivot     = new Vector2(0.5f, 0f);
        brR.anchoredPosition = new Vector2(0f, 36f);
        brR.sizeDelta = new Vector2(1100f, 84f);

        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 35f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // BOTH BUTTONS EXACT SAME SIZE (420x76), 1 LINE ONLY
        CreateButton("StartBtn", "START TUTORIAL", Vector2.zero, new Vector2(420f, 76f), new Color(0.08f, 0.65f, 0.55f), OnPromptStartTutorial, btnRow.transform, font, 32);
        CreateButton("SkipBtn", "SKIP TO DIVE", Vector2.zero, new Vector2(420f, 76f), new Color(0.18f, 0.25f, 0.35f), OnPromptSkipTutorial, btnRow.transform, font, 32);

        _proceduralPromptModal.SetActive(false);
    }

    private void BuildProceduralStepCard(Transform parent, TMP_FontAsset font)
    {
        _proceduralStepCard = new GameObject("TutorialStepCard", typeof(RectTransform), typeof(Image));
        _proceduralStepCard.transform.SetParent(parent, false);

        var cR = _proceduralStepCard.GetComponent<RectTransform>();
        cR.anchorMin = new Vector2(0.5f, 0.5f);
        cR.anchorMax = new Vector2(0.5f, 0.5f);
        cR.pivot     = new Vector2(0.5f, 0.5f);
        cR.anchoredPosition = Vector2.zero;
        cR.sizeDelta = new Vector2(1360f, 780f);
        _proceduralStepCard.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.14f, 0.96f);

        // Step Number Banner (Top-anchored)
        var stepNumGO = new GameObject("StepNum", typeof(RectTransform));
        stepNumGO.transform.SetParent(_proceduralStepCard.transform, false);
        var snR = stepNumGO.GetComponent<RectTransform>();
        snR.anchorMin = new Vector2(0.5f, 1f);
        snR.anchorMax = new Vector2(0.5f, 1f);
        snR.pivot     = new Vector2(0.5f, 1f);
        snR.anchoredPosition = new Vector2(0f, -22f);
        snR.sizeDelta = new Vector2(1260f, 38f);
        _proceduralStepNumText = stepNumGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralStepNumText.font = font;
        _proceduralStepNumText.fontSize = 32;
        _proceduralStepNumText.fontStyle = FontStyles.Bold;
        _proceduralStepNumText.alignment = TextAlignmentOptions.Center;
        _proceduralStepNumText.color = new Color(0.3f, 0.85f, 1f);

        // Title (Top-anchored)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(_proceduralStepCard.transform, false);
        var tR = titleGO.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.5f, 1f);
        tR.anchorMax = new Vector2(0.5f, 1f);
        tR.pivot     = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -64f);
        tR.sizeDelta = new Vector2(1260f, 46f);
        _proceduralTitleText = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralTitleText.font = font;
        _proceduralTitleText.fontSize = 34;
        _proceduralTitleText.fontStyle = FontStyles.Bold;
        _proceduralTitleText.alignment = TextAlignmentOptions.Center;
        _proceduralTitleText.color = Color.white;

        // Description (Top-anchored, generous 410px height for multi-paragraph guidance, auto-fitting to prevent overlap)
        var descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(_proceduralStepCard.transform, false);
        var dR = descGO.GetComponent<RectTransform>();
        dR.anchorMin = new Vector2(0.5f, 1f);
        dR.anchorMax = new Vector2(0.5f, 1f);
        dR.pivot     = new Vector2(0.5f, 1f);
        dR.anchoredPosition = new Vector2(0f, -116f);
        dR.sizeDelta = new Vector2(1260f, 410f);
        _proceduralDescText = descGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralDescText.font = font;
        _proceduralDescText.fontSize = 32;
        _proceduralDescText.lineSpacing = 4f;
        _proceduralDescText.paragraphSpacing = 8f;
        _proceduralDescText.enableAutoSizing = true;
        _proceduralDescText.fontSizeMin = 26f;
        _proceduralDescText.fontSizeMax = 32f;
        _proceduralDescText.alignment = TextAlignmentOptions.Top;
        _proceduralDescText.color = new Color(0.9f, 0.92f, 0.95f);

        // Objective Prompt (Top-anchored strictly below description with zero overlap)
        var objGO = new GameObject("Objective", typeof(RectTransform));
        objGO.transform.SetParent(_proceduralStepCard.transform, false);
        var oR = objGO.GetComponent<RectTransform>();
        oR.anchorMin = new Vector2(0.5f, 1f);
        oR.anchorMax = new Vector2(0.5f, 1f);
        oR.pivot     = new Vector2(0.5f, 1f);
        oR.anchoredPosition = new Vector2(0f, -540f);
        oR.sizeDelta = new Vector2(1260f, 52f);
        _proceduralObjectiveText = objGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _proceduralObjectiveText.font = font;
        _proceduralObjectiveText.fontSize = 30;
        _proceduralObjectiveText.fontStyle = FontStyles.Italic;
        _proceduralObjectiveText.alignment = TextAlignmentOptions.Center;
        _proceduralObjectiveText.color = new Color(0.2f, 1f, 0.7f);

        // Action Buttons Row (Bottom-anchored)
        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(_proceduralStepCard.transform, false);
        var brR = btnRow.GetComponent<RectTransform>();
        brR.anchorMin = new Vector2(0.5f, 0f);
        brR.anchorMax = new Vector2(0.5f, 0f);
        brR.pivot     = new Vector2(0.5f, 0f);
        brR.anchoredPosition = new Vector2(0f, 32f);
        brR.sizeDelta = new Vector2(1260f, 84f);

        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 35f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Next Button and Skip Button - EXACT SAME SIZE (400x76), 1 LINE ONLY
        _proceduralNextBtn = CreateButton("NextBtn", "NEXT STEP", Vector2.zero, new Vector2(400f, 76f), new Color(0.08f, 0.65f, 0.55f), OnNextStepClicked, btnRow.transform, font, 32, out _proceduralNextBtnText);

        CreateButton("SkipBtn", "SKIP TUTORIAL", Vector2.zero, new Vector2(400f, 76f), new Color(0.18f, 0.22f, 0.28f), OnSkipTutorialClicked, btnRow.transform, font, 32);

        _proceduralStepCard.SetActive(false);
    }

    private void BuildProceduralCompletionModal(Transform parent, TMP_FontAsset font)
    {
        _proceduralCompletionModal = new GameObject("TutorialCompletionModal", typeof(RectTransform), typeof(Image));
        _proceduralCompletionModal.transform.SetParent(parent, false);

        var bgR = _proceduralCompletionModal.GetComponent<RectTransform>();
        bgR.anchorMin = Vector2.zero;
        bgR.anchorMax = Vector2.one;
        bgR.sizeDelta = Vector2.zero;
        _proceduralCompletionModal.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

        var cardGO = new GameObject("CompletionCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(_proceduralCompletionModal.transform, false);
        var cR = cardGO.GetComponent<RectTransform>();
        cR.anchorMin = new Vector2(0.5f, 0.5f);
        cR.anchorMax = new Vector2(0.5f, 0.5f);
        cR.pivot     = new Vector2(0.5f, 0.5f);
        cR.sizeDelta = new Vector2(1240f, 720f);
        cardGO.GetComponent<Image>().color = new Color(0.04f, 0.09f, 0.16f, 0.98f);

        // Top accent line
        var topStripe = new GameObject("TopStripe", typeof(RectTransform), typeof(Image));
        topStripe.transform.SetParent(cardGO.transform, false);
        var tsR = topStripe.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0f, 1f);
        tsR.anchorMax = new Vector2(1f, 1f);
        tsR.pivot     = new Vector2(0.5f, 1f);
        tsR.sizeDelta = new Vector2(0f, 5f);
        topStripe.GetComponent<Image>().color = new Color(0.1f, 0.95f, 0.7f, 1f);

        // Title (Top-anchored)
        CreateText("Title", "TUTORIAL COMPLETED!", new Vector2(0f, -36f), new Vector2(1160f, 52f), 40, FontStyles.Bold, new Color(0.2f, 1f, 0.7f), cardGO.transform, font, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));

        // Subtitle (Top-anchored)
        CreateText("Subtitle", "CONGRATULATIONS, RESEARCHER!", new Vector2(0f, -96f), new Vector2(1160f, 44f), 32, FontStyles.Bold, Color.white, cardGO.transform, font, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));

        // Desc (Top-anchored, top-aligned)
        string completionDesc = "You have completed all submarine operational training.\n\n<color=#00e5ff><b>+100 Research Data Points (RDP)</b></color>\nhave been added to your submarine research fund.\n\nYou are now ready for free deep-sea exploration!";
        var descTMP = CreateText("Desc", completionDesc, new Vector2(0f, -158f), new Vector2(1120f, 380f), 32, FontStyles.Normal, new Color(0.92f, 0.94f, 0.97f), cardGO.transform, font, TextAlignmentOptions.Top, new Vector2(0.5f, 1f));
        descTMP.enableAutoSizing = true;
        descTMP.fontSizeMin = 26f;
        descTMP.fontSizeMax = 32f;
        descTMP.lineSpacing = 6f;
        descTMP.paragraphSpacing = 12f;

        // Button Row (Bottom-anchored)
        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(cardGO.transform, false);
        var brR = btnRow.GetComponent<RectTransform>();
        brR.anchorMin = new Vector2(0.5f, 0f);
        brR.anchorMax = new Vector2(0.5f, 0f);
        brR.pivot     = new Vector2(0.5f, 0f);
        brR.anchoredPosition = new Vector2(0f, 36f);
        brR.sizeDelta = new Vector2(1120f, 84f);

        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 35f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // BOTH BUTTONS EXACT SAME SIZE (440x76), 1 LINE ONLY
        CreateButton("ProceedBtn", "START EXPEDITION", Vector2.zero, new Vector2(440f, 76f), new Color(0.08f, 0.65f, 0.55f), OnCompletionProceedClicked, btnRow.transform, font, 32);
        CreateButton("ReplayBtn", "REPLAY TUTORIAL", Vector2.zero, new Vector2(440f, 76f), new Color(0.18f, 0.25f, 0.35f), OnCompletionReplayClicked, btnRow.transform, font, 32);

        _proceduralCompletionModal.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // UI Helpers
    // -----------------------------------------------------------------------

    private TextMeshProUGUI CreateText(string name, string text, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, Transform parent, TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Center, Vector2? anchor = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        if (anchor.HasValue)
        {
            r.anchorMin = anchor.Value;
            r.anchorMax = anchor.Value;
            r.pivot     = anchor.Value;
        }
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = Mathf.Max(30f, fontSize);
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.text = text;
        return tmp;
    }

    private Button CreateButton(string name, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, float fontSize = 32f)
    {
        return CreateButton(name, label, pos, size, color, action, parent, font, fontSize, out _);
    }

    private Button CreateButton(string name, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action, Transform parent, TMP_FontAsset font, float fontSize, out TMP_Text labelText)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var r = btnGO.GetComponent<RectTransform>();
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        btnGO.GetComponent<Image>().color = color;
        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(action);

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblR = lblGO.GetComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero;
        lblR.anchorMax = Vector2.one;
        lblR.offsetMin = new Vector2(16f, 4f);
        lblR.offsetMax = new Vector2(-16f, -4f);

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = label;

        // Enforce ONE LINE ONLY:
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 22f;
        tmp.fontSizeMax = Mathf.Max(28f, fontSize);

        labelText = tmp;
        return btn;
    }
}
