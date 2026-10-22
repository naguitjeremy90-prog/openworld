using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Alaala.Saving
{
    public static class AlaalaSaveValidation
    {
        public static bool Validate(AlaalaSaveSnapshot save, AlaalaSaveCatalog catalog, AlaalaSaveOwner owner, out string error)
        {
            error = null;
            if (save == null || catalog == null) return Fail("Missing snapshot/catalog.", out error);
            if (!catalog.Validate(out error)) return false;
            if (save.schemaVersion != AlaalaSaveSchema.Version || save.contentVersion != AlaalaSaveSchema.ContentVersion)
                return Fail("Unsupported save schema/content version; migration is required.", out error);
            if (string.IsNullOrWhiteSpace(owner.TitleId) || string.IsNullOrWhiteSpace(owner.AccountId) ||
                save.titleId != owner.TitleId || save.playFabAccountId != owner.AccountId)
                return Fail("Snapshot belongs to a different or unverified account/title.", out error);
            if (save.runKind != SaveRunKind.Genuine || !Guid.TryParseExact(save.runId, "N", out _) ||
                save.saveRevision < 1 || string.IsNullOrWhiteSpace(save.gameVersion))
                return Fail("Unverified/development run or invalid revision/header.", out error);
            if (save.world == null || save.tasks == null || save.inventory == null || save.activeMainTaskId == null)
                return Fail("Missing snapshot sections.", out error);
            var registry = new AlaalaDurableStateRegistry(catalog);
            if (!registry.Validate(save.session, out error) || !ValidateTasks(save.tasks, save.session, catalog, out error) ||
                !ValidateInventory(save.inventory, save.session, catalog, out error) || !ValidateJournal(save.session, catalog, out error)) return false;
            var view = new AlaalaStateView(save.session);
            string destination = view.Text(KalesaTravelController.CurrentDestinationStateId);
            var destinations = new[] { "bungad_ng_bayan", "plaza", "dulo_ng_bayan" };
            if (destination.Length > 0 && !destinations.Contains(destination))
                return Fail("Unknown authored kalesa destination.", out error);
            foreach (var id in destinations)
                if (view.Flag(KalesaTravelController.CurrentDestinationFlagPrefix + id) != (destination == id))
                    return Fail("Kalesa destination and completed relocation flags differ.", out error);
            var activeMain = save.tasks.Where(x => x.state == TaskState.Active && catalog.Tasks.First(d => d.TaskId == x.taskId).Type == TaskType.Main).ToArray();
            if (activeMain.Length > 1 || save.activeMainTaskId != (activeMain.Length == 0 ? "" : activeMain[0].taskId))
                return Fail("Main task identity is inconsistent.", out error);
            if (view.Flag("inventory_tutorial_reveal_ready") && (!view.Flag("inventory_tutorial_reveal_started") || !view.Flag("inventory_unlocked")))
                return Fail("Inventory reveal history is inconsistent.", out error);
            if (view.Flag("inventory_tutorial_reveal_started") != view.Flag("inventory_tutorial_reveal_ready"))
                return Fail("In-progress inventory reveal is not a resume checkpoint.", out error);
            foreach (var system in new[] { "journal", "inventory", "clarity" })
                if (view.Flag(system + "_tutorial_seen") && !view.Flag(system + "_unlocked"))
                    return Fail("Tutorial completed for a locked system.", out error);
            if (view.Flag("posada_morning_inventory_intro_completed") &&
                (!view.Flag("posada_first_sleep_completed") || !view.Flag("aling_sela_intro_completed") || !view.Flag("inventory_unlocked")))
                return Fail("Posada morning history is incomplete.", out error);
            if (view.Flag("posada_first_sleep_completed") && !save.world.isMorning)
                return Fail("Sleep/morning world state is inconsistent.", out error);
            int presentation = view.Int("maestro_ben_part2_cecilia_ysagani_presentation_phase");
            if (presentation != 0 && presentation != 6)
                return Fail("Partial Maestro Ben presentation is unsupported.", out error);
            int navigation = view.Int("draftworld_night_navigation_phase");
            if (navigation != 0 && navigation != 4)
                return Fail("Partial arrival navigation is unsupported.", out error);
            if (view.Flag("alaala_part2_complete") && (!view.Flag("maestro_ben_part2_cecilia_ysagani_handoff_complete") ||
                !view.Flag("maestro_ben_part2_cecilia_ysagani_presentation_complete") || presentation != 6))
                return Fail("Part 2 completion lacks the genuine completed handoff.", out error);
            if (!ValidateAnalysis(save.inventory, view, out error)) return false;
            if (view.Flag("journal_person_unlocked:padre_agaton") &&
                (!view.Flag("alaala_epilogue_complete") || !save.tasks.Any(t => t.taskId == "main_collect_remaining_sulatin" && t.state == TaskState.Completed)))
                return Fail("Padre Agaton discovery requires the completed genuine epilogue and Main Task.", out error);
            var collectionTask = save.tasks.First(t => t.taskId == "main_collect_remaining_sulatin");
            if (collectionTask.state != TaskState.Inactive)
                foreach (var id in catalog.Tasks.First(t => t.TaskId == collectionTask.taskId).Stages[0].AllowedProgressIds)
                {
                    int progress = view.Progress("task_progress:" + collectionTask.taskId + ":collect_remaining_sulatin:" + id);
                    if (progress > 0 && !save.inventory.collected.Contains(id) ||
                        collectionTask.stageId == "collect_remaining_sulatin" && save.inventory.owned.Contains(id) && progress == 0)
                        return Fail("Sulatin task progress and inventory history are inconsistent: " + id, out error);
                }
            return AlaalaCheckpointRegistry.Validate(save, out error);
        }

        public static bool ValidateTasks(List<AlaalaTaskSnapshot> tasks, AlaalaSessionSnapshot state, AlaalaSaveCatalog catalog, out string error)
        {
            error = null;
            if (catalog == null || !catalog.Validate(out error) || tasks == null || state == null)
                return Fail("Missing/invalid task snapshot or catalog.", out error);
            if (!new AlaalaDurableStateRegistry(catalog).Validate(state, out error)) return false;
            var view = new AlaalaStateView(state);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (tasks.Count != catalog.Tasks.Length) return Fail("Incomplete task snapshot.", out error);
            foreach (var record in tasks)
            {
                if (record == null || record.taskId == null || !seen.Add(record.taskId) || record.stageId == null || record.objective == null ||
                    !Enum.IsDefined(typeof(TaskState), record.state)) return Fail("Malformed/duplicate task record.", out error);
                var definition = catalog.Tasks.FirstOrDefault(x => x.TaskId == record.taskId);
                if (definition == null) return Fail("Unknown task ID: " + record.taskId, out error);
                bool active = view.Flag("task_active:" + record.taskId), completed = view.Flag("task_completed:" + record.taskId);
                if (active && completed) return Fail("Task is both active and completed.", out error);
                var expected = completed ? TaskState.Completed : active ? TaskState.Active : TaskState.Inactive;
                if (record.state != expected || record.stageId != view.Text("task_stage:" + record.taskId))
                    return Fail("Task/session stage or state mismatch.", out error);
                int index = Array.FindIndex(definition.Stages, x => x.StageId == record.stageId);
                if (record.stageId.Length > 0 && index < 0) return Fail("Unknown saved stage; no stage-one fallback is allowed.", out error);
                if (record.state != TaskState.Inactive && definition.Stages.Length > 0 && index < 0)
                    return Fail("Progressed task lacks a known stage.", out error);
                if (record.state == TaskState.Active ? string.IsNullOrWhiteSpace(record.objective) || record.objective.Length > 2048 : record.objective.Length != 0)
                    return Fail("Invalid dynamic objective text.", out error);
                if (record.state == TaskState.Inactive && record.stageId.Length > 0) return Fail("Inactive task has a stage.", out error);
                for (int s = 0; s < definition.Stages.Length; s++)
                {
                    var stage = definition.Stages[s]; int total = 0;
                    foreach (var id in stage.AllowedProgressIds)
                    {
                        string key = "task_progress:" + record.taskId + ":" + stage.StageId + ":" + id;
                        int progress = view.Progress(key); total += progress;
                        if (view.Int(key) > 0 && !view.Flag(key)) return Fail("Task progress counter lacks its completion marker.", out error);
                    }
                    if (total > 0 && (record.state == TaskState.Inactive || s > index)) return Fail("Progress exists beyond the saved task stage.", out error);
                    if (stage.RequiredCount > 0 && (s < index || record.state == TaskState.Completed) && total < stage.RequiredCount)
                        return Fail("Task advanced without its required objectives.", out error);
                }
            }
            return true;
        }

        public static bool ValidateInventory(AlaalaInventorySnapshot inventory, AlaalaSessionSnapshot state, AlaalaSaveCatalog catalog, out string error)
        {
            error = null;
            if (catalog == null || !catalog.Validate(out error) || inventory == null || inventory.owned == null ||
                inventory.collected == null || inventory.consumed == null || state == null)
                return Fail("Missing inventory history/catalog.", out error);
            if (!new AlaalaDurableStateRegistry(catalog).Validate(state, out error)) return false;
            foreach (var list in new[] { inventory.owned, inventory.collected, inventory.consumed })
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in list)
                    if (string.IsNullOrWhiteSpace(id) || !seen.Add(id) || catalog.Item(id) == null)
                        return Fail("Unknown, duplicate or development inventory item ID.", out error);
            }
            if (inventory.owned.Intersect(inventory.consumed).Any() ||
                !new HashSet<string>(inventory.collected).SetEquals(inventory.owned.Concat(inventory.consumed)))
                return Fail("Owned/collected/consumed history is inconsistent.", out error);
            var view = new AlaalaStateView(state);
            foreach (var item in catalog.Items)
            {
                bool owned = inventory.owned.Contains(item.ItemID);
                bool known = view.Flag("inventory.notification.known:" + item.ItemID);
                bool read = view.Flag("inventory.notification.read:" + item.ItemID);
                if ((known || read) && !owned || read && !known || owned && !known)
                    return Fail("Inventory read history does not match ownership.", out error);
            }
            return true;
        }

        private static bool ValidateJournal(AlaalaSessionSnapshot state, AlaalaSaveCatalog catalog, out string error)
        {
            var view = new AlaalaStateView(state); error = null;
            foreach (var entry in catalog.People)
                if (!Entry("People", "person", entry.personID, true, view, out error)) return false;
            foreach (var entry in catalog.Fragments)
                if (!Entry("Fragment", "fragment", entry.fragmentID, true, view, out error)) return false;
            foreach (var entry in catalog.Observations)
                if (!Entry("Observation", "observation", entry.observationID, false, view, out error)) return false;
            foreach (var entry in catalog.Reflections)
            {
                if (!Entry("Reflection", "reflection", entry.reflectionID, false, view, out error)) return false;
                string answer = view.Text("journal_reflection_response:" + entry.reflectionID);
                string follow = view.Text("journal_reflection_follow_up:" + entry.reflectionID);
                if (answer.Length > 0 && (!view.Flag("journal_reflection_unlocked:" + entry.reflectionID) || string.IsNullOrWhiteSpace(answer)) ||
                    follow.Length > 0 && answer.Length == 0)
                    return Fail("Reflection answer/follow-up has no valid discovered entry.", out error);
            }
            return true;
        }

        private static bool Entry(string category, string storage, string id, bool staged, AlaalaStateView view, out string error)
        {
            bool discovered = view.Flag("journal_" + storage + "_unlocked:" + id);
            bool known = view.Flag("journal.e12.known:" + category + ":" + id), read = view.Flag("journal.e12.read:" + category + ":" + id);
            if ((known || read) && !discovered || read && !known || staged && !discovered && view.Int("journal_" + storage + "_stage:" + id) != 0)
                return Fail("Journal discovery/stage/read history is inconsistent: " + id, out error);
            error = null; return true;
        }

        private static bool ValidateAnalysis(AlaalaInventorySnapshot inventory, AlaalaStateView view, out string error)
        {
            var requirements = new[]
            {
                new[] { "present_pili_archival_rizal_novel_pagsusuri_complete", "present_pili_archival_rizal_novel" },
                new[] { "present_pili_antique_tagalog_espanol_manuscripts_pagsusuri_complete", "present_pili_tagalog_espanol_manuscripts" },
                new[] { "present_pili_antique_letter_sealed_envelope_pagsusuri_complete", "present_pili_letter_sealed_envelope" },
                new[] { "present_pili_weathered_filipino_archive_note_pagsusuri_complete", "present_pili_filipino_archive_note" },
                new[] { "church_manuscript_investigated", "church_lumang_sulatin" }
            };
            foreach (var pair in requirements)
                if (view.Flag(pair[0]) && !inventory.collected.Contains(pair[1]))
                    return Fail("Completed investigation is missing its awarded item history: " + pair[0], out error);
            error = null; return true;
        }
        private static bool Fail(string message, out string error) => AlaalaSaveCatalog.Fail(message, out error);
    }
}
