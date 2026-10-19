using UnityEditor;
using UnityEngine;

/// <summary>Launch a disposable review in Play Mode without completing zone objectives.</summary>
public static class ResearchVerificationQuickTest
{
    [MenuItem("HadalDescent/Research/Quick Test/Sunlight (5 questions)")]
    private static void Sunlight() => Launch(0);
    [MenuItem("HadalDescent/Research/Quick Test/Twilight (7 questions)")]
    private static void Twilight() => Launch(1);
    [MenuItem("HadalDescent/Research/Quick Test/Midnight (9 questions)")]
    private static void Midnight() => Launch(2);
    [MenuItem("HadalDescent/Research/Quick Test/Abyss (11 questions)")]
    private static void Abyss() => Launch(3);
    [MenuItem("HadalDescent/Research/Quick Test/Hadal (13 questions)")]
    private static void Hadal() => Launch(4);

    [MenuItem("HadalDescent/Research/Quick Test/Sunlight (5 questions)", true)]
    [MenuItem("HadalDescent/Research/Quick Test/Twilight (7 questions)", true)]
    [MenuItem("HadalDescent/Research/Quick Test/Midnight (9 questions)", true)]
    [MenuItem("HadalDescent/Research/Quick Test/Abyss (11 questions)", true)]
    [MenuItem("HadalDescent/Research/Quick Test/Hadal (13 questions)", true)]
    private static bool CanLaunch() => EditorApplication.isPlaying && !EditorApplication.isPaused;

    private static void Launch(int zoneIndex)
    {
        if (!CanLaunch()) return;
        if ((TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive) ||
            (CinematicSequencer.Instance != null && CinematicSequencer.Instance.IsPlaying))
        {
            Debug.LogWarning("[ResearchQuickTest] Open Assets/Scenes/MainMenu.unity directly and enter Play Mode to test without the Sunlight tutorial or cinematics.");
            return;
        }
        UIManager.Instance?.HideMinigamesAndModals();
        MinigameInstructionOverlay.Instance?.HideInstant();
        RadioTextboxUI.Instance?.HideInstant();
        var review = ResearchVerificationMinigame.Instance
            ?? ZoneManager.Instance.gameObject.AddComponent<ResearchVerificationMinigame>();
        review.OpenForTesting(zoneIndex);
    }
}
