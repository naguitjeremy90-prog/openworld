using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TaskManager : MonoBehaviour
{
    private const string ActiveFlagPrefix = "task_active:";
    private const string CompletedFlagPrefix = "task_completed:";

    public static TaskManager Instance { get; private set; }

    [SerializeField] private TaskData[] taskDefinitions = Array.Empty<TaskData>();
    [SerializeField] private TaskNotificationUI notificationUI;
    [SerializeField] private TaskTrackerUI trackerUI;

    private readonly Dictionary<string, TaskData> definitionsById =
        new Dictionary<string, TaskData>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> objectivesById =
        new Dictionary<string, string>(StringComparer.Ordinal);

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
        trackerUI?.HideTask();
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
        objectivesById[taskId] = task.StartingObjective;
        currentTaskId = taskId;

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

        trackerUI?.HideTask();

        objectivesById[taskId] = normalizedObjective;
        currentTaskId = taskId;

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

        trackerUI?.HideTask();
        presentationVersion++;

        SessionStoryState.SetFlag(GetActiveFlag(taskId), false);
        SessionStoryState.SetFlag(GetCompletedFlag(taskId), true);
        objectivesById.Remove(taskId);

        notificationUI?.ShowTaskCompleted(task.Title);

        if (string.Equals(currentTaskId, taskId, StringComparison.Ordinal))
        {
            currentTaskId = null;
        }

        return true;
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
        trackerUI?.HideTask();
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

            trackerUI?.ShowTask(currentObjective);
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
}
