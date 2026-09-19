using UnityEngine;
using DialogueEditor;

public class DialogueMovementLock : MonoBehaviour
{
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    private bool ownsConversationLock;
    private bool previousMovementEnabled;
    private bool capturedMovementState;

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
        if (ownsConversationLock)
            return;

        ownsConversationLock = true;

        if (playerMovementScript != null)
        {
            previousMovementEnabled = playerMovementScript.enabled;
            capturedMovementState = true;
            playerMovementScript.enabled = false;
        }

        if (playerAnimator != null)
            playerAnimator.SetFloat("MoveSpeed", 0f);
    }

    public void UnlockMovement()
    {
        if (!ownsConversationLock)
            return;

        if (capturedMovementState && playerMovementScript != null)
            playerMovementScript.enabled = previousMovementEnabled;

        ownsConversationLock = false;
        capturedMovementState = false;
    }
}
