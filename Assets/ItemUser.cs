using UnityEngine;

public class ItemUser : MonoBehaviour
{
    public static ItemUser Instance;

    [Header("Hand / Equip Point")]
    public Transform handPoint;

    [Header("Flashlight")]
    public Light flashlight;

    [Header("Player Stats")]
    public PlayerStats playerStats;

    private GameObject currentHandItem;
    private ItemSO currentItem;
    private bool flashlightOn = false;

    private void Awake()
    {
        Instance = this;
    }

    public void UseItem(ItemSO item)
    {
        if (item == null) return;

        currentItem = item;

        switch (item.itemType)
        {
            case ItemType.Flashlight:
                ToggleFlashlight(item);
                break;

            case ItemType.Potion:
                UsePotion(item);
                break;

            case ItemType.Weapon:
                EquipToHand(item);
                break;

            case ItemType.Default:
            default:
                Debug.Log("[ItemUser] No use action for: " + item.itemName);
                break;
        }
    }

    // ------------------------------------------------
    // FLASHLIGHT
    // ------------------------------------------------
    private void ToggleFlashlight(ItemSO item)
    {
        flashlightOn = !flashlightOn;

        if (flashlight != null)
        {
            flashlight.enabled = flashlightOn;
            Debug.Log("[ItemUser] Flashlight: " + (flashlightOn ? "ON" : "OFF"));
        }
        else
        {
            Debug.LogWarning("[ItemUser] No flashlight Light component assigned!");
        }

        if (flashlightOn)
            EquipToHand(item);
        else
            UnequipHand();
    }

    // ------------------------------------------------
    // POTION
    // ------------------------------------------------
    private void UsePotion(ItemSO item)
    {
        if (playerStats == null)
        {
            Debug.LogWarning("[ItemUser] PlayerStats is not assigned!");
            return;
        }

        Debug.Log("[ItemUser] Used potion: " + item.itemName);

        if (item.itemName == "Elixir ng Buhay")
        {
            Debug.Log("Matched Elixir ng Buhay");
            playerStats.UseElixirNgBuhay();
        }
        else if (item.itemName == "Elixir ng Hangin")
        {
            Debug.Log("Matched Elixir ng Hangin");
            playerStats.UseElixirNgHangin();
        }
        else if (item.itemName == "Elixir ng Liwanag")
        {
            Debug.Log("Matched Elixir ng Liwanag");
            playerStats.UseElixirNgLiwanag();
        }
        else if (item.itemName == "Elixir ng Tibay")
        {
            Debug.Log("Matched Elixir ng Tibay");
            playerStats.UseElixirNgTibay();
        }
        else if (item.itemName == "Seresa ng Lunas")
        {
            Debug.Log("Matched Seresa ng Lunas");
            playerStats.UseSeresaNgLunas();
        }
        else if (item.itemName == "Ubas ng Galaw")
        {
            Debug.Log("Matched Ubas ng Galaw");
            playerStats.UseUbasNgGalaw();
        }
        else if (item.itemName == "Pakwan ng Ginhawan")
        {
            Debug.Log("Matched Pakwan ng Ginhawan");
            playerStats.UsePakwanNgGinhawan();
        }
        else if (item.itemName == "Elixir ng Pagkain")
        {
            playerStats.UseElixirNgPagkain();
        }
        else if (item.itemName == "Kahel ng Lakas")
        {
            Debug.Log("Matched Kahel ng Lakas");
            playerStats.UseKahelNgLakas();
        }
        else
        {
            Debug.LogWarning("[ItemUser] Unknown potion item: " + item.itemName);
        }
    }

    // ------------------------------------------------
    // EQUIP TO HAND
    // ------------------------------------------------
    private void EquipToHand(ItemSO item)
    {
        UnequipHand();

        if (item.handItemPrefab != null && handPoint != null)
        {
            currentHandItem = Instantiate(item.handItemPrefab, handPoint.position, handPoint.rotation, handPoint);
            Debug.Log("[ItemUser] Equipped to hand: " + item.itemName);
        }
        else
        {
            if (handPoint == null) Debug.LogWarning("[ItemUser] No hand point assigned!");
            if (item.handItemPrefab == null) Debug.LogWarning("[ItemUser] No handItemPrefab on: " + item.itemName);
        }
    }

    private void UnequipHand()
    {
        if (currentHandItem != null)
        {
            Destroy(currentHandItem);
            currentHandItem = null;
        }
    }

    // ------------------------------------------------
    // UNEQUIP — called when dropping an equipped item
    // ------------------------------------------------
    public void UnequipItem()
    {
        flashlightOn = false;

        if (flashlight != null)
            flashlight.enabled = false;

        UnequipHand();
        currentItem = null;

        Debug.Log("[ItemUser] Item unequipped and light turned off");
    }
}