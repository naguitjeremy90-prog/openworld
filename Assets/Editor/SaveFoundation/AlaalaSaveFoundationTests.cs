#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alaala.Saving;
using UnityEditor;
using UnityEngine;

// Explicitly invoked Edit Mode checks. No startup hooks, API calls or player-save files.
public static class AlaalaSaveFoundationTests
{
    private const string CatalogPath = "Assets/Resources/SaveFoundation/AlaalaSaveCatalog.asset";
    private static readonly BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        var catalog = AssetDatabase.LoadAssetAtPath<AlaalaSaveCatalog>(CatalogPath);
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
        Check(catalog != null && catalog.Validate(out _), "Catalog validity");
        var owner = (AlaalaSaveOwner)typeof(AlaalaSaveOwner).GetConstructor(Fields, null, new[] { typeof(string), typeof(string) }, null)
            .Invoke(new object[] { "VALIDATION_TITLE", "VALIDATION_ACCOUNT" });
        var save = Fixture(catalog);
        Check(AlaalaSaveValidation.Validate(save, catalog, owner, out var error), "Baseline fixture: " + error);
        var registry = new AlaalaDurableStateRegistry(catalog);
        var roundTrip = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save));
        Check(JsonUtility.ToJson(roundTrip) == JsonUtility.ToJson(save), "JSON lossless roundtrip");
        Check(roundTrip.session.strings.Any(x => x.value.Contains("pagbabago\n")), "Multiline Filipino reflection survives");

        void Reject(Action<AlaalaSaveSnapshot> mutation, string label)
        {
            var broken = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save)); mutation(broken);
            Check(!AlaalaSaveValidation.Validate(broken, catalog, owner, out _), label);
        }
        Reject(x => x.schemaVersion++, "Unknown schema");
        Reject(x => x.contentVersion = "other", "Unknown content version");
        Reject(x => x.playFabAccountId = "another", "Wrong owner");
        Reject(x => x.titleId = "another", "Wrong title");
        Reject(x => x.runKind = SaveRunKind.Synthetic, "Synthetic provenance");
        Reject(x => x.runKind = SaveRunKind.Unverified, "Unknown provenance");
        Reject(x => x.runId = "", "Missing run ID");
        Reject(x => x.saveRevision = 0, "Invalid revision");
        Reject(x => x.inventory = null, "Null inventory");
        Reject(x => x.world = null, "Null world");
        Reject(x => x.session.strings = null, "Null string list");
        Reject(x => x.checkpointId = "NEWMAKAMISA.chase", "Unsafe Part 3 checkpoint");
        Reject(x => x.targetScene = "Assets/Scenes/NEWMAKAMISA.unity", "Scene substitution");
        Reject(x => x.spawnRoute = "Part3Spawn", "Spawn/provenance substitution");
        Reject(x => SetText(x.session, KalesaTravelController.CurrentDestinationStateId, "missing_stop"), "Unknown kalesa stop");
        Reject(x => SetFlag(x.session, "makamisa_kalesa_at_plaza"), "Mismatched kalesa flags");
        Reject(x => x.session.booleans.Add(new SaveBoolean { key = "unknown_story_flag", value = true }), "Unknown Boolean");
        Reject(x => x.session.integers.Add(new SaveInteger { key = "unknown_counter", value = 1 }), "Unknown integer");
        Reject(x => x.session.strings.Add(new SaveString { key = "unknown_answer", value = "text" }), "Unknown string");
        Reject(x => x.session.booleans.Add(new SaveBoolean { key = "alaala_epilogue_development_test", value = true }), "Development key");
        Reject(x => x.session.booleans.Add(new SaveBoolean { key = "alaala_part3_ch10_owner_active", value = true }), "Cinematic claim");
        Reject(x => x.session.integers.Add(new SaveInteger { key = "alaala_part3_ch12_cinematic_step", value = 2 }), "Cinematic step");
        Reject(x => x.session.booleans.Add(x.session.booleans[0]), "Duplicate Boolean");
        Reject(x => x.session.integers.Add(x.session.integers[0]), "Duplicate integer");
        Reject(x => x.session.strings.Add(x.session.strings[0]), "Duplicate string");
        Reject(x => x.tasks[0].taskId = "missing_task", "Unknown task");
        Reject(x => x.tasks.RemoveAt(0), "Missing task");
        Reject(x => x.tasks.First(t => t.taskId == "main_collect_remaining_sulatin").stageId = "missing_stage", "Unknown stage");
        Reject(x => x.activeMainTaskId = "main_visit_maestro_ben", "Wrong Main Task identity");
        Reject(x => x.session.booleans.Add(new SaveBoolean { key = "task_completed:main_collect_remaining_sulatin", value = true }), "Active/completed collision");
        Reject(x => x.inventory.owned.Add(x.inventory.owned[0]), "Duplicate inventory");
        Reject(x => x.inventory.owned[0] = "test_letter", "Development item");
        Reject(x => x.inventory.collected.Clear(), "Ownership/history mismatch");
        Reject(x => x.inventory.consumed.Add(x.inventory.owned[0]), "Owned/consumed collision");
        Reject(x => x.session.booleans.Add(new SaveBoolean { key = "journal_person_unlocked:missing_person", value = true }), "Unknown journal ID");
        Reject(x => x.session.integers.Add(new SaveInteger { key = "journal_person_stage:cecilia", value = 999 }), "Invalid interpretation stage");
        Reject(x => SetFlag(x.session, "journal_reflection_unlocked:anday_story_reflection", false), "Answer for undiscovered reflection");
        Reject(x => x.session.strings.First(v => v.key == "journal_reflection_response:anday_story_reflection").value = null, "Null answer");
        Reject(x => x.session.strings.First(v => v.key == "journal_reflection_response:anday_story_reflection").value = new string('a', 32769), "Oversized answer");
        Reject(x => SetFlag(x.session, "present_pili_weathered_filipino_archive_note_pagsusuri_complete", true), "Completed scan without reward");
        Reject(x => SetFlag(x.session, "inventory_tutorial_reveal_started", false), "Inconsistent reveal history");
        Reject(x => SetFlag(x.session, "journal_person_unlocked:padre_agaton", true), "Early Padre discovery");

        // Verify asset coverage, scene identity and authored spawn without loading/mutating scenes.
        var productionItems = AssetDatabase.FindAssets("t:InventoryItemData", new[] { "Assets/Inventory/Items" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !System.IO.Path.GetFileName(p).StartsWith("Test", StringComparison.Ordinal))
            .Select(AssetDatabase.LoadAssetAtPath<InventoryItemData>).ToArray();
        Check(productionItems.Length == 9 && productionItems.All(x => catalog.Item(x.ItemID) == x), "Complete production item resolver");
        var scenePath = "Assets/Scenes/DraftWorld.unity";
        string scene = System.IO.File.ReadAllText(scenePath);
        Check(scene.Split(new[] { "  m_Name: MaestroSpawn\n", "  m_Name: MaestroSpawn\r\n" }, StringSplitOptions.None).Length == 2, "Unique authored MaestroSpawn");
        Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath), "Checkpoint scene enabled");
        Check(scene.Contains("Assembly-CSharp::PlayerSpawner"), "Authored PlayerSpawner");

        // Preserve any existing Editor-only session values and subscriptions; never call ResetSession.
        var flagsField = typeof(SessionStoryState).GetField("completedFlags", BindingFlags.Static | BindingFlags.NonPublic);
        var intsField = typeof(SessionStoryState).GetField("integerValues", BindingFlags.Static | BindingFlags.NonPublic);
        var stringsField = typeof(SessionStoryState).GetField("stringValues", BindingFlags.Static | BindingFlags.NonPublic);
        var flags = (HashSet<string>)flagsField.GetValue(null); var oldFlags = flags.ToArray();
        var ints = (Dictionary<string, int>)intsField.GetValue(null); var oldInts = ints.ToArray();
        var strings = (Dictionary<string, string>)stringsField.GetValue(null); var oldStrings = strings.ToArray();
        var knownItems = (HashSet<string>)typeof(InventoryItemReadState).GetField("knownItems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var oldKnownItems = knownItems.ToArray();
        int flagEvents = 0, itemEvents = 0, taskEvents = 0, journalEvents = 0;
        Action<string, bool> flagListener = (a, b) => flagEvents++;
        SessionStoryState.FlagChanged += flagListener;
        var temporaries = new List<GameObject>();
        try
        {
            Check(SessionStoryState.TryRestoreDurableSilently(save.session, registry, out error), "Silent session install: " + error);
            Check(SessionStoryState.TryCaptureDurable(registry, out var captured, out error), "Capture all three types: " + error);
            Check(captured.booleans.Count == save.session.booleans.Count && captured.integers.Count == save.session.integers.Count &&
                captured.strings.Count == save.session.strings.Count, "No omitted state types");
            Check(captured.strings.Any(x => x.key == "journal_reflection_response:anday_story_reflection" && x.value.Contains("pagbabago\n")), "Exact player response");
            Check(JsonUtility.ToJson(captured) == JsonUtility.ToJson(Canonical(save.session)), "Deterministic key ordering");
            string before = JsonUtility.ToJson(captured);
            var invalid = new AlaalaSessionSnapshot(); invalid.booleans.Add(new SaveBoolean { key = "unknown", value = true });
            Check(!SessionStoryState.TryRestoreDurableSilently(invalid, registry, out _), "Invalid import rejected");
            SessionStoryState.TryCaptureDurable(registry, out captured, out _);
            Check(before == JsonUtility.ToJson(captured), "Invalid import preserves live state");

            var taskObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TaskManager.prefab"));
            taskObject.hideFlags = HideFlags.HideAndDontSave; temporaries.Add(taskObject);
            var tasks = taskObject.GetComponent<TaskManager>();
            tasks.TaskChanged += () => taskEvents++; tasks.StageChanged += (a, b) => taskEvents++; tasks.PresentationChanged += change => taskEvents++;
            Check(tasks.TryHydrateSaveTasksSilently(save.tasks, save.session, catalog, out error), "Silent tasks: " + error);
            Check(tasks.TryGetCurrentObjective("aling_ika_errand", out var objective) && objective == "Ibalik ang bayad. [dynamic fixture]", "Dynamic side objective");
            Check(JsonUtility.ToJson(new TaskList { values = tasks.CaptureSaveTasks() }) == JsonUtility.ToJson(new TaskList { values = save.tasks }), "Task roundtrip");
            Check(tasks.TryHydrateSaveTasksSilently(save.tasks, save.session, catalog, out _), "Idempotent task hydration");

            var inventoryObject = new GameObject("SaveInventoryValidation", typeof(InventoryManager)); inventoryObject.hideFlags = HideFlags.HideAndDontSave; temporaries.Add(inventoryObject);
            var inventory = inventoryObject.GetComponent<InventoryManager>();
            inventory.ItemAdded += item => itemEvents++; inventory.OnInventoryChanged += () => itemEvents++;
            Check(inventory.TryHydrateSaveInventorySilently(save.inventory, save.session, catalog, out error), "Silent inventory: " + error);
            Check(inventory.GetOwnedItems().Count == 2 && inventory.HasItem(save.inventory.owned[0]), "Ownership reconstructed");
            Check(inventory.TryHydrateSaveInventorySilently(save.inventory, save.session, catalog, out _), "Idempotent inventory hydration");
            Check(JsonUtility.ToJson(inventory.CaptureSaveInventory()) == JsonUtility.ToJson(save.inventory), "Consumed/collected history retained");
            Check(!InventoryItemReadState.IsUnread(save.inventory.owned[0]) || InventoryManager.Instance == null, "Read flags not reset by acquisition");

            var journalObject = new GameObject("SaveJournalValidation"); journalObject.hideFlags = HideFlags.HideAndDontSave; temporaries.Add(journalObject);
            var people = journalObject.AddComponent<ReconstructionPeople>(); var observations = journalObject.AddComponent<ReconstructionObservations>();
            var fragments = journalObject.AddComponent<ReconstructionFragments>(); var reflections = journalObject.AddComponent<ReconstructionReflections>();
            var journal = journalObject.AddComponent<ReconstructionJournalManager>();
            void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
            Set(people, "allPeople", catalog.People.ToList()); Set(observations, "allObservations", catalog.Observations.ToList());
            Set(fragments, "allFragments", catalog.Fragments.ToList()); Set(reflections, "allReflections", catalog.Reflections.ToList());
            Set(journal, "people", people); Set(journal, "observations", observations); Set(journal, "fragments", fragments); Set(journal, "reflections", reflections);
            people.EntryUnlocked += entry => journalEvents++; observations.EntryUnlocked += entry => journalEvents++;
            fragments.EntryUnlocked += entry => journalEvents++; reflections.EntryUnlocked += entry => journalEvents++;
            Check(journal.TryHydrateSaveJournalSilently(save.session, catalog, out error), "Silent journal: " + error);
            Check(people.IsPersonUnlocked("cecilia") && !people.IsPersonUnlocked("padre_agaton"), "Only saved discoveries restored");
            Check(reflections.GetSubmittedResponse("anday_story_reflection") == save.session.strings.First(x => x.key == "journal_reflection_response:anday_story_reflection").value, "Reflection answer cache restored");
            Check(journal.TryHydrateSaveJournalSilently(save.session, catalog, out _), "Idempotent journal hydration");
            flags.Add("makamisa_kutsero_pasabi_offer_available"); // Deliberately inject stale state without a gameplay event.
            Check(!tasks.TryHydrateSaveTasksSilently(save.tasks, save.session, catalog, out _), "Stale task hydration rejected");
            Check(!inventory.TryHydrateSaveInventorySilently(save.inventory, save.session, catalog, out _), "Stale inventory hydration rejected");
            Check(!journal.TryHydrateSaveJournalSilently(save.session, catalog, out _), "Stale journal hydration rejected");
            SessionStoryState.TryRestoreDurableSilently(save.session, registry, out _);
            Check(flagEvents == 0 && taskEvents == 0 && itemEvents == 0 && journalEvents == 0, "No progression/acquisition/discovery events");

            var ending = EndingFixture(catalog); Check(AlaalaSaveValidation.Validate(ending, catalog, owner, out error), "Completed epilogue fixture: " + error);
            Check(SessionStoryState.TryRestoreDurableSilently(ending.session, registry, out _), "Final state import");
            Check(journal.TryHydrateSaveJournalSilently(ending.session, catalog, out _), "Final journal import");
            Check(people.IsPersonUnlocked("padre_agaton") && !string.IsNullOrWhiteSpace(reflections.GetSubmittedResponse("part3_final_pagninilay")), "Padre and final Pagninilay retained");
            var executionGuard = typeof(SessionStoryState).GetMethod("HasUnsafeSaveExecutionState", BindingFlags.NonPublic | BindingFlags.Static);
            ints["alaala_part3_ch12_cinematic_step"] = 5;
            Check(!(bool)executionGuard.Invoke(null, new object[] { null }), "Completed cinematic residue permits stable capture");
            flags.Remove("alaala_part3_ch12_complete");
            Check((bool)executionGuard.Invoke(null, new object[] { null }), "Unfinished cinematic residue blocks capture");
            SessionStoryState.TryRestoreDurableSilently(ending.session, registry, out _);

            Check(!AlaalaSaveRunContext.TryAuthorizeGenuineRun(out _), "Editor cannot authorize genuine saves");
            Check(!AlaalaSaveFoundation.TryCapture(AlaalaCheckpointRegistry.SulatinSearch, 1, catalog, tasks, inventory, out _, out _), "Unverified Editor capture blocked");
            Check(!AlaalaSaveFoundation.TryPrepareRestore(save, catalog, out _, out _), "Unverified Editor restore blocked");
        }
        finally
        {
            SessionStoryState.FlagChanged -= flagListener;
            foreach (var go in temporaries) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            flags.Clear(); foreach (var key in oldFlags) flags.Add(key);
            ints.Clear(); foreach (var pair in oldInts) ints.Add(pair.Key, pair.Value);
            strings.Clear(); foreach (var pair in oldStrings) strings.Add(pair.Key, pair.Value);
            knownItems.Clear(); foreach (var id in oldKnownItems) knownItems.Add(id);
        }
        return checks + " Edit Mode save-foundation checks passed. Fixtures stayed in memory; no authentication or storage requests.";
    }

    [Serializable] private sealed class TaskList { public List<AlaalaTaskSnapshot> values; }
    private static void SetFlag(AlaalaSessionSnapshot s, string key, bool value = true)
    { s.booleans.RemoveAll(x => x.key == key); if (value) s.booleans.Add(new SaveBoolean { key = key, value = true }); }
    private static void SetInt(AlaalaSessionSnapshot s, string key, int value)
    { s.integers.RemoveAll(x => x.key == key); if (value != 0) s.integers.Add(new SaveInteger { key = key, value = value }); }
    private static void SetText(AlaalaSessionSnapshot s, string key, string value)
    { s.strings.RemoveAll(x => x.key == key); if (value.Length > 0) s.strings.Add(new SaveString { key = key, value = value }); }
    private static AlaalaSessionSnapshot Canonical(AlaalaSessionSnapshot s) => new AlaalaSessionSnapshot
    { booleans = s.booleans.OrderBy(x => x.key, StringComparer.Ordinal).ToList(), integers = s.integers.OrderBy(x => x.key, StringComparer.Ordinal).ToList(), strings = s.strings.OrderBy(x => x.key, StringComparer.Ordinal).ToList() };

    private static AlaalaSaveSnapshot Fixture(AlaalaSaveCatalog catalog)
    {
        var save = new AlaalaSaveSnapshot
        {
            titleId = "VALIDATION_TITLE", playFabAccountId = "VALIDATION_ACCOUNT", gameVersion = Application.version, saveRevision = 1,
            runKind = SaveRunKind.Genuine, runId = "00000000000000000000000000000001", checkpointId = AlaalaCheckpointRegistry.SulatinSearch,
            targetScene = "Assets/Scenes/DraftWorld.unity", spawnRoute = "MaestroSpawn", activeMainTaskId = "main_collect_remaining_sulatin",
            world = new AlaalaWorldSnapshot { isMorning = true, movementDone = true, playerMovementDone = true }
        };
        foreach (var key in new[] { "alaala_part2_complete", "alaala_post_part2_draftworld_arrival_handled", "posada_first_sleep_completed", "journal_unlocked", "inventory_unlocked", "clarity_unlocked",
            "inventory_tutorial_reveal_ready", "inventory_tutorial_reveal_started", "maestro_ben_part2_cecilia_ysagani_handoff_complete", "maestro_ben_part2_cecilia_ysagani_presentation_complete" }) SetFlag(save.session, key);
        SetInt(save.session, "maestro_ben_part2_cecilia_ysagani_presentation_phase", 6);
        foreach (var definition in catalog.Tasks.OrderBy(x => x.TaskId, StringComparer.Ordinal))
        {
            bool active = definition.TaskId == "main_collect_remaining_sulatin" || definition.TaskId == "aling_ika_errand";
            string stage = definition.Stages.Length == 0 ? "" : definition.TaskId == "main_collect_remaining_sulatin" ? definition.Stages[0].StageId : definition.Stages.Last().StageId;
            var task = new AlaalaTaskSnapshot { taskId = definition.TaskId, stageId = stage, state = active ? TaskState.Active : TaskState.Completed,
                objective = active ? definition.TaskId == "aling_ika_errand" ? "Ibalik ang bayad. [dynamic fixture]" : "Hanapin ang Sulatin. (2/5)" : "" };
            save.tasks.Add(task); SetFlag(save.session, (active ? "task_active:" : "task_completed:") + task.taskId); SetText(save.session, "task_stage:" + task.taskId, stage);
            if (definition.TaskId == "main_investigate_pili")
                foreach (var id in definition.Stages[0].AllowedProgressIds.Take(3)) Progress(save.session, definition.TaskId, definition.Stages[0].StageId, id);
        }
        save.inventory.owned.AddRange(new[] { "church_lumang_sulatin", "present_pili_archival_rizal_novel" });
        save.inventory.collected.AddRange(save.inventory.owned); save.inventory.collected.Add("kutsero_rope"); save.inventory.collected.Sort(StringComparer.Ordinal);
        save.inventory.consumed.Add("kutsero_rope");
        foreach (var id in save.inventory.owned) { SetFlag(save.session, "inventory.notification.known:" + id); Progress(save.session, "main_collect_remaining_sulatin", "collect_remaining_sulatin", id); }
        SetFlag(save.session, "inventory.notification.read:" + save.inventory.owned[0]);
        SetFlag(save.session, "journal_person_unlocked:cecilia"); SetFlag(save.session, "journal_reflection_unlocked:anday_story_reflection");
        SetText(save.session, "journal_reflection_response:anday_story_reflection", "May pagbabago\nsa aking pag-unawa sa kuwento ni Anday.");
        SetText(save.session, "journal_reflection_follow_up:anday_story_reflection", "Napiling karagdagang tala.");
        save.session = Canonical(save.session); return save;
    }

    private static void Progress(AlaalaSessionSnapshot s, string task, string stage, string id)
    { string key = "task_progress:" + task + ":" + stage + ":" + id; SetFlag(s, key); SetInt(s, key, 1); }

    private static AlaalaSaveSnapshot EndingFixture(AlaalaSaveCatalog catalog)
    {
        var save = Fixture(catalog); save.checkpointId = AlaalaCheckpointRegistry.CompletedEpilogue; save.activeMainTaskId = "";
        var task = save.tasks.First(x => x.taskId == "main_collect_remaining_sulatin"); task.state = TaskState.Completed; task.stageId = "part3_run_to_church"; task.objective = "";
        SetFlag(save.session, "task_active:" + task.taskId, false); SetFlag(save.session, "task_completed:" + task.taskId); SetText(save.session, "task_stage:" + task.taskId, task.stageId);
        foreach (var id in catalog.Tasks.First(x => x.TaskId == task.taskId).Stages[0].AllowedProgressIds)
        {
            if (!save.inventory.owned.Contains(id)) save.inventory.owned.Add(id);
            if (!save.inventory.collected.Contains(id)) save.inventory.collected.Add(id);
            SetFlag(save.session, "inventory.notification.known:" + id); Progress(save.session, task.taskId, "collect_remaining_sulatin", id);
        }
        foreach (var key in new[] { "alaala_part3_arrival_complete", "alaala_part3_ch11_complete", "alaala_part3_ch11_white_presented", "alaala_part3_ch12_dialogue_complete",
            "alaala_part3_ch12_ellipsis_complete", "alaala_part3_ch12_complete", "alaala_part3_ch13_complete", "alaala_epilogue_dialogue_complete", "alaala_epilogue_complete",
            "journal_person_unlocked:padre_agaton", "journal_reflection_unlocked:part3_final_pagninilay" }) SetFlag(save.session, key);
        SetText(save.session, "journal_reflection_response:part3_final_pagninilay", "Pangangalagaan ko ang mga kuwentong naiwan.");
        SetText(save.session, "journal_reflection_follow_up:part3_final_pagninilay", catalog.Reflections.First(x => x.reflectionID == "part3_final_pagninilay").finalEducationalNote);
        save.inventory.owned.Sort(StringComparer.Ordinal); save.inventory.collected.Sort(StringComparer.Ordinal);
        save.session = Canonical(save.session); return save;
    }
}
#endif
