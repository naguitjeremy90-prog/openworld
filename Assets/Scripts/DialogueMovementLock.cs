using UnityEngine;
using DialogueEditor;

public class DialogueMovementLock : MonoBehaviour
{
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    private void OnEnable()
    {
        ConversationManager.OnConversationStarted += LockMovement;
        ConversationManager.OnConversationEnded += UnlockMovement;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationStarted -= LockMovement;
        ConversationManager.OnConversationEnded -= UnlockMovement;
    }

    public void LockMovement()
    {
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        if (playerAnimator != null)
            playerAnimator.SetFloat("MoveSpeed", 0f);
    }

    public void UnlockMovement()
    {
        if (playerMovementScript != null)
            playerMovementScript.enabled = true;
    }
}
