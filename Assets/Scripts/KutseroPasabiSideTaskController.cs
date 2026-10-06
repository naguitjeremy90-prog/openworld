using System.Collections;
using DialogueEditor;
using UnityEngine;

/// <summary>Coordinates the one-time Kutsero message task and its kalesa-side handoff.</summary>
public sealed class KutseroPasabiSideTaskController : MonoBehaviour
{
    public const string SideTaskId = "side_kutsero_pasabi";
    public const string OfferAvailableFlag = "makamisa_kutsero_pasabi_offer_available";
    public const string CompletedFlag = "makamisa_kutsero_pasabi_complete";
    public const string TravelUnlockedFlag = "makamisa_kalesa_travel_unlocked";
    public const string OfferConversationStageId = "deliver_message";
    public const string ReturnConversationStageId = "return_to_kutsero";

    private const string MainTaskId = "main_investigate_pili";
    private const string MainTaskEligibilityStageId = "return_to_anday";
    private const float HudRestoreSettleSeconds = 0.25f;

    [Header("Existing characters and dialogue")]
    [SerializeField] private NPCConversationTrigger kutseroTrigger;
    [SerializeField] private NPCConversationTrigger tinderaTrigger;
    [SerializeField] private NPCConversation offerConversation;
    [SerializeField] private NPCConversation tinderaConversation;
    [SerializeField] private NPCConversation returnConversation;
    [SerializeField] private Transform kutseroRoot;
    [SerializeField] private Transform kutseroKalesaPosition;

    [Header("Existing presentation and player control")]
    [SerializeField] private FadeController fadeController;
    [SerializeField] private MonoBehaviour[] gameplayBehavioursToDisable =
        System.Array.Empty<MonoBehaviour>();

    private Coroutine pendingRoutine;
    private StorySequenceToken storySequenceToken;
    private bool[] previousBehaviourStates;
    private bool sequenceRunning;

    private void OnEnable()
    {
        if (kutseroTrigger != null)
            kutseroTrigger.ConversationFinished += HandleKutseroConversationFinished;
        if (tinderaTrigger != null)
            tinderaTrigger.ConversationFinished += HandleTinderaConversationFinished;
    }

    private void Start()
    {
        if (SessionStoryState.GetFlag(CompletedFlag) ||
            SessionStoryState.GetFlag(TravelUnlockedFlag))
            MoveKutseroToKalesaPosition();

        RefreshOfferAvailability();
    }

    private void Update()
    {
        RefreshOfferAvailability();
    }

    private void LateUpdate()
    {
        if (!sequenceRunning || gameplayBehavioursToDisable == null)
            return;

        // Keep the explicit sequence lock after dialogue-owned locks are released.
        foreach (MonoBehaviour behaviour in gameplayBehavioursToDisable)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (kutseroTrigger != null)
            kutseroTrigger.ConversationFinished -= HandleKutseroConversationFinished;
        if (tinderaTrigger != null)
            tinderaTrigger.ConversationFinished -= HandleTinderaConversationFinished;

        if (pendingRoutine != null)
            StopCoroutine(pendingRoutine);
        pendingRoutine = null;
        sequenceRunning = false;
        RestoreGameplayBehaviours();
        ReleaseStorySequence();
    }

    private void HandleKutseroConversationFinished()
    {
        if (pendingRoutine != null || sequenceRunning || kutseroTrigger == null)
            return;

        NPCConversation playedConversation = kutseroTrigger.LastStartedConversation;
        if (playedConversation == offerConversation && IsOfferEligible())
        {
            BeginSequenceLock();
            pendingRoutine = StartCoroutine(StartTaskAfterDialogueCloses());
        }
        else if (playedConversation == returnConversation && IsReturnStageActive())
        {
            BeginSequenceLock();
            pendingRoutine = StartCoroutine(CompleteTaskAndRelocateAfterDialogueCloses());
        }
    }

    private void HandleTinderaConversationFinished()
    {
        if (pendingRoutine != null || sequenceRunning || tinderaTrigger == null ||
            tinderaTrigger.LastStartedConversation != tinderaConversation ||
            TaskManager.Instance == null ||
            TaskManager.Instance.GetTaskState(SideTaskId) != TaskState.Active)
            return;

        BeginSequenceLock();
        pendingRoutine = StartCoroutine(ReleaseAfterDialogueCloses());
    }

    private IEnumerator StartTaskAfterDialogueCloses()
    {
        yield return WaitForDialogueToClose();

        // Keep Miguel locked, but let the HUD finish restoring its captured state
        // before StartTask populates a tracker that was empty when suppression began.
        ReleaseStorySequence();
        yield return new WaitForSecondsRealtime(HudRestoreSettleSeconds);

        TaskManager manager = TaskManager.Instance;
        if (manager != null && IsOfferEligible())
            manager.StartTask(SideTaskId);

        RefreshOfferAvailability();
        FinishSequenceLock();
        pendingRoutine = null;
    }

    private IEnumerator ReleaseAfterDialogueCloses()
    {
        yield return WaitForDialogueToClose();
        FinishSequenceLock();
        pendingRoutine = null;
    }

