using UnityEngine;
using UnityEngine.Events;
using DialogueEditor;

public class SelfDialogueTrigger : MonoBehaviour
{
    public event System.Action ConversationFinished;
    [Header("Conversation")]
    [SerializeField] private NPCConversation myConversation;

    [Header("Settings")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool autoStartOnEnter = true;

    private bool hasTriggered = false;
    private bool conversationStarted = false;

    [Header("Callbacks")]
    public UnityEvent OnConversationFinished = new UnityEvent();

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += HandleConversationEnded;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= HandleConversationEnded;
        conversationStarted = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoStartOnEnter)
            return;

        if (!other.CompareTag("Player"))
            return;

        StartSelfDialogue();
    }

    public void StartSelfDialogue()
    {
        if (triggerOnce && hasTriggered)
            return;

        if (myConversation == null)
            return;

        conversationStarted = true;
        hasTriggered = true;
        ConversationManager.Instance.StartConversation(myConversation);
    }

    private void HandleConversationEnded()
    {
        if (!conversationStarted)
            return;

        conversationStarted = false;
        ConversationFinished?.Invoke();
        OnConversationFinished?.Invoke();
    }
}
