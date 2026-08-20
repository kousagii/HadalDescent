using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Renders an interactive 3D model into a RenderTexture displayed on UI RawImage elements.
///
/// Features:
///   - Discovered Mode: Full-colour PBR materials + smooth rotation + interactive drag-to-rotate.
///   - Undiscovered Mode: Blackened unlit silhouette + smooth rotation + interactive drag-to-rotate.
///   - Touch/Mouse Drag: Players can drag horizontally/vertically to spin the 3D model 360°.
/// </summary>
public class ModelPreviewSystem : MonoBehaviour
{
    public static ModelPreviewSystem Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Render Target")]
    [Tooltip("The RenderTexture the Preview Camera renders into.")]
    [SerializeField] private RenderTexture previewRT;

    [Header("Preview Camera")]
    [Tooltip("A dedicated Camera that only sees the ModelPreview layer.")]
    [SerializeField] private Camera previewCamera;

    [Header("UI (Default / Main)")]
    [Tooltip("Optional default RawImage.")]
    [SerializeField] private RawImage previewRawImage;

    [Header("Rotation Settings")]
    [Tooltip("Degrees per second the model rotates automatically when idle.")]
    [SerializeField] private float autoRotationSpeed = 35f;

    [Tooltip("Mouse/Touch drag rotation sensitivity.")]
    [SerializeField] private float dragSensitivity = 0.4f;

    [Header("Silhouette Visuals")]
    [Tooltip("Color tint used for the 3D silhouette.")]
    [SerializeField] private Color silhouetteColor = new Color(0.04f, 0.06f, 0.09f, 1f);

    [Header("Placement")]
    [SerializeField] private Vector3 previewWorldPosition = new Vector3(0f, 200f, 0f);

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private GameObject _previewInstance;
    private int        _previewLayerId = -1;
    private bool       _isDragging     = false;
    private float      _idleTimer      = 0f;
    private Material   _silhouetteMat;

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
                             "Add it in Edit → Project Settings → Tags and Layers.");

        if (previewCamera != null && previewRT != null)
            previewCamera.targetTexture = previewRT;
        if (previewRawImage != null && previewRT != null)
            previewRawImage.texture = previewRT;

        CreateSilhouetteMaterial();
    }

    private void Update()
    {
        if (_previewInstance != null && !_isDragging)
        {
            _idleTimer += Time.deltaTime;
            _previewInstance.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Displays the 3D creature model.
    /// If isSilhouette is true, renders with a blackened unlit silhouette shader.
    /// </summary>
    public void ShowPreview(SpeciesData data, bool isSilhouette = false, RawImage customRawImage = null)
    {
        ClearPreview();
        if (data == null) return;

        // 1. Instantiate 3D model or placeholder primitive
        if (data.modelPrefab != null)
        {
            _previewInstance = Instantiate(data.modelPrefab, previewWorldPosition, Quaternion.identity);
            float scaleMul = data.previewScaleMultiplier > 0f ? data.previewScaleMultiplier : 1f;
            _previewInstance.transform.localScale *= scaleMul;
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
            _previewInstance.transform.position = previewWorldPosition;

            float scaleMul = data.previewScaleMultiplier > 0f ? data.previewScaleMultiplier : 1f;
            _previewInstance.transform.localScale = (data.placeholderScale != Vector3.zero ? data.placeholderScale : Vector3.one) * 1.2f * scaleMul;

            var rend = _previewInstance.GetComponent<Renderer>();
            if (rend != null && !isSilhouette)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                             ?? Shader.Find("Standard");
                if (shader != null)
                    rend.material = new Material(shader) { color = data.placeholderColor };
            }
        }

        // 2. If Silhouette mode: apply blackened unlit material
        if (isSilhouette)
        {
            ApplySilhouetteMaterial(_previewInstance);
        }

        // 3. Remove colliders (display only)
        foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
            Destroy(col);

        // 4. Assign to ModelPreview layer
        SetLayerRecursive(_previewInstance, _previewLayerId >= 0 ? _previewLayerId : 0);

        // 5. Frame camera to fit bounds
        FrameCamera(_previewInstance);

        // 6. Assign RenderTexture to target RawImage
        var targetImage = customRawImage != null ? customRawImage : previewRawImage;
        if (targetImage != null)
        {
            targetImage.texture = previewRT;
            targetImage.gameObject.SetActive(true);
        }

        if (previewCamera != null) previewCamera.gameObject.SetActive(true);
    }

    /// <summary>
    /// Interactive drag rotation called by UI drag events.
    /// </summary>
    public void RotateModel(Vector2 delta)
    {
        if (_previewInstance == null) return;
        _isDragging = true;
        _idleTimer  = 0f;

        _previewInstance.transform.Rotate(Vector3.up, -delta.x * dragSensitivity, Space.World);
        _previewInstance.transform.Rotate(Vector3.right, delta.y * dragSensitivity, Space.World);
    }

    public void EndDrag()
    {
        _isDragging = false;
    }

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

    private void CreateSilhouetteMaterial()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color")
                          ?? Shader.Find("Universal Render Pipeline/Simple Lit");

        if (unlitShader != null)
        {
            _silhouetteMat = new Material(unlitShader)
            {
                color = silhouetteColor
            };
        }
    }

    private void ApplySilhouetteMaterial(GameObject target)
    {
        if (_silhouetteMat == null) CreateSilhouetteMaterial();
        if (_silhouetteMat == null) return;

        var renderers = target.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = _silhouetteMat;
            r.materials = mats;
        }
    }

    private void FrameCamera(GameObject model)
    {
        if (previewCamera == null) return;

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
            float dist   = (size / Mathf.Tan(fovRad * 0.5f)) * 1.55f;

            previewCamera.transform.position =
                previewWorldPosition + new Vector3(0f, bounds.extents.y * 0.4f, -Mathf.Max(dist, 2f));
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