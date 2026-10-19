using System.Collections;
using DialogueEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the post-Posada return conversation with Maestro Ben without changing
/// the original Part 1 encounter flow.
/// </summary>
public sealed class MaestroBenPart2ReturnController : MonoBehaviour
{
    private const string RequiredScene = "MaestroBenHouse";
    private const string OldTaskId = "main_investigate_pili";
    private const string NewTaskId = "main_visit_maestro_ben";
    private const string NewStageId = "visit_maestro_ben";
    private const string HandoffStageId = "receive_cecilia_ysagani_pages";
    private const string CompletionFlag = "maestro_ben_part2_return_conversation_complete";
    private const string HandoffCompletionFlag = "maestro_ben_part2_cecilia_ysagani_handoff_complete";
    private const string PresentationCompleteFlag = "maestro_ben_part2_cecilia_ysagani_presentation_complete";
    public const string Part2CompleteFlag = "alaala_part2_complete";
    private const string DiscussionStageId = "discuss_cecilia_ysagani_pages";
    private const float JournalUnlockRecoveryInterval = 0.5f;
    private const string PresentationPhaseKey = "maestro_ben_part2_cecilia_ysagani_presentation_phase";
    private const int NeedAcquisitionPopup = 0;
    private const int AcquisitionPopupQueued = 1;
    private const int AcquisitionPopupComplete = 2;
    private const int StoryViewerOpened = 3;
    private const int StoryViewerClosed = 4;
    private const int InventoryClosedAfterViewer = 5;
    private const int Part2Complete = 6;

    [Header("Scene References")]
    [SerializeField] private GameObject originalMaestro;
    [SerializeField] private GameObject standingMaestro;
    [SerializeField] private CapsuleCollider standingMaestroCollider;
    [SerializeField] private GameObject standingMaestroInteraction;
    [SerializeField] private Transform miguelRoot;
    [SerializeField] private NPCConversation returnConversation;
    [SerializeField] private NPCConversation handoffConversation;
    [SerializeField] private NPCConversation finalFollowUpConversation;
    [SerializeField] private InventoryItemData handoffItem;
    [SerializeField] private CameraFocusManager finalConversationFocusManager;
    [SerializeField] private CameraFocusPoint finalConversationFocusPoint;
    [SerializeField] private NPCConversation postPart2SulatinInstructionConversation;

    private const float PostPart2FadeDuration = 1.5f;
    private const int DialogueAboveBlackFadeSortingOrder = 32101;

    private StorySequenceToken storyToken;
    private ConversationManager conversationManager;
    private bool subscribedToConversationClose;
    private bool sequenceRunning;
    private bool conversationStarted;
    private bool closeReceived;
    private bool closeWasNormal;
    private bool completedSuccessfully;
    private bool handoffRewardRoutineRunning;
    private bool finalConversationRoutineRunning;
    private bool finalConversationStarted;
    private bool finalConversationClosed;
    private bool finalConversationClosedNormally;
    private bool finalConversationCameraFocusRequested;
    private bool retriedInterruptedFinalConversation;
    private bool postPart2InstructionStarted;
    private bool postPart2InstructionClosed;
    private bool postPart2InstructionClosedNormally;
    private bool postPart2TransitionStarted;
    private DialogueMovementLock postPart2MovementLock;
    private int rewardCallbackGeneration;
    private NPCConversationTrigger standingInteractionTrigger;
    private InventoryUI presentationInventoryUI;
    private InventoryDocumentViewer presentationViewer;
    private Coroutine journalUnlockRecoveryRoutine;

    private void Start()
    {
        StartCoroutine(WaitForSceneReadiness());
    }

    private void OnEnable()
    {
        ConversationManager.ConversationClosed += HandleConversationClosed;
        subscribedToConversationClose = true;
        if (SessionStoryState.GetFlag(Part2CompleteFlag))
            StartPart2JournalUnlockRecovery();
        else if (SessionStoryState.GetFlag(HandoffCompletionFlag) &&
            !SessionStoryState.GetFlag(Part2CompleteFlag))
            StartHandoffRewardDelivery();
    }

    private IEnumerator WaitForSceneReadiness()
    {
        if (SceneManager.GetActiveScene().name != RequiredScene)
            yield break;

        // The Part 1 controller runs from Awake at -10000. Let scene Start and
        // the first editor-restored task/UI initialization pass complete first.
        while (TaskManager.Instance == null || ConversationManager.Instance == null ||
               miguelRoot == null || standingMaestro == null || originalMaestro == null ||
               returnConversation == null || StorySequenceCoordinator.Instance == null)
        {
            if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                yield break;
            yield return null;
        }

        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);

        // Keep the standing presentation on every Part 2 visit. The completion
        // flag suppresses only the automatic story conversation.
        if (!IsPart2PresentationState())
            yield break;

        if (!ApplyPart2Presentation())
            yield break;

        if (SessionStoryState.GetFlag(CompletionFlag))
        {
            if (SessionStoryState.GetFlag(HandoffCompletionFlag) &&
                !SessionStoryState.GetFlag(Part2CompleteFlag))
                StartHandoffRewardDelivery();
            yield break;
        }

