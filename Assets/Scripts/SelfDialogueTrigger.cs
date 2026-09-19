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

    [Header("Presentation (Optional)")]
    [SerializeField] private bool treatAsStorySequence;

    private bool hasTriggered = false;
    private bool conversationStarted = false;
    private StorySequenceToken storySequenceToken;

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
        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }
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
        if (conversationStarted)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (myConversation == null)
            return;

        conversationStarted = true;
        hasTriggered = true;
        if (treatAsStorySequence)
            storySequenceToken = StorySequenceCoordinator.Acquire(this);
        ConversationManager.Instance.StartConversation(myConversation);
    }

    public void ConfigureConversation(NPCConversation conversation)
    {
        if (conversation != null)
            myConversation = conversation;
    }

    private void HandleConversationEnded()
    {
        if (!conversationStarted)
            return;

        conversationStarted = false;
        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }
        ConversationFinished?.Invoke();
        OnConversationFinished?.Invoke();
    }
}
