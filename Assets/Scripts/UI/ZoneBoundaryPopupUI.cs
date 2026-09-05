using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Zone Boundary Confirmation & Pressure Warning Popups in the Persistent UI.
/// 
/// Setup:
///   1. Attach this component to 'BoundaryPopupPanel' under Canvas (or to Canvas itself).
///   2. Wire up your custom panel, buttons, and message texts in the Inspector.
///   3. Triggers across all scenes automatically call ZoneBoundaryPopupUI.Instance.ShowPrompt().
/// </summary>
public class ZoneBoundaryPopupUI : MonoBehaviour
{
    public static ZoneBoundaryPopupUI Instance { get; private set; }

    [Header("Confirmation Prompt Panel")]
    [Tooltip("Panel containing the transition prompt (e.g. 'Proceed to Twilight Zone?').")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text   promptMessageText;
    [SerializeField] private Button     confirmButton;
    [SerializeField] private Button     cancelButton;

    [Header("Warning Panel (Hull Tier / Species Progress)")]
    [Tooltip("Optional warning panel when requirements are not met.")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text   warningMessageText;
    [SerializeField] private Button     warningCloseButton;
    [SerializeField] private Button     warningShopButton;

    private Action _onConfirm;
    private Action _onCancel;
    private Action _onShop;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoWireComponents();
        HideAll();
    }

    private void Start()
    {
        AutoWireComponents();
        HideAll();
    }

    private void AutoWireComponents()
    {
        if (promptPanel == null)
            promptPanel = gameObject;

        UIThemeManager.ApplyAlohaTheme(promptPanel);
        if (warningPanel != null) UIThemeManager.ApplyAlohaTheme(warningPanel);

        // Auto-find text if unassigned
        if (promptMessageText == null && promptPanel != null)
            promptMessageText = promptPanel.GetComponentInChildren<TMP_Text>(true);

        // Auto-find buttons if unassigned
        if (confirmButton == null && promptPanel != null)
        {
            var btnTr = promptPanel.transform.Find("ConfirmBtn") 
                     ?? promptPanel.transform.Find("YesButton") 
                     ?? promptPanel.transform.Find("Yes") 
                     ?? promptPanel.transform.Find("Confirm")
                     ?? promptPanel.transform.Find("Button (Confirm)");
            if (btnTr == null)
            {
                var buttons = promptPanel.GetComponentsInChildren<Button>(true);
                if (buttons.Length > 0) confirmButton = buttons[0];
            }
            else
            {
                confirmButton = btnTr.GetComponent<Button>();
            }
        }

        if (cancelButton == null && promptPanel != null)
        {
            var btnTr = promptPanel.transform.Find("CancelBtn") 
                     ?? promptPanel.transform.Find("NoButton") 
                     ?? promptPanel.transform.Find("No") 
                     ?? promptPanel.transform.Find("Cancel")
                     ?? promptPanel.transform.Find("Button (Cancel)");
            if (btnTr == null)
            {
                var buttons = promptPanel.GetComponentsInChildren<Button>(true);
                if (buttons.Length > 1) cancelButton = buttons[1];
            }
            else
            {
                cancelButton = btnTr.GetComponent<Button>();
            }
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (warningCloseButton != null)
        {
            warningCloseButton.onClick.RemoveAllListeners();
            warningCloseButton.onClick.AddListener(OnWarningCloseClicked);
        }

        if (warningShopButton != null)
        {
            warningShopButton.onClick.RemoveAllListeners();
            warningShopButton.onClick.AddListener(OnShopClicked);
        }
    }

    /// <summary>
    /// Displays the zone transition confirmation prompt with Yes/No callbacks.
    /// </summary>
    public void ShowPrompt(string message, Action onConfirm, Action onCancel = null)
    {
        _onConfirm = onConfirm;
        _onCancel  = onCancel;

        AutoWireComponents();

        if (promptMessageText != null)
        {
            promptMessageText.text = message;
            promptMessageText.lineSpacing = 10f;
            promptMessageText.paragraphSpacing = 10f;
        }

        if (promptPanel != null)
            promptPanel.SetActive(true);

        if (warningPanel != null)
            warningPanel.SetActive(false);

        Time.timeScale = 0f; // Pause gameplay while deciding
    }

    /// <summary>
    /// Displays warning when hull tier or species requirement is not met.
    /// </summary>
    public void ShowWarning(string warningMessage, Action onShop = null, bool isSpeciesWarning = false)
    {
        _onShop = onShop;
        AutoWireComponents();

        // Update action button label based on warning type
        string actionLabel = isSpeciesWarning ? "GO TO BESTIARY" : "GO TO SHOP";
        UpdateButtonLabel(warningShopButton, actionLabel);
        // Also update confirmButton label and click handler as fallback path
        if (warningPanel == null && confirmButton != null)
        {
            UpdateButtonLabel(confirmButton, actionLabel);
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnShopClicked);
        }

        if (warningPanel != null)
        {
            if (warningMessageText != null)
            {
                warningMessageText.text = warningMessage;
                warningMessageText.lineSpacing = 10f;
                warningMessageText.paragraphSpacing = 10f;
            }
            warningPanel.SetActive(true);
            if (promptPanel != null && promptPanel != gameObject) promptPanel.SetActive(false);
        }
        else if (promptPanel != null)
        {
            // Fallback to prompt panel if no separate warning panel exists
            if (promptMessageText != null)
            {
                promptMessageText.text = warningMessage;
                promptMessageText.lineSpacing = 10f;
                promptMessageText.paragraphSpacing = 10f;
            }
            promptPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    private static void UpdateButtonLabel(Button btn, string label)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) { tmp.text = label; return; }
        var legacy = btn.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = label;
    }

    public void HideAll()
    {
        if (promptPanel != null && promptPanel != gameObject)
            promptPanel.SetActive(false);
        else if (promptPanel == gameObject)
            gameObject.SetActive(false);

        if (warningPanel != null)
            warningPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    private void OnConfirmClicked()
    {
        HideAll();
        _onConfirm?.Invoke();
    }

    private void OnCancelClicked()
    {
        HideAll();
        _onCancel?.Invoke();
    }

    private void OnWarningCloseClicked()
    {
        HideAll();
        var triggers = FindObjectsByType<ZoneBoundaryTrigger>(FindObjectsSortMode.None);
        foreach (var t in triggers) t.PushPlayerAway();
    }

    private void OnShopClicked()
    {
        HideAll();
        if (_onShop != null)
        {
            _onShop.Invoke();
        }
        else
        {
            var triggers = FindObjectsByType<ZoneBoundaryTrigger>(FindObjectsSortMode.None);
            foreach (var t in triggers) t.PushPlayerAway();
            ShopManager.Instance?.ShowShop();
        }
    }
}
