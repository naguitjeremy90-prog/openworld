using System;
using System.Collections.Generic;
using System.Linq;
using Alaala.Saving;

public sealed partial class InventoryManager
{
    // In-memory bookkeeping only. Acquisition/removal semantics and events are unchanged.
    private readonly HashSet<string> saveCollectedIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> saveConsumedIds = new HashSet<string>(StringComparer.Ordinal);

    public AlaalaInventorySnapshot CaptureSaveInventory()
    {
        var owned = ownedItems.Select(x => x.ItemID).OrderBy(x => x, StringComparer.Ordinal).ToList();
        return new AlaalaInventorySnapshot
        {
            owned = owned,
            collected = saveCollectedIds.Union(owned).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            consumed = saveConsumedIds.OrderBy(x => x, StringComparer.Ordinal).ToList()
        };
    }

    public bool TryHydrateSaveInventorySilently(AlaalaInventorySnapshot record, AlaalaSessionSnapshot state,
        AlaalaSaveCatalog catalog, out string error)
    {
        if (!AlaalaSaveValidation.ValidateInventory(record, state, catalog, out error)) return false;
        if (!SessionStoryState.MatchesDurableSnapshot(state, catalog, out error)) return false;
        var resolved = record.owned.Select(catalog.Item).ToArray(); // Resolve every ID before changing ownership.
        ownedItems.Clear(); ownedItems.AddRange(resolved);
        saveCollectedIds.Clear(); saveConsumedIds.Clear();
        foreach (var id in record.collected) saveCollectedIds.Add(id);
        foreach (var id in record.consumed) saveConsumedIds.Add(id);
        foreach (var item in catalog.Items) definitionsByID[item.ItemID] = item;
        ownedItemsView = ownedItems.AsReadOnly();
        InventoryItemReadState.HydrateKnownItemsSilently(record.owned);
        // No AddItem(), RegisterAcquisition(), ItemAdded or OnInventoryChanged.
        return true;
    }
}

public static partial class InventoryItemReadState
{
    internal static void HydrateKnownItemsSilently(IEnumerable<string> owned)
    {
        knownItems.Clear();
        foreach (var id in owned) knownItems.Add(id);
        // The validated session snapshot already holds exact read/unread flags.
    }
}