    private IEnumerator CompleteTaskAndRelocateAfterDialogueCloses()
    {
        yield return WaitForDialogueToClose();

        TaskManager manager = TaskManager.Instance;
        if (manager == null || !manager.CompleteTask(SideTaskId))
        {
            FinishSequenceLock();
            pendingRoutine = null;
            yield break;
        }

        SessionStoryState.SetFlag(CompletedFlag, true);
        SessionStoryState.SetFlag(TravelUnlockedFlag, true);
        SessionStoryState.SetFlag(OfferAvailableFlag, false);

        if (fadeController != null)
        {
            yield return fadeController.FadeToBlack();
            // Let the existing full-screen fade render fully black before moving the NPC.
            yield return null;
        }
        else
            Debug.LogWarning("Kutsero side-task transition has no FadeController reference.", this);

        MoveKutseroToKalesaPosition();

        if (fadeController != null)
            yield return fadeController.FadeFromBlack();

        FinishSequenceLock();
        pendingRoutine = null;
    }

    private IEnumerator WaitForDialogueToClose()
    {
        ConversationManager manager = ConversationManager.Instance;
        while (manager != null && manager.IsConversationActive)
        {
            manager = ConversationManager.Instance;
            yield return null;
        }

        // Allow the dialogue manager's final UI-off update to render before presentation changes.
        yield return null;
    }

    private void BeginSequenceLock()
    {
        sequenceRunning = true;
        storySequenceToken = StorySequenceCoordinator.Acquire(this);
        CaptureAndDisableGameplayBehaviours();
    }

    private void FinishSequenceLock()
    {
        sequenceRunning = false;
        RestoreGameplayBehaviours();
        ReleaseStorySequence();
    }

    private void CaptureAndDisableGameplayBehaviours()
    {
        if (gameplayBehavioursToDisable == null)
            return;

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
        if (previousBehaviourStates == null || gameplayBehavioursToDisable == null)
            return;

        for (int i = 0; i < gameplayBehavioursToDisable.Length &&
             i < previousBehaviourStates.Length; i++)
        {
            MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
            if (behaviour != null)
                behaviour.enabled = previousBehaviourStates[i];
        }

        previousBehaviourStates = null;
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken == null)
            return;

        storySequenceToken.Release();
        storySequenceToken = null;
    }

    private void RefreshOfferAvailability()
    {
        ConversationManager conversationManager = ConversationManager.Instance;
        bool conversationClosed = conversationManager == null ||
                                 !conversationManager.IsConversationActive;
        bool available = conversationClosed && IsOfferEligible();
        SessionStoryState.SetFlag(OfferAvailableFlag, available);
    }

    private bool IsOfferEligible()
    {
        if (SessionStoryState.GetFlag(CompletedFlag) ||
            SessionStoryState.GetFlag(TravelUnlockedFlag))
            return false;

        TaskManager manager = TaskManager.Instance;
        if (manager == null || manager.GetTaskState(SideTaskId) != TaskState.Inactive)
            return false;

        TaskState mainTaskState = manager.GetTaskState(MainTaskId);
        return mainTaskState == TaskState.Completed ||
               (mainTaskState == TaskState.Active &&
                manager.IsCurrentStageAtOrAfter(MainTaskId, MainTaskEligibilityStageId));
    }

    private bool IsReturnStageActive()
    {
        TaskManager manager = TaskManager.Instance;
        return manager != null && manager.GetTaskState(SideTaskId) == TaskState.Active &&
               manager.IsCurrentStage(SideTaskId, ReturnConversationStageId);
    }

    private void MoveKutseroToKalesaPosition()
    {
        if (kutseroRoot == null || kutseroKalesaPosition == null)
        {
            Debug.LogWarning("Kutsero relocation is missing its root or position marker.", this);
            return;
        }

        kutseroRoot.SetPositionAndRotation(
            kutseroKalesaPosition.position,
            kutseroKalesaPosition.rotation);
        Physics.SyncTransforms();
    }

#if UNITY_EDITOR
    [ContextMenu("Prepare Kalesa Travel Test")]
    internal bool PrepareKalesaTravelTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Prepare Kalesa Travel Test is available only during Play Mode.", this);
            return false;
        }

        UnityEngine.SceneManagement.Scene activeScene =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.name != "NEWMAKAMISA")
        {
            Debug.LogWarning("Prepare Kalesa Travel Test can only run in NEWMAKAMISA.", this);
            return false;
        }

        if (kutseroRoot == null || kutseroKalesaPosition == null)
        {
            Debug.LogError("Prepare Kalesa Travel Test is missing Kutsero or KutseroKalesaPosition.", this);
            return false;
        }

        KalesaTravelController travelController = FindAnyObjectByType<KalesaTravelController>();
        if (travelController == null || travelController.gameObject.scene != activeScene)
        {
            Debug.LogError("Prepare Kalesa Travel Test could not find the NEWMAKAMISA KalesaTravelController.", this);
            return false;
        }

        ConversationManager conversationManager = ConversationManager.Instance;
        if (StorySequenceCoordinator.IsStorySequenceActive ||
            (conversationManager != null && conversationManager.IsConversationActive))
        {
            Debug.LogWarning("Finish the current conversation or story sequence before preparing the Kalesa test.", this);
            return false;
        }

        SessionStoryState.SetFlag(CompletedFlag, true);
        SessionStoryState.SetFlag(TravelUnlockedFlag, true);
        SessionStoryState.SetFlag(OfferAvailableFlag, false);
        SessionStoryState.SetString(KalesaTravelController.CurrentDestinationStateId, string.Empty);
        SessionStoryState.SetString(KalesaTravelController.PendingDestinationStateId, string.Empty);

        travelController.RefreshAfterDevelopmentTravelStateReset();
        MoveKutseroToKalesaPosition();

        Debug.Log(
            "[KutseroPasabiSideTaskController] Prepared Kalesa travel test; " +
            "Pasabi completion and travel unlock are session-only; the existing Kalesa was left in place.",
            this);
        return true;
    }
#endif
}
