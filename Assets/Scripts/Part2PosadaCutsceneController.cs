using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;

/// <summary>
/// Runs the one-shot Part 2 return conversation in PosadaRoom.
/// The trigger owns only this presentation; task progression is handled separately.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class Part2PosadaCutsceneController : MonoBehaviour
{
    public const string CompletionFlag = "posada_part2_intro_cutscene_complete";
    private const string Part1ReturnPrerequisiteFlag = "makamisa_part1_return_transport_started";
    private const string PostCutsceneTaskId = "main_visit_maestro_ben";

    [Header("Player and trigger")]
    [SerializeField] private BoxCollider triggerCollider;
    [SerializeField] private Collider miguelCollider;
    [SerializeField] private Transform miguelRoot;

    [Header("Sequence references")]
    [SerializeField] private Transform part2Spawn;
    [SerializeField] private Camera roomCamera;
    [SerializeField] private FadeController fadeController;
    [SerializeField] private ConversationManager conversationManager;
    [SerializeField] private GameObject padrePrefab;
    [SerializeField] private GameObject alingSelaPrefab;
    [SerializeField] private NPCConversation padreConversation;
    [SerializeField] private NPCConversation alingSelaConversation;
    [SerializeField] private AudioSource appearanceAudioSource;
    [SerializeField] private AudioClip padreAppearSfx;
    [SerializeField] private AudioClip alingSelaAppearSfx;

    [Header("NPC turning")]
    [SerializeField, Min(0.01f)] private float turnDuration = 0.45f;
    [SerializeField, Min(0f)] private float exitTurnHoldDuration = 0.5f;

    private StorySequenceToken storyToken;
    private Coroutine sequenceRoutine;
    private GameObject spawnedNpc;
    private bool playerInside;
    private bool pendingStart;
    private bool running;
    private bool completed;
    private bool requireExitToRetry;
    private bool openingFadeStarted;
    private bool cleanupInProgress;

    private void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();
        if (appearanceAudioSource == null)
            appearanceAudioSource = GetComponent<AudioSource>();

        completed = SessionStoryState.GetFlag(CompletionFlag);
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
        if (completed && triggerCollider != null)
            triggerCollider.enabled = false;
    }

    private void OnEnable()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnDisable()
    {
        AbortAndCleanup();
    }

    private void OnDestroy()
    {
        AbortAndCleanup();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsMiguel(other))
            return;

        playerInside = true;
        if (!SessionStoryState.GetFlag(Part1ReturnPrerequisiteFlag))
        {
            pendingStart = false;
            return;
        }

        if (!completed && !requireExitToRetry)
            pendingStart = true;
        TryBeginSequence();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsMiguel(other))
            return;

        playerInside = false;
        pendingStart = false;
        requireExitToRetry = false;
    }

    private void Update()
    {
        if (pendingStart && playerInside && !running && !completed && !requireExitToRetry)
            TryBeginSequence();
    }

    private bool IsMiguel(Collider other)
    {
        if (other == null || miguelCollider == null || miguelRoot == null || other != miguelCollider)
            return false;

        Rigidbody expectedBody = miguelCollider.attachedRigidbody;
        Rigidbody enteringBody = other.attachedRigidbody;
        if (expectedBody != null && enteringBody != expectedBody)
            return false;

        return miguelRoot.CompareTag("Player") ||
               (expectedBody != null && expectedBody.CompareTag("Player"));
    }

    private void TryBeginSequence()
    {
        if (!SessionStoryState.GetFlag(Part1ReturnPrerequisiteFlag))
        {
            pendingStart = false;
            return;
        }

        if (!pendingStart || !playerInside || running || completed ||
            !ReferencesAreValid() || StorySequenceCoordinator.IsStorySequenceActive ||
            (conversationManager != null && conversationManager.IsConversationActive))
            return;

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null || !storyToken.IsValid)
        {
            storyToken = null;
            return;
        }

        pendingStart = false;
        running = true;
        sequenceRoutine = StartCoroutine(RunSequence());
    }

    private bool ReferencesAreValid()
    {
        return triggerCollider != null && miguelCollider != null && miguelRoot != null &&
               part2Spawn != null && roomCamera != null && fadeController != null &&
               fadeController.isActiveAndEnabled && conversationManager != null &&
               conversationManager.isActiveAndEnabled && padrePrefab != null &&
               alingSelaPrefab != null && padreConversation != null &&
               alingSelaConversation != null;
    }

    private IEnumerator RunSequence()
    {
        bool sequenceCompleted = false;
        try
        {
            openingFadeStarted = true;
            yield return fadeController.FadeToBlack();
            yield return new WaitForSeconds(1f);
            yield return fadeController.FadeFromBlack();
            openingFadeStarted = false;

            spawnedNpc = SpawnFacingMiguel(padrePrefab, padreAppearSfx);
            if (spawnedNpc == null)
                yield break;

            yield return new WaitForSeconds(1f);
            bool padreClosedNormally = false;
            yield return StartConversationAndWait(padreConversation, result => padreClosedNormally = result);
            if (!padreClosedNormally)
                yield break;

            yield return new WaitForSeconds(1f);
            yield return TurnTowardScreenRight(spawnedNpc.transform);
            if (exitTurnHoldDuration > 0f)
                yield return new WaitForSeconds(exitTurnHoldDuration);
            DestroySpawnedNpc();
            yield return new WaitForSeconds(1f);

            spawnedNpc = SpawnFacingMiguel(alingSelaPrefab, alingSelaAppearSfx);
            if (spawnedNpc == null)
                yield break;

            yield return new WaitForSeconds(1f);
            bool alingSelaClosedNormally = false;
            yield return StartConversationAndWait(
                alingSelaConversation,
                result => alingSelaClosedNormally = result);
            if (!alingSelaClosedNormally)
                yield break;

            yield return new WaitForSeconds(1f);
            yield return TurnTowardScreenRight(spawnedNpc.transform);
            if (exitTurnHoldDuration > 0f)
                yield return new WaitForSeconds(exitTurnHoldDuration);
            DestroySpawnedNpc();
            yield return new WaitForSeconds(1f);

            SessionStoryState.SetFlag(CompletionFlag, true);
            completed = SessionStoryState.GetFlag(CompletionFlag);
            if (completed)
            {
                StartPostCutsceneTask();
                ReconstructionJournalManager.Instance?.UnlockReflection("anday_story_reflection");
            }
            if (completed && triggerCollider != null)
                triggerCollider.enabled = false;
            sequenceCompleted = completed;
        }
        finally
        {
            sequenceRoutine = null;
            running = false;
            if (!sequenceCompleted && playerInside)
                requireExitToRetry = true;
            CleanupOwnedState(!sequenceCompleted);
        }
    }

    private void StartPostCutsceneTask()
    {
        TaskManager taskManager = TaskManager.Instance;
        if (taskManager == null)
        {
            Debug.LogError(
                "The Posada Part 2 cutscene completed, but TaskManager is unavailable; " +
                "the Maestro Ben task could not start.",
                this);
            return;
        }

        bool started = taskManager.StartTask(PostCutsceneTaskId);
        if (!started && taskManager.GetTaskState(PostCutsceneTaskId) == TaskState.Inactive)
        {
            Debug.LogError(
                "The Posada Part 2 cutscene completed, but main_visit_maestro_ben " +
                "could not start.",
                this);
        }
    }

    private GameObject SpawnFacingMiguel(GameObject prefab, AudioClip appearanceSfx)
    {
        if (prefab == null || part2Spawn == null || miguelRoot == null)
            return null;

        GameObject instance = Instantiate(
            prefab,
            part2Spawn.position,
            part2Spawn.rotation);
        if (appearanceSfx != null && appearanceAudioSource != null)
            appearanceAudioSource.PlayOneShot(appearanceSfx);
        FaceMiguel(instance.transform);
        return instance;
    }

    private void FaceMiguel(Transform npcRoot)
    {
        Vector3 direction = miguelRoot.position - npcRoot.position;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude > 0.0001f)
            npcRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private IEnumerator TurnTowardScreenRight(Transform npcRoot)
    {
        if (npcRoot == null || roomCamera == null)
            yield break;

        Vector3 screenRight = Vector3.ProjectOnPlane(roomCamera.transform.right, Vector3.up);
        if (screenRight.sqrMagnitude < 0.0001f)
            yield break;

        Quaternion start = npcRoot.rotation;
        Quaternion target = Quaternion.LookRotation(screenRight.normalized, Vector3.up);
        float elapsed = 0f;
        while (elapsed < turnDuration && npcRoot != null)
        {
            elapsed += Time.deltaTime;
            npcRoot.rotation = Quaternion.Slerp(
                start,
                target,
                Mathf.SmoothStep(0f, 1f, elapsed / turnDuration));
            yield return null;
        }

        if (npcRoot != null)
            npcRoot.rotation = target;
    }

    private IEnumerator StartConversationAndWait(
        NPCConversation conversation,
        Action<bool> onFinished)
    {
        ConversationManager manager = conversationManager;
        if (manager == null || conversation == null || manager.IsConversationActive)
        {
            onFinished?.Invoke(false);
            yield break;
        }

        bool started = false;
        bool closed = false;
        bool closedNormally = false;
        ConversationManager.ConversationStartEvent startedHandler = () =>
        {
            if (manager.ActiveConversation == conversation)
                started = true;
        };
        Action<NPCConversation, bool> closedHandler = (closedConversation, normal) =>
        {
            if (closedConversation != conversation)
                return;
            closed = true;
            closedNormally = normal;
        };

        ConversationManager.OnConversationStarted += startedHandler;
        ConversationManager.ConversationClosed += closedHandler;
        try
        {
            manager.StartConversation(conversation);

            float startDeadline = Time.realtimeSinceStartup + 2f;
            while (!started && !closed && Time.realtimeSinceStartup < startDeadline)
                yield return null;

            if (!started)
            {
                onFinished?.Invoke(false);
                yield break;
            }

            while (!closed && manager != null && manager.isActiveAndEnabled)
                yield return null;

            onFinished?.Invoke(closed && closedNormally);
        }
        finally
        {
            ConversationManager.OnConversationStarted -= startedHandler;
            ConversationManager.ConversationClosed -= closedHandler;
        }
    }

    private void DestroySpawnedNpc()
    {
        if (spawnedNpc == null)
            return;

        Destroy(spawnedNpc);
        spawnedNpc = null;
    }

    private void AbortAndCleanup()
    {
        if (cleanupInProgress)
            return;

        cleanupInProgress = true;
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        running = false;
        if (!completed && playerInside)
            requireExitToRetry = true;
        CleanupOwnedState(!completed);
        cleanupInProgress = false;
    }

    private void CleanupOwnedState(bool recoverFade)
    {
        DestroySpawnedNpc();

        if (recoverFade && openingFadeStarted &&
            fadeController != null && fadeController.isActiveAndEnabled)
        {
            fadeController.StartCoroutine(fadeController.FadeFromBlack());
        }
        openingFadeStarted = false;

        if (storyToken != null)
        {
            storyToken.Release();
            storyToken = null;
        }
    }
}