        // Do not overlap another authored sequence. Re-check all story state
        // after waiting because the task can change while another sequence runs.
        while (StorySequenceCoordinator.IsStorySequenceActive)
        {
            if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                yield break;
            yield return null;
        }

        if (!IsEligible())
            yield break;

        if (standingMaestroCollider == null)
            standingMaestroCollider = standingMaestro.GetComponent<CapsuleCollider>();

        if (standingMaestroCollider == null)
        {
            Debug.LogWarning("Maestro Ben Part 2 return could not find the standing Maestro collider.", this);
            yield break;
        }

        if (standingMaestro.GetComponent<CharacterReactionController>() == null)
        {
            Debug.LogWarning("Maestro Ben Part 2 return is missing the standing Maestro reaction controller.", this);
            yield break;
        }

        conversationManager = ConversationManager.Instance;
        if (conversationManager == null || conversationManager.IsConversationActive)
        {
            Debug.LogWarning("Maestro Ben Part 2 return could not start because the conversation system is unavailable or busy.", this);
            yield break;
        }

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null || !storyToken.IsValid)
        {
            Debug.LogWarning("Maestro Ben Part 2 return could not acquire its story-sequence token.", this);
            ReleaseStoryToken();
            yield break;
        }

        sequenceRunning = true;

        try
        {
            standingMaestroCollider.enabled = false;

            yield return new WaitForSecondsRealtime(1f);

            yield return RotateCharactersToFaceEachOther();

            yield return new WaitForSecondsRealtime(0.8f);

            if (!IsEligible() || conversationManager.IsConversationActive)
            {
                AbortSequence();
                yield break;
            }

            conversationManager.StartConversation(returnConversation);
            conversationStarted = true;

            while (!closeReceived)
            {
                if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                {
                    AbortSequence();
                    yield break;
                }
                yield return null;
            }

            if (!closeWasNormal)
            {
                AbortSequence();
                yield break;
            }

            SessionStoryState.SetFlag(CompletionFlag, true);
            TaskManager manager = TaskManager.Instance;
            if (manager == null || !manager.AdvanceTaskStage(NewTaskId, HandoffStageId))
            {
                Debug.LogWarning(
                    "Maestro Ben Part 2 return completed, but its handoff task stage could not be activated.",
                    this);
            }
            completedSuccessfully = true;
            sequenceRunning = false;
            ApplyPart2Presentation();
            ReleaseStoryToken();
        }
        finally
        {
            if (!completedSuccessfully && sequenceRunning)
                AbortSequence();
        }
    }

    private bool IsEligible()
    {
        TaskManager manager = TaskManager.Instance;
        return manager != null &&
               manager.GetTaskState(NewTaskId) == TaskState.Active &&
               manager.IsCurrentStage(NewTaskId, NewStageId) &&
               manager.GetTaskState(OldTaskId) == TaskState.Completed &&
               SessionStoryState.GetFlag(MaestroBenEncounterController.IntroSeenFlag) &&
               SessionStoryState.GetFlag(MaestroBenEncounterController.CompletedFlag) &&
               !SessionStoryState.GetFlag(CompletionFlag);
    }

    private bool IsPart2PresentationState()
    {
        TaskManager manager = TaskManager.Instance;
        bool part2TaskActive = manager != null &&
                               manager.GetTaskState(NewTaskId) == TaskState.Active &&
                               manager.IsCurrentStage(NewTaskId, NewStageId);
        return part2TaskActive || SessionStoryState.GetFlag(CompletionFlag);
    }

    private bool ApplyPart2Presentation()
    {
        if (originalMaestro == null || standingMaestro == null)
        {
            Debug.LogWarning("Maestro Ben Part 2 presentation is missing a Maestro reference.", this);
            return false;
        }

        if (standingMaestroCollider == null)
            standingMaestroCollider = standingMaestro.GetComponent<CapsuleCollider>();

        if (standingMaestroInteraction == null)
        {
            Debug.LogWarning("Maestro Ben Part 2 presentation is missing its repeat-interaction object.", this);
            return false;
        }

        standingInteractionTrigger = standingMaestroInteraction.GetComponent<NPCConversationTrigger>();
        if (standingInteractionTrigger == null)
        {
            Debug.LogWarning("Maestro Ben Part 2 interaction object is missing NPCConversationTrigger.", this);
            return false;
        }

        Transform interactionTransform = standingMaestroInteraction.transform;
        if (interactionTransform.parent != standingMaestro.transform)
            interactionTransform.SetParent(standingMaestro.transform, false);

        standingInteractionTrigger.SetFacingTransforms(standingMaestro.transform, miguelRoot);

        originalMaestro.SetActive(false);
        standingMaestro.SetActive(true);

        // The physical capsule is disabled only while the automatic dialogue
        // plays; later normal interaction uses the authored collider state.
        if (standingMaestroCollider != null && !sequenceRunning)
            standingMaestroCollider.enabled = true;

        return true;
    }

    private IEnumerator RotateCharactersToFaceEachOther()
    {
        if (miguelRoot == null || standingMaestro == null)
            yield break;

        Transform maestroRoot = standingMaestro.transform;
        Quaternion miguelStart = Quaternion.Euler(0f, miguelRoot.eulerAngles.y, 0f);
        Quaternion maestroStart = Quaternion.Euler(0f, maestroRoot.eulerAngles.y, 0f);
        Quaternion miguelTarget = GetHorizontalFacingRotation(miguelRoot, maestroRoot);
        Quaternion maestroTarget = GetHorizontalFacingRotation(maestroRoot, miguelRoot);

        const float turnDuration = 0.35f;
        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / turnDuration));
            miguelRoot.rotation = Quaternion.Slerp(miguelStart, miguelTarget, t);
            maestroRoot.rotation = Quaternion.Slerp(maestroStart, maestroTarget, t);
            yield return null;
        }

        miguelRoot.rotation = miguelTarget;
        maestroRoot.rotation = maestroTarget;
    }

    private static Quaternion GetHorizontalFacingRotation(Transform source, Transform target)
    {
        if (source == null || target == null)
            return source != null ? source.rotation : Quaternion.identity;

        Vector3 direction = target.position - source.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return source.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void HandleConversationClosed(NPCConversation closedConversation, bool normal)
    {
        if (postPart2InstructionStarted &&
            closedConversation == postPart2SulatinInstructionConversation)
        {
            postPart2InstructionClosedNormally = normal;
            postPart2InstructionClosed = true;
            postPart2InstructionStarted = false;
            return;
        }

        if (finalConversationStarted && closedConversation == finalFollowUpConversation)
        {
            finalConversationClosedNormally = normal;
            finalConversationClosed = true;
            finalConversationStarted = false;
            return;
        }

        if (conversationStarted && closedConversation == returnConversation)
        {
            closeWasNormal = normal;
            closeReceived = true;
            conversationStarted = false;
            return;
        }

        if (closedConversation != handoffConversation || !normal ||
            handoffConversation == null ||
            SessionStoryState.GetFlag(HandoffCompletionFlag) ||
            !SessionStoryState.GetFlag(CompletionFlag))
            return;

        TaskManager manager = TaskManager.Instance;
        if (manager == null ||
            manager.GetTaskState(NewTaskId) != TaskState.Active ||
            !manager.IsCurrentStage(NewTaskId, HandoffStageId))
            return;

        SessionStoryState.SetFlag(HandoffCompletionFlag, true);
        if (!SessionStoryState.GetFlag(PresentationCompleteFlag) &&
            SessionStoryState.GetInt(PresentationPhaseKey) == 0)
            SessionStoryState.SetInt(PresentationPhaseKey, NeedAcquisitionPopup);
        StartHandoffRewardDelivery();
    }

    private void StartHandoffRewardDelivery()
    {
        if (!isActiveAndEnabled || handoffRewardRoutineRunning ||
            SceneManager.GetActiveScene().name != RequiredScene ||
            !SessionStoryState.GetFlag(HandoffCompletionFlag) ||
            SessionStoryState.GetFlag(Part2CompleteFlag))
        {
            return;
        }

        handoffRewardRoutineRunning = true;
        StartCoroutine(ResumePart2EndingFlow());
    }

    private IEnumerator ResumePart2EndingFlow()
    {
        int callbackGeneration = rewardCallbackGeneration;
        try
        {
            if (handoffItem == null)
            {
                Debug.LogWarning("Maestro Ben handoff has no Inventory item assigned.", this);
                yield break;
            }

            InventoryManager inventory = InventoryManager.Instance;
            while (inventory == null && IsRewardCallbackCurrent(callbackGeneration))
            {
                yield return null;
                inventory = InventoryManager.Instance;
            }

            if (!IsRewardCallbackCurrent(callbackGeneration) || inventory == null)
                yield break;

            if (!inventory.HasItem(handoffItem.ItemID) && !inventory.AddItem(handoffItem))
            {
                if (!inventory.HasItem(handoffItem.ItemID))
                    Debug.LogWarning("Maestro Ben handoff could not add its Inventory item.", this);
                yield break;
            }

            int phase = GetPresentationPhase();
            if (phase == NeedAcquisitionPopup || phase == AcquisitionPopupQueued)
            {
                ItemAcquisitionPopupPresenter popupPresenter = FindAcquisitionPopupPresenter();
                while (popupPresenter == null && IsRewardCallbackCurrent(callbackGeneration))
                {
                    yield return null;
                    popupPresenter = FindAcquisitionPopupPresenter();
                }

                if (popupPresenter == null || !IsRewardCallbackCurrent(callbackGeneration))
                    yield break;

                while (GetPresentationPhase() == AcquisitionPopupQueued &&
                       popupPresenter != null && popupPresenter.IsPresentingOrQueued &&
                       IsRewardCallbackCurrent(callbackGeneration))
                {
                    yield return null;
                    popupPresenter = FindAcquisitionPopupPresenter();
                }

                if (GetPresentationPhase() == AcquisitionPopupComplete)
                {
                    yield return OpenInitialStoryPresentation(callbackGeneration);
                    yield break;
                }

                if (GetPresentationPhase() == AcquisitionPopupQueued)
                    SessionStoryState.SetInt(PresentationPhaseKey, NeedAcquisitionPopup);

                if (!IsRewardCallbackCurrent(callbackGeneration) || popupPresenter == null)
                    yield break;

                SessionStoryState.SetInt(PresentationPhaseKey, AcquisitionPopupQueued);
                popupPresenter.Enqueue(
                    handoffItem.Icon,
                    handoffItem.DisplayName,
                    ItemAcquisitionPopupDestination.Inventory,
                    completed =>
                    {
                        if (!completed ||
                            SceneManager.GetActiveScene().name != RequiredScene ||
                            !SessionStoryState.GetFlag(HandoffCompletionFlag) ||
                            SessionStoryState.GetFlag(Part2CompleteFlag) ||
                            GetPresentationPhase() != AcquisitionPopupQueued)
                        {
                            return;
                        }

                        SessionStoryState.SetInt(PresentationPhaseKey, AcquisitionPopupComplete);
                        if (isActiveAndEnabled &&
                            callbackGeneration == rewardCallbackGeneration)
                            StartHandoffRewardDelivery();
                    });
                yield break;
            }

            if (phase == AcquisitionPopupComplete || phase == StoryViewerOpened)
            {
                yield return OpenInitialStoryPresentation(callbackGeneration);
                yield break;
            }

            if (phase == StoryViewerClosed)
            {
                yield return ResumeWaitingForInventoryClose(callbackGeneration);
                yield break;
            }

            if (phase == InventoryClosedAfterViewer)
                StartFinalConversationWhenReady();
        }
        finally
        {
            handoffRewardRoutineRunning = false;
        }
    }

    private IEnumerator OpenInitialStoryPresentation(int callbackGeneration)
    {
        while (IsRewardCallbackCurrent(callbackGeneration))
        {
            InventoryManager inventory = InventoryManager.Instance;
            InventoryUI inventoryUI = FindInventoryUI();
            if (inventory != null && inventory.HasItem(handoffItem.ItemID) &&
                inventoryUI != null && inventoryUI.OpenInventoryAndSelectItem(handoffItem))
            {
                BindPresentationEvents(inventoryUI);
                if (GetPresentationPhase() == StoryViewerOpened &&
                    inventoryUI.DocumentViewer != null &&
                    inventoryUI.DocumentViewer.IsOpen &&
                    inventoryUI.DocumentViewer.OpenStoryItemID == handoffItem.ItemID)
                    yield break;

                inventoryUI.OpenSelectedDocumentPreview();
                InventoryDocumentViewer viewer = inventoryUI.DocumentViewer;
                if (viewer != null && viewer.IsOpen && viewer.OpenStoryItemID == handoffItem.ItemID)
                {
                    SessionStoryState.SetInt(PresentationPhaseKey, StoryViewerOpened);
                    yield break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator ResumeWaitingForInventoryClose(int callbackGeneration)
    {
        while (IsRewardCallbackCurrent(callbackGeneration) &&
               GetPresentationPhase() == StoryViewerClosed)
        {
            InventoryUI inventoryUI = FindInventoryUI();
            InventoryManager inventory = InventoryManager.Instance;
            if (inventoryUI != null && inventory != null && inventory.HasItem(handoffItem.ItemID))
            {
                BindPresentationEvents(inventoryUI);
                if (!inventoryUI.IsOpen)
                    inventoryUI.OpenInventoryAndSelectItem(handoffItem);
            }

            yield return null;
        }

        if (GetPresentationPhase() == InventoryClosedAfterViewer)
            StartFinalConversationWhenReady();
    }

    private void BindPresentationEvents(InventoryUI inventoryUI)
    {
        if (presentationInventoryUI != inventoryUI)
        {
            UnbindPresentationEvents();
            presentationInventoryUI = inventoryUI;
            if (presentationInventoryUI != null)
                presentationInventoryUI.InventoryClosed += HandleInventoryClosedAfterViewer;
        }

        InventoryDocumentViewer viewer = inventoryUI != null ? inventoryUI.DocumentViewer : null;
        if (presentationViewer == viewer)
            return;

        if (presentationViewer != null)
            presentationViewer.StoryPagesClosed -= HandleStoryPagesClosed;
        presentationViewer = viewer;
        if (presentationViewer != null)
            presentationViewer.StoryPagesClosed += HandleStoryPagesClosed;
    }

    private void UnbindPresentationEvents()
    {
        if (presentationInventoryUI != null)
            presentationInventoryUI.InventoryClosed -= HandleInventoryClosedAfterViewer;
        if (presentationViewer != null)
            presentationViewer.StoryPagesClosed -= HandleStoryPagesClosed;
        presentationInventoryUI = null;
        presentationViewer = null;
    }

    private void HandleStoryPagesClosed(InventoryDocumentViewer viewer, string storyItemID)
    {
        if (viewer == null || viewer != presentationViewer || handoffItem == null ||
            !string.Equals(storyItemID, handoffItem.ItemID, System.StringComparison.Ordinal) ||
            SceneManager.GetActiveScene().name != RequiredScene ||
            GetPresentationPhase() != StoryViewerOpened)
            return;

        SessionStoryState.SetInt(PresentationPhaseKey, StoryViewerClosed);
    }

    private void HandleInventoryClosedAfterViewer(InventoryUI inventoryUI)
    {
        if (inventoryUI == null || inventoryUI != presentationInventoryUI ||
            SceneManager.GetActiveScene().name != RequiredScene ||
            GetPresentationPhase() != StoryViewerClosed)
            return;

        SessionStoryState.SetInt(PresentationPhaseKey, InventoryClosedAfterViewer);
        SessionStoryState.SetFlag(PresentationCompleteFlag, true);
        StartFinalConversationWhenReady();
    }

    private void StartFinalConversationWhenReady()
    {
        if (!isActiveAndEnabled || finalConversationRoutineRunning ||
            SceneManager.GetActiveScene().name != RequiredScene ||
            GetPresentationPhase() != InventoryClosedAfterViewer ||
            SessionStoryState.GetFlag(Part2CompleteFlag))
            return;

        finalConversationRoutineRunning = true;
        StartCoroutine(RunFinalConversationWhenReady());
    }

    private IEnumerator RunFinalConversationWhenReady()
    {
        try
        {
            while (IsFinalConversationPending())
            {
                TaskManager taskManager = TaskManager.Instance;
                ConversationManager manager = ConversationManager.Instance;
                if (taskManager == null || manager == null || finalFollowUpConversation == null ||
                    HasConflictingModalOrSequence(manager))
                {
                    yield return null;
                    continue;
                }

                if (!IsExpectedPart2TaskState(taskManager))
                    yield break;

                if (standingMaestro == null || miguelRoot == null ||
                    (standingMaestroInteraction == null) ||
                    (!standingMaestro.activeInHierarchy && !ApplyPart2Presentation()))
                    yield break;

                storyToken = StorySequenceCoordinator.Acquire(this);
                if (storyToken == null || !storyToken.IsValid)
                {
                    yield return null;
                    continue;
                }

                if (standingMaestroCollider == null && standingMaestro != null)
                    standingMaestroCollider = standingMaestro.GetComponent<CapsuleCollider>();
                if (standingMaestroCollider != null)
                    standingMaestroCollider.enabled = false;

                yield return RotateCharactersToFaceEachOther();

                if (!IsFinalConversationPending() ||
                    SceneManager.GetActiveScene().name != RequiredScene ||
                    ConversationManager.Instance == null ||
                    ConversationManager.Instance.IsConversationActive)
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield return null;
                    continue;
                }

                taskManager = TaskManager.Instance;
                if (!IsExpectedPart2TaskState(taskManager))
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield break;
                }

                if (!taskManager.IsCurrentStage(NewTaskId, DiscussionStageId) &&
                    !taskManager.AdvanceTaskStage(NewTaskId, DiscussionStageId))
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield break;
                }

                conversationManager = ConversationManager.Instance;
                if (conversationManager == null || conversationManager.IsConversationActive)
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield return null;
                    continue;
                }

                if (finalConversationFocusManager == null ||
                    !finalConversationFocusManager.isActiveAndEnabled ||
                    finalConversationFocusPoint == null ||
                    !finalConversationFocusPoint.isActiveAndEnabled ||
                    finalConversationFocusManager.IsFocusing)
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield return null;
                    continue;
                }

                // Match the established regular-camera flow: acquire the story
                // lock first, then focus immediately before starting this dialogue.
                finalConversationFocusManager.FocusOn(finalConversationFocusPoint);
                if (!finalConversationFocusManager.IsFocusing)
                {
                    RestoreStandingMaestroInteraction();
                    ReleaseStoryToken();
                    yield return null;
                    continue;
                }
                finalConversationCameraFocusRequested = true;

                finalConversationClosed = false;
                finalConversationClosedNormally = false;
                finalConversationStarted = true;
                conversationManager.StartConversation(finalFollowUpConversation);

                while (finalConversationStarted && !finalConversationClosed)
                {
                    if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                        yield break;
                    yield return null;
                }

                finalConversationStarted = false;
                RestoreStandingMaestroInteraction();
                yield return RestoreFinalConversationCameraFocus();

                if (finalConversationClosed && finalConversationClosedNormally &&
                    TaskManager.Instance != null &&
                    TaskManager.Instance.IsCurrentStage(NewTaskId, DiscussionStageId) &&
                    TaskManager.Instance.CompleteTask(NewTaskId))
                {
                    SessionStoryState.SetFlag(Part2CompleteFlag, true);
                    SessionStoryState.SetInt(PresentationPhaseKey, Part2Complete);
                    yield return CommitUnlocksAndRunPostPart2Transition();
                    yield break;
                }

                ReleaseStoryToken();
                if (!retriedInterruptedFinalConversation)
                {
                    retriedInterruptedFinalConversation = true;
                    yield return null;
                    continue;
                }

                yield break;
            }
        }
        finally
        {
            finalConversationStarted = false;
            RequestFinalConversationCameraReturn();
            RestoreStandingMaestroInteraction();
            ReleaseStoryToken();
            finalConversationRoutineRunning = false;
        }
    }

    private IEnumerator CommitUnlocksAndRunPostPart2Transition()
    {
        while (isActiveAndEnabled &&
               SceneManager.GetActiveScene().name == RequiredScene &&
               !ArePart2JournalEntriesUnlocked())
        {
            TryUnlockPart2JournalEntries();
            if (!ArePart2JournalEntriesUnlocked())
                yield return new WaitForSecondsRealtime(JournalUnlockRecoveryInterval);
        }

        if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene ||
            !ArePart2JournalEntriesUnlocked())
        {
            StartPart2JournalUnlockRecovery();
            yield break;
        }

        yield return RunPostPart2Transition();
    }

    private IEnumerator RunPostPart2Transition()
    {
        if (postPart2TransitionStarted)
            yield break;

        FadeController fade = FindAnyObjectByType<FadeController>();
        ConversationManager manager = ConversationManager.Instance;
        if (fade == null || postPart2SulatinInstructionConversation == null || manager == null ||
            manager.IsConversationActive || storyToken == null || !storyToken.IsValid)
        {
            Debug.LogError("Post-Part-2 transition is missing its fade, dialogue, conversation manager, or story token.", this);
            yield break;
        }

        postPart2TransitionStarted = true;
        postPart2MovementLock = FindAnyObjectByType<DialogueMovementLock>();
        if (postPart2MovementLock != null)
            postPart2MovementLock.HoldMovementForStorySequence();

        yield return fade.FadeToBlack(PostPart2FadeDuration);
        if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
            yield break;

        Canvas dialogueCanvas = FindDialogueCanvas();
        bool originalOverrideSorting = dialogueCanvas != null && dialogueCanvas.overrideSorting;
        int originalSortingOrder = dialogueCanvas != null ? dialogueCanvas.sortingOrder : 0;
        if (dialogueCanvas == null)
        {
            Debug.LogError("Post-Part-2 dialogue could not find the scene DialogueCanvas.", this);
            yield return fade.FadeFromBlack(PostPart2FadeDuration);
            if (postPart2MovementLock != null)
                postPart2MovementLock.ReleaseStorySequenceHold();
            yield break;
        }

        dialogueCanvas.overrideSorting = true;
        dialogueCanvas.sortingOrder = DialogueAboveBlackFadeSortingOrder;
        postPart2InstructionClosed = false;
        postPart2InstructionClosedNormally = false;
        postPart2InstructionStarted = true;
        manager.StartConversation(postPart2SulatinInstructionConversation);

        while (!postPart2InstructionClosed)
        {
            if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                break;
            yield return null;
        }

        postPart2InstructionStarted = false;
        dialogueCanvas.overrideSorting = originalOverrideSorting;
        dialogueCanvas.sortingOrder = originalSortingOrder;

        if (!postPart2InstructionClosedNormally || !isActiveAndEnabled ||
            SceneManager.GetActiveScene().name != RequiredScene)
        {
            yield return fade.FadeFromBlack(PostPart2FadeDuration);
            if (postPart2MovementLock != null)
                postPart2MovementLock.ReleaseStorySequenceHold();
            Debug.LogWarning("Post-Part-2 transition stopped because Maestro Ben's final instruction did not close normally.", this);
            yield break;
        }

        PostPart2SulatinTaskCoordinator arrivalCoordinator = TaskManager.Instance != null
            ? TaskManager.Instance.GetComponent<PostPart2SulatinTaskCoordinator>()
            : null;
        if (arrivalCoordinator == null || !arrivalCoordinator.PrepareForArrival())
        {
            yield return fade.FadeFromBlack(PostPart2FadeDuration);
            if (postPart2MovementLock != null)
                postPart2MovementLock.ReleaseStorySequenceHold();
            Debug.LogError("Post-Part-2 transition could not transfer its story lock to the persistent arrival coordinator.", this);
            yield break;
        }

        SpawnData.spawnPointName = "MaestroSpawn";
        SessionStoryState.SetFlag(PostPart2SulatinTaskCoordinator.ArrivalPendingFlag, true);
        AsyncOperation load = SceneManager.LoadSceneAsync("DraftWorld", LoadSceneMode.Single);
        if (load == null)
        {
            SessionStoryState.SetFlag(PostPart2SulatinTaskCoordinator.ArrivalPendingFlag, false);
            SpawnData.spawnPointName = string.Empty;
            arrivalCoordinator.CancelPreparedArrival();
            yield return fade.FadeFromBlack(PostPart2FadeDuration);
            if (postPart2MovementLock != null)
                postPart2MovementLock.ReleaseStorySequenceHold();
            Debug.LogError("Post-Part-2 transition could not load DraftWorld.", this);
            yield break;
        }

        // The persistent arrival coordinator acquired its token before the
        // source token is released, so gameplay suppression has no gap.
        storyToken.Release();
        storyToken = null;
    }

    private Canvas FindDialogueCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.scene == gameObject.scene &&
                string.Equals(canvas.gameObject.name, "DialogueCanvas", System.StringComparison.Ordinal))
                return canvas;
        }
        return null;
    }

    private IEnumerator RestoreFinalConversationCameraFocus()
    {
        if (!finalConversationCameraFocusRequested)
            yield break;

        RequestFinalConversationCameraReturn();
        while (finalConversationFocusManager != null &&
               finalConversationFocusManager.IsFocusing)
        {
            if (!isActiveAndEnabled || SceneManager.GetActiveScene().name != RequiredScene)
                yield break;
            yield return null;
        }

        finalConversationCameraFocusRequested = false;
    }

    private bool TryUnlockPart2JournalEntries()
    {
        if (!SessionStoryState.GetFlag(Part2CompleteFlag) ||
            TaskManager.Instance == null ||
            TaskManager.Instance.GetTaskState(NewTaskId) != TaskState.Completed)
        {
            return false;
        }

        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        if (journal == null)
            return false;

        if (!SessionStoryState.GetFlag("journal_observation_unlocked:ang_hindi_natapos_na_salaysay"))
            journal.UnlockObservation("ang_hindi_natapos_na_salaysay");
        if (!SessionStoryState.GetFlag("journal_person_unlocked:cecilia"))
            journal.UnlockPerson("cecilia");
        if (!SessionStoryState.GetFlag("journal_person_unlocked:ysagani"))
            journal.UnlockPerson("ysagani");
        if (!SessionStoryState.GetFlag("journal_fragment_unlocked:ang_kuwento_nina_cecilia_at_ysagani"))
            journal.UnlockFragment("ang_kuwento_nina_cecilia_at_ysagani");
        if (!SessionStoryState.GetFlag("journal_reflection_unlocked:ang_halaga_ng_hindi_natapos"))
            journal.UnlockReflection("ang_halaga_ng_hindi_natapos");

        return ArePart2JournalEntriesUnlocked();
    }

    private static bool ArePart2JournalEntriesUnlocked()
    {
        return SessionStoryState.GetFlag(
                   "journal_observation_unlocked:ang_hindi_natapos_na_salaysay") &&
               SessionStoryState.GetFlag("journal_person_unlocked:cecilia") &&
               SessionStoryState.GetFlag("journal_person_unlocked:ysagani") &&
               SessionStoryState.GetFlag(
                   "journal_fragment_unlocked:ang_kuwento_nina_cecilia_at_ysagani") &&
               SessionStoryState.GetFlag(
                   "journal_reflection_unlocked:ang_halaga_ng_hindi_natapos");
    }

    private void StartPart2JournalUnlockRecovery()
    {
        if (journalUnlockRecoveryRoutine != null ||
            !SessionStoryState.GetFlag(Part2CompleteFlag))
        {
            return;
        }

        journalUnlockRecoveryRoutine =
            StartCoroutine(ReconcilePart2JournalEntriesWhenReady());
    }

    private IEnumerator ReconcilePart2JournalEntriesWhenReady()
    {
        while (isActiveAndEnabled &&
               SceneManager.GetActiveScene().name == RequiredScene &&
               SessionStoryState.GetFlag(Part2CompleteFlag))
        {
            if (TaskManager.Instance != null &&
                TaskManager.Instance.GetTaskState(NewTaskId) == TaskState.Completed &&
                ReconstructionJournalManager.Instance != null)
            {
                TryUnlockPart2JournalEntries();
                break;
            }

            yield return new WaitForSecondsRealtime(JournalUnlockRecoveryInterval);
        }

        journalUnlockRecoveryRoutine = null;
    }

    private void RequestFinalConversationCameraReturn()
    {
        if (!finalConversationCameraFocusRequested ||
            finalConversationFocusManager == null ||
            finalConversationFocusPoint == null ||
            !finalConversationFocusManager.IsFocusing)
        {
            return;
        }

        finalConversationFocusManager.ReturnToNormal(finalConversationFocusPoint);
    }

    private bool IsFinalConversationPending()
    {
        return isActiveAndEnabled &&
               SceneManager.GetActiveScene().name == RequiredScene &&
               SessionStoryState.GetFlag(CompletionFlag) &&
               SessionStoryState.GetFlag(HandoffCompletionFlag) &&
               SessionStoryState.GetFlag(PresentationCompleteFlag) &&
               GetPresentationPhase() == InventoryClosedAfterViewer &&
               !SessionStoryState.GetFlag(Part2CompleteFlag);
    }

    private bool IsExpectedPart2TaskState(TaskManager manager)
    {
        return manager != null && manager.GetTaskState(NewTaskId) == TaskState.Active &&
               (manager.IsCurrentStage(NewTaskId, HandoffStageId) ||
                manager.IsCurrentStage(NewTaskId, DiscussionStageId));
    }

    private bool HasConflictingModalOrSequence(ConversationManager manager)
    {
        if (manager == null || manager.IsConversationActive ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            InventoryDocumentViewer.IsAnyOpen)
            return true;

        InventoryUI[] inventories = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include);
        for (int i = 0; i < inventories.Length; i++)
            if (inventories[i] != null && inventories[i].gameObject.scene == gameObject.scene && inventories[i].IsOpen)
                return true;

        ClarityDocumentViewer[] clarityViewers = FindObjectsByType<ClarityDocumentViewer>(FindObjectsInactive.Include);
        for (int i = 0; i < clarityViewers.Length; i++)
            if (clarityViewers[i] != null && clarityViewers[i].IsOpen)
                return true;

        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        if (journal != null && journal.IsOpen)
            return true;

        AlaalaPauseMenuController[] pauseMenus = FindObjectsByType<AlaalaPauseMenuController>(FindObjectsInactive.Include);
        for (int i = 0; i < pauseMenus.Length; i++)
            if (pauseMenus[i] != null && pauseMenus[i].IsOpen)
                return true;

        ItemAcquisitionPopupPresenter[] presenters = FindObjectsByType<ItemAcquisitionPopupPresenter>(FindObjectsInactive.Include);
        for (int i = 0; i < presenters.Length; i++)
            if (presenters[i] != null && presenters[i].IsPresentingOrQueued)
                return true;

        return false;
    }

    private int GetPresentationPhase()
    {
        return SessionStoryState.GetInt(PresentationPhaseKey);
    }

    private void RestoreStandingMaestroInteraction()
    {
        if (standingMaestroCollider != null && standingMaestro != null && standingMaestro.activeInHierarchy)
            standingMaestroCollider.enabled = true;
    }

    private ItemAcquisitionPopupPresenter FindAcquisitionPopupPresenter()
    {
        ItemAcquisitionPopupPresenter[] presenters =
            FindObjectsByType<ItemAcquisitionPopupPresenter>(FindObjectsInactive.Include);
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < presenters.Length; i++)
        {
            if (presenters[i] != null && presenters[i].gameObject.scene == activeScene)
                return presenters[i];
        }

        return presenters.Length > 0 ? presenters[0] : null;
    }

    private InventoryUI FindInventoryUI()
    {
        InventoryUI[] inventoryUIs = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include);
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < inventoryUIs.Length; i++)
        {
            if (inventoryUIs[i] != null && inventoryUIs[i].gameObject.scene == activeScene)
                return inventoryUIs[i];
        }

        return inventoryUIs.Length > 0 ? inventoryUIs[0] : null;
    }

    private bool IsRewardCallbackCurrent(int callbackGeneration)
    {
        return this != null && isActiveAndEnabled &&
               callbackGeneration == rewardCallbackGeneration &&
               SceneManager.GetActiveScene().name == RequiredScene &&
               SessionStoryState.GetFlag(HandoffCompletionFlag) &&
               handoffItem != null;
    }

    private void AbortSequence()
    {
        if (!sequenceRunning)
        {
            ReleaseStoryToken();
            return;
        }

        if (conversationStarted && conversationManager != null &&
            conversationManager.ActiveConversation == returnConversation)
        {
            conversationManager.EndConversation();
        }
        sequenceRunning = false;
        ApplyPart2Presentation();
        ReleaseStoryToken();
    }

    private void UnsubscribeFromConversationClose()
    {
        if (!subscribedToConversationClose)
            return;

        ConversationManager.ConversationClosed -= HandleConversationClosed;
        subscribedToConversationClose = false;
    }

    private void ReleaseStoryToken()
    {
        if (storyToken == null)
            return;

        storyToken.Release();
        storyToken = null;
    }

    private void OnDisable()
    {
        if (journalUnlockRecoveryRoutine != null)
        {
            StopCoroutine(journalUnlockRecoveryRoutine);
            journalUnlockRecoveryRoutine = null;
        }
        rewardCallbackGeneration++;
        handoffRewardRoutineRunning = false;
        UnbindPresentationEvents();
        if (finalConversationStarted && ConversationManager.Instance != null &&
            ConversationManager.Instance.ActiveConversation == finalFollowUpConversation)
            ConversationManager.Instance.EndConversation();
        finalConversationStarted = false;
        RequestFinalConversationCameraReturn();
        finalConversationRoutineRunning = false;
        RestoreStandingMaestroInteraction();
        ReleaseStoryToken();
        UnsubscribeFromConversationClose();
        if (!completedSuccessfully)
            AbortSequence();
    }

    private void OnDestroy()
    {
        UnbindPresentationEvents();
        UnsubscribeFromConversationClose();
        if (!completedSuccessfully)
            AbortSequence();
        else
            ReleaseStoryToken();
    }
}

