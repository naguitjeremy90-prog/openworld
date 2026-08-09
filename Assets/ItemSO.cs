using UnityEngine;

public enum ItemType
{
    Default,
    Flashlight,
    Potion,
    Weapon
    // Add more types here anytime you want
}

[CreateAssetMenu(fileName = "Item", menuName = "NewItem")]
public class ItemSO : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int maxStackSize;
    public GameObject itemPrefab;
    public GameObject handItemPrefab;

    [Header("Usage")]
    public ItemType itemType;
}