using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryUI : MonoBehaviour
{
    private const int FullInterfaceSortingOrder = 300;
    private const int WindowSortingOrder = 301;
    private const int SlotsPerPage = 20;
    private const string InventoryAudioConfigResourcePath = "InventoryAudioConfig";
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

    [Header("Paged Presentation")]
    [SerializeField] private bool enablePagination;
    [SerializeField] private GameObject pageControlsRoot;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageNumberText;

    private int currentPageIndex;

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

    [Header("Inventory UI Audio")]
    [SerializeField] private AudioSource inventoryAudioSource;

    [Header("Story Sequence Behavior")]
    [Tooltip("When enabled, an inventory open request made during a story sequence waits until the sequence ends. Keep disabled unless this scene requires it.")]
    [SerializeField] private bool deferOpenUntilStorySequenceEnds;
    private bool openPendingAfterStorySequence;

    private readonly List<InventorySlotUI> spawnedSlots = new List<InventorySlotUI>();
    private readonly List<InventoryItemData> filteredItems = new List<InventoryItemData>();

    private InventoryManager boundManager;
    private InventoryItemData selectedItem;
    private InventoryDocumentViewer documentViewer;
    private InventoryDocumentPreview documentPreview;
    private Coroutine fadeRoutine;
    private Coroutine inventoryRevealRoutine;
    private GameplayHUDUnlockReveal inventoryReveal;
    private bool inventoryRevealScheduled;
    private bool ownsInventoryReveal;
    private bool inventoryRevealNeedsRestore;
    private Vector3 inventoryIconNormalScale;
    private float inventoryIconNormalAlpha;
    private bool inventoryIconNormalInteractable;
    private bool inventoryIconNormalBlocksRaycasts;
    private bool[] previousBehaviourStates;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool inputIsBlocked;
    private InventoryFilter currentFilter = InventoryFilter.All;
    private InventoryAudioConfig inventoryAudioConfig;
    private AudioClip lastTabSwitchClip;

    public bool IsOpen { get; private set; }
    public event Action<InventoryUI> InventoryClosed;
    public InventoryItemData SelectedItem => selectedItem;
    public InventoryDocumentViewer DocumentViewer => documentViewer;
    public bool IsTutorialUIAvailable => isActiveAndEnabled && inventoryRoot != null &&
        canvasGroup != null && slotContainer != null && slotPrefab != null &&
        (inventoryRoot.transform.parent == null || inventoryRoot.transform.parent.gameObject.activeInHierarchy);
    public bool IsWindowTransitioning => fadeRoutine != null;

    public RectTransform TutorialWindow => inventoryRoot != null ? inventoryRoot.transform as RectTransform : null;

    public RectTransform GetTutorialTabTarget(int index)
    {
        Button button = GetTutorialTabButton(index);
        return button != null && button.isActiveAndEnabled && button.IsInteractable()
            ? button.transform as RectTransform : null;
    }

    public void SelectTutorialTab(int index)
    {
        Button button = GetTutorialTabButton(index);
        if (GetTutorialTabTarget(index) == null) return;
        if ((int)currentFilter != index) button.onClick.Invoke();
    }

    private Button GetTutorialTabButton(int index)
    {
        switch (index)
        {
            case 0: return allTabButton;
            case 1: return questItemsTabButton;
            case 2: return keyItemsTabButton;
            case 3: return documentsTabButton;
            default: return null;
        }
    }

    public bool HasTutorialItem
    {
        get
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return false;
            foreach (var item in manager.GetOwnedItems())
                if (item != null && !string.IsNullOrWhiteSpace(item.ItemID)) return true;
            return false;
        }
    }

    public bool TryGetTutorialItemTarget(out RectTransform target)
    {
        target = null;
        if (!IsOpen || !IsTutorialUIAvailable || boundManager == null) return false;
        foreach (var item in boundManager.GetOwnedItems())
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemID) || !boundManager.HasItem(item.ItemID))
                continue;
            if (!TryGetItemSlot(item.ItemID, out target))
            {
                if (GetTutorialTabTarget(0) == null) return false;
                SelectTutorialTab(0);
                if (!TryGetItemSlot(item.ItemID, out target)) continue;
            }
            if (target != null && target.gameObject.activeInHierarchy &&
                target.GetComponent<Button>() is Button button && button.isActiveAndEnabled && button.IsInteractable())
                return true;
        }
        target = null;
        return false;
    }

    public bool TryGetItemSlot(string itemID, out RectTransform slotRect)
    {
        slotRect = null;
        if (string.IsNullOrEmpty(itemID))
            return false;

        // Tutorial requests must reveal an owned item even when it is on another page.
        if (enablePagination && IsOpen)
        {
            int itemIndex = filteredItems.FindIndex(item => item != null &&
                string.Equals(item.ItemID, itemID, System.StringComparison.OrdinalIgnoreCase));
            if (itemIndex >= 0 && currentPageIndex != itemIndex / SlotsPerPage)
                SelectPage(itemIndex / SlotsPerPage);
        }

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            InventorySlotUI slot = spawnedSlots[i];
            if (slot == null || slot.Item == null || slot.RectTransform == null)
                continue;

            if (string.Equals(slot.Item.ItemID, itemID, System.StringComparison.OrdinalIgnoreCase))
            {
                slotRect = slot.RectTransform;
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        inventoryAudioConfig = Resources.Load<InventoryAudioConfig>(InventoryAudioConfigResourcePath);
        ConfigureInventoryAudioSource();

        Canvas inventoryCanvas = GetComponentInParent<Canvas>();
        if (inventoryCanvas != null)
        {
            // Keep the full-screen inventory system UI explicitly above the
            // normal gameplay HUD/task canvas. This is a visual Canvas rule;
            // task visibility and gameplay state remain unchanged.
            inventoryCanvas.overrideSorting = true;
            inventoryCanvas.sortingOrder = FullInterfaceSortingOrder;
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (inventoryRoot == null && canvasGroup != null)
            inventoryRoot = canvasGroup.gameObject;

        ConfigureWindowCanvas();
        ConfigureDocumentPreview();

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseInventory);

        AddFilterListeners();
        previousPageButton?.onClick.AddListener(ShowPreviousPage);
        nextPageButton?.onClick.AddListener(ShowNextPage);
        UpdatePageControls();
        UpdateTabVisuals();

        SetVisibleImmediately(false);
        ClearDetails();

        GameplayHUDTarget target = inventoryRoot != null
            ? inventoryRoot.GetComponent<GameplayHUDTarget>()
            : null;
        if (target == null)
            target = GetComponent<GameplayHUDTarget>();
        if (target == null)
            target = (inventoryRoot != null ? inventoryRoot : gameObject).AddComponent<GameplayHUDTarget>();
        target.Configure(canvasGroup, inventoryRoot);

        ResolveInventoryHUD();
    }

    private void ConfigureWindowCanvas()
    {
        if (inventoryRoot == null)
            return;

        // A nested Canvas inherits its parent's render mode without changing layout.
        Canvas windowCanvas = inventoryRoot.GetComponent<Canvas>();
        if (windowCanvas == null)
            windowCanvas = inventoryRoot.AddComponent<Canvas>();
        windowCanvas.enabled = true;
        windowCanvas.overrideSorting = true;
        windowCanvas.sortingLayerName = "Default";
        windowCanvas.sortingOrder = WindowSortingOrder;

        GraphicRaycaster raycaster = inventoryRoot.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = inventoryRoot.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
    }

    private void ResolveInventoryHUD()
    {
        InventoryHUDItemAttention.Instance?.ConfigureTabBadges(
            this, allTabButton, questItemsTabButton, keyItemsTabButton, documentsTabButton);
        GameObject icon = inventoryReveal != null && inventoryReveal.gameObject.scene == gameObject.scene
            ? inventoryReveal.gameObject
            : null;
        if (icon == null)
        {
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
                if (rect.name == "InventoryButton" && rect.gameObject.scene == gameObject.scene)
                {
                    icon = rect.gameObject;
                    break;
                }
        }
        if (icon != null && icon != gameObject)
        {
            InventoryHUDItemAttention.Instance?.Configure(this, icon.transform as RectTransform);
            if (inventoryReveal != null && inventoryReveal.gameObject == icon)
            {
                if (isActiveAndEnabled)
                {
                    inventoryReveal.RevealCompleted -= HandleInventoryRevealCompleted;
                    inventoryReveal.RevealCompleted += HandleInventoryRevealCompleted;
                }
                return;
            }
            GameplayHUDTarget iconTarget = icon.GetComponent<GameplayHUDTarget>();
            if (iconTarget == null)
                iconTarget = icon.AddComponent<GameplayHUDTarget>();
            CanvasGroup iconGroup = icon.GetComponent<CanvasGroup>();
            if (iconGroup == null)
                iconGroup = icon.AddComponent<CanvasGroup>();
            iconTarget.Configure(iconGroup, icon);

            if (inventoryReveal == null)
            {
                inventoryReveal = icon.GetComponent<GameplayHUDUnlockReveal>();
                if (inventoryReveal == null)
                    inventoryReveal = icon.AddComponent<GameplayHUDUnlockReveal>();
                iconTarget.GetNormalPresentationState(out float normalAlpha, out bool normalInteractable, out bool normalBlocksRaycasts);
                inventoryReveal.ConfigureNormalState(iconGroup, normalAlpha, normalInteractable, normalBlocksRaycasts);
            }
            GameplaySystemTutorialAnchor.AttachTo(icon, GameplaySystemId.Inventory);
            if (isActiveAndEnabled)
            {
                inventoryReveal.RevealCompleted -= HandleInventoryRevealCompleted;
                inventoryReveal.RevealCompleted += HandleInventoryRevealCompleted;
            }
        }
    }

    private void OnEnable()
    {
        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        ResolveInventoryHUD();
        if (inventoryReveal != null)
        {
            inventoryReveal.RevealCompleted -= HandleInventoryRevealCompleted;
            inventoryReveal.RevealCompleted += HandleInventoryRevealCompleted;
        }
        BindToManager();
        if (GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory)) BeginInventoryReveal();
    }

    private void OnDisable()
    {
        documentViewer?.CloseSilently();
        documentPreview?.ResetHover();
        openPendingAfterStorySequence = false;
        InventoryHUDItemAttention.Instance?.Detach(this);
        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;
        UnbindFromManager();

        if (inventoryReveal != null)
            inventoryReveal.RevealCompleted -= HandleInventoryRevealCompleted;

        if (inventoryRevealRoutine != null)
        {
            StopCoroutine(inventoryRevealRoutine);
            inventoryRevealRoutine = null;
        }
        CancelOwnedInventoryReveal();
        inventoryRevealScheduled = false;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (inputIsBlocked)
            SetGameplayInputBlocked(false);

        IsOpen = false;
        if (GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.NotifySystemClosed(GameplaySystemId.Inventory);
        SetVisibleImmediately(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseInventory);

        RemoveFilterListeners();
        previousPageButton?.onClick.RemoveListener(ShowPreviousPage);
        nextPageButton?.onClick.RemoveListener(ShowNextPage);
    }

    // Scene-local presentation owners can block I without disabling this UI or its backend.
    public bool GameplayShortcutSuppressed { get; set; }

    private void Update()
    {
        bool inventoryInputDetected = !GameplayShortcutSuppressed && Input.GetKeyDown(KeyCode.I);

        // A disabled/destroyed HUD root stops its own reveal coroutine without a completion event.
        if (ownsInventoryReveal && (inventoryReveal == null || !inventoryReveal.isActiveAndEnabled))
        {
            CancelOwnedInventoryReveal();
            inventoryRevealScheduled = false;
        }
        if (!inventoryRevealScheduled && GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory))
            BeginInventoryReveal();
        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            if (IsOpen)
                CloseInventory();
            else if (deferOpenUntilStorySequenceEnds && inventoryInputDetected)
                openPendingAfterStorySequence = !openPendingAfterStorySequence;
            return;
        }

        if (documentViewer != null && documentViewer.IsOpen)
            return;

        if (ReconstructionJournalManager.IsPagninilayResponseInputFocusedForGameplay)
            return;

        if (openPendingAfterStorySequence)
        {
            openPendingAfterStorySequence = false;
            OpenInventory();
            return;
        }

        if (boundManager == null)
            BindToManager();

        if (inventoryInputDetected)
            ToggleInventory();
    }

    public void ToggleInventory()
    {
        if (!GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory))
            return;

        if (deferOpenUntilStorySequenceEnds && StorySequenceCoordinator.IsStorySequenceActive)
        {
            if (IsOpen)
            {
                openPendingAfterStorySequence = false;
                CloseInventory();
            }
            else
            {
                openPendingAfterStorySequence = !openPendingAfterStorySequence;
            }
            return;
        }

        if (IsOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        OpenInventoryInternal(true);
    }

    /// <summary>Opens Inventory for an acquired item without replaying the Inventory tutorial.</summary>
    public bool OpenInventoryAndSelectItem(InventoryItemData item)
    {
        if (item == null)
            return false;

        if (!IsOpen && deferOpenUntilStorySequenceEnds && StorySequenceCoordinator.IsStorySequenceActive)
            return false;

        if (!IsOpen && !OpenInventoryInternal(false))
            return false;

        if (boundManager == null)
            BindToManager();
        if (boundManager == null || !boundManager.HasItem(item.ItemID))
            return false;

        if (!MatchesCurrentFilter(item))
        {
            if (item.Category == ItemCategory.Document)
                ShowDocuments();
            else
                ShowAllItems();
        }

        int itemIndex = filteredItems.FindIndex(candidate =>
            candidate != null && candidate.ItemID == item.ItemID);
        if (itemIndex < 0)
            return false;

        if (enablePagination && currentPageIndex != itemIndex / SlotsPerPage)
        {
            currentPageIndex = itemIndex / SlotsPerPage;
            Refresh();
        }

        SelectItem(item);
        return true;
    }

    private bool OpenInventoryInternal(bool notifyTutorial)
    {
        if (IsOpen ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory))
            return IsOpen;

        if (deferOpenUntilStorySequenceEnds && StorySequenceCoordinator.IsStorySequenceActive)
        {
            openPendingAfterStorySequence = true;
            return false;
        }

        IsOpen = true;
        PlayInventorySound(inventoryAudioConfig != null ? inventoryAudioConfig.InventoryOpenSound : null);
        // Activate the hidden slot container before constructing slots so their
        // Awake methods run before Setup assigns presentation state.
        if (inventoryRoot != null)
            inventoryRoot.SetActive(true);
        BindToManager();
        Refresh();
        SetGameplayInputBlocked(true);
        FadeTo(1f, false);
        if (notifyTutorial && GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.NotifySystemOpened(GameplaySystemId.Inventory);
        return true;
    }

    public void CloseInventory()
    {
        if (!IsOpen)
            return;

        documentViewer?.Close();
        documentPreview?.ResetHover();
        IsOpen = false;
        PlayInventorySound(inventoryAudioConfig != null ? inventoryAudioConfig.InventoryCloseSound : null);
        SetGameplayInputBlocked(false);
        FadeTo(0f, true);
        if (GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.NotifySystemClosed(GameplaySystemId.Inventory);
        InventoryClosed?.Invoke(this);
    }

    private void HandleSystemUnlockChanged(
        GameplaySystemId system,
        bool unlocked)
    {
        if (system == GameplaySystemId.Inventory && !unlocked)
            openPendingAfterStorySequence = false;

        if (system == GameplaySystemId.Inventory && !unlocked && IsOpen)
            CloseInventory();

        if (system == GameplaySystemId.Inventory && unlocked)
            BeginInventoryReveal();
    }

    private void BeginInventoryReveal()
    {
        if (inventoryRevealScheduled)
            return;

        inventoryRevealScheduled = true;
        if (GameplaySystemTutorialState.InventoryRevealReady)
        {
            HandleInventoryRevealCompleted();
            return;
        }
        inventoryRevealRoutine = StartCoroutine(PlayInventoryRevealWhenSequenceEnds());
    }

    private IEnumerator PlayInventoryRevealWhenSequenceEnds()
    {
        // Wait one frame for scene-local Awake/OnEnable and lock views to finish binding.
        yield return null;
        while (isActiveAndEnabled)
        {
            ResolveInventoryHUD();
            if (inventoryRevealNeedsRestore && inventoryReveal != null && inventoryReveal.isActiveAndEnabled)
                yield return RestoreInterruptedInventoryHUD();
            var tutorial = GameplaySystemTutorialManager.Instance;
            if (inventoryReveal != null && inventoryReveal.isActiveAndEnabled &&
                tutorial != null && tutorial.IsInventoryPresentationEnvironmentSafe()) break;
            yield return null;
        }

        inventoryRevealRoutine = null;
        if (!isActiveAndEnabled) yield break;
        if (GameplaySystemTutorialState.InventoryRevealStarted)
        {
            // An interrupted attempt has already introduced the HUD. Recover readiness without replaying it.
            HandleInventoryRevealCompleted();
            yield break;
        }
        PlayInventoryReveal();
    }

    private void PlayInventoryReveal()
    {
        var rect = inventoryReveal != null ? inventoryReveal.transform as RectTransform : null;
        var group = inventoryReveal != null ? inventoryReveal.GetComponent<CanvasGroup>() : null;
        if (rect != null && group != null)
        {
            inventoryIconNormalScale = rect.localScale;
            inventoryIconNormalAlpha = group.alpha;
            inventoryIconNormalInteractable = group.interactable;
            inventoryIconNormalBlocksRaycasts = group.blocksRaycasts;
        }
        if (rect != null && group != null && inventoryReveal.PlayReveal())
        {
            ownsInventoryReveal = true;
            GameplaySystemTutorialState.InventoryRevealStarted = true;
            return;
        }

        inventoryRevealScheduled = false;

        Debug.LogWarning(
            "Inventory unlock reveal could not start because the Inventory HUD icon is unavailable.",
            this);
    }

    private void HandleInventoryRevealCompleted()
    {
        ownsInventoryReveal = false;
        GameplaySystemTutorialState.InventoryRevealReady = true;
        GameplaySystemTutorialManager.Instance.NotifySystemRevealCompleted(
            GameplaySystemId.Inventory);
    }

    private void CancelOwnedInventoryReveal()
    {
        if (!ownsInventoryReveal) return;
        ownsInventoryReveal = false;
        if (inventoryReveal == null) return;
        inventoryReveal.StopAllCoroutines();
        inventoryReveal.transform.localScale = inventoryIconNormalScale;
        inventoryRevealNeedsRestore = true;
        var group = inventoryReveal.GetComponent<CanvasGroup>();
        if (group == null) return;
        bool hidden = StorySequenceCoordinator.IsStorySequenceActive;
        group.alpha = hidden ? 0f : inventoryIconNormalAlpha;
        group.interactable = !hidden && inventoryIconNormalInteractable;
        group.blocksRaycasts = !hidden && inventoryIconNormalBlocksRaycasts;
    }

    private IEnumerator RestoreInterruptedInventoryHUD()
    {
        // Let existing HUD suppression/restoration finish before restoring our captured normal state.
        // Observe settling rather than assuming the HUD's configured fade duration.
        float previousAlpha = float.NaN;
        int stableFrames = 0;
        while (inventoryReveal != null)
        {
            var group = inventoryReveal.GetComponent<CanvasGroup>();
            if (group == null) break;
            if (StorySequenceCoordinator.IsStorySequenceActive || !inventoryReveal.isActiveAndEnabled)
                stableFrames = 0;
            else
            {
                stableFrames = group.alpha == previousAlpha ? stableFrames + 1 : 0;
                if (stableFrames >= 2)
                {
                    inventoryReveal.transform.localScale = inventoryIconNormalScale;
                    group.alpha = inventoryIconNormalAlpha;
                    group.interactable = inventoryIconNormalInteractable;
                    group.blocksRaycasts = inventoryIconNormalBlocksRaycasts;
                    break;
                }
            }
            previousAlpha = group.alpha;
            yield return null;
        }
        inventoryRevealNeedsRestore = false;
    }

    public void Refresh()
    {
        ClearSlots();

        if (boundManager == null || slotContainer == null || slotPrefab == null)
        {
            filteredItems.Clear();
            currentPageIndex = 0;
            UpdatePageControls();
            return;
        }

        IReadOnlyList<InventoryItemData> ownedItems = boundManager.GetOwnedItems();
        filteredItems.Clear();

        for (int i = 0; i < ownedItems.Count; i++)
        {
            InventoryItemData item = ownedItems[i];
            if (item != null && MatchesCurrentFilter(item))
                filteredItems.Add(item);
        }

        currentPageIndex = enablePagination
            ? Mathf.Clamp(currentPageIndex, 0, GetPageCount() - 1)
            : 0;
        int firstItemIndex = enablePagination ? currentPageIndex * SlotsPerPage : 0;
        int displayedSlotCount = enablePagination
            ? SlotsPerPage
            : Mathf.Max(visibleSlotCount, filteredItems.Count);

        for (int i = 0; i < displayedSlotCount; i++)
        {
            int itemIndex = firstItemIndex + i;
            InventoryItemData item = itemIndex < filteredItems.Count ? filteredItems[itemIndex] : null;
            InventorySlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.Setup(item, SelectItem);
            spawnedSlots.Add(slot);
        }

        UpdatePageControls();
        if (enablePagination && slotContainer is RectTransform slotRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotRect);

        if (selectedItem != null &&
            (!boundManager.HasItem(selectedItem.ItemID) ||
             !MatchesCurrentFilter(selectedItem)))
        {
            ClearDetails();
        }
    }

    private int GetPageCount()
    {
        return Mathf.Max(1, filteredItems.Count / SlotsPerPage +
            (filteredItems.Count % SlotsPerPage == 0 ? 0 : 1));
    }

    public void ShowPreviousPage()
    {
        SelectPage(currentPageIndex - 1);
    }

    public void ShowNextPage()
    {
        SelectPage(currentPageIndex + 1);
    }

    private void SelectPage(int pageIndex)
    {
        if (!enablePagination || !IsOpen)
            return;

        int validPageIndex = Mathf.Clamp(pageIndex, 0, GetPageCount() - 1);
        if (validPageIndex == currentPageIndex)
            return;

        currentPageIndex = validPageIndex;
        Refresh();
    }

    private void UpdatePageControls()
    {
        int pageCount = GetPageCount();
        bool showControls = enablePagination && pageCount > 1;
        if (pageControlsRoot != null)
            pageControlsRoot.SetActive(showControls);
        if (previousPageButton != null)
            previousPageButton.interactable = showControls && currentPageIndex > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = showControls && currentPageIndex < pageCount - 1;
        if (pageNumberText != null)
            pageNumberText.text = $"{currentPageIndex + 1} / {pageCount}";
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
        if (selectedItem != item)
            documentViewer?.Close();
        documentPreview?.ResetHover();
        selectedItem = item;

        if (detailIcon != null)
        {
            detailIcon.sprite = item != null ? item.Icon : null;
            detailIcon.enabled = item != null && item.Icon != null;
            detailIcon.preserveAspect = true;
        }

        documentPreview?.SetEligible(
            item != null &&
            item.Category == ItemCategory.Document &&
            item.Icon != null);

        if (detailName != null)
            detailName.text = item != null ? item.DisplayName : string.Empty;

        if (detailDescription != null)
            detailDescription.text = item != null ? item.Description : string.Empty;

        if (item != null && (detailIcon != null || detailName != null || detailDescription != null))
        {
            if (InventoryItemReadState.MarkRead(item.ItemID))
                Refresh();
        }
    }

    public void OpenSelectedDocumentPreview()
    {
        if (!IsOpen ||
            selectedItem == null ||
            selectedItem.Category != ItemCategory.Document ||
            selectedItem.Icon == null ||
            documentViewer == null)
        {
            return;
        }

        documentPreview?.ResetHover();
        Sprite[] storyPages = selectedItem.StoryPages;
        if (storyPages != null && storyPages.Length > 0)
            documentViewer.OpenPages(storyPages, selectedItem.ItemID);
        else
            documentViewer.Open(selectedItem.Icon);
    }

    private void ConfigureDocumentPreview()
    {
        if (inventoryRoot == null)
            return;

        documentViewer = GetComponent<InventoryDocumentViewer>();
        if (documentViewer == null)
            documentViewer = gameObject.AddComponent<InventoryDocumentViewer>();
        documentViewer.Configure(inventoryRoot.transform);

        if (detailIcon == null)
            return;

        documentPreview = detailIcon.GetComponent<InventoryDocumentPreview>();
        if (documentPreview == null)
            documentPreview = detailIcon.gameObject.AddComponent<InventoryDocumentPreview>();
        documentPreview.Configure(detailIcon, this);
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
        if (GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.NotifyInventoryItemsChanged();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
            {
                // Destroy is deferred; exclude the old page from this frame's grid layout.
                if (enablePagination)
                    spawnedSlots[i].gameObject.SetActive(false);
                Destroy(spawnedSlots[i].gameObject);
            }
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
        bool filterChanged = filter != currentFilter;
        currentFilter = filter;
        if (filterChanged)
            currentPageIndex = 0;
        UpdateTabVisuals();

        if (filterChanged && IsOpen)
            PlayRandomTabSwitchSound();

        if (IsOpen)
            Refresh();
    }

    private void ConfigureInventoryAudioSource()
    {
        if (inventoryAudioSource == null)
            inventoryAudioSource = gameObject.AddComponent<AudioSource>();

        inventoryAudioSource.spatialBlend = 0f;
        inventoryAudioSource.playOnAwake = false;
        inventoryAudioSource.loop = false;
        inventoryAudioSource.volume = 1f;
        inventoryAudioSource.clip = null;
    }

    private void PlayRandomTabSwitchSound()
    {
        if (inventoryAudioConfig == null || inventoryAudioConfig.TabSwitchSounds == null)
            return;

        List<AudioClip> validClips = new List<AudioClip>();
        AudioClip[] configuredClips = inventoryAudioConfig.TabSwitchSounds;
        for (int i = 0; i < configuredClips.Length; i++)
        {
            AudioClip clip = configuredClips[i];
            if (clip != null && !validClips.Contains(clip))
                validClips.Add(clip);
        }

        if (validClips.Count == 0)
            return;

        if (validClips.Count > 1)
            validClips.Remove(lastTabSwitchClip);

        AudioClip selectedClip = validClips[UnityEngine.Random.Range(0, validClips.Count)];
        lastTabSwitchClip = selectedClip;
        PlayInventorySound(selectedClip);
    }

    private void PlayInventorySound(AudioClip clip)
    {
        if (clip == null || inventoryAudioSource == null || inventoryAudioConfig == null)
            return;

        inventoryAudioSource.PlayOneShot(clip, Mathf.Clamp01(inventoryAudioConfig.UIVolume));
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
