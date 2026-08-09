using UnityEngine;
using DialogueEditor;

public class NPCRepeatDialogue : MonoBehaviour
{
    public NPCConversation firstConversation;
    public NPCConversation repeatConversation;

    bool alreadyTalked;
    bool playerInside;

    void Update()
    {
        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            if (!alreadyTalked)
            {
                ConversationManager.Instance.StartConversation(firstConversation);
                alreadyTalked = true;
            }
            else
            {
                ConversationManager.Instance.StartConversation(repeatConversation);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }
}