using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReconstructionPeople : MonoBehaviour
{
    private const string UnlockFlagPrefix = "journal_person_unlocked:";
    private const string StageValuePrefix = "journal_person_stage:";

    [Header("Available People")]
    [SerializeField] private List<PersonData> allPeople = new List<PersonData>();

    [Header("People List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private Button listButtonPrefab;
    [SerializeField] private TMP_Text emptyListText;

    [Header("Person Details")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text portraitPlaceholderText;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailContextText;
    [SerializeField] private TMP_Text detailDescriptionText;

    [Header("Temporary Testing")]
    [SerializeField] private Button testUnlockButton;
    [SerializeField] private Button testStageOneButton;
    [SerializeField] private string testPersonID = "test_person";

    private readonly HashSet<string> unlockedPersonIDs = new HashSet<string>();
    private readonly Dictionary<string, int> currentStages = new Dictionary<string, int>();
    private readonly List<GameObject> spawnedListButtons = new List<GameObject>();

    private PersonData selectedPerson;

    public event Action<JournalEntryUnlockedInfo> EntryUnlocked;

    public int UnlockedPersonCount
    {
        get { return unlockedPersonIDs.Count; }
    }

    private void Awake()
    {
        RestoreSessionState();

        if (testUnlockButton != null)
            testUnlockButton.onClick.AddListener(UnlockTestPerson);

        if (testStageOneButton != null)
            testStageOneButton.onClick.AddListener(SetTestPersonStageOne);

        RefreshList();
    }

    private void OnDestroy()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.RemoveListener(UnlockTestPerson);

        if (testStageOneButton != null)
            testStageOneButton.onClick.RemoveListener(SetTestPersonStageOne);
    }

    public bool UnlockPerson(string personID)
    {
        PersonData person = FindPerson(personID);

        if (person == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Person ID '" + personID + "' was not found.");
            return false;
        }

        if (!unlockedPersonIDs.Add(personID))
            return false;

        currentStages[personID] = 0;
        SessionStoryState.SetFlag(UnlockFlagPrefix + personID, true);
        SessionStoryState.SetInt(StageValuePrefix + personID, 0);
        RefreshList();
        EntryUnlocked?.Invoke(new JournalEntryUnlockedInfo(
            JournalEntryCategory.People,
            person.personID,
            person.characterName));
        Debug.Log("Reconstruction Journal: Unlocked person '" + personID + "'.");
        return true;
    }

    public bool SetPersonStage(string personID, int stageIndex)
    {
        PersonData person = FindPerson(personID);

        if (person == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Person ID '" + personID + "' was not found.");
            return false;
        }

        if (!unlockedPersonIDs.Contains(personID))
        {
            Debug.LogWarning(
                "Reconstruction Journal: Person '" + personID +
                "' must be unlocked before changing its stage.");
            return false;
        }

        if (stageIndex < 0 || stageIndex >= person.descriptionStages.Count)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Stage " + stageIndex +
                " is invalid for person '" + personID + "'.");
            return false;
        }

        currentStages[personID] = stageIndex;
        SessionStoryState.SetInt(StageValuePrefix + personID, stageIndex);

        if (selectedPerson == person)
            ShowPerson(person);

        Debug.Log(
            "Reconstruction Journal: Set person '" + personID +
            "' to stage " + stageIndex + ".");
        return true;
    }

    public bool IsPersonUnlocked(string personID)
    {
        return unlockedPersonIDs.Contains(personID);
    }

    public int GetPersonStage(string personID)
    {
        int stageIndex;
        return currentStages.TryGetValue(personID, out stageIndex) ? stageIndex : -1;
    }

    public void UnlockTestPerson()
    {
        UnlockPerson(testPersonID);
    }

    public void SetTestPersonStageOne()
    {
        SetPersonStage(testPersonID, 1);
    }

    public void RefreshList()
    {
        ClearSpawnedButtons();

        foreach (PersonData person in allPeople)
        {
            if (person == null || !unlockedPersonIDs.Contains(person.personID))
                continue;

            CreateListButton(person);
        }

        if (emptyListText != null)
            emptyListText.gameObject.SetActive(spawnedListButtons.Count == 0);

        if (selectedPerson == null || !unlockedPersonIDs.Contains(selectedPerson.personID))
            ClearDetails();
    }

    private PersonData FindPerson(string personID)
    {
        if (string.IsNullOrWhiteSpace(personID))
            return null;

        return allPeople.Find(
            person => person != null && person.personID == personID);
    }

    private void RestoreSessionState()
    {
        unlockedPersonIDs.Clear();
        currentStages.Clear();

        foreach (PersonData person in allPeople)
        {
            if (person == null ||
                !SessionStoryState.GetFlag(
                    UnlockFlagPrefix + person.personID))
            {
                continue;
            }

            unlockedPersonIDs.Add(person.personID);
            int maximumStage = Mathf.Max(0, person.descriptionStages.Count - 1);
            currentStages[person.personID] = Mathf.Clamp(
                SessionStoryState.GetInt(StageValuePrefix + person.personID),
                0,
                maximumStage);
        }
    }

    private void CreateListButton(PersonData person)
    {
        if (listContent == null || listButtonPrefab == null)
            return;

        Button newButton = Instantiate(listButtonPrefab, listContent);
        newButton.gameObject.SetActive(true);
        newButton.name = "Person_" + person.personID;

        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = person.characterName;

        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(() => ShowPerson(person));
        spawnedListButtons.Add(newButton.gameObject);
    }

    private void ShowPerson(PersonData person)
    {
        selectedPerson = person;

        if (portraitImage != null)
        {
            portraitImage.sprite = person.portrait;
            portraitImage.gameObject.SetActive(person.portrait != null);
        }

        if (portraitPlaceholderText != null)
            portraitPlaceholderText.gameObject.SetActive(person.portrait == null);

        if (detailNameText != null)
            detailNameText.text = person.characterName;

        if (detailContextText != null)
            detailContextText.text = person.GetContextLabel();

        int stageIndex = GetPersonStage(person.personID);
        if (detailDescriptionText != null)
            detailDescriptionText.text = person.descriptionStages[stageIndex];
    }

    private void ClearDetails()
    {
        selectedPerson = null;

        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.gameObject.SetActive(false);
        }

        if (portraitPlaceholderText != null)
        {
            portraitPlaceholderText.gameObject.SetActive(true);
            portraitPlaceholderText.text = "Walang larawan.";
        }

        if (detailNameText != null)
            detailNameText.text = "Walang napiling tao.";

        if (detailContextText != null)
            detailContextText.text = "";

        if (detailDescriptionText != null)
            detailDescriptionText.text =
                "I-unlock at pumili ng tao para basahin ang pagkaunawa ni Peter tungkol sa kanya.";
    }

    private void ClearSpawnedButtons()
    {
        foreach (GameObject buttonObject in spawnedListButtons)
        {
            if (buttonObject != null)
            {
                buttonObject.SetActive(false);
                Destroy(buttonObject);
            }
        }

        spawnedListButtons.Clear();
    }
}
