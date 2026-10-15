using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages visual upgrade parts on the 3D submarine model.
///
/// Inspector Setup:
///   1. Attach this component to the root of your Submarine preview model.
///   2. Assign child GameObjects to each category's tier array:
///        - Hull: Tiers 1 to 5
///        - Sonar: Tiers 1 to 5
///        - Scanner: Tiers 1 to 5
///        - Engine: Tiers 1 to 5
///        - Lights: Tiers 1 to 5
///        - Utilities: Tiers 1 to 3
///   3. Any slot left unassigned (null) is safely ignored so you can populate parts incrementally.
///
/// Features:
///   - Automatically synchronizes with GameManager saved tiers when the shop opens.
///   - Reacts to OnUpgradePurchased to instantly swap part models with optional scale-pop feedback.
/// </summary>
public class SubmarineCustomizer : MonoBehaviour
{
    [Header("Core Base Model (Always Visible)")]
    [Tooltip("Optional core submarine body/cabin that remains visible across all tiers.")]
    [SerializeField] private GameObject baseSubmarineBody;

    [Header("1. Reinforced Hull Upgrades (Tiers 1 - 5)")]
    [Tooltip("GameObject for each Hull tier. Index 0 = Tier 1, Index 1 = Tier 2, etc.")]
    [SerializeField] private GameObject[] hullTiers = new GameObject[5];

    [Header("2. Sonar Upgrades (Tiers 1 - 5)")]
    [Tooltip("GameObject for each Sonar tier. Index 0 = Tier 1, Index 1 = Tier 2, etc.")]
    [SerializeField] private GameObject[] sonarTiers = new GameObject[5];

    [Header("3. Research Scanner Upgrades (Tiers 1 - 5)")]
    [Tooltip("GameObject for each Scanner tier. Index 0 = Tier 1, Index 1 = Tier 2, etc.")]
    [SerializeField] private GameObject[] scannerTiers = new GameObject[5];

    [Header("4. Engine Upgrades (Tiers 1 - 5)")]
    [Tooltip("GameObject for each Engine tier. Index 0 = Tier 1, Index 1 = Tier 2, etc.")]
    [SerializeField] private GameObject[] engineTiers = new GameObject[5];

    [Header("5. Lights Upgrades (Tiers 1 - 5)")]
    [Tooltip("GameObject for each Light tier. Index 0 = Tier 1, Index 1 = Tier 2, etc.")]
    [SerializeField] private GameObject[] lightTiers = new GameObject[5];

    [Header("6. Utilities Upgrades (Tiers 1 - 3)")]
    [Tooltip("GameObject for each Utilities tier. Index 0 = Tier 1, Index 1 = Tier 2, Index 2 = Tier 3.")]
    [SerializeField] private GameObject[] utilityTiers = new GameObject[3];

    [Header("Upgrade Visual Feedback")]
    [Tooltip("Play a subtle scale-pulse animation on the upgraded part.")]
    [SerializeField] private bool animateOnUpgrade = true;

    private readonly System.Collections.Generic.Dictionary<UpgradeCategory, Coroutine> _activeCoroutines = 
        new System.Collections.Generic.Dictionary<UpgradeCategory, Coroutine>();

    private void Awake()
    {
        if (baseSubmarineBody == null)
        {
            var child = transform.Find("Submarine_Model");
            if (child != null) baseSubmarineBody = child.gameObject;
        }
    }

    private void Start()
    {
        RefreshAllFromGameManager();
    }

    private void OnEnable()
    {
        RefreshAllFromGameManager();
    }

    /// <summary>
    /// Reads all 6 upgrade tiers from GameManager and updates active 3D parts.
    /// </summary>
    public void RefreshAllFromGameManager()
    {
        int hullTier    = GameManager.Instance != null ? GameManager.Instance.HullTier : 1;
        int sonarTier   = GameManager.Instance != null ? GameManager.Instance.SonarTier : 1;
        int scannerTier = GameManager.Instance != null ? GameManager.Instance.ScannerTier : 1;
        int engineTier  = GameManager.Instance != null ? GameManager.Instance.EngineTier : 1;
        int lightTier   = GameManager.Instance != null ? GameManager.Instance.LightTier : 1;
        int utilTier    = GameManager.Instance != null ? GameManager.Instance.UtilitiesTier : 1;

        SetCategoryVisual(UpgradeCategory.Hull, hullTier, animate: false);
        SetCategoryVisual(UpgradeCategory.Sonar, sonarTier, animate: false);
        SetCategoryVisual(UpgradeCategory.Scanner, scannerTier, animate: false);
        SetCategoryVisual(UpgradeCategory.Engine, engineTier, animate: false);
        SetCategoryVisual(UpgradeCategory.Lights, lightTier, animate: false);
        SetCategoryVisual(UpgradeCategory.Utilities, utilTier, animate: false);
    }

