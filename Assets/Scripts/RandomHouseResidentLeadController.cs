using System;
using DialogueEditor;
using UnityEngine;

/// <summary>Adds the resident's main-task follow-up without replacing the information interaction.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NPCConversationTrigger))]
public sealed class RandomHouseResidentLeadController : MonoBehaviour
{
    private const string TaskId = "main_investigate_pili";
    private const string LeadStage = "find_knowledgeable_person";
    private const string NextStage = "ask_aling_ika_about_maestro";
    private const string LeadFlag = "randomhouse_maestro_lead_received";

    [SerializeField] private NPCConversationTrigger conversationTrigger;
    [SerializeField] private SphereCollider interactionVolume;
    [SerializeField] private Transform playerTransform;

    [SerializeField] private NPCConversation leadConversation;
    [SerializeField] private NPCConversation reminderConversation;
    private bool leadAttemptActive;

    private void Awake()
    {
        if (conversationTrigger == null)
            conversationTrigger = GetComponent<NPCConversationTrigger>();
        if (interactionVolume == null)
            interactionVolume = GetComponent<SphereCollider>();

        if (leadConversation == null)
            Debug.LogError("Resident lead controller is missing its authored Lead Conversation. Assign the scene's NPCConversation in the Inspector; lead progression cannot run without it.", this);
        if (reminderConversation == null)
            Debug.LogError("Resident lead controller is missing its authored Reminder Conversation. Assign the scene's NPCConversation in the Inspector; reminder teaching cannot run without it.", this);
    }

    private void OnEnable()
    {
        ConversationManager.ConversationClosed += HandleConversationClosed;
    }

    private void OnDisable()
    {
        ConversationManager.ConversationClosed -= HandleConversationClosed;
        leadAttemptActive = false;
    }

    private void Update()
    {
        if (leadAttemptActive && !CanRemainInLead())
            leadAttemptActive = false;

        // Run before the existing trigger's Update. Its normal interaction path remains
        // untouched until this follow-up is eligible; the programmatic API preserves
        // facing/camera behavior without setting the original first-conversation flag.
        if (!Input.GetKeyDown(KeyCode.E) || Time.timeScale <= 0f ||
            conversationTrigger == null || !conversationTrigger.isActiveAndEnabled ||
            conversationTrigger.IsTalking || StorySequenceCoordinator.IsStorySequenceActive ||
            !IsPlayerInside())
            return;

        ConversationManager manager = ConversationManager.Instance;
        if (manager == null || manager.IsConversationActive)
            return;

        bool isLead = IsLeadEligible();
        NPCConversation conversation = isLead ? leadConversation :
            SessionStoryState.GetFlag(LeadFlag) ? reminderConversation : null;
        if (conversation == null)
            return;

        leadAttemptActive = isLead;
        if (!conversationTrigger.StartConversationProgrammatically(conversation))
            leadAttemptActive = false;
    }

    private void OnTriggerExit(Collider other)
    {
        // Leaving invalidates this attempt even if Miguel returns before the final line.
        if (other.CompareTag("Player"))
            leadAttemptActive = false;
    }

    private bool IsLeadEligible()
    {
        TaskManager manager = TaskManager.Instance;
        return !SessionStoryState.GetFlag(LeadFlag) && manager != null &&
               manager.GetTaskState(TaskId) == TaskState.Active &&
               manager.IsCurrentStage(TaskId, LeadStage);
    }

    private bool CanRemainInLead()
    {
        return isActiveAndEnabled && conversationTrigger != null &&
               conversationTrigger.isActiveAndEnabled && IsLeadEligible() && IsPlayerInside();
    }

    private bool IsPlayerInside()
    {
        if (interactionVolume == null || !interactionVolume.enabled ||
            !interactionVolume.gameObject.activeInHierarchy || playerTransform == null)
            return false;

        // Re-evaluate live colliders rather than trusting a stale trigger-enter latch.
        foreach (Collider playerCollider in playerTransform.GetComponentsInChildren<Collider>())
        {
            if (playerCollider == null || !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy || !playerCollider.CompareTag("Player"))
                continue;

            if (Physics.ComputePenetration(interactionVolume, interactionVolume.transform.position,
                interactionVolume.transform.rotation, playerCollider, playerCollider.transform.position,
                playerCollider.transform.rotation, out _, out _))
                return true;
        }
        return false;
    }

    private void HandleConversationClosed(NPCConversation conversation, bool completedNormally)
    {
        if (conversation != leadConversation)
            return;

        bool canCommit = leadAttemptActive && completedNormally && CanRemainInLead() &&
                         conversationTrigger.LastStartedConversation == leadConversation;
        leadAttemptActive = false;
        if (!canCommit)
            return;

        TaskManager manager = TaskManager.Instance;
        try
        {
            manager.AdvanceTaskStage(TaskId, NextStage);
        }
        catch (Exception exception)
        {
            // TaskChanged listeners can throw after the stage has already been stored.
            Debug.LogError("Resident lead stage advancement raised an error; verifying the stored stage.", this);
            Debug.LogException(exception, this);
        }

        if (manager.GetTaskState(TaskId) != TaskState.Active ||
            !manager.IsCurrentStage(TaskId, NextStage))
        {
            Debug.LogWarning("Resident lead could not advance to ask_aling_ika_about_maestro; the lead remains retryable.", this);
            return;
        }

        // Do not record a reward if the task API rejected advancement. SetFlag itself
        // coalesces repeated writes, and this handler consumes the attempt only once.
        SessionStoryState.SetFlag(LeadFlag, true);
    }

}
