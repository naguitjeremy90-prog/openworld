using System;

/// <summary>Read-only description of a task state change for tracker presentation.</summary>
public sealed class TaskPresentationChange
{
    public string TaskId { get; }
    public TaskType TaskType { get; }
    public string PreviousObjective { get; }
    public string CompletedObjective { get; }
    public string NewObjective { get; }
    public bool ProgressIncreased { get; }
    public bool StageChanged { get; }
    public bool TaskStarted { get; }
    public bool TaskCompleted { get; }

    public TaskPresentationChange(
        string taskId,
        TaskType taskType,
        string previousObjective,
        string completedObjective,
        string newObjective,
        bool progressIncreased = false,
        bool stageChanged = false,
        bool taskStarted = false,
        bool taskCompleted = false)
    {
        TaskId = taskId;
        TaskType = taskType;
        PreviousObjective = previousObjective ?? string.Empty;
        CompletedObjective = completedObjective ?? PreviousObjective;
        NewObjective = newObjective ?? string.Empty;
        ProgressIncreased = progressIncreased;
        StageChanged = stageChanged;
        TaskStarted = taskStarted;
        TaskCompleted = taskCompleted;
    }
}
