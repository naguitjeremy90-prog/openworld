using UnityEngine;
using DialogueEditor;

public class NPCDialogueGate : MonoBehaviour
{
    public NPCConversation lockedConversation;
    public NPCConversation firstConversation;
    public NPCConversation repeatConversation;

    public bool requiresUnlock = true;
    public bool isUnlockingNPC = false;

    bool alreadyTalked = false;
    bool playerInside = false;

    void Update()
    {
        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            if (isUnlockingNPC)
            {
                StartNormalDialogue();

                if (NPCDialogueProgress.Instance != null)
                    NPCDialogueProgress.Instance.UnlockAllNPCDialogue();

                return;
            }

            if (requiresUnlock && NPCDialogueProgress.Instance != null && !NPCDialogueProgress.Instance.dialogueUnlocked)
            {
                if (lockedConversation != null)
                    ConversationManager.Instance.StartConversation(lockedConversation);

                return;
            }

            StartNormalDialogue();
        }
    }

    void StartNormalDialogue()
    {
        if (!alreadyTalked)
        {
            if (firstConversation != null)
                ConversationManager.Instance.StartConversation(firstConversation);

            alreadyTalked = true;
        }
        else
        {
            if (repeatConversation != null)
                ConversationManager.Instance.StartConversation(repeatConversation);
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