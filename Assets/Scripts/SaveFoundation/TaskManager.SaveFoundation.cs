using System;
using System.Collections.Generic;
using System.Linq;
using Alaala.Saving;

public sealed partial class TaskManager
{
    public TaskData[] SaveDefinitions => (TaskData[])taskDefinitions.Clone();

    public List<AlaalaTaskSnapshot> CaptureSaveTasks()
    {
        var result = new List<AlaalaTaskSnapshot>();
        foreach (var task in taskDefinitions.OrderBy(t => t.TaskId, StringComparer.Ordinal))
        {
            var state = GetTaskState(task.TaskId);
            result.Add(new AlaalaTaskSnapshot
            {
                taskId = task.TaskId, state = state,
                stageId = SessionStoryState.GetString(GetStageValue(task.TaskId)),
                objective = state == TaskState.Active && objectivesById.TryGetValue(task.TaskId, out var objective) ? objective : ""
            });
        }
        return result;
    }

    /// <summary>Does not write progression, emit task events, invoke callbacks or queue notifications.</summary>
    public bool TryHydrateSaveTasksSilently(List<AlaalaTaskSnapshot> records, AlaalaSessionSnapshot state,
        AlaalaSaveCatalog catalog, out string error)
    {
        if (!CanHydrateSaveTasks(records, state, catalog, out error)) return false;
        if (!SessionStoryState.MatchesDurableSnapshot(state, catalog, out error)) return false;
        ++presentationVersion; // Old callbacks cannot present a pre-load objective.
        objectivesById.Clear(); currentStageByTaskId.Clear(); currentTaskId = null;
        foreach (var record in records)
        {
            if (record.stageId.Length > 0) currentStageByTaskId.Add(record.taskId, record.stageId);
            if (record.state != TaskState.Active) continue;
            objectivesById.Add(record.taskId, record.objective);
            currentTaskId = record.taskId;
        }
        return true;
    }

    public bool CanHydrateSaveTasks(List<AlaalaTaskSnapshot> records, AlaalaSessionSnapshot state,
        AlaalaSaveCatalog catalog, out string error)
    {
        if (HasPendingNotifications) { error = "Task presentation must be idle before hydration."; return false; }
        if (catalog == null || !catalog.Validate(out error)) { error = "Missing/invalid catalog."; return false; }
        if (!AlaalaSaveValidation.ValidateTasks(records, state, catalog, out error)) return false;
        if (!taskDefinitions.Select(x => x.TaskId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(
            catalog.Tasks.Select(x => x.TaskId).OrderBy(x => x, StringComparer.Ordinal)))
        { error = "Live task definitions do not match the approved catalog."; return false; }
        // Validate actual live stages too: scene/prefab overrides are not guessed from the catalog.
        foreach (var record in records)
        {
            var definition = taskDefinitions.First(x => x.TaskId == record.taskId);
            if (record.stageId.Length > 0 && !definition.Stages.Any(x => x.StageId == record.stageId))
            { error = "Stage missing from live task definition: " + record.stageId; return false; }
        }
        return true;
    }
}
