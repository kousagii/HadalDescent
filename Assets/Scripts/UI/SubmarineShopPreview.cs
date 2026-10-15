using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the real-time offscreen 3D Submarine Studio for the Upgrade Shop.
///
/// Features:
///   - Offscreen isolated render chamber at (8500, 8500, 8500) using 'ModelPreview' layer.
///   - Real-time RenderTexture connected to the Shop's RawImage viewport (~40% right column).
///   - Dual-source studio lighting: Warm key light + marine cyan fill light.
///   - Automatically centers and dynamically frames the submarine regardless of model scale.
///   - Hosts SubmarineCustomizer to dynamically swap visual upgrade parts across all 6 categories.
///   - Pairs with SubmarinePreviewRotator for 360° mouse/touch rotation and smooth idle turntable spin.
///   - Deactivates preview camera when the shop closes to optimize mobile/desktop performance.
/// </summary>
public class SubmarineShopPreview : MonoBehaviour
{
    [Header("UI Viewport Binding")]
    [Tooltip("The RawImage in the Shop UI that displays the 3D submarine.")]
    [SerializeField] private RawImage viewportRawImage;

    [Tooltip("Rotator script attached to the RawImage viewport.")]
    [SerializeField] private SubmarinePreviewRotator previewRotator;

    [Header("Submarine Model & Customizer")]
    [Tooltip("Prefab for the submarine visual preview. Defaults to Resources/Submarine_ShopVisual if null.")]
    [SerializeField] private GameObject submarinePrefab;

    public GameObject SubmarinePrefab
    {
        get => submarinePrefab;
        set => submarinePrefab = value;
    }

    [Tooltip("Reference to the SubmarineCustomizer managing visual parts.")]
    [SerializeField] private SubmarineCustomizer customizer;

    [Header("Studio World Chamber")]
    [Tooltip("World coordinate far away from gameplay to isolate the preview.")]
    [SerializeField] private Vector3 studioWorldPosition = new Vector3(8500f, 8500f, 8500f);

    [Tooltip("Default camera distance from submarine center.")]
    [SerializeField] private float cameraDistance = 3.6f;

    [Tooltip("Camera elevation offset above submarine center.")]
    [SerializeField] private float cameraHeight = 0.35f;

    [Tooltip("Field of view for the preview camera.")]
    [SerializeField] private float cameraFov = 30f;

    [Header("Studio Lighting (Neutral Studio Rig)")]
    [Tooltip("Key light intensity (pure neutral white).")]
    [SerializeField] private float keyLightIntensity = 1.25f;

    [Tooltip("Fill light intensity (soft neutral white).")]
    [SerializeField] private float fillLightIntensity = 0.70f;

    [Tooltip("Back/Rim light intensity (subtle edge definition).")]
    [SerializeField] private float rimLightIntensity = 0.45f;

    private Camera        _previewCamera;
    private RenderTexture _previewRT;
    private GameObject    _studioRoot;
    private GameObject    _submarinePivot;
    private GameObject    _submarineInstance;
    private Light         _keyLight;
    private Light         _fillLight;
    private Light         _rimLight;
    private int           _previewLayerId = -1;
    private bool          _isInitialized = false;
    private float         _cachedAspect = -1f;

    public SubmarineCustomizer Customizer => customizer;
    public SubmarinePreviewRotator Rotator => previewRotator;
    public RawImage ViewportRawImage => viewportRawImage;

    private void Awake()
    {
        EnsureStudioReady();
    }

    private void Start()
    {
        SubscribeToShopEvents();
    }

    private void OnEnable()
    {
        SubscribeToShopEvents();
        if (_isInitialized)
        {
            UpdateRenderTextureAspect();
            RefreshCustomizer();
        }
    }

