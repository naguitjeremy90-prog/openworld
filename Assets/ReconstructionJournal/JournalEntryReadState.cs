using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-entry read metadata stored in the existing play-session state.</summary>
public static class JournalEntryReadState
{
    private const string KnownPrefix = "journal.e12.known:";
    private const string ReadPrefix = "journal.e12.read:";

    // Cache identities and key strings, not read-state values or UI instances.
    private static readonly Dictionary<JournalEntryUnlockedInfo, EntryKeys> entries =
        new Dictionary<JournalEntryUnlockedInfo, EntryKeys>();

    public static int UnreadCount => GetUnreadCount();

    /// <summary>
    /// Call only for a successful new unlock, before writing its discovery flag.
    /// Existing discoveries without E.12 metadata retain their legacy read status.
    /// </summary>
    public static bool RegisterDiscovery(JournalEntryCategory category, string entryID)
    {
        if (!TryGetKeys(category, entryID, out EntryKeys keys) ||
            SessionStoryState.GetFlag(keys.Known) ||
            SessionStoryState.GetFlag(keys.Discovered))
            return false;

        SessionStoryState.SetFlag(keys.Known, true);
        return true;
    }

    public static bool IsUnread(JournalEntryCategory category, string entryID)
    {
        return TryGetKeys(category, entryID, out EntryKeys keys) && IsUnread(keys);
    }

    public static bool IsRead(JournalEntryCategory category, string entryID)
    {
        return TryGetKeys(category, entryID, out EntryKeys keys) &&
               SessionStoryState.GetFlag(keys.Discovered) && !IsUnread(keys);
    }

    /// <summary>Returns true only for the first unread-to-read transition.</summary>
    public static bool MarkRead(JournalEntryCategory category, string entryID)
    {
        if (!TryGetKeys(category, entryID, out EntryKeys keys) || !IsUnread(keys))
            return false;

        SessionStoryState.SetFlag(keys.Read, true);
        return true;
    }

    public static int GetUnreadCount()
    {
        int count = 0;
        foreach (var entry in entries)
            if (IsUnread(entry.Value))
                count++;
        return count;
    }

    /// <summary>Counts unread entries from all registered session categories without using scene catalogs.</summary>
    public static int GetUnreadCount(JournalEntryCategory category)
    {
        int count = 0;
        foreach (var entry in entries)
            if (entry.Key.Category == category && IsUnread(entry.Value))
                count++;
        return count;
    }

    private static bool IsUnread(EntryKeys keys)
    {
        return SessionStoryState.GetFlag(keys.Discovered) &&
               SessionStoryState.GetFlag(keys.Known) &&
               !SessionStoryState.GetFlag(keys.Read);
    }

    private static bool TryGetKeys(JournalEntryCategory category, string entryID, out EntryKeys keys)
    {
        keys = default;
        if (string.IsNullOrWhiteSpace(entryID))
            return false;

        string discoveryPrefix;
        string categoryName;
        switch (category)
        {
            case JournalEntryCategory.Observation:
                discoveryPrefix = "journal_observation_unlocked:";
                categoryName = "Observation";
                break;
            case JournalEntryCategory.People:
                discoveryPrefix = "journal_person_unlocked:";
                categoryName = "People";
                break;
            case JournalEntryCategory.Fragment:
                discoveryPrefix = "journal_fragment_unlocked:";
                categoryName = "Fragment";
                break;
            case JournalEntryCategory.Reflection:
                discoveryPrefix = "journal_reflection_unlocked:";
                categoryName = "Reflection";
                break;
            default:
                return false;
        }

        var identity = new JournalEntryUnlockedInfo(category, entryID, string.Empty);
        if (!entries.TryGetValue(identity, out keys))
        {
            string suffix = categoryName + ":" + entryID;
            keys = new EntryKeys(KnownPrefix + suffix, ReadPrefix + suffix, discoveryPrefix + entryID);
            entries.Add(identity, keys);
        }
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKeyCache()
    {
        entries.Clear();
    }

    private readonly struct EntryKeys
    {
        public readonly string Known;
        public readonly string Read;
        public readonly string Discovered;

        public EntryKeys(string known, string read, string discovered)
        {
            Known = known;
            Read = read;
            Discovered = discovered;
        }
    }
}
