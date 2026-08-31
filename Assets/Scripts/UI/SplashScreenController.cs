using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashScreenController : MonoBehaviour
{
    [Tooltip("Time in seconds before transitioning to the Main Menu")]
    [SerializeField] private float delay = 3f;
    
    [Tooltip("Exact name of the Main Menu scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainMenuBGM();
        }

        Invoke(nameof(LoadMainMenu), delay);
    }

    private void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
