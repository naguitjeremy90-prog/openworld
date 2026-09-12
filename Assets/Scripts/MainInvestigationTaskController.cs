using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>Small story bridge for starting the opening task and handling its exceptional lead.</summary>
public sealed class MainInvestigationTaskController : MonoBehaviour
{
    private const string TaskId = "main_investigate_pili";
    private const string StartedFlag = "main_investigate_pili_started";
    private const string MaestroLeadFlag = "maestro_ben_lead_received";
    private const string MaestroBenCompletedFlag = "maestro_ben_completed";
    private const string AlingIkaCompletedFlag = "aling_ika_completed";

    [Header("Testing (Development Only)")]
    [SerializeField] private bool forceMainTaskForTesting = false;
    [SerializeField] private bool enableProgressTesting = false;

    private bool morningWasObserved;

    private void Awake() { morningWasObserved = GameFlags.isMorning; }

    private void Update()
    {
        if (enableProgressTesting)
            HandleProgressTestKeys();

        if (!GameFlags.isMorning && !forceMainTaskForTesting)
        { morningWasObserved = false; return; }
        if (!morningWasObserved || TaskManager.Instance == null ||
            TaskManager.Instance.GetTaskState(TaskId) == TaskState.Inactive)
        { morningWasObserved = true; EnsureTaskStarted(); }
        if (SessionStoryState.GetFlag(AlingIkaCompletedFlag)) AdvanceToMaestroLead();
    }

    private void HandleProgressTestKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7)) RegisterProgressForTesting("overheard_maria_nene", "7");
        if (Input.GetKeyDown(KeyCode.Alpha8)) RegisterProgressForTesting("questioned_maria_nene", "8");
        if (Input.GetKeyDown(KeyCode.Alpha9)) RegisterProgressForTesting("mang_kardo", "9");
    }

    private void RegisterProgressForTesting(string progressId, string keyName)
    {
        TaskManager manager = TaskManager.Instance;
        bool active = manager != null && manager.GetTaskState(TaskId) == TaskState.Active;
        Debug.Log($"[TASK TEST] {keyName} registering {progressId}", this);

        bool registered = active && manager.RegisterProgress(TaskId, progressId, 1, true);
        string stageId = GetCurrentStageIdForTesting(manager);
        string objective = string.Empty;
        manager?.TryGetCurrentObjective(TaskId, out objective);
        Debug.Log($"[TASK TEST] {keyName} result={registered}, active={active}, stage={stageId}, hasProgress={manager?.HasProgress(TaskId, progressId)}, progress={manager?.GetProgress(TaskId, progressId)}, objective='{objective}'", this);
    }

    private static string GetCurrentStageIdForTesting(TaskManager manager)
    {
        if (manager == null)
            return "none";

        FieldInfo field = typeof(TaskManager).GetField(
            "currentStageByTaskId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(manager) is Dictionary<string, string> stages &&
            stages.TryGetValue(TaskId, out string stageId))
            return stageId;

        return "none";
    }

    private void EnsureTaskStarted()
    {
        if (TaskManager.Instance == null ||
            TaskManager.Instance.GetTaskState(TaskId) != TaskState.Inactive) return;
        if (TaskManager.Instance.StartTask(TaskId)) SessionStoryState.SetFlag(StartedFlag, true);
    }

    private void AdvanceToMaestroLead()
    {
        if (SessionStoryState.GetFlag(MaestroBenCompletedFlag))
            return;

        TaskManager manager = TaskManager.Instance;
        if (manager == null || manager.GetTaskState(TaskId) != TaskState.Active)
            return;

        if (!SessionStoryState.GetFlag(MaestroLeadFlag))
        {
            SessionStoryState.SetFlag(MaestroLeadFlag, true);
            manager.RegisterProgress(TaskId, "aling_ika_lead", 1, true);
        }

        if (!manager.IsCurrentStage(TaskId, "maestro_ben_lead"))
            manager.AdvanceTaskStage(TaskId, "maestro_ben_lead");
    }
}
