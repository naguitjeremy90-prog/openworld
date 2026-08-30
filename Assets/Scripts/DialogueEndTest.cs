using UnityEngine;
using DialogueEditor;

public class DialogueEndTest : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (ConversationManager.Instance != null)
                ConversationManager.Instance.EndConversation();
        }
    }
}
