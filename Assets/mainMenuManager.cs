using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string NewGameSceneName = "IntroScene";
    private bool newGameRequested;

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
        if (newGameRequested)
        {
            Debug.LogWarning("New Game was already requested. Wait for the scene load or resolve the reported failure.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(NewGameSceneName))
        {
            Debug.LogError("New Game rejected: IntroScene is not loadable. Existing progress was preserved.", this);
            return;
        }
        string reason;
        if (!IsNewGameContextSafe(out reason))
        {
            Debug.LogWarning("New Game rejected: " + reason + " Existing progress was preserved.", this);
            return;
        }

        newGameRequested = true;
        bool resetCompleted = false;
        string operation = "tutorial presentation reset";
        try
        {
            if (GameplaySystemTutorialManager.HasInstance)
                GameplaySystemTutorialManager.Instance.ResetForNewGame();
            operation = "Journal notification reset";
            JournalEntryPresentationController.Instance?.ResetForNewGame();
            operation = "Journal attention reset";
            JournalHUDEntryAttention.Instance?.ResetForNewGame();
            operation = "task presentation and cache reset";
            // TaskManager clears caches and invalidates callbacks atomically, without flag events.
            TaskManager.Instance?.ResetForNewGame();
            operation = "story, quest, unlock and tutorial data reset";
            SessionStoryState.ResetDataForNewGame();
            operation = "Inventory ownership reset";
            InventoryManager.Instance?.ClearOwnedItemsForNewGame();
            operation = "static playthrough reset";
            GameFlags.ResetForNewGame();
            SleepInteraction.ResetForNewGame();
            CameraFocusTrigger.ResetForNewGame();
            TaskStageTimelineWaypointSequence.ResetForNewGame();
            PlayerMovement.movementDone = false;
            TutorialState.movementDone = false;
            SpawnData.spawnPointName = string.Empty;
            resetCompleted = true;
            operation = "IntroScene load";
            SceneManager.LoadScene(NewGameSceneName);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("New Game failed during " + operation + ". " +
                (resetCompleted ? "The reset completed, but scene loading failed." :
                    "Reset may be partial; IntroScene was not loaded. Resolve the failure before restarting.") +
                " No rollback was attempted.", this);
            Debug.LogException(exception, this);
        }
    }

    private bool IsNewGameContextSafe(out string reason)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || Application.isLoadingLevel || !isActiveAndEnabled || !scene.isLoaded ||
            gameObject.scene != scene || (scene.name != "SceneMenu" && scene.name != "MainMenu") ||
            SceneManager.sceneCount != 1)
        {
            reason = "the active scene must be a single, fully loaded main menu.";
            return false;
        }
        if (Time.timeScale <= 0f || StorySequenceCoordinator.IsStorySequenceActive)
        {
            reason = "a pause or story presentation still owns the session.";
            return false;
        }
        if (FindObjectsByType<TransportationSceneController>(FindObjectsInactive.Include).Length > 0)
        {
            reason = "a transport controller is still present; finish its handoff first.";
            return false;
        }
        if (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered)
        {
            reason = "the iris transition is still covered.";
            return false;
        }
        // The iris Canvas is enabled throughout closing/opening, before IsCovered becomes true.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (canvas.enabled && canvas.gameObject.activeInHierarchy && canvas.sortingOrder >= 32100)
            {
                reason = "a full-screen transition overlay is still enabled.";
                return false;
            }
        foreach (var fade in FindObjectsByType<FadeController>())
            if (fade.isActiveAndEnabled && !fade.IncomingFadeCompleted)
            {
                reason = "a fade has not reported completion.";
                return false;
            }
        if (DialogueEditor.ConversationManager.Instance != null &&
            DialogueEditor.ConversationManager.Instance.IsConversationActive)
        {
            reason = "a conversation is still active.";
            return false;
        }
        foreach (var focus in FindObjectsByType<CameraFocusManager>())
            if (focus.IsFocusing)
            {
                reason = "a camera focus sequence is still active.";
                return false;
            }
        reason = null;
        return true;
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
