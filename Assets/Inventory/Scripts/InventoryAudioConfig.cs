using UnityEngine;

[CreateAssetMenu(fileName = "InventoryAudioConfig", menuName = "ALAALA/Inventory Audio Config")]
public sealed class InventoryAudioConfig : ScriptableObject
{
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;
    [SerializeField] private AudioClip[] tabSwitchSounds;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

    public AudioClip InventoryOpenSound => inventoryOpenSound;
    public AudioClip InventoryCloseSound => inventoryCloseSound;
    public AudioClip[] TabSwitchSounds => tabSwitchSounds;
    public float UIVolume => uiVolume;
}
