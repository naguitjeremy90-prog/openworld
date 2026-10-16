using System.Collections;
using UnityEngine;
using DialogueEditor;

/// <summary>Stages the existing Miguel actor when Anday advances the main task to find_kutsero.</summary>
public sealed class MiguelFindKutseroTransition : MonoBehaviour
{
    private const float FadeToBlackDuration = 0.7f;
    private const float BlackHoldDuration = 0.4f;
    private const float FadeFromBlackDuration = 0.7f;
    private const string MainTaskId = "main_investigate_pili";
    private const string FindKutseroStageId = "find_kutsero";
    private const string CompletedFlag = "newmakamisa_miguel_find_kutsero_transition_complete";
    private const string KalesaCurrentDestinationState = KalesaTravelController.CurrentDestinationStateId;
    private const string KalesaPendingDestinationState = KalesaTravelController.PendingDestinationStateId;
    private const string KalesaDestinationFlagPrefix = KalesaTravelController.CurrentDestinationFlagPrefix;

    [SerializeField] private Transform miguelRoot;
    [SerializeField] private Rigidbody miguelRigidbody;
    [SerializeField] private MonoBehaviour miguelMovementBehaviour;
    [SerializeField] private Transform findKutseroSpawn;
    [SerializeField] private FadeController fadeController;

    private CanvasGroup fadeCanvasGroup;
    private Coroutine transitionRoutine;
    private StorySequenceToken storyToken;
    private bool previousMovementEnabled;
    private bool previousRigidbodyKinematic;
    private bool movementStateCaptured;
    private bool rigidbodyStateCaptured;
    private bool fadeRecoveryNeeded;
    private Vector3 miguelBodyRestingLocalPosition;
    private Quaternion miguelBodyRestingLocalRotation;
    private bool hasMiguelBodyRestingLocalPose;

    private void Awake()
    {
        if (miguelRoot != null && miguelRigidbody != null &&
            miguelRigidbody.transform != miguelRoot &&
            miguelRigidbody.transform.IsChildOf(miguelRoot))
        {
            miguelBodyRestingLocalPosition = miguelRigidbody.transform.localPosition;
            miguelBodyRestingLocalRotation = miguelRigidbody.transform.localRotation;
            hasMiguelBodyRestingLocalPose = true;
        }

        if (fadeController != null)
            fadeCanvasGroup = fadeController.GetComponent<CanvasGroup>();
    }

    /// <summary>Called after StoryFlagTaskCondition has attempted the stage advance.</summary>
    public void TryRun()
    {
        TaskManager manager = TaskManager.Instance;
        if (transitionRoutine != null || SessionStoryState.GetFlag(CompletedFlag) ||
            manager == null || manager.GetTaskState(MainTaskId) != TaskState.Active ||
            !manager.IsCurrentStage(MainTaskId, FindKutseroStageId))
            return;

        if (miguelRoot == null || miguelRigidbody == null ||
            miguelMovementBehaviour == null || findKutseroSpawn == null ||
            fadeController == null || !fadeController.isActiveAndEnabled ||
            fadeCanvasGroup == null)
        {
            Debug.LogError("Miguel find_kutsero transition is missing a required scene reference.", this);
            return;
        }

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("Miguel find_kutsero transition could not acquire story ownership.", this);
            return;
        }

        transitionRoutine = StartCoroutine(RunTransition());
    }

    private IEnumerator RunTransition()
    {
        bool relocationCompleted = false;
        try
        {
            previousMovementEnabled = miguelMovementBehaviour.enabled;
            movementStateCaptured = true;
            miguelMovementBehaviour.enabled = false;

            previousRigidbodyKinematic = miguelRigidbody.isKinematic;
            rigidbodyStateCaptured = true;
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
            miguelRigidbody.isKinematic = true;

            ConversationManager conversation = ConversationManager.Instance;
            while (conversation != null && conversation.isActiveAndEnabled &&
                   conversation.IsConversationActive)
            {
                conversation = ConversationManager.Instance;
                yield return null;
            }

            fadeRecoveryNeeded = true;
            yield return FadeToBlackForTransition();
            yield return null;

            TaskManager manager = TaskManager.Instance;
            if (manager == null || manager.GetTaskState(MainTaskId) != TaskState.Active ||
                !manager.IsCurrentStage(MainTaskId, FindKutseroStageId))
                yield break;

            miguelRoot.SetPositionAndRotation(
                findKutseroSpawn.position,
                findKutseroSpawn.rotation);

            if (hasMiguelBodyRestingLocalPose)
            {
                miguelRigidbody.transform.SetLocalPositionAndRotation(
                    miguelBodyRestingLocalPosition,
                    miguelBodyRestingLocalRotation);
            }

            miguelRigidbody.position = miguelRigidbody.transform.position;
            miguelRigidbody.rotation = miguelRigidbody.transform.rotation;
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();

            ClearStaleKalesaRestoreState();
            SessionStoryState.SetFlag(CompletedFlag, true);
            relocationCompleted = true;

            yield return new WaitForSeconds(BlackHoldDuration);
            yield return fadeController.FadeFromBlack(FadeFromBlackDuration);
            fadeRecoveryNeeded = false;
        }
        finally
        {
            RestoreActorAndReleaseSequence();
            RecoverFadeIfNeeded();

            transitionRoutine = null;
            if (!relocationCompleted)
                Debug.LogWarning("Miguel find_kutsero transition ended before relocation completed.", this);
        }
    }

    private IEnumerator FadeToBlackForTransition()
    {
        float elapsed = 0f;
        float startingAlpha = fadeCanvasGroup.alpha;

        while (elapsed < FadeToBlackDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(
                startingAlpha,
                1f,
                Mathf.Clamp01(elapsed / FadeToBlackDuration));
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    private static void ClearStaleKalesaRestoreState()
    {
        SessionStoryState.SetString(KalesaCurrentDestinationState, string.Empty);
        SessionStoryState.SetString(KalesaPendingDestinationState, string.Empty);
        SessionStoryState.SetFlag(KalesaDestinationFlagPrefix + "bungad_ng_bayan", false);
        SessionStoryState.SetFlag(KalesaDestinationFlagPrefix + "plaza", false);
        SessionStoryState.SetFlag(KalesaDestinationFlagPrefix + "dulo_ng_bayan", false);
    }

    private void RestoreActorAndReleaseSequence()
    {
        if (rigidbodyStateCaptured && miguelRigidbody != null)
        {
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
            miguelRigidbody.isKinematic = previousRigidbodyKinematic;
        }
        rigidbodyStateCaptured = false;

        if (movementStateCaptured && miguelMovementBehaviour != null)
            miguelMovementBehaviour.enabled = previousMovementEnabled;
        movementStateCaptured = false;

        storyToken?.Release();
        storyToken = null;
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        RestoreActorAndReleaseSequence();
        RecoverFadeIfNeeded();
    }

    private void RecoverFadeIfNeeded()
    {
        if (!fadeRecoveryNeeded || fadeController == null)
            return;

        fadeRecoveryNeeded = false;
        if (fadeController.gameObject.activeInHierarchy)
        {
            fadeController.StartCoroutine(fadeController.FadeFromBlack(FadeFromBlackDuration));
        }
    }
}
