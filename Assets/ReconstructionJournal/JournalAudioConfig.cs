using UnityEngine;

[CreateAssetMenu(fileName = "JournalAudioConfig", menuName = "ALAALA/Journal Audio Config")]
public sealed class JournalAudioConfig : ScriptableObject
{
    [SerializeField] private AudioClip journalOpenSound;
    [SerializeField] private AudioClip journalCloseSound;
    [SerializeField] private AudioClip[] tabSwitchSounds;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

    public AudioClip JournalOpenSound => journalOpenSound;
    public AudioClip JournalCloseSound => journalCloseSound;
    public AudioClip[] TabSwitchSounds => tabSwitchSounds;
    public float UIVolume => uiVolume;
}
