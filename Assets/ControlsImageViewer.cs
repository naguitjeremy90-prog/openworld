using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ControlsImageViewer : MonoBehaviour
{
    [SerializeField] private Image previewImage;
    [SerializeField] private GameObject fullscreenViewer;
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject previewPlaceholder;

    private Button previewButton;

    private void Awake()
    {
        previewButton = GetComponent<Button>();

        previewButton.onClick.AddListener(OpenFullscreen);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseFullscreen);

        RefreshImages();
        CloseFullscreen();
    }

    private void OnDisable()
    {
        CloseFullscreen();
    }

    private void OnDestroy()
    {
        if (previewButton != null)
            previewButton.onClick.RemoveListener(OpenFullscreen);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseFullscreen);
    }

    public void OpenFullscreen()
    {
        RefreshImages();

        if (fullscreenViewer != null && previewImage != null && previewImage.sprite != null)
            fullscreenViewer.SetActive(true);
    }

    public void CloseFullscreen()
    {
        if (fullscreenViewer != null)
            fullscreenViewer.SetActive(false);
    }

    private void RefreshImages()
    {
        if (previewImage == null)
            return;

        previewImage.preserveAspect = true;

        bool hasSprite = previewImage.sprite != null;

        if (previewPlaceholder != null)
            previewPlaceholder.SetActive(!hasSprite);

        if (fullscreenImage != null)
        {
            fullscreenImage.sprite = previewImage.sprite;
            fullscreenImage.preserveAspect = true;
        }
    }
}
