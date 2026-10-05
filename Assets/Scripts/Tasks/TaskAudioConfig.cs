using UnityEngine;

[CreateAssetMenu(fileName = "TaskAudioConfig", menuName = "ALAALA/Task Audio Config")]
public sealed class TaskAudioConfig : ScriptableObject
{
    [SerializeField] private AudioClip mainTaskReceivedSound;
    [SerializeField] private AudioClip sideTaskReceivedSound;
    [SerializeField] private AudioClip taskUpdatedSound;
    [SerializeField] private AudioClip mainTaskCompletedSound;
    [SerializeField] private AudioClip sideTaskCompletedSound;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

    public AudioClip MainTaskReceivedSound => mainTaskReceivedSound;
    public AudioClip SideTaskReceivedSound => sideTaskReceivedSound;
    public AudioClip TaskUpdatedSound => taskUpdatedSound;
    public AudioClip MainTaskCompletedSound => mainTaskCompletedSound;
    public AudioClip SideTaskCompletedSound => sideTaskCompletedSound;
    public float UIVolume => uiVolume;
}
