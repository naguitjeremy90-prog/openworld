using UnityEngine;

public class InspectableObject : MonoBehaviour
{
    [SerializeField] private ObjectViewer objectViewer;
    [SerializeField] private Sprite objectSprite;

    private bool isOpen = false;

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
        }
    }

    public void Close()
    {
        if (objectViewer != null)
            objectViewer.CloseObject();

        isOpen = false;
    }
}