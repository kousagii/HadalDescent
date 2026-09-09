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

    [Header("References (auto-found)")]
    [SerializeField] private DepthTracker   depthTracker;
    [SerializeField] private ScannerSystem  scannerSystem;

    private Image _scanInnerGlow;
    private Image _interactInnerGlow;
    private Image _moveInnerGlow;
    private static Sprite _cachedInnerGlowSprite;

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
                sonarMap = sonarMapImage.GetComponent<SonarMapUI>() ?? sonarMapImage.gameObject.AddComponent<SonarMapUI>();
            }
            else
            {
                var mapGO = GameObject.Find("SonarMap") ?? GameObject.Find("Map placeholder") ?? GameObject.Find("Map");
                if (mapGO != null)
                {
                    sonarMap = mapGO.GetComponent<SonarMapUI>() ?? mapGO.AddComponent<SonarMapUI>();
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

    public void RefreshDepth()
    {
        if (depthLabel == null || depthTracker == null) return;
        depthLabel.text = $"DEPTH  {depthTracker.DisplayDepthMetres:0} m";
    }

    public void RefreshHUD()
    {
        if (zoneNameLabel != null)
        {
            int z = ZoneManager.CurrentZoneIndex;
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

        var reticle = ScanReticleUI.Instance;
        if (reticle != null) reticle.SetVisible(visible);

        var dragZone = FindFirstObjectByType<TouchDragZone>();
        if (dragZone != null) dragZone.gameObject.SetActive(visible);

        var joystickGO = GameObject.Find("MobileControls")
                      ?? GameObject.Find("Joystick") 
                      ?? GameObject.Find("Fixed Joystick") 
                      ?? GameObject.Find("Floating Joystick") 
                      ?? GameObject.Find("Dynamic Joystick")
                      ?? GameObject.Find("Virtual Joystick");
        if (joystickGO != null) joystickGO.SetActive(visible);
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

    /// <summary>Tint the SCAN button when a target is ready to scan.</summary>
    public void ShowScanButton(bool active)
    {
        var targetColor = active ? BtnActiveIcon : BtnDefault;
        if (scanButtonImage != null)
        {
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
        if (_scanInnerGlow != null)
        {
            _scanInnerGlow.color = active ? InnerGlowActive : InnerGlowIdle;
        }
    }

    /// <summary>Tint the INTERACT button when near an interactive target or debris cluster.</summary>
    public void ShowInteractButton(bool active)
    {
        var targetColor = active ? BtnActiveIcon : BtnDefault;
        if (interactButtonImage != null)
        {
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
        if (_interactInnerGlow != null)
        {
            _interactInnerGlow.color = active ? InnerGlowActive : InnerGlowIdle;
        }
    }

    public void ShowScanPrompt(bool visible) => ShowScanButton(visible);

    /// <summary>
    /// Deactivates any minigame HUDs or modal popups that may have been left active in the prefab,
    /// ensuring only the main exploration HUD is visible during standard gameplay.
    /// </summary>
    public void HideMinigamesAndModals()
    {
        // 1. Minigame 3 (Environmental Cleanup)
        var mg3s = FindObjectsByType<EnvironmentalCleanupMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mg in mg3s)
        {
            if (mg.customUIRoot != null) mg.customUIRoot.SetActive(false);
            mg.gameObject.SetActive(false);
        }

        // 2. Minigame 4 (Hazard Dodge)
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

        // 5. Clean up any stray MainMenu overwrite modals or boundary popups that could have attached to PersistentCanvasUI
        var orphanModal = FindChildRecursive(root, "OverwriteConfirmModal");
        if (orphanModal != null)
        {
            Destroy(orphanModal.gameObject);
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
    /// Explicitly binds click listeners for all HUD buttons to guarantee clickability,
    /// even if prefabs had their inspector UnityEvent references severed.
    /// </summary>
    public void BindHUDButtons()
    {
        // 1. Scan Button
        Button scanBtn = null;
        if (scanButtonImage != null) scanBtn = scanButtonImage.GetComponent<Button>();
        if (scanBtn == null)
        {
            var btnGO = GameObject.Find("ScanOuterRing") ?? GameObject.Find("ScanButton") ?? GameObject.Find("Scan") ?? GameObject.Find("ScanBtn");
            if (btnGO != null)
            {
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
            RemoveGlowEffects(scanButtonImage.gameObject);
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

        // 2. Interact Button
        Button interactBtn = null;
        if (interactButtonImage != null) interactBtn = interactButtonImage.GetComponent<Button>();
        if (interactBtn == null)
        {
            var btnGO = GameObject.Find("InteractButton") ?? GameObject.Find("InteractOuterRing") ?? GameObject.Find("Interact") ?? GameObject.Find("InteractBtn");
            if (btnGO != null)
            {
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

        // Apply idle colors
        ShowScanButton(false);
        ShowInteractButton(false);

        // MoveOuterRing: soft inner gradient only (no outside blur), child matches ring
        var moveRingGO = GameObject.Find("MoveOuterRing");
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

        // 3. Top-Right Buttons Container: disable force expand so Inspector spacing controls distance manually
        var buttonsGO = GameObject.Find("Buttons");
        if (buttonsGO != null)
        {
            var hlg = buttonsGO.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }

        // 4. Top-Right: Shop, Bestiary, Pause (Outer ring styling: circular disk, inner glow, centered icon)
        var shopGO = GameObject.Find("Shop") ?? GameObject.Find("ShopButton") ?? GameObject.Find("BtnShop");
        SetupNavButtonLikeOuterRing(shopGO, OnShopButtonPressed);

        var bestiaryGO = GameObject.Find("Bestiary") ?? GameObject.Find("BestiaryButton") ?? GameObject.Find("BtnBestiary");
        SetupNavButtonLikeOuterRing(bestiaryGO, OnBestiaryButtonPressed);

        var pauseGO = GameObject.Find("Pause") ?? GameObject.Find("PauseButton") ?? GameObject.Find("BtnPause");
        SetupNavButtonLikeOuterRing(pauseGO, OnPauseButtonPressed);
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
        if (btn != null && onClickAction != null)
        {
            btn.onClick.RemoveListener(onClickAction);
            btn.onClick.AddListener(onClickAction);
        }

        // 1. Resolve outer ring circular sprite
        var ringSprite = GetOuterRingSprite();

        var baseImg = buttonGO.GetComponent<Image>();

        // 2. Setup or preserve child Icon GameObject
        var iconTr = buttonGO.transform.Find("Icon");
        Image iconImg = null;
        if (iconTr != null)
        {
            iconImg = iconTr.GetComponent<Image>();
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

            var parentRt = buttonGO.GetComponent<RectTransform>();
            float w = parentRt != null && parentRt.rect.width > 0 ? parentRt.rect.width : 125f;
            float h = parentRt != null && parentRt.rect.height > 0 ? parentRt.rect.height : 125f;
            rt.sizeDelta = new Vector2(w * 0.55f, h * 0.55f);
            rt.anchoredPosition = Vector2.zero;

            iconImg = iconGO.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null) iconImg.sprite = iconSprite;
        }

        // Safety: If iconImg was polluted with the hand or ring, recover proper icon
        if (iconImg != null && (iconImg.sprite == null || iconImg.sprite.name == "buttons-prototype_8" || iconImg.sprite == ringSprite))
        {
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            if (buttonGO.name.IndexOf("Bestiary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var s in allSprites) { if (s.name == "buttons-prototype_14") { iconImg.sprite = s; break; } }
            }
            else if (buttonGO.name.IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var s in allSprites) { if (s.name.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0 || s.name == "buttons-prototype_12") { iconImg.sprite = s; break; } }
            }
        }

        if (iconImg != null)
        {
            iconImg.color = BtnDefault;
        }

        // 3. Make base image the circular outer ring background
        if (baseImg != null)
        {
            if (ringSprite != null) baseImg.sprite = ringSprite;
            baseImg.color = BtnDefault;
            baseImg.type = Image.Type.Simple;
            baseImg.preserveAspect = true;
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
                if (iconImg != null) iconImg.color = BtnDefault;
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
}
