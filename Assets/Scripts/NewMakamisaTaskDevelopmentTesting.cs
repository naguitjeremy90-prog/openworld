using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using DialogueEditor;
#endif

/// <summary>Optional real TaskManager state for starting NEWMAKAMISA directly in the Editor.</summary>
[DefaultExecutionOrder(-29999)]
[DisallowMultipleComponent]
public sealed class NewMakamisaTaskDevelopmentTesting : MonoBehaviour
{
    private const string MainTaskId = "main_investigate_pili";
    private const string SideTaskId = "aling_ika_errand";
    private const string KutseroSideTaskId = KutseroPasabiSideTaskController.SideTaskId;
    private const string CurrentStageId = "investigate_church_document";
    private const string PostChurchObjective = "Alamin ang Nangyari Pagkatapos ng Misa";
    private const string RecollectionStageId = "return_to_anday";
    private const string RecollectionStartedFlag = AndayRecollectionEntryController.StartedFlag;
    private const string RecollectionCompleteFlag = AndayRecollectionEntryController.CompleteFlag;
    private const string PostRecollectionPayoffFlag =
        "makamisa_anday_post_recollection_payoff_applied";
    private const string ReturnTransportStartedFlag =
        "makamisa_part1_return_transport_started";
    private const string AndayDiscoveredFlag = "makamisa_anday_discovered";
    private const string AndayFirstConversationFlag =
        "makamisa_anday_first_conversation_complete";
    private const string AndayPersonId = "anday";
    private const string BinasbasangBataId = "hindi_binasbasang_bata";
    private const string RopeItemId = "kutsero_rope";
    private const string PaymentItemId = "aling_ika_payment";

    private static readonly string[] RequiredPriorFlags =
    {
        "makamisa_pili_intro_seen",
        "gather_information_reaction_seen",
        "aling_ika_started",
        "aling_ika_payment_received",
        "aling_ika_completed",
        "maestro_ben_lead_received",
        "maestro_ben_intro_seen",
        "maestro_ben_completed",
        "church_manuscript_investigated",
        "makamisa_sebia_barang_overheard_complete",
        "makamisa_fura_clodio_overheard_complete",
        "makamisa_lopez_paquito_overheard_complete",
        AndayDiscoveredFlag,
        AndayFirstConversationFlag,
        "main_investigate_pili_started"
    };

    private static readonly string[] GatherProgressIds =
    {
        "overheard_maria_nene", "questioned_maria_nene", "mang_kardo",
        "mang_nardo", "mang_iko", "mang_tibo", "aling_ika_lead",
        "nena", "toto", "randomhouse_resident", "randomhouse1_anday",
        "church_aling_rosa", "church_aling_pilar", "church_aling_marta",
        "church_mang_tomas", "church_mang_lando", "church_mang_pedro",
        "church_aling_elena"
    };

    private static readonly string[] PriorPeopleIds =
    {
        "fura", "clodio", "dr_lopez", "don_paquito", "capitana_barang", "mana_sebia"
    };

#if UNITY_EDITOR
    [Header("END-OF-PART-1 TESTING (EDITOR ONLY)")]
    [SerializeField] private NPCConversationTrigger andayConversationTrigger;
    [SerializeField] private NPCConversation andayReturnConversation;
#endif

