using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [SerializeField] private GameObject interactText;
    [SerializeField] private UnityEvent onInteract;
    [SerializeField] private UnityEvent onExitInteraction;

    private bool playerNear = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (interactText != null)
                interactText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;

            if (interactText != null)
                interactText.SetActive(false);

            onExitInteraction.Invoke();
        }
    }

    private void Update()
    {
        if (playerNear && Input.GetKeyDown(KeyCode.E))
        {
            if (interactText != null)
                interactText.SetActive(false);

            onInteract.Invoke();
        }
    }
}