using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReconstructionJournalManager : MonoBehaviour
{
    private const string FirstDreamObservationId = "ang_panaginip";
    private const int FullInterfaceSortingOrder = 300;
    private const string JournalAudioConfigResourcePath = "JournalAudioConfig";

    public static ReconstructionJournalManager Instance { get; private set; }
    public static event Action<JournalEntryUnlockedInfo> NewEntryUnlocked;

    [Header("Journal Window")]
    [SerializeField] private GameObject journalWindow;

    [Header("Tab Content Panels")]
    [SerializeField] private GameObject observationsPanel;
    [SerializeField] private GameObject peoplePanel;
    [SerializeField] private GameObject fragmentsPanel;
    [SerializeField] private GameObject reflectionsPanel;

    [Header("Observations")]
    [SerializeField] private ReconstructionObservations observations;

    [Header("People")]
    [SerializeField] private ReconstructionPeople people;

    [Header("Fragments")]
    [SerializeField] private ReconstructionFragments fragments;

    [Header("Reflections")]
    [SerializeField] private ReconstructionReflections reflections;

    [Header("Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button observationsTabButton;
    [SerializeField] private Button peopleTabButton;
    [SerializeField] private Button fragmentsTabButton;
    [SerializeField] private Button reflectionsTabButton;

    // NEW: Visual movement for the journal tabs
    [Header("Tab Visuals")]
    [SerializeField] private JournalTabHover observationsTabVisual;
    [SerializeField] private JournalTabHover peopleTabVisual;
    [SerializeField] private JournalTabHover fragmentsTabVisual;
    [SerializeField] private JournalTabHover reflectionsTabVisual;

    [Header("Journal UI Audio")]
    [SerializeField] private AudioSource journalAudioSource;

    public bool IsOpen { get; private set; }
    public JournalTab CurrentTab { get; private set; } = JournalTab.Observations;

    private GameplayHUDUnlockReveal unlockReveal;
    private JournalHUDEntryPulse entryPulse;
    private GameplaySystemTutorialAnchor tutorialAnchor;
    private JournalAudioConfig journalAudioConfig;
    private AudioClip lastTabSwitchClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetNewEntryEvent()
    {
        NewEntryUnlocked = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        journalAudioConfig = Resources.Load<JournalAudioConfig>(JournalAudioConfigResourcePath);
        ConfigureJournalAudioSource();

        Canvas journalCanvas = journalWindow != null
            ? journalWindow.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
        if (journalCanvas != null)
        {
            // The opened journal is a full system interface. Keep it above all
            // normal HUD canvases while remaining below tutorial presentation.
            journalCanvas.overrideSorting = true;
            journalCanvas.sortingOrder = FullInterfaceSortingOrder;
        }

        if (openButton != null)
        {
            GameplayHUDTarget.AttachTo(openButton.gameObject);

            CanvasGroup group = openButton.GetComponent<CanvasGroup>();
            unlockReveal = openButton.GetComponent<GameplayHUDUnlockReveal>();
            if (unlockReveal == null)
                unlockReveal = openButton.gameObject.AddComponent<GameplayHUDUnlockReveal>();
            unlockReveal.Configure(group);
            unlockReveal.RevealCompleted += HandleUnlockRevealCompleted;

            entryPulse = openButton.GetComponent<JournalHUDEntryPulse>();
            if (entryPulse == null)
                entryPulse = openButton.gameObject.AddComponent<JournalHUDEntryPulse>();
            entryPulse.Configure(openButton.transform as RectTransform);

            tutorialAnchor = GameplaySystemTutorialAnchor.AttachTo(
                openButton.gameObject,
                GameplaySystemId.Journal);
        }

        GameplaySystemTutorialManager.Instance.ConfigureJournal(
            journalWindow != null ? journalWindow.transform as RectTransform : null,
            observationsTabButton,
            peopleTabButton,
            fragmentsTabButton,
            reflectionsTabButton);

        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        SubscribeToEntryUnlocks();

        AddButtonListeners();
        ShowTab(JournalTab.Observations, playTabSwitchSound: false);
        CloseJournal();
    }

    private void OnDestroy()
    {
        if (unlockReveal != null)
            unlockReveal.RevealCompleted -= HandleUnlockRevealCompleted;

        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;
        UnsubscribeFromEntryUnlocks();
        RemoveButtonListeners();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (StorySequenceCoordinator.IsStorySequenceActive && IsOpen)
            CloseJournal();
    }

    public void OpenJournal()
    {
        if (!GameplaySystemState.IsUnlocked(GameplaySystemId.Journal) ||
            StorySequenceCoordinator.IsStorySequenceActive)
            return;

        bool wasClosed = !IsOpen;
        IsOpen = true;

        if (journalWindow != null)
            journalWindow.SetActive(true);

        OpenObservationsTab(playTabSwitchSound: !wasClosed);
        if (wasClosed)
            PlayJournalSound(journalAudioConfig != null ? journalAudioConfig.JournalOpenSound : null);

        GameplaySystemTutorialManager.Instance.NotifyJournalOpened();
    }

    public void CloseJournal()
    {
        bool wasOpen = IsOpen;
        IsOpen = false;

        if (journalWindow != null)
            journalWindow.SetActive(false);

        if (wasOpen)
            PlayJournalSound(journalAudioConfig != null ? journalAudioConfig.JournalCloseSound : null);

        if (wasOpen && GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.NotifyJournalClosed();
    }

    public void ToggleJournal()
    {
        if (IsOpen)
            CloseJournal();
        else
            OpenJournal();
    }

    public bool UnlockJournalForFirstDream()
    {
        if (GameplaySystemState.IsUnlocked(GameplaySystemId.Journal))
        {
            return false;
        }

        GameplaySystemState.SetUnlocked(GameplaySystemId.Journal, true);
        UnlockObservation(FirstDreamObservationId);

        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            StartCoroutine(CompleteFirstUnlockWhenSequenceEnds());
        }
        else
        {
            CompleteFirstUnlock();
        }

        return true;
    }

    private IEnumerator CompleteFirstUnlockWhenSequenceEnds()
    {
        while (StorySequenceCoordinator.IsStorySequenceActive)
            yield return null;

        CompleteFirstUnlock();
    }

    private void CompleteFirstUnlock()
    {
        if (unlockReveal == null || !unlockReveal.PlayReveal())
        {
            Debug.LogWarning(
                "Journal tutorial was not started because the existing Journal reveal could not play.");
        }
    }

    private void HandleUnlockRevealCompleted()
    {
        GameplaySystemTutorialManager.Instance.NotifySystemRevealCompleted(
            GameplaySystemId.Journal);
    }

    private void HandleSystemUnlockChanged(
        GameplaySystemId system,
        bool unlocked)
    {
        if (system == GameplaySystemId.Journal && !unlocked && IsOpen)
            CloseJournal();
    }

    public bool PulseNewEntryIcon()
    {
        return entryPulse != null && entryPulse.PlayPulse();
    }

    private void SubscribeToEntryUnlocks()
    {
        if (observations != null)
            observations.EntryUnlocked += HandleEntryUnlocked;
        if (people != null)
            people.EntryUnlocked += HandleEntryUnlocked;
        if (fragments != null)
            fragments.EntryUnlocked += HandleEntryUnlocked;
        if (reflections != null)
            reflections.EntryUnlocked += HandleEntryUnlocked;
    }

    private void UnsubscribeFromEntryUnlocks()
    {
        if (observations != null)
            observations.EntryUnlocked -= HandleEntryUnlocked;
        if (people != null)
            people.EntryUnlocked -= HandleEntryUnlocked;
        if (fragments != null)
            fragments.EntryUnlocked -= HandleEntryUnlocked;
        if (reflections != null)
            reflections.EntryUnlocked -= HandleEntryUnlocked;
    }

    private void HandleEntryUnlocked(JournalEntryUnlockedInfo entry)
    {
        NewEntryUnlocked?.Invoke(entry);
    }

    public void OpenObservationsTab()
    {
        OpenObservationsTab(playTabSwitchSound: true);
    }

    private void OpenObservationsTab(bool playTabSwitchSound)
    {
        ShowTab(JournalTab.Observations, playTabSwitchSound);

        // NEW: Keep Observations tab raised
        if (observationsTabVisual != null)
            observationsTabVisual.SelectTab();

        if (observations != null)
            observations.RefreshList();
    }

    public void OpenPeopleTab()
    {
        ShowTab(JournalTab.People, playTabSwitchSound: true);

        // NEW: Keep People tab raised
        if (peopleTabVisual != null)
            peopleTabVisual.SelectTab();

        if (people != null)
            people.RefreshList();
    }

    public void OpenFragmentsTab()
    {
        ShowTab(JournalTab.Fragments, playTabSwitchSound: true);

        // NEW: Keep Fragments tab raised
        if (fragmentsTabVisual != null)
            fragmentsTabVisual.SelectTab();

        if (fragments != null)
            fragments.RefreshList();
    }

    public void OpenReflectionsTab()
    {
        ShowTab(JournalTab.Reflections, playTabSwitchSound: true);

        // NEW: Keep Reflections tab raised
        if (reflectionsTabVisual != null)
            reflectionsTabVisual.SelectTab();

        if (reflections != null)
            reflections.RefreshList();
    }

    private void ShowTab(JournalTab tab, bool playTabSwitchSound)
    {
        bool tabChanged = tab != CurrentTab;
        CurrentTab = tab;

        if (tabChanged && playTabSwitchSound && IsOpen)
            PlayRandomTabSwitchSound();

        if (observationsPanel != null)
            observationsPanel.SetActive(tab == JournalTab.Observations);

        if (peoplePanel != null)
            peoplePanel.SetActive(tab == JournalTab.People);

        if (fragmentsPanel != null)
            fragmentsPanel.SetActive(tab == JournalTab.Fragments);

        if (reflectionsPanel != null)
            reflectionsPanel.SetActive(tab == JournalTab.Reflections);
    }

    private void ConfigureJournalAudioSource()
    {
        if (journalAudioSource == null)
            journalAudioSource = gameObject.AddComponent<AudioSource>();

        journalAudioSource.spatialBlend = 0f;
        journalAudioSource.playOnAwake = false;
        journalAudioSource.loop = false;
        journalAudioSource.volume = 1f;
        journalAudioSource.clip = null;
    }

    private void PlayRandomTabSwitchSound()
    {
        if (journalAudioConfig == null || journalAudioConfig.TabSwitchSounds == null)
            return;

        List<AudioClip> validClips = new List<AudioClip>();
        AudioClip[] configuredClips = journalAudioConfig.TabSwitchSounds;
        for (int i = 0; i < configuredClips.Length; i++)
        {
            AudioClip clip = configuredClips[i];
            if (clip != null && !validClips.Contains(clip))
                validClips.Add(clip);
        }

        if (validClips.Count == 0)
            return;

        if (validClips.Count > 1)
            validClips.Remove(lastTabSwitchClip);

        AudioClip selectedClip = validClips[UnityEngine.Random.Range(0, validClips.Count)];
        lastTabSwitchClip = selectedClip;
        PlayJournalSound(selectedClip);
    }

    private void PlayJournalSound(AudioClip clip)
    {
        if (clip == null || journalAudioSource == null || journalAudioConfig == null)
            return;

        journalAudioSource.PlayOneShot(clip, Mathf.Clamp01(journalAudioConfig.UIVolume));
    }

    private void AddButtonListeners()
    {
        if (openButton != null)
            openButton.onClick.AddListener(OpenJournal);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseJournal);

        if (observationsTabButton != null)
            observationsTabButton.onClick.AddListener(OpenObservationsTab);

        if (peopleTabButton != null)
            peopleTabButton.onClick.AddListener(OpenPeopleTab);

        if (fragmentsTabButton != null)
            fragmentsTabButton.onClick.AddListener(OpenFragmentsTab);

        if (reflectionsTabButton != null)
            reflectionsTabButton.onClick.AddListener(OpenReflectionsTab);
    }

    private void RemoveButtonListeners()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(OpenJournal);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseJournal);

        if (observationsTabButton != null)
            observationsTabButton.onClick.RemoveListener(OpenObservationsTab);

        if (peopleTabButton != null)
            peopleTabButton.onClick.RemoveListener(OpenPeopleTab);

        if (fragmentsTabButton != null)
            fragmentsTabButton.onClick.RemoveListener(OpenFragmentsTab);

        if (reflectionsTabButton != null)
            reflectionsTabButton.onClick.RemoveListener(OpenReflectionsTab);
    }

    public bool UnlockObservation(string observationID)
    {
        if (observations == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The Observations system is not assigned.");
            return false;
        }

        return observations.UnlockObservation(observationID);
    }

    public bool UnlockPerson(string personID)
    {
        if (people == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The People system is not assigned.");
            return false;
        }

        return people.UnlockPerson(personID);
    }

