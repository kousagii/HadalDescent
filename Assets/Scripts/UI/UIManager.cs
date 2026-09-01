using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    // -----------------------------------------------------------------------
    // Inspector - HUD references
    // -----------------------------------------------------------------------

    [Header("Top-Left HUD")]
    [SerializeField] private TMP_Text depthLabel;       // "DEPTH  100 m"
    [SerializeField] private TMP_Text zoneNameLabel;    // "SUNLIGHT ZONE"
    [SerializeField] private SonarMapUI sonarMap;       // Fixed circular Sonar Map

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (depthTracker == null)
            depthTracker = FindFirstObjectByType<DepthTracker>();

        if (scannerSystem == null)
            scannerSystem = FindFirstObjectByType<ScannerSystem>();

        if (sonarMap == null)
        {
            sonarMap = FindFirstObjectByType<SonarMapUI>();
            if (sonarMap == null)
            {
                var mapGO = GameObject.Find("Map placeholder") ?? GameObject.Find("SonarMap") ?? GameObject.Find("Map");
                if (mapGO != null)
                {
                    sonarMap = mapGO.AddComponent<SonarMapUI>();
                }
            }
        }

        // Auto-find Interact button if unassigned
        if (interactButtonImage == null)
        {
            var btnGO = GameObject.Find("Interact") ?? GameObject.Find("InteractBtn") ?? GameObject.Find("Interact Button") ?? GameObject.Find("InteractButton");
            if (btnGO != null)
            {
                interactButtonImage = btnGO.GetComponent<Image>();
                var btn = btnGO.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(OnInteractButtonPressed);
            }
        }

        // Auto-find Scan button if unassigned
        if (scanButtonImage == null)
        {
            var btnGO = GameObject.Find("Scan") ?? GameObject.Find("ScanBtn") ?? GameObject.Find("Scan Button") ?? GameObject.Find("ScanButton");
            if (btnGO != null)
            {
                scanButtonImage = btnGO.GetComponent<Image>();
                var btn = btnGO.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(OnScanButtonPressed);
            }
        }
        RefreshHUD();
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

        var canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
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

    public void OnShopButtonPressed()
    {
        Debug.Log("[UIManager] Shop pressed.");
        var sm = ShopManager.Instance;
        if (sm != null) sm.ToggleShop();
    }

    public void OnBestiaryButtonPressed()
    {
        Debug.Log("[UIManager] Bestiary pressed.");
        var bm = BestiaryManager.Instance;
        if (bm != null) bm.ToggleBestiary();
    }

    public void OnPauseButtonPressed()
    {
        Debug.Log("[UIManager] Pause button clicked.");
        if (PauseMenuUI.Instance != null)
        {
            PauseMenuUI.Instance.TogglePause();
        }
        else
        {
            var pauseGO = new GameObject("PauseMenuUI");
            var pm = pauseGO.AddComponent<PauseMenuUI>();
            pm.TogglePause();
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
}
