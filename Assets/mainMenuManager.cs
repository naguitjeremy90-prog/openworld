using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void NewGame()
    {
        SceneManager.LoadScene("IntroScene");
    }

    public void ContinueGame()
    {
        SceneManager.LoadScene("Makamisa");
    }

    public void OpenSettings()
    {
        Debug.Log("Settings Opened");
    }

    public void ExitGame()
    {
        Application.Quit();
        Debug.Log("Game Closed");
    }
}