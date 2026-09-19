using System;

public enum JournalEntryCategory
{
    Observation,
    People,
    Fragment,
    Reflection
}

[Serializable]
public readonly struct JournalEntryUnlockedInfo : IEquatable<JournalEntryUnlockedInfo>
{
    public JournalEntryCategory Category { get; }
    public string EntryID { get; }
    public string DisplayTitle { get; }

    public JournalEntryUnlockedInfo(
        JournalEntryCategory category,
        string entryID,
        string displayTitle)
    {
        Category = category;
        EntryID = entryID ?? string.Empty;
        DisplayTitle = displayTitle ?? string.Empty;
    }

    public bool Equals(JournalEntryUnlockedInfo other)
    {
        return Category == other.Category &&
               string.Equals(EntryID, other.EntryID, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is JournalEntryUnlockedInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Category * 397) ^
                   (EntryID != null ? StringComparer.Ordinal.GetHashCode(EntryID) : 0);
        }
    }
}
