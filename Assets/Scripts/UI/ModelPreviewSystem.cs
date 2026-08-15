using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a rotating 3D model (or coloured primitive) into a RenderTexture
/// displayed inside the Bestiary detail panel as a RawImage.
///
/// Architecture:
///   A dedicated "Preview Camera" renders ONLY objects on the _ModelPreview layer.
///   When ShowPreview(data) is called, a copy of the model (or placeholder) is
///   instantiated, placed on that layer, and rotated each frame.
///   The camera's output is assigned to a RenderTexture ? shown in a UI RawImage.
///
/// Setup in Unity (one time):
///   1. Add ModelPreviewSystem to a persistent or scene GameObject.
///   2. Create a Camera child ? assign to previewCamera.
///      - Set Culling Mask to ONLY the "ModelPreview" layer.
///      - Clear Flags = Solid Colour, Background = #0A1020 (dark navy).
///      - Position: (0, 100, -3) — far from the game world.
///   3. Create a RenderTexture asset (256×256, 16-bit depth) ? assign to previewRT.
///      - Also assign previewRT to the Preview Camera's Target Texture field.
///   4. In the Bestiary canvas, add a RawImage ? assign to previewRawImage.
///   5. Assign previewRawImage to ModelPreviewSystem in Inspector.
///   6. Call ModelPreviewSystem.Instance.ShowPreview(speciesData) from BestiaryManager.
///
/// Layer setup:
///   Go to Edit ? Project Settings ? Tags and Layers.
///   Add a layer named exactly "ModelPreview" (use any free slot, e.g. Layer 9).
/// </summary>
public class ModelPreviewSystem : MonoBehaviour
{
    public static ModelPreviewSystem Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Render Target")]
    [Tooltip("The RenderTexture the Preview Camera renders into. Assign to RawImage too.")]
    [SerializeField] private RenderTexture previewRT;

    [Header("Preview Camera")]
    [Tooltip("A dedicated Camera that only sees the ModelPreview layer.")]
    [SerializeField] private Camera previewCamera;

    [Header("UI")]
    [Tooltip("The RawImage in the Bestiary panel that displays the rotating model.")]
    [SerializeField] private RawImage previewRawImage;

    [Header("Preview Settings")]
    [Tooltip("World position where the preview model is placed (should be far from gameplay).")]
    [SerializeField] private Vector3 previewWorldPosition = new Vector3(0f, 200f, 0f);
    [Tooltip("Degrees per second the model rotates.")]
    [SerializeField] private float   rotationSpeed = 45f;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private GameObject _previewInstance;
    private int        _previewLayerId = -1;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _previewLayerId = LayerMask.NameToLayer("ModelPreview");
        if (_previewLayerId < 0)
            Debug.LogWarning("[ModelPreviewSystem] Layer 'ModelPreview' not found. " +
                             "Add it in Edit ? Project Settings ? Tags and Layers.");

        // Link render texture to camera and raw image
        if (previewCamera != null && previewRT != null)
            previewCamera.targetTexture = previewRT;
        if (previewRawImage != null && previewRT != null)
            previewRawImage.texture = previewRT;
    }

    private void Update()
    {
        if (_previewInstance != null)
            _previewInstance.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawn the species model (or placeholder primitive) in the preview scene
    /// and start rendering it to the Bestiary RawImage.
    /// </summary>
    public void ShowPreview(SpeciesData data)
    {
        ClearPreview();

        if (data == null) return;

        // Instantiate model prefab or create placeholder primitive
        if (data.modelPrefab != null)
        {
            _previewInstance = Instantiate(data.modelPrefab, previewWorldPosition, Quaternion.identity);
        }
        else
        {
            PrimitiveType pType = data.placeholderShape switch
            {
                PlaceholderShape.Capsule  => PrimitiveType.Capsule,
                PlaceholderShape.Cube     => PrimitiveType.Cube,
                PlaceholderShape.Cylinder => PrimitiveType.Cylinder,
                _                         => PrimitiveType.Sphere,
            };
            _previewInstance = GameObject.CreatePrimitive(pType);
            _previewInstance.transform.position   = previewWorldPosition;
            _previewInstance.transform.localScale  = data.placeholderScale * 1.2f;

            var rend = _previewInstance.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Standard")) { color = data.placeholderColor };
            }
        }

        // Remove any colliders — this is display-only
        foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
            Destroy(col);

        // Assign to ModelPreview layer so ONLY the preview camera sees it
        SetLayerRecursive(_previewInstance, _previewLayerId >= 0 ? _previewLayerId : 0);

        // Frame the camera nicely: position it relative to the model's bounds
        FrameCamera(_previewInstance);

        if (previewRawImage != null) previewRawImage.gameObject.SetActive(true);
        if (previewCamera  != null) previewCamera.gameObject.SetActive(true);
    }

    /// <summary>Destroy the preview model and hide the RawImage.</summary>
    public void ClearPreview()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
        if (previewRawImage != null) previewRawImage.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void FrameCamera(GameObject model)
    {
        if (previewCamera == null) return;

        // Use renderer bounds to determine how far to place the camera
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            previewCamera.transform.position = previewWorldPosition + new Vector3(0f, 0.5f, -3f);
        }
        else
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float size   = bounds.extents.magnitude;
            float fovRad = previewCamera.fieldOfView * Mathf.Deg2Rad;
            float dist   = (size / Mathf.Tan(fovRad * 0.5f)) * 1.5f;

            previewCamera.transform.position =
                previewWorldPosition + new Vector3(0f, bounds.extents.y * 0.5f, -dist);
        }

        previewCamera.transform.LookAt(previewWorldPosition);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
