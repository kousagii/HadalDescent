using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Preview cleanup in Play Mode, including all ten debris variants in the sorting preview.</summary>
public static class CleanupQuickTest
{
    [MenuItem("HadalDescent/Cleanup/Validate Connected Assets")]
    public static void ValidateAssetsForBatch()
    {
        var library = Resources.Load<CleanupModelLibrary>("Minigames/CleanupModelLibrary");
        if (library == null) throw new System.InvalidOperationException("CleanupModelLibrary could not be loaded.");
        var connected = new[] { library.sodaCan, library.metalPipe, library.metalTube, library.metalDrum,
            library.plasticBin, library.metalBin, library.hazardBin };
        foreach (var model in connected)
            if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new System.InvalidOperationException("A connected cleanup model is missing or has no renderers.");
        Debug.Log("[CleanupValidation] All four metal models and three sorting bins are connected and renderable.");
    }

    [MenuItem("HadalDescent/Cleanup/Quick Test/Collection - Sunlight")]
    private static void Collection() => Launch(false, 0);
    [MenuItem("HadalDescent/Cleanup/Quick Test/Collection - Hadal")]
    private static void DeepCollection() => Launch(false, 4);
    [MenuItem("HadalDescent/Cleanup/Quick Test/Sorting - All Debris")]
    private static void Sorting() => Launch(true, 0);

    [MenuItem("HadalDescent/Cleanup/Quick Test/Collection - Sunlight", true)]
    [MenuItem("HadalDescent/Cleanup/Quick Test/Collection - Hadal", true)]
    [MenuItem("HadalDescent/Cleanup/Quick Test/Sorting - All Debris", true)]
    private static bool CanLaunch() => EditorApplication.isPlaying && !EditorApplication.isPaused;

    private static void Launch(bool sortingOnly, int zone)
    {
        if (!CanLaunch()) return;
        if (!UIManager.IsMenuScene(SceneManager.GetActiveScene().name))
        {
            Debug.LogWarning("[CleanupQuickTest] Open Assets/Scenes/MainMenu.unity and enter Play Mode for an isolated preview.");
            return;
        }
        if ((MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive) ||
            (ResearchVerificationMinigame.Instance != null && ResearchVerificationMinigame.Instance.IsOpen) ||
            (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive) ||
            (CinematicSequencer.Instance != null && CinematicSequencer.Instance.IsPlaying) ||
            (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused))
        {
            Debug.LogWarning("[CleanupQuickTest] Finish the active activity first. For an isolated test, open MainMenu and enter Play Mode.");
            return;
        }
        var candidates = Object.FindObjectsByType<EnvironmentalCleanupMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EnvironmentalCleanupMinigame game = null;
        foreach (var candidate in candidates)
        {
            if (candidate.IsActive) return;
            if (game == null || candidate.customUIRoot != null) game = candidate;
        }
        if (game == null) game = new GameObject("CleanupPlaytest").AddComponent<EnvironmentalCleanupMinigame>();
        UIManager.Instance?.HideMinigamesAndModals();
        MinigameInstructionOverlay.Instance?.HideInstant();
        RadioTextboxUI.Instance?.HideInstant();
        game.OpenForTesting(zone, sortingOnly);
    }
}
