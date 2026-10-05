using System.Collections;
using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;

/// <summary>Loads Anday's recollection after her return conversation fully closes.</summary>
public sealed class AndayRecollectionEntryController : MonoBehaviour
{
    public const string StartedFlag = "makamisa_anday_recollection_started";
    public const string CompleteFlag = "makamisa_anday_recollection_complete";
    private const string PostRecollectionPayoffFlag =
        "makamisa_anday_post_recollection_payoff_applied";

    private const string DestinationScene = "ANDAYRECOLLECTION";
    private const string RecollectionReturnPoint = "AndayRecollectionReturnPoint";
    private const string MainInvestigationTaskId = "main_investigate_pili";
    private const string AndayPersonId = "anday";
    private const string BinasbasangBataObservationId = "hindi_binasbasang_bata";
    private const float GroundingSettleTimeout = 2f;

    [SerializeField] private NPCConversationTrigger conversationTrigger;
    [SerializeField] private NPCConversation recollectionConversation;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private NPCConversation postRecollectionConversation;
    [SerializeField] private CameraFocusManager postRecollectionFocusManager;
    [SerializeField] private CameraFocusPoint postRecollectionFocusPoint;
    [SerializeField] private Part1ReturnTransportController part1ReturnTransportController;

    private Coroutine transitionRoutine;
    private Coroutine postRecollectionRoutine;
    private StorySequenceToken storyToken;
    private StorySequenceToken postRecollectionStoryToken;
    private bool previousMovementEnabled;
    private bool movementStateCaptured;
    private bool transitionHandedOff;
    private bool returningFromRecollection;
    private bool postRecollectionMovementEnabled;
    private bool postRecollectionMovementCaptured;
    private bool postRecollectionGroundingStabilizing;

    private void Awake()
    {
        returningFromRecollection = string.Equals(
            SpawnData.spawnPointName,
            RecollectionReturnPoint,
            System.StringComparison.Ordinal);

        if (conversationTrigger == null)
            conversationTrigger = FindAnyObjectByType<NPCConversationTrigger>();

        if (returningFromRecollection)
        {
            postRecollectionStoryToken = StorySequenceCoordinator.Acquire(this);
            if (playerMovement != null)
            {
                postRecollectionMovementEnabled = playerMovement.enabled;
                postRecollectionMovementCaptured = true;
            }

            // Keep the movement component available to process physics and
            // ground its Animator while the story token suppresses input.
            postRecollectionGroundingStabilizing = true;
        }
    }

    private void Start()
    {
        if (returningFromRecollection)
            postRecollectionRoutine = StartCoroutine(PlayPostRecollectionConversation());
    }

    private void OnEnable()
    {
        if (conversationTrigger != null)
            conversationTrigger.ConversationFinished += HandleConversationFinished;
    }

    private void OnDisable()
    {
        if (conversationTrigger != null)
            conversationTrigger.ConversationFinished -= HandleConversationFinished;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
        transitionRoutine = null;

        if (postRecollectionRoutine != null)
            StopCoroutine(postRecollectionRoutine);
        postRecollectionRoutine = null;

        if (!transitionHandedOff)
        {
            RestoreMovement();
            storyToken?.Release();
        }
        storyToken = null;

        returningFromRecollection = false;
        RestorePostRecollectionMovement();
        postRecollectionStoryToken?.Release();
        postRecollectionStoryToken = null;
    }

    private void LateUpdate()
    {
        // NPCConversationTrigger and DialogueMovementLock receive the same
        // conversation-ended event. Reassert this short transition lock after
        // their listeners release their own locks.
        if (playerMovement != null &&
            (transitionRoutine != null ||
             (returningFromRecollection && !postRecollectionGroundingStabilizing)))
            playerMovement.enabled = false;
    }

    private void HandleConversationFinished()
    {
        if (transitionRoutine != null || conversationTrigger == null ||
            conversationTrigger.LastStartedConversation != recollectionConversation ||
            SessionStoryState.GetFlag(StartedFlag) ||
            SessionStoryState.GetFlag(CompleteFlag))
            return;

        transitionRoutine = StartCoroutine(WaitForConversationAndLoad());
    }

    private IEnumerator WaitForConversationAndLoad()
    {
        storyToken = StorySequenceCoordinator.Acquire(this);
        if (playerMovement != null)
        {
            previousMovementEnabled = playerMovement.enabled;
            movementStateCaptured = true;
            playerMovement.enabled = false;
        }

        ConversationManager manager = ConversationManager.Instance;
        while (manager != null && manager.isActiveAndEnabled && manager.IsConversationActive)
            yield return null;

        SessionStoryState.SetFlag(StartedFlag, true);
        IrisTransitionController transition = GetOrCreateTransitionController();
        Coroutine handoff = transition != null
            ? transition.TransitionToScene(DestinationScene, storyToken)
            : null;

        if (handoff == null)
        {
            SessionStoryState.SetFlag(StartedFlag, false);
            Debug.LogError("Anday recollection could not start its scene transition.", this);
            transitionRoutine = null;
            RestoreMovement();
            storyToken?.Release();
            storyToken = null;
            yield break;
        }

        transitionHandedOff = true;
        storyToken = null; // IrisTransitionController releases the token after opening.
        yield return handoff;
    }

