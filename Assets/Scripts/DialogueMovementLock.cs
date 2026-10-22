using UnityEngine;
using DialogueEditor;

public class DialogueMovementLock : MonoBehaviour
{
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    private bool ownsConversationLock;
    private bool previousMovementEnabled;
    private bool capturedMovementState;
    private bool storySequenceHoldActive;
    private bool previousStoryHoldMovementEnabled;
    private bool capturedStoryHoldMovementState;

    public bool OwnsConversationLock => ownsConversationLock;
    internal Transform SaveRestorationPlayer => playerMovementScript != null ? playerMovementScript.transform : null;

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

        if (!storySequenceHoldActive && capturedMovementState && playerMovementScript != null)
            playerMovementScript.enabled = previousMovementEnabled;

        ownsConversationLock = false;
        capturedMovementState = false;
    }

    /// <summary>Keeps movement disabled after a dialogue closes until a scene transition finishes.</summary>
    public void HoldMovementForStorySequence()
    {
        if (storySequenceHoldActive)
            return;

        storySequenceHoldActive = true;
        if (playerMovementScript != null)
        {
            previousStoryHoldMovementEnabled = playerMovementScript.enabled;
            capturedStoryHoldMovementState = true;
            playerMovementScript.enabled = false;
        }

        if (playerAnimator != null)
            playerAnimator.SetFloat("MoveSpeed", 0f);
    }

    /// <summary>Restores the exact movement state captured when the sequence hold began.</summary>
    public void ReleaseStorySequenceHold()
    {
        if (!storySequenceHoldActive)
            return;

        storySequenceHoldActive = false;
        if (!ownsConversationLock && capturedStoryHoldMovementState && playerMovementScript != null)
            playerMovementScript.enabled = previousStoryHoldMovementEnabled;

        capturedStoryHoldMovementState = false;
    }
}
