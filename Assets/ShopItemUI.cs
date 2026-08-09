using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ShopItemUI : MonoBehaviour
{
    [Header("Item Info")]
    public string itemName;
    public int price;
    public ItemSO itemSO;

    [Header("UI References")]
    public Image background;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public TMP_Text descriptionText;
    public TMP_Text basketText;
    public TMP_Text quantityText;

    [HideInInspector] public int quantity = 0;
    [HideInInspector] public ShopManager shopManager;

    void Start()
    {
        if (shopManager == null)
            shopManager = FindAnyObjectByType<ShopManager>();

        if (nameText != null)
            nameText.text = itemName;

        if (priceText != null)
            priceText.text = price.ToString();

        if (basketText != null)
            basketText.gameObject.SetActive(false);

        UpdateVisual();
        UpdateQuantityText();
    }

    public void AddQuantity()
    {
        quantity++;
        UpdateVisual();
        UpdateQuantityText();

        if (shopManager != null)
            shopManager.UpdateTotal();

        if (basketText != null)
        {
            basketText.gameObject.SetActive(false);
            basketText.text = "";
            basketText.gameObject.SetActive(true);
            basketText.text = "Added " + itemName;

            StopCoroutine("HideBasketText");
            StartCoroutine("HideBasketText");
        }
    }

    public void RemoveQuantity()
    {
        if (quantity <= 0)
            return;

        quantity--;
        UpdateVisual();
        UpdateQuantityText();

        if (shopManager != null)
            shopManager.UpdateTotal();

        if (basketText != null)
        {
            basketText.gameObject.SetActive(false);
            basketText.text = "";
            basketText.gameObject.SetActive(true);
            basketText.text = "Removed " + itemName;

            StopCoroutine("HideBasketText");
            StartCoroutine("HideBasketText");
        }
    }

    public void ResetQuantity()
    {
        quantity = 0;
        UpdateVisual();
        UpdateQuantityText();
    }

    public void UpdateVisual()
    {
        if (background != null)
        {
            background.color = quantity > 0
                ? new Color(0.7f, 1f, 0.7f, 1f)
                : Color.white;
        }
    }

    public void UpdateQuantityText()
    {
        if (quantityText != null)
            quantityText.text = quantity > 0 ? "Quantity: " + quantity : "Quantity:";
    }

    IEnumerator HideBasketText()
    {
        yield return new WaitForSeconds(1.5f);

        if (basketText != null)
        {
            basketText.text = "";
            basketText.gameObject.SetActive(false);
        }
    }
}