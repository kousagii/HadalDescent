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
            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(true);
                subtitleText.text = $"Class: <pos=170>{data.taxonomicClass}</pos>";
                subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (depthText != null)
            {
                depthText.gameObject.SetActive(true);
                depthText.text = $"Depth: <pos=170>{data.depthRangeText}</pos>";
                depthText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (habitatOrClueText != null)
            {
                habitatOrClueText.gameObject.SetActive(true);
                habitatOrClueText.text = $"Habitat: <pos=170>{data.habitat}</pos>";
                habitatOrClueText.textWrappingMode = TextWrappingModes.Normal;
            }

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
            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(true);
                subtitleText.text = $"Class: <pos=170>{data.taxonomicClass}</pos>";
                subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (depthText != null)
            {
                depthText.gameObject.SetActive(true);
                depthText.text = $"Depth: <pos=170>{data.depthRangeText}</pos>";
                depthText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            string clue = !string.IsNullOrEmpty(data.explorationHint) ? data.explorationHint : data.habitat;
            if (habitatOrClueText != null)
            {
                habitatOrClueText.gameObject.SetActive(true);
                habitatOrClueText.text = $"Habitat: <pos=170>{clue}</pos>";
                habitatOrClueText.textWrappingMode = TextWrappingModes.Normal;
            }

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

    public void SetupHabitat(HabitatLandmarkData data, bool isSurveyed, Action onCardClick)
    {
        if (data == null) return;

        if (cardButton == null) cardButton = GetComponent<Button>();

        if (isSurveyed)
        {
            if (cardButton != null)
            {
                cardButton.interactable = true;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(() => onCardClick?.Invoke());
            }

            // Put ONLY the name on the habitat card (no .ToUpper(), matching species commonName font weight)
            if (titleText != null)
            {
                titleText.text = data.habitatName;
                titleText.gameObject.SetActive(true);
            }

            // Remove Class, Depth, and Role from the card per user requirement
            if (subtitleText != null)
            {
                subtitleText.text = "";
                subtitleText.gameObject.SetActive(false);
            }
            if (depthText != null)
            {
                depthText.text = "";
                depthText.gameObject.SetActive(false);
            }
            if (habitatOrClueText != null)
            {
                habitatOrClueText.text = "";
                habitatOrClueText.gameObject.SetActive(false);
            }

            if (accentImage != null)
                accentImage.color = new Color(1.0f, 0.88f, 0.15f, 1f); // Radiant Gold Accent
        }
        else
        {
            if (cardButton != null)
            {
                cardButton.interactable = false; // Locked until surveyed in ocean
                cardButton.onClick.RemoveAllListeners();
            }

            // Put ONLY the name on the habitat card (no .ToUpper(), matching species "??? [Uncataloged Specimen]")
            if (titleText != null)
            {
                titleText.text = "??? [Uncataloged Habitat]";
                titleText.gameObject.SetActive(true);
            }

            // Remove Class, Depth, and Role from the card per user requirement
            if (subtitleText != null)
            {
                subtitleText.text = "";
                subtitleText.gameObject.SetActive(false);
            }
            if (depthText != null)
            {
                depthText.text = "";
                depthText.gameObject.SetActive(false);
            }
            if (habitatOrClueText != null)
            {
                habitatOrClueText.text = "";
                habitatOrClueText.gameObject.SetActive(false);
            }

            if (accentImage != null)
                accentImage.color = new Color(0.40f, 0.35f, 0.15f, 1f); // Dim Gold/Ochre
        }

        // Hide 3D model raw image for habitats and display the photo
        if (thumbnailRawImage != null)
            thumbnailRawImage.gameObject.SetActive(false);

        if (thumbnailImage != null)
        {
            thumbnailImage.gameObject.SetActive(true);
            if (isSurveyed && data.habitatPhoto != null)
            {
                thumbnailImage.sprite = data.habitatPhoto;
                thumbnailImage.color = Color.white;
            }
            else
            {
                thumbnailImage.sprite = data.habitatPhoto;
                thumbnailImage.color = isSurveyed ? new Color(1f, 0.9f, 0.3f, 0.85f) : new Color(0.12f, 0.15f, 0.20f, 1f);
            }

            // Clicking thumbnail on surveyed habitat card also opens FactCard
            var thumbBtn = thumbnailImage.GetComponent<Button>();
            if (thumbBtn != null)
            {
                thumbBtn.interactable = isSurveyed;
                thumbBtn.onClick.RemoveAllListeners();
                if (isSurveyed) thumbBtn.onClick.AddListener(() => onCardClick?.Invoke());
            }
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