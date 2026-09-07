using System;
using UnityEngine;

[Serializable]
public sealed class TaskData
{
    [SerializeField] private string taskId;
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 4)] private string startingObjective;
    [SerializeField] private TaskType taskType = TaskType.Side;
    [SerializeField] private TaskStage[] stages = Array.Empty<TaskStage>();

    public string TaskId => taskId;
    public string Title => title;
    public string StartingObjective => startingObjective;
    public TaskType Type => taskType;
    public TaskStage[] Stages => stages;
}

[Serializable]
public sealed class TaskStage
{
    [SerializeField] private string stageId;
    [SerializeField, TextArea(2, 4)] private string objectiveText;
    [SerializeField, Min(0)] private int requiredCount;
    [SerializeField] private string[] allowedProgressIds = Array.Empty<string>();

    public string StageId => stageId;
    public string ObjectiveText => objectiveText;
    public int RequiredCount => requiredCount;
    public string[] AllowedProgressIds => allowedProgressIds;
}

public enum TaskType
{
    Main,
    Side
}

public enum TaskState
{
    Inactive,
    Active,
    Completed
}
