using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Alaala.Saving
{
    // Configuration references only. These asset references never enter a save snapshot.
    public sealed class AlaalaSaveCatalog : ScriptableObject
    {
        [SerializeField] private TaskManager taskDefinitionSource;
        [SerializeField] private InventoryItemData[] items = Array.Empty<InventoryItemData>();
        [SerializeField] private PersonData[] people = Array.Empty<PersonData>();
        [SerializeField] private ObservationData[] observations = Array.Empty<ObservationData>();
        [SerializeField] private FragmentData[] fragments = Array.Empty<FragmentData>();
        [SerializeField] private ReflectionData[] reflections = Array.Empty<ReflectionData>();

        public TaskData[] Tasks => taskDefinitionSource == null ? Array.Empty<TaskData>() : taskDefinitionSource.SaveDefinitions;
        internal GameObject TaskManagerTemplate => taskDefinitionSource != null ? taskDefinitionSource.gameObject : null;
        public IReadOnlyList<InventoryItemData> Items => Array.AsReadOnly(items);
        public IReadOnlyList<PersonData> People => Array.AsReadOnly(people);
        public IReadOnlyList<ObservationData> Observations => Array.AsReadOnly(observations);
        public IReadOnlyList<FragmentData> Fragments => Array.AsReadOnly(fragments);
        public IReadOnlyList<ReflectionData> Reflections => Array.AsReadOnly(reflections);
        public InventoryItemData Item(string id) => items.FirstOrDefault(x => x != null && x.ItemID == id);

        public bool Validate(out string error)
        {
            error = null;
            if (taskDefinitionSource == null || items == null || people == null || observations == null || fragments == null || reflections == null)
                return Fail("Catalog has missing arrays or task-definition source.", out error);
            if (!Unique(items.Select(x => x == null ? null : x.ItemID)) ||
                !Unique(people.Select(x => x == null ? null : x.personID)) ||
                !Unique(observations.Select(x => x == null ? null : x.observationID)) ||
                !Unique(fragments.Select(x => x == null ? null : x.fragmentID)) ||
                !Unique(reflections.Select(x => x == null ? null : x.reflectionID)))
                return Fail("Catalog contains missing, duplicate or development IDs.", out error);
            if (people.Any(x => x.descriptionStages == null || x.descriptionStages.Count == 0) ||
                fragments.Any(x => x.interpretationStages == null || x.interpretationStages.Count == 0))
                return Fail("A staged journal entry has no authored stages.", out error);
            var tasks = Tasks;
            if (tasks.Length == 0 || !Unique(tasks.Select(x => x == null ? null : x.TaskId)))
                return Fail("Invalid task catalog.", out error);
            foreach (var task in tasks)
            {
                if (task.Stages == null || !Unique(task.Stages.Select(s => s == null ? null : s.StageId)))
                    return Fail("Invalid stages for " + task.TaskId, out error);
                foreach (var stage in task.Stages)
                    if (stage.RequiredCount < 0 || stage.AllowedProgressIds == null || !Unique(stage.AllowedProgressIds))
                        return Fail("Invalid progress IDs for " + task.TaskId + "/" + stage.StageId, out error);
            }
            return true;
        }

        private static bool Unique(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.StartsWith("test_", StringComparison.Ordinal) || !seen.Add(id)) return false;
            return true;
        }

        internal static bool Fail(string message, out string error) { error = message; return false; }
    }
}
