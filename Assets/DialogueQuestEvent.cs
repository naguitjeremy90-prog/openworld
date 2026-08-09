using UnityEngine;

public class DialogueQuestEvent : MonoBehaviour
{
    public ImportantNPC npcID;

    public void CompleteNPCDialogue()
    {
        if (JournalProgressTracker.Instance != null)
            JournalProgressTracker.Instance.RegisterTalk(npcID);
    }
}