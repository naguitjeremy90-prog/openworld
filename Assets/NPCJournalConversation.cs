using UnityEngine;
using DialogueEditor;

public class NPCJournalConversation : MonoBehaviour
{
    [SerializeField] private NPCConversation myConversation;
    [SerializeField] private ImportantNPC npcID;

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            // Check ConversationManager
            if (ConversationManager.Instance == null)
            {
                Debug.LogError("NPCJournalConversation: ConversationManager.Instance is NULL on " + gameObject.name);
                return;
            }

            // Check conversation assignment
            if (myConversation == null)
            {
                Debug.LogError("NPCJournalConversation: myConversation is NOT assigned on " + gameObject.name);
                return;
            }

            // Check JournalProgressTracker
            if (JournalProgressTracker.Instance == null)
            {
                Debug.LogError("NPCJournalConversation: JournalProgressTracker.Instance is NULL. Make sure JournalProgressTracker exists in the scene.");
                return;
            }

            // Poor Townsman lock check
            if (npcID == ImportantNPC.PoorTownsman && !JournalProgressTracker.Instance.CanTalkToPoorTownsman())
            {
                Debug.Log("Poor Townsman is locked. Talk to the required NPCs first.");
                return;
            }

            // Start dialogue
            ConversationManager.Instance.StartConversation(myConversation);

            // Register progression
            JournalProgressTracker.Instance.RegisterTalk(npcID);

            Debug.Log("Started conversation with: " + npcID);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (ConversationManager.Instance != null)
        {
            ConversationManager.Instance.EndConversation();
        }
    }
}

public enum ImportantNPC
{
    Vendor,
    Gossips,
    Beata,
    Sacristan,
    ManaSebia,
    PoorTownsman
}