    /// <summary>
    /// Activates the GameObject corresponding to currentTier (1-indexed)
    /// and deactivates all other tiers in that category.
    /// If the target tier is unpopulated, falls back gracefully to the highest assigned tier below it.
    /// </summary>
    public void SetCategoryVisual(UpgradeCategory category, int currentTier, bool animate = true)
    {
        GameObject[] partsArray = GetPartsArray(category);
        if (partsArray == null || partsArray.Length == 0) return;

        int targetIndex = Mathf.Clamp(currentTier - 1, 0, partsArray.Length - 1);
        int activeIndex = -1;

        // If targetIndex is assigned, use it. Otherwise fall back to highest assigned tier below it.
        for (int i = targetIndex; i >= 0; i--)
        {
            if (partsArray[i] != null)
            {
                activeIndex = i;
                break;
            }
        }
        // If still not found, check if any slot in the array is assigned
        if (activeIndex == -1)
        {
            for (int i = 0; i < partsArray.Length; i++)
            {
                if (partsArray[i] != null) { activeIndex = i; break; }
            }
        }

        GameObject newlyActivatedGO = null;

        for (int i = 0; i < partsArray.Length; i++)
        {
            GameObject partGO = partsArray[i];
            if (partGO == null) continue;

            bool shouldBeActive = (i == activeIndex);
            if (shouldBeActive && !partGO.activeSelf)
            {
                newlyActivatedGO = partGO;
            }
            partGO.SetActive(shouldBeActive);
        }

        // If newly activated part and animation enabled, trigger smooth scale pulse
        if (animate && animateOnUpgrade && newlyActivatedGO != null && gameObject.activeInHierarchy)
        {
            if (_activeCoroutines.TryGetValue(category, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }
            _activeCoroutines[category] = StartCoroutine(PulsePartRoutine(newlyActivatedGO.transform));
        }
    }

    private GameObject[] GetPartsArray(UpgradeCategory category)
    {
        return category switch
        {
            UpgradeCategory.Hull      => hullTiers,
            UpgradeCategory.Sonar     => sonarTiers,
            UpgradeCategory.Scanner   => scannerTiers,
            UpgradeCategory.Engine    => engineTiers,
            UpgradeCategory.Lights    => lightTiers,
            UpgradeCategory.Utilities => utilityTiers,
            _                         => null
        };
    }

    private IEnumerator PulsePartRoutine(Transform partTransform)
    {
        if (partTransform == null) yield break;

        Vector3 originalScale = partTransform.localScale;
        Vector3 targetScale   = originalScale * 1.25f;
        float duration        = 0.20f;
        float elapsed         = 0f;

        // Pop out
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            partTransform.localScale = Vector3.LerpUnclamped(originalScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            yield return null;
        }

        // Return to original
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            partTransform.localScale = Vector3.LerpUnclamped(targetScale, originalScale, t);
            yield return null;
        }

        partTransform.localScale = originalScale;
    }

    /// <summary>
    /// Helper to assign part arrays programmatically or from Editor scripts.
    /// </summary>
    public void AssignCategoryTiers(UpgradeCategory category, GameObject[] parts)
    {
        switch (category)
        {
            case UpgradeCategory.Hull:      hullTiers = parts; break;
            case UpgradeCategory.Sonar:     sonarTiers = parts; break;
            case UpgradeCategory.Scanner:   scannerTiers = parts; break;
            case UpgradeCategory.Engine:    engineTiers = parts; break;
            case UpgradeCategory.Lights:    lightTiers = parts; break;
            case UpgradeCategory.Utilities: utilityTiers = parts; break;
        }
    }
}
