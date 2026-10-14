using System;
using System.Collections.Generic;

/// <summary>Session-scoped unread metadata for currently owned Inventory items.</summary>
public static class InventoryItemReadState
{
    private const string KnownPrefix = "inventory.notification.known:";
    private const string ReadPrefix = "inventory.notification.read:";
    private static readonly HashSet<string> knownItems = new HashSet<string>(StringComparer.Ordinal);

    public static event Action StateChanged;

    public static bool RegisterAcquisition(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID)) return false;
        itemID = itemID.Trim();
        bool wasUnread = IsUnread(itemID);
        SessionStoryState.SetFlag(KnownPrefix + itemID, true);
        SessionStoryState.SetFlag(ReadPrefix + itemID, false);
        knownItems.Add(itemID);
        if (!wasUnread) StateChanged?.Invoke();
        return true;
    }

    public static bool IsUnread(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID)) return false;
        itemID = itemID.Trim();
        return SessionStoryState.GetFlag(KnownPrefix + itemID) &&
               !SessionStoryState.GetFlag(ReadPrefix + itemID) &&
               InventoryManager.Instance != null && InventoryManager.Instance.HasItem(itemID);
    }

    public static bool MarkRead(string itemID)
    {
        if (!IsUnread(itemID)) return false;
        SessionStoryState.SetFlag(ReadPrefix + itemID.Trim(), true);
        StateChanged?.Invoke();
        return true;
    }

    public static int GetUnreadCount()
    {
        InventoryManager manager = InventoryManager.Instance;
        if (manager == null) return 0;
        int count = 0;
        foreach (InventoryItemData item in manager.GetOwnedItems())
            if (item != null && IsUnread(item.ItemID)) count++;
        return count;
    }

    public static int GetUnreadCount(ItemCategory category)
    {
        InventoryManager manager = InventoryManager.Instance;
        if (manager == null) return 0;
        int count = 0;
        foreach (InventoryItemData item in manager.GetOwnedItems())
            if (item != null && item.Category == category && IsUnread(item.ItemID)) count++;
        return count;
    }

    public static void HandleItemRemoved(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID)) return;
        itemID = itemID.Trim();
        bool hadMetadata = SessionStoryState.GetFlag(KnownPrefix + itemID) ||
                           SessionStoryState.GetFlag(ReadPrefix + itemID);
        SessionStoryState.SetFlag(KnownPrefix + itemID, false);
        SessionStoryState.SetFlag(ReadPrefix + itemID, false);
        knownItems.Remove(itemID);
        if (hadMetadata) StateChanged?.Invoke();
    }

    public static void ResetForNewGame()
    {
        foreach (string itemID in knownItems)
        {
            SessionStoryState.SetFlag(KnownPrefix + itemID, false);
            SessionStoryState.SetFlag(ReadPrefix + itemID, false);
        }
        knownItems.Clear();
        StateChanged?.Invoke();
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        knownItems.Clear();
        StateChanged = null;
    }
}
