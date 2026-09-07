using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TaskManager : MonoBehaviour
{
    private const string ActiveFlagPrefix = "task_active:";
    private const string CompletedFlagPrefix = "task_completed:";
    private const string ProgressFlagPrefix = "task_progress:";

    public static TaskManager Instance { get; private set; }

    [SerializeField] private TaskData[] taskDefinitions = Array.Empty<TaskData>();
    [SerializeField] private TaskNotificationUI notificationUI;
    [SerializeField] private TaskTrackerUI[] trackerUIs = Array.Empty<TaskTrackerUI>();

    private readonly Dictionary<string, TaskData> definitionsById =
        new Dictionary<string, TaskData>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> objectivesById =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> currentStageByTaskId =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public event Action TaskChanged;

    private string currentTaskId;
    private int presentationVersion;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildDefinitionLookup();
        RefreshTrackers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool StartTask(string taskId)
    {
        if (!TryGetDefinition(taskId, out TaskData task) ||
            GetTaskState(taskId) != TaskState.Inactive)
        {
            return false;
        }

        SessionStoryState.SetFlag(GetActiveFlag(taskId), true);
        TaskStage firstStage = task.Stages?.FirstOrDefault(s => s != null);
        currentStageByTaskId[taskId] = firstStage?.StageId?.Trim();
        objectivesById[taskId] = firstStage == null
            ? task.StartingObjective
            : FormatStageObjective(taskId, firstStage);
        currentTaskId = taskId;
        TaskChanged?.Invoke();

        ShowNotificationThenTracker(
            taskId,
            task.StartingObjective,
            onFinished => notificationUI.ShowTaskStarted(
                task.StartingObjective,
                onFinished));
        return true;
    }

    public bool UpdateTask(string taskId, string objective)
    {
        if (!TryGetDefinition(taskId, out _) ||
            GetTaskState(taskId) != TaskState.Active ||
            string.IsNullOrWhiteSpace(objective))
        {
            return false;
        }

        string normalizedObjective = objective.Trim();
        if (objectivesById.TryGetValue(taskId, out string currentObjective) &&
            string.Equals(currentObjective, normalizedObjective, StringComparison.Ordinal))
        {
            return false;
        }

        objectivesById[taskId] = normalizedObjective;
        currentTaskId = taskId;
        TaskChanged?.Invoke();

        ShowNotificationThenTracker(
            taskId,
            normalizedObjective,
            onFinished => notificationUI.ShowTaskUpdated(
                normalizedObjective,
                onFinished));
        return true;
    }

    public bool CompleteTask(string taskId)
    {
        if (!TryGetDefinition(taskId, out TaskData task) ||
            GetTaskState(taskId) != TaskState.Active)
        {
            return false;
        }

        presentationVersion++;

        SessionStoryState.SetFlag(GetActiveFlag(taskId), false);
        SessionStoryState.SetFlag(GetCompletedFlag(taskId), true);
        objectivesById.Remove(taskId);

        notificationUI?.ShowTaskCompleted(task.Title);

        if (string.Equals(currentTaskId, taskId, StringComparison.Ordinal))
        {
            currentTaskId = null;
        }

        TaskChanged?.Invoke();

        return true;
    }

    public bool RegisterProgress(string taskId, string progressId, int amount = 1, bool once = true)
    {
        if (!TryGetDefinition(taskId, out _) ||
            GetTaskState(taskId) != TaskState.Active ||
            string.IsNullOrWhiteSpace(progressId) ||
            amount <= 0)
        {
            return false;
        }

        string normalizedTaskId = taskId.Trim();
        string normalizedProgressId = progressId.Trim();
        if (!TryGetCurrentStage(normalizedTaskId, out TaskData task, out TaskStage stage))
            return false;


        if (stage.AllowedProgressIds == null || !stage.AllowedProgressIds.Any(id =>
            string.Equals(id?.Trim(), normalizedProgressId, StringComparison.Ordinal)))
            return false;

        string normalizedStageId = stage.StageId.Trim();
        string storageKey = GetProgressFlag(
            normalizedTaskId,
            normalizedStageId,
            normalizedProgressId);
        int currentAmount = GetStoredProgress(storageKey);

        if (once && currentAmount > 0)
            return false;

        int updatedAmount = currentAmount + amount;
        SessionStoryState.SetInt(storageKey, updatedAmount);
        SessionStoryState.SetFlag(storageKey, true);

        int stageProgress = GetCurrentStageProgress(normalizedTaskId, stage);
        if (stage.RequiredCount > 0 && stageProgress >= stage.RequiredCount)
        {
            TaskStage next = GetNextStage(task, stage);
            if (next != null)
                SetStage(normalizedTaskId, next);
            else
                objectivesById[normalizedTaskId] = FormatStageObjective(normalizedTaskId, stage);
        }
        else
        {
            objectivesById[normalizedTaskId] = FormatStageObjective(normalizedTaskId, stage);
        }

        TaskChanged?.Invoke();
        return true;
    }

    public bool AdvanceTaskStage(string taskId, string stageId)
    {
        if (!TryGetDefinition(taskId, out TaskData task) ||
            GetTaskState(taskId) != TaskState.Active ||
            string.IsNullOrWhiteSpace(stageId))
            return false;

        TaskStage stage = task.Stages?.FirstOrDefault(s => s != null &&
            string.Equals(s.StageId?.Trim(), stageId.Trim(), StringComparison.Ordinal));
        if (stage == null)
            return false;

        SetStage(taskId.Trim(), stage);
        TaskChanged?.Invoke();
        return true;
    }

    public bool HasProgress(string taskId, string progressId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(progressId))
            return false;

        string taskKey = taskId.Trim();
        string progressKey = progressId.Trim();
        string stageId = currentStageByTaskId.TryGetValue(taskKey, out string currentStage)
            ? currentStage : string.Empty;
        return GetProgressForStage(taskKey, stageId, progressKey) > 0;
    }

    public int GetProgress(string taskId, string progressId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(progressId))
            return 0;

        string taskKey = taskId.Trim();
        string progressKey = progressId.Trim();
        string stageId = currentStageByTaskId.TryGetValue(taskKey, out string currentStage)
            ? currentStage : string.Empty;
        return GetProgressForStage(taskKey, stageId, progressKey);
    }

    public bool TryGetActiveTask(TaskType type, out TaskData definition, out string objective)
    {
        foreach (TaskData candidate in taskDefinitions)
        {
            if (candidate == null || candidate.Type != type ||
                GetTaskState(candidate.TaskId) != TaskState.Active)
            {
                continue;
            }

            definition = candidate;
            if (!objectivesById.TryGetValue(candidate.TaskId.Trim(), out objective))
                objective = candidate.StartingObjective;
            return true;
        }

        definition = null;
        objective = null;
        return false;
    }

    public TaskState GetTaskState(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return TaskState.Inactive;

        taskId = taskId.Trim();

        if (SessionStoryState.GetFlag(GetCompletedFlag(taskId)))
            return TaskState.Completed;

        return SessionStoryState.GetFlag(GetActiveFlag(taskId))
            ? TaskState.Active
            : TaskState.Inactive;
    }

    public bool TryGetCurrentObjective(string taskId, out string objective)
    {
        return objectivesById.TryGetValue(taskId, out objective);
    }

    private void BuildDefinitionLookup()
    {
        definitionsById.Clear();

        foreach (TaskData task in taskDefinitions)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.TaskId))
                continue;

            if (!definitionsById.TryAdd(task.TaskId.Trim(), task))
            {
                Debug.LogWarning(
                    $"TaskManager has more than one definition for '{task.TaskId}'.",
                    this);
            }
        }
    }

    private bool TryGetDefinition(string taskId, out TaskData task)
    {
        string normalizedId = taskId == null ? string.Empty : taskId.Trim();
        return definitionsById.TryGetValue(normalizedId, out task);
    }

    private void ShowNotificationThenTracker(
        string taskId,
        string objective,
        Action<Action> showNotification)
    {
        int version = ++presentationVersion;

        void ShowCurrentTracker()
        {
            if (version != presentationVersion ||
                !string.Equals(currentTaskId, taskId, StringComparison.Ordinal) ||
                GetTaskState(taskId) != TaskState.Active ||
                !objectivesById.TryGetValue(taskId, out string currentObjective) ||
                !string.Equals(currentObjective, objective, StringComparison.Ordinal))
            {
                return;
            }

            TaskChanged?.Invoke();
        }

        if (notificationUI != null)
            showNotification(ShowCurrentTracker);
        else
            ShowCurrentTracker();
    }

    private static string GetActiveFlag(string taskId)
    {
        return ActiveFlagPrefix + taskId;
    }

    private static string GetCompletedFlag(string taskId)
    {
        return CompletedFlagPrefix + taskId;
    }

    private static string GetProgressFlag(string taskId, string stageId, string progressId)
    {
        return ProgressFlagPrefix + taskId + ":" + stageId + ":" + progressId;
    }

    private static int GetStoredProgress(string storageKey)
    {
        int value = SessionStoryState.GetInt(storageKey);
        if (value > 0)
            return value;

        // Compatibility with progress written as a bool before integer storage existed.
        return SessionStoryState.GetFlag(storageKey) ? 1 : 0;
    }

    private static int GetProgressForStage(string taskId, string stageId, string progressId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return 0;

        string storageKey = GetProgressFlag(
            taskId.Trim(),
            stageId.Trim(),
            progressId.Trim());
        return GetStoredProgress(storageKey);
    }

    private void RefreshTrackers()
    {
        TaskChanged?.Invoke();
    }

    private bool TryGetCurrentStage(string taskId, out TaskData task, out TaskStage stage)
    {
        if (!TryGetDefinition(taskId, out task) || task.Stages == null || task.Stages.Length == 0 ||
            !currentStageByTaskId.TryGetValue(taskId, out string stageId))
        {
            stage = null;
            return false;
        }
        stage = task.Stages.FirstOrDefault(s => s != null &&
            string.Equals(s.StageId?.Trim(), stageId, StringComparison.Ordinal));
        return stage != null;
    }

    private TaskStage GetNextStage(TaskData task, TaskStage current)
    {
        int index = Array.IndexOf(task.Stages, current);
        return index >= 0 && index + 1 < task.Stages.Length ? task.Stages[index + 1] : null;
    }

    private int GetCurrentStageProgress(string taskId, TaskStage stage)
    {
        int total = 0;
        if (stage.AllowedProgressIds == null) return total;
        foreach (string id in stage.AllowedProgressIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal))
        {
            total += GetProgressForStage(taskId, stage.StageId, id);
        }
        return total;
    }

    private string FormatStageObjective(string taskId, TaskStage stage)
    {
        return stage.RequiredCount > 0
            ? stage.ObjectiveText + " (" + GetCurrentStageProgress(taskId, stage) + "/" + stage.RequiredCount + ")"
            : stage.ObjectiveText;
    }

    private void SetStage(string taskId, TaskStage stage)
    {
        currentStageByTaskId[taskId] = stage.StageId?.Trim();
        objectivesById[taskId] = FormatStageObjective(taskId, stage);
    }
}
