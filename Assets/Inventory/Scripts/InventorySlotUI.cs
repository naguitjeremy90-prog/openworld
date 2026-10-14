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
    [SerializeField] private GameObject unreadIndicator;

    private InventoryItemData item;
    private Action<InventoryItemData> onSelected;
    private UnityAction clickAction;
    private InventoryNotificationPulse unreadPulse;

    public InventoryItemData Item => item;
    public RectTransform RectTransform => transform as RectTransform;

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

        if (unreadIndicator == null)
        {
            Transform indicator = transform.Find("InventoryUnreadIndicator");
            if (indicator != null) unreadIndicator = indicator.gameObject;
        }
        if (unreadIndicator != null)
        {
            unreadPulse = unreadIndicator.GetComponent<InventoryNotificationPulse>();
            RectTransform indicatorRect = unreadIndicator.transform as RectTransform;
            if (indicatorRect != null)
            {
                indicatorRect.anchorMin = indicatorRect.anchorMax = Vector2.one;
                indicatorRect.pivot = Vector2.one;
                indicatorRect.anchoredPosition = new Vector2(-3f, -3f);
                indicatorRect.sizeDelta = new Vector2(67.85542f, 67.85542f);
                indicatorRect.localScale = Vector3.one * 0.35f;
            }
            if (unreadPulse != null)
            {
                unreadPulse.SetBaselineScale(indicatorRect != null ? indicatorRect.localScale : Vector3.one);
                unreadPulse.SetVisible(item != null && InventoryItemReadState.IsUnread(item.ItemID));
            }
        }

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