    private void Update()
    {
        // Continuously ensure the RenderTexture exactly matches the Viewport aspect ratio without distortion
        if (_previewCamera != null && _previewCamera.enabled && viewportRawImage != null && viewportRawImage.gameObject.activeInHierarchy)
        {
            Rect rect = viewportRawImage.rectTransform.rect;
            if (rect.width > 20f && rect.height > 20f)
            {
                float currentAspect = rect.width / rect.height;
                if (Mathf.Abs(currentAspect - _cachedAspect) > 0.015f)
                {
                    UpdateRenderTextureAspect();
                }
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromShopEvents();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromShopEvents();
        CleanupStudio();
    }

    // -----------------------------------------------------------------------
    // Initialization & Setup
    // -----------------------------------------------------------------------

    public void EnsureStudioReady()
    {
        if (_isInitialized) return;

        _previewLayerId = LayerMask.NameToLayer("ModelPreview");

        // 1. Create isolated Studio Root in world space
        if (_studioRoot == null)
        {
            _studioRoot = new GameObject("_SubmarineStudio_Chamber");
            _studioRoot.transform.position = studioWorldPosition;
            DontDestroyOnLoad(_studioRoot);
        }

        // 2. Setup Studio Camera first so it can receive dynamic RT
        if (_previewCamera == null)
        {
            var camGO = new GameObject("StudioCamera", typeof(Camera));
            camGO.transform.SetParent(_studioRoot.transform, false);
            _previewCamera = camGO.GetComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.02f, 0.05f, 0.09f, 0f); // Clean transparent slate
            _previewCamera.fieldOfView = cameraFov;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 200f;

            if (_previewLayerId >= 0)
                _previewCamera.cullingMask = 1 << _previewLayerId;
            else
                _previewCamera.cullingMask = ~0;

            var camData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = false;
                camData.renderShadows = false;
            }
        }

        // 3. Setup high-fidelity RenderTexture matched to the UI viewport aspect ratio
        UpdateRenderTextureAspect();

        // 4. Setup Neutral Studio Lighting (Pure white, zero color cast to show original submarine textures)
        if (_keyLight == null)
        {
            var keyGO = new GameObject("StudioLight_Key", typeof(Light));
            keyGO.transform.SetParent(_studioRoot.transform, false);
            keyGO.transform.localRotation = Quaternion.Euler(32f, 40f, 0f);
            _keyLight = keyGO.GetComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.intensity = keyLightIntensity;
            _keyLight.color = Color.white; // Pure neutral white
            if (_previewLayerId >= 0) _keyLight.cullingMask = 1 << _previewLayerId;
        }

        if (_fillLight == null)
        {
            var fillGO = new GameObject("StudioLight_Fill", typeof(Light));
            fillGO.transform.SetParent(_studioRoot.transform, false);
            fillGO.transform.localRotation = Quaternion.Euler(-18f, -140f, 0f);
            _fillLight = fillGO.GetComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.intensity = fillLightIntensity;
            _fillLight.color = new Color(0.96f, 0.96f, 0.98f); // Soft neutral white fill
            if (_previewLayerId >= 0) _fillLight.cullingMask = 1 << _previewLayerId;
        }

        if (_rimLight == null)
        {
            var rimGO = new GameObject("StudioLight_Rim", typeof(Light));
            rimGO.transform.SetParent(_studioRoot.transform, false);
            rimGO.transform.localRotation = Quaternion.Euler(45f, 180f, 0f);
            _rimLight = rimGO.GetComponent<Light>();
            _rimLight.type = LightType.Directional;
            _rimLight.intensity = rimLightIntensity;
            _rimLight.color = Color.white; // Pure white rim for metallic edges and silhouette
            if (_previewLayerId >= 0) _rimLight.cullingMask = 1 << _previewLayerId;
        }

        // 5. Setup Submarine Pivot & Model Instance
        if (_submarinePivot == null)
        {
            _submarinePivot = new GameObject("Submarine_PreviewPivot");
            _submarinePivot.transform.SetParent(_studioRoot.transform, false);
            _submarinePivot.transform.position = studioWorldPosition;
            _submarinePivot.transform.rotation = Quaternion.identity;

            SpawnSubmarineModel();
        }

        // 6. Connect UI Viewport & Rotator
        ConnectViewport();

        _isInitialized = true;
    }

