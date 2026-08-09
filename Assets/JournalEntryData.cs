using System;

[Serializable]
public class JournalEntryData
{
    public string id;
    public string title;
    public string description;
    public EntryType type;
    public bool isUnlocked;
    public bool isCompleted;

    public JournalEntryData(string id, string title, string description, EntryType type)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.type = type;
        this.isUnlocked = false;
        this.isCompleted = false;
    }
}

public enum EntryType
{
    Quest,
    Note,
    Memory
}