    private IEnumerator PlayPostRecollectionConversation()
    {
        // PlayerSpawner places Miguel during Start and the Kalesa controller's
        // saved-stop restore completes after one frame. Wait an extra frame so
        // both startup routines finish before focusing the camera or speaking.
        yield return null;
        yield return null;

        IrisTransitionController transition = IrisTransitionController.Instance;
        while (transition != null && transition.IsCovered)
            yield return null;

        yield return StabilizeReturnedPlayerGrounding();
        postRecollectionGroundingStabilizing = false;
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (conversationTrigger == null || postRecollectionConversation == null ||
            postRecollectionFocusManager == null || postRecollectionFocusPoint == null ||
            !conversationTrigger.OwnsCameraFocus(
                postRecollectionFocusManager, postRecollectionFocusPoint))
        {
            Debug.LogError("Anday's post-recollection conversation is missing its existing conversation or focus references.", this);
            CompletePostRecollectionPresentation();
            yield break;
        }

        while (postRecollectionFocusManager.IsFocusing ||
               (ConversationManager.Instance != null &&
                ConversationManager.Instance.IsConversationActive))
        {
            yield return null;
        }

        if (!conversationTrigger.StartConversationProgrammatically(postRecollectionConversation))
        {
            Debug.LogError("Anday's post-recollection conversation could not start.", this);
            CompletePostRecollectionPresentation();
            yield break;
        }

        ConversationManager manager = ConversationManager.Instance;
        while (manager != null && manager.IsConversationActive)
            yield return null;

        // NPCConversationTrigger returns its assigned focus camera when the
        // final manually advanced line ends. Keep player control held through
        // that existing camera blend.
        while (postRecollectionFocusManager != null &&
               postRecollectionFocusManager.IsFocusing)
        {
            yield return null;
        }

        ApplyPostRecollectionPayoff();
        CompletePostRecollectionPresentation();
    }

    private IEnumerator StabilizeReturnedPlayerGrounding()
    {
        // PlayerSpawner places Miguel in Start and holds the movement component
        // until the incoming iris opens. Do not check grounding before that.
        SimpleSampleCharacterControl characterController =
            playerMovement as SimpleSampleCharacterControl;
        float deadline = Time.realtimeSinceStartup + GroundingSettleTimeout;
        bool isGrounded = false;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (playerMovement == null || !playerMovement.enabled)
            {
                yield return null;
                continue;
            }

            // Let physics produce collision callbacks, then let the controller's
            // story-sequence FixedUpdate synchronize the grounded Animator state.
            yield return new WaitForFixedUpdate();
            if (characterController != null && characterController.DebugIsGrounded)
            {
                isGrounded = true;
                break;
            }
        }

        if (isGrounded)
        {
            // Give the Animator a further fixed/update step to complete its
            // normal grounded transition before Anday's focus shot begins.
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        else
        {
            Debug.LogWarning(
                "Miguel did not report grounded before Anday's post-recollection dialogue; continuing after the settle timeout.",
                this);
        }
    }

    private void ApplyPostRecollectionPayoff()
    {
        if (SessionStoryState.GetFlag(PostRecollectionPayoffFlag))
            return;

        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        if (journal != null)
        {
            journal.UnlockPerson(AndayPersonId);
            journal.UnlockObservation(BinasbasangBataObservationId);
        }
        else
        {
            Debug.LogError(
                "Anday's post-recollection payoff could not update the Tala-arawan because its manager is missing.",
                this);
        }

        TaskManager.Instance?.CompleteTask(MainInvestigationTaskId);
        SessionStoryState.SetFlag(PostRecollectionPayoffFlag, true);
    }

    private void CompletePostRecollectionPresentation()
    {
        bool completedRecollectionReturn = returningFromRecollection;
        returningFromRecollection = false;
        RestorePostRecollectionMovement();
        postRecollectionStoryToken?.Release();
        postRecollectionStoryToken = null;
        postRecollectionRoutine = null;

        if (!completedRecollectionReturn ||
            !SessionStoryState.GetFlag(PostRecollectionPayoffFlag))
            return;

        if (part1ReturnTransportController == null)
            part1ReturnTransportController =
                FindAnyObjectByType<Part1ReturnTransportController>();

        part1ReturnTransportController?.BeginAfterAndayPayoff();
    }

    private void RestorePostRecollectionMovement()
    {
        if (postRecollectionMovementCaptured && playerMovement != null)
            playerMovement.enabled = postRecollectionMovementEnabled;
        postRecollectionMovementCaptured = false;
    }

    private static IrisTransitionController GetOrCreateTransitionController()
    {
        IrisTransitionController transition = IrisTransitionController.Instance;
        if (transition == null)
            transition = FindAnyObjectByType<IrisTransitionController>();
        if (transition == null)
            transition = new GameObject("IrisTransitionController")
                .AddComponent<IrisTransitionController>();
        return transition;
    }

    private void RestoreMovement()
    {
        if (movementStateCaptured && playerMovement != null)
            playerMovement.enabled = previousMovementEnabled;
        movementStateCaptured = false;
    }
}
