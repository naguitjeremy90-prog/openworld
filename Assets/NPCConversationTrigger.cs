using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using DialogueEditor;

public class NPCConversationTrigger : MonoBehaviour
{
    private static readonly HashSet<NPCConversationTrigger> nearbyTriggers =
        new HashSet<NPCConversationTrigger>();
    private static NPCConversationTrigger activeConversationTrigger;

    public event System.Action ConversationFinished;
    [Header("Dialogue")]
    [FormerlySerializedAs("myConversation")]
    [SerializeField] private NPCConversation firstConversation;
    [SerializeField] private NPCConversation repeatConversation;
    [SerializeField] private GameObject talkText;

    [Header("Story State (Optional)")]
    [SerializeField] private string firstConversationCompletedFlagId;

    [Header("Camera Focus (Optional)")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;

    [Header("Presentation (Optional)")]
    [SerializeField] private bool treatAsStorySequence;

    [Header("Face Player (Optional)")]
    [SerializeField] private bool facePlayerWhenTalking = true;
    [SerializeField] private Transform npcTransform;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private bool returnToOriginalDirection = true;

    [Header("Walking NPC (Optional)")]
    [SerializeField] private NPCPatrol linkedPatrol;
    [SerializeField] private MonoBehaviour linkedConversationMovement;

    [Header("Interaction Availability (Optional)")]
    [SerializeField] private MonoBehaviour availabilityCondition;

    [Header("After Conversation")]
    public UnityEvent OnConversationFinished = new UnityEvent();
    public UnityEvent OnFirstConversationFinished = new UnityEvent();

    private bool playerNear = false;
    private bool isTalking = false;
    private bool hasCompletedFirstConversation = false;
    private bool currentConversationCountsAsFirst;
    private INPCConversationMovement pausedMovement;
    private StoryConversationSelector storyConversationSelector;
    private TaskStageConversationSelector taskStageConversationSelector;

    private Quaternion originalRotation;
    private Coroutine turnCoroutine;
    private StorySequenceToken storySequenceToken;
    private readonly UnityEvent storyFocusFinishedEvent = new UnityEvent();

    /// <summary>The most recent conversation started through this trigger.</summary>
    public NPCConversation LastStartedConversation { get; private set; }
    public bool IsTalking => isTalking;

    private void Awake()
    {
        storyConversationSelector = GetComponent<StoryConversationSelector>();
        taskStageConversationSelector = GetComponent<TaskStageConversationSelector>();
        storyFocusFinishedEvent.AddListener(HandleStoryFocusFinished);
        GameplayHUDTarget.AttachTo(talkText);

        if (linkedConversationMovement != null &&
            !(linkedConversationMovement is INPCConversationMovement))
            Debug.LogWarning("Linked conversation movement must implement INPCConversationMovement.", this);

        if (availabilityCondition != null &&
            !(availabilityCondition is IInteractionAvailabilityCondition))
            Debug.LogWarning("Interaction availability must implement IInteractionAvailabilityCondition.", this);
    }

    public void SetConversations(
        NPCConversation nextFirstConversation,
        NPCConversation nextRepeatConversation)
    {
        firstConversation = nextFirstConversation;
        repeatConversation = nextRepeatConversation;
    }

    public bool OwnsCameraFocus(
        CameraFocusManager manager,
        CameraFocusPoint point)
    {
        return focusManager != null &&
               focusPoint != null &&
               focusManager == manager &&
               focusPoint == point;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInteractionState()
    {
        nearbyTriggers.Clear();
        activeConversationTrigger = null;
    }

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += OnConversationEnded;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= OnConversationEnded;

        if (turnCoroutine != null)
        {
            StopCoroutine(turnCoroutine);
            turnCoroutine = null;
        }

        nearbyTriggers.Remove(this);
        if (activeConversationTrigger == this)
            activeConversationTrigger = null;

        ResumeOwnedMovement();
        isTalking = false;
        ReleaseStorySequence();
        playerNear = false;
        RefreshPrompt(talkText);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
            nearbyTriggers.Add(this);
            RefreshPrompt(talkText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            nearbyTriggers.Remove(this);
            RefreshPrompt(talkText);
        }
    }

    private void Update()
    {
        if (playerNear)
            RefreshPrompt(talkText);

        if (!StorySequenceCoordinator.IsStorySequenceActive &&
            activeConversationTrigger == null && IsInteractionCandidate() &&
            IsClosestAvailableTrigger() &&
            Input.GetKeyDown(KeyCode.E))
        {
            StartConversation();
        }
    }

    private void StartConversation()
    {
        NPCConversation conversation =
            taskStageConversationSelector != null
                ? taskStageConversationSelector.GetCurrentConversation()
                : null;

        if (conversation == null)
            conversation = storyConversationSelector != null
                ? storyConversationSelector.GetCurrentConversation()
                : null;

        if (conversation == null)
        {
            conversation =
                HasCompletedFirstConversation() && repeatConversation != null
                    ? repeatConversation
                    : firstConversation;
        }

        BeginConversation(conversation, true);
    }

    /// <summary>Starts a specified follow-up conversation through this NPC's normal presentation.</summary>
    public bool StartConversationProgrammatically(NPCConversation conversation)
    {
        ConversationManager manager = ConversationManager.Instance;
        if (conversation == null || !isActiveAndEnabled || isTalking ||
            activeConversationTrigger != null || manager == null ||
            manager.IsConversationActive)
        {
            return false;
        }

        BeginConversation(conversation, false);
        return true;
    }

    private void BeginConversation(NPCConversation conversation, bool countsAsFirstConversation)
    {
        isTalking = true;
        activeConversationTrigger = this;
        currentConversationCountsAsFirst = countsAsFirstConversation;

        if (treatAsStorySequence)
            storySequenceToken = StorySequenceCoordinator.Acquire(this);

        RefreshPrompt(talkText);

        // Capture this conversation's heading before turning toward the player.
        if (facePlayerWhenTalking &&
            npcTransform != null &&
            playerTransform != null)
        {
            originalRotation = npcTransform.rotation;
            if (turnCoroutine != null)
                StopCoroutine(turnCoroutine);

            turnCoroutine = StartCoroutine(TurnTowardPlayer());
        }

        // Optional camera focus
        if (focusManager != null && focusPoint != null)
            focusManager.FocusOn(focusPoint, storyFocusFinishedEvent);

        LastStartedConversation = conversation;

        ConversationManager.Instance.StartConversation(conversation);

        INPCConversationMovement movement = linkedConversationMovement != null
            ? linkedConversationMovement as INPCConversationMovement
            : linkedPatrol;
        if (movement != null)
        {
            movement.SetConversationMovementPaused(true);
            pausedMovement = movement;
        }
    }

    private bool IsInteractionAvailable()
    {
        return availabilityCondition == null ||
               (availabilityCondition is IInteractionAvailabilityCondition condition &&
                condition.IsAvailable());
    }

    private bool IsInteractionCandidate()
    {
        return isActiveAndEnabled && playerNear && !isTalking &&
               IsInteractionAvailable();
    }

    private bool IsClosestAvailableTrigger()
    {
        NPCConversationTrigger closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (NPCConversationTrigger candidate in nearbyTriggers)
        {
            if (candidate == null || !candidate.IsInteractionCandidate())
                continue;

            Transform npc = candidate.npcTransform != null
                ? candidate.npcTransform : candidate.transform;
            Transform player = candidate.playerTransform != null
                ? candidate.playerTransform : playerTransform;
            float distance = player != null
                ? (npc.position - player.position).sqrMagnitude
                : 0f;

            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest == this;
    }

    private static void RefreshPrompt(GameObject prompt)
    {
        if (prompt == null)
            return;

        bool shouldShow = false;
        if (activeConversationTrigger == null &&
            !StorySequenceCoordinator.IsStorySequenceActive)
        {
            foreach (NPCConversationTrigger candidate in nearbyTriggers)
            {
                if (candidate != null && candidate.talkText == prompt &&
                    candidate.IsInteractionCandidate())
                {
                    shouldShow = true;
                    break;
                }
            }
        }

        if (prompt.activeSelf != shouldShow)
            prompt.SetActive(shouldShow);
    }

    private void ResumeOwnedMovement()
    {
        if (pausedMovement is UnityEngine.Object movementObject && movementObject != null)
            pausedMovement.SetConversationMovementPaused(false);
        pausedMovement = null;
    }

    private IEnumerator TurnTowardPlayer()
    {
        Vector3 direction = playerTransform.position - npcTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        while (Quaternion.Angle(
            npcTransform.rotation,
            targetRotation) > 1f)
        {
            npcTransform.rotation = Quaternion.Slerp(
                npcTransform.rotation,
                targetRotation,
                Time.deltaTime * turnSpeed);

            yield return null;
        }

        npcTransform.rotation = targetRotation;
    }

    private IEnumerator ReturnToOriginalRotation()
    {
        while (Quaternion.Angle(
            npcTransform.rotation,
            originalRotation) > 1f)
        {
            npcTransform.rotation = Quaternion.Slerp(
                npcTransform.rotation,
                originalRotation,
                Time.deltaTime * turnSpeed);

            yield return null;
        }

        npcTransform.rotation = originalRotation;
        turnCoroutine = null;
        FinishOwnedInteraction();
    }

    private void OnConversationEnded()
    {
        if (!isTalking)
            return;

        isTalking = false;

        bool finishedFirstConversation = currentConversationCountsAsFirst &&
            !HasCompletedFirstConversation();
        currentConversationCountsAsFirst = false;
        if (finishedFirstConversation)
        {
            hasCompletedFirstConversation = true;

            if (!string.IsNullOrEmpty(firstConversationCompletedFlagId))
            {
                SessionStoryState.SetFlag(
                    firstConversationCompletedFlagId,
                    true);
            }
        }

        ConversationFinished?.Invoke();
        OnConversationFinished?.Invoke();

        if (finishedFirstConversation)
            OnFirstConversationFinished?.Invoke();

        // Return camera
        if (focusManager != null && focusPoint != null)
        {
            if (!focusManager.TryReturnToNormal(focusPoint))
                ReleaseStorySequence();
        }
        else
            ReleaseStorySequence();

        if (turnCoroutine != null)
        {
            StopCoroutine(turnCoroutine);
            turnCoroutine = null;
        }

        // Keep this NPC paused until its return turn has completed.
        if (facePlayerWhenTalking &&
            returnToOriginalDirection &&
            npcTransform != null &&
            playerTransform != null)
        {
            turnCoroutine = StartCoroutine(
                ReturnToOriginalRotation());
        }
        else
            FinishOwnedInteraction();
    }

    private void FinishOwnedInteraction()
    {
        ResumeOwnedMovement();
        if (activeConversationTrigger == this)
            activeConversationTrigger = null;
        RefreshPrompt(talkText);
    }

    private void HandleStoryFocusFinished()
    {
        ReleaseStorySequence();
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken == null)
            return;

        storySequenceToken.Release();
        storySequenceToken = null;
    }

    private bool HasCompletedFirstConversation()
    {
        if (!string.IsNullOrEmpty(firstConversationCompletedFlagId))
        {
            return SessionStoryState.GetFlag(
                firstConversationCompletedFlagId);
        }

        return hasCompletedFirstConversation;
    }
}
