using UnityEngine;
using UnityEngine.UI;
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

    public static float _globalCooldownTimer = 3.0f; // Startup grace period preventing frame-1 popup
    private bool _popupActive = false;
    private GameObject _playerSub;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _globalCooldownTimer = 4.0f;
        _popupActive = false;
    }

    private void OnDestroy()
    {
        DismissAllPopups();
        if (boundaryPopupUI != null && boundaryPopupUI.name.Contains("_Auto"))
        {
            Destroy(boundaryPopupUI);
        }
        if (hullWarningUI != null && hullWarningUI.name.Contains("_Auto"))
        {
            Destroy(hullWarningUI);
        }
    }

    private void Start()
    {
        _globalCooldownTimer = 4.0f;
        _popupActive = false;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            var strayB = canvas.transform.Find("BoundaryPopup_Auto");
            if (strayB != null) Destroy(strayB.gameObject);
            var strayH = canvas.transform.Find("HullWarning_Auto");
            if (strayH != null) Destroy(strayH.gameObject);
        }

        var col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            int zoneIdx = ZoneManager.CurrentZoneIndex;
            if (ZoneConfig.IsValidZone(zoneIdx))
            {
                ZoneDefinition zone = ZoneConfig.Zones[zoneIdx];
                float w = Mathf.Max(zone.playableWidth * 2f, 1500f);
                float l = Mathf.Max(zone.playableLength * 2f, 1500f);

                if (isBottomBoundary)
                {
                    var tracker = FindFirstObjectByType<DepthTracker>();
                    float floorY = tracker != null ? tracker.ZoneBottomY : -145f;
                    transform.position = new Vector3(0f, floorY, 0f);
                    col.size = new Vector3(w, 35f, l);
                    col.center = Vector3.zero;
                }
                else
                {
                    // Strictly at or above surface Y >= 0 (thickness 4m, centered at Y=2)
                    transform.position = new Vector3(0f, 0f, 0f);
                    col.size = new Vector3(w, 4f, l);
                    col.center = new Vector3(0f, 2f, 0f);
                }
            }
        }

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

    private void Update()
    {
        if (_globalCooldownTimer > 0f)
        {
            _globalCooldownTimer -= Time.unscaledDeltaTime;
            return;
        }

        if (_popupActive) return;

        if (_playerSub == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) _playerSub = pm.gameObject;
        }

        var tracker = FindFirstObjectByType<DepthTracker>();
        if (tracker != null)
        {
            int currentZone = ZoneManager.CurrentZoneIndex;
            if (ZoneConfig.IsValidZone(currentZone))
            {
                ZoneDefinition zone = ZoneConfig.Zones[currentZone];

                if (isBottomBoundary)
                {
                    // STRICT 3m difference: only trigger if depth is within 3m of the bottom
                    if (tracker.CurrentDepth >= zone.displayDepthMax - 3f)
                    {
                        HandleDescentBoundary();
                    }
                }
                else
                {
                    // STRICT 3m difference: only trigger if depth is within 3m of the surface
                    if (tracker.CurrentDepth <= 3f)
                    {
                        HandleAscentBoundary();
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerSub == null) _playerSub = other.gameObject;
        if (_popupActive || _globalCooldownTimer > 0f) return;

        var tracker = FindFirstObjectByType<DepthTracker>();
        if (tracker == null) return;

        int currentZone = ZoneManager.CurrentZoneIndex;
        if (!ZoneConfig.IsValidZone(currentZone)) return;
        ZoneDefinition zone = ZoneConfig.Zones[currentZone];

        if (isBottomBoundary)
        {
            if (tracker.CurrentDepth >= zone.displayDepthMax - 3f)
            {
                HandleDescentBoundary();
            }
        }
        else
        {
            if (tracker.CurrentDepth <= 3f)
            {
                HandleAscentBoundary();
            }
        }
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
        if (_popupActive || _globalCooldownTimer > 0f) return;

        int currentZone = ZoneManager.CurrentZoneIndex;
        if (!ZoneConfig.IsValidZone(currentZone)) return;
        ZoneDefinition curZone = ZoneConfig.Zones[currentZone];

        var tracker = FindFirstObjectByType<DepthTracker>();
        if (tracker != null && tracker.CurrentDepth < curZone.displayDepthMax - 3f) return;

        int nextZone = currentZone + 1;

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
            confirmAction: () =>
            {
                if (ZoneManager.Instance != null)
                {
                    ZoneManager.Instance.LoadZone(nextZone, enteredFromAbove: true);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(next.sceneName);
                }
            }
        );
    }

    private void HandleAscentBoundary()
    {
        if (_popupActive || _globalCooldownTimer > 0f) return;

        var tracker = FindFirstObjectByType<DepthTracker>();
        // STRICT 3m difference: only trigger if depth is within 3m of the surface (depth <= 3m)
        if (tracker != null && tracker.CurrentDepth > 3f) return;

        int currentZone   = ZoneManager.CurrentZoneIndex;
        int previousZone  = currentZone - 1;

        if (!ZoneConfig.IsValidZone(previousZone))
        {
            // Already in shallowest zone (Sunlight Zone surface) — open Zone Selection UI!
            ShowBoundaryPopup(
                "Return to Zone Selection?",
                confirmAction: () =>
                {
                    if (ZoneSelectionUI.Instance != null)
                    {
                        ZoneSelectionUI.Instance.OpenZoneSelection();
                    }
                    else if (UIManager.Instance != null)
                    {
                        UIManager.Instance.OpenZoneSelection();
                    }
                    else if (ZoneManager.Instance != null)
                    {
                        ZoneManager.Instance.ReturnToZoneSelect();
                    }
                }
            );
            return;
        }

        ZoneDefinition prev = ZoneConfig.Zones[previousZone];
        ShowBoundaryPopup(
            $"Return to {prev.zoneName}?",
            confirmAction: () =>
            {
                if (ZoneManager.Instance != null)
                {
                    ZoneManager.Instance.LoadZone(previousZone, enteredFromAbove: false);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(prev.sceneName);
                }
            }
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
            var existing = canvas.transform.Find("BoundaryPopup_Auto");
            if (existing != null) Destroy(existing.gameObject);
            boundaryPopupUI = CreateSimplePopupPanel(canvas.transform, "BoundaryPopup_Auto", out boundaryPopupMessage, isWarning: false);
        }

        if (hullWarningUI == null)
        {
            var existingH = canvas.transform.Find("HullWarning_Auto");
            if (existingH != null) Destroy(existingH.gameObject);
            hullWarningUI = CreateSimplePopupPanel(canvas.transform, "HullWarning_Auto", out hullWarningMessage, isWarning: true);
        }
    }

    private GameObject CreateSimplePopupPanel(Transform canvasTransform, string panelName, out TMP_Text textComponent, bool isWarning)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvasTransform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(620, 360);
        rect.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image img = panel.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.04f, 0.08f, 0.16f, 0.98f);

        GameObject textGO = new GameObject("MessageText", typeof(RectTransform));
        textGO.transform.SetParent(panel.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.30f);
        textRect.anchorMax = new Vector2(0.95f, 0.95f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        textComponent = tmp;

        if (isWarning)
        {
            CreateButton(panel.transform, "ShopBtn", "GO TO SHOP 🛠️", new Vector2(-125f, -100f), () => OnWarningShopClicked(), 220f);
            CreateButton(panel.transform, "CancelBtn", "CANCEL", new Vector2(145f, -100f), () => OnCancel(), 160f);
        }
        else
        {
            CreateButton(panel.transform, "ConfirmBtn", "YES", new Vector2(-110f, -100f), () => OnConfirm(), 160f);
            CreateButton(panel.transform, "CancelBtn", "NO", new Vector2(110f, -100f), () => OnCancel(), 160f);
        }

        panel.SetActive(false);
        return panel;
    }

    private void CreateButton(Transform parent, string name, string labelText, Vector2 position, UnityEngine.Events.UnityAction onClick, float width = 160f)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/Poppins-Regular SDF")
                ?? Resources.Load<TMP_FontAsset>("Poppins-Regular SDF")
                ?? TMP_Settings.defaultFontAsset;

        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 54);
        rect.anchoredPosition = position;

        UnityEngine.UI.Image img = btnGO.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.12f, 0.45f, 0.7f, 1f);

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
        txt.fontSize = 24;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.raycastTarget = false;
    }

    public void PushPlayerAway()
    {
        PushPlayerFromActiveBoundary();
    }

    public static void PushPlayerFromActiveBoundary()
    {
        _globalCooldownTimer = 3.5f;

        var triggers = FindObjectsByType<ZoneBoundaryTrigger>(FindObjectsSortMode.None);
        foreach (var t in triggers)
        {
            t.DismissAllPopups();
        }

        var tracker = FindFirstObjectByType<DepthTracker>();
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm == null) return;
        var sub = pm.gameObject;
        var rb = sub.GetComponent<Rigidbody>();

        float topY = tracker != null ? tracker.ZoneTopY : 0f;
        float bottomY = tracker != null ? tracker.ZoneBottomY : -145f;
        float currentDepth = tracker != null ? tracker.CurrentDepth : 10f;

        int currentZone = ZoneManager.CurrentZoneIndex;
        float maxDepth = ZoneConfig.IsValidZone(currentZone) ? ZoneConfig.Zones[currentZone].displayDepthMax : 200f;

        if (currentDepth >= maxDepth - 10f)
        {
            // Push player upwards off the floor to safe depth (~185m, safely outside 3m threshold)
            float safeY = bottomY + 8f;
            sub.transform.position = new Vector3(sub.transform.position.x, safeY, sub.transform.position.z);
            if (rb != null)
            {
                rb.position = sub.transform.position;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Max(3f, rb.linearVelocity.y), rb.linearVelocity.z);
            }
        }
        else
        {
            // Push player downwards below the surface to safe depth (~8-10m, safely outside 3m threshold)
            float safeY = topY - 5f;
            sub.transform.position = new Vector3(sub.transform.position.x, safeY, sub.transform.position.z);
            if (rb != null)
            {
                rb.position = sub.transform.position;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Min(-3f, rb.linearVelocity.y), rb.linearVelocity.z);
            }
        }
    }

    public void OnWarningShopClicked()
    {
        DismissAllPopups();
        PushPlayerAway();
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ShowShop();
        }
        else
        {
            var sm = FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
            sm?.ShowShop();
        }
    }

    public void OnWarningBestiaryClicked()
    {
        DismissAllPopups();
        PushPlayerAway();
        BestiaryManager.OpenBestiary();
    }

    private void ShowBoundaryPopup(string message, System.Action confirmAction)
    {
        if (ZoneBoundaryPopupUI.Instance != null)
        {
            _popupActive = true;
            ZoneBoundaryPopupUI.Instance.ShowPrompt(message, () => {
                _popupActive = false;
                confirmAction?.Invoke();
            }, () => {
                _popupActive = false;
                PushPlayerAway();
            });
            return;
        }

        EnsureFallbackPopups();
        _popupActive   = true;
        _confirmAction = confirmAction;

        if (boundaryPopupMessage != null) boundaryPopupMessage.text = message;
        if (boundaryPopupUI     != null)
        {
            boundaryPopupUI.transform.SetAsLastSibling();
            boundaryPopupUI.SetActive(true);
        }

        Debug.Log($"[ZoneBoundary] Popup: {message}");
        PausePlayerInput(true);
    }

    private void ShowHullWarning(ZoneDefinition zone,
                                 bool hullMet = false, bool speciesNotMet = false)
    {
        string msg = speciesNotMet
            ? $"Discover at least {Mathf.CeilToInt(zone.unlockThreshold * 100f)}% of " +
              $"{zone.zoneName} species before descending."
            : $"Warning: Pressure Threshold Exceeded!\n" +
              $"Hull Tier {zone.requiredHullTier} required.\n" +
              $"Upgrade your Hull in the Shop.";

        if (ZoneBoundaryPopupUI.Instance != null)
        {
            _popupActive = true;
            if (speciesNotMet)
            {
                ZoneBoundaryPopupUI.Instance.ShowWarning(msg, () => {
                    _popupActive = false;
                    PushPlayerAway();
                    BestiaryManager.OpenBestiary();
                }, isSpeciesWarning: true);
            }
            else
            {
                ZoneBoundaryPopupUI.Instance.ShowWarning(msg, () => {
                    _popupActive = false;
                    PushPlayerAway();
                    ShopManager.Instance?.ShowShop();
                });
            }
            return;
        }

        EnsureFallbackPopups();
        _popupActive = true;

        if (hullWarningMessage != null) hullWarningMessage.text = msg;
        if (hullWarningUI      != null)
        {
            // Dynamically update the ShopBtn label and action based on warning type
            Transform shopBtnTr = hullWarningUI.transform.Find("ShopBtn");
            Button shopBtn = shopBtnTr != null ? shopBtnTr.GetComponent<Button>() : null;
            if (shopBtn == null)
            {
                var allBtns = hullWarningUI.GetComponentsInChildren<Button>(true);
                foreach (var b in allBtns)
                {
                    if (b.name.Contains("Shop") || b.name.Contains("Confirm") || !b.name.Contains("Cancel"))
                    {
                        shopBtn = b;
                        break;
                    }
                }
            }

            if (shopBtn != null)
            {
                var shopLabel = shopBtn.GetComponentInChildren<TMP_Text>(true);
                var legacyLabel = shopBtn.GetComponentInChildren<UnityEngine.UI.Text>(true);
                shopBtn.onClick.RemoveAllListeners();
                if (speciesNotMet)
                {
                    if (shopLabel != null) shopLabel.text = "GO TO BESTIARY";
                    if (legacyLabel != null) legacyLabel.text = "BESTIARY";
                    shopBtn.onClick.AddListener(() => OnWarningBestiaryClicked());
                }
                else
                {
                    if (shopLabel != null) shopLabel.text = "GO TO SHOP";
                    if (legacyLabel != null) legacyLabel.text = "GO TO SHOP";
                    shopBtn.onClick.AddListener(() => OnWarningShopClicked());
                }
            }

            hullWarningUI.transform.SetAsLastSibling();
            hullWarningUI.SetActive(true);
        }
        Debug.Log($"[ZoneBoundary] Warning: {msg}");
        PausePlayerInput(true);
    }

    public void DismissAllPopups()
    {
        _popupActive = false;
        if (ZoneBoundaryPopupUI.Instance != null) ZoneBoundaryPopupUI.Instance.HideAll();
        if (boundaryPopupUI != null) boundaryPopupUI.SetActive(false);
        if (hullWarningUI   != null) hullWarningUI.SetActive(false);

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            var autoB = canvas.transform.Find("BoundaryPopup_Auto");
            if (autoB != null) autoB.gameObject.SetActive(false);
            var autoH = canvas.transform.Find("HullWarning_Auto");
            if (autoH != null) autoH.gameObject.SetActive(false);
        }

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
        PushPlayerAway();
        _globalCooldownTimer = 4.0f;
        _confirmAction?.Invoke();
    }

    /// <summary>Call this from the Cancel button's OnClick event.</summary>
    public void OnCancel()
    {
        DismissAllPopups();
        PushPlayerAway();
    }
}
