using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kind = HazardDodgeMinigame.HazardType;

/// <summary>Capture the real runtime stage in disposable Play Mode; never awards or deducts RDP.</summary>
[InitializeOnLoad]
public static class HazardDodgeTutorialCapture
{
    private const string SessionKey = "HazardTutorialCapture.Pending";
    private const string SceneKey = "HazardTutorialCapture.Scene";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static HazardDodgeMinigame game;
    private static GameObject canvasRoot;
    private static Canvas[] hiddenCanvases;
    private static int page;
    private static double nextTime;
    private static bool waitingForFile;
    private static string Output => Path.GetFullPath("output/TutorialCaptures/Minigame4");

    static HazardDodgeTutorialCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("HadalDescent/Hazard Dodge/Capture Tutorial Screenshots")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying || SessionState.GetBool(SessionKey, false)) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[HazardCapture] Save your current scene before starting the disposable tutorial capture.");
            return;
        }
        SessionState.SetString(SceneKey, scene.path);
        SessionState.SetBool(SessionKey, true);
        Directory.CreateDirectory(Output);
        File.Delete(Path.Combine(Output, "capture-error.txt"));
        File.Delete(Path.Combine(Output, "capture.txt"));
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += Begin;
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Update;
            SessionState.SetBool(SessionKey, false);
            string path = SessionState.GetString(SceneKey, "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }
    }

    private static void Begin()
    {
        try
        {
            hiddenCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in hiddenCanvases) canvas.enabled = false;
            canvasRoot = new GameObject("HazardTutorialCaptureCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvasComponent = canvasRoot.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 32000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Minigame4HUD.prefab");
            var hud = UnityEngine.Object.Instantiate(prefab, canvasRoot.transform);
            // The isolated menu scene has no exploration HUD holding these atlases.
            AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/UI/buttons-prototype.png");
            AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/UI/buttons-prototype 1.png");
            game = hud.GetComponent<HazardDodgeMinigame>();
            game.Initialize(0, null, null);
            game.Show();
            game.StopAllCoroutines();
            Call("HideAllBanners");
            Call("SwitchToMinigameCamera");
            hud.SetActive(true);
            var group = hud.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1;
            ClearImage("_explorationVignette");
            ClearImage("customRedVignette");
            page = 0;
            PreparePage();
            EditorApplication.update += Update;
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || game == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup < nextTime) return;
            string path = Path.Combine(Output, "page-" + (page + 1) + ".png");
            if (!waitingForFile)
            {
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(path, 2);
                waitingForFile = true;
                nextTime = EditorApplication.timeSinceStartup + 1;
                return;
            }
            if (!File.Exists(path)) throw new IOException("Screenshot was not written: " + path);
            page++;
            if (page < 4) PreparePage();
            else
            {
                File.WriteAllText(Path.Combine(Output, "capture.txt"), "Four native runtime captures completed. Rewards and penalties were not invoked.");
                Cleanup();
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void PreparePage()
    {
        var items = (IList)Get("_activeItems");
        foreach (var item in items)
            UnityEngine.Object.Destroy((GameObject)item.GetType().GetField("GameObject").GetValue(item));
        items.Clear();
        Call("HideAllBanners");
        Set("_remainingHits", page >= 2 ? 2 : 3);
        Set("_distanceLeft", page == 1 ? 65f : page >= 2 ? 86f : 80f);
        Set("_collectedRdp", page == 1 ? 5 : 0);
        float width = (float)Get("laneWidth");
        int lane = page == 0 ? 0 : 1;
        Set("_currentLane", lane);
        Set("_targetX", (lane - 1) * width);
        Set("_currentX", (lane - 1) * width);
        Set("_steeringX", (lane - 1) * width);
        Call("UpdateSubmarineMotion", 0f, 18f);
        if (page == 0)
        {
            Spawn(Kind.RockOutcrop, 1, 6, 0);
            Spawn(Kind.CurrentSurge, 2, 12, 0, -1);
            Spawn(Kind.SedimentCloud, 0, 16, 0);
        }
        else
        {
            Spawn(Kind.DataPod, 1, 7, 1);
            Spawn(Kind.RockOutcrop, 0, 15, 1);
            Spawn(Kind.CurrentSurge, 2, 17, 1, -1);
        }
        if (page == 3) Call("ShowFinishBanner", false, 0);
        Call("UpdateHUD");
        waitingForFile = false;
        nextTime = EditorApplication.timeSinceStartup + 1.2;
    }

    private static void Spawn(Kind kind, int lane, float z, int open, int direction = 0)
        => Call("SpawnItem", kind, lane, z, open, direction);
    private static object Get(string name) => typeof(HazardDodgeMinigame).GetField(name, Private).GetValue(game);
    private static void Set(string name, object value) => typeof(HazardDodgeMinigame).GetField(name, Private).SetValue(game, value);
    private static void Call(string name, params object[] args) => typeof(HazardDodgeMinigame).GetMethod(name, Private).Invoke(game, args);
    private static void ClearImage(string name) { if (Get(name) is Image image) image.color = Color.clear; }
    private static void Fail(Exception exception)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "capture-error.txt"), exception.ToString());
        Debug.LogException(exception);
        Cleanup();
    }
    private static void Cleanup()
    {
        EditorApplication.update -= Update;
        if (game != null) game.CancelMinigame();
        if (canvasRoot != null) UnityEngine.Object.Destroy(canvasRoot);
        if (hiddenCanvases != null) foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
        EditorApplication.isPlaying = false;
    }
}
