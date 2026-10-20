using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class Events { public static readonly List<string> Values = new List<string>(); }
public class Widget
{
    public bool activeSelf = true;
    public Widget gameObject => this;
    public void SetActive(bool active) => activeSelf = active;
}
public class Group { public float alpha = 1f; public bool blocksRaycasts = true, interactable = true; }
public static class Globals { public static MinigameInstructionOverlay Overlay; public static MinigameManager Manager; }
public class Activity
{
    public bool Active = true;
    public int Cancels;
    public void CancelMinigame()
    {
        if (!Globals.Manager.IsMinigameActive) throw new Exception("HUD ownership released during cancellation");
        Active = false;
        Cancels++;
        Events.Values.Add("cancel");
    }
}
public class ResearchVerificationMinigame
{
    public static ResearchVerificationMinigame Instance;
    public Action OnCancel;
    public void CancelMinigame() => OnCancel?.Invoke();
}
public class UIManager
{
    public static UIManager Instance = new UIManager();
    public bool Visible;
    public static bool IsMenuScene(string name) => name == "MainMenu";
    public void SetExplorationHUDVisible(bool visible)
    {
        if (visible && (Globals.Manager.IsMinigameActive || Globals.Overlay.IsOpen))
            throw new Exception("HUD restored before minigame/overlay teardown");
        Visible = visible;
        Events.Values.Add("hud");
    }
}
public static class SceneManager { public static Scene GetActiveScene() => new Scene(); }
public class Scene { public string name = "SunlightZone"; }

namespace UnityEngine
{
    public enum FindObjectsInactive { Include }
    public static class Time { public static float timeScale; }
    public struct Color { public static Color clear => new Color(); }
    public class Camera
    {
        public string Name = "";
        private bool _enabled;
        public bool enabled { get => _enabled; set { _enabled = value; Events.Values.Add(Name + value); } }
        public bool allowHDR = true, allowMSAA = true, allowDynamicResolution = true, useOcclusionCulling = true;
        public RenderTexture targetTexture;
        public int RenderCalls;
        public void Render() => RenderCalls++;
    }
    public class RenderTexture
    {
        public static RenderTexture active;
        public bool Created, CanCreate = true;
        public bool IsCreated() => Created;
        public bool Create() { Created = CanCreate; return Created; }
    }
    public static class RenderSettings { public static bool fog; }
    public static class GL { public static void Clear(bool depth, bool color, Color value) { } }
}
namespace UnityEngine.Rendering
{
    public static class GraphicsSettings { public static object currentRenderPipeline; }
    public static class RenderPipeline
    {
        public static bool Supported = true, Throw;
        public static int Submissions;
        public static bool SupportsRenderRequest<T>(Camera camera, T request) => Supported;
        public static void SubmitRenderRequest<T>(Camera camera, T request)
        {
            Submissions++;
            if (Throw) throw new InvalidOperationException("Simulated driver exception");
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipeline { public class SingleCameraRequest { public RenderTexture destination; } }
}

public static class Program
{
    private static int _checks;
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }
    private static MinigameInstructionOverlay Arm(Action callback)
    {
        var overlay = new MinigameInstructionOverlay { _isOpen = true, _isDragging = true, _onFinishedCallback = callback };
        Globals.Overlay = overlay;
        return overlay;
    }