    [Header("DIRECT NEWMAKAMISA DEVELOPMENT TESTING ONLY")]
    [SerializeField] private bool enablePriorTaskState = false;
    [SerializeField] private TaskManager taskManagerPrefab;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!enablePriorTaskState ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "NEWMAKAMISA" ||
            TaskManager.Instance != null)
            return;

        if (taskManagerPrefab == null || !UnityEditor.EditorUtility.IsPersistent(taskManagerPrefab))
        {
            Debug.LogError("Direct NEWMAKAMISA Task testing requires the configured TaskManager prefab asset.", this);
            return;
        }

        // Set only the task state earned before NEWMAKAMISA, before the prefab's Awake restores it.
        SessionStoryState.SetFlag("task_completed:" + SideTaskId, true);
        SessionStoryState.SetFlag("task_active:" + SideTaskId, false);
        SessionStoryState.SetFlag("task_active:" + MainTaskId, true);
        SessionStoryState.SetFlag("task_completed:" + MainTaskId, false);
        SessionStoryState.SetString("task_stage:" + MainTaskId, CurrentStageId);

        TaskManager manager = Instantiate(taskManagerPrefab);
        if (manager != TaskManager.Instance || !manager.IsCurrentStage(MainTaskId, CurrentStageId) ||
            !manager.RestoreObjectiveForDirectSceneTesting(MainTaskId, PostChurchObjective))
        {
            Debug.LogError("Direct NEWMAKAMISA Task state could not be restored from the configured prefab.", this);
        }
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Prepare Main Story + Start Anday Recollection Test")]
    private void PrepareMainStoryAndStartRecollectionTest()
    {
        PrepareAndStartRecollectionTest(false);
    }

    [ContextMenu("Prepare FULL NEWMAKAMISA + Start Anday Recollection Test")]
    private void PrepareFullNewMakamisaAndStartRecollectionTest()
    {
        PrepareAndStartRecollectionTest(true);
    }

    private void PrepareAndStartRecollectionTest(bool includeOptionalContent)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        ConversationManager conversationManager = ConversationManager.Instance;
        TaskManager manager = TaskManager.Instance;
        ReconstructionJournalManager journal = ReconstructionJournalManager.Instance;
        InventoryManager inventory = InventoryManager.Instance;
        AndayRecollectionEntryController entryController =
            FindAnyObjectByType<AndayRecollectionEntryController>();
        KutseroPasabiSideTaskController sideTaskController =
            FindAnyObjectByType<KutseroPasabiSideTaskController>();
        KalesaTravelController travelController =
            FindAnyObjectByType<KalesaTravelController>();

        if (!Application.isPlaying || activeScene.name != "NEWMAKAMISA" ||
            conversationManager == null || !conversationManager.isActiveAndEnabled ||
            conversationManager.IsConversationActive || StorySequenceCoordinator.IsStorySequenceActive ||
            journal == null || journal.IsOpen || inventory == null ||
            andayConversationTrigger == null || !andayConversationTrigger.isActiveAndEnabled ||
            andayReturnConversation == null || entryController == null ||
            (includeOptionalContent && (sideTaskController == null || travelController == null)) ||
            (manager == null && (taskManagerPrefab == null ||
                                 !UnityEditor.EditorUtility.IsPersistent(taskManagerPrefab))))
        {
            Debug.LogWarning(
                "Anday recollection test was not started. Require active NEWMAKAMISA, idle dialogue/story sequence, " +
                "Journal, Inventory, the configured Anday trigger/conversation, a TaskManager or valid TaskManager " +
                "prefab, and the optional Kutsero/Kalesa systems for the FULL profile.", this);
            return;
        }

        if (manager == null)
        {
            manager = Instantiate(taskManagerPrefab);
            if (manager == null || TaskManager.Instance != manager)
            {
                Debug.LogWarning(
                    "Anday recollection test was not started because the configured TaskManager prefab " +
                    "could not initialize.", this);
                return;
            }
        }

        PrepareSessionFlags(includeOptionalContent);
        if (!PrepareMainTask(manager) || !PrepareJournal(journal) ||
            !PrepareInventory(inventory))
        {
            Debug.LogError(
                "Anday recollection test state was not fully prepared; the Anday conversation was not started.",
                this);
            return;
        }

        if (includeOptionalContent)
        {
            if (!sideTaskController.PrepareKalesaTravelTest())
            {
                Debug.LogWarning(
                    "The FULL Anday test profile could not prepare the existing Kutsero/Kalesa state; " +
                    "the Anday recollection conversation was not started.", this);
                return;
            }
            SessionStoryState.SetFlag("task_active:" + KutseroSideTaskId, false);
            SessionStoryState.SetFlag("task_completed:" + KutseroSideTaskId, true);
        }

        // Let TaskManager, Journal, inventory, and marker listeners process the
        // prepared state before starting the same conversation used in production.
        StartCoroutine(StartAndayConversationNextFrame(andayConversationTrigger, andayReturnConversation));
    }

    private static void PrepareSessionFlags(bool includeOptionalContent)
    {
        foreach (string flag in RequiredPriorFlags)
            SessionStoryState.SetFlag(flag, true);

        SessionStoryState.SetFlag(RecollectionStartedFlag, false);
        SessionStoryState.SetFlag(RecollectionCompleteFlag, false);
        SessionStoryState.SetFlag(PostRecollectionPayoffFlag, false);
        SessionStoryState.SetFlag(ReturnTransportStartedFlag, false);
        SessionStoryState.SetFlag("makamisa_kutsero_pasabi_offer_available", false);

        // Aling Ika's payment errand is already completed on the legitimate lead path.
        SessionStoryState.SetFlag("task_active:" + SideTaskId, false);
        SessionStoryState.SetFlag("task_completed:" + SideTaskId, true);

        // Select a clean optional-content baseline, then apply the FULL profile below.
        SessionStoryState.SetFlag("task_active:" + KutseroSideTaskId, false);
        SessionStoryState.SetFlag("task_completed:" + KutseroSideTaskId, false);
        SessionStoryState.SetFlag(KutseroPasabiSideTaskController.CompletedFlag, false);
        SessionStoryState.SetFlag(KutseroPasabiSideTaskController.TravelUnlockedFlag, false);
        SessionStoryState.SetFlag(KutseroPasabiSideTaskController.OfferAvailableFlag, false);
        SessionStoryState.SetFlag(
            KutseroPasabiSideTaskController.TravelUnlockedFlag, includeOptionalContent);

        // NEWMAKAMISA is reached after the morning wake-up in the real story.
        GameFlags.isMorning = true;
        SpawnData.spawnPointName = string.Empty;
    }

    private static bool PrepareMainTask(TaskManager manager)
    {
        SessionStoryState.SetFlag("task_active:" + MainTaskId, true);
        SessionStoryState.SetFlag("task_completed:" + MainTaskId, false);
        SessionStoryState.SetString("task_stage:" + MainTaskId, string.Empty);

        foreach (string progressId in GatherProgressIds)
        {
            string key = "task_progress:" + MainTaskId + ":gather_information:" + progressId;
            SessionStoryState.SetFlag(key, false);
            SessionStoryState.SetInt(key, 0);
        }

        // Force the live TaskManager cache through its own stage API so its
        // objective and tracker state match the session state.
        bool resetStage = manager.AdvanceTaskStage(MainTaskId, "gather_information");
        bool preparedStage = manager.AdvanceTaskStage(MainTaskId, RecollectionStageId);
        return resetStage && preparedStage &&
               manager.GetTaskState(MainTaskId) == TaskState.Active &&
               manager.IsCurrentStage(MainTaskId, RecollectionStageId);
    }

    private static bool PrepareJournal(ReconstructionJournalManager journal)
    {
        bool resetAnday = journal.SetPersonUnlockedForDevelopmentTesting(AndayPersonId, false);
        bool resetRecollection = journal.SetObservationUnlockedForDevelopmentTesting(
            BinasbasangBataId, false);

        bool prepared = journal.SetObservationUnlockedForDevelopmentTesting("ang_panaginip", true) &
            journal.SetObservationUnlockedForDevelopmentTesting("ang_hindi_natapos_na_akda", true) &
            journal.SetObservationUnlockedForDevelopmentTesting("mga_bulung_bulungan_sa_pili", true) &
            journal.SetFragmentUnlockedForDevelopmentTesting("ang_lumang_sulatin", true);

        foreach (string personId in PriorPeopleIds)
            prepared &= journal.SetPersonUnlockedForDevelopmentTesting(personId, true);

        return resetAnday && resetRecollection && prepared;
    }

    private static bool PrepareInventory(InventoryManager inventory)
    {
        while (inventory.HasItem(PaymentItemId))
            inventory.RemoveItem(PaymentItemId);
        while (inventory.HasItem(RopeItemId))
            inventory.RemoveItem(RopeItemId);
        return !inventory.HasItem(PaymentItemId) && !inventory.HasItem(RopeItemId);
    }

    private System.Collections.IEnumerator StartAndayConversationNextFrame(
        NPCConversationTrigger trigger, NPCConversation conversation)
    {
        yield return null;

        ConversationManager manager = ConversationManager.Instance;
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "NEWMAKAMISA" ||
            manager == null || !manager.isActiveAndEnabled || manager.IsConversationActive ||
            StorySequenceCoordinator.IsStorySequenceActive || trigger == null ||
            !trigger.isActiveAndEnabled || conversation == null ||
            !trigger.StartConversationProgrammatically(conversation))
        {
            Debug.LogWarning(
                "Prepared Anday test state is ready, but the real Anday return conversation could not start. " +
                "No recollection scene was loaded directly.", this);
        }
    }
#endif
}
