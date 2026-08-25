using UnityEngine;
using DialogueEditor;

public class SelfDialogueTrigger : MonoBehaviour
{
    [Header("Conversation")]
    [SerializeField] private NPCConversation myConversation;

    [Header("Settings")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool autoStartOnEnter = true;

    private bool hasTriggered = false;

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

        hasTriggered = true;

        ConversationManager.Instance.StartConversation(myConversation);
    }
}
