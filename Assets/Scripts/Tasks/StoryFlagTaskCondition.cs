using System;
using UnityEngine;

/// <summary>Advances a task stage when every configured story flag is set.</summary>
[DisallowMultipleComponent]
public sealed class StoryFlagTaskCondition : MonoBehaviour
{
    [SerializeField] private string taskId;
    [SerializeField] private string expectedCurrentStageId;
    [SerializeField] private string targetStageId;
    [SerializeField] private string[] requiredStoryFlags = Array.Empty<string>();

    public void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(taskId) ||
            string.IsNullOrWhiteSpace(expectedCurrentStageId) ||
            string.IsNullOrWhiteSpace(targetStageId) ||
            requiredStoryFlags == null || requiredStoryFlags.Length == 0)
        {
            Debug.LogWarning("Story flag task condition is incomplete.", this);
            return;
        }

        TaskManager manager = TaskManager.Instance;
        if (manager == null || manager.GetTaskState(taskId) != TaskState.Active ||
            !manager.IsCurrentStage(taskId, expectedCurrentStageId))
            return;

        foreach (string flag in requiredStoryFlags)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                Debug.LogWarning("Story flag task condition has an empty required flag.", this);
                return;
            }

            if (!SessionStoryState.GetFlag(flag))
                return;
        }

        manager.AdvanceTaskStage(taskId, targetStageId);
    }
}
