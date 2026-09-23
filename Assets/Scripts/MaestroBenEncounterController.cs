using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(-10000)]
public sealed class MaestroBenEncounterController : MonoBehaviour
{
    public const string IntroSeenFlag = "maestro_ben_intro_seen";
    public const string CompletedFlag = "maestro_ben_completed";

    private const string MainTaskId = "main_investigate_pili";
    private const string ChurchStageId = "investigate_church_document";
    private const string MaestroBenObservationId = "ang_hindi_natapos_na_akda";

    [SerializeField] private PlayableDirector entranceTimeline;
    [SerializeField] private SelfDialogueTrigger introConversation;
    [SerializeField] private NPCConversationTrigger interaction;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;

    [Header("Development Testing")]
    [Tooltip("Testing only. Skips the entrance Timeline without changing story state or progression.")]
    [SerializeField] private bool skipIntroTimelineForTesting = false;

    private StorySequenceToken storySequenceToken;

    private void Awake()
    {
        if (introConversation != null)
            introConversation.ConversationFinished += HandleTimelineConversationFinished;

        if (interaction != null)
            interaction.OnFirstConversationFinished.AddListener(HandleManualFirstConversationFinished);

        if (entranceTimeline != null)
            entranceTimeline.playOnAwake = false;

        if (skipIntroTimelineForTesting)
        {
            entranceTimeline?.Stop();

            if (playerMovement != null)
                playerMovement.enabled = true;

            if (interaction != null)
                interaction.enabled = true;

            return;
        }

        bool completed = SessionStoryState.GetFlag(CompletedFlag);
        bool introSeen = SessionStoryState.GetFlag(IntroSeenFlag);

        // Keep older/in-progress session state internally consistent.
        if (completed && !introSeen)
        {
            SessionStoryState.SetFlag(IntroSeenFlag, true);
            introSeen = true;
        }

        // Manual interaction stays unavailable during the first Timeline so its
        // signal remains the sole owner of the first automatic conversation.
        if (interaction != null)
            interaction.enabled = introSeen;

        if (introSeen)
        {
            entranceTimeline?.Stop();

            // The skipped Timeline cannot own movement state on a return visit.
            if (playerMovement != null)
                playerMovement.enabled = true;

            return;
        }

        if (entranceTimeline == null || entranceTimeline.playableAsset == null)
        {
            Debug.LogWarning(
                "Maestro Ben entrance Timeline is missing; the intro was not marked as seen.",
                this);
            return;
        }

        // The visit is committed immediately before starting the existing asset.
        SessionStoryState.SetFlag(IntroSeenFlag, true);
        storySequenceToken = StorySequenceCoordinator.Acquire(this);
        entranceTimeline.Play();
    }

    private void OnDestroy()
    {
        if (introConversation != null)
            introConversation.ConversationFinished -= HandleTimelineConversationFinished;

        if (interaction != null)
            interaction.OnFirstConversationFinished.RemoveListener(HandleManualFirstConversationFinished);

        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }
    }

    private void HandleTimelineConversationFinished()
    {
        if (SessionStoryState.GetFlag(CompletedFlag))
            return;

        CompleteFirstConversation();
    }

    private void HandleManualFirstConversationFinished()
    {
        CompleteFirstConversation();
    }

    private void CompleteFirstConversation()
    {
        SessionStoryState.SetFlag(CompletedFlag, true);

        ReconstructionJournalManager.Instance?.UnlockObservation(MaestroBenObservationId);

        if (interaction != null)
            interaction.enabled = true;

        TaskManager manager = TaskManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning(
                "Maestro Ben conversation finished, but TaskManager.Instance is null; " +
                "the Church investigation stage could not be applied.",
                this);
            ReleaseStorySequence();
            return;
        }

        bool advanced = manager.AdvanceTaskStage(MainTaskId, ChurchStageId);
        if (!advanced)
        {
            Debug.LogWarning(
                "Maestro Ben conversation finished, but the main task could not advance " +
                "to the Church investigation stage.",
                this);
        }
        else
        {
            GameplaySystemState.SetUnlocked(GameplaySystemId.Clarity, true);
        }

        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }

        if (advanced && GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity))
        {
            GameplaySystemTutorialManager.Instance?.NotifySystemRevealCompleted(
                GameplaySystemId.Clarity);
        }
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken == null)
            return;

        storySequenceToken.Release();
        storySequenceToken = null;
    }
}
