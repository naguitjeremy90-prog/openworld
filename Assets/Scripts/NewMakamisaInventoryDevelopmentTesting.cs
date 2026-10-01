using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

/// <summary>Editor-only Inventory backend and optional UI access for direct NEWMAKAMISA testing.</summary>
[DefaultExecutionOrder(-29998)]
[DisallowMultipleComponent]
public sealed class NewMakamisaInventoryDevelopmentTesting : MonoBehaviour
{
    [Header("DIRECT NEWMAKAMISA DEVELOPMENT TESTING ONLY")]
    [SerializeField] private bool enableDirectSceneInventoryTesting = true;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private bool unlockInventoryForTesting;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!enableDirectSceneInventoryTesting ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "NEWMAKAMISA")
            return;

        // The direct-scene UI unlock is session-only. Suppress the tutorial before
        // the real unlock event so no tutorial step or seen flag is consumed.
        if (unlockInventoryForTesting)
        {
            GameplaySystemTutorialManager.Instance.SuppressTutorialForDevelopment(
                GameplaySystemId.Inventory);
            GameplaySystemState.SetUnlocked(GameplaySystemId.Inventory, true);
        }

#endif
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (!enableDirectSceneInventoryTesting ||
            SceneManager.GetActiveScene().name != "NEWMAKAMISA")
            return;

        // Wait until scene Awake initialization has finished before creating the
        // persistent manager, so any scene-provided singleton gets first choice.
        if (InventoryManager.Instance != null)
        {
            Debug.Log(
                "[NewMakamisaInventoryDevelopmentTesting] Existing InventoryManager retained; Instance=" +
                InventoryManager.Instance.name + ".",
                this);
            return;
        }

        if (inventoryManagerPrefab == null ||
            !UnityEditor.EditorUtility.IsPersistent(inventoryManagerPrefab))
        {
            Debug.LogError(
                "Direct NEWMAKAMISA Inventory testing requires the configured InventoryManager prefab asset.",
                this);
            return;
        }

        GameObject inventorySystem = Instantiate(inventoryManagerPrefab.gameObject);
        InventoryManager manager = inventorySystem.GetComponent<InventoryManager>();
        if (manager == null || InventoryManager.Instance != manager)
        {
            Debug.LogError(
                "[NewMakamisaInventoryDevelopmentTesting] InventorySystem instantiated; " +
                "InventoryManager.Instance=" + (InventoryManager.Instance != null ? "assigned" : "null") + ".",
                this);
            return;
        }

        Debug.Log(
            "[NewMakamisaInventoryDevelopmentTesting] InventorySystem instantiated; " +
            "InventoryManager.Instance=" + InventoryManager.Instance.name + ".",
            this);
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Log Inventory Runtime State")]
    private void LogInventoryRuntimeState()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        InventoryUI inventoryUI = null;
        InventoryUI[] inventoryUIs = FindObjectsByType<InventoryUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < inventoryUIs.Length; i++)
        {
            if (inventoryUIs[i].gameObject.scene == activeScene)
            {
                inventoryUI = inventoryUIs[i];
                break;
            }
        }

        StorySequenceCoordinator coordinator;
        bool coordinatorFound = StorySequenceCoordinator.TryGetExisting(out coordinator);
        Canvas canvas = inventoryUI != null ? inventoryUI.GetComponentInParent<Canvas>() : null;
        StringBuilder message = new StringBuilder("[NewMakamisaInventoryDiagnostic]");
        message.Append(" unlocked=").Append(GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory));
        message.Append(" inventoryManagerNull=").Append(InventoryManager.Instance == null);
        message.Append(" inventoryUINull=").Append(inventoryUI == null);

        if (inventoryUI != null)
        {
            message.Append(" inventoryUIObject=").Append(inventoryUI.gameObject.name);
            message.Append(" inventoryUIActive=").Append(inventoryUI.gameObject.activeInHierarchy);
            message.Append(" inventoryUIEnabled=").Append(inventoryUI.enabled);
            message.Append(" inventoryUIActiveAndEnabled=").Append(inventoryUI.isActiveAndEnabled);
            message.Append(" inventoryOpen=").Append(inventoryUI.IsOpen);
        }

        message.Append(" coordinatorNull=").Append(!coordinatorFound);
        message.Append(" storySequenceActive=").Append(StorySequenceCoordinator.IsStorySequenceActive);
        message.Append(" activeOwnerCount=").Append(StorySequenceCoordinator.ActiveOwnerCount);

        if (canvas != null)
            message.Append(" inventoryCanvasActive=").Append(canvas.gameObject.activeInHierarchy);
        else
            message.Append(" inventoryCanvasNull=true");

        if (inventoryUI != null)
        {
            CanvasGroup[] groups = inventoryUI.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                message.Append(" canvasGroup[").Append(group.gameObject.name).Append("]={active:")
                    .Append(group.gameObject.activeInHierarchy)
                    .Append(",alpha:").Append(group.alpha)
                    .Append(",interactable:").Append(group.interactable)
                    .Append(",raycasts:").Append(group.blocksRaycasts).Append('}');
            }
        }

        Debug.Log(message.ToString(), this);
    }
#endif
}
