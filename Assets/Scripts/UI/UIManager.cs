using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages all HUD elements in a zone scene.
///
/// HUD Layout (matches wireframes):
///   Top-Left  : Depth gauge label + Sonar map
///   Top-Right : RDP counter, Shop, Bestiary, Pause buttons
///   Bottom    : Joystick area, Scanner button, Interact button
///
/// Setup:
///   Attach to a UIManager GameObject (or the Canvas root).
///   Wire up all serialized references in the Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapUI()
    {
        if (Instance != null) return;
        // Auto-load master UI prefab from Resources (checks PersistentCanvasUI, PersistentUICanvas, or Canvas)
        var prefab = Resources.Load<GameObject>("UI/PersistentCanvasUI")
                  ?? Resources.Load<GameObject>("UI/PersistentUICanvas")
                  ?? Resources.Load<GameObject>("UI/Canvas");
        if (prefab != null)
        {
            var go = Instantiate(prefab);
            go.name = "PersistentCanvasUI";
            DontDestroyOnLoad(go);
        }
    }

    /// <summary>
    /// Returns true if any major UI panel, modal, or menu (Shop, Bestiary, Pause, FactCard, etc.) is currently open.
    /// Used to freeze world timers, prevent background triggers, and pause hazard encounters.
    /// </summary>
    public static bool IsAnyPanelOrModalOpen(bool includeMinigames = true)
    {
        if (ShopManager.Instance != null && ShopManager.Instance.IsOpen) return true;
        if (BestiaryManager.Instance != null && BestiaryManager.Instance.IsOpen) return true;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return true;
        if (FactCardUI.Instance != null && FactCardUI.Instance.IsOpen) return true;
        if (BestiaryDiscoveryPopup.Instance != null && BestiaryDiscoveryPopup.Instance.IsOpen) return true;
        if (ZoneSelectionUI.Instance != null && ZoneSelectionUI.Instance.IsOpen) return true;
        if (ZoneBoundaryPopupUI.Instance != null && ZoneBoundaryPopupUI.Instance.IsOpen) return true;
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialModalOpen) return true;
        if (includeMinigames && MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) return true;
        return false;
    }

    // -----------------------------------------------------------------------
    // Inspector - HUD references
    // -----------------------------------------------------------------------

    [Header("Top-Left HUD")]
    [SerializeField] private TMP_Text depthLabel;       // "DEPTH  100 m"
    [SerializeField] private TMP_Text zoneNameLabel;    // "SUNLIGHT ZONE"


    [Tooltip("Assign your Sonar Map Image directly here.")]
    [SerializeField] private Image sonarMapImage;
    private SonarMapUI sonarMap;

    [Header("Top-Right HUD")]
    [SerializeField] private TMP_Text rdpLabel;         // "RDP  150"

    [Header("Bottom HUD Buttons")]
    [Tooltip("SCAN button Image - tinted cyan when a species is in the reticle.")]
    [SerializeField] private UnityEngine.UI.Image scanButtonImage;
    [Tooltip("INTERACT button Image - tinted cyan when near a stationary species or debris cluster.")]
    [SerializeField] private UnityEngine.UI.Image interactButtonImage;
    [Tooltip("LIGHT button Image - toggles submarine headlights (tinted cyan when ON, dark blue when OFF).")]
    [SerializeField] private UnityEngine.UI.Image lightButtonImage;

    [Header("References (auto-found)")]
    [SerializeField] private DepthTracker   depthTracker;
    [SerializeField] private ScannerSystem  scannerSystem;

    private Image _scanInnerGlow;
    private Image _interactInnerGlow;
    private Image _lightInnerGlow;
    private Image _moveInnerGlow;
    private static Sprite _cachedInnerGlowSprite;

    private CanvasGroup _scanCanvasGroup;
    private CanvasGroup _interactCanvasGroup;
    private CanvasGroup _lightCanvasGroup;

    [Header("Button Opacity Settings")]
    [Tooltip("Opacity of the Scan and Interact buttons when usable (locked on target / ready).")]
    [Range(0f, 1f)]
    [SerializeField] private float usableButtonOpacity = 1.00f;

    [Tooltip("Opacity of the Scan and Interact buttons when unusable / idle (default 75% = 0.75f).")]
    [Range(0f, 1f)]
    [SerializeField] private float unusableButtonOpacity = 0.75f;

    public const float BtnUsableOpacity   = 1.00f; // 100% opacity when locked on target / ready
    public const float BtnUnusableOpacity = 0.75f; // 75% opacity when unusable / no target

    public static readonly UnityEngine.Color BtnDefault       = new UnityEngine.Color(0.02f, 0.08f, 0.13f, 1.00f); // #051421 (Sonar Map dark ocean, fully opaque)
    public static readonly UnityEngine.Color BtnActiveIcon     = new UnityEngine.Color(0.20f, 1.00f, 0.95f, 1.00f); // Clean, sharp, luminous cyan-white (Active Detection)
    public static readonly UnityEngine.Color BtnActiveRing     = new UnityEngine.Color(0.08f, 0.75f, 0.68f, 1.00f); // Active Ring Cyan
    public static readonly UnityEngine.Color NavBtnDefault     = new UnityEngine.Color(0.60f, 0.88f, 0.90f, 1.00f); // Subtle, softer/lighter oceanic cyan (Top-Right Buttons)
    public static readonly UnityEngine.Color InnerGlowIdle     = new UnityEngine.Color(0.00f, 0.93f, 0.85f, 0.28f); // Soft subtle inner radial gradient
    public static readonly UnityEngine.Color InnerGlowActive   = new UnityEngine.Color(0.00f, 0.93f, 0.85f, 0.75f); // Vibrant active inner cavity illumination

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private GameObject _hudRoot;
    private bool _isExplorationHUDVisible = false;
    public bool IsExplorationHUDVisible => _isExplorationHUDVisible;

    public static bool IsMenuScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return sceneName == "MainMenu" || sceneName == "SplashScreen" || sceneName == "ZoneSelection" || sceneName == "ZoneSelect";
    }

    private void EnsureHudRoot()
    {
        if (_hudRoot != null) return;
        var hudTr = transform.Find("HUD");
        if (hudTr != null)
        {
            _hudRoot = hudTr.gameObject;
            return;
        }

        var canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
        if (canvas != null)
        {
            hudTr = canvas.transform.Find("HUD");
            if (hudTr != null)
            {
                _hudRoot = hudTr.gameObject;
                return;
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name.Equals("HUD", StringComparison.OrdinalIgnoreCase))
            {
                _hudRoot = child.gameObject;
                return;
            }
        }

        if (_hudRoot == null)
        {
            var rootTr = transform.root;
            if (rootTr != null)
            {
                var h = FindChildRecursive(rootTr, "HUD");
                if (h != null) _hudRoot = h.gameObject;
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject != gameObject ? gameObject : gameObject);
            return;
        }
        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        else
            DontDestroyOnLoad(transform.root.gameObject);

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        SubmarineLightController.OnLightsChanged -= OnSubmarineLightsChanged;
        SubmarineLightController.OnLightsChanged += OnSubmarineLightsChanged;

        EnsureHudRoot();
        HideMinigamesAndModals();

        // Immediately hide HUD if starting in a menu scene
        string currentScene = SceneManager.GetActiveScene().name;
        if (IsMenuScene(currentScene))
        {
            SetExplorationHUDVisible(false);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SubmarineLightController.OnLightsChanged -= OnSubmarineLightsChanged;
    }

    private void OnSubmarineLightsChanged(bool lightsOn)
    {
        ShowLightButton(lightsOn);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;

        EnsureHudRoot();

        // 1. Is this a menu scene or an exploration zone?
        bool isMenu = IsMenuScene(scene.name);
        SetExplorationHUDVisible(!isMenu);

        HideMinigamesAndModals();

        if (isMenu)
        {
            // Close any open modals
            ShopManager.Instance?.HideShop();
            BestiaryManager.Instance?.HideBestiary();
            ZoneSelectionUI.Instance?.CloseZoneSelection();
            PauseMenuUI.Instance?.ResumeGame();
            return;
        }

        // 2. Re-bind to the new scene's player, camera, and sensors
        depthTracker = FindFirstObjectByType<DepthTracker>();
        scannerSystem = FindFirstObjectByType<ScannerSystem>() ?? ScannerSystem.Instance;

        EnsureSonarMap();

        // 3. Update Zone Header ("TWILIGHT ZONE", "ABYSS ZONE", etc.)
        RefreshHUD();
        RefreshDepth();
        BindHUDButtons();
    }

    private void Start()
    {
        EnsureHudRoot();

        string currentScene = SceneManager.GetActiveScene().name;
        bool isMenu = IsMenuScene(currentScene);
        if (isMenu)
        {
            SetExplorationHUDVisible(false);
            return;
        }

        if (depthTracker == null)
            depthTracker = FindFirstObjectByType<DepthTracker>();

        if (scannerSystem == null)
            scannerSystem = FindFirstObjectByType<ScannerSystem>() ?? ScannerSystem.Instance;

        EnsureSonarMap();
        BindHUDButtons();
        RefreshHUD();
    }

    private void EnsureSonarMap()
    {
        if (sonarMap == null)
        {
            if (sonarMapImage != null)
            {
                sonarMap = sonarMapImage.GetComponent<SonarMapUI>();
                if (sonarMap == null) sonarMap = sonarMapImage.gameObject.AddComponent<SonarMapUI>();
            }
            else
            {
                var mapGO = GameObject.Find("SonarMap") ?? GameObject.Find("Map placeholder") ?? GameObject.Find("Map");
                if (mapGO != null)
                {
                    sonarMap = mapGO.GetComponent<SonarMapUI>();
                    if (sonarMap == null) sonarMap = mapGO.AddComponent<SonarMapUI>();
                }
                else
                {
                    sonarMap = SonarMapUI.Instance ?? FindFirstObjectByType<SonarMapUI>();
                }
            }
        }
    }

    private void Update()
    {
        if (!_isExplorationHUDVisible) return;
        RefreshDepth();
        RefreshHUD();
    }

    // -----------------------------------------------------------------------
    // Refresh methods (called every frame or on-demand)
    // -----------------------------------------------------------------------

    private void EnsureHudLabels()
    {
        EnsureHudRoot();
        if (zoneNameLabel == null)
        {
            var tr = (_hudRoot != null ? _hudRoot.transform.Find("Zone") : null)
                  ?? GameObject.Find("Zone")?.transform;
            if (tr != null) zoneNameLabel = tr.GetComponent<TMP_Text>();
        }
        if (depthLabel == null)
        {
            var tr = (_hudRoot != null ? _hudRoot.transform.Find("Depth") : null)
                  ?? GameObject.Find("Depth")?.transform;
            if (tr != null) depthLabel = tr.GetComponent<TMP_Text>();
        }
        if (rdpLabel == null)
        {
            var tr = (_hudRoot != null ? _hudRoot.transform.Find("RDP counter") : null)
                  ?? GameObject.Find("RDP counter")?.transform;
            if (tr != null) rdpLabel = tr.GetComponent<TMP_Text>();
        }
    }

    public void RefreshDepth()
    {
        EnsureHudLabels();
        if (depthTracker == null) depthTracker = FindFirstObjectByType<DepthTracker>();
        if (depthLabel == null || depthTracker == null) return;
        depthLabel.text = $"DEPTH  {depthTracker.DisplayDepthMetres:0} m";
    }

    public void RefreshHUD()
    {
        EnsureHudLabels();

        int z = ZoneManager.CurrentZoneIndex;
        string activeScene = SceneManager.GetActiveScene().name;

        // If ZoneManager index doesn't match the active scene, sync it from the active scene name
        if (!ZoneConfig.IsValidZone(z) || ZoneConfig.Zones[z].sceneName != activeScene)
        {
            for (int i = 0; i < ZoneConfig.ZoneCount; i++)
            {
                if (ZoneConfig.Zones[i].sceneName.Equals(activeScene, StringComparison.OrdinalIgnoreCase))
                {
                    z = i;
                    ZoneManager.SetCurrentZone(i);
                    break;
                }
            }
        }

        if (zoneNameLabel != null)
        {
            zoneNameLabel.text = ZoneConfig.IsValidZone(z)
                ? ZoneConfig.Zones[z].zoneName.ToUpper()
                : "SUNLIGHT ZONE";
        }

        if (rdpLabel != null && GameManager.Instance != null)
            rdpLabel.text = $"RDP  {GameManager.Instance.RDP}";
    }

    // -----------------------------------------------------------------------
    // Exploration HUD Visibility Toggle (Used by Minigames)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Show or hide the main exploration HUD (Canvas / HUD / Sonar / Depth / Zone / Scan / Interact / Reticle / Joysticks).
    /// Used when entering and exiting minigames.
    /// </summary>
    public void SetExplorationHUDVisible(bool visible)
    {
        _isExplorationHUDVisible = visible;

        EnsureHudRoot();
        if (_hudRoot != null)
        {
            _hudRoot.SetActive(visible);
        }

        var canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
        if (canvas != null)
        {
            var hudTr = canvas.transform.Find("HUD");
            if (hudTr != null) hudTr.gameObject.SetActive(visible);
        }

        var hudGO = GameObject.Find("HUD");
        if (hudGO != null)
        {
            hudGO.SetActive(visible);
        }

        if (sonarMap != null) sonarMap.gameObject.SetActive(visible);
        if (depthLabel != null) depthLabel.gameObject.SetActive(visible);
        if (zoneNameLabel != null) zoneNameLabel.gameObject.SetActive(visible);
        if (rdpLabel != null) rdpLabel.gameObject.SetActive(visible);
        if (scanButtonImage != null) scanButtonImage.gameObject.SetActive(visible);
        if (interactButtonImage != null) interactButtonImage.gameObject.SetActive(visible);
        if (lightButtonImage != null) lightButtonImage.gameObject.SetActive(visible);

        var reticle = ScanReticleUI.Instance;
        if (reticle != null) reticle.SetVisible(visible);

        var dragZone = FindFirstObjectByType<TouchDragZone>(FindObjectsInactive.Include);
        if (dragZone != null)
        {
            dragZone.gameObject.SetActive(visible);
            dragZone.enabled = visible;
        }

        var joystickGO = GameObject.Find("MobileControls")
                      ?? GameObject.Find("Joystick") 
                      ?? GameObject.Find("Fixed Joystick") 
                      ?? GameObject.Find("Floating Joystick") 
                      ?? GameObject.Find("Dynamic Joystick")
                      ?? GameObject.Find("Virtual Joystick");
        if (joystickGO == null && _hudRoot != null)
        {
            var mcTr = FindChildRecursive(_hudRoot.transform, "MobileControls")
                    ?? FindChildRecursive(_hudRoot.transform, "Joystick")
                    ?? FindChildRecursive(_hudRoot.transform, "Fixed Joystick")
                    ?? FindChildRecursive(_hudRoot.transform, "Floating Joystick")
                    ?? FindChildRecursive(_hudRoot.transform, "Dynamic Joystick")
                    ?? FindChildRecursive(_hudRoot.transform, "Virtual Joystick");
            if (mcTr != null) joystickGO = mcTr.gameObject;
        }
        if (joystickGO == null && canvas != null)
        {
            var mcTr = FindChildRecursive(canvas.transform, "MobileControls")
                    ?? FindChildRecursive(canvas.transform, "Joystick")
                    ?? FindChildRecursive(canvas.transform, "Fixed Joystick")
                    ?? FindChildRecursive(canvas.transform, "Floating Joystick")
                    ?? FindChildRecursive(canvas.transform, "Dynamic Joystick")
                    ?? FindChildRecursive(canvas.transform, "Virtual Joystick");
            if (mcTr != null) joystickGO = mcTr.gameObject;
        }
        if (joystickGO != null) joystickGO.SetActive(visible);

        ObjectiveHUDUI.Instance?.SetVisible(visible);
    }

    // -----------------------------------------------------------------------
    // Button handlers - Top Right
    // -----------------------------------------------------------------------

    private float _lastShopToggleTime = -1f;
    public void OnShopButtonPressed()
    {
        if (Time.unscaledTime - _lastShopToggleTime < 0.25f) return;
        _lastShopToggleTime = Time.unscaledTime;

        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("[UIManager] Shop pressed.");
        var sm = ShopManager.Instance ?? FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
        if (sm != null)
        {
            sm.gameObject.SetActive(true);
            sm.ToggleShop();
        }
    }

    private float _lastBestiaryToggleTime = -1f;
    public void OnBestiaryButtonPressed()
    {
        if (Time.unscaledTime - _lastBestiaryToggleTime < 0.25f) return;
        _lastBestiaryToggleTime = Time.unscaledTime;

        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("[UIManager] Bestiary pressed.");
        var bm = BestiaryManager.Instance ?? FindFirstObjectByType<BestiaryManager>(FindObjectsInactive.Include);
        if (bm != null)
        {
            bm.gameObject.SetActive(true);
            bm.ToggleBestiary();
        }
    }

    public void OnPauseButtonPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("[UIManager] Pause button clicked.");
        var pm = PauseMenuUI.Instance ?? FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (pm != null)
        {
            pm.gameObject.SetActive(true);
            pm.TogglePause();
        }
        else
        {
            var pauseGO = new GameObject("PauseMenuUI");
            var newPm = pauseGO.AddComponent<PauseMenuUI>();
            newPm.TogglePause();
        }
    }

    public void OpenZoneSelection()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (ZoneSelectionUI.Instance != null)
        {
            ZoneSelectionUI.Instance.OpenZoneSelection();
        }
        else
        {
            var prefab = Resources.Load<GameObject>("UI/ZoneSelectionUI")
                      ?? Resources.Load<GameObject>("Prefabs/UI/ZoneSelectionUI")
                      ?? Resources.Load<GameObject>("UI/ZoneSelectionCanvas");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                if (go.transform.parent == null) DontDestroyOnLoad(go);
                var ui = go.GetComponent<ZoneSelectionUI>() ?? go.GetComponentInChildren<ZoneSelectionUI>();
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
    }

    public void OpenTutorial()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.StartTutorial();
        }
        else
        {
            var tutGO = new GameObject("TutorialManager");
            var tm = tutGO.AddComponent<TutorialManager>();
            tm.StartTutorial();
        }
    }

    public void OnInteractButtonPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        if (scannerSystem == null) scannerSystem = ScannerSystem.Instance;
        if (scannerSystem != null) { scannerSystem.TryInteract(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Interact();
    }

    public void OnScanButtonPressed()
    {
        AudioManager.Instance?.PlayCameraShutter();
        if (scannerSystem == null) scannerSystem = ScannerSystem.Instance;
        if (scannerSystem != null) { scannerSystem.TryScan(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Scan();
    }

    // -----------------------------------------------------------------------
    // Button glow helpers
    // -----------------------------------------------------------------------

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        cg.interactable = true;
        cg.blocksRaycasts = true;
        return cg;
    }

    /// <summary>Tint and adjust opacity of the SCAN button (100% when locked/ready, 75% when unusable).</summary>
    public void ShowScanButton(bool active)
    {
        ValidateAndRepairHUDButtonReferences();
        if (scanButtonImage != null)
        {
            if (_scanCanvasGroup == null || _scanCanvasGroup.gameObject != scanButtonImage.gameObject)
            {
                _scanCanvasGroup = GetOrAddCanvasGroup(scanButtonImage.gameObject);
            }
            if (_scanCanvasGroup != null)
            {
                _scanCanvasGroup.alpha = active ? usableButtonOpacity : unusableButtonOpacity;
            }

            var targetColor = active ? BtnActiveIcon : BtnDefault;
            scanButtonImage.color = active ? BtnActiveRing : BtnDefault;
            for (int i = 0; i < scanButtonImage.transform.childCount; i++)
            {
                var child = scanButtonImage.transform.GetChild(i);
                if (child.name == "InnerGlow") continue;
                var childImg = child.GetComponent<Image>();
                if (childImg != null) childImg.color = targetColor;
            }

            var btn = scanButtonImage.GetComponent<Button>();
            if (btn != null)
            {
                var cb = btn.colors;
                cb.normalColor = scanButtonImage.color;
                cb.highlightedColor = BtnActiveIcon;
                cb.pressedColor = BtnActiveIcon;
                cb.selectedColor = scanButtonImage.color;
                btn.colors = cb;
            }
        }
        if (_scanInnerGlow == null && scanButtonImage != null)
        {
            var glowTr = scanButtonImage.transform.Find("InnerGlow");
            if (glowTr != null) _scanInnerGlow = glowTr.GetComponent<Image>();
            else _scanInnerGlow = EnsureInnerGlow(scanButtonImage.gameObject, 0.85f);
        }
        if (_scanInnerGlow != null)
        {
            _scanInnerGlow.color = active ? InnerGlowActive : InnerGlowIdle;
        }
    }

    /// <summary>Tint and adjust opacity of the INTERACT button (100% when near debris/target, 75% when unusable).</summary>
    public void ShowInteractButton(bool active)
    {
        ValidateAndRepairHUDButtonReferences();
        if (interactButtonImage != null)
        {
            if (_interactCanvasGroup == null || _interactCanvasGroup.gameObject != interactButtonImage.gameObject)
            {
                _interactCanvasGroup = GetOrAddCanvasGroup(interactButtonImage.gameObject);
            }
            if (_interactCanvasGroup != null)
            {
                _interactCanvasGroup.alpha = active ? usableButtonOpacity : unusableButtonOpacity;
            }

            var targetColor = active ? BtnActiveIcon : BtnDefault;
            interactButtonImage.color = active ? BtnActiveRing : BtnDefault;
            for (int i = 0; i < interactButtonImage.transform.childCount; i++)
            {
                var child = interactButtonImage.transform.GetChild(i);
                if (child.name == "InnerGlow") continue;
                var childImg = child.GetComponent<Image>();
                if (childImg != null) childImg.color = targetColor;
            }

            var btn = interactButtonImage.GetComponent<Button>();
            if (btn != null)
            {
                var cb = btn.colors;
                cb.normalColor = interactButtonImage.color;
                cb.highlightedColor = BtnActiveIcon;
                cb.pressedColor = BtnActiveIcon;
                cb.selectedColor = interactButtonImage.color;
                btn.colors = cb;
            }
        }
        if (_interactInnerGlow == null && interactButtonImage != null)
        {
            var glowTr = interactButtonImage.transform.Find("InnerGlow");
            if (glowTr != null) _interactInnerGlow = glowTr.GetComponent<Image>();
            else _interactInnerGlow = EnsureInnerGlow(interactButtonImage.gameObject, 0.85f);
        }
        if (_interactInnerGlow != null)
        {
            _interactInnerGlow.color = active ? InnerGlowActive : InnerGlowIdle;
        }
    }

    /// <summary>Tint and adjust opacity of the LIGHT button (100% and cyan when lights ON, 75% and dark blue when lights OFF).</summary>
    public void ShowLightButton(bool active)
    {
        ValidateAndRepairHUDButtonReferences();
        if (lightButtonImage != null)
        {
            if (_lightCanvasGroup == null || _lightCanvasGroup.gameObject != lightButtonImage.gameObject)
            {
                _lightCanvasGroup = GetOrAddCanvasGroup(lightButtonImage.gameObject);
            }
            if (_lightCanvasGroup != null)
            {
                _lightCanvasGroup.alpha = active ? usableButtonOpacity : unusableButtonOpacity;
            }

            var targetColor = active ? BtnActiveIcon : BtnDefault;
            lightButtonImage.color = active ? BtnActiveRing : BtnDefault;
            for (int i = 0; i < lightButtonImage.transform.childCount; i++)
            {
                var child = lightButtonImage.transform.GetChild(i);
                if (child.name == "InnerGlow") continue;
                var childImg = child.GetComponent<Image>();
                if (childImg != null) childImg.color = targetColor;
            }

            var btn = lightButtonImage.GetComponent<Button>();
            if (btn != null)
            {
                var cb = btn.colors;
                cb.normalColor = lightButtonImage.color;
                cb.highlightedColor = BtnActiveIcon;
                cb.pressedColor = BtnActiveIcon;
                cb.selectedColor = lightButtonImage.color;
                btn.colors = cb;
            }
        }
        if (_lightInnerGlow == null && lightButtonImage != null)
        {
            var glowTr = lightButtonImage.transform.Find("InnerGlow");
            if (glowTr != null) _lightInnerGlow = glowTr.GetComponent<Image>();
            else _lightInnerGlow = EnsureInnerGlow(lightButtonImage.gameObject, 0.85f);
        }
        if (_lightInnerGlow != null)
        {
            _lightInnerGlow.color = active ? InnerGlowActive : InnerGlowIdle;
        }
    }

    public void OnLightButtonPressed()
    {
        ToggleSubmarineLights();
    }

    public void ShowScanPrompt(bool visible) => ShowScanButton(visible);

    /// <summary>
    /// Deactivates any minigame HUDs or modal popups that may have been left active in the prefab,
    /// ensuring only the main exploration HUD is visible during standard gameplay.
    /// </summary>
    public void HideMinigamesAndModals()
    {
        // 0. Abort and clean up any active minigames
        MinigameManager.Instance?.CancelActiveMinigame();

        // 1. Destroy lingering procedural Minigame 1 & 2 panels (ReconMinigame / CaptureMinigame)
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && (t.name == "ReconMinigame" || t.name == "CaptureMinigame"))
            {
                Destroy(t.gameObject);
            }
        }

        // 2. Minigame 3 (Environmental Cleanup)
        var mg3s = FindObjectsByType<EnvironmentalCleanupMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mg in mg3s)
        {
            if (mg.customUIRoot != null) mg.customUIRoot.SetActive(false);
            mg.gameObject.SetActive(false);
        }

        // 3. Minigame 4 (Hazard Dodge)
        var mg4s = FindObjectsByType<HazardDodgeMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mg in mg4s)
        {
            if (mg.customUIRoot != null) mg.customUIRoot.SetActive(false);
            mg.gameObject.SetActive(false);
        }

        // 3. Close open modals via their managers (leaving the manager GameObjects active)
        ShopManager.Instance?.HideShop();
        BestiaryManager.Instance?.HideBestiary();
        PauseMenuUI.Instance?.ResumeGame();
        ZoneSelectionUI.Instance?.CloseZoneSelection();
        ZoneBoundaryPopupUI.Instance?.HideAll();
        if (FactCardUI.Instance != null) FactCardUI.Instance.Hide();

        // 4. Ensure inner popup panels are deactivated on startup
        var root = transform.root;
        string[] innerPanels = { "ShopPanel", "DetailModal", "BestiaryPanel", "ZoneSelectionPanel", "PausePanel", "CardPanel", "FactCardPanel", "BoundaryPopupPanel", "HullWarningPanel" };
        foreach (var name in innerPanels)
        {
            var t = FindChildRecursive(root, name);
            if (t != null && t.gameObject != gameObject)
            {
                t.gameObject.SetActive(false);
            }
        }

        // 5. Clean up any stray MainMenu overwrite/exit modals or boundary popups that could have attached to PersistentCanvasUI
        var orphanModal = FindChildRecursive(root, "OverwriteConfirmModal");
        if (orphanModal != null)
        {
            Destroy(orphanModal.gameObject);
        }
        var orphanExitModal = FindChildRecursive(root, "ExitConfirmModal");
        if (orphanExitModal != null)
        {
            Destroy(orphanExitModal.gameObject);
        }
        var orphanPauseExitModal = FindChildRecursive(root, "PauseExitConfirmModal");
        if (orphanPauseExitModal != null)
        {
            Destroy(orphanPauseExitModal.gameObject);
        }
        var orphanBoundary = FindChildRecursive(root, "BoundaryPopup_Auto");
        if (orphanBoundary != null)
        {
            Destroy(orphanBoundary.gameObject);
        }
        var orphanHull = FindChildRecursive(root, "HullWarning_Auto");
        if (orphanHull != null)
        {
            Destroy(orphanHull.gameObject);
        }
    }

    /// <summary>
    /// Validates that scanButtonImage is assigned to the Camera button (ScanOuterRing)
    /// and interactButtonImage is assigned to the Hand button (InteractButton).
    /// If references are cross-wired, inverted, or missing, repairs them automatically.
    /// </summary>
    public void ValidateAndRepairHUDButtonReferences()
    {
        var root = transform.root;

        // Check if scanButtonImage is misassigned to Interact button
        bool scanIsInteract = scanButtonImage != null && (
            scanButtonImage.gameObject.name.IndexOf("Interact", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (scanButtonImage.sprite != null && scanButtonImage.sprite.name == "buttons-prototype_8")
        );

        // Check if interactButtonImage is misassigned to Scan button
        bool interactIsScan = interactButtonImage != null && (
            interactButtonImage.gameObject.name.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0 ||
            interactButtonImage.transform.Find("Scan") != null
        );

        if (scanIsInteract && interactIsScan)
        {
            var temp = scanButtonImage;
            scanButtonImage = interactButtonImage;
            interactButtonImage = temp;
        }
        else
        {
            if (scanIsInteract || scanButtonImage == null)
            {
                var scanT = FindChildRecursive(root, "ScanOuterRing") ?? FindChildRecursive(root, "ScanButton") ?? FindChildRecursive(root, "Scan");
                if (scanT != null)
                {
                    if (scanT.name == "Scan" && scanT.parent != null && scanT.parent.name.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0)
                        scanT = scanT.parent;
                    scanButtonImage = scanT.GetComponent<Image>() ?? scanT.GetComponentInChildren<Image>();
                }
            }

            if (interactIsScan || interactButtonImage == null)
            {
                var interactT = FindChildRecursive(root, "InteractButton") ?? FindChildRecursive(root, "InteractOuterRing") ?? FindChildRecursive(root, "Interact");
                if (interactT != null)
                {
                    interactButtonImage = interactT.GetComponent<Image>() ?? interactT.GetComponentInChildren<Image>();
                }
            }

            if (lightButtonImage == null)
            {
                var lightT = FindChildRecursive(root, "LightButton") ?? FindChildRecursive(root, "LightOuterRing") ?? FindChildRecursive(root, "BtnLight") ?? FindChildRecursive(root, "Light");
                if (lightT != null)
                {
                    lightButtonImage = lightT.GetComponent<Image>() ?? lightT.GetComponentInChildren<Image>();
                }
            }
        }
    }

    /// <summary>
    /// Explicitly binds click listeners for all HUD buttons to guarantee clickability,
    /// even if prefabs had their inspector UnityEvent references severed.
    /// </summary>
    public void BindHUDButtons()
    {
        ValidateAndRepairHUDButtonReferences();
        var root = transform.root;

        // 1. Scan Button (Camera icon / species scan minigame)
        Button scanBtn = null;
        if (scanButtonImage != null) scanBtn = scanButtonImage.GetComponent<Button>();
        if (scanBtn == null)
        {
            var btnTr = FindChildRecursive(root, "ScanOuterRing") ?? FindChildRecursive(root, "ScanButton") ?? FindChildRecursive(root, "Scan") ?? FindChildRecursive(root, "ScanBtn");
            if (btnTr != null)
            {
                var btnGO = btnTr.gameObject;
                if (btnGO.name == "Scan" && btnGO.transform.parent != null && btnGO.transform.parent.name.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0)
                    btnGO = btnGO.transform.parent.gameObject;
                scanButtonImage = btnGO.GetComponent<Image>() ?? btnGO.GetComponentInChildren<Image>();
                scanBtn = btnGO.GetComponent<Button>() ?? btnGO.GetComponentInChildren<Button>();
            }
        }
        if (scanBtn != null)
        {
            scanBtn.onClick.RemoveListener(OnScanButtonPressed);
            scanBtn.onClick.AddListener(OnScanButtonPressed);

            var trigger = scanButtonImage.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = scanButtonImage.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var pDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            pDown.callback.AddListener((_) =>
            {
                if (_scanInnerGlow != null) _scanInnerGlow.color = InnerGlowActive;
                if (scanButtonImage != null)
                {
                    for (int i = 0; i < scanButtonImage.transform.childCount; i++)
                    {
                        var ch = scanButtonImage.transform.GetChild(i);
                        if (ch.name == "InnerGlow") continue;
                        var img = ch.GetComponent<Image>();
                        if (img != null) img.color = BtnActiveIcon;
                    }
                }
            });
            trigger.triggers.Add(pDown);

            var pUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            pUp.callback.AddListener((_) =>
            {
                if (_scanInnerGlow != null) _scanInnerGlow.color = InnerGlowIdle;
                if (scanButtonImage != null)
                {
                    for (int i = 0; i < scanButtonImage.transform.childCount; i++)
                    {
                        var ch = scanButtonImage.transform.GetChild(i);
                        if (ch.name == "InnerGlow") continue;
                        var img = ch.GetComponent<Image>();
                        if (img != null) img.color = BtnDefault;
                    }
                }
            });
            trigger.triggers.Add(pUp);
        }
        if (scanButtonImage != null)
        {
            // Remove any stray icon child that may have been created on ScanOuterRing during a miswired run
            var strayIcon = scanButtonImage.transform.Find("Icon");
            if (strayIcon != null && scanButtonImage.transform.Find("Scan") != null)
            {
                if (Application.isPlaying) Destroy(strayIcon.gameObject);
                else DestroyImmediate(strayIcon.gameObject);
            }

            RemoveGlowEffects(scanButtonImage.gameObject);
            _scanCanvasGroup = GetOrAddCanvasGroup(scanButtonImage.gameObject);
            _scanInnerGlow = EnsureInnerGlow(scanButtonImage.gameObject, 0.85f);
            if (_scanInnerGlow != null)
            {
                _scanInnerGlow.transform.SetSiblingIndex(0);
            }
            scanButtonImage.color = BtnDefault;
            for (int i = 0; i < scanButtonImage.transform.childCount; i++)
            {
                var child = scanButtonImage.transform.GetChild(i);
                if (child.name != "InnerGlow")
                {
                    RemoveGlowEffects(child.gameObject);
                    var cImg = child.GetComponent<Image>();
                    if (cImg != null) cImg.color = BtnDefault;
                }
            }
        }

        // 2. Interact Button (Hand icon / debris cleanup & landmark facts)
        Button interactBtn = null;
        if (interactButtonImage != null) interactBtn = interactButtonImage.GetComponent<Button>();
        if (interactBtn == null)
        {
            var btnTr = FindChildRecursive(root, "InteractButton") ?? FindChildRecursive(root, "InteractOuterRing") ?? FindChildRecursive(root, "Interact") ?? FindChildRecursive(root, "InteractBtn");
            if (btnTr != null)
            {
                var btnGO = btnTr.gameObject;
                interactButtonImage = btnGO.GetComponent<Image>() ?? btnGO.GetComponentInChildren<Image>();
                interactBtn = btnGO.GetComponent<Button>() ?? btnGO.GetComponentInChildren<Button>();
            }
        }
        if (interactBtn != null)
        {
            interactBtn.onClick.RemoveListener(OnInteractButtonPressed);
            interactBtn.onClick.AddListener(OnInteractButtonPressed);

            var trigger = interactButtonImage.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = interactButtonImage.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var pDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            pDown.callback.AddListener((_) =>
            {
                if (_interactInnerGlow != null) _interactInnerGlow.color = InnerGlowActive;
                if (interactButtonImage != null)
                {
                    for (int i = 0; i < interactButtonImage.transform.childCount; i++)
                    {
                        var ch = interactButtonImage.transform.GetChild(i);
                        if (ch.name == "InnerGlow") continue;
                        var img = ch.GetComponent<Image>();
                        if (img != null) img.color = BtnActiveIcon;
                    }
                }
            });
            trigger.triggers.Add(pDown);

            var pUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            pUp.callback.AddListener((_) =>
            {
                if (_interactInnerGlow != null) _interactInnerGlow.color = InnerGlowIdle;
                if (interactButtonImage != null)
                {
                    for (int i = 0; i < interactButtonImage.transform.childCount; i++)
                    {
                        var ch = interactButtonImage.transform.GetChild(i);
                        if (ch.name == "InnerGlow") continue;
                        var img = ch.GetComponent<Image>();
                        if (img != null) img.color = BtnDefault;
                    }
                }
            });
            trigger.triggers.Add(pUp);
        }
        if (interactButtonImage != null)
        {
            RemoveGlowEffects(interactButtonImage.gameObject);

            // Separate the hand icon into a child Icon so that InnerGlow is rendered strictly BEHIND the hand (like the camera)
            var ringSprite = GetOuterRingSprite();
            var iconTr = interactButtonImage.transform.Find("Icon");
            Image iconImg = null;
            if (iconTr != null)
            {
                iconImg = iconTr.GetComponent<Image>();
            }
            else
            {
                Sprite handSprite = null;
                if (interactButtonImage.sprite != null && interactButtonImage.sprite != ringSprite)
                {
                    handSprite = interactButtonImage.sprite;
                }
                if (handSprite == null)
                {
                    var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    foreach (var s in allSprites)
                    {
                        if (s.name == "buttons-prototype_8") { handSprite = s; break; }
                    }
                }

                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(interactButtonImage.transform, false);

                var rt = iconGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                iconImg = iconGO.GetComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                if (handSprite != null) iconImg.sprite = handSprite;
            }

            if (iconImg != null)
            {
                iconImg.color = BtnDefault;
            }

            // Set parent image to outer ring
            if (ringSprite != null)
            {
                interactButtonImage.sprite = ringSprite;
                interactButtonImage.type = Image.Type.Simple;
                interactButtonImage.preserveAspect = true;
            }
            interactButtonImage.color = BtnDefault;

            // Inner glow placed at index 0 (behind child Icon)
            _interactCanvasGroup = GetOrAddCanvasGroup(interactButtonImage.gameObject);
            _interactInnerGlow = EnsureInnerGlow(interactButtonImage.gameObject, 0.85f);
            if (_interactInnerGlow != null)
            {
                _interactInnerGlow.transform.SetSiblingIndex(0);
            }
            if (iconImg != null)
            {
                iconImg.transform.SetAsLastSibling();
            }

            for (int i = 0; i < interactButtonImage.transform.childCount; i++)
            {
                var child = interactButtonImage.transform.GetChild(i);
                if (child.name != "InnerGlow")
                {
                    RemoveGlowEffects(child.gameObject);
                    var cImg = child.GetComponent<Image>();
                    if (cImg != null) cImg.color = BtnDefault;
                }
            }
        }

        // 2b. Light Button (Submarine headlights toggle)
        Button lightBtn = null;
        if (lightButtonImage != null) lightBtn = lightButtonImage.GetComponent<Button>();
        if (lightBtn == null && lightButtonImage != null)
        {
            lightBtn = lightButtonImage.gameObject.AddComponent<Button>();
        }
        if (lightBtn == null)
        {
            var btnTr = FindChildRecursive(root, "LightButton") ?? FindChildRecursive(root, "LightOuterRing") ?? FindChildRecursive(root, "BtnLight") ?? FindChildRecursive(root, "Light");
            if (btnTr != null)
            {
                var btnGO = btnTr.gameObject;
                lightButtonImage = btnGO.GetComponent<Image>() ?? btnGO.GetComponentInChildren<Image>();
                lightBtn = btnGO.GetComponent<Button>() ?? btnGO.AddComponent<Button>();
            }
        }
        if (lightBtn != null)
        {
            lightBtn.onClick.RemoveListener(OnLightButtonPressed);
            lightBtn.onClick.AddListener(OnLightButtonPressed);

            var trigger = lightButtonImage.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = lightButtonImage.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var pDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            pDown.callback.AddListener((_) =>
            {
                if (_lightInnerGlow != null) _lightInnerGlow.color = InnerGlowActive;
                if (lightButtonImage != null)
                {
                    for (int i = 0; i < lightButtonImage.transform.childCount; i++)
                    {
                        var ch = lightButtonImage.transform.GetChild(i);
                        if (ch.name == "InnerGlow") continue;
                        var img = ch.GetComponent<Image>();
                        if (img != null) img.color = BtnActiveIcon;
                    }
                }
            });
            trigger.triggers.Add(pDown);

            var pUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            pUp.callback.AddListener((_) =>
            {
                bool isLightActive = SubmarineLightController.Instance != null && SubmarineLightController.Instance.AreLightsOn;
                ShowLightButton(isLightActive);
            });
            trigger.triggers.Add(pUp);
        }
        if (lightButtonImage != null)
        {
            RemoveGlowEffects(lightButtonImage.gameObject);

            // Separate the custom light logo into a child Icon so that InnerGlow is rendered strictly BEHIND the logo
            var ringSprite = GetOuterRingSprite();
            var iconTr = lightButtonImage.transform.Find("Icon");
            Image iconImg = null;
            if (iconTr != null)
            {
                iconImg = iconTr.GetComponent<Image>();
            }
            else
            {
                Sprite customLightSprite = null;
                if (lightButtonImage.sprite != null && lightButtonImage.sprite != ringSprite)
                {
                    customLightSprite = lightButtonImage.sprite;
                }

                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(lightButtonImage.transform, false);

                var rt = iconGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                iconImg = iconGO.GetComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                if (customLightSprite != null) iconImg.sprite = customLightSprite;
            }

            // Set parent image to outer ring
            if (ringSprite != null)
            {
                lightButtonImage.sprite = ringSprite;
                lightButtonImage.type = Image.Type.Simple;
                lightButtonImage.preserveAspect = true;
            }

            // Pre-attach CanvasGroup and Inner glow placed at index 0 (behind child Icon)
            _lightCanvasGroup = GetOrAddCanvasGroup(lightButtonImage.gameObject);
            _lightInnerGlow = EnsureInnerGlow(lightButtonImage.gameObject, 0.85f);
            if (_lightInnerGlow != null)
            {
                _lightInnerGlow.transform.SetSiblingIndex(0);
            }
            if (iconImg != null)
            {
                iconImg.transform.SetAsLastSibling();
            }

            for (int i = 0; i < lightButtonImage.transform.childCount; i++)
            {
                var child = lightButtonImage.transform.GetChild(i);
                if (child.name != "InnerGlow")
                {
                    RemoveGlowEffects(child.gameObject);
                }
            }
        }

        // 3. MoveOuterRing: soft inner gradient only (no outside blur), child matches ring
        var moveRingGO = GameObject.Find("MoveOuterRing") ?? FindChildRecursive(root, "MoveOuterRing")?.gameObject;
        if (moveRingGO != null)
        {
            RemoveGlowEffects(moveRingGO);
            var moveImg = moveRingGO.GetComponent<Image>();
            if (moveImg != null) moveImg.color = BtnDefault;
            _moveInnerGlow = EnsureInnerGlow(moveRingGO, 0.85f);

            for (int i = 0; i < moveRingGO.transform.childCount; i++)
            {
                var child = moveRingGO.transform.GetChild(i);
                if (child.name != "InnerGlow")
                {
                    RemoveGlowEffects(child.gameObject);
                    var childImg = child.GetComponent<Image>();
                    if (childImg != null) childImg.color = BtnDefault;
                }
            }
        }

        // 4. Top-Right Buttons Container: disable force expand so Inspector spacing controls distance manually
        var buttonsGO = GameObject.Find("Buttons") ?? FindChildRecursive(root, "Buttons")?.gameObject;
        if (buttonsGO != null)
        {
            var hlg = buttonsGO.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }

        // 5. Top-Right: Shop, Bestiary, Pause (Outer ring styling: circular disk, inner glow, centered icon)
        var shopGO = FindChildRecursive(root, "Shop")?.gameObject 
                     ?? FindChildRecursive(root, "ShopButton")?.gameObject 
                     ?? FindChildRecursive(root, "BtnShop")?.gameObject 
                     ?? GameObject.Find("Shop");
        SetupNavButtonLikeOuterRing(shopGO, OnShopButtonPressed);

        var bestiaryGO = FindChildRecursive(root, "Bestiary")?.gameObject 
                         ?? FindChildRecursive(root, "BestiaryButton")?.gameObject 
                         ?? FindChildRecursive(root, "BtnBestiary")?.gameObject 
                         ?? GameObject.Find("Bestiary");
        SetupNavButtonLikeOuterRing(bestiaryGO, OnBestiaryButtonPressed);

        var pauseGO = FindChildRecursive(root, "Pause")?.gameObject 
                      ?? FindChildRecursive(root, "PauseButton")?.gameObject 
                      ?? FindChildRecursive(root, "BtnPause")?.gameObject 
                      ?? GameObject.Find("Pause");
        SetupNavButtonLikeOuterRing(pauseGO, OnPauseButtonPressed);

        // 6. Apply idle colors and opacity
        ShowScanButton(false);
        ShowInteractButton(false);
        ShowLightButton(SubmarineLightController.Instance != null ? SubmarineLightController.Instance.AreLightsOn : true);
    }

    public static Sprite GetOrCreateInnerGlowSprite()
    {
        if (_cachedInnerGlowSprite != null) return _cachedInnerGlowSprite;

        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float radius = center - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (d <= radius)
                {
                    float t = d / radius; // 0 at center, 1 at edge
                    // Smooth inner radial gradient: transparent center, soft ambient glow towards rim
                    float glowIntensity = Mathf.Pow(t, 2.0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, glowIntensity));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        _cachedInnerGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _cachedInnerGlowSprite;
    }

    public static Image EnsureInnerGlow(GameObject parentGO, float sizeRatio = 0.85f, Color? glowColor = null)
    {
        if (parentGO == null) return null;
        var existingTr = parentGO.transform.Find("InnerGlow");
        Image glowImg = null;
        if (existingTr != null)
        {
            glowImg = existingTr.GetComponent<Image>();
        }
        else
        {
            var glowGO = new GameObject("InnerGlow", typeof(RectTransform), typeof(Image));
            glowGO.transform.SetParent(parentGO.transform, false);
            glowGO.transform.SetAsFirstSibling(); // Placed behind the icon!

            var rt = glowGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var parentRt = parentGO.GetComponent<RectTransform>();
            float w = parentRt != null && parentRt.rect.width > 0 ? parentRt.rect.width : 100f;
            float h = parentRt != null && parentRt.rect.height > 0 ? parentRt.rect.height : 100f;
            rt.sizeDelta = new Vector2(w * sizeRatio, h * sizeRatio);
            rt.anchoredPosition = Vector2.zero;

            glowImg = glowGO.GetComponent<Image>();
            glowImg.raycastTarget = false;
        }

        if (glowImg != null)
        {
            glowImg.sprite = GetOrCreateInnerGlowSprite();
            glowImg.color = glowColor ?? InnerGlowIdle;
        }
        return glowImg;
    }

    private void RemoveGlowEffects(GameObject go)
    {
        if (go == null) return;
        var outlines = go.GetComponents<UnityEngine.UI.Outline>();
        for (int i = outlines.Length - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(outlines[i]);
            else DestroyImmediate(outlines[i]);
        }
        var shadows = go.GetComponents<UnityEngine.UI.Shadow>();
        for (int i = shadows.Length - 1; i >= 0; i--)
        {
            if (!(shadows[i] is UnityEngine.UI.Outline))
            {
                if (Application.isPlaying) Destroy(shadows[i]);
                else DestroyImmediate(shadows[i]);
            }
        }
    }

    private static Sprite _cachedOuterRingSprite;
    public static Sprite GetOuterRingSprite()
    {
        if (_cachedOuterRingSprite != null && _cachedOuterRingSprite.name != "buttons-prototype_8") 
            return _cachedOuterRingSprite;

        _cachedOuterRingSprite = null;

        if (Instance != null && Instance.scanButtonImage != null && Instance.scanButtonImage.sprite != null && Instance.scanButtonImage.sprite.name != "buttons-prototype_8")
        {
            _cachedOuterRingSprite = Instance.scanButtonImage.sprite;
        }
        else
        {
            var scanRing = GameObject.Find("ScanOuterRing");
            if (scanRing != null)
            {
                var sImg = scanRing.GetComponent<Image>();
                if (sImg != null && sImg.sprite != null && sImg.sprite.name != "buttons-prototype_8")
                    _cachedOuterRingSprite = sImg.sprite;
            }
        }

        if (_cachedOuterRingSprite == null)
        {
            var moveRing = GameObject.Find("MoveOuterRing");
            if (moveRing != null)
            {
                var mImg = moveRing.GetComponent<Image>();
                if (mImg != null && mImg.sprite != null && mImg.sprite.name != "buttons-prototype_8")
                    _cachedOuterRingSprite = mImg.sprite;
            }
        }

        if (_cachedOuterRingSprite == null)
        {
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in allSprites)
            {
                if (s.name == "buttons-prototype_2")
                {
                    _cachedOuterRingSprite = s;
                    break;
                }
            }
        }

        return _cachedOuterRingSprite;
    }

    private void SetupNavButtonLikeOuterRing(GameObject buttonGO, UnityEngine.Events.UnityAction onClickAction)
    {
        if (buttonGO == null) return;

        RemoveGlowEffects(buttonGO);

        var btn = buttonGO.GetComponent<Button>() ?? buttonGO.GetComponentInChildren<Button>();
        if (btn == null) btn = buttonGO.AddComponent<Button>();
        btn.interactable = true;
        if (onClickAction != null)
        {
            btn.onClick.RemoveListener(onClickAction);
            btn.onClick.AddListener(onClickAction);
        }

        // 1. Resolve outer ring circular sprite
        var ringSprite = GetOuterRingSprite();

        var baseImg = buttonGO.GetComponent<Image>();

        // 2. Setup or preserve child Icon GameObject
        bool isShop = buttonGO.name.IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0;
        float iconScale = isShop ? 0.75f : 0.60f;

        var parentRt = buttonGO.GetComponent<RectTransform>();
        float w = parentRt != null && parentRt.rect.width > 0 ? parentRt.rect.width : 125f;
        float h = parentRt != null && parentRt.rect.height > 0 ? parentRt.rect.height : 125f;

        var iconTr = buttonGO.transform.Find("Icon");
        Image iconImg = null;
        if (iconTr != null)
        {
            iconImg = iconTr.GetComponent<Image>();
            var rt = iconTr.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(w * iconScale, h * iconScale);
                rt.anchoredPosition = Vector2.zero;
            }
        }
        else
        {
            Sprite iconSprite = null;
            if (baseImg != null && baseImg.sprite != ringSprite && (baseImg.sprite == null || baseImg.sprite.name != "buttons-prototype_8"))
            {
                iconSprite = baseImg.sprite;
            }

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(buttonGO.transform, false);

            var rt = iconGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w * iconScale, h * iconScale);
            rt.anchoredPosition = Vector2.zero;

            iconImg = iconGO.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null) iconImg.sprite = iconSprite;
        }

        // Safety: If iconImg was polluted with the hand or ring, recover proper icon
        if (iconImg != null && (iconImg.sprite == null || iconImg.sprite.name == "buttons-prototype_8" || iconImg.sprite == ringSprite || buttonGO.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            if (buttonGO.name.IndexOf("Bestiary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var s in allSprites) { if (s.name == "buttons-prototype_14") { iconImg.sprite = s; break; } }
            }
            else if (buttonGO.name.IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var s in allSprites) { if (s.name.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0 || s.name.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0 || s.name == "buttons-prototype_12") { iconImg.sprite = s; break; } }
            }
            else if (buttonGO.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                iconImg.sprite = GetPauseButtonSprite();
            }
        }

        if (buttonGO.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var pRt = buttonGO.GetComponent<RectTransform>();
            if (pRt != null) pRt.sizeDelta = new Vector2(150f, 150f);
        }

        // 3. Make base image the circular outer ring background
        if (baseImg != null)
        {
            if (ringSprite != null) baseImg.sprite = ringSprite;
            baseImg.color = BtnDefault;
            baseImg.type = Image.Type.Simple;
            baseImg.preserveAspect = true;
            baseImg.raycastTarget = true;
        }

        // Match icon color exactly to border (baseImg.color)
        Color borderColor = baseImg != null ? baseImg.color : BtnDefault;
        if (iconImg != null)
        {
            iconImg.preserveAspect = true;
            iconImg.color = borderColor;
        }

        // 4. Inner radial gradient glow
        var innerGlow = EnsureInnerGlow(buttonGO, 0.85f);
        if (innerGlow != null)
        {
            innerGlow.transform.SetSiblingIndex(0);
        }
        if (iconImg != null)
        {
            iconImg.transform.SetAsLastSibling();
        }

        // 5. Button transitions and interactive press/click feedback
        if (btn != null)
        {
            btn.targetGraphic = baseImg;
            ApplyNavButtonColors(btn);

            var trigger = buttonGO.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = buttonGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var pDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            pDown.callback.AddListener((_) =>
            {
                if (iconImg != null) iconImg.color = BtnActiveIcon;
                if (innerGlow != null) innerGlow.color = InnerGlowActive;
            });
            trigger.triggers.Add(pDown);

            var pUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            pUp.callback.AddListener((_) =>
            {
                if (iconImg != null) iconImg.color = (baseImg != null ? baseImg.color : borderColor);
                if (innerGlow != null) innerGlow.color = InnerGlowIdle;
            });
            trigger.triggers.Add(pUp);
        }
    }

    private void ApplyNavButtonColors(Button btn)
    {
        if (btn == null) return;
        var cb = btn.colors;
        cb.normalColor = BtnDefault;
        cb.highlightedColor = NavBtnDefault;
        cb.pressedColor = BtnActiveIcon;
        cb.selectedColor = BtnDefault;
        btn.colors = cb;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
            var deep = FindChildRecursive(child, name);
            if (deep != null) return deep;
        }
        return null;
    }

    private static Sprite _cachedPauseIconSprite;
    public static Sprite GetPauseButtonSprite()
    {
        if (_cachedPauseIconSprite != null && _cachedPauseIconSprite.name != "buttons-prototype_8" && _cachedPauseIconSprite.name != "buttons-prototype_2")
            return _cachedPauseIconSprite;

        // 1. Search for pause sprite asset in Resources
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "buttons-prototype 1_6" || s.name == "buttons-prototype_6")
            {
                _cachedPauseIconSprite = s;
                return _cachedPauseIconSprite;
            }
        }

        // 2. Fallback to existing pause icon child in scene if valid pause name
        var pauseGO = GameObject.Find("Pause") ?? GameObject.Find("PauseButton") ?? GameObject.Find("BtnPause");
        if (pauseGO != null)
        {
            var iconTr = pauseGO.transform.Find("Icon");
            if (iconTr != null)
            {
                var img = iconTr.GetComponent<Image>();
                if (img != null && img.sprite != null && !img.sprite.name.Contains("8") && !img.sprite.name.Contains("14"))
                {
                    _cachedPauseIconSprite = img.sprite;
                    return _cachedPauseIconSprite;
                }
            }
         
            var pImg = pauseGO.GetComponent<Image>();
            if (pImg != null && pImg.sprite != null && pImg.sprite != GetOuterRingSprite() && !pImg.sprite.name.Contains("8") && !pImg.sprite.name.Contains("14"))
            {
                _cachedPauseIconSprite = pImg.sprite;
                return _cachedPauseIconSprite;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the main exploration Canvas, strictly excluding PauseMenu_Canvas and popup/button canvases.
    /// </summary>
    public static Canvas GetExplorationCanvas()
    {
        if (Instance != null)
        {
            var c = Instance.GetComponentInParent<Canvas>() ?? Instance.GetComponent<Canvas>();
            if (c != null && IsValidRootCanvas(c)) return c;
        }

        var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (IsValidRootCanvas(c)) return c;
        }

        var inactiveCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in inactiveCanvases)
        {
            if (IsValidRootCanvas(c)) return c;
        }

        return null;
    }

    private static bool IsValidRootCanvas(Canvas c)
    {
        if (c == null) return false;
        string n = c.name;
        if (n.Contains("Pause") || n.Contains("Skip") || n.Contains("Arrow") || n.Contains("Pointer") 
            || n.Contains("Popup") || n.Contains("Warning") || n.Contains("Briefing"))
            return false;
        return c.renderMode == RenderMode.ScreenSpaceOverlay && c.GetComponent<CanvasScaler>() != null;
    }

    /// <summary>
    /// Creates and configures a Pause button on a minigame canvas / panel at the exact same screen position
    /// and style as the main exploration HUD pause button (Top-Right: -50px, -50px, size 150x150, outer ring + inner glow + pause icon matching Shop & Bestiary).
    /// </summary>
    public static GameObject CreateMinigamePauseButton(Transform parentTransform)
    {
        if (parentTransform == null) return null;

        var existing = parentTransform.Find("MinigamePauseButton");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
            var existingRt = existing.GetComponent<RectTransform>();
            if (existingRt != null) existingRt.sizeDelta = new Vector2(150f, 150f);
            return existing.gameObject;
        }

        var pauseGO = new GameObject("MinigamePauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        pauseGO.transform.SetParent(parentTransform, false);
        pauseGO.transform.SetAsLastSibling();

        var rt = pauseGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-50f, -50f);
        rt.sizeDelta = new Vector2(150f, 150f);

        var ringImg = pauseGO.GetComponent<Image>();
        var ringSprite = GetOuterRingSprite();
        if (ringSprite != null) ringImg.sprite = ringSprite;
        ringImg.color = BtnDefault;
        ringImg.type = Image.Type.Simple;
        ringImg.preserveAspect = true;
        ringImg.raycastTarget = true;

        var innerGlow = EnsureInnerGlow(pauseGO, 0.85f, InnerGlowIdle);
        if (innerGlow != null) innerGlow.transform.SetSiblingIndex(0);

        // Icon child matching HUD SetupNavButtonLikeOuterRing
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(pauseGO.transform, false);
        var iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(150f * 0.55f, 150f * 0.55f);
        iconRt.anchoredPosition = Vector2.zero;

        var iconImg = iconGO.GetComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.color = BtnDefault;

        Sprite pauseSprite = GetPauseButtonSprite();
        if (pauseSprite != null)
        {
            iconImg.sprite = pauseSprite;
        }
        else
        {
            Destroy(iconImg);
            var txt = iconGO.AddComponent<TextMeshProUGUI>();
            txt.text = "❚❚";
            txt.font = UIThemeManager.AlohaFont;
            txt.fontSize = 44f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = BtnDefault;
            txt.raycastTarget = false;
        }

        var btn = pauseGO.GetComponent<Button>();
        btn.targetGraphic = ringImg;

        var trigger = pauseGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var pDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
        pDown.callback.AddListener((_) =>
        {
            if (iconImg != null) iconImg.color = BtnActiveIcon;
            if (innerGlow != null) innerGlow.color = InnerGlowActive;
        });
        trigger.triggers.Add(pDown);

        var pUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
        pUp.callback.AddListener((_) =>
        {
            if (iconImg != null) iconImg.color = BtnDefault;
            if (innerGlow != null) innerGlow.color = InnerGlowIdle;
        });
        trigger.triggers.Add(pUp);

        btn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            var pm = PauseMenuUI.Instance ?? FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
            if (pm != null)
            {
                pm.gameObject.SetActive(true);
                pm.TogglePause();
            }
            else
            {
                var pgo = new GameObject("PauseMenuUI");
                var newPm = pgo.AddComponent<PauseMenuUI>();
                newPm.TogglePause();
            }
        });

        return pauseGO;
    }

    // -----------------------------------------------------------------------
    // Tutorial Target RectTransform Getters
    // -----------------------------------------------------------------------

    public RectTransform GetDepthGaugeRect()
    {
        if (depthLabel != null) return depthLabel.rectTransform;
        var go = GameObject.Find("Depth") ?? GameObject.Find("DepthGauge") ?? GameObject.Find("DepthLabel");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    public RectTransform GetSonarMapRect()
    {
        if (sonarMapImage != null) return sonarMapImage.rectTransform;
        var go = GameObject.Find("SonarMap") ?? GameObject.Find("Sonar") ?? GameObject.Find("SonarMapImage");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    public RectTransform GetRDPCounterRect()
    {
        if (rdpLabel != null) return rdpLabel.rectTransform;
        var go = GameObject.Find("RDP") ?? GameObject.Find("RDPLabel") ?? GameObject.Find("RDPCounter");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    public RectTransform GetScanButtonRect()
    {
        if (scanButtonImage != null) return scanButtonImage.rectTransform;
        var go = GameObject.Find("Scan") ?? GameObject.Find("ScanButton") ?? GameObject.Find("BtnScan");
        if (go != null) return go.GetComponent<RectTransform>();
        if (_hudRoot != null)
        {
            var tr = FindChildRecursive(_hudRoot.transform, "ScanButton") ?? FindChildRecursive(_hudRoot.transform, "BtnScan") ?? FindChildRecursive(_hudRoot.transform, "Scan");
            if (tr != null) return tr.GetComponent<RectTransform>();
        }
        return null;
    }

    public RectTransform GetInteractButtonRect()
    {
        if (interactButtonImage != null) return interactButtonImage.rectTransform;
        var go = GameObject.Find("Interact") ?? GameObject.Find("InteractButton") ?? GameObject.Find("BtnInteract");
        if (go != null) return go.GetComponent<RectTransform>();
        if (_hudRoot != null)
        {
            var tr = FindChildRecursive(_hudRoot.transform, "InteractButton") ?? FindChildRecursive(_hudRoot.transform, "BtnInteract") ?? FindChildRecursive(_hudRoot.transform, "Interact");
            if (tr != null) return tr.GetComponent<RectTransform>();
        }
        return null;
    }

    public RectTransform GetShopButtonRect()
    {
        var go = GameObject.Find("Shop") ?? GameObject.Find("ShopButton") ?? GameObject.Find("BtnShop");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    public RectTransform GetBestiaryButtonRect()
    {
        var go = GameObject.Find("Bestiary") ?? GameObject.Find("BestiaryButton") ?? GameObject.Find("BtnBestiary");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    public RectTransform GetLightButtonRect()
    {
        if (lightButtonImage != null) return lightButtonImage.rectTransform;
        var go = GameObject.Find("LightButton") ?? GameObject.Find("LightOuterRing") ?? GameObject.Find("BtnLight") ?? GameObject.Find("Light");
        if (go != null) return go.GetComponent<RectTransform>();
        if (_hudRoot != null)
        {
            var tr = FindChildRecursive(_hudRoot.transform, "LightButton") ?? FindChildRecursive(_hudRoot.transform, "LightOuterRing") ?? FindChildRecursive(_hudRoot.transform, "BtnLight") ?? FindChildRecursive(_hudRoot.transform, "Light");
            if (tr != null) return tr.GetComponent<RectTransform>();
        }
        return null;
    }

    /// <summary>
    /// Toggles the submarine's exterior headlights on/off. Can be hooked to a UI button.
    /// </summary>
    public void ToggleSubmarineLights()
    {
        if (SubmarineLightController.Instance != null)
        {
            SubmarineLightController.Instance.ToggleLights();
            ShowLightButton(SubmarineLightController.Instance.AreLightsOn);
        }
        else if (lightButtonImage != null)
        {
            bool isNowActive = lightButtonImage.color != BtnActiveRing;
            ShowLightButton(isNowActive);
        }
    }

    public RectTransform GetJoystickRect()
    {
        var go = GameObject.Find("MovementJoystick") ?? GameObject.Find("Joystick") ?? GameObject.Find("Floating Joystick") ?? GameObject.Find("Variable Joystick");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    private GameObject _hudSkipTutorialGO;

    public void SetHUDSkipTutorialVisible(bool visible, Action onSkipClicked = null)
    {
        if (visible)
        {
            if (_hudSkipTutorialGO == null)
            {
                var canvas = GetExplorationCanvas() ?? (GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>());
                if (canvas == null) return;

                // Middle-top placement to avoid overlapping any HUD icons
                _hudSkipTutorialGO = new GameObject("HUDSkipTutorialBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                _hudSkipTutorialGO.transform.SetParent(canvas.transform, false);
                var rt = _hudSkipTutorialGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(320f, 50f);
                rt.anchoredPosition = new Vector2(0f, -22f);

                // High sorting order so button remains visible and clickable over any minigame or overlay
                var btnCanvas = _hudSkipTutorialGO.AddComponent<Canvas>();
                btnCanvas.overrideSorting = true;
                btnCanvas.sortingOrder = 32000;
                var raycaster = _hudSkipTutorialGO.AddComponent<GraphicRaycaster>();
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

                var img = _hudSkipTutorialGO.GetComponent<Image>();
                img.color = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f); // #1f5f7d (matches PREV/NEXT buttons)

                var outline = _hudSkipTutorialGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.00f, 0.90f, 1.00f, 1.00f);
                outline.effectDistance = new Vector2(2f, -2f);

                var btnComp = _hudSkipTutorialGO.GetComponent<Button>();
                btnComp.targetGraphic = img;

                var colors = btnComp.colors;
                colors.normalColor = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f);
                colors.highlightedColor = new Color(0.18f, 0.48f, 0.62f, 1.00f);
                colors.pressedColor = new Color(0.08f, 0.26f, 0.35f, 1.00f);
                colors.selectedColor = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f);
                btnComp.colors = colors;

                var txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(_hudSkipTutorialGO.transform, false);
                var txtRt = txtGO.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;
                txtRt.anchoredPosition = Vector2.zero;

                var txt = txtGO.GetComponent<TextMeshProUGUI>();
                txt.font = UIThemeManager.AntoneFont;
                txt.fontStyle = FontStyles.Normal;
                txt.fontSize = 28f;
                txt.color = Color.white; // Pure white text
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false;
                txt.text = "SKIP TUTORIAL";
            }

            // Ensure colors are updated even if previously instantiated
            var existingImg = _hudSkipTutorialGO.GetComponent<Image>();
            if (existingImg != null) existingImg.color = new Color(0.1216f, 0.3725f, 0.4902f, 1.00f);
            var existingTxt = _hudSkipTutorialGO.GetComponentInChildren<TextMeshProUGUI>();
            if (existingTxt != null) existingTxt.color = Color.white;

            var btn = _hudSkipTutorialGO.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButtonClick();
                onSkipClicked?.Invoke();
            });

            _hudSkipTutorialGO.transform.SetAsLastSibling();
            _hudSkipTutorialGO.SetActive(true);
        }
        else
        {
            if (_hudSkipTutorialGO != null) _hudSkipTutorialGO.SetActive(false);
        }
    }
}
