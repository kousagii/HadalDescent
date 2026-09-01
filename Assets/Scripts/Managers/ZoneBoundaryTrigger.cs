using UnityEngine;
using TMPro;

/// <summary>
/// Invisible trigger volume placed at the top and bottom of each zone scene.
/// When the submarine enters the trigger, a popup is displayed asking whether
/// to transition or stay.
///
/// Setup:
///   1. Create an empty GameObject named "BottomBoundary" at Y = -playableDepth.
///   2. Add a Box Collider (Is Trigger = true) that spans the full zone width/length.
///      Suggested size: (playableWidth, 5, playableLength)
///   3. Attach this script. Set isBottomBoundary = true.
///   4. Repeat for "TopBoundary" at Y = 5, isBottomBoundary = false.
///
/// The script checks hull-tier requirements before allowing descent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZoneBoundaryTrigger : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Tooltip("True = bottom of this zone (going deeper). False = top (going shallower/returning).")]
    [SerializeField] private bool isBottomBoundary = true;

    [Tooltip("Assign the UI popup GameObject that contains the confirm/cancel buttons.")]
    [SerializeField] private GameObject boundaryPopupUI;

    [Tooltip("TMP_Text inside the boundary popup that shows the message.")]
    [SerializeField] private TMP_Text boundaryPopupMessage;

    [Tooltip("Assign the UI popup that warns about hull tier being too low.")]
    [SerializeField] private GameObject hullWarningUI;

    [Tooltip("TMP_Text inside the hull warning popup.")]
    [SerializeField] private TMP_Text hullWarningMessage;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private bool _popupActive = false;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        if (boundaryPopupUI == null)
        {
            var found = GameObject.Find("BoundaryPopupPanel");
            if (found != null) boundaryPopupUI = found;
        }

        if (hullWarningUI == null)
        {
            var found = GameObject.Find("HullWarningPanel");
            if (found != null) hullWarningUI = found;
        }

        if (boundaryPopupUI != null)  boundaryPopupUI.SetActive(false);
        if (hullWarningUI   != null)  hullWarningUI.SetActive(false);

        // Ensure time is unpaused on scene start
        Time.timeScale = 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_popupActive) return;

        if (isBottomBoundary) HandleDescentBoundary();
        else                  HandleAscentBoundary();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        DismissAllPopups();
    }

    // -----------------------------------------------------------------------
    // Boundary logic
    // -----------------------------------------------------------------------

    private void HandleDescentBoundary()
    {
        int currentZone = ZoneManager.CurrentZoneIndex;
        int nextZone    = currentZone + 1;

        if (!ZoneConfig.IsValidZone(nextZone))
        {
            // Already at the deepest zone — no popup needed
            return;
        }

        ZoneDefinition next = ZoneConfig.Zones[nextZone];

        // --- Hull tier check ---
        int playerHullTier = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;
        if (playerHullTier < next.requiredHullTier)
        {
            ShowHullWarning(next);
            return;
        }

        // --- Requirements check: species discovered ---
        ZoneDefinition current = ZoneConfig.Zones[currentZone];
        int discovered   = GameManager.Instance != null
            ? GameManager.Instance.GetDiscoveredCountInZone(currentZone)
            : 0;
        float progress   = current.totalSpeciesCount > 0
            ? (float)discovered / current.totalSpeciesCount
            : 0f;

        if (progress < current.unlockThreshold)
        {
            ShowHullWarning(current, hullMet: true, speciesNotMet: true);
            return;
        }

        // --- All requirements met — show descent prompt ---
        ShowBoundaryPopup(
            $"Proceed to {next.zoneName}?",
            confirmAction: () => ZoneManager.Instance.LoadZone(nextZone, enteredFromAbove: true)
        );
    }

    private void HandleAscentBoundary()
    {
        int currentZone   = ZoneManager.CurrentZoneIndex;
        int previousZone  = currentZone - 1;

        if (!ZoneConfig.IsValidZone(previousZone))
        {
            // Already in shallowest zone — could load zone select instead
            ShowBoundaryPopup(
                "Return to Zone Selection?",
                confirmAction: () => ZoneManager.Instance.ReturnToZoneSelect()
            );
            return;
        }

        ZoneDefinition prev = ZoneConfig.Zones[previousZone];
        ShowBoundaryPopup(
            $"Return to {prev.zoneName}?",
            confirmAction: () => ZoneManager.Instance.LoadZone(previousZone, enteredFromAbove: false)
        );
    }

    // -----------------------------------------------------------------------
    // UI helpers (wire these up to your actual UI system)
    // -----------------------------------------------------------------------

    private System.Action _confirmAction;

    private void EnsureFallbackPopups()
    {
        if (boundaryPopupUI != null && hullWarningUI != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (boundaryPopupUI == null)
        {
            boundaryPopupUI = CreateSimplePopupPanel(canvas.transform, "BoundaryPopup_Auto", out boundaryPopupMessage, showCancel: true);
        }

        if (hullWarningUI == null)
        {
            hullWarningUI = CreateSimplePopupPanel(canvas.transform, "HullWarning_Auto", out hullWarningMessage, showCancel: false);
        }
    }

    private GameObject CreateSimplePopupPanel(Transform canvasTransform, string panelName, out TMP_Text textComponent, bool showCancel)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvasTransform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(560, 320);
        rect.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image img = panel.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.08f, 0.12f, 0.20f, 0.96f);

        GameObject textGO = new GameObject("MessageText", typeof(RectTransform));
        textGO.transform.SetParent(panel.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.06f, 0.32f);
        textRect.anchorMax = new Vector2(0.94f, 0.95f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        textComponent = tmp;

        CreateButton(panel.transform, "ConfirmBtn", "YES", new Vector2(showCancel ? -100f : 0f, -85f), () => OnConfirm());

        if (showCancel)
        {
            CreateButton(panel.transform, "CancelBtn", "NO", new Vector2(100f, -85f), () => OnCancel());
        }

        panel.SetActive(false);
        return panel;
    }

    private void CreateButton(Transform parent, string name, string labelText, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160, 54);
        rect.anchoredPosition = position;

        UnityEngine.UI.Image img = btnGO.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.2f, 0.45f, 0.7f, 1f);

        UnityEngine.UI.Button btn = btnGO.GetComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(onClick);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) txt.font = font;
        txt.text = labelText;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 36;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
    }

    private void ShowBoundaryPopup(string message, System.Action confirmAction)
    {
        EnsureFallbackPopups();
        _popupActive   = true;
        _confirmAction = confirmAction;

        if (boundaryPopupMessage != null) boundaryPopupMessage.text = message;
        if (boundaryPopupUI     != null) boundaryPopupUI.SetActive(true);

        Debug.Log($"[ZoneBoundary] Popup: {message}");
        PausePlayerInput(true);
    }

    private void ShowHullWarning(ZoneDefinition zone,
                                 bool hullMet = false, bool speciesNotMet = false)
    {
        EnsureFallbackPopups();
        _popupActive = true;

        string msg = speciesNotMet
            ? $"Discover at least {Mathf.CeilToInt(zone.unlockThreshold * 100f)}% of " +
              $"{zone.zoneName} species before descending."
            : $"Warning: Pressure Threshold Exceeded!\n" +
              $"Hull Tier {zone.requiredHullTier} required.\n" +
              $"Upgrade your Hull in the Shop.";

        if (hullWarningMessage != null) hullWarningMessage.text = msg;
        if (hullWarningUI      != null) hullWarningUI.SetActive(true);
        Debug.Log($"[ZoneBoundary] Warning: {msg}");
    }

    private void DismissAllPopups()
    {
        _popupActive = false;
        if (boundaryPopupUI != null) boundaryPopupUI.SetActive(false);
        if (hullWarningUI   != null) hullWarningUI.SetActive(false);
        PausePlayerInput(false);
    }

    private static void PausePlayerInput(bool pause)
    {
        // Pause/unpause the player's movement while popup is visible.
        // Swap this for your actual input-disable method when ready.
        Time.timeScale = pause ? 0f : 1f;
    }

    // -----------------------------------------------------------------------
    // Called by UI confirm / cancel buttons
    // -----------------------------------------------------------------------

    /// <summary>Call this from the Confirm button's OnClick event.</summary>
    public void OnConfirm()
    {
        PausePlayerInput(false);
        DismissAllPopups();
        _confirmAction?.Invoke();
    }

    /// <summary>Call this from the Cancel button's OnClick event.</summary>
    public void OnCancel()
    {
        DismissAllPopups();
    }
}
