using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Completes the one-shot post-Part-2 arrival and tracks the five authored
/// Sulatin items through the existing persistent task and inventory services.
/// Attach beside TaskManager on its persistent prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class PostPart2SulatinTaskCoordinator : MonoBehaviour
{
    public const string ArrivalPendingFlag = "alaala_post_part2_draftworld_arrival_pending";
    private const string ArrivalHandledFlag = "alaala_post_part2_draftworld_arrival_handled";
    private const string Part2CompleteFlag = "alaala_part2_complete";
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string CollectionStageId = "collect_remaining_sulatin";
    private const string DraftWorldScene = "DraftWorld";
    private const float ArrivalFadeDuration = 1.5f;

    private static readonly HashSet<string> requiredItemIds = new HashSet<string>
    {
        "present_pili_archival_rizal_novel",
        "present_pili_tagalog_espanol_manuscripts",
        "present_pili_letter_sealed_envelope",
        "present_pili_filipino_archive_note",
        "church_lumang_sulatin"
    };

    private TaskManager taskManager;
    private InventoryManager subscribedInventory;
    private DialogueMovementLock arrivalMovementLock;
    private FadeController arrivalFade;
    private StorySequenceToken arrivalToken;
    private Coroutine inventoryWaitRoutine;
    private Coroutine arrivalRoutine;

    private void Awake()
    {
        taskManager = GetComponent<TaskManager>();
        if (taskManager == null ||
            (TaskManager.Instance != null && TaskManager.Instance != taskManager))
        {
            enabled = false;
            return;
        }

        taskManager.TaskChanged += HandleTaskChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureInventorySubscription();
    }

    private void Start()
    {
        if (!enabled)
            return;

        if (subscribedInventory == null)
            inventoryWaitRoutine = StartCoroutine(WaitForInventoryManager());

        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (taskManager != null)
            taskManager.TaskChanged -= HandleTaskChanged;
        UnsubscribeInventory();
        if (arrivalToken != null)
        {
            arrivalToken.Release();
            arrivalToken = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!enabled)
            return;

        EnsureInventorySubscription();
        if (scene.name != DraftWorldScene || arrivalRoutine != null ||
            !SessionStoryState.GetFlag(Part2CompleteFlag) ||
            !SessionStoryState.GetFlag(ArrivalPendingFlag) ||
            SessionStoryState.GetFlag(ArrivalHandledFlag))
        {
            return;
        }

        SessionStoryState.SetFlag(ArrivalPendingFlag, false);
        SessionStoryState.SetFlag(ArrivalHandledFlag, true);

        if (arrivalToken == null || !arrivalToken.IsValid)
            arrivalToken = StorySequenceCoordinator.Acquire(this);
        arrivalMovementLock = FindAnyObjectByType<DialogueMovementLock>();
        if (arrivalMovementLock != null)
            arrivalMovementLock.HoldMovementForStorySequence();
        else
            Debug.LogError("Post-Part-2 arrival could not find DraftWorld's DialogueMovementLock.", this);

        arrivalFade = FindAnyObjectByType<FadeController>();
        if (arrivalFade != null)
            arrivalFade.HoldBlackForSequence();
        else
            Debug.LogError("Post-Part-2 arrival could not find DraftWorld's FadeController.", this);

        arrivalRoutine = StartCoroutine(CompleteArrivalAndStartTask());
    }

    public bool PrepareForArrival()
    {
        if (!enabled || taskManager == null || TaskManager.Instance != taskManager ||
            SessionStoryState.GetFlag(ArrivalHandledFlag))
        {
            return false;
        }

        if (arrivalToken == null || !arrivalToken.IsValid)
            arrivalToken = StorySequenceCoordinator.Acquire(this);
        return arrivalToken != null && arrivalToken.IsValid;
    }

    public void CancelPreparedArrival()
    {
        if (arrivalToken == null)
            return;

        arrivalToken.Release();
        arrivalToken = null;
    }

    private IEnumerator CompleteArrivalAndStartTask()
    {
        try
        {
            if (arrivalFade == null)
                yield break;

            yield return arrivalFade.FadeFromBlack(ArrivalFadeDuration);
            if (TaskManager.Instance == null)
            {
                while (TaskManager.Instance == null)
                    yield return null;
                taskManager = TaskManager.Instance;
                taskManager.TaskChanged += HandleTaskChanged;
            }

            EnsureInventorySubscription();
            ReleaseArrivalControls();
            if (TaskManager.Instance.GetTaskState(TaskId) == TaskState.Inactive)
                TaskManager.Instance.StartTask(TaskId);

            SeedOwnedRequiredItems();
        }
        finally
        {
            ReleaseArrivalControls();
            arrivalRoutine = null;
        }
    }

    private void ReleaseArrivalControls()
    {
        if (arrivalMovementLock != null)
            arrivalMovementLock.ReleaseStorySequenceHold();
        if (arrivalToken != null)
        {
            arrivalToken.Release();
            arrivalToken = null;
        }
    }

    private void HandleTaskChanged()
    {
        if (taskManager == null || taskManager.GetTaskState(TaskId) != TaskState.Active)
            return;

        EnsureInventorySubscription();
        SeedOwnedRequiredItems();
    }

    private void HandleItemAdded(InventoryItemData item)
    {
        if (item == null || taskManager == null ||
            taskManager.GetTaskState(TaskId) != TaskState.Active ||
            !requiredItemIds.Contains(item.ItemID))
        {
            return;
        }

        if (taskManager.IsCurrentStage(TaskId, CollectionStageId))
            taskManager.RegisterProgress(TaskId, item.ItemID, 1, true);
    }

    private void SeedOwnedRequiredItems()
    {
        if (taskManager == null || subscribedInventory == null ||
            taskManager.GetTaskState(TaskId) != TaskState.Active ||
            !taskManager.IsCurrentStage(TaskId, CollectionStageId))
        {
            return;
        }

        IReadOnlyList<InventoryItemData> owned = subscribedInventory.GetOwnedItems();
        for (int i = 0; i < owned.Count; i++)
        {
            InventoryItemData item = owned[i];
            if (item != null && requiredItemIds.Contains(item.ItemID))
                taskManager.RegisterProgress(TaskId, item.ItemID, 1, true);
        }
    }

    private void EnsureInventorySubscription()
    {
        InventoryManager current = InventoryManager.Instance;
        if (current == null || current == subscribedInventory)
            return;

        UnsubscribeInventory();
        subscribedInventory = current;
        subscribedInventory.ItemAdded += HandleItemAdded;

        if (inventoryWaitRoutine != null)
        {
            StopCoroutine(inventoryWaitRoutine);
            inventoryWaitRoutine = null;
        }

        SeedOwnedRequiredItems();
    }

    private IEnumerator WaitForInventoryManager()
    {
        while (enabled && subscribedInventory == null)
        {
            EnsureInventorySubscription();
            yield return null;
        }
        inventoryWaitRoutine = null;
    }

    private void UnsubscribeInventory()
    {
        if (subscribedInventory == null)
            return;

        subscribedInventory.ItemAdded -= HandleItemAdded;
        subscribedInventory = null;
    }
}
