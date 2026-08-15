using UnityEngine;
using UnityEngine.UI;

public class ObjectViewer : MonoBehaviour
{
    [SerializeField] private GameObject viewerPanel;
    [SerializeField] private Image objectImage;

    public void OpenObject(Sprite sprite)
    {
        objectImage.sprite = sprite;
        viewerPanel.SetActive(true);
    }

    public void CloseObject()
    {
        viewerPanel.SetActive(false);
    }
}