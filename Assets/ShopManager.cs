using UnityEngine;
using TMPro;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Items")]
    public ShopItemUI[] items;

    [Header("UI")]
    public TMP_Text totalText;
    public TMP_Text resultText;

    private Inventory2D inventory;
    private Coroutine resultCoroutine;

    void Start()
    {
        inventory = FindAnyObjectByType<Inventory2D>();
UpdateTotal();

        if (resultText != null)
            resultText.text = "";
    }

    public void UpdateTotal()
    {
        int total = 0;
        foreach (ShopItemUI item in items)
        {
            if (item != null && item.quantity > 0)
                total += item.price * item.quantity;
        }

        if (totalText != null)
            totalText.text = "Total: " + total + " Coins";
    }

    public void BuySelected()
    {
        int total = 0;
        foreach (ShopItemUI item in items)
        {
            if (item != null && item.quantity > 0)
                total += item.price * item.quantity;
        }

        if (total == 0)
        {
            if (resultText != null) resultText.text = "No items selected!";
            return;
        }

        // Use singleton
        if (!CurrencyManager.Instance.SpendCoins(total))
        {
            if (resultText != null) resultText.text = "Not enough coins!";
            return;
        }

        if (inventory != null)
        {
            foreach (ShopItemUI item in items)
            {
                if (item != null && item.quantity > 0 && item.itemSO != null)
                {
                    for (int i = 0; i < item.quantity; i++)
                        inventory.AddItem(item.itemSO, 1);

                    item.ResetQuantity();
                }
            }
        }

        UpdateTotal();

        if (resultText != null)
        {
            resultText.text = "Purchase successful!";
            if (resultCoroutine != null)
                StopCoroutine(resultCoroutine);

            resultCoroutine = StartCoroutine(HideResultText());
        }
    }

    IEnumerator HideResultText()
    {
        yield return new WaitForSeconds(1.5f);

        if (resultText != null)
        {
            resultText.text = "";
            resultText.gameObject.SetActive(false);
        }
    }

    public void ClearAllSelections()
    {
        foreach (ShopItemUI item in items)
            item?.ResetQuantity();

        UpdateTotal();

        if (resultText != null)
            resultText.text = "";
    }
}