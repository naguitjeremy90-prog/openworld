using GDS.Core;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Inventory2D : MonoBehaviour
{
    public ItemSO potionItem;

    [Header("Player")]
    public Transform playerTransform;

    [Header("UI")]
    public GameObject hotbarObj;
    public GameObject inventorySlotParent;
    public GameObject Container;
    public Image dragIcon;

    [Header("Pickup Settings")]
    public float pickUpRange = 3f;
    public Color highlightColor = Color.yellow;

    [Header("Key Bindings")]
    public KeyCode pickupKey = KeyCode.R;
    public KeyCode equipKey = KeyCode.Q;
    public KeyCode dropKey = KeyCode.F;

    private SpriteRenderer lookAtRenderer = null;
    private bool isInventoryOpen = false;

    private List<Slot> inventorySlots = new List<Slot>();
    private List<Slot> hotbarSlots = new List<Slot>();
    private List<Slot> allSlots = new List<Slot>();

    private Slot draggedSlot = null;
    private bool isDragging = false;

    private int equippedHotbarIndex = 0;
    public float equippedOpacity = 0.9f;
    public float normalOpacity = 0.58f;

    private void Awake()
{
        inventorySlots.Clear();
        hotbarSlots.Clear();
        allSlots.Clear();

        inventorySlots.AddRange(inventorySlotParent.GetComponentsInChildren<Slot>());
        hotbarSlots.AddRange(hotbarObj.GetComponentsInChildren<Slot>());

        allSlots.AddRange(inventorySlots);
        allSlots.AddRange(hotbarSlots);
    }

    private void Start()
    {
        // Start with cursor locked (gameplay mode)
        LockCursor();
    }

    private float detectInterval = 0.1f;
    private float detectTimer = 0f;

    private void Update()
    {
        if (ShopInteraction.IsShopOpen) return;

        // 🔄 Toggle Inventory
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            isInventoryOpen = !isInventoryOpen;
            Container.SetActive(isInventoryOpen);

            if (isInventoryOpen)
                UnlockCursor();
            else
                LockCursor();

            // Cancel drag if closing inventory
            if (!isInventoryOpen && isDragging)
            {
                dragIcon.enabled = false;
                draggedSlot = null;
                isDragging = false;
            }
        }

        // Optional: ESC always unlocks cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        // Gameplay actions ONLY when inventory closed
        if (!isInventoryOpen)
        {
            detectTimer -= Time.deltaTime;
            if (detectTimer <= 0)
            {
                DetectNearbyItem();
                detectTimer = detectInterval;
            }
            PickupItem();
        }

        // Inventory interactions ONLY when open
        if (isInventoryOpen)
        {
            StartDrag();
            UpdateDragItemPosition();
            EndDrag();
        }

        HandleHotBarSelection();
        HandleUseEquippedItem();
        HandleDropEquippedItem();
    }

    // 🔒 Cursor control
    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 🧠 NO STACKING
    public void AddItem(ItemSO itemToAdd, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            bool placed = false;

            foreach (Slot slot in hotbarSlots)
            {
                if (!slot.HasItem())
                {
                    slot.SetItem(itemToAdd, 1);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                foreach (Slot slot in inventorySlots)
                {
                    if (!slot.HasItem())
                    {
                        slot.SetItem(itemToAdd, 1);
                        placed = true;
                        break;
                    }
                }
            }

            if (!placed)
            {
                Debug.Log("Inventory Full!");
                return;
            }
        }
    }

    #region Drag & Drop
    private void StartDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Slot hovered = GetHoveredSlot();
            if (hovered != null && hovered.HasItem())
            {
                draggedSlot = hovered;
                isDragging = true;

                dragIcon.sprite = hovered.GetItem().icon;
                dragIcon.color = new Color(1, 1, 1, 0.5f);
                dragIcon.enabled = true;
            }
        }
    }

    private void EndDrag()
    {
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Slot hovered = GetHoveredSlot();

            if (hovered != null)
                HandleDrop(draggedSlot, hovered);

            dragIcon.enabled = false;
            draggedSlot = null;
            isDragging = false;
        }
    }

    private Slot GetHoveredSlot()
    {
        foreach (Slot s in allSlots)
        {
            if (s.hovering)
                return s;
        }
        return null;
    }

    private void HandleDrop(Slot from, Slot to)
    {
        if (from == to) return;

        if (to.HasItem())
        {
            ItemSO tempItem = to.GetItem();
            int tempAmount = to.GetAmount();

            to.SetItem(from.GetItem(), from.GetAmount());
            from.SetItem(tempItem, tempAmount);
        }
        else
        {
            to.SetItem(from.GetItem(), from.GetAmount());
            from.ClearSlot();
        }
    }

    private void UpdateDragItemPosition()
    {
        if (isDragging)
            dragIcon.transform.position = Input.mousePosition;
    }
    #endregion

    #region Pickup System
    private void DetectNearbyItem()
    {
        if (lookAtRenderer != null)
        {
            lookAtRenderer.color = Color.white;
            lookAtRenderer = null;
        }

        Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;
        Vector2 origin2D = new Vector2(origin.x, origin.y);
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin2D, pickUpRange);

        float closestDistance = Mathf.Infinity;
        SpriteRenderer closestRenderer = null;

        foreach (Collider2D col in hits)
        {
            Item item = col.GetComponent<Item>();
            if (item != null)
            {
                float distance = Vector2.Distance(origin2D, col.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRenderer = item.GetComponentInChildren<SpriteRenderer>();
                }
            }
        }

        if (closestRenderer != null)
        {
            lookAtRenderer = closestRenderer;
            lookAtRenderer.color = highlightColor;
        }
    }

    private void PickupItem()
    {
        if (lookAtRenderer != null && Input.GetKeyDown(pickupKey))
        {
            Item item = lookAtRenderer.GetComponentInParent<Item>();
            if (item != null)
            {
                AddItem(item.item, item.amount);
                Destroy(item.gameObject);
                lookAtRenderer = null;
            }
        }
    }
    #endregion

    #region Hotbar
    private void UpdateHotbarOpacity()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            Image icon = hotbarSlots[i].GetComponent<Image>();
            if (icon != null)
            {
                icon.color = (i == equippedHotbarIndex)
                    ? new Color(1, 1, 1, equippedOpacity)
                    : new Color(1, 1, 1, normalOpacity);
            }
        }
    }

    private void HandleHotBarSelection()
    {
        for (int i = 0; i < 6; i++)
        {
            if (Input.GetKeyDown((i + 1).ToString()))
            {
                equippedHotbarIndex = i;
                UpdateHotbarOpacity();
            }
        }
    }

    private void HandleUseEquippedItem()
    {
        if (Input.GetKeyDown(equipKey))
        {
            Slot equipped = hotbarSlots[equippedHotbarIndex];

            if (!equipped.HasItem()) return;

            if (ItemUser.Instance == null) return;

            ItemUser.Instance.UseItem(equipped.GetItem());

            equipped.ClearSlot();
            }
            }

    private void HandleDropEquippedItem()
    {
        if (!Input.GetKeyDown(dropKey)) return;

        Slot slot = hotbarSlots[equippedHotbarIndex];
        if (!slot.HasItem()) return;

        ItemSO itemSO = slot.GetItem();
        GameObject prefab = itemSO.itemPrefab;

        if (prefab == null) return;

        Vector3 dropPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
        GameObject dropped = Instantiate(prefab, dropPosition, Quaternion.identity);

        Item item = dropped.GetComponent<Item>();
        if (item != null)
        {
            item.item = itemSO;
            item.amount = slot.GetAmount();
        }

        slot.ClearSlot();
    }
    #endregion
}