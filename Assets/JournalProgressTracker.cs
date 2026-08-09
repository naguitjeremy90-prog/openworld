using System.Collections.Generic;
using UnityEngine;

public class JournalProgressTracker : MonoBehaviour
{
    public static JournalProgressTracker Instance;

    private HashSet<ImportantNPC> talkedNPCs = new HashSet<ImportantNPC>();

    // Vendor counts as one of the five
    private readonly HashSet<ImportantNPC> requiredNPCs = new HashSet<ImportantNPC>
    {
        ImportantNPC.Vendor,
        ImportantNPC.Gossips,
        ImportantNPC.Beata,
        ImportantNPC.Sacristan,
        ImportantNPC.ManaSebia
    };

    private bool poorTownsmanUnlocked = false;
    private bool poorTownsmanCompleted = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Start the journal flow with the first quest
        JournalManager.Instance.UnlockEntry("quest_vendor");
        JournalManager.Instance.RefreshJournal();
    }

    public void RegisterTalk(ImportantNPC npcID)
    {
        // Prevent double-counting
        if (talkedNPCs.Contains(npcID))
            return;

        talkedNPCs.Add(npcID);

        switch (npcID)
        {
            case ImportantNPC.Vendor:
                OnTalkedToVendor();
                break;

            case ImportantNPC.Gossips:
                OnTalkedToGossips();
                break;

            case ImportantNPC.Beata:
                OnTalkedToBeata();
                break;

            case ImportantNPC.Sacristan:
                OnTalkedToSacristan();
                break;

            case ImportantNPC.ManaSebia:
                OnTalkedToManaSebia();
                break;

            case ImportantNPC.PoorTownsman:
                OnTalkedToPoorTownsman();
                break;
        }

        CheckPoorTownsmanUnlock();
        JournalManager.Instance.RefreshJournal();
    }

    public bool CanTalkToPoorTownsman()
    {
        return poorTownsmanUnlocked;
    }

    private int GetRequiredTalkCount()
    {
        int count = 0;

        foreach (ImportantNPC npc in requiredNPCs)
        {
            if (talkedNPCs.Contains(npc))
                count++;
        }

        return count;
    }

    private void CheckPoorTownsmanUnlock()
    {
        if (!poorTownsmanUnlocked && GetRequiredTalkCount() >= 5)
        {
            poorTownsmanUnlocked = true;

            JournalManager.Instance.CompleteEntry("quest_gather_info");
            JournalManager.Instance.UnlockEntry("quest_poor_townsman");

            if (ReflectionProgressTracker.Instance != null)
            {
                ReflectionProgressTracker.Instance.UnlockReflection("reflection_vendor");
                ReflectionProgressTracker.Instance.UnlockReflection("reflection_gather_info");
            }

            if (JournalManager.Instance != null && JournalManager.Instance.journalIconUI != null)
            {
                JournalManager.Instance.journalIconUI.ShowBadge();
            }

            Debug.Log("Poor Townsman unlocked.");
        }
    }

    private void OnTalkedToVendor()
    {
        JournalManager.Instance.CompleteEntry("quest_vendor");
        JournalManager.Instance.UnlockEntry("quest_gather_info");
        JournalManager.Instance.UnlockEntry("note_vendor");

        Debug.Log("Vendor counted. Gather information quest unlocked.");
    }

    private void OnTalkedToGossips()
    {
        JournalManager.Instance.UnlockEntry("note_gossips");
        Debug.Log("Gossips counted.");
    }

    private void OnTalkedToBeata()
    {
        JournalManager.Instance.UnlockEntry("note_beata");
        Debug.Log("Beata counted.");
    }

    private void OnTalkedToSacristan()
    {
        JournalManager.Instance.UnlockEntry("note_sacristan");
        Debug.Log("Sacristan counted.");
    }

    private void OnTalkedToManaSebia()
    {
        JournalManager.Instance.UnlockEntry("note_mana_sebia");
        Debug.Log("Mana Sebia counted.");
    }

    private void OnTalkedToPoorTownsman()
    {
        if (poorTownsmanCompleted)
            return;

        poorTownsmanCompleted = true;

        JournalManager.Instance.CompleteEntry("quest_poor_townsman");
        JournalManager.Instance.UnlockEntry("note_poor_townsman");
        JournalManager.Instance.UnlockEntry("quest_find_anday");

        if (ReflectionProgressTracker.Instance != null)
        {
            ReflectionProgressTracker.Instance.UnlockReflection("reflection_poor_townsman");
        }

        if (JournalManager.Instance != null && JournalManager.Instance.journalIconUI != null)
        {
            JournalManager.Instance.journalIconUI.ShowBadge();
        }

        Debug.Log("Poor Townsman clue unlocked: Anday at Capitan Panchong's place.");
    }
}