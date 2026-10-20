using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>Camera-local water/silt rendering. Never changes shared renderer assets or global volumes.</summary>
[RequireComponent(typeof(Camera))]
public sealed class HazardDodgeAtmosphere : MonoBehaviour
{
    private Camera _camera;
    private Material _material;
    private WaterPass _pass;
    private ParticleSystem _snow, _silt;
    public bool SupportsScreenEffect => _pass != null && _material != null &&
        _pass.LastRenderedFrame >= Time.frameCount - 1;

    public void Initialize(Material template, Material particles, float laneWidth)
    {
        _camera = GetComponent<Camera>();
        if (template != null && template.shader != null && template.shader.isSupported)
        {
            _material = new Material(template);
            _pass = new WaterPass(_material);
        }
        _snow = CreateParticles("MarineSnow", particles, new Vector3(laneWidth * 3f, 4f, 42f),
            64, new Color(.52f,.83f,.87f,.20f), .035f, .13f);
        _snow.transform.localPosition = new Vector3(0f,1.8f,16f);
        var emission = _snow.emission;
        emission.rateOverTime = 15f;
        _silt = CreateParticles("StirredSilt", particles, new Vector3(5f,2.4f,7f),
            80, new Color(.58f,.43f,.24f,.50f), .4f, 1.6f);
        _silt.transform.localPosition = new Vector3(0f,1.1f,.8f);
    }

    private ParticleSystem CreateParticles(string name, Material material, Vector3 box, int cap,
        Color color, float minSize, float maxSize)
    {
        var go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(transform.parent, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = cap;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f,4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize,maxSize);
        main.startColor = color;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = .06f;
        velocity.y = .1f;
        velocity.z = -.6f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .16f;
        noise.frequency = .3f;
        noise.scrollSpeed = .12f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,1),new Keyframe(.8f,1),new Keyframe(1,0)));
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    public void Tick(HazardDodgeConditions conditions, float movement, float submarineX, int zone)
    {
        if (_material != null)
        {
            Vector3 point = _camera.WorldToViewportPoint(transform.parent.position + new Vector3(submarineX,0f,-2.2f));
            _material.SetVector("_WaterState", new Vector4(conditions.Sediment, conditions.Pressure,
                conditions.Clock, zone / 4f));
            _material.SetVector("_Submarine", new Vector4(point.x,point.y,_camera.aspect,0f));
        }
        if (_silt != null)
        {
            _silt.transform.localPosition = new Vector3(submarineX,1.1f,.8f);
            var emission = _silt.emission;
            emission.rateOverTime = conditions.Sediment * (32f + movement * 24f);
            var velocity = _silt.velocityOverLifetime;
            velocity.x = conditions.CurrentDirection * conditions.Pressure * 1.1f;
            velocity.y = .12f + movement * .3f;
        }
        if (_snow != null)
        {
            var velocity = _snow.velocityOverLifetime;
            velocity.x = .06f + conditions.CurrentDirection * conditions.Pressure * .6f;
        }
    }

    public void ClearConditions()
    {
        if (_material != null) _material.SetVector("_WaterState", Vector4.zero);
        if (_silt != null) _silt.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void SetPaused(bool paused)
    {
        PauseParticles(_snow,paused);
        PauseParticles(_silt,paused);
    }

    private static void PauseParticles(ParticleSystem ps, bool paused)
    {
        if (ps == null) return;
        if (paused) ps.Pause();
        else ps.Play();
    }

    private void OnEnable() => RenderPipelineManager.beginCameraRendering += Enqueue;
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Enqueue;
        _pass?.Dispose();
    }
    private void Enqueue(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera || _pass == null) return;
        camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);
    }
    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private sealed class WaterPass : ScriptableRenderPass
    {
        public int LastRenderedFrame { get; private set; } = -2;
        private readonly Material _material;
        private RTHandle _copy;
        public WaterPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        private sealed class PassData { public TextureHandle Source; public Material Material; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            desc.name = "HazardWaterCopy";
            desc.clearBuffer = false;
            var copy = graph.CreateTexture(desc);
            graph.AddBlitPass(source,copy,Vector2.one,Vector2.zero,passName: "Hazard Water Copy");
            using (var builder = graph.AddRasterRenderPass<PassData>("Hazard Water and Sediment",out var data))
            {
                data.Source = copy;
                data.Material = _material;
                builder.UseTexture(copy,AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture,AccessFlags.Read);
                builder.SetRenderAttachment(source,0,AccessFlags.Write);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd,pass.Source,new Vector4(1,1,0,0),pass.Material,0);
                    LastRenderedFrame = Time.frameCount;
                });
            }
        }

        // Support projects switching URP to compatibility mode without changing renderer assets.
#pragma warning disable CS0672, CS0618
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _copy,descriptor,name: "HazardWaterCopy");
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (_copy == null) return;
            var cmd = CommandBufferPool.Get("Hazard Water and Sediment");
            var target = data.cameraData.renderer.cameraColorTargetHandle;
            Blitter.BlitCameraTexture(cmd,target,_copy);
            Blitter.BlitCameraTexture(cmd,_copy,target,_material,0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            LastRenderedFrame = Time.frameCount;
        }
#pragma warning restore CS0672, CS0618
        public void Dispose() { _copy?.Release(); _copy = null; LastRenderedFrame = -2; }
    }
}
