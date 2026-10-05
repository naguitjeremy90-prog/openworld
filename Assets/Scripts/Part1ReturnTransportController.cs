using System;
using System.Collections;
using DialogueEditor;
using UnityEngine;

/// <summary>Runs the short Part 1 return transport after Anday's recollection payoff.</summary>
public sealed class Part1ReturnTransportController : MonoBehaviour
{
    private const string TransportStartedFlag = "makamisa_part1_return_transport_started";
    private const string PostRecollectionPayoffFlag = "makamisa_anday_post_recollection_payoff_applied";
    private const string TransportDestinationStateId = "makamisa_transport_destination";
    private const string Part1PosadaDestination = "posada_part1";

    [Header("Existing gameplay systems")]
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private CameraFocusManager cameraFocusManager;
    [SerializeField] private CameraShakeController cameraShake;
    [SerializeField] private SelfDialogueTrigger firstReaction;
    [SerializeField] private SelfDialogueTrigger secondReaction;
    [SerializeField] private IrisTransitionController irisTransition;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float normalGameplayDuration = 10f;
    [SerializeField, Min(0f)] private float betweenReactionsDelay = 1f;
    [SerializeField, Min(0f)] private float finalPause = 1.5f;
    [SerializeField, Min(0f)] private float firstShakeDuration = 0.5f;
    [SerializeField, Min(0f)] private float firstShakeStrength = 0.12f;
    [SerializeField, Min(0f)] private float secondShakeDuration = 1f;
    [SerializeField, Min(0f)] private float secondShakeStrength = 0.32f;
    [SerializeField] private string destinationSceneName = "TransportationScene";

    private Coroutine sequenceRoutine;
    private StorySequenceToken storyToken;
    private bool handoffReceived;
    private bool sequenceRunning;
    private bool transitionHandedOff;
    private bool previousMovementEnabled;
    private bool movementStateCaptured;
    private bool dialogueFailed;

    public void BeginAfterAndayPayoff()
    {
        if (handoffReceived ||
            SessionStoryState.GetFlag(TransportStartedFlag) ||
            !SessionStoryState.GetFlag(AndayRecollectionEntryController.CompleteFlag) ||
            !SessionStoryState.GetFlag(PostRecollectionPayoffFlag))
            return;

        if (playerMovement == null || cameraShake == null ||
            firstReaction == null || secondReaction == null)
        {
            Debug.LogError("Part 1 return transport is missing a required scene reference.", this);
            return;
        }

        handoffReceived = true;
        sequenceRoutine = StartCoroutine(WaitForNormalGameplayThenTransport());
    }

    private void LateUpdate()
    {
        if (sequenceRunning && playerMovement != null)
            playerMovement.enabled = false;
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);
        sequenceRoutine = null;

        if (!transitionHandedOff)
        {
            RestoreMovement();
            storyToken?.Release();
        }
        storyToken = null;
        sequenceRunning = false;
    }

    private IEnumerator WaitForNormalGameplayThenTransport()
    {
        float elapsed = 0f;
        while (elapsed < normalGameplayDuration)
        {
            if (IsGameplaySafe())
                elapsed += Time.deltaTime;
            yield return null;
        }

        // Do not interrupt a conversation, camera focus, or another story sequence
        // that began during the quiet period.
        while (!IsGameplaySafe())
            yield return null;

        SessionStoryState.SetFlag(TransportStartedFlag, true);
        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("Part 1 return transport could not acquire its story-sequence token.", this);
            sequenceRoutine = null;
            yield break;
        }

        sequenceRunning = true;
        previousMovementEnabled = playerMovement.enabled;
        movementStateCaptured = true;
        playerMovement.enabled = false;

        yield return cameraShake.ShakeAndWait(firstShakeDuration, firstShakeStrength);
        yield return PlayReaction(firstReaction);
        if (dialogueFailed)
        {
            AbortSequence();
            yield break;
        }

        yield return WaitScaled(betweenReactionsDelay);
        yield return cameraShake.ShakeAndWait(secondShakeDuration, secondShakeStrength);
        yield return PlayReaction(secondReaction);
        if (dialogueFailed)
        {
            AbortSequence();
            yield break;
        }

        yield return WaitScaled(finalPause);

        IrisTransitionController transition = irisTransition != null
            ? irisTransition
            : IrisTransitionController.Instance;
        SessionStoryState.SetString(TransportDestinationStateId, Part1PosadaDestination);
        Coroutine handoff = transition != null
            ? transition.TransitionToScene(destinationSceneName, storyToken)
            : null;

        if (handoff == null)
        {
            SessionStoryState.SetString(TransportDestinationStateId, string.Empty);
            Debug.LogError("Part 1 return transport could not start the iris scene transition.", this);
            AbortSequence();
            yield break;
        }

        transitionHandedOff = true;
        storyToken = null; // IrisTransitionController releases the token after the scene opens.
        yield return handoff;
        sequenceRoutine = null;
    }

    private IEnumerator PlayReaction(SelfDialogueTrigger trigger)
    {
        ConversationManager manager = ConversationManager.Instance;
        if (manager == null || trigger == null)
        {
            dialogueFailed = true;
            Debug.LogError("Part 1 return transport has no active ConversationManager or reaction.", this);
            yield break;
        }

        bool started = false;
        bool finished = false;
        ConversationManager.ConversationStartEvent startedHandler = () => started = true;
        Action finishedHandler = () => finished = true;
        ConversationManager.OnConversationStarted += startedHandler;
        trigger.ConversationFinished += finishedHandler;
        trigger.StartSelfDialogue();

        float startDeadline = Time.realtimeSinceStartup + 1f;
        while (!started && Time.realtimeSinceStartup < startDeadline)
            yield return null;

        if (!started)
        {
            dialogueFailed = true;
            Debug.LogError("A Part 1 return self-dialogue did not start.", trigger);
        }
        else
        {
            while (!finished)
            {
                if (!manager.IsConversationActive)
                    break;
                yield return null;
            }
        }

        trigger.ConversationFinished -= finishedHandler;
        ConversationManager.OnConversationStarted -= startedHandler;
    }

    private bool IsGameplaySafe()
    {
        ConversationManager manager = ConversationManager.Instance;
        return playerMovement != null && playerMovement.enabled &&
               !StorySequenceCoordinator.IsStorySequenceActive &&
               (manager == null || !manager.IsConversationActive) &&
               (cameraFocusManager == null || !cameraFocusManager.IsFocusing);
    }

    private IEnumerator WaitScaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void AbortSequence()
    {
        sequenceRunning = false;
        RestoreMovement();
        storyToken?.Release();
        storyToken = null;
        sequenceRoutine = null;
    }

    private void RestoreMovement()
    {
        if (movementStateCaptured && playerMovement != null)
            playerMovement.enabled = previousMovementEnabled;
        movementStateCaptured = false;
    }
}
