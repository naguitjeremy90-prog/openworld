using System.Collections;
using UnityEngine;

/// <summary>Owns only the AudioSource on the scene-local Part3ChaseMusic root.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class NewMakamisaPart3ChaseMusic : MonoBehaviour
{
    [SerializeField] private AudioClip chaseMusicClip;
    [SerializeField, Range(0f, 1f)] private float targetVolume = 0.40f;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 1.5f;

    private AudioSource source;
    private Coroutine fade;
    private bool startRequested;
    private bool encounterStarted;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.clip = chaseMusicClip;
    }

    // Called only after Chapter 10 successfully releases its held wall toward poorhouse2.
    internal void BeginReveal()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || startRequested || encounterStarted)
            return;
        startRequested = true;
        if (chaseMusicClip == null || source == null || !source.isActiveAndEnabled)
            return; // An empty Inspector slot is intentional; no warnings or deferred playback.

        source.clip = chaseMusicClip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
        BeginFade(fadeInDuration, false);
    }

    // Terminal for this scene instance, including interrupted or repeated encounter entries.
    internal void BeginPadreEncounter()
    {
        if (encounterStarted) return;
        encounterStarted = true;
        if (source == null) return;
        if (!isActiveAndEnabled || !source.isPlaying)
        {
            StopOwnedAudio();
            return;
        }
        BeginFade(fadeOutDuration, true);
    }

    private void BeginFade(float duration, bool stopping)
    {
        if (fade != null) StopCoroutine(fade);
        fade = null;
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
        {
            source.volume = stopping ? 0f : SafeTargetVolume;
            if (stopping) source.Stop();
            return;
        }
        fade = StartCoroutine(FadeVolume(duration, stopping));
    }

    private float SafeTargetVolume => float.IsNaN(targetVolume) ? 0f : Mathf.Clamp01(targetVolume);

    private IEnumerator FadeVolume(float duration, bool stopping)
    {
        float initialVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration && source != null && source.isActiveAndEnabled)
        {
            // ESC pauses gameplay time, but does not pause audio in this project.
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(initialVolume, stopping ? 0f : SafeTargetVolume,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (source != null)
        {
            source.volume = stopping ? 0f : SafeTargetVolume;
            if (stopping) source.Stop();
        }
        fade = null;
    }

    private void StopOwnedAudio()
    {
        if (fade != null) StopCoroutine(fade);
        fade = null;
        if (source == null) return;
        source.volume = 0f;
        source.Stop();
    }

    private void OnDisable() => StopOwnedAudio();
    private void OnDestroy() => StopOwnedAudio();
}
