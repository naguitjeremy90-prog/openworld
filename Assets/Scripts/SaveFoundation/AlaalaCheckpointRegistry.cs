using System.Linq;
using UnityEngine.SceneManagement;

namespace Alaala.Saving
{
    public sealed class AlaalaCheckpointDefinition
    {
        public string Id { get; }
        public string ScenePath => "Assets/Scenes/DraftWorld.unity";
        public string SpawnRoute => "MaestroSpawn";
        public bool RequiresEarlyPart3Provenance => false;
        internal AlaalaCheckpointDefinition(string id) { Id = id; }
    }

    public static class AlaalaCheckpointRegistry
    {
        public const string SulatinSearch = "draftworld.sulatin_search";
        public const string CompletedEpilogue = "draftworld.completed_epilogue";
        public static bool TryGet(string id, out AlaalaCheckpointDefinition checkpoint)
        {
            checkpoint = id == SulatinSearch || id == CompletedEpilogue ? new AlaalaCheckpointDefinition(id) : null;
            return checkpoint != null;
        }

        public static bool Validate(AlaalaSaveSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot == null || snapshot.session == null || snapshot.session.booleans == null || snapshot.session.integers == null ||
                snapshot.session.strings == null || snapshot.session.booleans.Any(x => x == null) ||
                snapshot.session.integers.Any(x => x == null) || snapshot.session.strings.Any(x => x == null) ||
                snapshot.tasks == null || snapshot.tasks.Any(t => t == null) || snapshot.world == null ||
                !TryGet(snapshot.checkpointId, out var checkpoint))
                return AlaalaSaveCatalog.Fail("Unknown/unsupported checkpoint. Part 3 cinematic resumes are not supported.", out error);
            if (snapshot.targetScene != checkpoint.ScenePath || snapshot.spawnRoute != checkpoint.SpawnRoute ||
                SceneUtility.GetBuildIndexByScenePath(checkpoint.ScenePath) < 0)
                return AlaalaSaveCatalog.Fail("Checkpoint scene/spawn/build identity is invalid.", out error);
            var view = new AlaalaStateView(snapshot.session);
            foreach (var flag in new[] { "alaala_part2_complete", "alaala_post_part2_draftworld_arrival_handled", "posada_first_sleep_completed",
                "journal_unlocked", "inventory_unlocked", "clarity_unlocked", "inventory_tutorial_reveal_ready" })
                if (!view.Flag(flag)) return AlaalaSaveCatalog.Fail("Checkpoint prerequisite missing: " + flag, out error);
            if (!snapshot.world.isMorning || !Completed(snapshot, "main_investigate_pili") || !Completed(snapshot, "main_visit_maestro_ben"))
                return AlaalaSaveCatalog.Fail("Checkpoint requires the completed earlier Main Tasks and morning state.", out error);
            var remaining = snapshot.tasks.FirstOrDefault(x => x.taskId == "main_collect_remaining_sulatin");
            if (remaining == null) return AlaalaSaveCatalog.Fail("Remaining-Sulatin task record is missing.", out error);
            if (snapshot.checkpointId == SulatinSearch)
            {
                if (remaining.state != TaskState.Active || (remaining.stageId != "collect_remaining_sulatin" && remaining.stageId != "return_to_maestro_ben") ||
                    view.Flag("alaala_part3_sulatin_handoff_complete") || view.Flag("alaala_part3_light_transport_started") || view.Flag("alaala_epilogue_complete"))
                    return AlaalaSaveCatalog.Fail("Not a stable post-Part-2 Sulatin search checkpoint.", out error);
            }
            else
            {
                foreach (var flag in new[] { "alaala_part3_arrival_complete", "alaala_part3_ch11_complete", "alaala_part3_ch11_white_presented",
                    "alaala_part3_ch12_dialogue_complete", "alaala_part3_ch12_ellipsis_complete", "alaala_part3_ch12_complete", "alaala_part3_ch13_complete",
                    "alaala_epilogue_dialogue_complete", "alaala_epilogue_complete", "journal_person_unlocked:padre_agaton" })
                    if (!view.Flag(flag)) return AlaalaSaveCatalog.Fail("Completed epilogue prerequisite missing: " + flag, out error);
                if (remaining.state != TaskState.Completed || string.IsNullOrWhiteSpace(view.Text("journal_reflection_response:part3_final_pagninilay")))
                    return AlaalaSaveCatalog.Fail("Final task/reflection is incomplete.", out error);
            }
            return true;
        }
        private static bool Completed(AlaalaSaveSnapshot snapshot, string id) => snapshot.tasks.Any(x => x.taskId == id && x.state == TaskState.Completed);
    }
}
