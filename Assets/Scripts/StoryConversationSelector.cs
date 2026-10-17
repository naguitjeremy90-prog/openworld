using System;
using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoryConversationSelector : MonoBehaviour
{
    [Serializable]
    private sealed class ConversationState
    {
        [Tooltip("A descriptive label used only to identify this state in the Inspector.")]
        [SerializeField] private string stateName;

        [Tooltip("Leave empty to make this state valid as a default conversation.")]
        [SerializeField] private string requiredStoryFlagId;

        [Tooltip("Optional task that must be completed for this state to be eligible.")]
        [SerializeField] private string requiredCompletedTaskId;

        [Tooltip("Optional additional story/session flags. Every configured flag must be true.")]
        [SerializeField] private List<string> requiredSessionStateFlagIds =
            new List<string>();

        [SerializeField] private NPCConversation conversation;

        [Tooltip("When multiple states are valid, the state with the highest priority is selected.")]
        [SerializeField] private int priority;

        public string RequiredStoryFlagId => requiredStoryFlagId;
        public string RequiredCompletedTaskId => requiredCompletedTaskId;
        public List<string> RequiredSessionStateFlagIds => requiredSessionStateFlagIds;
        public NPCConversation Conversation => conversation;
        public int Priority => priority;
    }

    [Header("Conversation States")]
    [Tooltip("Valid states are evaluated whenever the NPC is spoken to. The first state wins if priorities are tied.")]
    [SerializeField] private List<ConversationState> conversationStates =
        new List<ConversationState>();

    [Header("Development Override")]
    [Tooltip("Testing only. When enabled, ignores story flags and returns the Debug Conversation without changing story progress.")]
    [SerializeField] private bool enableDebugOverride;

    [Tooltip("Testing only. Conversation returned while Enable Debug Override is active.")]
    [SerializeField] private NPCConversation debugConversation;

    public NPCConversation GetCurrentConversation()
    {
        if (enableDebugOverride)
            return debugConversation;

        ConversationState selectedState = null;

        if (conversationStates == null)
            return null;

        for (int i = 0; i < conversationStates.Count; i++)
        {
            ConversationState state = conversationStates[i];
            if (state == null || state.Conversation == null)
                continue;

            bool isValid = IsValid(state);
            if (!isValid)
                continue;

            if (selectedState == null || state.Priority > selectedState.Priority)
                selectedState = state;
        }

        return selectedState?.Conversation;
    }

    private static bool IsValid(ConversationState state)
    {
        if (!string.IsNullOrWhiteSpace(state.RequiredStoryFlagId) &&
            !SessionStoryState.GetFlag(state.RequiredStoryFlagId.Trim()))
            return false;

        if (!string.IsNullOrWhiteSpace(state.RequiredCompletedTaskId) &&
            !IsTaskCompleted(state.RequiredCompletedTaskId))
            return false;

        List<string> requiredFlags = state.RequiredSessionStateFlagIds;
        if (requiredFlags == null)
            return true;

        for (int i = 0; i < requiredFlags.Count; i++)
        {
            string flagId = requiredFlags[i];
            if (!string.IsNullOrWhiteSpace(flagId) &&
                !SessionStoryState.GetFlag(flagId.Trim()))
                return false;
        }

        return true;
    }

    private static bool IsTaskCompleted(string taskId)
    {
        string normalizedTaskId = taskId.Trim();
        TaskManager manager = TaskManager.Instance;
        if (manager != null)
            return manager.GetTaskState(normalizedTaskId) == TaskState.Completed;

        return SessionStoryState.GetFlag("task_completed:" + normalizedTaskId);
    }
}
