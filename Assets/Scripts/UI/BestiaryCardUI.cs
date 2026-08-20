using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Attach to DiscoveredCardPrefab and UndiscoveredCardPrefab.
///
/// Features:
///   - Left Thumbnail Box: Displays the 3D model via RawImage with interactive drag-to-rotate.
///   - Discovered Card: Clicking card opens Detail Modal with Real Biological Photo.
///   - Undiscovered Card: Displays hint/clue on card; clicking is locked (does not open Detail Modal).
///   - Smooth ScrollView Support: Card does not intercept vertical scroll gestures from parent ScrollRect.
/// </summary>
public class BestiaryCardUI : MonoBehaviour
{
    [Header("Text Labels")]
    [Tooltip("Primary title: Common Name or '??? [Uncataloged Specimen]'.")]
    public TMP_Text titleText;

    [Tooltip("Subtitle: Scientific Name or 'Class: Anthozoa'.")]
    public TMP_Text subtitleText;

    [Tooltip("Depth badge text (e.g. '📍 Depth: 0–35 m').")]
    public TMP_Text depthText;

    [Tooltip("Habitat description (discovered) or Search Clue (undiscovered).")]
    public TMP_Text habitatOrClueText;

    [Header("3D Model Thumbnail (RawImage)")]
    [Tooltip("Drag ThumbnailBox (with RawImage) here to display the 3D model.")]
    public RawImage thumbnailRawImage;

    [Header("Visual Elements")]
    [Tooltip("Optional 2D photo/sprite fallback (if not using 3D model).")]
    public Image thumbnailImage;

    [Tooltip("Left accent bar (Cyan for discovered, Slate/Gray for undiscovered).")]
    public Image accentImage;

    [Header("Interaction")]
    [Tooltip("Button on the card (enabled only for discovered cards).")]
    public Button cardButton;

    private SpeciesData _speciesData;
    private bool        _isDiscovered;

    private void Awake()
    {
        if (thumbnailRawImage == null) thumbnailRawImage = GetComponentInChildren<RawImage>();
        if (cardButton == null)        cardButton        = GetComponent<Button>();
    }

    public void Setup(SpeciesData data, bool isDiscovered, Action onCardClick)
    {
        _speciesData  = data;
        _isDiscovered = isDiscovered;

        if (cardButton == null) cardButton = GetComponent<Button>();

        if (isDiscovered)
        {
            if (cardButton != null)
            {
                cardButton.interactable = true;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(() => onCardClick?.Invoke());
            }

            if (titleText != null) titleText.text = data.commonName;
            if (subtitleText != null) subtitleText.text = $"<i>{data.scientificName}</i>";
            if (depthText != null) depthText.text = $"📍 Depth: {data.depthRangeText}";
            if (habitatOrClueText != null) habitatOrClueText.text = $"Habitat: {data.habitat}";

            if (accentImage != null)
                accentImage.color = new Color(0.0f, 0.9f, 1.0f, 1f); // Vibrant Cyan

            if (thumbnailImage != null && data.photo != null)
            {
                thumbnailImage.sprite = data.photo;
                thumbnailImage.color  = Color.white;
            }
        }
        else
        {
            if (cardButton != null)
            {
                cardButton.interactable = false; // Locked
                cardButton.onClick.RemoveAllListeners();
            }

            if (titleText != null) titleText.text = "??? [Uncataloged Specimen]";
            if (subtitleText != null) subtitleText.text = $"Class: {data.taxonomicClass}";
            if (depthText != null) depthText.text = $"📍 Depth: {data.depthRangeText}";

            string clue = !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : data.habitat;
            if (habitatOrClueText != null) habitatOrClueText.text = $"🔍 Search Clue: {clue}";

            if (accentImage != null)
                accentImage.color = new Color(0.35f, 0.40f, 0.45f, 1f); // Slate / Gray

            if (thumbnailImage != null && data.silhouette != null)
            {
                thumbnailImage.sprite = data.silhouette;
                thumbnailImage.color  = new Color(0.12f, 0.15f, 0.20f, 1f);
            }
        }

        // Render the 3D model into the RawImage thumbnail
        if (thumbnailRawImage != null && ModelPreviewSystem.Instance != null)
        {
            ModelPreviewSystem.Instance.ShowPreview(data, isSilhouette: !isDiscovered, thumbnailRawImage);

            // Ensure ThumbnailDragRotator is attached ONLY to the thumbnail box
            var rotator = thumbnailRawImage.GetComponent<ThumbnailDragRotator>();
            if (rotator == null) rotator = thumbnailRawImage.gameObject.AddComponent<ThumbnailDragRotator>();
        }
    }
}

/// <summary>
/// Helper component attached strictly to the ThumbnailBox RawImage.
/// Handles 3D drag-to-rotate without intercepting ScrollRect vertical drag gestures.
/// </summary>
public class ThumbnailDragRotator : MonoBehaviour, IDragHandler, IEndDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        if (ModelPreviewSystem.Instance != null)
            ModelPreviewSystem.Instance.RotateModel(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ModelPreviewSystem.Instance != null)
            ModelPreviewSystem.Instance.EndDrag();
    }
}