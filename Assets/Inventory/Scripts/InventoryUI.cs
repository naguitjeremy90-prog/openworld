using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryUI : MonoBehaviour
{
    private enum InventoryFilter
    {
        All,
        Quest,
        Key,
        Document
    }

    [Header("Window")]
    [Tooltip("The visual overlay/panel. Keep this separate from the GameObject holding InventoryUI so the I key still works while hidden.")]
    [SerializeField] private GameObject inventoryRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;
    [SerializeField] private Button closeButton;

    [Header("Owned Item List")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField, Min(0)] private int visibleSlotCount = 8;

    [Header("Filters")]
    [SerializeField] private Button allTabButton;
    [SerializeField] private Button questItemsTabButton;
    [SerializeField] private Button keyItemsTabButton;
    [SerializeField] private Button documentsTabButton;
    [SerializeField] private Graphic allTabGraphic;
    [SerializeField] private Graphic questItemsTabGraphic;
    [SerializeField] private Graphic keyItemsTabGraphic;
    [SerializeField] private Graphic documentsTabGraphic;
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor =
        new Color(0.82f, 0.74f, 0.62f, 1f);

    [Header("Selected Item Details")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;

    [Header("Gameplay Input Blocking")]
    [Tooltip("Assign only gameplay input behaviours (movement, camera look, interaction) that should be disabled while this menu is open.")]
    [SerializeField] private MonoBehaviour[] gameplayBehavioursToDisable = new MonoBehaviour[0];

    private readonly List<InventorySlotUI> spawnedSlots = new List<InventorySlotUI>();
    private readonly List<InventoryItemData> filteredItems = new List<InventoryItemData>();

    private InventoryManager boundManager;
    private InventoryItemData selectedItem;
    private Coroutine fadeRoutine;
    private bool[] previousBehaviourStates;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool inputIsBlocked;
    private InventoryFilter currentFilter = InventoryFilter.All;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (inventoryRoot == null && canvasGroup != null)
            inventoryRoot = canvasGroup.gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseInventory);

        AddFilterListeners();
        UpdateTabVisuals();

        SetVisibleImmediately(false);
        ClearDetails();
    }

    private void OnEnable()
    {
        BindToManager();
    }

    private void OnDisable()
    {
        UnbindFromManager();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (inputIsBlocked)
            SetGameplayInputBlocked(false);

        IsOpen = false;
        SetVisibleImmediately(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseInventory);

        RemoveFilterListeners();
    }

    private void Update()
    {
        if (boundManager == null)
            BindToManager();

        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    public void ToggleInventory()
    {
        if (IsOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        BindToManager();
        Refresh();
        SetGameplayInputBlocked(true);
        FadeTo(1f, false);
    }

    public void CloseInventory()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        SetGameplayInputBlocked(false);
        FadeTo(0f, true);
    }

    public void Refresh()
    {
        ClearSlots();

        if (boundManager == null || slotContainer == null || slotPrefab == null)
            return;

        IReadOnlyList<InventoryItemData> ownedItems = boundManager.GetOwnedItems();
        filteredItems.Clear();

        for (int i = 0; i < ownedItems.Count; i++)
        {
            InventoryItemData item = ownedItems[i];
            if (item != null && MatchesCurrentFilter(item))
                filteredItems.Add(item);
        }

        int displayedSlotCount = Mathf.Max(visibleSlotCount, filteredItems.Count);

        for (int i = 0; i < displayedSlotCount; i++)
        {
            InventoryItemData item = i < filteredItems.Count ? filteredItems[i] : null;
            InventorySlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.Setup(item, SelectItem);
            spawnedSlots.Add(slot);
        }

        if (selectedItem != null &&
            (!boundManager.HasItem(selectedItem.ItemID) ||
             !MatchesCurrentFilter(selectedItem)))
        {
            ClearDetails();
        }
    }

    public void ShowAllItems()
    {
        SetFilter(InventoryFilter.All);
    }

    public void ShowQuestItems()
    {
        SetFilter(InventoryFilter.Quest);
    }

    public void ShowKeyItems()
    {
        SetFilter(InventoryFilter.Key);
    }

    public void ShowDocuments()
    {
        SetFilter(InventoryFilter.Document);
    }

    public void SelectItem(InventoryItemData item)
    {
        selectedItem = item;

        if (detailIcon != null)
        {
            detailIcon.sprite = item != null ? item.Icon : null;
            detailIcon.enabled = item != null && item.Icon != null;
            detailIcon.preserveAspect = true;
        }

        if (detailName != null)
            detailName.text = item != null ? item.DisplayName : string.Empty;

        if (detailDescription != null)
            detailDescription.text = item != null ? item.Description : string.Empty;
    }

    private void BindToManager()
    {
        if (boundManager == InventoryManager.Instance)
            return;

        UnbindFromManager();
        boundManager = InventoryManager.Instance;

        if (boundManager != null)
        {
            boundManager.OnInventoryChanged += HandleInventoryChanged;

            if (IsOpen)
                Refresh();
        }
    }

    private void UnbindFromManager()
    {
        if (boundManager != null)
            boundManager.OnInventoryChanged -= HandleInventoryChanged;

        boundManager = null;
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i].gameObject);
        }

        spawnedSlots.Clear();
    }

    private void ClearDetails()
    {
        selectedItem = null;
        SelectItem(null);
    }

    private void SetFilter(InventoryFilter filter)
    {
        currentFilter = filter;
        UpdateTabVisuals();

        if (IsOpen)
            Refresh();
    }

    private bool MatchesCurrentFilter(InventoryItemData item)
    {
        switch (currentFilter)
        {
            case InventoryFilter.Quest:
                return item.Category == ItemCategory.Quest;
            case InventoryFilter.Key:
                return item.Category == ItemCategory.Key;
            case InventoryFilter.Document:
                return item.Category == ItemCategory.Document;
            default:
                return true;
        }
    }

    private void AddFilterListeners()
    {
        allTabButton?.onClick.AddListener(ShowAllItems);
        questItemsTabButton?.onClick.AddListener(ShowQuestItems);
        keyItemsTabButton?.onClick.AddListener(ShowKeyItems);
        documentsTabButton?.onClick.AddListener(ShowDocuments);
    }

    private void RemoveFilterListeners()
    {
        allTabButton?.onClick.RemoveListener(ShowAllItems);
        questItemsTabButton?.onClick.RemoveListener(ShowQuestItems);
        keyItemsTabButton?.onClick.RemoveListener(ShowKeyItems);
        documentsTabButton?.onClick.RemoveListener(ShowDocuments);
    }

    private void UpdateTabVisuals()
    {
        SetTabColor(allTabGraphic, currentFilter == InventoryFilter.All);
        SetTabColor(questItemsTabGraphic, currentFilter == InventoryFilter.Quest);
        SetTabColor(keyItemsTabGraphic, currentFilter == InventoryFilter.Key);
        SetTabColor(documentsTabGraphic, currentFilter == InventoryFilter.Document);
    }

    private void SetTabColor(Graphic graphic, bool isActive)
    {
        if (graphic != null)
            graphic.color = isActive ? activeTabColor : inactiveTabColor;
    }

    private void FadeTo(float targetAlpha, bool deactivateAfterFade)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (inventoryRoot != null)
            inventoryRoot.SetActive(true);

        if (canvasGroup == null || fadeDuration <= 0f)
        {
            SetVisibleImmediately(targetAlpha > 0f);
            return;
        }

        canvasGroup.interactable = targetAlpha > 0f;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, deactivateAfterFade));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool deactivateAfterFade)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;

        if (deactivateAfterFade && inventoryRoot != null && inventoryRoot != gameObject)
            inventoryRoot.SetActive(false);
    }

    private void SetVisibleImmediately(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (inventoryRoot != null && inventoryRoot != gameObject)
            inventoryRoot.SetActive(visible);
    }

    private void SetGameplayInputBlocked(bool blocked)
    {
        if (inputIsBlocked == blocked)
            return;

        inputIsBlocked = blocked;

        if (blocked)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            previousBehaviourStates = new bool[gameplayBehavioursToDisable.Length];
            for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
            {
                MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
                if (behaviour == null || behaviour == this)
                    continue;

                previousBehaviourStates[i] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }
        else
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;

            if (previousBehaviourStates != null)
            {
                int count = Mathf.Min(gameplayBehavioursToDisable.Length, previousBehaviourStates.Length);
                for (int i = 0; i < count; i++)
                {
                    MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
                    if (behaviour != null && behaviour != this)
                        behaviour.enabled = previousBehaviourStates[i];
                }
            }

            previousBehaviourStates = null;
        }
    }
}
