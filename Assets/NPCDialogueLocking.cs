using UnityEngine;

public class NPCDialogueProgress : MonoBehaviour
{
    public static NPCDialogueProgress Instance;

    public bool dialogueUnlocked = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UnlockAllNPCDialogue()
    {
        dialogueUnlocked = true;
    }
}