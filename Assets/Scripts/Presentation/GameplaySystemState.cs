using System;
using UnityEngine;

public enum GameplaySystemId
{
    Journal,
    Inventory,
    Clarity
}

/// <summary>Session-scoped source of truth for gameplay-system unlock state.</summary>
public static class GameplaySystemState
{
    private const string JournalUnlockedFlag = "journal_unlocked";
    private const string InventoryUnlockedFlag = "inventory_unlocked";
    private const string ClarityUnlockedFlag = "clarity_unlocked";

    private static bool listeningForFlagChanges;

    public static event Action<GameplaySystemId, bool> UnlockChanged;

    public static bool IsUnlocked(GameplaySystemId system)
    {
        EnsureListeningForFlagChanges();
        return SessionStoryState.GetFlag(GetUnlockFlagId(system));
    }

    public static void SetUnlocked(GameplaySystemId system, bool unlocked)
    {
        EnsureListeningForFlagChanges();
        SessionStoryState.SetFlag(GetUnlockFlagId(system), unlocked);
    }

    public static string GetUnlockFlagId(GameplaySystemId system)
    {
        switch (system)
        {
            case GameplaySystemId.Journal:
                return JournalUnlockedFlag;
            case GameplaySystemId.Inventory:
                return InventoryUnlockedFlag;
            case GameplaySystemId.Clarity:
                return ClarityUnlockedFlag;
            default:
                throw new ArgumentOutOfRangeException(nameof(system), system, null);
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only, session-scoped test hook. It creates no saved player state.</summary>
    public static void SetDevelopmentUnlocks(
        bool journalUnlocked,
        bool inventoryUnlocked,
        bool clarityUnlocked)
    {
        SetUnlocked(GameplaySystemId.Journal, journalUnlocked);
        SetUnlocked(GameplaySystemId.Inventory, inventoryUnlocked);
        SetUnlocked(GameplaySystemId.Clarity, clarityUnlocked);
    }
#endif

    private static void EnsureListeningForFlagChanges()
    {
        if (listeningForFlagChanges)
            return;

        SessionStoryState.FlagChanged += HandleFlagChanged;
        listeningForFlagChanges = true;
    }

    private static void HandleFlagChanged(string flagId, bool unlocked)
    {
        GameplaySystemId system;
        if (!TryGetSystemId(flagId, out system))
            return;

        UnlockChanged?.Invoke(system, unlocked);
    }

    private static bool TryGetSystemId(string flagId, out GameplaySystemId system)
    {
        if (string.Equals(flagId, JournalUnlockedFlag, StringComparison.Ordinal))
        {
            system = GameplaySystemId.Journal;
            return true;
        }

        if (string.Equals(flagId, InventoryUnlockedFlag, StringComparison.Ordinal))
        {
            system = GameplaySystemId.Inventory;
            return true;
        }

        if (string.Equals(flagId, ClarityUnlockedFlag, StringComparison.Ordinal))
        {
            system = GameplaySystemId.Clarity;
            return true;
        }

        system = default(GameplaySystemId);
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        listeningForFlagChanges = false;
        UnlockChanged = null;
    }
}
