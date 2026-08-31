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
        }

        // 3D Model Thumbnail Rendering
        Sprite thumb = ModelPreviewSystem.Instance != null
            ? ModelPreviewSystem.Instance.GetOrRenderThumbnail(data, isSilhouette: !isDiscovered)
            : (isDiscovered ? data.photo : data.silhouette);

        if (thumbnailImage != null)
        {
            if (thumb != null)
            {
                thumbnailImage.sprite = thumb;
                thumbnailImage.color = Color.white;
                thumbnailImage.gameObject.SetActive(true);
            }
            else if (data.photo != null)
            {
                thumbnailImage.sprite = isDiscovered ? data.photo : (data.silhouette != null ? data.silhouette : data.photo);
                thumbnailImage.color = isDiscovered ? Color.white : new Color(0.12f, 0.15f, 0.20f, 1f);
                thumbnailImage.gameObject.SetActive(true);
            }
        }

        if (thumbnailRawImage != null)
        {
            if (thumb != null)
            {
                thumbnailRawImage.texture = thumb.texture;
                thumbnailRawImage.color = Color.white;
                thumbnailRawImage.gameObject.SetActive(true);
            }
        }

        // Tap on 3D thumbnail to open full-screen 3D Model Inspection modal
        Button thumbBtn = null;
        if (thumbnailImage != null)
            thumbBtn = thumbnailImage.GetComponent<Button>() ?? thumbnailImage.gameObject.AddComponent<Button>();
        else if (thumbnailRawImage != null)
            thumbBtn = thumbnailRawImage.GetComponent<Button>() ?? thumbnailRawImage.gameObject.AddComponent<Button>();

        if (thumbBtn != null)
        {
            thumbBtn.onClick.RemoveAllListeners();
            thumbBtn.onClick.AddListener(() =>
            {
                if (BestiaryManager.Instance != null)
                    BestiaryManager.Instance.OpenModelInspectionModal(data, isDiscovered);
            });
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