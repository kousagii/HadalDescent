using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this component to your custom Zone Header UI Prefab in Unity Canvas.
///
/// Designed so you can style and size your text (zone title, depth range, percentage,
/// unlock badge) however you like in Canvas.
///
/// Supports either:
///   - Option A (Simple): A single 'ZoneTitle' text (auto-formats title + depth)
///                        and single 'Progress' text (auto-formats count + percentage).
///   - Option B (Split): Separate Title, Depth, Progress, Percent, and Badge text objects.
/// </summary>
public class ZoneHeaderUI : MonoBehaviour
{
    [Header("Text Elements (Assign in Inspector or leave for auto-wire)")]
    [Tooltip("Main zone title (e.g. 'SUNLIGHT ZONE'). If depthRangeText is unassigned, this will format as 'SUNLIGHT ZONE (0 – 200 m)'.")]
    [SerializeField] private TMP_Text zoneTitleText;

    [Tooltip("Optional separate depth range text (e.g. '(0 – 200 m)'). Leave empty if you want title + depth in one label.")]
    [SerializeField] private TMP_Text depthRangeText;

    [Tooltip("Discovery count text (e.g. '8 / 15 Discovered'). If percentageText is unassigned, this formats as '8 / 15 Discovered (53%)'.")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("Optional separate discovery percentage text (e.g. '53%'). Leave empty if you want progress + percentage in one label.")]
    [SerializeField] private TMP_Text percentageText;

    [Tooltip("Unlock requirement badge text (e.g. '✓ 50% UNLOCKED' or '15% MORE NEEDED').")]
    [SerializeField] private TMP_Text unlockBadgeText;

    [Header("Visual Elements")]
    [Tooltip("Background panel or left accent bar to tint with the zone's atmospheric ocean color.")]
    [SerializeField] private Image zoneAccentImage;

    private void Awake()
    {
    }

    /// <summary>
    /// Populates all data for this zone header.
    /// </summary>
    public void Setup(ZoneDefinition zone, int discoveredCount, int totalCount)
    {

        float pct = totalCount > 0 ? (float)discoveredCount / totalCount * 100f : 0f;
        int neededToUnlock = Mathf.Max(0, 50 - Mathf.RoundToInt(pct));
        bool isUnlocked = pct >= 50f;

        // 1. Zone Title & Depth Range Formatting (Prevents overlapping)
        if (zoneTitleText != null)
        {
            if (depthRangeText != null && depthRangeText != zoneTitleText && depthRangeText.gameObject != zoneTitleText.gameObject)
            {
                zoneTitleText.text = zone.zoneName.ToUpper();
                depthRangeText.text = $"({zone.displayDepthMin:0} – {zone.displayDepthMax:0} m)";
            }
            else
            {
                // Single unified label: title and depth together with proportional size
                zoneTitleText.text = $"<b>{zone.zoneName.ToUpper()}</b>  <size=85%><color=#a0d8ef>({zone.displayDepthMin:0} – {zone.displayDepthMax:0} m)</color></size>";
            }
        }
        else if (depthRangeText != null)
        {
            depthRangeText.text = $"<b>{zone.zoneName.ToUpper()}</b> ({zone.displayDepthMin:0} – {zone.displayDepthMax:0} m)";
        }

        // 2. Discovery Progress Count & Percentage Formatting
        if (progressText != null)
        {
            if (percentageText != null && percentageText != progressText && percentageText.gameObject != progressText.gameObject)
            {
                progressText.text = $"{discoveredCount} / {totalCount} Discovered";
                percentageText.text = $"{pct:0}%";
            }
            else
            {
                // Single unified progress label
                progressText.text = $"<b>{discoveredCount} / {totalCount}</b> Discovered <color=#88ccff>({pct:0}%)</color>";
            }
        }
        else if (percentageText != null)
        {
            percentageText.text = $"{pct:0}%";
        }

        // 3. Unlock Badge Text
        if (unlockBadgeText != null)
        {
            if (isUnlocked)
            {
                unlockBadgeText.text = "<color=#66ffbb><b>✓ 50% UNLOCKED</b></color>";
            }
            else
            {
                unlockBadgeText.text = $"<color=#ffcc44><b>{neededToUnlock}% MORE NEEDED</b></color>";
            }
        }

        // 4. Zone Theme Color Tint
        if (zoneAccentImage != null)
        {
            zoneAccentImage.color = new Color(zone.fogColor.r, zone.fogColor.g, zone.fogColor.b, 0.95f);
        }
    }

}