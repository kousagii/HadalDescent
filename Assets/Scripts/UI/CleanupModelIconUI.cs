using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>Renders once into a CPU-backed thumbnail; never displays an unresolved GPU target.</summary>
[RequireComponent(typeof(RawImage))]
public class CleanupModelIconUI : MonoBehaviour
{
    private Texture2D _texture;
    private GameObject _source;
    private GameObject _stage;
    private Coroutine _capture;

    public void SetModel(GameObject source)
    {
        _source = source;
        GetComponent<RawImage>().raycastTarget = false;
        QueueCapture();
    }

    private void OnEnable()
    {
        if (_texture == null && _source != null) QueueCapture();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused && isActiveAndEnabled && _source != null) QueueCapture();
    }

    private void QueueCapture()
    {
        if (_capture != null) StopCoroutine(_capture);
        _capture = null;
        ReleaseStage();
        GetComponent<RawImage>().enabled = _texture != null;
        if (_source != null && isActiveAndEnabled) _capture = StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        // Each stage is active only during its synchronous offscreen request,
        // after the main camera frame, so captures cannot overlap one another.
        yield return new WaitForEndOfFrame();
        _capture = null;
        if (_source == null) yield break;
        int layer = LayerMask.NameToLayer("ModelPreview");
        if (layer < 0) { Debug.LogWarning("[CleanupPreview] ModelPreview layer is missing."); yield break; }
        RenderTexture target = null;
        Texture2D snapshot = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            _stage = new GameObject("CleanupIconStage");
            _stage.SetActive(false);
            _stage.transform.position = new Vector3(0f, -20f, 0f);
            var model = Instantiate(_source, _stage.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 25f, 0f) * _source.transform.localRotation;
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            model.SetActive(true);
            _stage.SetActive(true);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) yield break;
            Bounds bounds = Measure(renderers);
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (diameter <= 0.00001f) yield break;
            model.transform.localScale /= diameter;
            bounds = Measure(renderers);
            model.transform.position += _stage.transform.position - bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            var cameraGO = new GameObject("CleanupIconCamera", typeof(Camera));
            cameraGO.transform.SetParent(_stage.transform, false);
            var iconCamera = cameraGO.GetComponent<Camera>();
            iconCamera.enabled = false;
            iconCamera.orthographic = true;
            iconCamera.orthographicSize = radius * 1.08f;
            iconCamera.clearFlags = CameraClearFlags.SolidColor;
            iconCamera.backgroundColor = Color.clear;
            iconCamera.cullingMask = 1 << layer;
            iconCamera.nearClipPlane = 0.01f;
            iconCamera.farClipPlane = 10f;
            iconCamera.transform.position = _stage.transform.position + new Vector3(0f, radius * 0.8f, -radius * 4f);
            iconCamera.transform.LookAt(_stage.transform.position);
            var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorTexture = false;
            cameraData.requiresDepthTexture = false;
            var lightGO = new GameObject("IconLight", typeof(Light));
            lightGO.transform.SetParent(_stage.transform, false);
            lightGO.transform.localPosition = new Vector3(-radius, radius * 2f, -radius * 2f);
            var key = lightGO.GetComponent<Light>();
            key.type = LightType.Point;
            key.range = radius * 10f;
            key.intensity = 4f;
            key.cullingMask = 1 << layer;
            target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
            {
                name = "CleanupCapture", antiAliasing = 1,
                memorylessMode = RenderTextureMemoryless.None, useMipMap = false
            };
            if (!PreviewRenderUtility.Render(iconCamera, target))
            {
                Debug.LogWarning($"[CleanupPreview] Capture failed for {_source.name}; GPU={SystemInfo.graphicsDeviceName}, API={SystemInfo.graphicsDeviceType}.");
                yield break;
            }
            RenderTexture.active = target;
            snapshot = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            snapshot.name = "CleanupIcon_" + _source.name;
            snapshot.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0, false);
            snapshot.Apply(false, false); // Retain CPU data for Android graphics-context recovery.
            if (_texture != null) Destroy(_texture);
            _texture = snapshot;
            snapshot = null;
            var image = GetComponent<RawImage>();
            image.texture = _texture;
            image.enabled = true;
        }
        finally
        {
            RenderTexture.active = previous;
            if (snapshot != null) Destroy(snapshot);
            if (target != null) { target.Release(); Destroy(target); }
            ReleaseStage();
        }
    }

    private static Bounds Measure(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void ReleaseStage()
    {
        if (_stage == null) return;
        _stage.SetActive(false);
        Destroy(_stage);
        _stage = null;
    }

    private void OnDisable()
    {
        if (_capture != null) StopCoroutine(_capture);
        _capture = null;
        ReleaseStage();
    }

    private void OnDestroy()
    {
        ReleaseStage();
        if (_texture != null) Destroy(_texture);
    }
}
