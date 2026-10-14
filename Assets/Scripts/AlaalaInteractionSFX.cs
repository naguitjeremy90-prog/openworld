using System.Collections;
using UnityEngine;

/// <summary>Plays a configured one-shot after an owning interaction accepts its action.</summary>
[DisallowMultipleComponent]
public sealed class AlaalaInteractionSFX : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private AlaalaInteractionType interactionType;
    [SerializeField] private AlaalaInteractionSFXDefaults defaults;
    [SerializeField] private AudioClip clipOverride;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Min(0f)] private float pitchVariation;
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [SerializeField] private bool transitionSafe;

    public AlaalaInteractionType InteractionType => interactionType;

    /// <summary>Call only after the owning interaction has accepted its action.</summary>
    public void PlayAcceptedInteraction()
    {
        AudioClip clip = clipOverride != null
            ? clipOverride
            : defaults != null ? defaults.GetClip(interactionType) : null;
        if (clip == null)
            return;

        float variation = Mathf.Clamp(pitchVariation, 0f, 0.95f);
        float pitch = variation > 0f
            ? Random.Range(1f - variation, 1f + variation)
            : 1f;

        GameObject playbackObject = new GameObject("ALAALA Interaction SFX");
        playbackObject.transform.position = transform.position;

        AudioSource source = playbackObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = null;
        source.volume = 1f;
        source.pitch = pitch;
        source.spatialBlend = spatialBlend;
        source.ignoreListenerVolume = false;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));

        if (transitionSafe)
            DontDestroyOnLoad(playbackObject);

        playbackObject.AddComponent<AlaalaInteractionSFXLifetime>()
            .Begin(clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));
    }
}

/// <summary>Destroys an individual interaction one-shot using unscaled time.</summary>
internal sealed class AlaalaInteractionSFXLifetime : MonoBehaviour
{
    public void Begin(float duration)
    {
        StartCoroutine(DestroyAfterPlayback(duration));
    }

    private IEnumerator DestroyAfterPlayback(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, duration));
        Destroy(gameObject);
    }
}
