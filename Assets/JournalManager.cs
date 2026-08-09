using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance;

    public enum JournalTab
    {
        Quest,
        Reflection,
        Memory
    }

    [Header("Main UI")]
    public GameObject journalOverlay;

    [Header("Left Page")]
    public TMP_Text leftTitleText;
    public Transform entryListContent;
    public GameObject journalEntryButtonPrefab;

    [Header("Right Page - Normal Detail")]
    public TMP_Text detailTitleText;
    public TMP_Text detailDescriptionText;

    [Header("Right Page - Reflection")]
    public GameObject reflectionPanel;
    public ReflectionQuizUI reflectionQuizUI;

    [Header("Tab Buttons")]
    public Button questsTabButton;
    public Button reflectionsTabButton;
    public Button memoriesTabButton;
    public Button closeButton;

    [Header("Journal Entries")]
    public List<JournalEntryData> allJournalEntries = new List<JournalEntryData>();

    [Header("Reflection Questions")]
    public List<ReflectionQuestionData> allReflectionQuestions = new List<ReflectionQuestionData>();

    [Header("Notification")]
    public TMP_Text questNotificationText;
    public float notificationDuration = 2f;

    public JournalIconUI journalIconUI;

    private JournalTab currentTab = JournalTab.Quest;
    private bool journalOpen = false;

    private JournalEntryData currentSelectedEntry;

    private ReflectionQuestionData currentSelectedReflection;

    

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        CreateDefaultJournalEntries();

        if (journalOverlay != null)
            journalOverlay.SetActive(false);

        if (questsTabButton != null)
            questsTabButton.onClick.AddListener(OpenQuestTab);

        if (reflectionsTabButton != null)
            reflectionsTabButton.onClick.AddListener(OpenReflectionTab);

        if (memoriesTabButton != null)
            memoriesTabButton.onClick.AddListener(OpenMemoryTab);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseJournal);

        ShowNormalDetailMode();
        OpenQuestTab();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            ToggleJournal();
        }
    }

    private void CreateDefaultJournalEntries()
    {
        allJournalEntries.Clear();

        allJournalEntries.Add(new JournalEntryData(
            "quest_vendor",
            "Talk to the Vendor",
            "Speak to the Vendor to learn how to begin gathering information in Makamisa.",
            EntryType.Quest
        ));

        allJournalEntries.Add(new JournalEntryData(
            "quest_gather_info",
            "Gather Information",
            "Talk to the townspeople and learn what you can before proceeding.",
            EntryType.Quest
        ));

        allJournalEntries.Add(new JournalEntryData(
            "quest_poor_townsman",
            "Talk to the Poor Townsman",
            "Now that enough information has been gathered, speak to the Poor Townsman.",
            EntryType.Quest
        ));

        allJournalEntries.Add(new JournalEntryData(
            "quest_find_anday",
            "Find Anday",
            "Use the clue about Anday's location and continue your search.",
            EntryType.Quest
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_vendor",
            "The Vendor's Advice",
            "The Vendor suggested asking around town and speaking to the people of Makamisa before continuing.",
            EntryType.Note
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_gossips",
            "The Gossips' Words",
            "The gossips shared useful observations about the people and happenings in town.",
            EntryType.Note
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_beata",
            "Beata's Account",
            "Beata offered a piece of local information that may help in understanding the town better.",
            EntryType.Note
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_sacristan",
            "The Sacristan's Reminder",
            "The Sacristan shared insight connected to the community and daily life in Makamisa.",
            EntryType.Note
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_mana_sebia",
            "Mana Sebia's Words",
            "Mana Sebia provided another clue that contributes to the player's understanding of the town.",
            EntryType.Note
        ));

        allJournalEntries.Add(new JournalEntryData(
            "note_poor_townsman",
            "Poor Townsman's Testimony",
            "The Poor Townsman revealed that Anday is a maid at Capitan Panchong's place.",
            EntryType.Memory
        ));
    }

    public void ToggleJournal()
    {
        journalOpen = !journalOpen;

        if (journalOverlay != null)
            journalOverlay.SetActive(journalOpen);

        if (journalIconUI != null)
            journalIconUI.gameObject.SetActive(!journalOpen);

        if (journalOpen)
        {
            RefreshJournal();

            if (journalIconUI != null)
                journalIconUI.HideBadge();
        }
    }

    public void CloseJournal()
    {
        journalOpen = false;

        if (journalOverlay != null)
            journalOverlay.SetActive(false);

        if (journalIconUI != null)
        {
            journalIconUI.HideBadge();
            journalIconUI.gameObject.SetActive(true);
        }
    }

    public void RefreshJournal()
    {
        switch (currentTab)
        {
            case JournalTab.Quest:
                PopulateQuestEntries();
                break;

            case JournalTab.Reflection:
                PopulateReflectionEntries();
                break;

            case JournalTab.Memory:
                PopulateMemoryEntries();
                break;
        }
    }

    public void OpenQuestTab()
    {
        currentTab = JournalTab.Quest;
        ShowNormalDetailMode();

        if (leftTitleText != null)
            leftTitleText.text = "Current Objectives";

        PopulateQuestEntries();
    }

    public void OpenReflectionTab()
    {
        currentTab = JournalTab.Reflection;
        ShowReflectionMode();

        if (leftTitleText != null)
            leftTitleText.text = "Reflections";

        PopulateReflectionEntries();
    }

    public void OpenMemoryTab()
    {
        currentTab = JournalTab.Memory;
        ShowNormalDetailMode();

        if (leftTitleText != null)
            leftTitleText.text = "Memories";

        PopulateMemoryEntries();
    }

    private void ShowNormalDetailMode()
    {
        if (detailTitleText != null)
            detailTitleText.gameObject.SetActive(true);

        if (detailDescriptionText != null)
            detailDescriptionText.gameObject.SetActive(true);

        if (reflectionPanel != null)
            reflectionPanel.SetActive(false);
    }

    private void ShowReflectionMode()
    {
        if (detailTitleText != null)
            detailTitleText.gameObject.SetActive(false);

        if (detailDescriptionText != null)
            detailDescriptionText.gameObject.SetActive(false);

        if (reflectionPanel != null)
            reflectionPanel.SetActive(true);
    }

    private void ClearEntryList()
    {
        if (entryListContent == null)
            return;

        for (int i = entryListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(entryListContent.GetChild(i).gameObject);
        }
    }

    private void PopulateQuestEntries()
    {
        ClearEntryList();

        bool foundAny = false;
        JournalEntryData firstEntry = null;

        foreach (JournalEntryData entry in allJournalEntries)
        {
            if (!entry.isUnlocked)
                continue;

            if (entry.type != EntryType.Quest)
                continue;

            CreateJournalEntryButton(entry);
            foundAny = true;

            if (firstEntry == null)
                firstEntry = entry;
        }

        if (!foundAny)
        {
            Debug.Log("No visible entries in tab: Quest");

            if (detailTitleText != null)
                detailTitleText.text = "No Entry";

            if (detailDescriptionText != null)
                detailDescriptionText.text = "There are no unlocked quest entries yet.";
        }
        else
        {
            ShowEntryDetails(firstEntry);
        }
    }

    private void PopulateMemoryEntries()
    {
        ClearEntryList();

        bool foundAny = false;
        JournalEntryData firstEntry = null;

        foreach (JournalEntryData entry in allJournalEntries)
        {
            if (!entry.isUnlocked)
                continue;

            if (entry.type != EntryType.Memory)
                continue;

            CreateJournalEntryButton(entry);
            foundAny = true;

            if (firstEntry == null)
                firstEntry = entry;
        }

        if (!foundAny)
        {
            Debug.Log("No visible entries in tab: Memory");

            if (detailTitleText != null)
                detailTitleText.text = "No Entry";

            if (detailDescriptionText != null)
                detailDescriptionText.text = "There are no unlocked memory entries yet.";
        }
        else
        {
            ShowEntryDetails(firstEntry);
        }
    }

    private void PopulateReflectionEntries()
    {
        ClearEntryList();

        bool foundAny = false;
        ReflectionQuestionData firstQuestion = null;

        if (ReflectionProgressTracker.Instance == null)
        {
            Debug.LogError("ReflectionProgressTracker.Instance is NULL.");
            return;
        }

        foreach (ReflectionQuestionData question in allReflectionQuestions)
        {
            if (!ReflectionProgressTracker.Instance.IsReflectionUnlocked(question.questionID))
                continue;

            CreateReflectionEntryButton(question);
            foundAny = true;

            if (firstQuestion == null)
                firstQuestion = question;
        }

        if (!foundAny)
        {
            Debug.Log("No visible entries in tab: Reflection");

            if (reflectionQuizUI != null && reflectionQuizUI.questionText != null)
                reflectionQuizUI.questionText.text = "No Reflection Yet";

            if (reflectionQuizUI != null && reflectionQuizUI.feedbackText != null)
                reflectionQuizUI.feedbackText.text = "Complete an important quest first to unlock a reflection.";
        }
        else
        {
            SelectReflectionQuestion(firstQuestion);
        }
    }

    private void CreateJournalEntryButton(JournalEntryData entry)
    {
        if (journalEntryButtonPrefab == null || entryListContent == null)
            return;

        GameObject obj = Instantiate(journalEntryButtonPrefab, entryListContent);

        JournalEntryButtonUI buttonUI = obj.GetComponent<JournalEntryButtonUI>();
        if (buttonUI != null)
        {
            buttonUI.Setup(entry, this);
        }
    }

    private void CreateReflectionEntryButton(ReflectionQuestionData question)
    {
        if (journalEntryButtonPrefab == null || entryListContent == null)
            return;

        GameObject obj = Instantiate(journalEntryButtonPrefab, entryListContent);

        JournalEntryButtonUI buttonUI = obj.GetComponent<JournalEntryButtonUI>();
        if (buttonUI != null)
        {
            JournalEntryData fakeEntry = new JournalEntryData(
                question.questionID,
                question.title,
                question.questionText,
                EntryType.Note
            );

            fakeEntry.isUnlocked = true;
            fakeEntry.isCompleted = ReflectionProgressTracker.Instance.IsReflectionCompleted(question.questionID);

            buttonUI.Setup(fakeEntry, this);
        }

        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectReflectionQuestion(question));
        }
    }

    public void ShowEntryDetails(JournalEntryData entry)
    {
        currentSelectedEntry = entry;
        currentSelectedReflection = null;

        ShowNormalDetailMode();

        if (detailTitleText != null)
            detailTitleText.text = entry.title;

        if (detailDescriptionText != null)
            detailDescriptionText.text = entry.description;
    }

    private void SelectReflectionQuestion(ReflectionQuestionData question)
    {
        currentSelectedReflection = question;
        currentSelectedEntry = null;

        ShowReflectionMode();

        if (reflectionQuizUI != null)
            reflectionQuizUI.ShowQuestion(question);
    }

    public void UnlockEntry(string entryID)
    {
        JournalEntryData entry = allJournalEntries.Find(e => e.id == entryID);

        if (entry != null && !entry.isUnlocked)
        {
            entry.isUnlocked = true;

            // Only show badge for new QUEST entries
            if (entry.type == EntryType.Quest)
            {
                if (journalIconUI != null)
                    journalIconUI.ShowBadge();

                ShowQuestNotification("New Objective: " + entry.title);
            }

            Debug.Log("Unlocked journal entry: " + entryID);
        }
    }

    public void CompleteEntry(string entryID)
    {
        JournalEntryData entry = allJournalEntries.Find(e => e.id == entryID);

        if (entry != null && !entry.isCompleted)
        {
            entry.isCompleted = true;

            ShowQuestNotification("Objective Completed: " + entry.title);

            if (journalOpen)
                RefreshJournal();

            Debug.Log("Completed journal entry: " + entryID);
        }
    }

    public bool IsEntryUnlocked(string entryID)
    {
        JournalEntryData entry = allJournalEntries.Find(e => e.id == entryID);
        return entry != null && entry.isUnlocked;
    }

    public bool IsEntryCompleted(string entryID)
    {
        JournalEntryData entry = allJournalEntries.Find(e => e.id == entryID);
        return entry != null && entry.isCompleted;
    }


    public void ShowQuestNotification(string message)
    {
        if (questNotificationText == null)
            return;

        StopAllCoroutines();
        StartCoroutine(ShowQuestNotificationRoutine(message));
    }

    private System.Collections.IEnumerator ShowQuestNotificationRoutine(string message)
    {
        questNotificationText.gameObject.SetActive(true);
        questNotificationText.text = message;

        yield return new WaitForSeconds(notificationDuration);

        questNotificationText.gameObject.SetActive(false);
    }

    
}