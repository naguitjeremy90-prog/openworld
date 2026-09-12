using System;
using System.Collections;
using DialogueEditor;
using UnityEngine;

/// <summary>Story-facing bridge for the generic Lumang Sulatin document completion.</summary>
public sealed class ManuscriptTransportSequenceController : MonoBehaviour
{
    private const string DocumentId = "lumang_sulatin";
    private const string InvestigatedFlag = "church_manuscript_investigated";
    private const string MainTaskId = "main_investigate_pili";
    private const string ChurchStageId = "investigate_church_document";

    [Header("Generic Clarity completion")]
    [SerializeField] private ClarityDocumentViewer documentViewer;
    [SerializeField] private string destinationSceneName = "NEWMAKAMISA";

    [Header("Existing self-dialogue data")]
    [SerializeField] private SelfDialogueTrigger firstReaction;
    [SerializeField] private SelfDialogueTrigger secondReaction;

    [Header("Existing gameplay controls to lock")]
    [SerializeField] private MonoBehaviour[] gameplayBehavioursToDisable = Array.Empty<MonoBehaviour>();

    [Header("Reusable camera/transition")]
    [SerializeField] private CameraShakeController cameraShake;
    [SerializeField] private ScreenFadeController preReactionFade;
    [SerializeField] private IrisTransitionController irisTransition;
    [SerializeField, Min(0f)] private float betweenReactionDelay = 1f;
    [SerializeField, Min(0f)] private float afterSecondReactionDelay = 1f;
    [SerializeField, Min(0f)] private float firstShakeDuration = 0.4f;
    [SerializeField, Min(0f)] private float firstShakeStrength = 0.12f;
    [SerializeField, Min(0f)] private float secondShakeDuration = 0.65f;
    [SerializeField, Min(0f)] private float secondShakeStrength = 0.32f;

    private bool sequenceRunning;
    private Coroutine sequenceRoutine;
    private bool[] previousBehaviourStates;
    private StorySequenceToken storySequenceToken;
    private bool sequenceHandedOff;

    public bool IsRunning => sequenceRunning;

    private void OnEnable()
    {
        if (documentViewer != null)
            documentViewer.OnDocumentInvestigationCompleted += HandleDocumentCompleted;
    }

    private void Start()
    {
        if (documentViewer == null)
            documentViewer = FindAnyObjectByType<ClarityDocumentViewer>();
        if (cameraShake == null)
            cameraShake = FindAnyObjectByType<CameraShakeController>();
        if (preReactionFade == null)
            preReactionFade = FindAnyObjectByType<ScreenFadeController>();
        if (irisTransition == null)
            irisTransition = IrisTransitionController.Instance ?? FindAnyObjectByType<IrisTransitionController>();

        if (documentViewer != null)
            documentViewer.OnDocumentInvestigationCompleted -= HandleDocumentCompleted;
        if (documentViewer != null)
            documentViewer.OnDocumentInvestigationCompleted += HandleDocumentCompleted;
    }

