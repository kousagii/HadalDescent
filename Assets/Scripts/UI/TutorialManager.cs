using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the non-blocking event-driven interactive tutorial sequence.
/// Implements HadalDescent_Interactive_Tutorial_Story_Plan.md:
///   - Opening cinematic (~15s) via CinematicSequencer.cs
///   - Step 0: Mission Overview
///   - Step 1: HUD Orientation (sequential 5 arrow pointers)
///   - Step 2: Submarine Navigation (sustained joystick input >= 2.0s)
///   - Step 3: Sonar System & Color Radar Identification (Sonar legend + cyan blip proximity)
///   - Step 4A: Species Encounter 1 (Minigame 1 Capture & Focus slideshow + scan)
///   - Step 4B: Species Encounter 2 (Minigame 2 Reconstruction Scan slideshow + scan)
///   - Step 5: Bestiary Marine Encyclopedia (3D Inspection & details review)
///   - Step 6: Environmental Debris Cleanup (Seabed-anchored debris cluster + Minigame 3 slideshow + sort)
///   - Step 7: Hazard Encounter: "Dangers Ahead" (Automated trigger Minigame 4 + slideshow + evasion)
///   - Step 8: Submarine Upgrade Shop (Open Shop)
///   - Graduation: +100 RDP Bonus & completion save
///   - 4-Tier Non-compliance timers (20s nudge, 40s assist, 60s [SKIP STEP])
///   - Three [SKIP TUTORIAL] touchpoints with confirmation modal
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialPhase
    {
        Inactive,
        Cinematic,
        Step0_MissionOverview,
        Step1_HUDOrientation,
        Step2_Navigation,
        Step3_SonarSystem,
        Step4A_ScanEncounter1,
        Step4B_ScanEncounter2,
        Step5_BestiaryReview,
        Step6_DebrisCleanup,
        Step7_HazardEncounter,
        Step8_UpgradeShop,
        Graduation,
        Completed
    }

    [Header("Optional Custom UI References (Backwards Compatibility)")]
    [SerializeField] private GameObject customTutorialRoot;
    [SerializeField] private TMP_Text   customStepNumberText;
    [SerializeField] private TMP_Text   customTitleText;
    [SerializeField] private TMP_Text   customDescText;
    [SerializeField] private TMP_Text   customObjectiveText;
    [SerializeField] private Button     customNextButton;
    [SerializeField] private Button     customSkipButton;

    [Header("Confirmation Modal (Optional)")]
    [SerializeField] private GameObject customPromptModal;
    [SerializeField] private Button     customPromptStartBtn;
    [SerializeField] private Button     customPromptSkipBtn;

    [Header("Graduation / Completion Modal (Optional)")]
    [SerializeField] private GameObject customCompletionModal;
    [SerializeField] private Button     customCompletionProceedBtn;
    [SerializeField] private Button     customCompletionReplayBtn;

    // Runtime state
    private TutorialPhase _currentPhase = TutorialPhase.Inactive;
    public TutorialPhase CurrentPhase => _currentPhase;
    public bool IsTutorialActive => _currentPhase != TutorialPhase.Inactive && _currentPhase != TutorialPhase.Completed;
    public bool IsTutorialModalOpen => (_skipConfirmModal != null && _skipConfirmModal.activeSelf) ||
                                       (customPromptModal != null && customPromptModal.activeSelf) ||
                                       (customCompletionModal != null && customCompletionModal.activeSelf);
    public bool IsTutorialOpen   => IsTutorialModalOpen;

    // Non-compliance timers
    private float _phaseTimer = 0f;
    private bool  _tier1Nudged = false;
    private bool  _tier2Assisted = false;
    private bool  _tier3SkipStepAvailable = false;
    private bool  _species1VisualConfirmed = false;
    private bool  _step2NavigationHandled = false;
    private bool  _bestiaryOpenedInStep5 = false;
    private bool  _step6DebrisHandled = false;
    private bool  _step6DebrisCleanedTriggered = false;
    private bool  _step7CompletedHandled = false;

    // Submarine movement tracking (Step 2)
    private float _sustainedMoveTimer = 0f;

    // Targeted entities
    private SpeciesAI     _tutorialSpecies1;
    private SpeciesAI     _tutorialSpecies2;
    private DebrisCluster _tutorialDebris;
    private readonly List<SpeciesAI> _cachedSceneSpecies = new List<SpeciesAI>();
    private readonly List<DebrisCluster> _cachedSceneDebris = new List<DebrisCluster>();

    // Procedural Skip Confirmation Modal
    private GameObject _skipConfirmModal;
    private Canvas     _tutorialCanvas;

    private Coroutine _step1Routine;

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
        ScannerSystem.OnSpeciesScanned += OnSpeciesScannedHandler;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ScannerSystem.OnSpeciesScanned -= OnSpeciesScannedHandler;
    }

    private void Start()
    {
        EnsureCanvas();
        WireRadioCallbacks();

        if (SceneManager.GetActiveScene().name == "SunlightZone" && !GameManager.IsTutorialCompleted())
        {
            StartCoroutine(InitTutorialRoutine());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isMenu = scene.name == "MainMenu" || scene.name == "SplashScreen" || scene.name == "ZoneSelect";
        if (isMenu)
        {
            AbortTutorial();
            return;
        }

        if (scene.name == "SunlightZone" && !GameManager.IsTutorialCompleted())
        {
            StartCoroutine(InitTutorialRoutine());
        }
    }

    private void EnsureCanvas()
    {
        if (_tutorialCanvas != null) return;
        _tutorialCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
    }

    private void WireRadioCallbacks()
    {
        var radio = RadioTextboxUI.Instance;
        if (radio != null)
        {
            radio.OnSkipTutorialRequested -= ShowSkipTutorialPrompt;
            radio.OnSkipTutorialRequested += ShowSkipTutorialPrompt;

            radio.OnSkipStepRequested -= OnSkipStepClicked;
            radio.OnSkipStepRequested += OnSkipStepClicked;
        }
    }

    private IEnumerator InitTutorialRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        if (GameManager.IsTutorialCompleted()) yield break;

        HideAllTutorialEntities();

        // Hide HUD skip button during opening cinematic (cinematic has its own middle-top skip button)
        UIManager.Instance?.SetHUDSkipTutorialVisible(false);

        // Check if CinematicSequencer is present or add it
        var sequencer = CinematicSequencer.Instance ?? FindFirstObjectByType<CinematicSequencer>(FindObjectsInactive.Include);
        if (sequencer == null)
        {
            var go = new GameObject("CinematicSequencer");
            sequencer = go.AddComponent<CinematicSequencer>();
        }

        _currentPhase = TutorialPhase.Cinematic;
        sequencer.PlayOpeningCinematic(OnCinematicFinished);
    }

    private void OnCinematicFinished()
    {
        if (_currentPhase != TutorialPhase.Cinematic) return;
        UIManager.Instance?.SetHUDSkipTutorialVisible(true, ShowSkipTutorialPrompt);
        StartStep0();
    }

    // -----------------------------------------------------------------------
    // STEP 0 — Mission Overview
    // -----------------------------------------------------------------------

    public void StartTutorial() => StartStep0();

    public void StartStep0()
    {
        _currentPhase = TutorialPhase.Step0_MissionOverview;
        ResetPhaseTimers();

        HideAllTutorialEntities();
        UIManager.Instance?.SetHUDSkipTutorialVisible(true, ShowSkipTutorialPrompt);
        WireRadioCallbacks();

        string speech = "Welcome aboard, Researcher. This is Dr. Santos at the Benham Rise Marine Station. "
                      + "Submarine diagnostics are green. You are currently deployed in the Sunlight Zone. "
                      + "Your mission: document marine species in the Philippine Sea to build our Marine Biodiversity Database. "
                      + "Let's begin by testing your instruments.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Review mission parameters",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step0_MissionOverview)
                {
                    StartStep1();
                }
            },
            showSkipStep: false,
            autoDismissDelay: 2.0f
        );
    }

    // -----------------------------------------------------------------------
    // STEP 1 — HUD Orientation (5 Sequential Pointers)
    // -----------------------------------------------------------------------

    public void StartStep1()
    {
        _currentPhase = TutorialPhase.Step1_HUDOrientation;
        ResetPhaseTimers();

        HideAllTutorialEntities();

        string speech = "Your submarine's instrument panel is online. Let me walk you through your displays.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Familiarize yourself with HUD instruments",
            onComplete: null,
            showSkipStep: false
        );

        if (_step1Routine != null) StopCoroutine(_step1Routine);
        _step1Routine = StartCoroutine(Step1PointersRoutine());
    }

    private IEnumerator Step1PointersRoutine()
    {
        var arrow = TutorialArrowUI.Instance ?? FindFirstObjectByType<TutorialArrowUI>(FindObjectsInactive.Include);
        var ui = UIManager.Instance;

        // 1. Depth Gauge (top-left)
        if (ui != null && arrow != null)
        {
            var r = ui.GetDepthGaugeRect();
            if (r != null) arrow.PointAtHUD(r, "Current Depth (metres)", TutorialArrowUI.ArrowDirection.Left);
        }
        yield return new WaitForSeconds(1.6f);

        // 2. Sonar Map (top-left circle)
        if (ui != null && arrow != null)
        {
            var r = ui.GetSonarMapRect();
            if (r != null) arrow.PointAtHUD(r, "Sonar Radar", TutorialArrowUI.ArrowDirection.Left);
        }
        yield return new WaitForSeconds(1.6f);

        // 3. RDP Counter (top-left cluster)
        if (ui != null && arrow != null)
        {
            var r = ui.GetRDPCounterRect();
            if (r != null) arrow.PointAtHUD(r, "Research Data Points", TutorialArrowUI.ArrowDirection.Left);
        }
        yield return new WaitForSeconds(1.6f);

        // 4. Shop Button (top-right wrench, pointing Up from below)
        if (ui != null && arrow != null)
        {
            var r = ui.GetShopButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Submarine Upgrade Shop", TutorialArrowUI.ArrowDirection.Up);
        }
        yield return new WaitForSeconds(1.6f);

        // 5. Bestiary Button (top-right book, pointing Up from below)
        if (ui != null && arrow != null)
        {
            var r = ui.GetBestiaryButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Marine Biodiversity Bestiary", TutorialArrowUI.ArrowDirection.Up);
        }
        yield return new WaitForSeconds(1.6f);

        arrow?.Hide();

        // Finish all radio text from Step 1 before proceeding to Step 2
        while (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy)
        {
            yield return null;
        }

        if (_currentPhase == TutorialPhase.Step1_HUDOrientation)
        {
            StartStep2();
        }
    }

    // -----------------------------------------------------------------------
    // STEP 2 — Submarine Navigation & Viewport Controls
    // -----------------------------------------------------------------------

    public void StartStep2()
    {
        _currentPhase = TutorialPhase.Step2_Navigation;
        ResetPhaseTimers();
        _sustainedMoveTimer = 0f;
        _step2NavigationHandled = false;

        HideAllTutorialEntities();

        var player = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (player != null) player.enabled = true;

        var subCam = FindFirstObjectByType<SubmarineCamera>(FindObjectsInactive.Include);
        if (subCam != null) subCam.enabled = true;

        var dragZone = FindFirstObjectByType<TouchDragZone>(FindObjectsInactive.Include);
        if (dragZone != null)
        {
            dragZone.gameObject.SetActive(true);
            dragZone.enabled = true;
        }

        string speech = "Time to take the helm. Drag across the viewport to steer your camera view, "
                      + "and push the directional joystick to navigate forward.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Steer submarine with joystick for 2 seconds",
            onComplete: null,
            showSkipStep: false
        );

        var arrow = TutorialArrowUI.Instance ?? FindFirstObjectByType<TutorialArrowUI>(FindObjectsInactive.Include);
        var ui = UIManager.Instance;
        if (ui != null && arrow != null)
        {
            var stickRect = ui.GetJoystickRect();
            if (stickRect != null)
            {
                arrow.PointAtHUD(stickRect, "USE JOYSTICK TO NAVIGATE", TutorialArrowUI.ArrowDirection.Down);
            }
        }
    }

    // -----------------------------------------------------------------------
    // STEP 3 — Sonar System & Color Radar Identification
    // -----------------------------------------------------------------------

    public void StartStep3()
    {
        _currentPhase = TutorialPhase.Step3_SonarSystem;
        ResetPhaseTimers();

        TutorialArrowUI.Instance?.Hide();

        string speech = "Keep an eye on your sonar radar. Different contact signatures emit distinct frequencies: "
                      + "Cyan markers indicate marine species. Yellow markers pinpoint habitat landmarks. "
                      + "Orange markers reveal collectible debris, and Red markers highlight dangerous hazard zones. "
                      + "Steer toward the nearest cyan blip.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Track and approach a Cyan species signature on sonar",
            onComplete: null,
            showSkipStep: false
        );

        // Show Sonar Legend Card
        TutorialArrowUI.Instance?.SetSonarLegendVisible(true);

        // Point arrow to Sonar Map
        var ui = UIManager.Instance;
        if (ui != null)
        {
            var sonarRect = ui.GetSonarMapRect();
            if (sonarRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(sonarRect, "SONAR RADAR CONTACTS", TutorialArrowUI.ArrowDirection.Left);
            }
        }

        EnsureTutorialSpecies();
    }

    // -----------------------------------------------------------------------
    // STEP 4A — Species Encounter 1 (Minigame 1: Capture & Focus)
    // -----------------------------------------------------------------------

    public void StartStep4A()
    {
        // Smooth early-scan bypass: If first specimen was already scanned during Step 3, smoothly proceed to Step 4B!
        if (_tutorialSpecies1 != null && _tutorialSpecies1.Data != null && GameManager.Instance != null && GameManager.Instance.IsDiscovered(_tutorialSpecies1.Data.speciesId))
        {
            StartStep4B();
            return;
        }

        _currentPhase = TutorialPhase.Step4A_ScanEncounter1;
        ResetPhaseTimers();

        TutorialArrowUI.Instance?.SetSonarLegendVisible(false);

        string speech = "You have a creature in your sights! "
                      + "Align the target in your reticle and tap SCAN.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Align creature in reticle and complete scan",
            onComplete: null,
            showSkipStep: false
        );

        // Point to Scan button
        var ui = UIManager.Instance;
        if (ui != null)
        {
            var scanRect = ui.GetScanButtonRect();
            if (scanRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(scanRect, "TAP TO SCAN", TutorialArrowUI.ArrowDirection.Down);
            }
        }
    }

    // -----------------------------------------------------------------------
    // STEP 4B — Species Encounter 2 (Minigame 2: Reconstruction Scan)
    // -----------------------------------------------------------------------

    public void StartStep4B()
    {
        _currentPhase = TutorialPhase.Step4B_ScanEncounter2;
        ResetPhaseTimers();

        string speech = "Notice the different bio-signature on this second creature? Our automated scanner will switch "
                      + "to the alternative scanning module. Lock on and initiate scan.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Scan second creature to calibrate all scanner modes",
            onComplete: null,
            showSkipStep: false
        );

        EnsureTutorialSpecies(isSecond: true);

        var ui = UIManager.Instance;
        if (ui != null)
        {
            var scanRect = ui.GetScanButtonRect();
            if (scanRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(scanRect, "TAP TO SCAN", TutorialArrowUI.ArrowDirection.Down);
            }
        }
    }

    // -----------------------------------------------------------------------
    // STEP 5 — Bestiary Marine Encyclopedia
    // -----------------------------------------------------------------------

    public void StartStep5()
    {
        _currentPhase = TutorialPhase.Step5_BestiaryReview;
        _bestiaryOpenedInStep5 = false;
        ResetPhaseTimers();

        string speech = "Open the Bestiary to review your documented specimens. Every discovered entry features two interactive views: "
                      + "the 3D Model modal to inspect full creature anatomy, and the Detail Modal to view biological field photos, scientific classifications, "
                      + "and ecological facts. If you ever feel lost, the search clues on undiscovered entries will guide your expedition.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Open Bestiary and inspect your scanned species",
            onComplete: null,
            showSkipStep: false
        );

        var ui = UIManager.Instance;
        if (ui != null)
        {
            var bestiaryRect = ui.GetBestiaryButtonRect();
            if (bestiaryRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(bestiaryRect, "TAP TO OPEN BESTIARY", TutorialArrowUI.ArrowDirection.Right);
            }
        }
    }

    // -----------------------------------------------------------------------
    // STEP 6 — Environmental Debris Cleanup (Minigame 3)
    // -----------------------------------------------------------------------

    public void StartStep6()
    {
        _currentPhase = TutorialPhase.Step6_DebrisCleanup;
        ResetPhaseTimers();

        TutorialArrowUI.Instance?.Hide();

        string speech = "Researcher, I'm detecting discarded ocean debris polluting the coral bed ahead. "
                      + "Human pollution disrupts fragile marine life. Maneuver in close and deploy your robotic collection arm to collect and sort the waste.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Approach debris and tap INTERACT to sort waste",
            onComplete: null,
            showSkipStep: false
        );

        EnsureSeabedDebris();

        if (_tutorialDebris != null)
        {
            TutorialArrowUI.Instance?.PointAtWorld(_tutorialDebris.transform, "DEBRIS CLUSTER", Vector3.up * 1.5f, TutorialArrowUI.ArrowDirection.Down);
        }

        var ui = UIManager.Instance;
        if (ui != null)
        {
            var interactRect = ui.GetInteractButtonRect();
            if (interactRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(interactRect, "TAP TO DEPLOY ARM", TutorialArrowUI.ArrowDirection.Down);
            }
        }
    }

    // -----------------------------------------------------------------------
    // STEP 7 — Hazard Encounter: "Dangers Ahead" (Minigame 4)
    // -----------------------------------------------------------------------

    public void StartStep7()
    {
        _currentPhase = TutorialPhase.Step7_HazardEncounter;
        ResetPhaseTimers();
        _step7CompletedHandled = false;

        TutorialArrowUI.Instance?.Hide();

        var hazard = FindFirstObjectByType<HazardZoneTrigger>();
        if (hazard != null)
        {
            TutorialArrowUI.Instance?.PointAtWorld(hazard.transform, "HAZARD ZONE", Vector3.up * 2f, TutorialArrowUI.ArrowDirection.Down);
        }

        string speech = "⚠️ DANGERS AHEAD — Turbulent reef current and seamount terrain detected! "
                      + "Engage emergency evasive navigation. Swipe left and right across lanes to dodge falling rocks and vortexes. "
                      + "Collect floating rare pearls along the route to earn bonus research data!";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Engage emergency evasion and collect rare pearls",
            onComplete: () =>
            {
                // Automated trigger of Minigame 4 after radio speech completes
                if (!_step7CompletedHandled && MinigameManager.Instance != null && !MinigameManager.Instance.IsMinigameActive)
                {
                    TutorialArrowUI.Instance?.Hide();
                    MinigameManager.Instance.TriggerHazardDodgeMinigame(
                        0,
                        onSuccess: (reward) => OnHazardCompleted(survived: true),
                        onFail: () => OnHazardCompleted(survived: false)
                    );
                }
            },
            showSkipStep: false,
            autoDismissDelay: 1.0f,
            forceImmediate: true
        );
    }

    public void OnHazardCompleted(bool survived)
    {
        if (_step7CompletedHandled) return;
        _step7CompletedHandled = true;

        TutorialArrowUI.Instance?.Hide();

        string finishSpeech = survived
            ? "Masterful evasive piloting! You navigated the turbulence and secured valuable survey data."
            : "Hull alarms silenced. Even experienced deep-sea pilots take impacts. Reinforcing your hull at the Shop will increase your survival margin.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            finishSpeech,
            "",
            onComplete: () => StartStep8(),
            showSkipStep: false,
            autoDismissDelay: 2.5f,
            forceImmediate: true
        );
    }

    // -----------------------------------------------------------------------
    // STEP 8 — Submarine Upgrade Shop
    // -----------------------------------------------------------------------

    public void StartStep8()
    {
        _currentPhase = TutorialPhase.Step8_UpgradeShop;
        ResetPhaseTimers();

        string speech = "You've earned Research Data Points from species scanning, debris removal, and hazard navigation. "
                      + "Tap the Shop icon to inspect submarine upgrades.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Open the Upgrade Shop",
            onComplete: null,
            showSkipStep: false
        );

        var ui = UIManager.Instance;
        if (ui != null)
        {
            var shopRect = ui.GetShopButtonRect();
            if (shopRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(shopRect, "OPEN UPGRADE SHOP", TutorialArrowUI.ArrowDirection.Right);
            }
        }
    }

    // -----------------------------------------------------------------------
    // GRADUATION
    // -----------------------------------------------------------------------

    public void GraduateTutorial()
    {
        _currentPhase = TutorialPhase.Graduation;
        TutorialArrowUI.Instance?.Hide();
        UIManager.Instance?.SetHUDSkipTutorialVisible(false);

        // Award +100 RDP Expedition Bonus
        GameManager.SetTutorialCompleted(true);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddRDP(100);
            GameManager.Instance.SaveGame();
        }

        AudioManager.Instance?.PlayTutorialStepChime();

        string speech = "All systems verified and operational. Your training certification is complete, Researcher. "
                      + "The Philippine Sea holds 95 species waiting to be discovered across 5 depth zones. "
                      + "The deep ocean is calling. Dr. Santos out.";

        ObjectiveHUDUI.Instance?.SetObjective(ObjectiveHUDUI.PostTutorialDefaultObjective);

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "TUTORIAL COMPLETE — +100 RDP EXPEDITION BONUS!",
            onComplete: () =>
            {
                _currentPhase = TutorialPhase.Completed;
                ObjectiveHUDUI.Instance?.SetObjective(ObjectiveHUDUI.PostTutorialDefaultObjective);
            },
            showSkipStep: false,
            autoDismissDelay: 6.0f
        );

        RestoreAllTutorialEntities();
        Debug.Log("[TutorialManager] Tutorial graduated successfully! Awarded +100 RDP.");
    }

    // -----------------------------------------------------------------------
    // Update Loop & Non-Compliance Escalation
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (!IsTutorialActive) return;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;
        if (Time.timeScale <= 0f) return;

        _phaseTimer += Time.unscaledDeltaTime;

        // Non-compliance checks
        if (_phaseTimer >= 20f && !_tier1Nudged)
        {
            _tier1Nudged = true;
            ApplyTier1Nudge();
        }

        if (_phaseTimer >= 40f && !_tier2Assisted)
        {
            _tier2Assisted = true;
            ApplyTier2Assist();
        }

        if (_phaseTimer >= 60f && !_tier3SkipStepAvailable)
        {
            _tier3SkipStepAvailable = true;
            RadioTextboxUI.Instance?.SetSkipStepVisible(true);
        }

        // Prevent late-spawned entities or unmanaged creatures from appearing before Step 3
        if (_currentPhase < TutorialPhase.Step3_SonarSystem)
        {
            EnforceEntitiesHiddenBeforeStep3();
        }

        // Active Step State Checks
        switch (_currentPhase)
        {
            case TutorialPhase.Step2_Navigation:
                CheckStep2Navigation();
                break;

            case TutorialPhase.Step3_SonarSystem:
                CheckStep3SonarProximity();
                break;

            case TutorialPhase.Step5_BestiaryReview:
                CheckStep5Bestiary();
                break;

            case TutorialPhase.Step6_DebrisCleanup:
                CheckStep6DebrisCleaned();
                break;

            case TutorialPhase.Step8_UpgradeShop:
                CheckStep8ShopOpened();
                break;
        }
    }

    private void ResetPhaseTimers()
    {
        _phaseTimer = 0f;
        _tier1Nudged = false;
        _tier2Assisted = false;
        _tier3SkipStepAvailable = false;
        _species1VisualConfirmed = false;
        _step2NavigationHandled = false;
        _step6DebrisHandled = false;
        _step6DebrisCleanedTriggered = false;
        _step7CompletedHandled = false;
        RadioTextboxUI.Instance?.SetSkipStepVisible(false);
    }

    private void CheckStep2Navigation()
    {
        if (_step2NavigationHandled) return;

        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null && player.HasMovementInput)
        {
            _sustainedMoveTimer += Time.unscaledDeltaTime;
            if (_sustainedMoveTimer >= 2.0f)
            {
                _step2NavigationHandled = true;
                TutorialArrowUI.Instance?.Hide();
                RadioTextboxUI.Instance?.DisplayMessage(
                    "DR. SANTOS",
                    "Thrusters responding crisply. You're handling the submarine like a pro.",
                    "",
                    onComplete: () => StartStep3(),
                    showSkipStep: false,
                    forceImmediate: true,
                    autoDismissDelay: 2.0f
                );
            }
        }
        else
        {
            _sustainedMoveTimer = Mathf.Max(0f, _sustainedMoveTimer - Time.unscaledDeltaTime * 0.5f);
        }
    }

    private void CheckStep3SonarProximity()
    {
        if (_species1VisualConfirmed) return;

        // If species 1 was already discovered (e.g. player scanned early before proximity trigger completed)
        if (_tutorialSpecies1 != null && _tutorialSpecies1.Data != null && GameManager.Instance != null && GameManager.Instance.IsDiscovered(_tutorialSpecies1.Data.speciesId))
        {
            _species1VisualConfirmed = true;
            return;
        }

        var player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) return;

        if (_tutorialSpecies1 == null)
        {
            EnsureTutorialSpecies(false);
        }

        if (_tutorialSpecies1 != null)
        {
            float dist = Vector3.Distance(player.transform.position, _tutorialSpecies1.transform.position);
            if (dist <= 35f)
            {
                // Wait until previous radio box (sonar explanation) has finished before proceeding
                if (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy)
                {
                    return;
                }

                _species1VisualConfirmed = true;
                TutorialArrowUI.Instance?.SetSonarLegendVisible(false);
                RadioTextboxUI.Instance?.DisplayMessage(
                    "DR. SANTOS",
                    "Contact visual confirmed! Target is within scanning distance.",
                    "",
                    onComplete: () => StartStep4A(),
                    showSkipStep: false,
                    autoDismissDelay: 2.0f
                );
            }
        }
    }

    private void CheckStep5Bestiary()
    {
        if (!_bestiaryOpenedInStep5)
        {
            if (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen)
            {
                _bestiaryOpenedInStep5 = true;
                TutorialArrowUI.Instance?.Hide();
            }
        }
        else
        {
            if (BestiaryManager.Instance != null && !BestiaryManager.Instance.IsOpen)
            {
                if (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy) return;
                StartStep6();
            }
        }
    }

    public void NotifyDebrisCleaned()
    {
        if (_currentPhase == TutorialPhase.Step6_DebrisCleanup)
        {
            _step6DebrisCleanedTriggered = true;
        }
    }

    private void CheckStep6DebrisCleaned()
    {
        if (_step6DebrisHandled) return;

        if (_step6DebrisCleanedTriggered || (_tutorialDebris != null && _tutorialDebris.IsCleaned))
        {
            _step6DebrisCleanedTriggered = true;

            // Wait for previous radio box to finish before proceeding
            if (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy)
            {
                return;
            }

            _step6DebrisHandled = true;
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "Reef cleared! Removing debris revitalizes the ecosystem, increasing species encounter rates. You've earned research funding bonus.",
                "",
                onComplete: () => StartStep7(),
                showSkipStep: false,
                autoDismissDelay: 2.5f,
                forceImmediate: true
            );
        }
    }

    private void CheckStep8ShopOpened()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.IsOpen)
        {
            TutorialArrowUI.Instance?.Hide();
            GraduateTutorial();
        }
    }

    // -----------------------------------------------------------------------
    // Non-Compliance Escalations
    // -----------------------------------------------------------------------

    private void ApplyTier1Nudge()
    {
        if (_currentPhase == TutorialPhase.Step3_SonarSystem || _currentPhase == TutorialPhase.Step4A_ScanEncounter1)
        {
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "Researcher, check your sonar radar. There's a biological contact right near your heading.",
                "Track Cyan species signature on sonar",
                showSkipStep: false
            );
        }
    }

    private void ApplyTier2Assist()
    {
        if (_currentPhase == TutorialPhase.Step3_SonarSystem || _currentPhase == TutorialPhase.Step4A_ScanEncounter1)
        {
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "I'm highlighting the specimen on your HUD. Align it with your forward viewport reticle.",
                "Align creature in reticle and complete scan",
                showSkipStep: false
            );

            if (_tutorialSpecies1 != null)
            {
                TutorialArrowUI.Instance?.PointAtWorld(_tutorialSpecies1.transform, "TARGET SPECIMEN", Vector3.up * 1f, TutorialArrowUI.ArrowDirection.Down);

                // Steer creature gently towards player
                var player = FindFirstObjectByType<PlayerMovement>();
                if (player != null)
                {
                    Vector3 steerDir = (player.transform.position + player.transform.forward * 8f - _tutorialSpecies1.transform.position).normalized;
                    _tutorialSpecies1.transform.position += steerDir * 2.5f;
                }
            }
        }
    }

    private void OnSkipStepClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            "Remote telemetry captured the specimen's biological signature. Let's move on.",
            "",
            onComplete: () => AdvanceToNextStep(),
            showSkipStep: false,
            autoDismissDelay: 2.0f
        );
    }

    private void AdvanceToNextStep()
    {
        switch (_currentPhase)
        {
            case TutorialPhase.Step0_MissionOverview:
                StartStep1();
                break;
            case TutorialPhase.Step1_HUDOrientation:
                StartStep2();
                break;
            case TutorialPhase.Step2_Navigation:
                StartStep3();
                break;
            case TutorialPhase.Step3_SonarSystem:
                StartStep4A();
                break;
            case TutorialPhase.Step4A_ScanEncounter1:
                StartStep4B();
                break;
            case TutorialPhase.Step4B_ScanEncounter2:
                StartStep5();
                break;
            case TutorialPhase.Step5_BestiaryReview:
                StartStep6();
                break;
            case TutorialPhase.Step6_DebrisCleanup:
                StartStep7();
                break;
            case TutorialPhase.Step7_HazardEncounter:
                StartStep8();
                break;
            case TutorialPhase.Step8_UpgradeShop:
                GraduateTutorial();
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Scan Event Handling (Steps 4A & 4B)
    // -----------------------------------------------------------------------

    private void OnSpeciesScannedHandler(SpeciesData data)
    {
        if (_currentPhase == TutorialPhase.Step3_SonarSystem || _currentPhase == TutorialPhase.Step4A_ScanEncounter1)
        {
            _species1VisualConfirmed = true;
            TutorialArrowUI.Instance?.SetSonarLegendVisible(false);
            TutorialArrowUI.Instance?.Hide();
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "First specimen logged! Excellent work. But our survey requires data on another nearby creature to calibrate our secondary scanning protocol.",
                "",
                onComplete: () => StartStep4B(),
                showSkipStep: false,
                autoDismissDelay: 2.5f
            );
        }
        else if (_currentPhase == TutorialPhase.Step4B_ScanEncounter2)
        {
            TutorialArrowUI.Instance?.Hide();
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "Both scanning modules calibrated! All species data has been automatically synced with your on-board encyclopedia. Let's review the Bestiary.",
                "",
                onComplete: () => StartStep5(),
                showSkipStep: false,
                autoDismissDelay: 2.5f
            );
        }
    }

    public int GetTutorialMinigamePick()
    {
        if (_currentPhase == TutorialPhase.Step3_SonarSystem || _currentPhase == TutorialPhase.Step4A_ScanEncounter1) return 1; // Always Minigame 1
        if (_currentPhase == TutorialPhase.Step4B_ScanEncounter2) return 2; // Always Minigame 2
        return -1;
    }

    // -----------------------------------------------------------------------
    // Code-Driven Placement & Anchoring Helpers
    // -----------------------------------------------------------------------

    private float _hideEnforceTimer = 0f;
    private void EnforceEntitiesHiddenBeforeStep3()
    {
        _hideEnforceTimer += Time.unscaledDeltaTime;
        if (_hideEnforceTimer < 0.25f) return;
        _hideEnforceTimer = 0f;

        var creaturesParent = GameObject.Find("_Creatures");
        if (creaturesParent != null)
        {
            for (int i = 0; i < creaturesParent.transform.childCount; i++)
            {
                var child = creaturesParent.transform.GetChild(i).gameObject;
                if (child != null && child.activeSelf) child.SetActive(false);
            }
        }

        var debrisParent = GameObject.Find("_DebrisClusters");
        if (debrisParent != null)
        {
            for (int i = 0; i < debrisParent.transform.childCount; i++)
            {
                var child = debrisParent.transform.GetChild(i).gameObject;
                if (child != null && child.activeSelf) child.SetActive(false);
            }
        }
    }

    private void HideAllTutorialEntities()
    {
        _cachedSceneSpecies.Clear();
        var allSpecies = FindObjectsByType<SpeciesAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in allSpecies)
        {
            if (s != null)
            {
                _cachedSceneSpecies.Add(s);
                s.gameObject.SetActive(false);
            }
        }

        // Also deactivate stationary species (ScanTarget)
        var allTargets = FindObjectsByType<ScanTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTargets)
        {
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }

        // Also deactivate all children in _Creatures container
        var creaturesParent = GameObject.Find("_Creatures");
        if (creaturesParent != null)
        {
            for (int i = 0; i < creaturesParent.transform.childCount; i++)
            {
                var child = creaturesParent.transform.GetChild(i).gameObject;
                if (child != null) child.SetActive(false);
            }
        }

        // Pre-select two distinct species for Step 4A and Step 4B if available
        if (_cachedSceneSpecies.Count > 0)
        {
            _tutorialSpecies1 = _cachedSceneSpecies[0];
        }
        if (_cachedSceneSpecies.Count > 1)
        {
            _tutorialSpecies2 = _cachedSceneSpecies[1];
            for (int i = 1; i < _cachedSceneSpecies.Count; i++)
            {
                if (_cachedSceneSpecies[i].Data != null && _tutorialSpecies1?.Data != null &&
                    _cachedSceneSpecies[i].Data != _tutorialSpecies1.Data)
                {
                    _tutorialSpecies2 = _cachedSceneSpecies[i];
                    break;
                }
            }
        }

        _cachedSceneDebris.Clear();
        var allDebris = FindObjectsByType<DebrisCluster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in allDebris)
        {
            if (d != null)
            {
                _cachedSceneDebris.Add(d);
                d.gameObject.SetActive(false);
            }
        }

        var debrisParent = GameObject.Find("_DebrisClusters");
        if (debrisParent != null)
        {
            for (int i = 0; i < debrisParent.transform.childCount; i++)
            {
                var child = debrisParent.transform.GetChild(i).gameObject;
                if (child != null) child.SetActive(false);
            }
        }

        if (_cachedSceneDebris.Count > 0)
        {
            _tutorialDebris = _cachedSceneDebris[0];
        }
    }

    private void RestoreAllTutorialEntities()
    {
        var creaturesParent = GameObject.Find("_Creatures");
        if (creaturesParent != null)
        {
            for (int i = 0; i < creaturesParent.transform.childCount; i++)
            {
                var child = creaturesParent.transform.GetChild(i).gameObject;
                if (child != null) child.SetActive(true);
            }
        }

        var debrisParent = GameObject.Find("_DebrisClusters");
        if (debrisParent != null)
        {
            for (int i = 0; i < debrisParent.transform.childCount; i++)
            {
                var child = debrisParent.transform.GetChild(i).gameObject;
                if (child != null) child.SetActive(true);
            }
        }

        var allTargets = FindObjectsByType<ScanTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTargets)
        {
            if (t != null) t.gameObject.SetActive(true);
        }

        foreach (var s in _cachedSceneSpecies)
        {
            if (s != null) s.gameObject.SetActive(true);
        }
        foreach (var d in _cachedSceneDebris)
        {
            if (d != null) d.gameObject.SetActive(true);
        }
    }

    private void PositionTutorialEntity(GameObject entity, float distanceAhead, float verticalOffset = 0f)
    {
        if (entity == null) return;

        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 pPos = player != null ? player.transform.position : Vector3.zero;
        Vector3 fwd  = player != null ? player.transform.forward : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 spawnPos = pPos + fwd * distanceAhead + Vector3.up * verticalOffset;
        entity.transform.position = spawnPos;
        entity.transform.forward = -fwd;
        entity.SetActive(true);
    }

    private void PositionTutorialDebris(GameObject debris, float distanceAhead)
    {
        if (debris == null) return;
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 pPos = player != null ? player.transform.position : Vector3.zero;
        Vector3 fwd  = player != null ? player.transform.forward : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 candidatePos = pPos + fwd * distanceAhead;
        Vector3 rayOrigin = new Vector3(candidatePos.x, candidatePos.y + 40f, candidatePos.z);
        Vector3 floorPos = candidatePos;
        floorPos.y = -140f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
        {
            floorPos = hit.point;
        }

        debris.transform.position = floorPos;
        debris.SetActive(true);
    }

    private void EnsureTutorialSpecies(bool isSecond = false)
    {
        if (!isSecond)
        {
            if (_tutorialSpecies1 != null)
            {
                PositionTutorialEntity(_tutorialSpecies1.gameObject, 28f, 1f);
                return;
            }
        }
        else
        {
            if (_tutorialSpecies2 != null)
            {
                PositionTutorialEntity(_tutorialSpecies2.gameObject, 22f, 2f);
                return;
            }
        }

        var allAI = FindObjectsByType<SpeciesAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (!isSecond && allAI.Length > 0)
        {
            _tutorialSpecies1 = allAI[0];
            PositionTutorialEntity(_tutorialSpecies1.gameObject, 28f, 1f);
            return;
        }
        else if (isSecond && allAI.Length > 1)
        {
            _tutorialSpecies2 = allAI[1];
            PositionTutorialEntity(_tutorialSpecies2.gameObject, 22f, 2f);
            return;
        }
    }

    private void EnsureSeabedDebris()
    {
        if (_tutorialDebris != null)
        {
            PositionTutorialDebris(_tutorialDebris.gameObject, 25f);
            return;
        }

        // Check if a debris cluster already exists in the scene
        var existing = FindFirstObjectByType<DebrisCluster>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _tutorialDebris = existing;
            PositionTutorialDebris(_tutorialDebris.gameObject, 25f);
            return;
        }

        // Otherwise raycast onto the ocean floor in front of player
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 candidatePos = playerPos + forward * 25f;
        Vector3 rayOrigin = new Vector3(candidatePos.x, candidatePos.y + 50f, candidatePos.z);
        Vector3 floorPos = candidatePos;
        floorPos.y = -140f; // Sunlight zone default floor fallback

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
        {
            floorPos = hit.point;
        }

        var debrisGO = new GameObject("Tutorial_DebrisCluster");
        debrisGO.transform.position = floorPos;
        _tutorialDebris = debrisGO.AddComponent<DebrisCluster>();
        debrisGO.AddComponent<SphereCollider>().isTrigger = true;
        _cachedSceneDebris.Add(_tutorialDebris);
    }

    // -----------------------------------------------------------------------
    // Skip Tutorial System & Modal (Section 1D)
    // -----------------------------------------------------------------------

    public void ShowSkipTutorialPrompt()
    {
        if (customPromptModal != null)
        {
            customPromptModal.transform.SetAsLastSibling();
            customPromptModal.SetActive(true);
            return;
        }

        BuildSkipConfirmModal();
        if (_skipConfirmModal != null)
        {
            _skipConfirmModal.transform.SetAsLastSibling();
            _skipConfirmModal.SetActive(true);
        }
    }

    private void BuildSkipConfirmModal()
    {
        if (_skipConfirmModal != null) return;

        EnsureCanvas();
        if (_tutorialCanvas == null) return;

        TMP_FontAsset font = UIThemeManager.AntoneFont;

        // Expanded 940x460 modal dialog container
        _skipConfirmModal = new GameObject("SkipTutorialConfirmModal", typeof(RectTransform), typeof(Image));
        _skipConfirmModal.transform.SetParent(_tutorialCanvas.transform, false);

        // Dedicated top sorting order canvas so modal renders in front of all minigames and overlays
        var modalCanvas = _skipConfirmModal.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 32500;
        var modalRaycaster = _skipConfirmModal.AddComponent<GraphicRaycaster>();
        modalRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        var modalRect = _skipConfirmModal.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot     = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(940f, 460f);
        modalRect.anchoredPosition = Vector2.zero;

        var bg = _skipConfirmModal.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.07f, 0.14f, 0.96f);

        var outline = _skipConfirmModal.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.9f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Top cyan glowing accent line
        var topAccent = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
        topAccent.transform.SetParent(_skipConfirmModal.transform, false);
        var taRect = topAccent.GetComponent<RectTransform>();
        taRect.anchorMin = new Vector2(0f, 1f);
        taRect.anchorMax = new Vector2(1f, 1f);
        taRect.pivot     = new Vector2(0.5f, 1f);
        taRect.sizeDelta = new Vector2(0f, 3f);
        taRect.anchoredPosition = Vector2.zero;
        var taImg = topAccent.GetComponent<Image>();
        taImg.color = new Color(0f, 0.9f, 1f, 1f);
        taImg.raycastTarget = false;

        // Header Title: SKIP TUTORIAL?
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(_skipConfirmModal.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-60f, 50f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);

        var titleTxt = titleGO.GetComponent<TextMeshProUGUI>();
        titleTxt.font = font;
        titleTxt.fontStyle = FontStyles.Normal;
        titleTxt.fontSize = 36f;
        titleTxt.color = new Color(0.3f, 0.85f, 1f, 1f);
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.raycastTarget = false;
        titleTxt.text = "SKIP TUTORIAL?";

        // Body Description: Spacious and clean layout
        var descGO = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(_skipConfirmModal.transform, false);
        var descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.5f, 0.5f);
        descRect.anchorMax = new Vector2(0.5f, 0.5f);
        descRect.pivot     = new Vector2(0.5f, 0.5f);
        descRect.sizeDelta = new Vector2(840f, 180f);
        descRect.anchoredPosition = new Vector2(0f, 20f);

        var descTxt = descGO.GetComponent<TextMeshProUGUI>();
        descTxt.font = font;
        descTxt.fontStyle = FontStyles.Normal;
        descTxt.fontSize = 30f;
        descTxt.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.textWrappingMode = TextWrappingModes.Normal;
        descTxt.lineSpacing = 8f;
        descTxt.raycastTarget = false;
        descTxt.text = "Skip basic training and proceed directly to free ocean exploration?\nAll standard equipment and dive systems will be brought online immediately.";

        // Button Container Row
        var btnRow = new GameObject("BtnRow", typeof(RectTransform));
        btnRow.transform.SetParent(_skipConfirmModal.transform, false);
        var btnRect = btnRow.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot     = new Vector2(0.5f, 0f);
        btnRect.sizeDelta = new Vector2(-60f, 80f);
        btnRect.anchoredPosition = new Vector2(0f, 35f);

        // [ RESUME TUTORIAL ] Button (Left) - Solid background, no outline
        var resumeGO = new GameObject("ResumeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        resumeGO.transform.SetParent(btnRow.transform, false);
        var resRect = resumeGO.GetComponent<RectTransform>();
        resRect.anchorMin = new Vector2(0f, 0.5f);
        resRect.anchorMax = new Vector2(0.48f, 0.5f);
        resRect.pivot     = new Vector2(0.5f, 0.5f);
        resRect.sizeDelta = new Vector2(0f, 65f);
        resRect.anchoredPosition = Vector2.zero;

        var resImg = resumeGO.GetComponent<Image>();
        resImg.color = new Color(0.05f, 0.22f, 0.35f, 0.98f);

        var resBtn = resumeGO.GetComponent<Button>();
        resBtn.targetGraphic = resImg;
        resBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            _skipConfirmModal.SetActive(false);
        });

        var resTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        resTxtGO.transform.SetParent(resumeGO.transform, false);
        var rtRect = resTxtGO.GetComponent<RectTransform>();
        rtRect.anchorMin = Vector2.zero;
        rtRect.anchorMax = Vector2.one;
        rtRect.sizeDelta = Vector2.zero;
        rtRect.anchoredPosition = Vector2.zero;

        var resTxt = resTxtGO.GetComponent<TextMeshProUGUI>();
        resTxt.font = font;
        resTxt.fontStyle = FontStyles.Normal;
        resTxt.fontSize = 28f;
        resTxt.color = Color.white;
        resTxt.alignment = TextAlignmentOptions.Center;
        resTxt.raycastTarget = false;
        resTxt.text = "RESUME TUTORIAL";

        // [ SKIP NOW ] Button (Right) - Solid background, no outline
        var skipNowGO = new GameObject("SkipNowBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        skipNowGO.transform.SetParent(btnRow.transform, false);
        var snRect = skipNowGO.GetComponent<RectTransform>();
        snRect.anchorMin = new Vector2(0.52f, 0.5f);
        snRect.anchorMax = new Vector2(1f, 0.5f);
        snRect.pivot     = new Vector2(0.5f, 0.5f);
        snRect.sizeDelta = new Vector2(0f, 65f);
        snRect.anchoredPosition = Vector2.zero;

        var snImg = skipNowGO.GetComponent<Image>();
        snImg.color = new Color(0.42f, 0.09f, 0.09f, 0.98f);

        var snBtn = skipNowGO.GetComponent<Button>();
        snBtn.targetGraphic = snImg;
        snBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            _skipConfirmModal.SetActive(false);
            OnSkipTutorialConfirmed();
        });

        var snTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        snTxtGO.transform.SetParent(skipNowGO.transform, false);
        var snTxtRect = snTxtGO.GetComponent<RectTransform>();
        snTxtRect.anchorMin = Vector2.zero;
        snTxtRect.anchorMax = Vector2.one;
        snTxtRect.sizeDelta = Vector2.zero;
        snTxtRect.anchoredPosition = Vector2.zero;

        var snTxt = snTxtGO.GetComponent<TextMeshProUGUI>();
        snTxt.font = font;
        snTxt.fontStyle = FontStyles.Normal;
        snTxt.fontSize = 28f;
        snTxt.color = Color.white;
        snTxt.alignment = TextAlignmentOptions.Center;
        snTxt.raycastTarget = false;
        snTxt.text = "SKIP NOW";

        _skipConfirmModal.SetActive(false);
    }

    public void OnSkipTutorialConfirmed()
    {
        AbortTutorial();
        GameManager.SetTutorialCompleted(true);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddRDP(100);
            GameManager.Instance.SaveGame();
        }

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            "All systems good to go. Good luck out there, Researcher.",
            "",
            showSkipStep: false,
            autoDismissDelay: 3.0f
        );

        Debug.Log("[TutorialManager] Tutorial skipped by player — proceeding directly to free gameplay.");
    }

    public void AbortTutorial()
    {
        _currentPhase = TutorialPhase.Inactive;

        if (_step1Routine != null)
        {
            StopCoroutine(_step1Routine);
            _step1Routine = null;
        }

        CinematicSequencer.Instance?.SkipCinematic();
        RadioTextboxUI.Instance?.HideInstant();
        TutorialArrowUI.Instance?.Hide();
        TutorialArrowUI.Instance?.SetSonarLegendVisible(false);
        MinigameInstructionOverlay.Instance?.HideInstant();

        // Forcibly abort and clean up any active minigame & restore normal controls
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.CancelActiveMinigame();
        }

        ObjectiveHUDUI.Instance?.SetObjective(ObjectiveHUDUI.PostTutorialDefaultObjective);

        if (customTutorialRoot != null) customTutorialRoot.SetActive(false);
        if (_skipConfirmModal != null) _skipConfirmModal.SetActive(false);

        UIManager.Instance?.SetHUDSkipTutorialVisible(false);
        UIManager.Instance?.SetExplorationHUDVisible(true);
        RestoreAllTutorialEntities();
        Time.timeScale = 1.0f;
    }
}
