using UnityEngine;

public class InspectableObject : MonoBehaviour
{
    [SerializeField] private ObjectViewer objectViewer;
    [SerializeField] private Sprite objectSprite;
    [SerializeField] private AlaalaInteractionSFX interactionSFX;

    private bool isOpen = false;

    private void Awake()
    {
        if (interactionSFX == null)
            interactionSFX = GetComponent<AlaalaInteractionSFX>();
    }

    public void Inspect()
    {
        if (objectViewer == null || objectSprite == null)
            return;

        if (isOpen)
        {
            Close();
        }
        else
        {
            objectViewer.OpenObject(objectSprite);
            isOpen = true;
            if (interactionSFX != null)
                interactionSFX.PlayAcceptedInteraction();
        }
    }

    public void Close()
    {
        if (objectViewer != null)
            objectViewer.CloseObject();

        isOpen = false;
    }
}
