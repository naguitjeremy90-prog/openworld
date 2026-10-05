using UnityEngine;

/// <summary>Plays shared, cadence-limited sounds as dialogue characters become visible.</summary>
public sealed class DialogueTypingAudio : MonoBehaviour
{
    [SerializeField] private AudioClip[] typingSounds = System.Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float typingVolume = 0.35f;
    [SerializeField, Min(1)] private int charactersPerSound = 3;
    [SerializeField, Min(0f)] private float minimumSoundInterval = 0.07f;
    [SerializeField] private AudioSource typingAudioSource;
    [SerializeField] private bool debugTypingAudio;

    private bool lineIsRevealing;
    private bool loggedFirstEligibleCharacter;
    private bool hasPlayedSound;
    private int eligibleCharactersSinceSound;
    private AudioClip lastPlayedClip;
    private float lastSoundTime;

    private void Awake()
    {
        if (typingAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            DialogueEditor.ConversationManager manager =
                GetComponent<DialogueEditor.ConversationManager>();

            for (int i = 0; i < sources.Length; i++)
            {
                if (manager == null || sources[i] != manager.AudioPlayer)
                {
                    typingAudioSource = sources[i];
                    break;
                }
            }
        }

        if (typingAudioSource != null)
        {
            typingAudioSource.playOnAwake = false;
            typingAudioSource.loop = false;
            typingAudioSource.spatialBlend = 0f;
            typingAudioSource.clip = null;
        }
    }

    private void OnEnable()
    {
        DialogueEditor.ConversationManager.OnConversationEnded += HandleConversationEnded;
    }

    private void OnDisable()
    {
        DialogueEditor.ConversationManager.OnConversationEnded -= HandleConversationEnded;
        StopAndReset();
    }

    private void OnDestroy()
    {
        DialogueEditor.ConversationManager.OnConversationEnded -= HandleConversationEnded;
        StopAndReset();
    }

    public void BeginLine()
    {
        eligibleCharactersSinceSound = 0;
        loggedFirstEligibleCharacter = false;
        lineIsRevealing = true;
        DebugLog("Line started/reset.");
    }

    public void OnCharacterRevealed(char character)
    {
        if (!lineIsRevealing || !char.IsLetterOrDigit(character))
            return;

        if (!loggedFirstEligibleCharacter)
        {
            loggedFirstEligibleCharacter = true;
            DebugLog($"First eligible character received | Character='{character}'");
        }

        int cadence = Mathf.Max(1, charactersPerSound);
        bool soundIsDue = eligibleCharactersSinceSound == 0 ||
                          eligibleCharactersSinceSound >= cadence;

        if (soundIsDue)
        {
            // The first eligible character ticks, then every configured number of
            // eligible characters (1, 4, 7, ... when the cadence is three).
            eligibleCharactersSinceSound = 1;
            TryPlayTypingSound(character);
        }
        else
        {
            eligibleCharactersSinceSound++;
        }
    }

    public void EndLine()
    {
        lineIsRevealing = false;
    }

    private void TryPlayTypingSound(char character)
    {
        DebugLog($"Sound opportunity | Character='{character}'");

        if (typingAudioSource == null)
        {
            DebugLog("Sound skipped | AudioSource missing");
            return;
        }

        if (!HasValidTypingClip())
        {
            DebugLog("Sound skipped | No valid clips");
            return;
        }

        float now = Time.unscaledTime;
        float interval = Mathf.Max(0f, minimumSoundInterval);
        if (hasPlayedSound && now - lastSoundTime < interval)
        {
            DebugLog("Sound skipped | Minimum interval");
            return;
        }

        AudioClip clip = ChooseClip();
        if (clip == null)
        {
            DebugLog("Sound skipped | No valid clips");
            return;
        }

        lastPlayedClip = clip;
        lastSoundTime = now;
        hasPlayedSound = true;
        typingAudioSource.PlayOneShot(clip, Mathf.Clamp01(typingVolume));
        DebugLog($"PlayOneShot | Clip='{clip.name}' | Volume={typingVolume}");
    }

    private bool HasValidTypingClip()
    {
        if (typingSounds == null)
            return false;

        for (int i = 0; i < typingSounds.Length; i++)
        {
            if (typingSounds[i] != null)
                return true;
        }

        return false;
    }

    private AudioClip ChooseClip()
    {
        int validCount = 0;
        int nonRepeatingCount = 0;

        for (int i = 0; i < typingSounds.Length; i++)
        {
            AudioClip clip = typingSounds[i];
            if (clip == null)
                continue;

            validCount++;
            if (clip != lastPlayedClip)
                nonRepeatingCount++;
        }

        if (validCount == 0)
            return null;

        bool avoidRepeat = nonRepeatingCount > 0;
        int choice = Random.Range(0, avoidRepeat ? nonRepeatingCount : validCount);

        for (int i = 0; i < typingSounds.Length; i++)
        {
            AudioClip clip = typingSounds[i];
            if (clip == null || (avoidRepeat && clip == lastPlayedClip))
                continue;

            if (choice-- == 0)
                return clip;
        }

        return null;
    }

    private void HandleConversationEnded()
    {
        DebugLog("Conversation ended/reset.");
        StopAndReset();
    }

    private void DebugLog(string message)
    {
        if (debugTypingAudio)
            Debug.Log($"[DialogueTypingAudio] {message}", this);
    }

    private void StopAndReset()
    {
        lineIsRevealing = false;
        eligibleCharactersSinceSound = 0;
        hasPlayedSound = false;
        lastPlayedClip = null;
        lastSoundTime = 0f;

        if (typingAudioSource != null)
            typingAudioSource.Stop();
    }
}
