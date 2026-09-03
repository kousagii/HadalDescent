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

    private static readonly UnityEngine.Color BtnDefault = new UnityEngine.Color(0.15f, 0.20f, 0.28f, 0.90f);
    private static readonly UnityEngine.Color BtnActive  = new UnityEngine.Color(0.08f, 0.75f, 0.68f, 1.00f);

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

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

        HideMinigamesAndModals();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;

        // 1. Is this a menu scene or an exploration zone?
        bool isMenu = scene.name == "MainMenu" || scene.name == "SplashScreen" || scene.name == "ZoneSelect";
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
        var hudGO = GameObject.Find("HUD");
        if (hudGO != null)
        {
            hudGO.SetActive(visible);
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            var hudTr = canvas.transform.Find("HUD");
            if (hudTr != null) hudTr.gameObject.SetActive(visible);
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
        if (scannerSystem == null) scannerSystem = ScannerSystem.Instance;
        if (scannerSystem != null) { scannerSystem.TryInteract(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Interact();
    }

    public void OnScanButtonPressed()
    {
        if (scannerSystem == null) scannerSystem = ScannerSystem.Instance;
        if (scannerSystem != null) { scannerSystem.TryScan(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Scan();
    }

    // -----------------------------------------------------------------------
    // Button glow helpers
    // -----------------------------------------------------------------------

    /// <summary>Tint the SCAN button cyan when a mobile species is in the reticle.</summary>
    public void ShowScanButton(bool active)
    {
        if (scanButtonImage != null)
            scanButtonImage.color = active ? BtnActive : BtnDefault;
    }

    /// <summary>Tint the INTERACT button cyan when near a stationary species or debris cluster.</summary>
    public void ShowInteractButton(bool active)
    {
        if (interactButtonImage != null)
            interactButtonImage.color = active ? BtnActive : BtnDefault;
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
        string[] innerPanels = { "ShopPanel", "DetailModal", "BestiaryPanel", "ZoneSelectionPanel", "PausePanel", "CardPanel", "FactCardPanel" };
        foreach (var name in innerPanels)
        {
            var t = FindChildRecursive(root, name);
            if (t != null && t.gameObject != gameObject)
            {
                t.gameObject.SetActive(false);
            }
        }

        // 5. Clean up any stray MainMenu overwrite modals that could have attached to PersistentCanvasUI
        var orphanModal = FindChildRecursive(root, "OverwriteConfirmModal");
        if (orphanModal != null)
        {
            Destroy(orphanModal.gameObject);
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
            var btnGO = GameObject.Find("ScanButton") ?? GameObject.Find("Scan") ?? GameObject.Find("ScanBtn");
            if (btnGO != null)
            {
                scanButtonImage = btnGO.GetComponent<Image>();
                scanBtn = btnGO.GetComponent<Button>();
            }
        }
        if (scanBtn != null)
        {
            scanBtn.onClick.RemoveListener(OnScanButtonPressed);
            scanBtn.onClick.AddListener(OnScanButtonPressed);
        }

        // 2. Interact Button
        Button interactBtn = null;
        if (interactButtonImage != null) interactBtn = interactButtonImage.GetComponent<Button>();
        if (interactBtn == null)
        {
            var btnGO = GameObject.Find("InteractOuterRing") ?? GameObject.Find("Interact") ?? GameObject.Find("InteractBtn");
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
        }

        // 3. Top-Right: Shop
        var shopGO = GameObject.Find("Shop") ?? GameObject.Find("ShopButton") ?? GameObject.Find("BtnShop");
        if (shopGO != null)
        {
            var btn = shopGO.GetComponent<Button>() ?? shopGO.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnShopButtonPressed);
                btn.onClick.AddListener(OnShopButtonPressed);
            }
        }

        // 4. Top-Right: Bestiary
        var bestiaryGO = GameObject.Find("Bestiary") ?? GameObject.Find("BestiaryButton") ?? GameObject.Find("BtnBestiary");
        if (bestiaryGO != null)
        {
            var btn = bestiaryGO.GetComponent<Button>() ?? bestiaryGO.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnBestiaryButtonPressed);
                btn.onClick.AddListener(OnBestiaryButtonPressed);
            }
        }

        // 5. Top-Right: Pause
        var pauseGO = GameObject.Find("Pause") ?? GameObject.Find("PauseButton") ?? GameObject.Find("BtnPause");
        if (pauseGO != null)
        {
            var btn = pauseGO.GetComponent<Button>() ?? pauseGO.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnPauseButtonPressed);
                btn.onClick.AddListener(OnPauseButtonPressed);
            }
        }
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