    public static void Main()
    {
        int completed = 0;
        var overlay = Arm(() => completed++);
        overlay.CompleteForTest();
        overlay.CompleteForTest();
        Check(completed == 1, "Continue/slide-skip launches exactly once");
        Check(!overlay.IsOpen && overlay._onFinishedCallback == null, "Close clears state and pending callback");
        Check(!overlay._canvasGroup.blocksRaycasts && !overlay._canvasGroup.interactable && overlay._canvasGroup.alpha == 0, "Close releases input");
        overlay = Arm(() => completed++);
        overlay.CancelSlideshow();
        overlay.CompleteForTest();
        Check(completed == 1 && !overlay.IsOpen, "Global cancellation cannot launch pending game");
        overlay = Arm(() => { Globals.Overlay._isOpen = true; Globals.Overlay._onFinishedCallback = () => completed += 10; });
        overlay.CompleteForTest();
        Check(overlay._isOpen && overlay._onFinishedCallback != null, "Reentrant callback may install a new activity safely");

        overlay = Arm(() => completed++);
        var manager = new MinigameManager { _minigameActive = true };
        Globals.Manager = manager;
        Check(manager.CurrentForTest(0), "Initial activity ticket valid");
        ResearchVerificationMinigame.Instance = new ResearchVerificationMinigame { OnCancel = manager.CancelActiveMinigame };
        Events.Values.Clear();
        manager.CancelActiveMinigame();
        Check(!overlay.IsOpen && !manager.IsMinigameActive, "Cancel releases overlay and manager state");
        Check(manager._mg1.Cancels == 1 && manager._mg4.Cancels == 1, "Reentrant cancellation runs once");
        Check(!manager._mg1.Active && !manager._mg2.Active && !manager._mg3.Active && !manager._mg4.Active, "All activities stopped");
        Check(manager.ControlsEnabled && UIManager.Instance.Visible && Time.timeScale == 1f, "Controls, HUD and time restored");
        Check(Events.Values[Events.Values.Count - 1] == "hud", "HUD restore occurs last");
        Check(!manager.CurrentForTest(0), "Old launch/completion callbacks invalidated");
        overlay.CompleteForTest();
        Check(completed == 1, "Cancelled launch stays cancelled after cleanup");
        manager.CancelActiveMinigame();
        Check(!manager.IsMinigameActive && UIManager.Instance.Visible, "Repeated cancellation is safe");

        var stage = new Camera { Name = "stage", enabled = true };
        var exploration = new Camera { Name = "world", enabled = false };
        var cleanup = new CleanupCameraHost { _minigameCam = stage };
        cleanup._disabledSceneCams.Add(exploration);
        Events.Values.Clear();
        cleanup.RestoreForTest();
        Check(Events.Values[0] == "stageFalse" && Events.Values[1] == "worldTrue", "Cleanup screen camera stopped before world restore");
        Check(!stage.enabled && exploration.enabled && cleanup._disabledSceneCams.Count == 0, "Cleanup camera ownership restored");
        var hazard = new HazardCameraHost { _stageCamera = stage };
        hazard._disabledSceneCams.Add(exploration);
        Events.Values.Clear();
        hazard.RestoreForTest();
        Check(Events.Values[0] == "stageFalse" && Events.Values[1] == "worldTrue", "Hazard screen camera stopped before world restore");

        var previousActive = new RenderTexture();
        var previousTarget = new RenderTexture();
        var target = new RenderTexture();
        var camera = new Camera { targetTexture = previousTarget };
        RenderTexture.active = previousActive;
        RenderSettings.fog = true;
        GraphicsSettings.currentRenderPipeline = new object();
        Check(PreviewRenderUtility.Render(camera, target), "URP request accepted");
        Check(RenderPipeline.Submissions == 1 && camera.RenderCalls == 0, "URP uses supported request API");
        Check(camera.targetTexture == previousTarget && RenderTexture.active == previousActive && RenderSettings.fog, "Offscreen global state restored");
        Check(!camera.allowMSAA && !camera.allowHDR && !camera.allowDynamicResolution && !camera.useOcclusionCulling, "Stable offscreen camera settings");
        RenderPipeline.Supported = false;
        Check(!PreviewRenderUtility.Render(camera, target), "Unsupported pipeline rejected");
        Check(camera.targetTexture == previousTarget && RenderSettings.fog, "Unsupported request restores state");
        RenderPipeline.Supported = true;
        RenderPipeline.Throw = true;
        bool threw = false;
        try { PreviewRenderUtility.Render(camera, target); } catch (InvalidOperationException) { threw = true; }
        Check(threw && camera.targetTexture == previousTarget && RenderTexture.active == previousActive && RenderSettings.fog, "Driver exception cannot leak render state");
        RenderPipeline.Throw = false;
        Check(!PreviewRenderUtility.Render(camera, new RenderTexture { CanCreate = false }), "Failed allocation rejected");
        Check(camera.targetTexture == previousTarget, "Failed allocation leaves camera target unchanged");
        GraphicsSettings.currentRenderPipeline = null;
        Check(PreviewRenderUtility.Render(camera, target) && camera.RenderCalls == 1, "Built-in pipeline fallback preserved");
        Console.WriteLine($"Android rendering/cancellation control-flow checks passed: {_checks} assertions. GPU pixels are not simulated.");
    }
}
