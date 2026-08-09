using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public bool hovering;
    public ItemSO item;
    public int amount;
    private Image iconImage;
    private TextMeshProUGUI amountTxt;

    private void Awake()
    {
        iconImage = transform.GetChild(0).GetComponent<Image>();
        amountTxt = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
    }

    public bool HasItem()
    {
        return item != null && amount > 0;
    }

    public ItemSO GetItem()
    {
        return item;
    }

    public int GetAmount()
    {
        return amount;
    }

    public void SetItem(ItemSO newItem, int newAmount)
    {
        item = newItem;
        amount = newAmount;
        UpdateSlot();
    }

    public void UpdateSlot()
    {
        if (iconImage == null)
        {
            iconImage = transform.GetChild(0).GetComponent<Image>();
            amountTxt = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        }

        if (item != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = item.icon;
            amountTxt.text = amount.ToString();
        }
        else
        {
            iconImage.enabled = false;
            amountTxt.text = "";
        }
    }

    public void ClearSlot()
    {
        item = null;
        amount = 0;
        UpdateSlot();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
    }

    // RIGHT CLICK to use/equip item
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[Slot] Clicked! Button: " + eventData.button + " | Has item: " + HasItem());

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (HasItem() && ItemUser.Instance != null)
            {
                Debug.Log("[Slot] Using item: " + item.itemName);
                ItemUser.Instance.UseItem(item);
            }
            else
            {
                if (!HasItem()) Debug.LogWarning("[Slot] No item in slot!");
                if (ItemUser.Instance == null) Debug.LogWarning("[Slot] ItemUser.Instance is NULL!");
            }
        }
    }
}