#if UNITY_EDITOR
    public bool SetPersonUnlockedForDevelopmentTesting(string personID, bool unlocked)
    {
        return people != null &&
               people.SetPersonUnlockedForDevelopmentTesting(personID, unlocked);
    }

    public bool SetObservationUnlockedForDevelopmentTesting(
        string observationID, bool unlocked)
    {
        return observations != null &&
               observations.SetObservationUnlockedForDevelopmentTesting(
                   observationID, unlocked);
    }

    public bool SetFragmentUnlockedForDevelopmentTesting(
        string fragmentID, bool unlocked)
    {
        return fragments != null &&
               fragments.SetFragmentUnlockedForDevelopmentTesting(fragmentID, unlocked);
    }
#endif

    public bool SetPersonStage(string personID, int stageIndex)
    {
        if (people == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The People system is not assigned.");
            return false;
        }

        return people.SetPersonStage(personID, stageIndex);
    }

    public bool UnlockFragment(string fragmentID)
    {
        if (fragments == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The Fragments system is not assigned.");
            return false;
        }

        return fragments.UnlockFragment(fragmentID);
    }

    public bool SetFragmentStage(string fragmentID, int stageIndex)
    {
        if (fragments == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The Fragments system is not assigned.");
            return false;
        }

        return fragments.SetFragmentStage(fragmentID, stageIndex);
    }

    public bool UnlockReflection(string reflectionID)
    {
        if (reflections == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: The Reflections system is not assigned.");
            return false;
        }

        return reflections.UnlockReflection(reflectionID);
    }
}
