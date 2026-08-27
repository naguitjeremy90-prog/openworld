using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    private const string MasterVolumeKey = "MasterVolume";

    [Header("Settings UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Slider masterVolumeSlider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSavedMasterVolume()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
    }

    private void Awake()
    {
        float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        ApplyMasterVolume(savedVolume);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (backButton != null)
            backButton.onClick.AddListener(CloseSettings);

        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (backButton != null)
            backButton.onClick.RemoveListener(CloseSettings);
    }

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
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        ShowMainMenu();
    }

    public void SetMasterVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        ApplyMasterVolume(clampedVolume);

        PlayerPrefs.SetFloat(MasterVolumeKey, clampedVolume);
        PlayerPrefs.Save();
    }

    private void ShowMainMenu()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }

    private static void ApplyMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
    }

    public void ExitGame()
    {
        Application.Quit();
        Debug.Log("Game Closed");
    }
}
