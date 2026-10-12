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
///   - Step 1: HUD Orientation (sequential 8 arrow pointers)
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
    private SpeciesData _step4BSpeciesData;
    private bool  _bestiaryOpenedInStep5 = false;
    private bool  _step5ScrolledToCard = false;
    private bool  _step5Inspected3DModel = false;
    private bool  _step5ViewedDetailModal = false;
    private bool  _step5ReviewCompleted = false;
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
    private bool _sunlightSpeciesSpawned = false;

    // Step 4A species tracking for Step 4B distinction
    private SpeciesData _step4ASpeciesData;
    public SpeciesData Step4ASpeciesData => _step4ASpeciesData;
    public string Step4ASpeciesId => _step4ASpeciesData != null ? _step4ASpeciesData.speciesId : (_tutorialSpecies1?.Data != null ? _tutorialSpecies1.Data.speciesId : "");

    public bool IsStep4ASpecies(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return false;

        if (_step4ASpeciesData != null && !string.IsNullOrEmpty(_step4ASpeciesData.speciesId))
        {
            if (string.Equals(_step4ASpeciesData.speciesId, speciesId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (_tutorialSpecies1 != null && _tutorialSpecies1.Data != null && !string.IsNullOrEmpty(_tutorialSpecies1.Data.speciesId))
        {
            if (string.Equals(_tutorialSpecies1.Data.speciesId, speciesId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

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
    // STEP 1 — HUD Orientation (8 Sequential Pointers)
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
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step1_HUDOrientation)
                {
                    if (_step1Routine != null) StopCoroutine(_step1Routine);
                    _step1Routine = StartCoroutine(Step1PointersRoutine());
                }
            },
            showSkipStep: false
        );
    }

    private IEnumerator WaitForStep1Duration(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (_currentPhase != TutorialPhase.Step1_HUDOrientation) yield break;

            bool isBlocked = (TutorialArrowUI.Instance != null && TutorialArrowUI.Instance.IsArrowBlocked())
                          || (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused)
                          || (ShopManager.Instance != null && ShopManager.Instance.IsOpen)
                          || (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen)
                          || Time.timeScale <= 0f;

            if (!isBlocked)
            {
                timer += Time.unscaledDeltaTime;
            }

            yield return null;
        }
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
        yield return WaitForStep1Duration(1.6f);

        // 2. Sonar Map (top-left circle)
        if (ui != null && arrow != null)
        {
            var r = ui.GetSonarMapRect();
            if (r != null) arrow.PointAtHUD(r, "Sonar Radar", TutorialArrowUI.ArrowDirection.Left);
        }
        yield return WaitForStep1Duration(1.6f);

        // 3. RDP Counter (top-left cluster)
        if (ui != null && arrow != null)
        {
            var r = ui.GetRDPCounterRect();
            if (r != null) arrow.PointAtHUD(r, "Research Data Points", TutorialArrowUI.ArrowDirection.Left);
        }
        yield return WaitForStep1Duration(1.6f);

        // 4. Shop Button (top-right wrench, pointing Up from below)
        if (ui != null && arrow != null)
        {
            var r = ui.GetShopButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Submarine Upgrade Shop", TutorialArrowUI.ArrowDirection.Up);
        }
        yield return WaitForStep1Duration(1.6f);

        // 5. Bestiary Button (top-right book, pointing Up from below)
        if (ui != null && arrow != null)
        {
            var r = ui.GetBestiaryButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Marine Biodiversity Bestiary", TutorialArrowUI.ArrowDirection.Up);
        }
        yield return WaitForStep1Duration(1.6f);

        // 6. Submarine Light Button (bottom-right headlights icon, pointing Down from above)
        if (ui != null && arrow != null)
        {
            var r = ui.GetLightButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Submarine Headlights", TutorialArrowUI.ArrowDirection.Down);
        }
        yield return WaitForStep1Duration(1.6f);

        // 7. Interact Button (bottom-right robotic arm icon, pointing Down from above)
        if (ui != null && arrow != null)
        {
            var r = ui.GetInteractButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Interact", TutorialArrowUI.ArrowDirection.Down);
        }
        yield return WaitForStep1Duration(1.6f);

        // 8. Scan Button (bottom-right scanner reticle icon, pointing Down from above)
        if (ui != null && arrow != null)
        {
            var r = ui.GetScanButtonRect();
            if (r != null) arrow.PointAtHUD(r, "Scan", TutorialArrowUI.ArrowDirection.Down);
        }
        yield return WaitForStep1Duration(1.6f);

        arrow?.Hide();

        // Finish all radio text from Step 1 before proceeding to Step 2
        while ((RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy) || IsTutorialPaused())
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
                      + "and push the joystick to navigate forward.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Steer submarine with joystick for 2 seconds",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step2_Navigation)
                {
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
            },
            showSkipStep: false
        );
    }

    // -----------------------------------------------------------------------
    // STEP 3 — Sonar System & Color Radar Identification
    // -----------------------------------------------------------------------

    public void StartStep3()
    {
        _currentPhase = TutorialPhase.Step3_SonarSystem;
        ResetPhaseTimers();

        TutorialArrowUI.Instance?.Hide();
        EnsureTutorialSpecies();

        // Immediately show Sonar Legend card and point arrow to Sonar Map so player
        // sees what Dr. Santos is talking about as she describes each frequency color!
        TutorialArrowUI.Instance?.SetSonarLegendVisible(true);
        var ui = UIManager.Instance;
        if (ui != null)
        {
            var sonarRect = ui.GetSonarMapRect();
            if (sonarRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(sonarRect, "SONAR RADAR CONTACTS", TutorialArrowUI.ArrowDirection.Left);
            }
        }

        string speech = "Keep an eye on your sonar radar. Different contact signatures emit distinct frequencies: "
                      + "Cyan markers indicate marine species. Yellow markers pinpoint habitat landmarks. "
                      + "Orange markers reveal collectible debris, and Red markers highlight dangerous hazard zones. "
                      + "Steer toward the nearest cyan blip.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Track and approach a Cyan species signature on sonar",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step3_SonarSystem)
                {
                    // Ensure Sonar Legend Card and pointer stay active during tracking phase
                    TutorialArrowUI.Instance?.SetSonarLegendVisible(true);
                    if (ui != null)
                    {
                        var sonarRect = ui.GetSonarMapRect();
                        if (sonarRect != null)
                        {
                            TutorialArrowUI.Instance?.PointAtHUD(sonarRect, "SONAR RADAR CONTACTS", TutorialArrowUI.ArrowDirection.Left);
                        }
                    }
                }
            },
            showSkipStep: false
        );
    }

    // -----------------------------------------------------------------------
    // STEP 4A — Species Encounter 1 (Minigame 1: Capture & Focus)
    // -----------------------------------------------------------------------

    public void StartStep4A()
    {
        // Smooth early-scan bypass: If first specimen was already scanned during Step 3, smoothly proceed to Step 4B!
        if (_tutorialSpecies1 != null && _tutorialSpecies1.Data != null && GameManager.Instance != null && GameManager.Instance.IsDiscovered(_tutorialSpecies1.Data.speciesId))
        {
            _step4ASpeciesData = _tutorialSpecies1.Data;
            SpawnAllSunlightZoneSpecies();
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
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step4A_ScanEncounter1)
                {
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
            },
            showSkipStep: false
        );
    }

    // -----------------------------------------------------------------------
    // STEP 4B — Species Encounter 2 (Minigame 2: Reconstruction Scan)
    // -----------------------------------------------------------------------

    public void StartStep4B()
    {
        _currentPhase = TutorialPhase.Step4B_ScanEncounter2;
        ResetPhaseTimers();

        // After Step 4A, all species in the Sunlight Zone spawn!
        SpawnAllSunlightZoneSpecies();

        EnsureTutorialSpecies(isSecond: true);

        string speech = "Notice the different bio-signature on this second creature? Our automated scanner will switch to the alternative scanning module. Lock on and initiate scan.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Scan second creature to calibrate all scanner modes",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step4B_ScanEncounter2)
                {
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
            },
            showSkipStep: false
        );
    }

    // -----------------------------------------------------------------------
    // STEP 5 — Bestiary Marine Encyclopedia
    // -----------------------------------------------------------------------

    public void StartStep5()
    {
        _currentPhase = TutorialPhase.Step5_BestiaryReview;
        _bestiaryOpenedInStep5 = false;
        _step5ScrolledToCard = false;
        _step5Inspected3DModel = false;
        _step5ViewedDetailModal = false;
        _step5ReviewCompleted = false;
        ResetPhaseTimers();

        string speech = "Open the Bestiary to review your documented specimens. Every discovered entry features two interactive views: "
                      + "the 3D Model modal to inspect full creature anatomy, and the Detail Modal to view biological field photos, scientific classifications, "
                      + "and ecological facts. If you ever feel lost, the search clues on undiscovered entries will guide your expedition.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Open Bestiary and inspect your scanned species",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step5_BestiaryReview)
                {
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
            },
            showSkipStep: false
        );
    }

    // -----------------------------------------------------------------------
    // STEP 6 — Environmental Debris Cleanup (Minigame 3)
    // -----------------------------------------------------------------------

    public void StartStep6()
    {
        _currentPhase = TutorialPhase.Step6_DebrisCleanup;
        ResetPhaseTimers();

        TutorialArrowUI.Instance?.Hide();
        if (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen)
        {
            BestiaryManager.Instance.HideBestiary();
        }
        EnsureSeabedDebris();

        string speech = "Researcher, I'm detecting discarded ocean debris polluting the coral bed ahead. "
                      + "Human pollution disrupts fragile marine life. Maneuver in close and deploy your robotic collection arm to collect and sort the waste.";

        RadioTextboxUI.Instance?.DisplayMessage(
            "DR. SANTOS",
            speech,
            "Approach orange dot on the sonar map and tap INTERACT to sort waste",
            onComplete: () =>
            {
                if (_currentPhase == TutorialPhase.Step6_DebrisCleanup)
                {
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
            },
            showSkipStep: false
        );
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

        string speech = "DANGERS AHEAD — Turbulent reef current and seamount terrain detected! "
                      + "Engage emergency evasive navigation.";

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
                    hazard?.DeactivateHazard();
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

        var allHazards = FindObjectsByType<HazardZoneTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var h in allHazards)
        {
            h.DeactivateHazard();
        }

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
                      + "The Philippine Sea holds species waiting to be discovered across 5 depth zones. "
                      + "The deep ocean is calling. Good luck out there.";

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

    /// <summary>
    /// Returns true if tutorial progression and timers should pause because a blocking menu, modal, shop, pause, or minigame is open.
    /// In Step 5, Bestiary is part of the objective so Bestiary doesn't pause Step 5.
    /// In Step 8, Shop is part of the objective so Shop doesn't pause Step 8.
    /// </summary>
    public bool IsTutorialPaused()
    {
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return true;
        if (Time.timeScale <= 0f) return true;
        if (ShopManager.Instance != null && ShopManager.Instance.IsOpen)
        {
            if (_currentPhase != TutorialPhase.Step8_UpgradeShop) return true;
        }
        if (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen)
        {
            if (_currentPhase != TutorialPhase.Step5_BestiaryReview) return true;
        }
        if (MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) return true;
        if (MinigameInstructionOverlay.Instance != null && MinigameInstructionOverlay.Instance.IsOpen) return true;
        if (IsTutorialModalOpen) return true;
        if (ZoneSelectionUI.Instance != null && ZoneSelectionUI.Instance.IsOpen) return true;
        if (ZoneBoundaryPopupUI.Instance != null && ZoneBoundaryPopupUI.Instance.IsOpen) return true;
        return false;
    }

    private void Update()
    {
        if (!IsTutorialActive) return;
        if (IsTutorialPaused()) return;

        _phaseTimer += Time.unscaledDeltaTime;

        // Non-compliance checks
        if (_phaseTimer >= 10f && !_tier1Nudged)
        {
            _tier1Nudged = true;
            ApplyTier1Nudge();
        }

        if (_phaseTimer >= 20f && !_tier2Assisted)
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
                CheckStep4ASpeciesBoundary();
                break;

            case TutorialPhase.Step4A_ScanEncounter1:
                CheckStep4ASpeciesBoundary();
                break;

            case TutorialPhase.Step4B_ScanEncounter2:
                CheckStep4BSpeciesBoundary();
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
            // Species spawns at 28m ahead; player must actively navigate and approach to within 16m
            if (dist <= 16f)
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

    private void CheckStep4ASpeciesBoundary()
    {
        if (_tutorialSpecies1 == null || !_tutorialSpecies1.gameObject.activeSelf) return;

        Vector3 pos = _tutorialSpecies1.transform.position;
        bool hitsBoundary = false;
        Vector3 turnNormal = Vector3.zero;

        // 1. Top surface player boundary (water surface is Y = 0; keep creature safely submerged below -3.5m)
        if (pos.y >= -3.5f)
        {
            hitsBoundary = true;
            turnNormal += Vector3.down;
        }

        // 2. Perimeter walls and bottom boundary
        var steering = _tutorialSpecies1.GetComponent<ContextSteering>();
        float halfW = steering != null ? steering.SafeHalfWidth : 270f;
        float halfL = steering != null ? steering.SafeHalfLength : 270f;
        float bottomY = steering != null ? steering.SafeBottomY : -180f;

        if (pos.x >= halfW) { hitsBoundary = true; turnNormal += Vector3.left; }
        else if (pos.x <= -halfW) { hitsBoundary = true; turnNormal += Vector3.right; }

        if (pos.z >= halfL) { hitsBoundary = true; turnNormal += Vector3.back; }
        else if (pos.z <= -halfL) { hitsBoundary = true; turnNormal += Vector3.forward; }

        if (pos.y <= bottomY) { hitsBoundary = true; turnNormal += Vector3.up; }

        if (hitsBoundary)
        {
            _tutorialSpecies1.TurnAwayFromBoundary(turnNormal.normalized);
        }
    }

    private void CheckStep4BSpeciesBoundary()
    {
        if (_tutorialSpecies2 == null || !_tutorialSpecies2.gameObject.activeSelf) return;

        Vector3 pos = _tutorialSpecies2.transform.position;
        bool hitsBoundary = false;
        Vector3 turnNormal = Vector3.zero;

        if (pos.y >= -3.5f)
        {
            hitsBoundary = true;
            turnNormal += Vector3.down;
        }

        var steering = _tutorialSpecies2.GetComponent<ContextSteering>();
        float halfW = steering != null ? steering.SafeHalfWidth : 270f;
        float halfL = steering != null ? steering.SafeHalfLength : 270f;
        float bottomY = steering != null ? steering.SafeBottomY : -180f;

        if (pos.x >= halfW) { hitsBoundary = true; turnNormal += Vector3.left; }
        else if (pos.x <= -halfW) { hitsBoundary = true; turnNormal += Vector3.right; }

        if (pos.z >= halfL) { hitsBoundary = true; turnNormal += Vector3.back; }
        else if (pos.z <= -halfL) { hitsBoundary = true; turnNormal += Vector3.forward; }

        if (pos.y <= bottomY) { hitsBoundary = true; turnNormal += Vector3.up; }

        if (hitsBoundary)
        {
            _tutorialSpecies2.TurnAwayFromBoundary(turnNormal.normalized);
        }
    }

    private void CheckStep5Bestiary()
    {
        if (BestiaryManager.Instance == null) return;

        bool isOpen = BestiaryManager.Instance.IsOpen;

        if (!isOpen)
        {
            if (_step5ReviewCompleted)
            {
                if (RadioTextboxUI.Instance != null && RadioTextboxUI.Instance.IsBusy) return;
                StartStep6();
                return;
            }

            // If Bestiary is closed and dialogue finished, point arrow at HUD Bestiary button
            if (RadioTextboxUI.Instance != null && !RadioTextboxUI.Instance.IsBusy)
            {
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
            return;
        }

        // --- Bestiary IS OPEN ---
        _bestiaryOpenedInStep5 = true;

        // Resolve the scanned species to point to (scanned in 4A or 4B)
        SpeciesData targetSpecies = _step4ASpeciesData ?? _step4BSpeciesData;
        string speciesId = targetSpecies != null ? targetSpecies.speciesId : null;

        // Auto-scroll to the scanned species once upon opening
        if (!_step5ScrolledToCard)
        {
            _step5ScrolledToCard = true;
            if (!string.IsNullOrEmpty(speciesId))
            {
                BestiaryManager.Instance.ShowBestiaryAndScrollTo(speciesId);
            }
        }

        // Check if either modal is currently open
        bool is3DModalOpen = BestiaryManager.Instance.IsModelInspectionModalOpen;
        bool isDetailModalOpen = BestiaryManager.Instance.IsDetailModalOpen;

        if (is3DModalOpen)
        {
            _step5Inspected3DModel = true;
            TutorialArrowUI.Instance?.Hide();
            return;
        }

        if (isDetailModalOpen)
        {
            _step5ViewedDetailModal = true;
            TutorialArrowUI.Instance?.Hide();
            return;
        }

        // Both modals are currently closed; locate the target card UI
        BestiaryCardUI cardUI = null;
        if (!string.IsNullOrEmpty(speciesId))
        {
            cardUI = BestiaryManager.Instance.GetCardUIForSpecies(speciesId);
        }
        if (cardUI == null)
        {
            cardUI = BestiaryManager.Instance.GetFirstDiscoveredSpeciesCardUI();
        }

        if (cardUI == null)
        {
            // Cards might still be instantiating in scrollview; wait for next frame
            return;
        }

        // Option A Sequential flow:
        // 1. Point at 3D Model thumbnail first (chevron points Left towards thumbnail, label on the right)
        if (!_step5Inspected3DModel)
        {
            var thumbRect = cardUI.ThumbnailRect;
            if (thumbRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(thumbRect, "3D MODAL", TutorialArrowUI.ArrowDirection.Left, TutorialArrowUI.LabelSide.Right);
            }
            return;
        }

        // 2. Point at Scientific Detail card body next (chevron points Left towards card body, label on the right)
        if (!_step5ViewedDetailModal)
        {
            var cardBtnRect = cardUI.CardButtonRect;
            if (cardBtnRect != null)
            {
                TutorialArrowUI.Instance?.PointAtHUD(cardBtnRect, "SCIENTIFIC DETAIL MODAL", TutorialArrowUI.ArrowDirection.Left, TutorialArrowUI.LabelSide.Right);
            }
            return;
        }

        // 3. Both have been reviewed! Complete Step 5 Bestiary review
        if (!_step5ReviewCompleted)
        {
            _step5ReviewCompleted = true;
            TutorialArrowUI.Instance?.Hide();
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "With 95 species across 5 depth zones, those search clues will be invaluable when exploring deeper waters.",
                "Close Bestiary to proceed with expedition",
                showSkipStep: false,
                autoDismissDelay: 3.0f,
                forceImmediate: true
            );
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
        else if (_currentPhase == TutorialPhase.Step4B_ScanEncounter2)
        {
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "Researcher, scan any other creature nearby to calibrate our secondary scanning protocol.",
                "Scan any different creature to calibrate all scanner modes",
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

                // Steer creature gently towards player while keeping safe floor clearance and depth bounds
                var player = FindFirstObjectByType<PlayerMovement>();
                if (player != null)
                {
                    Vector3 steerDir = (player.transform.position + player.transform.forward * 8f - _tutorialSpecies1.transform.position).normalized;
                    Vector3 nudgedPos = _tutorialSpecies1.transform.position + steerDir * 2.5f;

                    float floorY = TerrainGenerator.Instance != null
                        ? TerrainGenerator.Instance.SampleHeight(nudgedPos.x, nudgedPos.z)
                        : -190f;
                    nudgedPos.y = Mathf.Max(nudgedPos.y, floorY + 4.5f);

                    if (_tutorialSpecies1.Data != null)
                    {
                        int zIdx = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : 0;
                        float zDepth = ZoneConfig.IsValidZone(zIdx) ? ZoneConfig.Zones[zIdx].playableDepth : 200f;
                        float sMaxY = -Mathf.Min(_tutorialSpecies1.Data.minDepthFraction, _tutorialSpecies1.Data.maxDepthFraction) * zDepth;
                        float sMinY = -Mathf.Max(_tutorialSpecies1.Data.minDepthFraction, _tutorialSpecies1.Data.maxDepthFraction) * zDepth;
                        nudgedPos.y = Mathf.Clamp(nudgedPos.y, sMinY, sMaxY);
                    }

                    _tutorialSpecies1.transform.position = nudgedPos;
                    var rb1 = _tutorialSpecies1.GetComponent<Rigidbody>();
                    if (rb1 != null) { rb1.position = nudgedPos; rb1.linearVelocity = Vector3.zero; }
                }
            }
        }
        else if (_currentPhase == TutorialPhase.Step4B_ScanEncounter2)
        {
            RadioTextboxUI.Instance?.DisplayMessage(
                "DR. SANTOS",
                "I've highlighted a secondary specimen on your HUD. Align it in your reticle to initiate the scan.",
                "Scan any different creature to calibrate all scanner modes",
                showSkipStep: false
            );

            if (_tutorialSpecies2 != null)
            {
                TutorialArrowUI.Instance?.PointAtWorld(_tutorialSpecies2.transform, "TARGET SPECIMEN", Vector3.up * 1f, TutorialArrowUI.ArrowDirection.Down);

                // Steer creature gently towards player while keeping safe floor clearance and depth bounds
                var player = FindFirstObjectByType<PlayerMovement>();
                if (player != null)
                {
                    Vector3 steerDir = (player.transform.position + player.transform.forward * 8f - _tutorialSpecies2.transform.position).normalized;
                    Vector3 nudgedPos = _tutorialSpecies2.transform.position + steerDir * 2.5f;

                    float floorY = TerrainGenerator.Instance != null
                        ? TerrainGenerator.Instance.SampleHeight(nudgedPos.x, nudgedPos.z)
                        : -190f;
                    nudgedPos.y = Mathf.Max(nudgedPos.y, floorY + 4.5f);

                    if (_tutorialSpecies2.Data != null)
                    {
                        int zIdx = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : 0;
                        float zDepth = ZoneConfig.IsValidZone(zIdx) ? ZoneConfig.Zones[zIdx].playableDepth : 200f;
                        float sMaxY = -Mathf.Min(_tutorialSpecies2.Data.minDepthFraction, _tutorialSpecies2.Data.maxDepthFraction) * zDepth;
                        float sMinY = -Mathf.Max(_tutorialSpecies2.Data.minDepthFraction, _tutorialSpecies2.Data.maxDepthFraction) * zDepth;
                        nudgedPos.y = Mathf.Clamp(nudgedPos.y, sMinY, sMaxY);
                    }

                    _tutorialSpecies2.transform.position = nudgedPos;
                    var rb2 = _tutorialSpecies2.GetComponent<Rigidbody>();
                    if (rb2 != null) { rb2.position = nudgedPos; rb2.linearVelocity = Vector3.zero; }
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
                if (_step4ASpeciesData == null && _tutorialSpecies1 != null)
                {
                    _step4ASpeciesData = _tutorialSpecies1.Data;
                }
                if (_step4ASpeciesData != null && GameManager.Instance != null)
                {
                    GameManager.Instance.DiscoverSpecies(_step4ASpeciesData.zoneIndex, _step4ASpeciesData.speciesId);
                }
                SpawnAllSunlightZoneSpecies();
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
            _step4ASpeciesData = data ?? _tutorialSpecies1?.Data;
            _species1VisualConfirmed = true;
            TutorialArrowUI.Instance?.SetSonarLegendVisible(false);
            TutorialArrowUI.Instance?.Hide();

            // After Step 4A, all species in the Sunlight Zone spawn!
            SpawnAllSunlightZoneSpecies();

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
            // For Step 4B: Any species EXCEPT the species from Step 4A scanned will be counted!
            if (data != null && IsStep4ASpecies(data.speciesId))
            {
                Debug.Log($"[TutorialManager] Step 4B scan ignored: '{data.commonName}' was already scanned in Step 4A.");
                RadioTextboxUI.Instance?.DisplayMessage(
                    "DR. SANTOS",
                    "We've already cataloged that species! Scan a different creature with a distinct bio-signature to calibrate our secondary scanning protocol.",
                    "Scan a different creature to calibrate all scanner modes",
                    showSkipStep: false
                );
                return;
            }

            _step4BSpeciesData = data;
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
        _sunlightSpeciesSpawned = false;
        _step4ASpeciesData = null;
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

        // Reset tutorial specimens so they are dynamically resolved at Step 3/4B matching current player depth & habitat
        _tutorialSpecies1 = null;
        _tutorialSpecies2 = null;

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

    private void SpawnAllSunlightZoneSpecies()
    {
        if (_sunlightSpeciesSpawned) return;
        _sunlightSpeciesSpawned = true;

        var creaturesParent = GameObject.Find("_Creatures");
        if (creaturesParent != null)
        {
            for (int i = 0; i < creaturesParent.transform.childCount; i++)
            {
                var child = creaturesParent.transform.GetChild(i).gameObject;
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

        var allAI = FindObjectsByType<SpeciesAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ai in allAI)
        {
            if (ai != null) ai.gameObject.SetActive(true);
        }

        // Safety fallback: if no creatures exist in the scene, invoke SpeciesSpawner to populate zone 0
        if ((creaturesParent == null || creaturesParent.transform.childCount == 0) && allAI.Length == 0)
        {
            var spawner = FindFirstObjectByType<SpeciesSpawner>();
            if (spawner != null)
            {
                spawner.SpawnForZone(0);
            }
        }

        if (DistanceCullingManager.Instance != null && creaturesParent != null)
        {
            DistanceCullingManager.Instance.RegisterParent(creaturesParent.transform);
        }

        Debug.Log("[TutorialManager] Step 4A completed: All Sunlight Zone species spawned and activated!");
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

        Vector3 candPos = pPos + fwd * distanceAhead;

        int zoneIdx = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : 0;
        float zoneDepth = ZoneConfig.IsValidZone(zoneIdx) ? ZoneConfig.Zones[zoneIdx].playableDepth : 200f;

        var ai = entity.GetComponent<SpeciesAI>();
        SpeciesData data = ai != null ? ai.Data : null;

        // Depth limits in world Y coordinates (surface Y = 0, floor Y = -zoneDepth)
        float speciesMaxY = -3.5f; // Keep safely below surface boundary
        float speciesMinY = -zoneDepth + 15f; // Keep safely above bottom zone transition
        bool isBenthic = false;

        if (data != null)
        {
            float rawUpper = -Mathf.Min(data.minDepthFraction, data.maxDepthFraction) * zoneDepth;
            float rawLower = -Mathf.Max(data.minDepthFraction, data.maxDepthFraction) * zoneDepth;
            bool isDrifter = data.locomotionArchetype == LocomotionArchetype.SurfaceDrifter;
            speciesMaxY = isDrifter ? -0.85f : Mathf.Min(-3.0f, rawUpper);
            speciesMinY = rawLower;
            isBenthic = data.isStationary || data.locomotionArchetype == LocomotionArchetype.BenthicFollower;
        }

        // Accurate seabed height query at candidate position (TerrainGenerator + Physics.Raycast)
        float floorY = TerrainGenerator.Instance != null 
            ? TerrainGenerator.Instance.SampleHeight(candPos.x, candPos.z) 
            : -zoneDepth;

        int terrainLayerMask = LayerMask.GetMask("Terrain", "Default");
        if (terrainLayerMask == 0) terrainLayerMask = ~0;
        Vector3 rayOrigin = new Vector3(candPos.x, pPos.y + 40f, candPos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 160f, terrainLayerMask, QueryTriggerInteraction.Ignore))
        {
            floorY = Mathf.Max(floorY, hit.point.y);
        }

        if (isBenthic)
        {
            // Benthic species sit directly atop the seabed with species surface offset
            float surfaceOffset = data != null ? Mathf.Max(0.15f, data.benthicSurfaceOffset) : 0.35f;
            candPos.y = floorY + surfaceOffset;
            candPos.y = Mathf.Clamp(candPos.y, speciesMinY, speciesMaxY);

            Vector3 normal = TerrainGenerator.Instance != null 
                ? TerrainGenerator.Instance.SampleNormal(candPos.x, candPos.z) 
                : Vector3.up;
            entity.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.LookRotation(-fwd, Vector3.up);
        }
        else
        {
            // Swimming / pelagic species: maintain safe vertical clearance from seabed so it never penetrates or touches floor
            const float minFloorClearance = 5.0f;
            float targetY = pPos.y + verticalOffset;

            // Clamp strictly to creature's configured depth range
            targetY = Mathf.Clamp(targetY, speciesMinY, speciesMaxY);

            // Enforce clearance above seabed
            if (targetY < floorY + minFloorClearance)
            {
                targetY = floorY + minFloorClearance;
            }

            // If seabed plateau rises above speciesMaxY, pull spawn position back horizontally toward player
            if (targetY > speciesMaxY)
            {
                targetY = speciesMaxY;
                if (targetY < floorY + 2.5f)
                {
                    candPos.x = Mathf.Lerp(pPos.x, candPos.x, 0.4f);
                    candPos.z = Mathf.Lerp(pPos.z, candPos.z, 0.4f);
                    float floorY2 = TerrainGenerator.Instance != null 
                        ? TerrainGenerator.Instance.SampleHeight(candPos.x, candPos.z) 
                        : floorY;
                    targetY = Mathf.Max(floorY2 + minFloorClearance, speciesMinY);
                    targetY = Mathf.Clamp(targetY, speciesMinY, speciesMaxY);
                }
            }

            candPos.y = targetY;
            entity.transform.forward = -fwd;
        }

        entity.transform.position = candPos;

        // Zero out physics velocity & angular velocity so creature does not fling, oscillate, or jitter
        var rb = entity.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = candPos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        entity.SetActive(true);

        if (ai != null && ai.Data != null)
        {
            ai.Initialize(ai.Data, candPos);
        }
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
        if (floorPos.y < -185f) floorPos.y = -185f;

        debris.transform.position = floorPos;
        debris.SetActive(true);
    }

    private SpeciesAI SelectBestTutorialSpecies(bool isSecond, Vector3 playerPos)
    {
        // Ensure scene species pool is cached
        if (_cachedSceneSpecies.Count == 0)
        {
            var allAI = FindObjectsByType<SpeciesAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ai in allAI)
            {
                if (ai != null && !_cachedSceneSpecies.Contains(ai))
                {
                    _cachedSceneSpecies.Add(ai);
                }
            }
        }

        // Safety fallback: if no creatures exist in the scene, invoke SpeciesSpawner
        if (_cachedSceneSpecies.Count == 0)
        {
            var spawner = FindFirstObjectByType<SpeciesSpawner>();
            if (spawner != null)
            {
                spawner.SpawnForZone(0);
                var allAI = FindObjectsByType<SpeciesAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var ai in allAI)
                {
                    if (ai != null && !_cachedSceneSpecies.Contains(ai))
                    {
                        _cachedSceneSpecies.Add(ai);
                    }
                }
            }
        }

        int zoneIdx = ZoneManager.CurrentZoneIndex >= 0 ? ZoneManager.CurrentZoneIndex : 0;
        float zoneDepth = ZoneConfig.IsValidZone(zoneIdx) ? ZoneConfig.Zones[zoneIdx].playableDepth : 200f;
        float playerDepthM = Mathf.Abs(playerPos.y);

        BiomeBand currentBiome = TerrainGenerator.Instance != null 
            ? TerrainGenerator.Instance.GetBiomeAt(playerPos.x, playerPos.z) 
            : BiomeBand.OpenWater;

        float floorY = TerrainGenerator.Instance != null 
            ? TerrainGenerator.Instance.SampleHeight(playerPos.x, playerPos.z) 
            : -zoneDepth;
        bool isNearSeabed = Mathf.Abs(playerPos.y - floorY) < 16f;

        SpeciesAI bestCandidate = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _cachedSceneSpecies.Count; i++)
        {
            var ai = _cachedSceneSpecies[i];
            if (ai == null || ai.Data == null) continue;
            if (ai.Data.isStationary) continue; // Mobile species required for scanning tutorial

            if (isSecond)
            {
                // Must be a different species than Step 4A
                if (ai == _tutorialSpecies1 || IsStep4ASpecies(ai.Data.speciesId) ||
                    (_tutorialSpecies1 != null && _tutorialSpecies1.Data != null && ai.Data == _tutorialSpecies1.Data))
                {
                    continue;
                }
            }

            // 1. Depth alignment score
            float minD = Mathf.Min(ai.Data.minDepthFraction, ai.Data.maxDepthFraction) * zoneDepth;
            float maxD = Mathf.Max(ai.Data.minDepthFraction, ai.Data.maxDepthFraction) * zoneDepth;

            float depthScore = 0f;
            if (playerDepthM >= minD && playerDepthM <= maxD)
            {
                // Exact natural depth range match!
                float dMid = (minD + maxD) * 0.5f;
                depthScore = 1000f - Mathf.Abs(playerDepthM - dMid);
            }
            else
            {
                // Outside configured range: penalty based on depth distance
                float dist = playerDepthM < minD ? (minD - playerDepthM) : (playerDepthM - maxD);
                depthScore = Mathf.Max(0f, 600f - dist * 10f);
            }

            // 2. Biome alignment score
            float biomeScore = 0f;
            if (ai.Data.preferredBiome == currentBiome)
            {
                biomeScore = 300f;
            }
            else if (IsCompatibleBiome(ai.Data.preferredBiome, currentBiome))
            {
                biomeScore = 150f;
            }

            // 3. Locomotion archetype suitability
            float locoScore = 0f;
            if (ai.Data.locomotionArchetype == LocomotionArchetype.PelagicCruiser ||
                ai.Data.locomotionArchetype == LocomotionArchetype.PulsatileJetter ||
                ai.Data.locomotionArchetype == LocomotionArchetype.HoverBurst)
            {
                locoScore = 100f;
            }
            else if (ai.Data.locomotionArchetype == LocomotionArchetype.Serpentine)
            {
                locoScore = 60f;
            }
            else if (ai.Data.locomotionArchetype == LocomotionArchetype.BenthicFollower)
            {
                locoScore = isNearSeabed ? 80f : -250f;
            }

            float totalScore = depthScore + biomeScore + locoScore;
            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestCandidate = ai;
            }
        }

        // Secondary fallback if isSecond candidate wasn't found
        if (isSecond && bestCandidate == null)
        {
            foreach (var ai in _cachedSceneSpecies)
            {
                if (ai != null && ai.Data != null && !ai.Data.isStationary &&
                    ai != _tutorialSpecies1 && !IsStep4ASpecies(ai.Data.speciesId))
                {
                    bestCandidate = ai;
                    break;
                }
            }
        }

        if (bestCandidate == null && _cachedSceneSpecies.Count > 0)
        {
            bestCandidate = _cachedSceneSpecies[0];
        }

        return bestCandidate;
    }

    private static bool IsCompatibleBiome(BiomeBand a, BiomeBand b)
    {
        if (a == b) return true;
        if ((a == BiomeBand.Hard && b == BiomeBand.Rock) || (a == BiomeBand.Rock && b == BiomeBand.Hard)) return true;
        if ((a == BiomeBand.Soft && b == BiomeBand.OpenWater) || (a == BiomeBand.OpenWater && b == BiomeBand.Soft)) return true;
        return false;
    }

    private void EnsureTutorialSpecies(bool isSecond = false)
    {
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        if (!isSecond)
        {
            if (_tutorialSpecies1 == null || !_tutorialSpecies1.gameObject.activeSelf)
            {
                _tutorialSpecies1 = SelectBestTutorialSpecies(isSecond: false, playerPos);
            }

            if (_tutorialSpecies1 != null)
            {
                PositionTutorialEntity(_tutorialSpecies1.gameObject, 26f, 1.5f);
            }
        }
        else
        {
            if (_tutorialSpecies2 == null || _tutorialSpecies2 == _tutorialSpecies1 || 
                (_tutorialSpecies2.Data != null && IsStep4ASpecies(_tutorialSpecies2.Data.speciesId)))
            {
                _tutorialSpecies2 = SelectBestTutorialSpecies(isSecond: true, playerPos);
            }

            if (_tutorialSpecies2 != null)
            {
                PositionTutorialEntity(_tutorialSpecies2.gameObject, 22f, 2f);
            }
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
        if (floorPos.y < -185f) floorPos.y = -185f;

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
        _step4ASpeciesData = null;

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
