using UnityEngine;

[CreateAssetMenu(fileName = "PagsusuriAudioConfig", menuName = "ALAALA/Pagsusuri Audio Config")]
public sealed class PagsusuriAudioConfig : ScriptableObject
{
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip deactivationSound;
    [SerializeField] private AudioClip[] discoverySounds;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

    public AudioClip ActivationSound => activationSound;
    public AudioClip DeactivationSound => deactivationSound;
    public AudioClip[] DiscoverySounds => discoverySounds;
    public float UIVolume => uiVolume;
}