    private void SpawnSubmarineModel()
    {
        GameObject prefabToUse = submarinePrefab;
        if (prefabToUse == null)
        {
            prefabToUse = Resources.Load<GameObject>("Submarine_ShopVisual");
        }
        if (prefabToUse == null)
        {
            prefabToUse = Resources.Load<GameObject>("Prefabs/Submarine/Submarine_ShopVisual");
        }
        if (prefabToUse == null)
        {
            prefabToUse = Resources.Load<GameObject>("Submarine_Visual");
        }

        if (prefabToUse != null)
        {
            _submarineInstance = Instantiate(prefabToUse, _submarinePivot.transform);
            _submarineInstance.transform.localPosition = Vector3.zero;
            _submarineInstance.transform.localRotation = Quaternion.identity;

            // Strip unnecessary gameplay colliders and physics
            foreach (var col in _submarineInstance.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var rb in _submarineInstance.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            // Find or attach SubmarineCustomizer
            if (customizer == null)
            {
                customizer = _submarineInstance.GetComponentInChildren<SubmarineCustomizer>();
                if (customizer == null)
                {
                    customizer = _submarineInstance.AddComponent<SubmarineCustomizer>();
                }
            }

            // Refresh parts first so only the active tier meshes are calculated for bounds
            RefreshCustomizer();

            // Center geometry inside the pivot and frame camera
            CenterGeometry(_submarineInstance);
            FrameCameraToModel(_submarineInstance);

            // Ensure layer isolation
            SetLayerRecursive(_submarinePivot, _previewLayerId >= 0 ? _previewLayerId : 0);
        }
        else
        {
            Debug.LogWarning("[SubmarineShopPreview] No Submarine prefab assigned or found in Resources. Creating placeholder visual.");
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.transform.SetParent(_submarinePivot.transform, false);
            fallback.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            fallback.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
            foreach (var col in fallback.GetComponentsInChildren<Collider>())
                Destroy(col);
            _submarineInstance = fallback;

            FrameCameraToModel(_submarineInstance);
            SetLayerRecursive(_submarinePivot, _previewLayerId >= 0 ? _previewLayerId : 0);

            if (customizer == null)
            {
                customizer = fallback.AddComponent<SubmarineCustomizer>();
            }
        }

        RefreshCustomizer();
    }

    public void BindToRawImage(RawImage rawImage)
    {
        viewportRawImage = rawImage;
        ConnectViewport();
    }

    private void ConnectViewport()
    {
        if (viewportRawImage == null) return;

        UpdateRenderTextureAspect();
        viewportRawImage.texture = _previewRT;
        viewportRawImage.gameObject.SetActive(true);

        // Ensure rotator exists on the viewport
        if (previewRotator == null)
        {
            previewRotator = viewportRawImage.GetComponent<SubmarinePreviewRotator>();
            if (previewRotator == null)
            {
                previewRotator = viewportRawImage.gameObject.AddComponent<SubmarinePreviewRotator>();
            }
        }

        if (previewRotator != null && _submarinePivot != null)
        {
            previewRotator.TargetTransform = _submarinePivot.transform;
            previewRotator.ResetToDefaultAngle();
        }
    }

    /// <summary>
    /// Dynamically sizes the RenderTexture and Camera aspect ratio to match the UI viewport RectTransform.
    /// Eliminates any stretching or distortion so the submarine maintains its true 3D aspect ratio at all times.
    /// </summary>
    public void UpdateRenderTextureAspect()
    {
        float targetAspect = 1.0f;
        if (viewportRawImage != null)
        {
            Rect rect = viewportRawImage.rectTransform.rect;
            if (rect.width > 20f && rect.height > 20f)
            {
                targetAspect = rect.width / rect.height;
            }
            else if (Screen.height > 0)
            {
                // Fallback estimate (~40% shop width, ~85% height)
                targetAspect = (Screen.width * 0.38f) / Mathf.Max(Screen.height * 0.85f, 1f);
            }
        }

        targetAspect = Mathf.Clamp(targetAspect, 0.4f, 2.5f);

        if (_previewRT != null && Mathf.Abs(targetAspect - _cachedAspect) < 0.015f)
        {
            return;
        }

        _cachedAspect = targetAspect;

        int rtHeight = 768;
        int rtWidth  = Mathf.Clamp(Mathf.RoundToInt(rtHeight * targetAspect), 256, 2048);

        if (_previewRT != null)
        {
            if (_previewCamera != null && _previewCamera.targetTexture == _previewRT)
            {
                _previewCamera.targetTexture = null;
            }
            _previewRT.Release();
            Destroy(_previewRT);
            _previewRT = null;
        }

        _previewRT = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "SubmarineShopPreview_RT",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear
        };
        _previewRT.Create();

        if (_previewCamera != null)
        {
            _previewCamera.targetTexture = _previewRT;
            _previewCamera.aspect = targetAspect;
        }

        if (viewportRawImage != null)
        {
            viewportRawImage.texture = _previewRT;
        }

        if (_submarineInstance != null)
        {
            FrameCameraToModel(_submarineInstance);
        }
    }

