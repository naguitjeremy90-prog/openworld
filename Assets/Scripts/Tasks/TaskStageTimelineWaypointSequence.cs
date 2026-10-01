using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Starts a Timeline when a player enters a trigger during a configured task stage,
/// then moves the player along authored Transform waypoints and holds the scene at
/// a final facing/camera presentation.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TaskStageTimelineWaypointSequence : MonoBehaviour
{
    private static readonly HashSet<string> completedRuntimeSequences =
        new HashSet<string>();

    private static readonly int MoveSpeedParameter =
        Animator.StringToHash("MoveSpeed");

    [Header("Task Gate")]
    [SerializeField] private string taskId;
    [SerializeField] private string requiredStageId;
    [SerializeField] private string runtimeSequenceId;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator playerAnimator;

    [Header("Waypoint Movement")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField, Min(0f)] private float movementSpeed = 2.5f;
    [SerializeField, Min(0.01f)] private float stoppingDistance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float walkingAnimationValue = 0.33f;

    [Header("Final Presentation")]
    [SerializeField] private Transform facingTarget;
    [SerializeField] private Transform characterToFacePlayer;
    [SerializeField] private Rigidbody characterToFacePlayerRigidbody;
    [SerializeField] private CinemachineCamera finalCamera;
    [SerializeField, Min(0f)] private int finalCameraPriority = 100;
    [SerializeField, Min(0f)] private float rotationSpeed = 180f;
    [SerializeField, Min(0.1f)] private float facingTolerance = 1f;

    private StorySequenceToken storyToken;
    private Coroutine sequenceRoutine;
    private bool playerInside;
    private bool sequenceRunning;
    private bool movementPhaseStarted;
    private bool retryRequiresExit;
    private bool stagedAtEndpoint;
    private bool previousMovementEnabled;
    private bool movementStateCaptured;
    private bool handoffSignalReceived;
    private bool suppressUnexpectedStop;
    private bool andayRotationCaptured;
    private Quaternion originalAndayRotation;
    private Unity.Cinemachine.PrioritySettings originalCameraPriority;
    private bool cameraPriorityCaptured;
    private bool originalCameraActiveState;
    private bool playerKinematicStateCaptured;
    private bool originalPlayerIsKinematic;
    private bool taskStageRejectionLogged;
    private bool storySequenceRejectionLogged;
    private bool conversationRejectionLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCompletions()
    {
        completedRuntimeSequences.Clear();
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        if (director != null)
            director.playOnAwake = false;

        ResolvePlayerReferences();

        if (characterToFacePlayer != null)
        {
            originalAndayRotation = characterToFacePlayer.rotation;
            andayRotationCaptured = true;
        }

        if (finalCamera != null)
        {
            originalCameraPriority = finalCamera.Priority;
            originalCameraActiveState = finalCamera.gameObject.activeSelf;
            cameraPriorityCaptured = true;
        }
    }

    private void OnEnable()
    {
        if (director != null)
            director.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        if (director != null)
            director.stopped -= HandleDirectorStopped;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = null;
        CleanupSequence(restoreCamera: true, restoreAndayFacing: true);
        playerInside = false;
        retryRequiresExit = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInside = true;
        Debug.Log("[AndaySequence] Player entered trigger.", this);
        TryBeginSequence();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInside = false;
        retryRequiresExit = false;
        taskStageRejectionLogged = false;
        storySequenceRejectionLogged = false;
        conversationRejectionLogged = false;
    }

    private void Update()
    {
        if (playerInside && !sequenceRunning && !stagedAtEndpoint && !retryRequiresExit)
            TryBeginSequence();
    }

    /// <summary>UnityEvent entry point called by the Timeline movement handoff signal.</summary>
    public void BeginMovementPhase()
    {
        if (!sequenceRunning || movementPhaseStarted || handoffSignalReceived)
            return;

        Debug.Log("[AndaySequence] Movement handoff received.", this);
        handoffSignalReceived = true;
        if (director != null && director.state == PlayState.Playing)
            director.Pause();

        sequenceRoutine = StartCoroutine(ContinueAfterTimelineHandoff());
    }

    private void TryBeginSequence()
    {
        if (!playerInside || sequenceRunning || stagedAtEndpoint || !isActiveAndEnabled)
            return;

        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            if (!storySequenceRejectionLogged)
            {
                Debug.Log("[AndaySequence] Another story sequence is active.", this);
                storySequenceRejectionLogged = true;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(runtimeSequenceId) ||
            completedRuntimeSequences.Contains(runtimeSequenceId))
        {
            return;
        }

        if (!IsRequiredTaskStageActive())
        {
            if (!taskStageRejectionLogged)
            {
                Debug.Log(
                    "[AndaySequence] Waiting for required task stage: " + taskId + " / " + requiredStageId + ".",
                    this);
                taskStageRejectionLogged = true;
            }
            return;
        }

        if (ConversationManager.Instance != null &&
            ConversationManager.Instance.IsConversationActive)
        {
            if (!conversationRejectionLogged)
            {
                Debug.Log("[AndaySequence] Conversation is active.", this);
                conversationRejectionLogged = true;
            }
            return;
        }

        ResolvePlayerReferences();
        string missingReferences = GetMissingReferences();
        if (!string.IsNullOrEmpty(missingReferences))
        {
            Debug.LogError(
                "[AndaySequence] Missing required references: " + missingReferences + ".",
                this);
            retryRequiresExit = true;
            return;
        }

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("[AndaySequence] Could not acquire the StorySequence token.", this);
            retryRequiresExit = true;
            return;
        }

        sequenceRunning = true;
        movementPhaseStarted = false;
        handoffSignalReceived = false;
        previousMovementEnabled = playerMovement.enabled;
        movementStateCaptured = true;
        playerMovement.enabled = false;
        StopHorizontalMotion(playerRigidbody);
        SetMovementAnimation(0f);

        taskStageRejectionLogged = false;
        storySequenceRejectionLogged = false;
        conversationRejectionLogged = false;
        Debug.Log("[AndaySequence] Sequence accepted. Starting TimelineAnday.", this);
        director.playOnAwake = false;
        suppressUnexpectedStop = false;
        director.Play();
        Debug.Log("[AndaySequence] Timeline started.", this);
    }

    private IEnumerator ContinueAfterTimelineHandoff()
    {
        // Let SignalReceiver finish dispatching before stopping the director graph.
        yield return null;

        if (!sequenceRunning || !isActiveAndEnabled)
            yield break;

        movementPhaseStarted = true;
        suppressUnexpectedStop = true;
        if (director != null && director.playableGraph.IsValid())
            director.Stop();
        suppressUnexpectedStop = false;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Debug.Log("[AndaySequence] Moving to " + waypoints[i].name + ".", this);
            BeginKinematicMovement();
            yield return MoveToWaypoint(waypoints[i]);
            Debug.Log("[AndaySequence] Reached " + waypoints[i].name + ".", this);
        }

        StopHorizontalMotion(playerRigidbody);
        SetMovementAnimation(0f);

        Debug.Log("[AndaySequence] Facing Anday.", this);
        yield return FaceEachOther();
        Debug.Log("[AndaySequence] Final facing complete.", this);

        StopHorizontalMotion(playerRigidbody);
        SetMovementAnimation(0f);
        RestorePlayerKinematicState();
        ActivateFinalCamera();

        stagedAtEndpoint = true;
        completedRuntimeSequences.Add(runtimeSequenceId);
        Debug.Log("[AndaySequence] Pre-dialogue staging complete.", this);

        // Keep the story token and player lock held. Dialogue and story completion
        // are intentionally added in a later pass.
        sequenceRoutine = null;
    }

    private IEnumerator MoveToWaypoint(Transform waypoint)
    {
        float speed = Mathf.Max(0f, movementSpeed);

        while (HorizontalDistance(player.position, waypoint.position) > stoppingDistance)
        {
            yield return new WaitForFixedUpdate();

            Vector3 currentPosition = player.position;
            Vector3 targetPosition = waypoint.position;
            targetPosition.y = currentPosition.y;
            Vector3 direction = targetPosition - currentPosition;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= stoppingDistance)
                break;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion movementFacing = Quaternion.LookRotation(direction);
                player.rotation = Quaternion.RotateTowards(
                    GetYawOnlyRotation(player.rotation),
                    movementFacing,
                    Mathf.Max(0f, rotationSpeed) * Time.fixedDeltaTime);
            }

            float travelDistance = Mathf.Min(
                speed * Time.fixedDeltaTime,
                Mathf.Max(0f, distance - stoppingDistance));
            Vector3 nextPosition = currentPosition + direction.normalized * travelDistance;
            playerRigidbody.MovePosition(nextPosition);
            SetMovementAnimation(speed > 0f ? walkingAnimationValue : 0f);
        }

        StopHorizontalMotion(playerRigidbody);
        SetMovementAnimation(0f);
    }

    private IEnumerator FaceEachOther()
    {
        Quaternion playerTargetRotation = GetFlatFacingRotation(
            player.position, facingTarget.position, player.rotation);
        Quaternion otherTargetRotation = GetFlatFacingRotation(
            characterToFacePlayer.position, player.position,
            characterToFacePlayer.rotation);

        float speed = Mathf.Max(0f, rotationSpeed);
        while (Quaternion.Angle(player.rotation, playerTargetRotation) > facingTolerance ||
               Quaternion.Angle(characterToFacePlayer.rotation, otherTargetRotation) > facingTolerance)
        {
            yield return new WaitForFixedUpdate();
            float turnStep = speed * Time.fixedDeltaTime;

            player.rotation = Quaternion.RotateTowards(
                GetYawOnlyRotation(player.rotation), playerTargetRotation, turnStep);

            if (characterToFacePlayerRigidbody != null)
            {
                characterToFacePlayerRigidbody.MoveRotation(Quaternion.RotateTowards(
                    characterToFacePlayer.rotation, otherTargetRotation, turnStep));
            }
            else
            {
                characterToFacePlayer.rotation = Quaternion.RotateTowards(
                    characterToFacePlayer.rotation, otherTargetRotation, turnStep);
            }

        }

        player.rotation = playerTargetRotation;
        if (characterToFacePlayerRigidbody != null)
            characterToFacePlayerRigidbody.MoveRotation(otherTargetRotation);
        else
            characterToFacePlayer.rotation = otherTargetRotation;
    }

    private bool IsRequiredTaskStageActive()
    {
        if (string.IsNullOrWhiteSpace(taskId) ||
            string.IsNullOrWhiteSpace(requiredStageId))
        {
            return false;
        }

        TaskManager manager = TaskManager.Instance;
        return manager != null &&
               manager.GetTaskState(taskId) == TaskState.Active &&
               manager.IsCurrentStage(taskId, requiredStageId);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (player != null)
            return other.transform == player || other.transform.IsChildOf(player);

        return other.CompareTag("Player") ||
               other.GetComponentInParent<SimpleSampleCharacterControl>() != null;
    }

    private bool HasMissingWaypoint()
    {
        for (int i = 0; i < waypoints.Length; i++)
            if (waypoints[i] == null)
                return true;

        return false;
    }

    private void ResolvePlayerReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
            return;

        if (playerMovement == null)
            playerMovement = player.GetComponent<SimpleSampleCharacterControl>();
        if (playerRigidbody == null)
            playerRigidbody = player.GetComponent<Rigidbody>();
        if (playerAnimator == null)
            playerAnimator = player.GetComponent<Animator>();
    }

    private void SetMovementAnimation(float value)
    {
        if (playerAnimator != null)
            playerAnimator.SetFloat(MoveSpeedParameter, value);
    }

    private static Quaternion GetFlatFacingRotation(
        Vector3 from, Vector3 to, Quaternion fallback)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude <= 0.0001f
            ? fallback
            : Quaternion.LookRotation(direction);
    }

    private static Quaternion GetYawOnlyRotation(Quaternion rotation)
    {
        return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static void StopHorizontalMotion(Rigidbody body)
    {
        if (body == null)
            return;

        Vector3 velocity = body.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private void ActivateFinalCamera()
    {
        if (finalCamera == null)
            return;

        finalCamera.gameObject.SetActive(true);
        Unity.Cinemachine.PrioritySettings priority = finalCamera.Priority;
        priority.Enabled = true;
        priority.Value = finalCameraPriority;
        finalCamera.Priority = priority;
        Debug.Log("[AndaySequence] AndayCM activated.", this);
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director || !sequenceRunning)
            return;

        if (movementPhaseStarted)
        {
            Debug.Log("[AndaySequence] Director stopped for the expected handoff.", this);
            return;
        }

        if (suppressUnexpectedStop)
        {
            Debug.Log("[AndaySequence] Director stopped during intentional cleanup.", this);
            return;
        }

        Debug.LogWarning("[AndaySequence] Director stopped unexpectedly before the handoff.", this);

        // A stop before the authored handoff is an interruption. Require the
        // player to leave and re-enter before attempting the sequence again.
        retryRequiresExit = true;
        CleanupSequence(restoreCamera: true, restoreAndayFacing: true);
    }

    private void CleanupSequence(bool restoreCamera, bool restoreAndayFacing)
    {
        if (director != null && director.playableGraph.IsValid())
        {
            suppressUnexpectedStop = true;
            director.Stop();
            suppressUnexpectedStop = false;
        }

        SetMovementAnimation(0f);
        StopHorizontalMotion(playerRigidbody);

        if (movementStateCaptured && playerMovement != null)
            playerMovement.enabled = previousMovementEnabled;

        RestorePlayerKinematicState();
        movementStateCaptured = false;
        sequenceRunning = false;
        movementPhaseStarted = false;
        handoffSignalReceived = false;
        stagedAtEndpoint = false;

        if (restoreCamera && cameraPriorityCaptured && finalCamera != null)
        {
            finalCamera.Priority = originalCameraPriority;
            finalCamera.gameObject.SetActive(originalCameraActiveState);
        }

        if (restoreAndayFacing && andayRotationCaptured && characterToFacePlayer != null)
        {
            if (characterToFacePlayerRigidbody != null)
                characterToFacePlayerRigidbody.MoveRotation(originalAndayRotation);
            else
                characterToFacePlayer.rotation = originalAndayRotation;
        }

        if (storyToken != null)
            storyToken.Release();
        storyToken = null;
    }

    private void BeginKinematicMovement()
    {
        if (playerRigidbody == null || playerKinematicStateCaptured)
            return;

        originalPlayerIsKinematic = playerRigidbody.isKinematic;
        playerKinematicStateCaptured = true;
        StopHorizontalMotion(playerRigidbody);
        playerRigidbody.isKinematic = true;
    }

    private void RestorePlayerKinematicState()
    {
        if (!playerKinematicStateCaptured || playerRigidbody == null)
            return;

        playerRigidbody.isKinematic = originalPlayerIsKinematic;
        playerKinematicStateCaptured = false;
        if (!originalPlayerIsKinematic)
            StopHorizontalMotion(playerRigidbody);
    }

    private string GetMissingReferences()
    {
        List<string> missing = new List<string>();
        if (director == null) missing.Add("PlayableDirector");
        else if (director.playableAsset == null) missing.Add("Timeline asset");
        if (player == null) missing.Add("player Transform");
        if (playerMovement == null) missing.Add("player movement controller");
        if (playerRigidbody == null) missing.Add("player Rigidbody");
        if (playerAnimator == null) missing.Add("player Animator");
        if (waypoints == null || waypoints.Length == 0) missing.Add("waypoints array");
        else
            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i] == null)
                    missing.Add("waypoint " + i);
        if (facingTarget == null) missing.Add("facing target");
        if (characterToFacePlayer == null) missing.Add("character to face player");
        if (finalCamera == null) missing.Add("final camera");
        return string.Join(", ", missing);
    }
}
