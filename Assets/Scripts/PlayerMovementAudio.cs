using UnityEngine;
using Supercyan.FreeSample;

/// <summary>
/// Plays configurable locomotion sounds for a player character.
/// Footstep timing comes from animation events on the walk/run clips.
/// </summary>
public sealed class PlayerMovementAudio : MonoBehaviour
{
    private const float MinimumFootstepInterval = 0.12f;

    [Header("Audio Source")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioSource runningSequenceAudioSource;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] walkingFootsteps;
    [SerializeField] private AudioClip[] runningFootsteps;
    [SerializeField, Range(0f, 1f)] private float walkingFootstepVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float runningFootstepVolume = 0.9f;

    [Header("Jump and Landing")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landingSound;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float landingVolume = 1f;

    private SimpleSampleCharacterControl movementController;
    private AudioClip previousWalkingFootstep;
    private AudioClip previousRunningFootstep;
    private float lastFootstepTime = float.NegativeInfinity;
    private bool runningSequenceActive;

    private void Awake()
    {
        movementController = GetComponent<SimpleSampleCharacterControl>();

        // Never borrow another component's AudioSource: reaction audio remains independent.
        if (movementAudioSource == null)
            movementAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureMovementAudioSource();

        // Running clips contain several contacts, so keep them on a separately
        // controllable source without interrupting walking, jump, or landing audio.
        if (runningSequenceAudioSource == null || runningSequenceAudioSource == movementAudioSource)
            runningSequenceAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureRunningSequenceAudioSource();
    }

    private void Update()
    {
        if (!runningSequenceActive)
            return;

        if (!CanContinueRunningSequence() || runningSequenceAudioSource == null)
        {
            StopRunningSequence();
            return;
        }

        if (!runningSequenceAudioSource.isPlaying && !PlayNextRunningSequence())
            runningSequenceActive = false;
    }

    private void OnDisable()
    {
        StopRunningSequence();
    }

    /// <summary>Called by walk animation events on the shared walk clip.</summary>
    public void AnimationEvent_WalkFootstep(AnimationEvent animationEvent)
    {
        if (movementAudioSource == null || !CanPlayFootstepEvent(isRunning: false) ||
            Time.time - lastFootstepTime < MinimumFootstepInterval)
        {
            return;
        }

        AudioClip clip = ChooseFootstep(walkingFootsteps, previousWalkingFootstep);
        if (clip == null)
            return;

        movementAudioSource.PlayOneShot(clip, walkingFootstepVolume);
        previousWalkingFootstep = clip;
        lastFootstepTime = Time.time;
    }

    /// <summary>Called by run animation events on the shared run clip.</summary>
    public void AnimationEvent_RunFootstep(AnimationEvent animationEvent)
    {
        if (runningSequenceAudioSource == null || runningSequenceActive ||
            !CanPlayFootstepEvent(isRunning: true) ||
            Time.time - lastFootstepTime < MinimumFootstepInterval)
        {
            return;
        }

        runningSequenceActive = true;
        if (!PlayNextRunningSequence())
        {
            runningSequenceActive = false;
            return;
        }

        lastFootstepTime = Time.time;
    }

    private bool CanPlayFootstepEvent(bool isRunning)
    {
        return isActiveAndEnabled && movementController != null &&
               movementController.isActiveAndEnabled &&
               movementController.IsMoving && movementController.IsGrounded &&
               movementController.IsRunning == isRunning && !StorySequenceCoordinator.IsStorySequenceActive;
    }

    private bool CanContinueRunningSequence()
    {
        return isActiveAndEnabled && movementController != null &&
               movementController.isActiveAndEnabled &&
               movementController.IsMoving && movementController.IsGrounded &&
               movementController.IsRunning && !StorySequenceCoordinator.IsStorySequenceActive;
    }

    private bool PlayNextRunningSequence()
    {
        AudioClip clip = ChooseFootstep(runningFootsteps, previousRunningFootstep);
        if (clip == null || runningSequenceAudioSource == null)
            return false;

        runningSequenceAudioSource.clip = clip;
        runningSequenceAudioSource.volume = movementAudioSource != null
            ? movementAudioSource.volume * runningFootstepVolume
            : runningFootstepVolume;
        runningSequenceAudioSource.Play();
        previousRunningFootstep = clip;
        return true;
    }

    private void StopRunningSequence()
    {
        runningSequenceActive = false;
        if (runningSequenceAudioSource != null)
        {
            runningSequenceAudioSource.Stop();
            runningSequenceAudioSource.clip = null;
        }
    }

    /// <summary>Called only after the movement controller applies a successful jump impulse.</summary>
    public void PlayJumpSound()
    {
        PlayOneShot(jumpSound, jumpVolume);
    }

    /// <summary>Called only on the controller's guarded airborne-to-grounded transition.</summary>
    public void PlayLandingSound()
    {
        PlayOneShot(landingSound, landingVolume);
    }

    private AudioClip ChooseFootstep(AudioClip[] clips, AudioClip previousClip)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        int nonRepeatingCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            validCount++;
            if (clips[i] != previousClip)
                nonRepeatingCount++;
        }

        if (validCount == 0)
            return null;

        bool avoidRepeat = nonRepeatingCount > 0;
        int selected = Random.Range(0, avoidRepeat ? nonRepeatingCount : validCount);

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null || (avoidRepeat && clip == previousClip))
                continue;

            if (selected-- == 0)
                return clip;
        }

        return null;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (movementAudioSource != null && clip != null)
            movementAudioSource.PlayOneShot(clip, volume);
    }

    private void ConfigureMovementAudioSource()
    {
        movementAudioSource.playOnAwake = false;
        movementAudioSource.loop = false;
        movementAudioSource.clip = null;
        movementAudioSource.spatialBlend = 0f;
    }

    private void ConfigureRunningSequenceAudioSource()
    {
        runningSequenceAudioSource.playOnAwake = false;
        runningSequenceAudioSource.loop = false;
        runningSequenceAudioSource.clip = null;
        runningSequenceAudioSource.outputAudioMixerGroup = movementAudioSource.outputAudioMixerGroup;
        runningSequenceAudioSource.pitch = movementAudioSource.pitch;
        runningSequenceAudioSource.spatialBlend = movementAudioSource.spatialBlend;
        runningSequenceAudioSource.mute = movementAudioSource.mute;
        runningSequenceAudioSource.priority = movementAudioSource.priority;
        runningSequenceAudioSource.bypassEffects = movementAudioSource.bypassEffects;
        runningSequenceAudioSource.bypassListenerEffects = movementAudioSource.bypassListenerEffects;
        runningSequenceAudioSource.bypassReverbZones = movementAudioSource.bypassReverbZones;
    }
}
