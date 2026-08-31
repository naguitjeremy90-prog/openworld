using UnityEngine;

public sealed class InventoryTestHarness : MonoBehaviour
{
    [Tooltip("Assign one temporary InventoryItemData asset, enter Play Mode, then use this component's context menu.")]
    [SerializeField] private InventoryItemData itemToTest;

    [ContextMenu("Inventory Test/Add Item")]
    public void AddTestItem()
    {
        if (!TryGetManager(out InventoryManager manager) || itemToTest == null)
            return;

        bool added = manager.AddItem(itemToTest);
        Debug.Log($"Inventory test: Add '{itemToTest.ItemID}' returned {added}. (False is expected for a duplicate.)", this);
    }

    [ContextMenu("Inventory Test/Add Item Twice (Duplicate Check)")]
    public void AddTestItemTwice()
    {
        if (!TryGetManager(out InventoryManager manager) || itemToTest == null)
            return;

        bool firstResult = manager.AddItem(itemToTest);
        bool secondResult = manager.AddItem(itemToTest);
        Debug.Log($"Inventory duplicate test: first={firstResult}, second={secondResult}. The second result should be False.", this);
    }

    [ContextMenu("Inventory Test/Check Has Item")]
    public void CheckTestItem()
    {
        if (!TryGetManager(out InventoryManager manager) || itemToTest == null)
            return;

        Debug.Log($"Inventory test: HasItem('{itemToTest.ItemID}') = {manager.HasItem(itemToTest.ItemID)}", this);
    }

    [ContextMenu("Inventory Test/Remove Item")]
    public void RemoveTestItem()
    {
        if (!TryGetManager(out InventoryManager manager) || itemToTest == null)
            return;

        Debug.Log($"Inventory test: RemoveItem('{itemToTest.ItemID}') = {manager.RemoveItem(itemToTest.ItemID)}", this);
    }

    private bool TryGetManager(out InventoryManager manager)
    {
        manager = InventoryManager.Instance;

        if (manager != null)
            return true;

        Debug.LogWarning("Inventory test: No active InventoryManager exists in the scene.", this);
        return false;
    }
}
