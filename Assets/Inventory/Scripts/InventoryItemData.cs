using UnityEngine;

public enum ItemCategory
{
    General,
    Story,
    Key,
    Document
}

[CreateAssetMenu(fileName = "InventoryItem", menuName = "ALAALA/Inventory Item")]
public sealed class InventoryItemData : ScriptableObject
{
    [SerializeField] private string itemID;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(3, 8)] private string description;
    [SerializeField] private ItemCategory category = ItemCategory.General;

    public string ItemID => itemID;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public ItemCategory Category => category;

    private void OnValidate()
    {
        itemID = itemID == null ? string.Empty : itemID.Trim();
    }
}
