using System;
using DialogueEditor;
using UnityEngine;

/// <summary>Selects conversations for active task stages and applies their completion effects.</summary>
[RequireComponent(typeof(NPCConversationTrigger))]
public sealed class TaskStageConversationSelector : MonoBehaviour
{
    [Serializable]
    private sealed class StageEntry
    {
        [SerializeField] private string requiredStageId;
        [SerializeField] private NPCConversation conversation;
        [SerializeField] private string nextStageId;
        [SerializeField] private InventoryItemData itemToGive;
        [SerializeField] private string itemToRemoveId;

        public string RequiredStageId => requiredStageId;
        public NPCConversation Conversation => conversation;
        public string NextStageId => nextStageId;
        public InventoryItemData ItemToGive => itemToGive;
        public string ItemToRemoveId => itemToRemoveId;
    }

    [SerializeField] private string taskId;
    [SerializeField] private string requiredStageId;
    [SerializeField] private NPCConversation stageConversation;
    [SerializeField] private string nextStageId;
    [SerializeField] private StageEntry[] additionalStages = Array.Empty<StageEntry>();

    private NPCConversationTrigger conversationTrigger;
    private StageEntry selectedEntry;
    private bool legacyStageConversationStarted;

    private void Awake()
    {
        conversationTrigger = GetComponent<NPCConversationTrigger>();
    }

    private void OnEnable()
    {
        if (conversationTrigger == null)
            conversationTrigger = GetComponent<NPCConversationTrigger>();
        conversationTrigger.ConversationFinished += OnConversationFinished;
    }

    private void OnDisable()
    {
        if (conversationTrigger != null)
            conversationTrigger.ConversationFinished -= OnConversationFinished;
        selectedEntry = null;
        legacyStageConversationStarted = false;
    }

    public NPCConversation GetCurrentConversation()
    {
        selectedEntry = null;
        legacyStageConversationStarted = false;
        TaskManager manager = TaskManager.Instance;
        if (manager == null || manager.GetTaskState(taskId) != TaskState.Active)
            return null;

        if (additionalStages != null)
        {
            foreach (StageEntry entry in additionalStages)
            {
                if (entry == null || entry.Conversation == null ||
                    !manager.IsCurrentStage(taskId, entry.RequiredStageId))
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.ItemToRemoveId) &&
                    (InventoryManager.Instance == null ||
                     !InventoryManager.Instance.HasItem(entry.ItemToRemoveId)))
                    return null;

                selectedEntry = entry;
                return entry.Conversation;
            }
        }

        legacyStageConversationStarted = stageConversation != null &&
            manager.IsCurrentStage(taskId, requiredStageId);
        return legacyStageConversationStarted ? stageConversation : null;
    }

    private void OnConversationFinished()
    {
        StageEntry completedEntry = selectedEntry;
        bool completedLegacy = legacyStageConversationStarted;
        selectedEntry = null;
        legacyStageConversationStarted = false;
        if (completedEntry == null && !completedLegacy)
            return;

        TaskManager manager = TaskManager.Instance;
        string currentStageId = completedEntry != null
            ? completedEntry.RequiredStageId : requiredStageId;
        if (manager == null || manager.GetTaskState(taskId) != TaskState.Active ||
            !manager.IsCurrentStage(taskId, currentStageId))
            return;

        if (completedEntry != null)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (completedEntry.ItemToGive != null)
            {
                if (inventory == null ||
                    (!inventory.HasItem(completedEntry.ItemToGive.ItemID) &&
                     !inventory.AddItem(completedEntry.ItemToGive)))
                {
                    Debug.LogWarning("Stage conversation could not give its inventory item.", this);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(completedEntry.ItemToRemoveId))
            {
                if (inventory == null ||
                    !inventory.RemoveItem(completedEntry.ItemToRemoveId))
                {
                    Debug.LogWarning("Stage conversation could not hand over its inventory item.", this);
                    return;
                }
            }
        }

        manager.AdvanceTaskStage(taskId,
            completedEntry != null ? completedEntry.NextStageId : nextStageId);
    }
}
