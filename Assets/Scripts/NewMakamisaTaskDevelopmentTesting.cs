using UnityEngine;

/// <summary>Optional real TaskManager state for starting NEWMAKAMISA directly in the Editor.</summary>
[DefaultExecutionOrder(-29999)]
[DisallowMultipleComponent]
public sealed class NewMakamisaTaskDevelopmentTesting : MonoBehaviour
{
    private const string MainTaskId = "main_investigate_pili";
    private const string SideTaskId = "aling_ika_errand";
    private const string CurrentStageId = "investigate_church_document";
    private const string PostChurchObjective = "Alamin ang Nangyari Pagkatapos ng Misa";

    [Header("DIRECT NEWMAKAMISA DEVELOPMENT TESTING ONLY")]
    [SerializeField] private bool enablePriorTaskState = false;
    [SerializeField] private TaskManager taskManagerPrefab;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!enablePriorTaskState ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "NEWMAKAMISA" ||
            TaskManager.Instance != null)
            return;

        if (taskManagerPrefab == null || !UnityEditor.EditorUtility.IsPersistent(taskManagerPrefab))
        {
            Debug.LogError("Direct NEWMAKAMISA Task testing requires the configured TaskManager prefab asset.", this);
            return;
        }

        // Set only the task state earned before NEWMAKAMISA, before the prefab's Awake restores it.
        SessionStoryState.SetFlag("task_completed:" + SideTaskId, true);
        SessionStoryState.SetFlag("task_active:" + SideTaskId, false);
        SessionStoryState.SetFlag("task_active:" + MainTaskId, true);
        SessionStoryState.SetFlag("task_completed:" + MainTaskId, false);
        SessionStoryState.SetString("task_stage:" + MainTaskId, CurrentStageId);

        TaskManager manager = Instantiate(taskManagerPrefab);
        if (manager != TaskManager.Instance || !manager.IsCurrentStage(MainTaskId, CurrentStageId) ||
            !manager.RestoreObjectiveForDirectSceneTesting(MainTaskId, PostChurchObjective))
        {
            Debug.LogError("Direct NEWMAKAMISA Task state could not be restored from the configured prefab.", this);
        }
#endif
    }
}
