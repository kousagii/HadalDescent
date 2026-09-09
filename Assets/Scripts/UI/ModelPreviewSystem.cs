using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Renders 3D models into real-time RenderTextures and crisp 2D thumbnail Sprites.
///
/// Features:
///   - Automatic Geometric Centering: Offsets any model so its center-of-mass is at the camera focus.
///   - Max-Space Aspect Framing: Dynamically calculates camera distance to maximize screen coverage (fills ~90% of the square) while preserving aspect ratio.
///   - Pure Solid Black Silhouette: Unlit pure black shader for mysterious undiscovered species.
///   - Dual-Source Lighting: Key warm light + fill ocean light for 3D depth and vibrant biological colors.
///   - Per-Card Cached Thumbnails: Generates and caches transparent 3D render sprites for all species.
///   - Dedicated 3D Inspection Modal: Interactive 360° touch/mouse drag rotation for both discovered and silhouette models.
/// </summary>
public class ModelPreviewSystem : MonoBehaviour
{
    private static ModelPreviewSystem _instance;
    public static ModelPreviewSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ModelPreviewSystem>();
                if (_instance == null)
                {
                    var go = new GameObject("_ModelPreviewSystem");
                    _instance = go.AddComponent<ModelPreviewSystem>();
                }
            }
            return _instance;
        }
    }

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

    [Header("Placement")]
    [Tooltip("Far-off world coordinate to isolate preview models from scene cameras.")]
    [SerializeField] private Vector3 previewWorldPosition = new Vector3(8000f, 8000f, 8000f);

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private GameObject _previewInstance;
    private int        _previewLayerId = -1;
    private bool       _isDragging     = false;
    private float      _idleTimer      = 0f;
    private Material   _silhouetteMat;
    private Light      _keyLight;
    private Light      _fillLight;

    private readonly Dictionary<string, Sprite> _thumbnailCache = new Dictionary<string, Sprite>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;

        EnsureInitialized();
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
    // Initialization
    // -----------------------------------------------------------------------

    private void EnsureInitialized()
    {
        _previewLayerId = LayerMask.NameToLayer("ModelPreview");

        if (previewRT == null)
        {
            previewRT = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "ModelPreview_RT",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear
            };
            previewRT.Create();
        }

        if (previewCamera == null)
        {
            var camGO = new GameObject("PreviewCamera", typeof(Camera));
            camGO.transform.SetParent(transform, false);
            previewCamera = camGO.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.fieldOfView = 28f;
            previewCamera.nearClipPlane = 0.02f;
            previewCamera.farClipPlane = 500f;
            previewCamera.targetTexture = previewRT;

            if (_previewLayerId >= 0)
                previewCamera.cullingMask = 1 << _previewLayerId;
            else
                previewCamera.cullingMask = ~0;

            var camData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = false;
                camData.renderShadows = false;
            }
        }

        if (_keyLight == null)
        {
            var lightGO = new GameObject("PreviewLight_Key", typeof(Light));
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localRotation = Quaternion.Euler(35f, 35f, 0f);
            _keyLight = lightGO.GetComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.intensity = 1.4f;
            _keyLight.color = new Color(1f, 0.98f, 0.94f);
            if (_previewLayerId >= 0)
                _keyLight.cullingMask = 1 << _previewLayerId;

            // Fill light from bottom-left for realistic rim & ambient lighting
            var fillGO = new GameObject("PreviewLight_Fill", typeof(Light));
            fillGO.transform.SetParent(transform, false);
            fillGO.transform.localRotation = Quaternion.Euler(-25f, -145f, 0f);
            _fillLight = fillGO.GetComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.intensity = 0.6f;
            _fillLight.color = new Color(0.65f, 0.85f, 1f);
            if (_previewLayerId >= 0)
                _fillLight.cullingMask = 1 << _previewLayerId;
        }

        if (previewRawImage != null && previewRT != null)
            previewRawImage.texture = previewRT;

        CreateSilhouetteMaterial();
    }

    // -----------------------------------------------------------------------
    // Thumbnail Generation & Caching
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates or retrieves a transparent, centered, max-size 2D Sprite snapshot of the species' 3D model.
    /// Used by Bestiary list cards.
    /// </summary>
    public Sprite GetOrRenderThumbnail(SpeciesData data, bool isSilhouette = false)
    {
        if (data == null) return null;

        string cacheKey = $"{data.speciesId}_{(isSilhouette ? "sil" : "color")}";
        if (_thumbnailCache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            return cached;

        EnsureInitialized();
        if (previewCamera == null || previewRT == null)
        {
            return isSilhouette && data.silhouette != null ? data.silhouette : data.photo;
        }

        // Hide any active live preview instance while rendering the snapshot
        if (_previewInstance != null) _previewInstance.SetActive(false);

        // 1. Create temporary pivot
        GameObject tempPivot = new GameObject("TempThumbPivot");
        tempPivot.transform.position = previewWorldPosition;
        tempPivot.transform.rotation = Quaternion.Euler(10f, 135f, 0f); // Sleek 3/4 isometric viewpoint

        // 2. Spawn temporary model
        GameObject tempModel = SpawnModelInstance(data, isSilhouette);
        if (tempModel == null)
        {
            DestroyImmediate(tempPivot);
            if (_previewInstance != null) _previewInstance.SetActive(true);
            return isSilhouette && data.silhouette != null ? data.silhouette : data.photo;
        }

        tempModel.transform.SetParent(tempPivot.transform, false);
        CenterModelGeometry(tempModel);

        // 3. Position and frame camera
        FrameCamera(tempPivot);

        // Enforce transparent clear flags on preview camera
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        // 4. Clear RenderTexture and Render offscreen
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = previewRT;
        GL.Clear(true, true, Color.clear); // Explicitly clear color and depth buffers

        var prevTarget = previewCamera.targetTexture;
        previewCamera.targetTexture = previewRT;
        previewCamera.Render();
        previewCamera.targetTexture = prevTarget;

        // 5. Read pixels into Texture2D
        Texture2D snap = new Texture2D(previewRT.width, previewRT.height, TextureFormat.RGBA32, false);
        snap.ReadPixels(new Rect(0, 0, previewRT.width, previewRT.height), 0, 0);
        snap.Apply();

        RenderTexture.active = prevActive;

        // 6. Cleanup temporary pivot & model immediately (prevents same-frame multi-model overlap in loops)
        DestroyImmediate(tempPivot);

        // Restore active live preview if present
        if (_previewInstance != null) _previewInstance.SetActive(true);

        // 7. Create and cache Sprite
        Sprite sprite = Sprite.Create(snap, new Rect(0, 0, snap.width, snap.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = $"Thumb_{cacheKey}";
        _thumbnailCache[cacheKey] = sprite;

        return sprite;
    }

    // -----------------------------------------------------------------------
    // Interactive Live 3D Preview (Detail Modal & Inspector)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Displays the live interactive 3D creature model in the RenderTexture.
    /// Centered on its geometric axis for pure in-place 360-degree rotation.
    /// </summary>
    public void ShowPreview(SpeciesData data, bool isSilhouette = false, RawImage customRawImage = null)
    {
        ClearPreview();
        if (data == null) return;

        EnsureInitialized();

        // 1. Create a root pivot container at previewWorldPosition
        _previewInstance = new GameObject("PreviewPivot");
        _previewInstance.transform.position = previewWorldPosition;
        _previewInstance.transform.rotation = Quaternion.identity;

        // 2. Spawn the creature model and parent under the pivot
        GameObject model = SpawnModelInstance(data, isSilhouette);
        if (model == null) return;
        model.transform.SetParent(_previewInstance.transform, false);

        // 3. Center the creature geometry exactly on the pivot axis
        CenterModelGeometry(model);

        // 4. Frame camera
        FrameCamera(_previewInstance);

        var targetImage = customRawImage != null ? customRawImage : previewRawImage;
        if (targetImage != null)
        {
            targetImage.texture = previewRT;
            targetImage.gameObject.SetActive(true);
        }

        if (previewCamera != null)
        {
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.gameObject.SetActive(true);
        }
    }

    private GameObject SpawnModelInstance(SpeciesData data, bool isSilhouette)
    {
        GameObject go;
        if (data.modelPrefab != null)
        {
            go = Instantiate(data.modelPrefab, previewWorldPosition, Quaternion.identity);
            float scaleMul = data.previewScaleMultiplier > 0f ? data.previewScaleMultiplier : 1f;
            go.transform.localScale *= scaleMul;
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
            go = GameObject.CreatePrimitive(pType);
            go.transform.position = previewWorldPosition;

            float scaleMul = data.previewScaleMultiplier > 0f ? data.previewScaleMultiplier : 1f;
            go.transform.localScale = (data.placeholderScale != Vector3.zero ? data.placeholderScale : Vector3.one) * 1.2f * scaleMul;

            var rend = go.GetComponent<Renderer>();
            if (rend != null && !isSilhouette)
            {
                rend.material = MaterialUtils.CreateColoredMaterial(data.placeholderColor);
            }
        }

        if (isSilhouette)
        {
            ApplySilhouetteMaterial(go);
        }

        foreach (var col in go.GetComponentsInChildren<Collider>())
            Destroy(col);

        SetLayerRecursive(go, _previewLayerId >= 0 ? _previewLayerId : 0);
        return go;
    }

    /// <summary>
    /// Centers the combined visual geometric bounds of a model at previewWorldPosition (pivot origin).
    /// </summary>
    private void CenterModelGeometry(GameObject model)
    {
        if (model == null) return;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 centerOffset = previewWorldPosition - bounds.center;
        model.transform.position += centerOffset;
    }

    /// <summary>
    /// Interactive drag rotation called by UI drag events.
    /// Rotates the model 360 degrees in all directions (horizontal, vertical, diagonal) centered on its geometric axis.
    /// </summary>
    public void RotateModel(Vector2 delta)
    {
        if (_previewInstance == null) return;
        _isDragging = true;
        _idleTimer  = 0f;

        Vector3 upAxis    = previewCamera != null ? previewCamera.transform.up : Vector3.up;
        Vector3 rightAxis = previewCamera != null ? previewCamera.transform.right : Vector3.right;

        _previewInstance.transform.Rotate(upAxis, -delta.x * dragSensitivity, Space.World);
        _previewInstance.transform.Rotate(rightAxis, delta.y * dragSensitivity, Space.World);
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
        // Darkened 3D silhouette material: URP Lit renders the geometry contours with directional lighting
        Color silColor = new Color(0.10f, 0.12f, 0.16f, 1.0f);
        _silhouetteMat = MaterialUtils.CreateColoredMaterial(silColor, 0.35f, 0.05f);
        if (_silhouetteMat != null)
        {

            // CRITICAL: Replace creature textures with flat white texture so _BaseColor * white = pure dark silhouette
            if (_silhouetteMat.HasProperty("_BaseMap"))  _silhouetteMat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (_silhouetteMat.HasProperty("_MainTex"))  _silhouetteMat.SetTexture("_MainTex", Texture2D.whiteTexture);

            // Subtle metallic & smoothness to catch soft specular rims on 3D curves
            if (_silhouetteMat.HasProperty("_Metallic"))    _silhouetteMat.SetFloat("_Metallic", 0.05f);
            if (_silhouetteMat.HasProperty("_Smoothness"))  _silhouetteMat.SetFloat("_Smoothness", 0.35f);
            if (_silhouetteMat.HasProperty("_BumpMap"))     _silhouetteMat.SetTexture("_BumpMap", null);
            if (_silhouetteMat.HasProperty("_EmissionMap")) _silhouetteMat.SetTexture("_EmissionMap", null);
            if (_silhouetteMat.HasProperty("_EmissionColor")) _silhouetteMat.SetColor("_EmissionColor", Color.black);

            // Disable unwanted keywords
            _silhouetteMat.DisableKeyword("_EMISSION");
            _silhouetteMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _silhouetteMat.DisableKeyword("_ALPHATEST_ON");

            if (_silhouetteMat.HasProperty("_Surface")) _silhouetteMat.SetFloat("_Surface", 0f); // Opaque
        }
    }

    private void ApplySilhouetteMaterial(GameObject target)
    {
        if (target == null) return;
        if (_silhouetteMat == null) CreateSilhouetteMaterial();
        if (_silhouetteMat == null) return;

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            int matCount = r.sharedMaterials != null && r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1;
            var mats = new Material[matCount];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = _silhouetteMat;
            r.materials = mats;
        }
    }

    private void FrameCamera(GameObject model)
    {
        if (previewCamera == null || model == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            previewCamera.transform.position = previewWorldPosition + new Vector3(0f, 0f, -2.5f);
            previewCamera.transform.LookAt(previewWorldPosition);
            return;
        }

        // 1. Calculate combined world bounds
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // 2. Compute tight camera distance so the model fills ~90% of the image
        float fovRad   = previewCamera.fieldOfView * Mathf.Deg2Rad;
        float halfFovY = fovRad * 0.5f;
        float aspect   = previewCamera.aspect > 0.01f ? previewCamera.aspect : 1.0f;
        float halfFovX = Mathf.Atan(Mathf.Tan(halfFovY) * aspect);

        float extY = bounds.extents.y;
        float extX = bounds.extents.x;
        float extZ = bounds.extents.z;

        float horizontalRadius = Mathf.Max(extX, extZ, 0.05f);
        float verticalRadius   = Mathf.Max(extY, 0.05f);

        float distVertical   = verticalRadius / Mathf.Tan(halfFovY);
        float distHorizontal = horizontalRadius / Mathf.Tan(halfFovX);

        // Tight framing: 10% padding so creature occupies 90% of the image box
        float cameraDistance = Mathf.Max(distVertical, distHorizontal) * 1.10f;
        cameraDistance = Mathf.Max(cameraDistance, 0.25f);

        previewCamera.farClipPlane = Mathf.Max(500f, cameraDistance * 3f);
        previewCamera.transform.position = previewWorldPosition + new Vector3(0f, 0f, -cameraDistance);
        previewCamera.transform.LookAt(previewWorldPosition);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}