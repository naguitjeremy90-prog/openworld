using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [SerializeField] private GameObject interactText;
    [SerializeField] private UnityEvent onInteract;
    [SerializeField] private UnityEvent onExitInteraction;
    [SerializeField] private MonoBehaviour availabilityCondition;

    private bool playerNear = false;
    private IInteractionAvailabilityCondition availabilityProvider;
    private IInteractionPromptAvailability promptAvailabilityProvider;
    private bool lastPromptVisible;
    private bool interactionConsumedUntilExit;

    private void Awake()
    {
        GameplayHUDTarget.AttachTo(interactText);
        availabilityProvider = availabilityCondition as IInteractionAvailabilityCondition;
        promptAvailabilityProvider = availabilityCondition as IInteractionPromptAvailability;
        lastPromptVisible = interactText != null && interactText.activeSelf;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
            interactionConsumedUntilExit = false;
            RefreshPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            interactionConsumedUntilExit = false;
            RefreshPrompt();

            onExitInteraction.Invoke();
        }
    }

    private void Update()
    {
        bool promptVisible = GetPromptVisibility();
        if (availabilityProvider != null && interactText != null &&
            promptVisible != lastPromptVisible)
            RefreshPrompt();

        if (!StorySequenceCoordinator.IsStorySequenceActive &&
            playerNear && Input.GetKeyDown(KeyCode.E))
        {
            if (!IsInteractionAvailable())
                return;

            if (interactText != null)
                interactText.SetActive(false);
            lastPromptVisible = false;
            interactionConsumedUntilExit = true;

            onInteract.Invoke();
        }
    }

    private bool IsInteractionAvailable()
    {
        return availabilityProvider == null || availabilityProvider.IsAvailable();
    }

    private bool GetPromptVisibility()
    {
        if (!playerNear || interactionConsumedUntilExit)
            return false;

        if (promptAvailabilityProvider != null)
            return promptAvailabilityProvider.ShouldShowPrompt;

        return IsInteractionAvailable();
    }

    private void RefreshPrompt()
    {
        bool visible = GetPromptVisibility();
        if (interactText != null)
            interactText.SetActive(visible);
        lastPromptVisible = visible;
    }
}

/// <summary>Optional prompt visibility contract for gated interactions.</summary>
public interface IInteractionPromptAvailability
{
    bool ShouldShowPrompt { get; }
}
