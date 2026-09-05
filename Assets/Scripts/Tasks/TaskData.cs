using System;
using UnityEngine;

[Serializable]
public sealed class TaskData
{
    [SerializeField] private string taskId;
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 4)] private string startingObjective;

    public string TaskId => taskId;
    public string Title => title;
    public string StartingObjective => startingObjective;
}

public enum TaskState
{
    Inactive,
    Active,
    Completed
}
