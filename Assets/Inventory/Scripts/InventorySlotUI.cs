using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    private InventoryItemData item;
    private Action<InventoryItemData> onSelected;
    private UnityAction clickAction;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    public void Setup(InventoryItemData itemData, Action<InventoryItemData> selectionCallback)
    {
        RemoveClickListener();

        item = itemData;
        onSelected = selectionCallback;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            iconImage.enabled = item != null && item.Icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
            nameText.text = item != null ? item.DisplayName : string.Empty;

        clickAction = SelectItem;
        button.onClick.AddListener(clickAction);
    }

    private void OnDestroy()
    {
        RemoveClickListener();
    }

    private void SelectItem()
    {
        if (item != null)
            onSelected?.Invoke(item);
    }

    private void RemoveClickListener()
    {
        if (button != null && clickAction != null)
            button.onClick.RemoveListener(clickAction);

        clickAction = null;
    }
}
