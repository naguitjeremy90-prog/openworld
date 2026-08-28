using UnityEngine;
using UnityEngine.UI;

public class ReconstructionJournalManager : MonoBehaviour
{
    public static ReconstructionJournalManager Instance { get; private set; }

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

    public bool IsOpen { get; private set; }
    public JournalTab CurrentTab { get; private set; } = JournalTab.Observations;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AddButtonListeners();
        ShowTab(JournalTab.Observations);
        CloseJournal();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();

        if (Instance == this)
            Instance = null;
    }

    public void OpenJournal()
    {
        IsOpen = true;

        if (journalWindow != null)
            journalWindow.SetActive(true);

        OpenObservationsTab();
    }

    public void CloseJournal()
    {
        IsOpen = false;

        if (journalWindow != null)
            journalWindow.SetActive(false);
    }

    public void ToggleJournal()
    {
        if (IsOpen)
            CloseJournal();
        else
            OpenJournal();
    }

    public void OpenObservationsTab()
    {
        ShowTab(JournalTab.Observations);

        // NEW: Keep Observations tab raised
        if (observationsTabVisual != null)
            observationsTabVisual.SelectTab();

        if (observations != null)
            observations.RefreshList();
    }

    public void OpenPeopleTab()
    {
        ShowTab(JournalTab.People);

        // NEW: Keep People tab raised
        if (peopleTabVisual != null)
            peopleTabVisual.SelectTab();

        if (people != null)
            people.RefreshList();
    }

    public void OpenFragmentsTab()
    {
        ShowTab(JournalTab.Fragments);

        // NEW: Keep Fragments tab raised
        if (fragmentsTabVisual != null)
            fragmentsTabVisual.SelectTab();

        if (fragments != null)
            fragments.RefreshList();
    }

    public void OpenReflectionsTab()
    {
        ShowTab(JournalTab.Reflections);

        // NEW: Keep Reflections tab raised
        if (reflectionsTabVisual != null)
            reflectionsTabVisual.SelectTab();

        if (reflections != null)
            reflections.RefreshList();
    }

    private void ShowTab(JournalTab tab)
    {
        CurrentTab = tab;

        if (observationsPanel != null)
            observationsPanel.SetActive(tab == JournalTab.Observations);

        if (peoplePanel != null)
            peoplePanel.SetActive(tab == JournalTab.People);

        if (fragmentsPanel != null)
            fragmentsPanel.SetActive(tab == JournalTab.Fragments);

        if (reflectionsPanel != null)
            reflectionsPanel.SetActive(tab == JournalTab.Reflections);
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
