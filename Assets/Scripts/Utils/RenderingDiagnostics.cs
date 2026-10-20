using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Development-build diagnostics for device-dependent rendering and camera handoffs.</summary>
public class RenderingDiagnostics : MonoBehaviour
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private float _nextCheck;
    private string _lastConflict = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<RenderingDiagnostics>() != null) return;
        var host = new GameObject("RenderingDiagnostics");
        DontDestroyOnLoad(host);
        host.AddComponent<RenderingDiagnostics>();
        Debug.Log($"[RenderingDiagnostics] Unity={Application.unityVersion}; GPU={SystemInfo.graphicsDeviceName}; " +
            $"API={SystemInfo.graphicsDeviceType}; driver={SystemInfo.graphicsDeviceVersion}; " +
            $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; pipeline={GraphicsSettings.currentRenderPipeline?.name}.");
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1f;
        if (UIManager.IsMenuScene(SceneManager.GetActiveScene().name)) return;
        var screenCameras = new List<string>();
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture != null || camera.cameraType != CameraType.Game) continue;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType == CameraRenderType.Overlay) continue;
            screenCameras.Add(camera.name + " (depth=" + camera.depth + ")");
        }
        string conflict = screenCameras.Count > 1 ? string.Join(", ", screenCameras) : "";
        if (conflict.Length > 0 && conflict != _lastConflict)
            Debug.LogWarning("[RenderingDiagnostics] Multiple base cameras target the screen: " + conflict);
        _lastConflict = conflict;
    }
#endif
}
