using System.Collections;
using DialogueEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>Runs one automatic overheard conversation followed by an optional Miguel reaction.</summary>
[RequireComponent(typeof(Collider))]
public sealed class OverheardConversationSequence : MonoBehaviour
{
    [Header("Conversation")]
    [SerializeField] private NPCConversation overheardConversation;
    [SerializeField] private SelfDialogueTrigger reaction;
    [SerializeField] private NPCConversation reactionConversation;

    [Header("Sequence gating")]
    [SerializeField] private PlayableDirector gameplayGateDirector;
    [SerializeField] private string requiredCompletionFlag;
    [SerializeField] private string completionFlag;

    [Header("Optional task update")]
    [SerializeField] private string taskId;
    [SerializeField] private string taskObjective;
    [SerializeField] private UnityEvent onSequenceCompleted = new UnityEvent();

    private StorySequenceToken sequenceToken;
    private Coroutine sequenceRoutine;
    private bool playerInside;
    private bool running;
    private bool completed;
    private bool waitingForOverheardClose;
    private bool reactionFinished;
    private bool introHasPlayed;
    private bool introCompleted;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
        completed = !string.IsNullOrEmpty(completionFlag) && SessionStoryState.GetFlag(completionFlag);
        if (reaction != null && reactionConversation != null)
            reaction.ConfigureConversation(reactionConversation);
    }

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += HandleConversationEnded;
        if (gameplayGateDirector != null)
        {
            gameplayGateDirector.played += HandleIntroPlayed;
            gameplayGateDirector.stopped += HandleIntroStopped;
            introHasPlayed |= gameplayGateDirector.state == PlayState.Playing;
        }
        if (reaction != null)
            reaction.ConversationFinished += HandleReactionFinished;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= HandleConversationEnded;
        if (gameplayGateDirector != null)
        {
            gameplayGateDirector.played -= HandleIntroPlayed;
            gameplayGateDirector.stopped -= HandleIntroStopped;
        }
        if (reaction != null)
            reaction.ConversationFinished -= HandleReactionFinished;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);
        sequenceRoutine = null;
        ReleaseSequence();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            TryBegin();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    private void TryBegin()
    {
        if (!playerInside || running || completed || overheardConversation == null ||
            reaction == null || ConversationManager.Instance == null ||
            ConversationManager.Instance.IsConversationActive ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            !GameplayGateComplete() ||
            (!string.IsNullOrEmpty(requiredCompletionFlag) &&
             !SessionStoryState.GetFlag(requiredCompletionFlag)))
            return;

        running = true;
        waitingForOverheardClose = true;
        reactionFinished = false;
        sequenceToken = StorySequenceCoordinator.Acquire(this);
        ConversationManager.Instance.StartConversation(overheardConversation);
    }

    private bool GameplayGateComplete()
    {
        if (gameplayGateDirector == null)
            return true;

        // This director resets time to zero when it stops. A stopped director
        // is eligible only after this scene's intro actually played and stopped.
        return introCompleted && gameplayGateDirector.state != PlayState.Playing;
    }

    private void HandleIntroPlayed(PlayableDirector director)
    {
        if (director == gameplayGateDirector)
            introHasPlayed = true;
    }

    private void HandleIntroStopped(PlayableDirector director)
    {
        if (director == gameplayGateDirector && introHasPlayed)
            introCompleted = true;
    }

    private void HandleConversationEnded()
    {
        if (!running || !waitingForOverheardClose || sequenceRoutine != null)
            return;

        sequenceRoutine = StartCoroutine(ContinueAfterOverheardCloses());
    }

    private IEnumerator ContinueAfterOverheardCloses()
    {
        while (ConversationManager.Instance != null &&
               ConversationManager.Instance.IsConversationActive)
            yield return null;

        waitingForOverheardClose = false;
        sequenceRoutine = null;
        if (!running || reaction == null)
            yield break;

        reaction.enabled = true;
        reaction.StartSelfDialogue();
    }

    private void HandleReactionFinished()
    {
        if (!running || reactionFinished)
            return;

        reactionFinished = true;
        if (sequenceRoutine == null)
            sequenceRoutine = StartCoroutine(CompleteAfterReactionCloses());
    }

    private IEnumerator CompleteAfterReactionCloses()
    {
        while (ConversationManager.Instance != null &&
               ConversationManager.Instance.IsConversationActive)
            yield return null;

        if (!running)
            yield break;

        completed = true;
        running = false;
        if (!string.IsNullOrEmpty(completionFlag))
            SessionStoryState.SetFlag(completionFlag, true);

        if (!string.IsNullOrEmpty(taskId) && !string.IsNullOrWhiteSpace(taskObjective))
            TaskManager.Instance?.UpdateTask(taskId, taskObjective);

        onSequenceCompleted.Invoke();
        ReleaseSequence();
        sequenceRoutine = null;
    }

    private void ReleaseSequence()
    {
        if (sequenceToken == null)
            return;

        sequenceToken.Release();
        sequenceToken = null;
    }
}
