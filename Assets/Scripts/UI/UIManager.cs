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
    // Inspector — HUD references
    // -----------------------------------------------------------------------

    [Header("Top-Left HUD")]
    [SerializeField] private TMP_Text depthLabel;       // "DEPTH  100 m"
    [SerializeField] private TMP_Text zoneNameLabel;    // "SUNLIGHT ZONE"

    [Header("Top-Right HUD")]
    [SerializeField] private TMP_Text rdpLabel;         // "RDP  150"

    [Header("Scan Prompt")]
    [Tooltip("Optional UI element shown when a species is in scanner range. Auto-created if null.")]
    [SerializeField] private GameObject scanPromptUI;

    [Header("References (auto-found)")]
    [SerializeField] private DepthTracker   depthTracker;
    [SerializeField] private ScannerSystem  scannerSystem;

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
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Interact();
    }

    public void OnScanButtonPressed()
    {
        // Delegate to ScannerSystem first (Phase 3 species scanning)
        if (scannerSystem == null) scannerSystem = FindFirstObjectByType<ScannerSystem>();
        if (scannerSystem != null) { scannerSystem.TryScan(); return; }

        // Fallback: original PlayerMovement.Scan() for non-species interactions
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.Scan();
    }

    // -----------------------------------------------------------------------
    // Scan prompt (shown when a species is in scanner range)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Show or hide the "TAP SCAN" prompt. Called by ScannerSystem each frame a target is detected.
    /// </summary>
    public void ShowScanPrompt(bool visible)
    {
        if (scanPromptUI == null) EnsureScanPromptUI();
        if (scanPromptUI != null) scanPromptUI.SetActive(visible);
    }

    private void EnsureScanPromptUI()
    {
        // Try to find an existing element tagged or named ScanPrompt
        var existing = GameObject.Find("ScanPrompt");
        if (existing != null) { scanPromptUI = existing; return; }

        // Auto-create a minimal scan prompt text at the top of the screen
        var canvas = GetComponentInParent<UnityEngine.Canvas>() ?? FindFirstObjectByType<UnityEngine.Canvas>();
        if (canvas == null) return;

        var go   = new GameObject("ScanPrompt", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.85f);
        rect.anchorMax        = new Vector2(0.5f, 0.85f);
        rect.sizeDelta        = new Vector2(220f, 40f);
        rect.anchoredPosition = Vector2.zero;

        var tmp       = go.AddComponent<TMP_Text>();
        tmp.text      = "<b>[ TAP SCAN ]</b>";
        tmp.fontSize  = 18;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color     = new UnityEngine.Color(0.20f, 0.95f, 0.75f, 1f);

        go.SetActive(false);
        scanPromptUI = go;
    }
}
