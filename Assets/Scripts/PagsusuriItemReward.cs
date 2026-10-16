using UnityEngine;

public enum PagsusuriRewardMode
{
    PresentationOnly,
    ImmediateInventory,
    JournalRecord
}

/// <summary>Optional reward policy invoked once when its Pagsusuri scan completes.</summary>
[DisallowMultipleComponent]
public sealed class PagsusuriItemReward : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private InventoryItemData item;
    [SerializeField] private PagsusuriRewardMode rewardMode = PagsusuriRewardMode.PresentationOnly;

    [Header("Presentation")]
    [SerializeField] private ItemAcquisitionPopupPresenter popupPresenter;
    [SerializeField] private Sprite overrideIcon;
    [SerializeField] private string overrideDisplayName;
    [SerializeField] private bool hideWorldObjectAfterSuccessfulInventoryReward = true;

    [Header("Journal Record")]
    [SerializeField] private JournalEntryCategory journalCategory;
    [SerializeField] private string journalEntryId;
    [SerializeField] private Sprite journalIcon;
    [SerializeField] private string journalDisplayName;

    private PagsusuriScanTarget scanTarget;
    private bool rewardTriggered;

    public InventoryItemData Item => item;
    public PagsusuriRewardMode RewardMode => rewardMode;

    private void Awake()
    {
        scanTarget = GetComponent<PagsusuriScanTarget>();
        if (popupPresenter == null)
            popupPresenter = FindAnyObjectByType<ItemAcquisitionPopupPresenter>();

        HideIfInventoryItemAlreadyOwned();
    }

    private void OnEnable()
    {
        if (scanTarget == null)
            scanTarget = GetComponent<PagsusuriScanTarget>();
        if (scanTarget != null)
            scanTarget.OnScanCompleted += HandleScanCompleted;
    }

    private void OnDisable()
    {
        if (scanTarget != null)
            scanTarget.OnScanCompleted -= HandleScanCompleted;
    }

    private void HandleScanCompleted(PagsusuriScanTarget completedTarget)
    {
        if (rewardTriggered || completedTarget != scanTarget)
            return;

        Sprite icon;
        string displayName;
        ItemAcquisitionPopupDestination destination = ItemAcquisitionPopupDestination.None;

        if (rewardMode == PagsusuriRewardMode.ImmediateInventory)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || item == null)
                return;

            if (!inventory.AddItem(item))
            {
                if (hideWorldObjectAfterSuccessfulInventoryReward &&
                    inventory.HasItem(item.ItemID))
                    HideWorldObject();
                return;
            }

            icon = overrideIcon != null ? overrideIcon : item.Icon;
            displayName = !string.IsNullOrWhiteSpace(overrideDisplayName)
                ? overrideDisplayName
                : item.DisplayName;
            destination = ItemAcquisitionPopupDestination.Inventory;
        }
        else if (rewardMode == PagsusuriRewardMode.JournalRecord)
        {
            ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
            if (journal == null || string.IsNullOrWhiteSpace(journalEntryId) ||
                !UnlockJournalEntry(journal))
                return;

            icon = journalIcon != null ? journalIcon : overrideIcon;
            displayName = !string.IsNullOrWhiteSpace(journalDisplayName)
                ? journalDisplayName
                : overrideDisplayName;
            destination = ItemAcquisitionPopupDestination.Journal;
        }
        else
        {
            icon = overrideIcon != null ? overrideIcon : item != null ? item.Icon : null;
            displayName = !string.IsNullOrWhiteSpace(overrideDisplayName)
                ? overrideDisplayName
                : item != null ? item.DisplayName : string.Empty;
        }

        rewardTriggered = true;
        if (popupPresenter != null)
            popupPresenter.Enqueue(icon, displayName, destination);

        if (rewardMode == PagsusuriRewardMode.ImmediateInventory &&
            hideWorldObjectAfterSuccessfulInventoryReward)
        {
            HideWorldObject();
        }
    }

    private void HideIfInventoryItemAlreadyOwned()
    {
        if (!hideWorldObjectAfterSuccessfulInventoryReward ||
            rewardMode != PagsusuriRewardMode.ImmediateInventory || item == null)
            return;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null && inventory.HasItem(item.ItemID))
            HideWorldObject();
    }

    private void HideWorldObject()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private bool UnlockJournalEntry(ReconstructionJournalManager journal)
    {
        switch (journalCategory)
        {
            case JournalEntryCategory.Observation:
                return journal.UnlockObservation(journalEntryId);
            case JournalEntryCategory.People:
                return journal.UnlockPerson(journalEntryId);
            case JournalEntryCategory.Fragment:
                return journal.UnlockFragment(journalEntryId);
            case JournalEntryCategory.Reflection:
                return journal.UnlockReflection(journalEntryId);
            default:
                return false;
        }
    }
}
