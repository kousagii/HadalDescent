using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit offscreen rendering with URP support and restored global state.</summary>
public static class PreviewRenderUtility
{
    public static bool Render(Camera camera, RenderTexture target)
    {
        if (camera == null || target == null || (!target.IsCreated() && !target.Create())) return false;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        bool previousFog = RenderSettings.fog;
        try
        {
            camera.targetTexture = target;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.useOcclusionCulling = false;
            RenderSettings.fog = false;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                camera.Render();
                return true;
            }
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return false;
            RenderPipeline.SubmitRenderRequest(camera, request);
            return true;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderSettings.fog = previousFog;
        }
    }
}
