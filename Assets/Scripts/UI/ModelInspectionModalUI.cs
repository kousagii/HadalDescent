using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to your custom 3D Model Inspection Modal prefab in Canvas.
///
/// Wiring:
///   1. Create your UI prefab in Canvas with a RawImage viewport, title text,
///      subtitle text, and close button.
///   2. Attach this component to the root GameObject of that prefab.
///   3. Drag-assign the Inspector fields below.
///   4. Drag the prefab into BestiaryManager → customModelInspectionPrefab.
///
/// At runtime, BestiaryManager.OpenModelInspectionModal() instantiates
/// this prefab and calls Setup(). The live 3D model renders into modelViewport
/// with interactive 360° drag rotation.
/// </summary>
public class ModelInspectionModalUI : MonoBehaviour
{
    [Header("Text Labels")]
    [Tooltip("Species name (discovered) or '??? UNCATALOGED SPECIMEN' (undiscovered).")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("Scientific name, class, and depth range subtitle.")]
    [SerializeField] private TMP_Text subtitleText;

    [Header("3D Model Viewport")]
    [Tooltip("RawImage where the live 3D model RenderTexture is displayed.")]
    [SerializeField] private RawImage modelViewport;

    [Header("Buttons")]
    [Tooltip("Close button that dismisses this modal.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Optional hint text (e.g. 'Touch and drag to spin 360°').")]
    [SerializeField] private TMP_Text hintText;

    /// <summary>
    /// Populates all UI elements and starts the live 3D preview.
    /// Called by BestiaryManager.OpenModelInspectionModal().
    /// </summary>
    public void Setup(SpeciesData data, bool isDiscovered)
    {
        if (data == null) return;

        // Title
        if (titleText != null)
        {
            titleText.text = isDiscovered
                ? data.commonName.ToUpper()
                : "??? UNCATALOGED SPECIMEN";
        }

        // Subtitle
        if (subtitleText != null)
        {
            subtitleText.text = isDiscovered
                ? $"<i>{data.scientificName}</i>  •  Depth: {data.depthRangeText}"
                : $"Class: {data.taxonomicClass}  •  Depth: {data.depthRangeText}";
        }

        // Hint
        if (hintText != null)
            hintText.text = "✦ Touch and drag to spin 360°";

        // 3D Model Preview
        if (modelViewport != null && ModelPreviewSystem.Instance != null)
        {
            ModelPreviewSystem.Instance.ShowPreview(data, isSilhouette: !isDiscovered, modelViewport);

            // Add drag rotator if not already present
            if (modelViewport.GetComponent<ThumbnailDragRotator>() == null)
                modelViewport.gameObject.AddComponent<ThumbnailDragRotator>();
        }

        // Close button
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                if (ModelPreviewSystem.Instance != null)
                    ModelPreviewSystem.Instance.ClearPreview();

                if (BestiaryManager.Instance != null)
                    BestiaryManager.Instance.CloseModelInspectionModal();
            });
        }
    }
}
