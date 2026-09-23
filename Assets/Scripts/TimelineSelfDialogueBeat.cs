using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>Holds one director while an existing self-dialogue runs and fully closes.</summary>
public sealed class TimelineSelfDialogueBeat : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private SelfDialogueTrigger selfDialogue;
    [SerializeField] private CharacterReactionController reactionController;
    [SerializeField] private UnityEvent onDialogueBeatCompleted = new UnityEvent();

    private PlayableDirector pausedDirector;
    private PlayableAsset pausedAsset;
    private PlayableGraph pausedGraph;
    private SelfDialogueTrigger activeTrigger;
    private ConversationManager conversationManager;
    private StorySequenceToken storyToken;
    private Coroutine routine;
    private double pausedTime;
    private bool running;
    private bool completed;
    private bool ownsPause;
    private bool acceptingStart;
    private bool startObserved;
    private bool triggerFinished;
    private bool shuttingDown;
    private readonly List<Animator> heldAnimators = new List<Animator>();

    public void PlayDialogueBeat()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || running || completed)
            return;

        ConversationManager manager = ConversationManager.Instance;
        if (director == null || !director.isActiveAndEnabled ||
            director.state != PlayState.Playing || !director.playableGraph.IsValid() ||
            selfDialogue == null || !selfDialogue.isActiveAndEnabled ||
            manager == null || !manager.isActiveAndEnabled || manager.IsConversationActive)
        {
            Debug.LogWarning("Timeline self-dialogue could not start; director or dialogue is unavailable/busy.", this);
            return;
        }

        running = true;
        pausedDirector = director;
        pausedAsset = director.playableAsset;
        pausedGraph = director.playableGraph;
        activeTrigger = selfDialogue;
        conversationManager = manager;
        pausedTime = director.time;
        ownsPause = true;
        startObserved = false;
        triggerFinished = false;
        pausedDirector.stopped += InvalidatePause;
        pausedDirector.played += InvalidatePause;
        pausedDirector.Pause();
        HoldBoundAnimators();
        storyToken = StorySequenceCoordinator.Acquire(this);

        Coroutine startedRoutine = StartCoroutine(RunBeat());
        routine = running ? startedRoutine : null;
    }

    private IEnumerator RunBeat()
    {
        try
        {
            while (reactionController != null && reactionController.IsReacting)
            {
                if (!StillOwnsPause() || !reactionController.isActiveAndEnabled)
                    yield break;
                yield return null;
            }

            // Re-check after the reaction: another system may have opened dialogue.
            if (!StillOwnsPause() || activeTrigger == null || !activeTrigger.isActiveAndEnabled ||
                conversationManager == null || !conversationManager.isActiveAndEnabled ||
                ConversationManager.Instance != conversationManager || conversationManager.IsConversationActive)
            {
                Debug.LogWarning("Timeline self-dialogue became unavailable before opening.", this);
                yield break;
            }

            activeTrigger.ConversationFinished += HandleTriggerFinished;
            ConversationManager.OnConversationStarted += HandleConversationStarted;
            if (!TryStartDialogue())
                yield break;

            while (!triggerFinished && conversationManager != null && conversationManager.IsConversationActive)
            {
                if (!StillOwnsPause() || !conversationManager.isActiveAndEnabled ||
                    ConversationManager.Instance != conversationManager)
                    yield break;
                yield return null;
            }

            // The trigger's completion event happens at the START of the closing fade.
            // Keep the director paused and the story token until the same manager is Off.
            while (conversationManager != null && conversationManager.IsConversationActive)
            {
                if (!StillOwnsPause() || !conversationManager.isActiveAndEnabled ||
                    ConversationManager.Instance != conversationManager)
                    yield break;
                yield return null;
            }

            completed = triggerFinished && conversationManager != null &&
                        !conversationManager.IsConversationActive;
            if (completed && StillOwnsPause())
                onDialogueBeatCompleted.Invoke();
        }
        finally
        {
            Cleanup();
        }
    }

    private bool TryStartDialogue()
    {
        acceptingStart = true;
        try
        {
            activeTrigger.StartSelfDialogue();
            if (startObserved && conversationManager != null && conversationManager.IsConversationActive)
                return true;

            Debug.LogWarning("Timeline self-dialogue trigger refused to start; releasing the owned Timeline pause.", this);
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            acceptingStart = false;
        }
    }

    private void HandleConversationStarted()
    {
        if (acceptingStart && ConversationManager.Instance == conversationManager)
            startObserved = true;
    }

    private void HandleTriggerFinished()
    {
        if (startObserved)
            triggerFinished = true;
    }

    private void InvalidatePause(PlayableDirector changedDirector)
    {
        ownsPause = false;
    }

    private void HoldBoundAnimators()
    {
        // A paused Timeline graph lets an Animator's controller keep evaluating.
        // Hold only this director's bound Animators, preserving their current pose.
        foreach (PlayableBinding output in pausedAsset.outputs)
        {
            UnityEngine.Object binding = pausedDirector.GetGenericBinding(output.sourceObject);
            Animator animator = binding as Animator;
            if (animator == null && binding is GameObject gameObject)
                animator = gameObject.GetComponent<Animator>();
            if (animator == null || !animator.enabled || heldAnimators.Contains(animator))
                continue;

            heldAnimators.Add(animator);
            animator.enabled = false;
        }
    }

    private bool StillOwnsPause()
    {
        return ownsPause && pausedDirector != null && pausedDirector.isActiveAndEnabled &&
               pausedDirector.state == PlayState.Paused && pausedDirector.playableAsset == pausedAsset &&
               pausedDirector.playableGraph.IsValid() && pausedDirector.playableGraph.Equals(pausedGraph) &&
               Math.Abs(pausedDirector.time - pausedTime) < 0.000001;
    }

    private void Cleanup()
    {
        bool resume = !shuttingDown && StillOwnsPause();
        PlayableDirector resumeDirector = pausedDirector;
        ConversationManager.OnConversationStarted -= HandleConversationStarted;
        if (activeTrigger != null)
            activeTrigger.ConversationFinished -= HandleTriggerFinished;
        if (pausedDirector != null)
        {
            pausedDirector.stopped -= InvalidatePause;
            pausedDirector.played -= InvalidatePause;
        }

        foreach (Animator animator in heldAnimators)
            if (animator != null)
                animator.enabled = true;
        heldAnimators.Clear();

        storyToken?.Release();
        storyToken = null;
        ownsPause = false;
        acceptingStart = false;
        pausedDirector = null;
        activeTrigger = null;
        conversationManager = null;
        routine = null;

        if (resume)
            resumeDirector.Resume();
        running = false;
    }

    private void OnDisable()
    {
        if (routine != null)
            StopCoroutine(routine);
        Cleanup();
    }

    private void OnApplicationQuit()
    {
        shuttingDown = true;
    }
}