    private void OnDisable()
    {
        if (documentViewer != null)
            documentViewer.OnDocumentInvestigationCompleted -= HandleDocumentCompleted;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);
        sequenceRoutine = null;
        sequenceRunning = false;
        if (!sequenceHandedOff && storySequenceToken != null)
            storySequenceToken.Release();
        storySequenceToken = null;
        sequenceHandedOff = false;
        if (preReactionFade != null)
            preReactionFade.ClearImmediately();
        RestoreGameplayBehaviours();
    }

    private void LateUpdate()
    {
        if (!sequenceRunning)
            return;

        // DialogueMovementLock intentionally remains enabled for presentation;
        // reassert only this sequence's explicit gameplay lock after it releases.
        for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private void HandleDocumentCompleted(string completedDocumentId)
    {
        if (sequenceRunning || !string.Equals(completedDocumentId, DocumentId, StringComparison.Ordinal))
            return;
        if (SessionStoryState.GetFlag(InvestigatedFlag))
            return;

        SessionStoryState.SetFlag(InvestigatedFlag, true);
        CompleteMainTaskObjective();
        if (documentViewer != null && documentViewer.IsOpen)
            documentViewer.CloseDocument();

        sequenceRunning = true;
        storySequenceToken = StorySequenceCoordinator.Acquire(this);
        sequenceHandedOff = false;
        CaptureAndDisableGameplayBehaviours();
        sequenceRoutine = StartCoroutine(TransportRoutine());
    }

    private void CompleteMainTaskObjective()
    {
        TaskManager manager = TaskManager.Instance;
        if (manager == null)
            return;

        if (manager.GetTaskState(MainTaskId) != TaskState.Active)
            return;

        // The authored task already ends at the church-document stage. Reassert
        // that stage, then complete it through the existing TaskManager API.
        manager.AdvanceTaskStage(MainTaskId, ChurchStageId);
        manager.CompleteTask(MainTaskId);
    }

    private IEnumerator TransportRoutine()
    {
        if (preReactionFade != null)
            yield return preReactionFade.FadeOutHoldAndIn();
        else
            Debug.LogWarning("Manuscript transport has no pre-reaction screen fade.", this);

        yield return PlayReaction(firstReaction);

        if (cameraShake != null)
            yield return cameraShake.ShakeAndWait(firstShakeDuration, firstShakeStrength);

        yield return WaitUnscaled(betweenReactionDelay);
        yield return PlayReaction(secondReaction);

        if (cameraShake != null)
            yield return cameraShake.ShakeAndWait(secondShakeDuration, secondShakeStrength);

        yield return WaitUnscaled(afterSecondReactionDelay);
        IrisTransitionController transition = irisTransition != null
            ? irisTransition
            : IrisTransitionController.Instance;
        if (transition != null)
        {
            Coroutine transitionRoutine = transition.TransitionToScene(
                destinationSceneName,
                storySequenceToken);
            sequenceHandedOff = transitionRoutine != null;
            yield return transitionRoutine;
        }
        else
            Debug.LogWarning("Manuscript transport has no IrisTransitionController; destination was not loaded.", this);

        if (!sequenceHandedOff && storySequenceToken != null)
            storySequenceToken.Release();
        storySequenceToken = null;
        sequenceRunning = false;
        RestoreGameplayBehaviours();
        sequenceRoutine = null;
    }

    private IEnumerator PlayReaction(SelfDialogueTrigger trigger)
    {
        if (trigger == null)
        {
            Debug.LogWarning("Manuscript transport is missing a self-dialogue reaction.", this);
            yield break;
        }

        bool finished = false;
        bool started = false;
        ConversationManager.ConversationStartEvent startedHandler = () => started = true;
        Action finishedHandler = () => finished = true;
        ConversationManager.OnConversationStarted += startedHandler;
        trigger.ConversationFinished += finishedHandler;
        trigger.StartSelfDialogue();

        float waitForStart = 0f;
        while (!started && waitForStart < 1f)
        {
            waitForStart += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!started)
            Debug.LogWarning("The configured self-dialogue did not start.", trigger);
        else
        {
            while (!finished)
            {
                // The project dialogue manager exposes both the end event and
                // an active-state property. Accept the latter as a safe
                // fallback if a trigger is disabled during UI teardown.
                if (DialogueEditor.ConversationManager.Instance != null &&
                    !DialogueEditor.ConversationManager.Instance.IsConversationActive)
                    break;
                yield return null;
            }
        }

        trigger.ConversationFinished -= finishedHandler;
        ConversationManager.OnConversationStarted -= startedHandler;
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CaptureAndDisableGameplayBehaviours()
    {
        previousBehaviourStates = new bool[gameplayBehavioursToDisable.Length];
        for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
            previousBehaviourStates[i] = behaviour != null && behaviour.enabled;
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private void RestoreGameplayBehaviours()
    {
        if (previousBehaviourStates == null)
            return;

        for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
            if (behaviour != null && i < previousBehaviourStates.Length)
                behaviour.enabled = previousBehaviourStates[i];
        }

        previousBehaviourStates = null;
    }
}