    // -----------------------------------------------------------------------
    // Camera Framing & Alignment
    // -----------------------------------------------------------------------

    private void CenterGeometry(GameObject model)
    {
        if (model == null) return;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 centerOffset = studioWorldPosition - bounds.center;
        model.transform.position += centerOffset;
    }

    private void FrameCameraToModel(GameObject model)
    {
        if (_previewCamera == null || model == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            _previewCamera.transform.position = studioWorldPosition + new Vector3(0f, cameraHeight, -cameraDistance);
            _previewCamera.transform.LookAt(studioWorldPosition);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float fovRad   = _previewCamera.fieldOfView * Mathf.Deg2Rad;
        float halfFovY = fovRad * 0.5f;
        float aspect   = _previewCamera.aspect > 0.01f ? _previewCamera.aspect : 1.0f;
        float halfFovX = Mathf.Atan(Mathf.Tan(halfFovY) * aspect);

        float extY = bounds.extents.y;
        float extX = bounds.extents.x;
        float extZ = bounds.extents.z;

        float horizontalRadius = Mathf.Max(extX, extZ, 0.05f);
        float verticalRadius   = Mathf.Max(extY, 0.05f);

        float distVertical   = verticalRadius / Mathf.Tan(halfFovY);
        float distHorizontal = horizontalRadius / Mathf.Tan(halfFovX);

        // Frame with comfortable margin so rotatable submarine never clips the edges
        float calculatedDist = Mathf.Max(distVertical, distHorizontal) * 1.30f;
        calculatedDist = Mathf.Max(calculatedDist, 1.2f);

        _previewCamera.farClipPlane = Mathf.Max(200f, calculatedDist * 3f);
        _previewCamera.transform.position = studioWorldPosition + new Vector3(0f, cameraHeight, -calculatedDist);
        _previewCamera.transform.LookAt(studioWorldPosition);
    }

    // -----------------------------------------------------------------------
    // Visibility & Lifecycle
    // -----------------------------------------------------------------------

    public void SetVisible(bool isVisible)
    {
        if (_previewCamera != null)
        {
            _previewCamera.enabled = isVisible;
        }

        if (isVisible)
        {
            UpdateRenderTextureAspect();
            RefreshCustomizer();
            if (_submarineInstance != null)
            {
                CenterGeometry(_submarineInstance);
                FrameCameraToModel(_submarineInstance);
            }
        }
    }

    public void ResetRotation()
    {
        if (previewRotator != null)
        {
            previewRotator.ResetToDefaultAngle();
        }
    }

    public void RefreshCustomizer()
    {
        if (customizer != null)
        {
            customizer.RefreshAllFromGameManager();
        }
    }

    // -----------------------------------------------------------------------
    // Event Subscriptions
    // -----------------------------------------------------------------------

    private void SubscribeToShopEvents()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
            ShopManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
        }
    }

    private void UnsubscribeFromShopEvents()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
        }
    }

    private void HandleUpgradePurchased(UpgradeCategory category, int newTier)
    {
        if (customizer != null)
        {
            customizer.SetCategoryVisual(category, newTier, animate: true);
            if (_submarineInstance != null)
            {
                CenterGeometry(_submarineInstance);
                FrameCameraToModel(_submarineInstance);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Cleanup
    // -----------------------------------------------------------------------

    private void CleanupStudio()
    {
        if (_studioRoot != null)
        {
            Destroy(_studioRoot);
            _studioRoot = null;
        }

        if (_previewRT != null)
        {
            _previewRT.Release();
            Destroy(_previewRT);
            _previewRT = null;
        }

        _isInitialized = false;
        _cachedAspect = -1f;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            if (child != null) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
