using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryDocumentPreview : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private Image previewImage;
    private InventoryUI inventoryUI;
    private Color originalColor = Color.white;
    private bool originalRaycastTarget;
    private bool isEligible;

    public void Configure(Image image, InventoryUI owner)
    {
        previewImage = image;
        inventoryUI = owner;
        if (previewImage != null)
        {
            originalColor = previewImage.color;
            originalRaycastTarget = previewImage.raycastTarget;
            previewImage.raycastTarget = originalRaycastTarget;
        }
    }

    public void SetEligible(bool eligible)
    {
        isEligible = eligible && previewImage != null;
        if (!isEligible)
            ResetHover();

        if (previewImage != null)
            previewImage.raycastTarget = isEligible || originalRaycastTarget;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isEligible || previewImage == null)
            return;

        previewImage.color = new Color(
            originalColor.r * 0.78f,
            originalColor.g * 0.78f,
            originalColor.b * 0.78f,
            originalColor.a);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEligible && eventData.button == PointerEventData.InputButton.Left)
            inventoryUI?.OpenSelectedDocumentPreview();
    }

    public void ResetHover()
    {
        if (previewImage != null)
            previewImage.color = originalColor;
    }
}
