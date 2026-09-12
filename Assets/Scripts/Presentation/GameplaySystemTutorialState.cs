using System;

/// <summary>Session-scoped tutorial completion state, separate from system ownership.</summary>
public static class GameplaySystemTutorialState
{
    private const string JournalSeenFlag = "journal_tutorial_seen";
    private const string InventorySeenFlag = "inventory_tutorial_seen";
    private const string InventoryFirstItemSeenFlag = "inventory_tutorial_first_item_seen";
    private const string ClaritySeenFlag = "clarity_tutorial_seen";
    private const string ClarityDocumentSeenFlag = "clarity_document_tutorial_seen";

    public static bool IsSeen(GameplaySystemId system)
    {
        return SessionStoryState.GetFlag(GetSeenFlagId(system));
    }

    public static void SetSeen(GameplaySystemId system, bool seen)
    {
        SessionStoryState.SetFlag(GetSeenFlagId(system), seen);
    }

    public static bool InventoryFirstItemSeen =>
        SessionStoryState.GetFlag(InventoryFirstItemSeenFlag);

    public static void SetInventoryFirstItemSeen(bool seen)
    {
        SessionStoryState.SetFlag(InventoryFirstItemSeenFlag, seen);
    }

    public static bool ClarityDocumentSeen =>
        SessionStoryState.GetFlag(ClarityDocumentSeenFlag);

    public static void SetClarityDocumentSeen(bool seen)
    {
        SessionStoryState.SetFlag(ClarityDocumentSeenFlag, seen);
    }

    public static string GetSeenFlagId(GameplaySystemId system)
    {
        switch (system)
        {
            case GameplaySystemId.Journal:
                return JournalSeenFlag;
            case GameplaySystemId.Inventory:
                return InventorySeenFlag;
            case GameplaySystemId.Clarity:
                return ClaritySeenFlag;
            default:
                throw new ArgumentOutOfRangeException(nameof(system), system, null);
        }
    }
}
