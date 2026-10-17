using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Seeds the late Part 1 story baseline for direct PosadaRoom Part 2 testing.
/// This is an Editor-only session setup and does not create saved player data.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class PostPart1DevelopmentTesting : MonoBehaviour
{
    private const string MainInvestigationTaskId = "main_investigate_pili";
    private const string GatherInformationStageId = "gather_information";
    private const string AlingIkaErrandTaskId = "aling_ika_errand";
    private const string Part2TaskId = "main_visit_maestro_ben";

    [SerializeField] private bool enablePostPart1Testing;
    [SerializeField] private bool seedPriorNpcInteractions;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || !enablePostPart1Testing)
            return;

        if (gameObject.scene.name != "PosadaRoom" ||
            SceneManager.GetActiveScene() != gameObject.scene)
        {
            return;
        }

        ApplyPostPart1Baseline(seedPriorNpcInteractions);
#endif
    }

#if UNITY_EDITOR
    private static void ApplyPostPart1Baseline(bool seedNpcInteractions)
    {
        // Seed the task state before the development helper instantiates TaskManager.
        SessionStoryState.SetFlag("task_active:" + MainInvestigationTaskId, false);
        SessionStoryState.SetFlag("task_completed:" + MainInvestigationTaskId, true);
        SessionStoryState.SetString("task_stage:" + MainInvestigationTaskId, "return_to_anday");
        SessionStoryState.SetFlag("main_investigate_pili_started", true);

        SessionStoryState.SetFlag("task_active:" + AlingIkaErrandTaskId, false);
        SessionStoryState.SetFlag("task_completed:" + AlingIkaErrandTaskId, true);

        SessionStoryState.SetFlag("makamisa_pili_intro_seen", true);
        SessionStoryState.SetFlag("gather_information_reaction_seen", true);
        SessionStoryState.SetFlag("randomhouse_maestro_lead_received", true);

        SessionStoryState.SetFlag("aling_ika_started", true);
        SessionStoryState.SetFlag("aling_ika_payment_received", true);
        SessionStoryState.SetFlag("aling_ika_completed", true);

        SessionStoryState.SetFlag("maestro_ben_lead_received", true);
        SessionStoryState.SetFlag("maestro_ben_intro_seen", true);
        SessionStoryState.SetFlag("maestro_ben_completed", true);

        SessionStoryState.SetFlag("church_manuscript_investigated", true);

        SessionStoryState.SetFlag("makamisa_anday_discovered", true);
        SessionStoryState.SetFlag("makamisa_anday_first_conversation_complete", true);
        SessionStoryState.SetFlag("makamisa_anday_recollection_started", true);
        SessionStoryState.SetFlag("makamisa_anday_recollection_complete", true);
        SessionStoryState.SetFlag("makamisa_anday_post_recollection_payoff_applied", true);
        SessionStoryState.SetFlag("makamisa_part1_return_transport_started", true);

        SessionStoryState.SetFlag("posada_first_sleep_completed", true);
        SessionStoryState.SetFlag("aling_sela_intro_completed", true);
        SessionStoryState.SetFlag("posada_morning_inventory_intro_completed", true);

        SessionStoryState.SetFlag("draftworld_movement_tutorial_complete", true);
        SessionStoryState.SetFlag("draftworld_run_tutorial_complete", true);
        SessionStoryState.SetFlag("draftworld_jump_tutorial_complete", true);

        SessionStoryState.SetFlag(Part2PosadaCutsceneController.CompletionFlag, false);
        SessionStoryState.SetFlag("task_active:" + Part2TaskId, false);
        SessionStoryState.SetFlag("task_completed:" + Part2TaskId, false);
        SessionStoryState.SetString("task_stage:" + Part2TaskId, string.Empty);

        if (seedNpcInteractions)
            SeedPriorNpcInteractions();

        // Let DraftWorld's normal DayNightManager select its authored daytime setup.
        GameFlags.isMorning = true;
    }

    private static void SeedPriorNpcInteractions()
    {
        // Match TaskManager's persisted progress key format without registering live task progress.
        string[] gatherInformationProgressIds =
        {
            "mang_iko",
            "mang_kardo",
            "mang_nardo",
            "mang_tibo",
            "nena",
            "toto",
            "questioned_maria_nene"
        };

        foreach (string progressId in gatherInformationProgressIds)
        {
            SessionStoryState.SetFlag(
                GetProgressFlag(MainInvestigationTaskId, GatherInformationStageId, progressId),
                true);
        }

        SessionStoryState.SetFlag("church_mang_lando_conversation_completed", true);
        SessionStoryState.SetFlag("church_aling_marta_conversation_completed", true);
        SessionStoryState.SetFlag("church_aling_pilar_conversation_completed", true);
        SessionStoryState.SetFlag("church_aling_elena_conversation_completed", true);
        SessionStoryState.SetFlag("church_aling_rosa_conversation_completed", true);
        SessionStoryState.SetFlag("church_mang_tomas_conversation_completed", true);
        SessionStoryState.SetFlag("church_mang_pedro_conversation_completed", true);

        SessionStoryState.SetFlag("randomhouse_resident_information_completed", true);
        SessionStoryState.SetFlag("randomhouse1_anday_information_completed", true);
    }

    private static string GetProgressFlag(string taskId, string stageId, string progressId)
    {
        // Mirrors TaskManager's task_progress:<task>:<stage>:<progress> storage key.
        return "task_progress:" + taskId + ":" + stageId + ":" + progressId;
    }
#endif
}
