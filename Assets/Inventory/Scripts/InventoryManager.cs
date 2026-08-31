using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Item Definitions")]
    [Tooltip("Every item that must be addable by item ID. Direct AddItem(asset) calls do not require the asset to be in this list.")]
    [SerializeField] private List<InventoryItemData> itemDatabase = new List<InventoryItemData>();

    [Header("Lifetime")]
    [Tooltip("Keep the runtime inventory when changing scenes.")]
    [SerializeField] private bool persistAcrossScenes = true;

    private readonly List<InventoryItemData> ownedItems = new List<InventoryItemData>();
    private readonly Dictionary<string, InventoryItemData> definitionsByID =
        new Dictionary<string, InventoryItemData>(StringComparer.Ordinal);

    private IReadOnlyList<InventoryItemData> ownedItemsView;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ownedItemsView = ownedItems.AsReadOnly();
        RebuildItemLookup();

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool AddItem(InventoryItemData item)
    {
        if (!IsValidItem(item))
            return false;

        if (HasItem(item.ItemID))
            return false;

        ownedItems.Add(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool AddItem(string itemID)
    {
        string normalizedID = NormalizeID(itemID);

        if (string.IsNullOrEmpty(normalizedID))
        {
            Debug.LogWarning("InventoryManager: Cannot add an item with an empty item ID.", this);
            return false;
        }

        if (!definitionsByID.TryGetValue(normalizedID, out InventoryItemData item))
        {
            Debug.LogWarning($"InventoryManager: No item definition with ID '{normalizedID}' exists in the item database.", this);
            return false;
        }

        return AddItem(item);
    }

    public bool RemoveItem(string itemID)
    {
        string normalizedID = NormalizeID(itemID);

        for (int i = 0; i < ownedItems.Count; i++)
        {
            InventoryItemData item = ownedItems[i];
            if (item != null && string.Equals(item.ItemID, normalizedID, StringComparison.Ordinal))
            {
                ownedItems.RemoveAt(i);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public bool HasItem(string itemID)
    {
        string normalizedID = NormalizeID(itemID);

        if (string.IsNullOrEmpty(normalizedID))
            return false;

        for (int i = 0; i < ownedItems.Count; i++)
        {
            InventoryItemData item = ownedItems[i];
            if (item != null && string.Equals(item.ItemID, normalizedID, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public IReadOnlyList<InventoryItemData> GetOwnedItems()
    {
        return ownedItemsView;
    }

    public bool TryGetItemDefinition(string itemID, out InventoryItemData item)
    {
        return definitionsByID.TryGetValue(NormalizeID(itemID), out item);
    }

    private void RebuildItemLookup()
    {
        definitionsByID.Clear();

        for (int i = 0; i < itemDatabase.Count; i++)
        {
            InventoryItemData item = itemDatabase[i];

            if (!IsValidItem(item))
                continue;

            if (!definitionsByID.TryAdd(item.ItemID, item))
            {
                Debug.LogWarning(
                    $"InventoryManager: Duplicate item ID '{item.ItemID}' in the item database. " +
                    "Only the first definition will be used.",
                    this);
            }
        }
    }

    private bool IsValidItem(InventoryItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("InventoryManager: Cannot use a missing item definition.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.ItemID))
        {
            Debug.LogWarning($"InventoryManager: Item '{item.name}' has an empty item ID.", item);
            return false;
        }

        return true;
    }

    private static string NormalizeID(string itemID)
    {
        return itemID == null ? string.Empty : itemID.Trim();
    }
}
