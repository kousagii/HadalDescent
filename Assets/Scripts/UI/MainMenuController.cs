using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Exact name of the Sunlight Zone scene to load when starting a new game")]
    [SerializeField] private string startingZoneSceneName = "SunlightZone";

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainMenuBGM();
        }
    }

    public void OnNewGameClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        // For now, directly load the Sunlight Zone scene
        // Later, you might want to call ZoneManager.Instance.LoadZone(0, true) if your ZoneManager is already initialized
        SceneManager.LoadScene(startingZoneSceneName);
    }
    
    public void OnQuitClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        Debug.Log("Quit Game Requested");
        Application.Quit();
    }
}
