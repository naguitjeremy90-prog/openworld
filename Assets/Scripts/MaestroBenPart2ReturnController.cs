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
    private const string CompletionFlag = "maestro_ben_part2_return_conversation_complete";

    [Header("Scene References")]
    [SerializeField] private GameObject originalMaestro;
    [SerializeField] private GameObject standingMaestro;
    [SerializeField] private CapsuleCollider standingMaestroCollider;
    [SerializeField] private GameObject standingMaestroInteraction;
    [SerializeField] private Transform miguelRoot;
    [SerializeField] private NPCConversation returnConversation;

    private StorySequenceToken storyToken;
    private ConversationManager conversationManager;
    private bool subscribedToConversationClose;
    private bool sequenceRunning;
    private bool conversationStarted;
    private bool closeReceived;
    private bool closeWasNormal;
    private bool completedSuccessfully;
    private NPCConversationTrigger standingInteractionTrigger;

    private void Start()
    {
        StartCoroutine(WaitForSceneReadiness());
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
            yield break;

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

            ConversationManager.ConversationClosed += HandleConversationClosed;
            subscribedToConversationClose = true;
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
        if (!conversationStarted || closedConversation != returnConversation)
            return;

        closeWasNormal = normal;
        closeReceived = true;
        UnsubscribeFromConversationClose();
    }

    private void AbortSequence()
    {
        if (!sequenceRunning)
        {
            ReleaseStoryToken();
            return;
        }

        UnsubscribeFromConversationClose();
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
        if (!completedSuccessfully)
            AbortSequence();
    }

    private void OnDestroy()
    {
        UnsubscribeFromConversationClose();
        if (!completedSuccessfully)
            AbortSequence();
        else
            ReleaseStoryToken();
    }
}

