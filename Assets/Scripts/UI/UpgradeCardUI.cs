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
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text categoryTitleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text perkDescriptionText;
    [SerializeField] private Button   upgradeButton;
    [SerializeField] private TMP_Text buttonLabel;

    [Header("Tier Level Pips (1 to 5)")]
    [Tooltip("5 pip images representing Tier 1 to Tier 5.")]
    [SerializeField] private Image[] pipImages = new Image[5];

    [Header("Styling")]
    [SerializeField] private Color pipFilledColor  = new Color(0.12f, 0.95f, 0.78f, 1.0f); // Bright Cyan / Teal
    [SerializeField] private Color pipEmptyColor   = new Color(0.15f, 0.20f, 0.28f, 0.8f); // Dark Slate
    [SerializeField] private Color buttonCanAfford = new Color(0.08f, 0.75f, 0.68f, 1.0f);
    [SerializeField] private Color buttonCannot    = new Color(0.35f, 0.38f, 0.45f, 0.6f);

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

        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }

        if (categoryTitleText != null)
            categoryTitleText.text = title;

        bool isMax = currentTier >= maxTier;

        // Cost label
        if (costText != null)
        {
            costText.text = isMax ? "MAX TIER" : $"{nextCost:N0} RDP";
            costText.color = isMax ? new Color(0.95f, 0.80f, 0.20f) : (canAfford ? Color.white : new Color(1f, 0.40f, 0.40f));
        }

        // Description
        if (perkDescriptionText != null)
            perkDescriptionText.text = perkDesc;

        // Pips (Tier indicators)
        if (pipImages != null)
        {
            for (int i = 0; i < pipImages.Length; i++)
            {
                if (pipImages[i] == null) continue;
                pipImages[i].color = (i < currentTier) ? pipFilledColor : pipEmptyColor;
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
        Image[] pips)
    {
        categoryTitleText   = title;
        costText            = cost;
        perkDescriptionText = desc;
        upgradeButton       = btn;
        buttonLabel         = btnLbl;
        pipImages           = pips;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnButtonClicked);
        }
    }
}
