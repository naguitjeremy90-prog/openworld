using System;
using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;

/// <summary>Temporarily switches configured Animators for a conversation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OverheardConversationSequence))]
public sealed class ConversationAnimationController : MonoBehaviour
{
    [Serializable]
    private struct Participant
    {
        public Animator animator;
        public RuntimeAnimatorController conversationController;
    }

    private struct OriginalState
    {
        public Animator animator;
        public RuntimeAnimatorController controller;
        public bool applyRootMotion;
    }

    [SerializeField] private List<Participant> participants = new List<Participant>();

    private readonly List<OriginalState> originalStates = new List<OriginalState>();
    private OverheardConversationSequence sequence;
    private bool conversationActive;

    private void Awake()
    {
        sequence = GetComponent<OverheardConversationSequence>();
    }

    private void OnEnable()
    {
        ConversationManager.OnConversationStarted += HandleConversationStarted;
    }

    private void HandleConversationStarted()
    {
        if (sequence != null && sequence.IsOverheardPresentationPending)
            BeginConversationAnimations();
    }

    public void BeginConversationAnimations()
    {
        if (conversationActive)
            return;

        originalStates.Clear();
        foreach (Participant participant in participants)
        {
            if (participant.animator == null || participant.conversationController == null)
                continue;

            Animator animator = participant.animator;
            originalStates.Add(new OriginalState
            {
                animator = animator,
                controller = animator.runtimeAnimatorController,
                applyRootMotion = animator.applyRootMotion
            });

            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = participant.conversationController;
        }

        conversationActive = true;
    }

    public void RestoreOriginalAnimations()
    {
        if (!conversationActive)
            return;

        foreach (OriginalState state in originalStates)
        {
            if (state.animator == null)
                continue;

            state.animator.runtimeAnimatorController = state.controller;
            state.animator.applyRootMotion = state.applyRootMotion;
        }

        originalStates.Clear();
        conversationActive = false;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationStarted -= HandleConversationStarted;
        RestoreOriginalAnimations();
    }
}
