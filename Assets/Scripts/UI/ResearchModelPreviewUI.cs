using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Independent live model view for each research card. Uses unscaled time because
/// the research check pauses the ocean. Photographs remain ordinary static Images.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ResearchModelPreviewUI : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;
    private const float RenderInterval = 1f / 15f;
    private static readonly Dictionary<string, float> SavedYaw = new Dictionary<string, float>();
    private static readonly HashSet<int> UsedSlots = new HashSet<int>();

    private RawImage _image;
    private GameObject _stage;
    private Transform _pivot;
    private Camera _camera;
    private RenderTexture _texture;
    private string _speciesId;
    private int _slot = -1;
    private float _yaw;
    private float _radius;
    private float _horizontalRadius;
    private float _verticalRadius;
    private float _nextRender;

    public bool Initialize(SpeciesData species)
    {
        if (species == null || species.modelPrefab == null) return false;
        int layer = LayerMask.NameToLayer("ModelPreview");
        if (layer < 0) return false;
        // Reuse the bestiary's preview-layer lighting, without taking over its
        // single inspection model or render target.
        rotationSpeed = ModelPreviewSystem.Instance.IdleRotationSpeed;
        _image = GetComponent<RawImage>();
        _image.raycastTarget = false;
        _speciesId = string.IsNullOrEmpty(species.speciesId) ? species.GetInstanceID().ToString() : species.speciesId;
        _yaw = SavedYaw.TryGetValue(_speciesId, out float saved) ? saved : 135f;
        _slot = 0;
        while (UsedSlots.Contains(_slot)) _slot++;
        UsedSlots.Add(_slot);

        _stage = new GameObject("ResearchModelStage_" + _slot);
        _stage.SetActive(false);
        _stage.transform.position = new Vector3(12000f + _slot * 2000f, 12000f, 12000f);
        var pivot = new GameObject("RotationPivot");
        pivot.transform.SetParent(_stage.transform, false);
        _pivot = pivot.transform;
        var model = Instantiate(species.modelPrefab, _pivot, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, species.modelYawOffset, 0f) * model.transform.localRotation;
        model.transform.localScale *= species.previewScaleMultiplier > 0f ? species.previewScaleMultiplier : 1f;
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Release(); return false; }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += _pivot.position - bounds.center;
        // Frame the full horizontal sweep once, so rotation never causes zoom
        // pumping. Include the camera tilt without wasting the width of the UI.
        _radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        _horizontalRadius = Mathf.Max(new Vector2(bounds.extents.x, bounds.extents.z).magnitude, 0.05f);
        float tilt = Mathf.Atan2(0.5f, 3f);
        _verticalRadius = Mathf.Max(bounds.extents.y * Mathf.Cos(tilt) + _horizontalRadius * Mathf.Sin(tilt), 0.05f);
        _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);

        var cameraGO = new GameObject("ResearchPreviewCamera", typeof(Camera));
        cameraGO.transform.SetParent(_stage.transform, false);
        _camera = cameraGO.GetComponent<Camera>();
        _camera.enabled = false;
        _camera.orthographic = true;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.clear;
        _camera.cullingMask = 1 << layer;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = _radius * 6f + 1f;
        _camera.transform.position = _pivot.position + new Vector3(0f, _radius * 0.5f, -_radius * 3f);
        _camera.transform.LookAt(_pivot.position);
        var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        _texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
        {
            name = "ResearchModelRT_" + _slot,
            antiAliasing = 2,
            filterMode = FilterMode.Bilinear
        };
        _texture.Create();
        _camera.targetTexture = _texture;
        _image.texture = _texture;
        _stage.SetActive(true);
        Render();
        return true;
    }

    private void LateUpdate()
    {
        if (_pivot == null || _image == null || !_image.isActiveAndEnabled) return;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;
        if (_image.canvasRenderer.cull) return;
        _yaw = Mathf.Repeat(_yaw + rotationSpeed * Time.unscaledDeltaTime, 360f);
        _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (Time.unscaledTime < _nextRender) return;
        _nextRender = Time.unscaledTime + RenderInterval;
        Render();
    }

    private void Render()
    {
        Rect area = _image.rectTransform.rect;
        float aspect = area.width > 1f && area.height > 1f ? area.width / area.height : 1f;
        _camera.aspect = aspect;
        _camera.orthographicSize = Mathf.Max(_verticalRadius, _horizontalRadius / aspect) * 1.04f;
        bool fog = RenderSettings.fog;
        RenderTexture active = RenderTexture.active;
        try
        {
            RenderSettings.fog = false;
            _camera.Render();
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderTexture.active = active;
        }
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();

    private void Release()
    {
        if (_pivot != null && !string.IsNullOrEmpty(_speciesId)) SavedYaw[_speciesId] = _yaw;
        if (_image != null) _image.texture = null;
        if (_camera != null) _camera.targetTexture = null;
        if (_stage != null) { _stage.SetActive(false); DestroyOwned(_stage); }
        if (_texture != null) { _texture.Release(); DestroyOwned(_texture); }
        if (_slot >= 0) UsedSlots.Remove(_slot);
        _slot = -1;
        _stage = null;
        _pivot = null;
        _camera = null;
        _texture = null;
    }

    private static void DestroyOwned(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
