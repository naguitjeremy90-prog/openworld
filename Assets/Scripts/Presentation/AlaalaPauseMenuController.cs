using System.Collections.Generic;
using DialogueEditor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Reusable scene-local ESC menu backed by an editable authored prefab.</summary>
public sealed class AlaalaPauseMenuController : MonoBehaviour
{
    private const string ResourcePath = "UI/AlaalaPauseMenu";
    private const string MasterVolumeKey = "MasterVolume";

    [Header("Authored Views")]
    [SerializeField] private string mainMenuSceneName = "SceneMenu";
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject hintRoot;
    [Header("Main Menu Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [Header("Settings Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Button backButton;

    private readonly List<Resolution> resolutions = new List<Resolution>();
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool isOpen;
    private bool inSettings;
    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGameplayScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "LoginPage" || scene == "MainMenu" || scene == "SceneMenu" ||
            FindAnyObjectByType<AlaalaPauseMenuController>() != null) return;
        AlaalaPauseMenuController prefab = Resources.Load<AlaalaPauseMenuController>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError("Missing pause menu prefab at Resources/" + ResourcePath + ".");
            return;
        }
        Instantiate(prefab).name = "AlaalaPauseMenu";
    }

    private void Awake()
    {
        EnsureEventSystem();
        continueButton.onClick.AddListener(CloseMenu);
        settingsButton.onClick.AddListener(ShowSettings);
        exitButton.onClick.AddListener(RequestExitToMainMenu);
        backButton.onClick.AddListener(ShowMainPanel);
        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        InitializeSettings();
        ConfigureHint();
        ShowMainPanel();
        pauseRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        continueButton.onClick.RemoveListener(CloseMenu);
        settingsButton.onClick.RemoveListener(ShowSettings);
        exitButton.onClick.RemoveListener(RequestExitToMainMenu);
        backButton.onClick.RemoveListener(ShowMainPanel);
        masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (isOpen)
        {
            if (inSettings) ShowMainPanel(); else CloseMenu();
            return;
        }
        if (!HandleExistingFullscreenBack() && !IsPauseBlocked()) OpenMenu();
    }

    private bool HandleExistingFullscreenBack()
    {
        ClarityDocumentViewer document = FindAnyObjectByType<ClarityDocumentViewer>();
        if (document != null && document.IsOpen) { document.CloseDocument(); return true; }
        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        if (journal != null && journal.IsOpen) { journal.CloseJournal(); return true; }
        InventoryUI inventory = FindAnyObjectByType<InventoryUI>();
        if (inventory != null && inventory.IsOpen) { inventory.CloseInventory(); return true; }
        return false;
    }

    private static bool IsPauseBlocked()
    {
        if (StorySequenceCoordinator.IsStorySequenceActive ||
            (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)) return true;
        if (GameplaySystemTutorialManager.HasInstance)
        {
            GameplaySystemTutorialManager tutorial = GameplaySystemTutorialManager.Instance;
            if (tutorial.HasActiveTutorial || tutorial.IsPresentationVisible) return true;
        }
        return false;
    }

    public void OpenMenu()
    {
        if (isOpen || IsPauseBlocked()) return;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isOpen = true;
        pauseRoot.SetActive(true);
        ShowMainPanel();
    }

    public void CloseMenu()
    {
        if (!isOpen) return;
        isOpen = false;
        inSettings = false;
        pauseRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    public void RequestExitToMainMenu()
    {
        if (isOpen) CloseMenu();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnDisable() { if (isOpen) CloseMenu(); }
    private void ShowMainPanel() { inSettings = false; mainPanel.SetActive(true); settingsPanel.SetActive(false); }
    private void ShowSettings() { inSettings = true; mainPanel.SetActive(false); settingsPanel.SetActive(true); }

    private void InitializeSettings()
    {
        masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        musicVolumeSlider.interactable = false;
        sfxVolumeSlider.interactable = false;
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        resolutions.Clear();
        resolutionDropdown.ClearOptions();
        HashSet<string> seen = new HashSet<string>();
        int selected = 0;
        foreach (Resolution resolution in Screen.resolutions)
        {
            string key = resolution.width + "x" + resolution.height;
            if (!seen.Add(key)) continue;
            if (resolution.width == Screen.width && resolution.height == Screen.height) selected = resolutions.Count;
            resolutions.Add(resolution);
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(resolution.width + " × " + resolution.height));
        }
        resolutionDropdown.SetValueWithoutNotify(selected);
        resolutionDropdown.RefreshShownValue();
    }

    private void ConfigureHint()
    {
        CanvasGroup group = hintRoot.GetComponent<CanvasGroup>() ?? hintRoot.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        GameplayHUDTarget.AttachTo(hintRoot).Configure(group, hintRoot);
    }

    private static void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
        PlayerPrefs.Save();
    }

    private static void SetFullscreen(bool value) => Screen.fullScreen = value;
    private void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
