using System.Collections;
using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;

/// <summary>
/// Reusable composition for a story boundary that presents dialogue, a Miguel
/// reaction, and a short controlled walk back to a safe point.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class StoryBoundarySequenceController : MonoBehaviour
{
    [Header("First-time dialogue")]
    [SerializeField] private NPCConversation firstConversation;
    [SerializeField] private SelfDialogueTrigger firstReaction;

    [Header("Repeat dialogue")]
    [SerializeField] private SelfDialogueTrigger repeatReaction;

    [Header("Story state")]
    [SerializeField] private string firstSequenceCompletedFlag =
        "present_church_boundary_intro_seen";

    [Header("Player return")]
    [SerializeField] private Transform returnPoint;
    [SerializeField] private Transform player;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField, Min(0f)] private float walkSpeed = 2f;
    [SerializeField, Min(0f)] private float turnSpeed = 240f;
    [SerializeField, Min(0.01f)] private float arrivalThreshold = 0.05f;

    private StorySequenceToken storyToken;
    private Coroutine sequenceRoutine;
    private bool playerInside;
    private bool running;
    private bool movementCaptured;
    private bool previousMovementEnabled;
    private bool phaseSucceeded;

    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");

    private void Awake()
    {
        Collider boundary = GetComponent<Collider>();
        boundary.isTrigger = true;

        ResolvePlayerReferences();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        TryBeginSequence();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    private void TryBeginSequence()
    {
        if (!playerInside || running || StorySequenceCoordinator.IsStorySequenceActive ||
            ConversationManager.Instance == null ||
            ConversationManager.Instance.IsConversationActive)
            return;

        if (returnPoint == null)
        {
            Debug.LogError("Story boundary has no ReturnPoint Transform.", this);
            return;
        }

        ResolvePlayerReferences();
        if (player == null || playerMovement == null || playerRigidbody == null ||
            playerAnimator == null)
        {
            Debug.LogError(
                "Story boundary needs the Player Transform, SimpleSampleCharacterControl, " +
                "Rigidbody, and Animator.", this);
            return;
        }

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("Story boundary could not acquire story ownership.", this);
            return;
        }

        running = true;
        sequenceRoutine = StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        bool firstSequence = !SessionStoryState.GetFlag(firstSequenceCompletedFlag);
        try
        {
            if (firstSequence)
            {
                phaseSucceeded = false;
                yield return StartConversationAndWait(firstConversation);
                if (!phaseSucceeded)
                    yield break;

                phaseSucceeded = false;
                yield return StartSelfDialogueAndWait(firstReaction);
                if (!phaseSucceeded)
                    yield break;

                SessionStoryState.SetFlag(firstSequenceCompletedFlag, true);
            }
            else
            {
                phaseSucceeded = false;
                yield return StartSelfDialogueAndWait(repeatReaction);
                if (!phaseSucceeded)
                    yield break;
            }

            yield return ReturnPlayerToPoint();
        }
        finally
        {
            CleanupSequence();
        }
    }

    private IEnumerator StartConversationAndWait(NPCConversation conversation)
    {
        if (conversation == null || ConversationManager.Instance == null)
        {
            Debug.LogError("Story boundary is missing its first NPCConversation.", this);
            phaseSucceeded = false;
            yield break;
        }

        bool started = false;
        ConversationManager.ConversationStartEvent startedHandler = () => started = true;
        ConversationManager.OnConversationStarted += startedHandler;
        try
        {
            ConversationManager.Instance.StartConversation(conversation);
        }
        finally
        {
            ConversationManager.OnConversationStarted -= startedHandler;
        }

        float timeout = 1f;
        while (!started && !ConversationManager.Instance.IsConversationActive && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!started && !ConversationManager.Instance.IsConversationActive)
        {
            Debug.LogError("Story boundary could not start its first conversation.", this);
            phaseSucceeded = false;
            yield break;
        }

        while (ConversationManager.Instance != null &&
               ConversationManager.Instance.IsConversationActive)
            yield return null;

        phaseSucceeded = true;
    }

    private IEnumerator StartSelfDialogueAndWait(SelfDialogueTrigger trigger)
    {
        if (trigger == null || ConversationManager.Instance == null)
        {
            Debug.LogError("Story boundary is missing a SelfDialogueTrigger.", this);
            phaseSucceeded = false;
            yield break;
        }

        trigger.enabled = true;
        bool started = false;
        ConversationManager.ConversationStartEvent startedHandler = () => started = true;
        ConversationManager.OnConversationStarted += startedHandler;
        try
        {
            trigger.StartSelfDialogue();
        }
        finally
        {
            ConversationManager.OnConversationStarted -= startedHandler;
        }

        float timeout = 1f;
        while (!started && !ConversationManager.Instance.IsConversationActive && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!started && !ConversationManager.Instance.IsConversationActive)
        {
            Debug.LogError("Story boundary could not start its Miguel reaction.", this);
            phaseSucceeded = false;
            yield break;
        }

        while (ConversationManager.Instance != null &&
               ConversationManager.Instance.IsConversationActive)
            yield return null;

        phaseSucceeded = true;
    }

    private IEnumerator ReturnPlayerToPoint()
    {
        if (returnPoint == null || player == null || playerRigidbody == null)
        {
            Debug.LogError("Story boundary cannot perform its return movement.", this);
            yield break;
        }

        CapturePlayerMovementState();
        playerMovement.enabled = false;
        SetMoveSpeed(0f);

        Vector3 horizontalDirection = returnPoint.position - player.position;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
            while (Quaternion.Angle(player.rotation, targetRotation) > 0.5f)
            {
                player.rotation = Quaternion.RotateTowards(
                    player.rotation,
                    targetRotation,
                    Mathf.Max(0f, turnSpeed) * Time.unscaledDeltaTime);
                yield return null;
            }

            player.rotation = targetRotation;
        }

        float speed = Mathf.Max(0f, walkSpeed);
        while (HorizontalDistance(player.position, returnPoint.position) > arrivalThreshold)
        {
            Vector3 targetPosition = returnPoint.position;
            targetPosition.y = player.position.y;
            Vector3 nextPosition = Vector3.MoveTowards(
                player.position,
                targetPosition,
                speed * Time.unscaledDeltaTime);

            playerRigidbody.MovePosition(nextPosition);
            SetMoveSpeed(speed > 0f ? 1f : 0f);
            yield return null;
        }

        playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);
        SetMoveSpeed(0f);
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

    private void CapturePlayerMovementState()
    {
        if (movementCaptured)
            return;

        previousMovementEnabled = playerMovement != null && playerMovement.enabled;
        movementCaptured = true;
    }

    private void SetMoveSpeed(float value)
    {
        if (playerAnimator != null)
            playerAnimator.SetFloat(MoveSpeed, value);
    }

    private void CleanupSequence()
    {
        SetMoveSpeed(0f);

        if (playerRigidbody != null)
            playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);

        if (movementCaptured && playerMovement != null)
            playerMovement.enabled = previousMovementEnabled;

        movementCaptured = false;
        running = false;
        sequenceRoutine = null;

        if (storyToken != null)
            storyToken.Release();
        storyToken = null;
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = null;
        CleanupSequence();
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
