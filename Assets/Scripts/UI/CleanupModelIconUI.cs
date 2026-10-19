using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>One rendered thumbnail of the actual recovered model, without a per-item live camera.</summary>
[RequireComponent(typeof(RawImage))]
public class CleanupModelIconUI : MonoBehaviour
{
    private RenderTexture _texture;

    public void SetModel(GameObject source)
    {
        if (source == null) return;
        int layer = LayerMask.NameToLayer("ModelPreview");
        if (layer < 0) return;
        var stage = new GameObject("CleanupIconStage");
        stage.SetActive(false);
        stage.transform.position = new Vector3(24000f, 24000f, 24000f);
        var model = Instantiate(source, stage.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, 25f, 0f) * source.transform.localRotation;
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        model.SetActive(true);
        stage.SetActive(true);
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { stage.SetActive(false); Destroy(stage); return; }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += stage.transform.position - bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        var cameraGO = new GameObject("IconCamera", typeof(Camera));
        cameraGO.transform.SetParent(stage.transform, false);
        var iconCamera = cameraGO.GetComponent<Camera>();
        iconCamera.enabled = false;
        iconCamera.orthographic = true;
        iconCamera.orthographicSize = radius * 1.08f;
        iconCamera.clearFlags = CameraClearFlags.SolidColor;
        iconCamera.backgroundColor = Color.clear;
        iconCamera.cullingMask = 1 << layer;
        iconCamera.nearClipPlane = radius * 0.01f;
        iconCamera.farClipPlane = radius * 10f;
        iconCamera.transform.position = stage.transform.position + new Vector3(0f, radius * 0.8f, -radius * 4f);
        iconCamera.transform.LookAt(stage.transform.position);
        var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        var lightGO = new GameObject("IconLight", typeof(Light));
        lightGO.transform.SetParent(stage.transform, false);
        lightGO.transform.localPosition = new Vector3(-radius, radius * 2f, -radius * 2f);
        var key = lightGO.GetComponent<Light>();
        key.type = LightType.Point;
        key.range = radius * 10f;
        key.intensity = 4f;
        key.cullingMask = 1 << layer;
        _texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
        {
            name = "CleanupIcon_" + source.name,
            antiAliasing = 2
        };
        _texture.Create();
        iconCamera.targetTexture = _texture;
        var image = GetComponent<RawImage>();
        image.texture = _texture;
        image.raycastTarget = false;
        RenderTexture previous = RenderTexture.active;
        bool fog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            iconCamera.Render();
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderTexture.active = previous;
            iconCamera.targetTexture = null;
            stage.SetActive(false);
            Destroy(stage);
        }
    }

    private void OnDestroy()
    {
        if (_texture == null) return;
        _texture.Release();
        Destroy(_texture);
    }
}
