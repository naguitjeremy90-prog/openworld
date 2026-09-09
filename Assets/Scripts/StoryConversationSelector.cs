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

        [SerializeField] private NPCConversation conversation;

        [Tooltip("When multiple states are valid, the state with the highest priority is selected.")]
        [SerializeField] private int priority;

        public string RequiredStoryFlagId => requiredStoryFlagId;
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

            bool isValid = string.IsNullOrEmpty(state.RequiredStoryFlagId) ||
                           SessionStoryState.GetFlag(state.RequiredStoryFlagId);
            if (!isValid)
                continue;

            if (selectedState == null || state.Priority > selectedState.Priority)
                selectedState = state;
        }

        return selectedState?.Conversation;
    }
}
