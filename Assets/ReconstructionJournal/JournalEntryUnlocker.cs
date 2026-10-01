using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Invokes configured Journal discoveries from a UnityEvent.</summary>
[DisallowMultipleComponent]
public sealed class JournalEntryUnlocker : MonoBehaviour
{
    [Serializable]
    private struct Entry
    {
        public JournalEntryCategory category;
        public string entryID;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public void UnlockConfiguredEntries()
    {
        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        if (journal == null)
        {
            Debug.LogWarning("Journal entries could not be unlocked because the Journal manager is absent.", this);
            return;
        }

        foreach (Entry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.entryID))
            {
                Debug.LogWarning("A configured Journal entry has no ID.", this);
                continue;
            }

            switch (entry.category)
            {
                case JournalEntryCategory.Observation:
                    journal.UnlockObservation(entry.entryID);
                    break;
                case JournalEntryCategory.People:
                    journal.UnlockPerson(entry.entryID);
                    break;
                case JournalEntryCategory.Fragment:
                    journal.UnlockFragment(entry.entryID);
                    break;
                case JournalEntryCategory.Reflection:
                    journal.UnlockReflection(entry.entryID);
                    break;
                default:
                    Debug.LogWarning("A configured Journal entry has an unsupported category.", this);
                    break;
            }
        }
    }
}
