using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CameraFocusTrigger : MonoBehaviour
{
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;

    [Header("Conversation Source (Optional)")]
    [SerializeField] private NPCConversationTrigger npcConversationTrigger;
    [SerializeField] private SelfDialogueTrigger selfDialogueTrigger;

    [Header("Story Event")]
    [Tooltip("Optional runtime ID that keeps this event completed across scene reloads.")]
    [SerializeField] private string eventId;

    [Header("After Focus")]
    public UnityEvent OnFocusFinished;

    private static readonly HashSet<string> completedEventIds = new HashSet<string>();
    private bool hasTriggered = false;
    private bool focusLifecycleActive = false;
    private bool subscribedToNpcConversation = false;
    private bool subscribedToSelfDialogue = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCompletedEventIds()
    {
        completedEventIds.Clear();
    }

    private void Reset()
    {
        AutoFindConversationSources();
    }

    private void OnValidate()
    {
        AutoFindConversationSources();
    }

    private void OnEnable()
    {
        AutoFindConversationSources();
        SubscribeToConversationSources();
    }

    private void OnDisable()
    {
        UnsubscribeFromConversationSources();
        focusLifecycleActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            TriggerFocus();
    }

    public void TriggerFocus()
    {
        if (hasTriggered || IsEventCompleted())
            return;

        hasTriggered = true;

        if (!string.IsNullOrEmpty(eventId))
            completedEventIds.Add(eventId);

        bool focusStarted = focusManager != null &&
            focusPoint != null &&
            focusManager.TryFocusOn(focusPoint, OnFocusFinished);

        focusLifecycleActive = focusStarted &&
            focusPoint.returnMode == CameraFocusPoint.ReturnMode.AfterConversation;
    }

    private void AutoFindConversationSources()
    {
        if (npcConversationTrigger == null)
            npcConversationTrigger = GetComponent<NPCConversationTrigger>();

        if (selfDialogueTrigger == null)
            selfDialogueTrigger = GetComponent<SelfDialogueTrigger>();
    }

    private void SubscribeToConversationSources()
    {
        if (focusPoint == null ||
            focusPoint.returnMode != CameraFocusPoint.ReturnMode.AfterConversation)
        {
            return;
        }

        if (selfDialogueTrigger != null && !subscribedToSelfDialogue)
        {
            selfDialogueTrigger.ConversationFinished += HandleConversationFinished;
            subscribedToSelfDialogue = true;
        }

        bool npcOwnsCameraLifecycle = npcConversationTrigger != null &&
            npcConversationTrigger.OwnsCameraFocus(focusManager, focusPoint);
        if (npcConversationTrigger != null &&
            !npcOwnsCameraLifecycle &&
            !subscribedToNpcConversation)
        {
            npcConversationTrigger.ConversationFinished += HandleConversationFinished;
            subscribedToNpcConversation = true;
        }
    }

    private void UnsubscribeFromConversationSources()
    {
        if (subscribedToSelfDialogue && selfDialogueTrigger != null)
            selfDialogueTrigger.ConversationFinished -= HandleConversationFinished;

        if (subscribedToNpcConversation && npcConversationTrigger != null)
            npcConversationTrigger.ConversationFinished -= HandleConversationFinished;

        subscribedToSelfDialogue = false;
        subscribedToNpcConversation = false;
    }

    private void HandleConversationFinished()
    {
        if (!focusLifecycleActive || focusManager == null || focusPoint == null)
            return;

        if (!focusManager.TryReturnToNormal(focusPoint))
            return;

        focusLifecycleActive = false;
        UnsubscribeFromConversationSources();
    }

    private bool IsEventCompleted()
    {
        return !string.IsNullOrEmpty(eventId) &&
               completedEventIds.Contains(eventId);
    }
}
