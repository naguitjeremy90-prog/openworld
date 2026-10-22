#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Alaala.Saving;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit asynchronous Edit Mode checks, exclusively under a random OS-temp root.</summary>
public static class AlaalaLocalSaveTests
{
    private static Task<string> running;
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "STOP: Edit Mode only.";
        if (running != null && !running.IsCompleted) return "Already running.";
        running = RunAsync(); return "Started isolated local-storage checks.";
    }
    public static string Status() => running == null ? "Not started." : !running.IsCompleted ? "Running." :
        running.IsFaulted ? "FAILED: " + running.Exception.GetBaseException() : running.Result;

    private static async Task<string> RunAsync()
    {
        int count = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); count++; }
        var catalog = AssetDatabase.LoadAssetAtPath<AlaalaSaveCatalog>("Assets/Resources/SaveFoundation/AlaalaSaveCatalog.asset");
        var fixture = (AlaalaSaveSnapshot)typeof(AlaalaSaveFoundationTests).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { catalog });
        string root = Path.Combine(Path.GetTempPath(), "alaala-storage-tests-" + Guid.NewGuid().ToString("N"));
        bool authorized = true;
        AlaalaSaveSnapshot Clone() => JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(fixture));
        AlaalaLocalSaveStore Store(string account) => AlaalaLocalSaveStore.CreateIsolatedEditorStore(root, catalog, fixture.titleId, account, () => authorized);
        var store = Store(fixture.playFabAccountId);
        string primary = Path.Combine(store.EditorTestDirectory, "checkpoint.json"), backup = Path.Combine(store.EditorTestDirectory, "checkpoint.backup.json");
        string liveBefore = SessionFingerprint();
        int changedEvents = 0;
        Action<string, SaveValueKind> listener = (a, b) => changedEvents++;
        SessionStoryState.ProgressValueChanged += listener;
        try
        {
            Check((await store.InspectSaveAvailabilityAsync()).Status == LocalSaveStatus.NoSave, "Empty account has no save");
            Check(!Directory.Exists(root), "Discovery did not manufacture player data");
            fixture.session.strings.First(x => x.key == "journal_reflection_response:anday_story_reflection").value =
                "Ang pagninilay: ñ, á, é; Pili—alaala.\n\"Bakit?\" \\ Pagbabago at pag-asa. 😀";
            var first = await store.SaveIsolatedFixtureAsync(fixture);
            Check(first.Status == LocalSaveStatus.Committed && first.Revision == 1, "First atomic commit revision 1: " + first.Status + " " + first.Diagnostic);
            Check(File.Exists(primary) && File.Exists(backup), "Primary and first known-good backup exist");
            var read = await store.ReadValidatedSnapshotAsync();
            Check(read.Status == LocalSaveStatus.ValidSave && read.PrimaryStatus == LocalSaveStatus.ValidSave && read.BackupStatus == LocalSaveStatus.ValidSave, "Both committed files validate");
            Check(read.Snapshot.session.strings.First(x => x.key == "journal_reflection_response:anday_story_reflection").value ==
                fixture.session.strings.First(x => x.key == "journal_reflection_response:anday_story_reflection").value, "Unicode/free text survives UTF-8 JSON");
            Check(read.Snapshot.tasks.First(x => x.taskId == "aling_ika_errand").objective.Contains("dynamic fixture"), "Dynamic objective survives disk roundtrip");
            Check(read.Snapshot.inventory.consumed.Contains("kutsero_rope") && read.Snapshot.inventory.owned.Count == 2, "Inventory history roundtrip");
            Check(JsonUtility.ToJson(read.Snapshot.world) == JsonUtility.ToJson(fixture.world), "World flags roundtrip");
            Check(read.Snapshot.session.booleans.Count == fixture.session.booleans.Count && read.Snapshot.session.integers.Count == fixture.session.integers.Count &&
                read.Snapshot.session.strings.Count == fixture.session.strings.Count, "All typed progression sections roundtrip");
            Check((await store.SaveIsolatedFixtureAsync(fixture)).Status == LocalSaveStatus.Unchanged, "Redundant request coalesced without revision advance");

            var next = Clone(); next.tasks.First(x => x.taskId == "aling_ika_errand").objective += " Bago.";
            var second = await store.SaveIsolatedFixtureAsync(next);
            Check(second.Status == LocalSaveStatus.Committed && second.Revision == 2, "Second commit increments revision");
            Check(JsonUtility.FromJson<AlaalaSaveSnapshot>(File.ReadAllText(backup)).saveRevision == 1, "Backup retains previous successful commit");

            var other = Store("OTHER_ACCOUNT");
            Check(other.EditorTestDirectory != store.EditorTestDirectory && (await other.InspectSaveAvailabilityAsync()).Status == LocalSaveStatus.NoSave, "Account paths isolated");
            Check((await other.SaveIsolatedFixtureAsync(fixture)).Status == LocalSaveStatus.InvalidSnapshot, "Wrong account cannot be saved");
            var unsafeName = Store("../../account\\escape");
            Check(Path.GetDirectoryName(unsafeName.EditorTestDirectory) == Path.GetDirectoryName(store.EditorTestDirectory) &&
                Path.GetFileName(unsafeName.EditorTestDirectory).Length == 32, "Account path traversal neutralized by hashing");
            var wrongTitle = AlaalaLocalSaveStore.CreateIsolatedEditorStore(root, catalog, "OTHER_TITLE", fixture.playFabAccountId, () => true);
            Check(wrongTitle.EditorTestDirectory != store.EditorTestDirectory, "Title paths isolated");
            File.Copy(primary, Path.Combine(other.EditorTestDirectory, "checkpoint.json"));
            Check((await other.InspectSaveAvailabilityAsync()).Status == LocalSaveStatus.AccountMismatch, "Copied foreign save rejected on discovery");

            var invalid = Clone(); invalid.checkpointId = "unsafe.chase";
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Unsupported checkpoint rejected");
            invalid = Clone(); invalid.inventory.owned[0] = "missing_item";
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Unknown item rejected");
            invalid = Clone(); invalid.inventory.owned.Add(invalid.inventory.owned[0]);
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Duplicate ownership rejected");
            invalid = Clone(); invalid.tasks.First(x => x.taskId == "main_collect_remaining_sulatin").stageId = "unknown_stage";
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Unknown task stage rejected");
            invalid = Clone(); invalid.runKind = SaveRunKind.Synthetic;
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Synthetic provenance rejected by validator");
            invalid = Clone(); invalid.schemaVersion++;
            Check((await store.SaveIsolatedFixtureAsync(invalid)).Status == LocalSaveStatus.InvalidSnapshot, "Unsupported schema rejected before writing");
            Check((await store.ReadValidatedSnapshotAsync()).Revision == 2, "Rejected data leaves existing save unchanged");
            var lostAnswer = Clone();
            lostAnswer.session.strings.RemoveAll(x => x.key.StartsWith("journal_reflection_response:") || x.key.StartsWith("journal_reflection_follow_up:"));
            Check((await store.SaveIsolatedFixtureAsync(lostAnswer)).Status == LocalSaveStatus.RevisionConflict, "Accepted reflection cannot disappear in a later same-run save");
            var stableGuard = typeof(AlaalaSaveFoundation).GetMethod("IsStableEnvironment", BindingFlags.NonPublic | BindingFlags.Static);
            var sequenceField = typeof(StorySequenceCoordinator).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            var originalSequence = sequenceField.GetValue(null);
            var sequenceObject = new GameObject("IsolatedSaveCinematicGuard") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var sequence = sequenceObject.AddComponent<StorySequenceCoordinator>();
                sequenceField.SetValue(null, sequence);
                typeof(StorySequenceCoordinator).GetMethod("AcquireInternal", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(sequence, new object[] { 123456789, "Isolated cinematic guard check" });
                Check(!(bool)stableGuard.Invoke(null, new object[] { null }), "Active cinematic ownership rejects checkpoint eligibility");
            }
            finally { UnityEngine.Object.DestroyImmediate(sequenceObject); sequenceField.SetValue(null, originalSequence); }

            string before = File.ReadAllText(primary), beforeBackup = File.ReadAllText(backup);
            store.EditorTransactionProbe = stage => throw new IOException("Simulated interruption before atomic replacement");
            Check((await store.SaveIsolatedFixtureAsync(fixture)).Status == LocalSaveStatus.IoError, "Interrupted write reported");
            Check(File.ReadAllText(primary) == before && File.ReadAllText(backup) == beforeBackup, "Interrupted write preserves both committed copies");
            Check(!Directory.GetFiles(store.EditorTestDirectory, "*.pending*").Any(), "Failed transaction temp cleaned up");
            store.EditorTransactionProbe = stage => authorized = false;
            Check((await store.SaveIsolatedFixtureAsync(fixture)).Status == LocalSaveStatus.OwnerChanged, "Owner change before commit cancels write");
            Check(File.ReadAllText(primary) == before && File.ReadAllText(backup) == beforeBackup, "Owner change cannot replace either file");
            Check((await store.ReadValidatedSnapshotAsync()).Status == LocalSaveStatus.OwnerChanged, "Stale store cannot read a previous account");
            authorized = true; store.EditorTransactionProbe = null;

            File.WriteAllText(primary, "{corrupt interrupted data", new UTF8Encoding(false));
            var corrupt = await store.InspectSaveAvailabilityAsync();
            Check(corrupt.Status == LocalSaveStatus.ValidBackup && corrupt.PrimaryStatus == LocalSaveStatus.CorruptPrimary && corrupt.Revision == 1, "Corrupt primary discovers valid backup");
            Check(File.ReadAllText(primary).StartsWith("{corrupt"), "Discovery does not silently repair primary");
            Check((await store.SaveIsolatedFixtureAsync(fixture)).Status == LocalSaveStatus.RecoveryRequired, "Autosave cannot overwrite corruption/known-good backup");
            Check((await store.RecoverBackupAsync()).Status == LocalSaveStatus.Recovered, "Explicit backup recovery succeeds");
            Check(Directory.GetFiles(store.EditorTestDirectory, "quarantine-*.json").Length == 1, "Corrupt primary preserved in quarantine");
            Check((await store.ReadValidatedSnapshotAsync()).Status == LocalSaveStatus.ValidSave, "Recovered primary is fully validated");

            var unsupported = Clone(); unsupported.schemaVersion = 123;
            File.WriteAllText(primary, JsonUtility.ToJson(unsupported));
            var availability = await store.InspectSaveAvailabilityAsync();
            Check(availability.Status == LocalSaveStatus.ValidBackup && availability.PrimaryStatus == LocalSaveStatus.UnsupportedSchema, "Unsupported primary distinguished from corruption");
            Check((await store.RecoverBackupAsync()).Status == LocalSaveStatus.RecoveryRequired, "Recovery will not overwrite a future-version primary");
            File.WriteAllText(primary, "bad"); File.WriteAllText(backup, "bad");
            Check(!(await store.ReadValidatedSnapshotAsync()).HasValidatedSave, "Two corrupt copies never become restoration sources");

            var concurrent = Store("CONCURRENT_ACCOUNT");
            var same = Clone(); same.playFabAccountId = "CONCURRENT_ACCOUNT";
            var pair = await Task.WhenAll(concurrent.SaveIsolatedFixtureAsync(same), concurrent.SaveIsolatedFixtureAsync(same));
            Check(pair.Count(x => x.Status == LocalSaveStatus.Committed) == 1 && pair.Count(x => x.Status == LocalSaveStatus.Unchanged) == 1, "Duplicate concurrent requests serialize/coalesce");
            var a = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(same)); a.tasks.First(x => x.state == TaskState.Active).objective += " A";
            var b = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(same)); b.tasks.First(x => x.state == TaskState.Active).objective += " B";
            var ordered = await Task.WhenAll(concurrent.SaveIsolatedFixtureAsync(a), concurrent.SaveIsolatedFixtureAsync(b));
            Check(ordered[0].Revision == 2 && ordered[1].Revision == 3 && (await concurrent.ReadValidatedSnapshotAsync()).Revision == 3, "Queued revisions cannot commit out of order");
            var lower = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(same));
            lower.session.booleans.RemoveAll(x => x.key == "task_progress:main_collect_remaining_sulatin:collect_remaining_sulatin:present_pili_archival_rizal_novel");
            lower.session.integers.RemoveAll(x => x.key == "task_progress:main_collect_remaining_sulatin:collect_remaining_sulatin:present_pili_archival_rizal_novel");
            lower.inventory.owned.Remove("present_pili_archival_rizal_novel"); lower.inventory.collected.Remove("present_pili_archival_rizal_novel");
            lower.session.booleans.RemoveAll(x => x.key.EndsWith(":present_pili_archival_rizal_novel"));
            Check((await concurrent.SaveIsolatedFixtureAsync(lower)).Status == LocalSaveStatus.RevisionConflict, "Same-run task/inventory regression rejected");
            var staged = Clone(); staged.playFabAccountId = "STAGE_ACCOUNT";
            var endingItems = (AlaalaSaveSnapshot)typeof(AlaalaSaveFoundationTests).GetMethod("EndingFixture", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { catalog });
            staged.inventory = endingItems.inventory;
            foreach (var entry in endingItems.session.booleans.Where(x => x.key.StartsWith("inventory.notification.") || x.key.StartsWith("task_progress:main_collect_remaining_sulatin:")))
                if (!staged.session.booleans.Any(x => x.key == entry.key)) staged.session.booleans.Add(entry);
            foreach (var entry in endingItems.session.integers.Where(x => x.key.StartsWith("task_progress:main_collect_remaining_sulatin:")))
                if (!staged.session.integers.Any(x => x.key == entry.key)) staged.session.integers.Add(entry);
            staged.tasks.First(x => x.taskId == "main_collect_remaining_sulatin").stageId = "return_to_maestro_ben";
            staged.session.strings.First(x => x.key == "task_stage:main_collect_remaining_sulatin").value = "return_to_maestro_ben";
            var stageStore = Store("STAGE_ACCOUNT");
            Check((await stageStore.SaveIsolatedFixtureAsync(staged)).Status == LocalSaveStatus.Committed, "Known advanced task stage persists");
            staged.tasks.First(x => x.taskId == "main_collect_remaining_sulatin").stageId = "collect_remaining_sulatin";
            staged.session.strings.First(x => x.key == "task_stage:main_collect_remaining_sulatin").value = "collect_remaining_sulatin";
            Check((await stageStore.SaveIsolatedFixtureAsync(staged)).Status == LocalSaveStatus.RevisionConflict, "Known-stage regression cannot overwrite advanced task");
            var ending = (AlaalaSaveSnapshot)typeof(AlaalaSaveFoundationTests).GetMethod("EndingFixture", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { catalog });
            ending.playFabAccountId = "ENDING_ACCOUNT";
            var endingStore = Store("ENDING_ACCOUNT");
            Check((await endingStore.SaveIsolatedFixtureAsync(ending)).Status == LocalSaveStatus.Committed, "Completed epilogue persists without loading gameplay");
            var final = (await endingStore.ReadValidatedSnapshotAsync()).Snapshot;
            Check(final.session.booleans.Any(x => x.key == "journal_person_unlocked:padre_agaton" && x.value) &&
                final.session.strings.Any(x => x.key == "journal_reflection_response:part3_final_pagninilay" && x.value.Contains("kuwentong")), "Padre and final reflection survive local storage");
            string endingPrimary = Path.Combine(endingStore.EditorTestDirectory, "checkpoint.json");
            File.Delete(endingPrimary); // Simulates interruption between first backup publication and primary publication, in the isolated test root.
            Check((await endingStore.InspectSaveAvailabilityAsync()).Status == LocalSaveStatus.ValidBackup, "Interrupted first publication has recoverable backup");
            Check((await endingStore.RecoverBackupAsync()).Status == LocalSaveStatus.Recovered, "Missing-primary recovery publishes the validated backup");
            using (var locked = new FileStream(Path.Combine(concurrent.EditorTestDirectory, "transaction.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check((await concurrent.InspectSaveAvailabilityAsync()).Status == LocalSaveStatus.IoError, "Cross-process transaction lock enforced");

            Check(!AlaalaLocalSaveStore.TryOpenAuthenticated(catalog, out _, out _), "Editor cannot open genuine player storage");
            Check((await store.SaveCheckpointAsync(AlaalaCheckpointRegistry.SulatinSearch)).Status == LocalSaveStatus.UnsafeCheckpoint ||
                (await store.SaveCheckpointAsync(AlaalaCheckpointRegistry.SulatinSearch)).Status == LocalSaveStatus.RecoveryRequired, "Real capture remains unauthorized in Editor");
            Check(SessionFingerprint() == liveBefore && changedEvents == 0, "Storage never hydrates or emits gameplay progression events");
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Editor stayed outside Play Mode");
            return count + " isolated local-storage checks passed. " + AlaalaSaveFoundationTests.Run();
        }
        finally
        {
            SessionStoryState.ProgressValueChanged -= listener;
            string full = Path.GetFullPath(root);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("alaala-storage-tests-", StringComparison.Ordinal) && Directory.Exists(full))
                Directory.Delete(full, true);
        }
    }

    private static string SessionFingerprint()
    {
        var fields = BindingFlags.NonPublic | BindingFlags.Static;
        var flags = (System.Collections.Generic.HashSet<string>)typeof(SessionStoryState).GetField("completedFlags", fields).GetValue(null);
        var ints = (System.Collections.Generic.Dictionary<string, int>)typeof(SessionStoryState).GetField("integerValues", fields).GetValue(null);
        var strings = (System.Collections.Generic.Dictionary<string, string>)typeof(SessionStoryState).GetField("stringValues", fields).GetValue(null);
        return string.Join("\n", flags.OrderBy(x => x)) + "|" + string.Join("\n", ints.OrderBy(x => x.Key)) + "|" + string.Join("\n", strings.OrderBy(x => x.Key));
    }
}
#endif
