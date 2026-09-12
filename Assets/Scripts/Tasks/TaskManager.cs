using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TaskManager : MonoBehaviour
{
    private const string ActiveFlagPrefix = "task_active:";
    private const string CompletedFlagPrefix = "task_completed:";
    private const string ProgressFlagPrefix = "task_progress:";
    private const string StageValuePrefix = "task_stage:";

    public static TaskManager Instance { get; private set; }

    [SerializeField] private TaskData[] taskDefinitions = Array.Empty<TaskData>();
    private TaskNotificationUI notificationUI;

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
        DontDestroyOnLoad(gameObject);
        BuildDefinitionLookup();
        RestoreRuntimeState();
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
        SessionStoryState.SetString(
            GetStageValue(taskId),
            currentStageByTaskId[taskId]);
        objectivesById[taskId] = firstStage == null
            ? task.StartingObjective
            : FormatStageObjective(taskId, firstStage);
        currentTaskId = taskId;
        TaskChanged?.Invoke();

        ShowNotificationThenTracker(
            taskId,
            task.StartingObjective,
            onFinished => notificationUI.ShowTaskStarted(
                task.Type,
                onFinished));
        return true;
    }

    public bool UpdateTask(string taskId, string objective)
    {
        if (!TryGetDefinition(taskId, out TaskData task) ||
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
                task.Type,
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

        notificationUI?.ShowTaskCompleted(task.Type);

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

        string previousStageId = currentStageByTaskId.TryGetValue(
            normalizedTaskId,
            out string currentStageId)
            ? currentStageId
            : normalizedStageId;
        bool stageChanged = false;
        int stageProgress = GetCurrentStageProgress(normalizedTaskId, stage);
        if (stage.RequiredCount > 0 && stageProgress >= stage.RequiredCount)
        {
            TaskStage next = GetNextStage(task, stage);
            if (next != null)
            {
                stageChanged = !string.Equals(
                    previousStageId,
                    next.StageId?.Trim(),
                    StringComparison.Ordinal);
                SetStage(normalizedTaskId, next);
            }
            else
                objectivesById[normalizedTaskId] = FormatStageObjective(normalizedTaskId, stage);
        }
        else
        {
            objectivesById[normalizedTaskId] = FormatStageObjective(normalizedTaskId, stage);
        }

        currentTaskId = normalizedTaskId;
        TaskChanged?.Invoke();

        if (stageChanged)
        {
            string updatedObjective = objectivesById[normalizedTaskId];
            ShowNotificationThenTracker(
                normalizedTaskId,
                updatedObjective,
                onFinished => notificationUI.ShowTaskUpdated(
                    task.Type,
                    onFinished));
        }
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

        string normalizedTaskId = taskId.Trim();
        string normalizedStageId = stage.StageId?.Trim();
        if (currentStageByTaskId.TryGetValue(normalizedTaskId, out string currentStageId) &&
            string.Equals(currentStageId, normalizedStageId, StringComparison.Ordinal))
        {
            return true;
        }

        SetStage(normalizedTaskId, stage);
        currentTaskId = normalizedTaskId;
        TaskChanged?.Invoke();

        string updatedObjective = objectivesById[normalizedTaskId];
        ShowNotificationThenTracker(
            normalizedTaskId,
            updatedObjective,
            onFinished => notificationUI.ShowTaskUpdated(
                task.Type,
                onFinished));
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

    public bool IsCurrentStage(string taskId, string stageId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(stageId))
            return false;

        return currentStageByTaskId.TryGetValue(taskId.Trim(), out string currentStageId) &&
            string.Equals(currentStageId, stageId.Trim(), StringComparison.Ordinal);
    }

    public void RegisterNotificationUI(TaskNotificationUI ui)
    {
        if (ui != null)
            notificationUI = ui;
    }

    public void UnregisterNotificationUI(TaskNotificationUI ui)
    {
        if (notificationUI == ui)
            notificationUI = null;
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

    private static string GetStageValue(string taskId)
    {
        return StageValuePrefix + taskId;
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
        SessionStoryState.SetString(
            GetStageValue(taskId),
            currentStageByTaskId[taskId]);
        objectivesById[taskId] = FormatStageObjective(taskId, stage);
    }

    private void RestoreRuntimeState()
    {
        foreach (TaskData task in taskDefinitions)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.TaskId) ||
                GetTaskState(task.TaskId) != TaskState.Active)
            {
                continue;
            }

            string taskId = task.TaskId.Trim();
            string storedStageId = SessionStoryState.GetString(
                GetStageValue(taskId));
            TaskStage stage = task.Stages?.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.StageId?.Trim(),
                    storedStageId,
                    StringComparison.Ordinal));

            if (stage == null)
                stage = task.Stages?.FirstOrDefault(candidate => candidate != null);

            if (stage != null)
            {
                currentStageByTaskId[taskId] = stage.StageId?.Trim();
                SessionStoryState.SetString(
                    GetStageValue(taskId),
                    currentStageByTaskId[taskId]);
                objectivesById[taskId] = FormatStageObjective(taskId, stage);
            }
            else
            {
                objectivesById[taskId] = task.StartingObjective;
            }

            currentTaskId = taskId;
        }
    }
}
