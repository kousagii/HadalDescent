using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component attached to an individual upgrade row/card in the Submarine Upgrade Shop.
/// Wire up serialized elements in the Inspector.
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main category logo / icon image (does not change between tiers).")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text categoryTitleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text perkDescriptionText;
    [SerializeField] private Button   upgradeButton;
    [SerializeField] private TMP_Text buttonLabel;

    [Header("Styling")]
    [SerializeField] private Color buttonCanAfford = new Color(0.08f, 0.75f, 0.68f, 1.0f);
    [SerializeField] private Color buttonCannot    = new Color(0.35f, 0.38f, 0.45f, 0.6f);

    [Header("Legacy Pips (Optional)")]
    [SerializeField] private Image[] pipImages = new Image[0];

    private UpgradeCategory _category;
    private Action<UpgradeCategory> _onUpgradeClicked;

    private void Awake()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnButtonClicked);
        }
    }

    /// <summary>
    /// Populate this card with data for the specified category.
    /// </summary>
    public void Setup(
        UpgradeCategory category,
        Sprite icon,
        string title,
        int currentTier,
        int maxTier,
        int nextCost,
        string perkDesc,
        bool canAfford,
        Action<UpgradeCategory> onUpgrade)
    {
        _category = category;
        _onUpgradeClicked = onUpgrade;

        // Category logo image (remains constant per category)
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = (icon != null);
        }

        bool isMax = currentTier >= maxTier;

        if (categoryTitleText != null)
            categoryTitleText.text = isMax ? $"{title} (MAX TIER)" : $"{title} (TIER {currentTier})";

        // Cost label
        if (costText != null)
        {
            costText.text = isMax ? "MAX TIER" : $"{nextCost:N0} RDP";
            costText.color = isMax ? new Color(0.95f, 0.80f, 0.20f) : (canAfford ? Color.white : new Color(1f, 0.40f, 0.40f));
        }

        // Description
        if (perkDescriptionText != null)
            perkDescriptionText.text = perkDesc;

        // Pips (Optional legacy indicators - only colored if populated)
        if (pipImages != null && pipImages.Length > 0)
        {
            for (int i = 0; i < pipImages.Length; i++)
            {
                if (pipImages[i] == null) continue;
                pipImages[i].color = (i < currentTier) ? new Color(0.12f, 0.95f, 0.78f, 1f) : new Color(0.15f, 0.20f, 0.28f, 0.8f);
            }
        }

        // Upgrade button
        if (upgradeButton != null)
        {
            upgradeButton.interactable = !isMax && canAfford;
            var btnImg = upgradeButton.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = isMax ? buttonCannot : (canAfford ? buttonCanAfford : buttonCannot);
        }

        if (buttonLabel != null)
            buttonLabel.text = isMax ? "MAXED" : "UPGRADE";
    }

    private void OnButtonClicked()
    {
        _onUpgradeClicked?.Invoke(_category);
    }

    /// <summary>
    /// Programmatic initialization helper for procedural shop generation.
    /// </summary>
    public void InjectReferences(
        TMP_Text title,
        TMP_Text cost,
        TMP_Text desc,
        Button btn,
        TMP_Text btnLbl,
        Image iconImg = null,
        Image[] pips = null)
    {
        categoryTitleText   = title;
        costText            = cost;
        perkDescriptionText = desc;
        upgradeButton       = btn;
        buttonLabel         = btnLbl;
        iconImage           = iconImg;
        pipImages           = pips ?? new Image[0];

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnButtonClicked);
        }
    }
}
