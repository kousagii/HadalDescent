using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all HUD elements in a zone scene.
///
/// HUD Layout (matches wireframes):
///   Top-Left  : Depth gauge label + Sonar minimap
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
    // Inspector â€” HUD references
    // -----------------------------------------------------------------------

    [Header("Top-Left HUD")]
    [SerializeField] private TMP_Text depthLabel;       // "DEPTH  100 m"
    [SerializeField] private TMP_Text zoneNameLabel;    // "SUNLIGHT ZONE"

    [Header("Top-Right HUD")]
    [SerializeField] private TMP_Text rdpLabel;         // "RDP  150"

    [Header("Bottom HUD Buttons")]
    [Tooltip("SCAN button Image — tinted cyan when a species is in the reticle.")]
    [SerializeField] private UnityEngine.UI.Image scanButtonImage;
    [Tooltip("INTERACT button Image — tinted cyan when near a stationary species.")]
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

        // Populate HUD immediately on scene load
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

    private void RefreshDepth()
    {
        if (depthTracker == null)
            depthTracker = FindFirstObjectByType<DepthTracker>();

        if (depthTracker == null) return;

        float depth = depthTracker.DisplayDepthMetres;

        if (depthLabel != null)
            depthLabel.text = $"DEPTH  {depth:0} m";
    }

    /// <summary>Call after any RDP change to update the counter.</summary>
    public void RefreshHUD()
    {
        int rdp = GameManager.Instance != null ? GameManager.Instance.RDP : 0;
        if (rdpLabel != null)
            rdpLabel.text = $"{rdp} RDP";

        int zone = ZoneManager.CurrentZoneIndex;
        if (zoneNameLabel != null && ZoneConfig.IsValidZone(zone))
            zoneNameLabel.text = ZoneConfig.Zones[zone].zoneName.ToUpper();
        else if (zoneNameLabel != null)
            zoneNameLabel.text = "SUNLIGHT ZONE";
    }

    // -----------------------------------------------------------------------
    // Button callbacks (wire these to your Canvas buttons in Inspector)
    // -----------------------------------------------------------------------

    public void OnShopButtonPressed()
    {
        Debug.Log("[UIManager] Shop opened.");
        // TODO: activate shop panel
    }

    public void OnBestiaryButtonPressed()
    {
        if (BestiaryManager.Instance != null)
            BestiaryManager.Instance.ToggleBestiary();
        else
            Debug.Log("[UIManager] Bestiary opened (BestiaryManager not in scene).");
    }

    public void OnPauseButtonPressed()
    {
        Debug.Log("[UIManager] Paused.");
        Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
    }

    public void OnInteractButtonPressed()
    {
        // Phase 4: route to ScannerSystem.TryInteract() for stationary species
        if (scannerSystem == null) scannerSystem = FindFirstObjectByType<ScannerSystem>();
        if (scannerSystem != null) { scannerSystem.TryInteract(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Interact();
    }

    public void OnScanButtonPressed()
    {
        if (scannerSystem == null) scannerSystem = FindFirstObjectByType<ScannerSystem>();
        if (scannerSystem != null) { scannerSystem.TryScan(); return; }
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Scan();
    }

    // -----------------------------------------------------------------------
    // Button glow helpers (called by ScannerSystem each frame)
    // -----------------------------------------------------------------------

    /// <summary>Tint the SCAN button cyan when a mobile species is in the reticle.</summary>
    public void ShowScanButton(bool active)
    {
        if (scanButtonImage != null)
            scanButtonImage.color = active ? BtnActive : BtnDefault;
    }

    /// <summary>Tint the INTERACT button cyan when near a stationary species.</summary>
    public void ShowInteractButton(bool active)
    {
        if (interactButtonImage != null)
            interactButtonImage.color = active ? BtnActive : BtnDefault;
    }

    // Legacy alias so any existing ScannerSystem calls to ShowScanPrompt still compile
    public void ShowScanPrompt(bool visible) => ShowScanButton(visible);
}

