#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alaala.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Policy, ownership, recovery and silent hydration checks. Never invokes a live loader.</summary>
public static class AlaalaResumeTests
{
    private static Task<string> running;
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "STOP: Edit Mode only.";
        if (running != null && !running.IsCompleted) return "Already running.";
        running = All(); return "Started isolated Phase 2B checks.";
    }
    public static string Status() => running == null ? "Not started." : !running.IsCompleted ? "Running." :
        running.IsFaulted ? "FAILED: " + running.Exception.GetBaseException() : running.Result;
    private static async Task<string> All()
    {
        string own = await CheckResume();
        var old = (Task<string>)typeof(AlaalaLocalSaveTests).GetMethod("RunAsync", PrivateStatic).Invoke(null, null);
        return own + " " + await old;
    }

    private static async Task<string> CheckResume()
    {
        int count = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); count++; }
        var catalog = AssetDatabase.LoadAssetAtPath<AlaalaSaveCatalog>("Assets/Resources/SaveFoundation/AlaalaSaveCatalog.asset");
        var save = Ready((AlaalaSaveSnapshot)typeof(AlaalaSaveFoundationTests).GetMethod("Fixture", PrivateStatic).Invoke(null, new object[] { catalog }));
        var owner = (AlaalaSaveOwner)typeof(AlaalaSaveOwner).GetConstructor(PrivateInstance, null, new[] { typeof(string), typeof(string) }, null)
            .Invoke(new object[] { save.titleId, save.playFabAccountId });
        var wrong = (AlaalaSaveOwner)typeof(AlaalaSaveOwner).GetConstructor(PrivateInstance, null, new[] { typeof(string), typeof(string) }, null)
            .Invoke(new object[] { save.titleId, "OTHER_OWNER" });
        Check(!AlaalaResumePolicy.CanOfferContinue(null, catalog, owner, out _), "Magpatuloy disabled without a validated save");
        Check(AlaalaResumePolicy.ValidateSnapshot(save, catalog, owner, out var error), "Supported Sulatin checkpoint: " + error);
        Check(!AlaalaResumePolicy.ValidateSnapshot(save, catalog, wrong, out _), "Foreign owner rejected");
        var invalid = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save)); invalid.checkpointId = "white_void.chase";
        Check(!AlaalaResumePolicy.ValidateSnapshot(invalid, catalog, owner, out _), "Unsupported checkpoint cannot continue");
        invalid = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save)); invalid.targetScene = "Assets/Scenes/Missing.unity";
        Check(!AlaalaResumePolicy.ValidateSnapshot(invalid, catalog, owner, out _), "Scene substitution/missing scene rejected");
        invalid = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save)); invalid.session.integers.RemoveAll(x => x.key == "draftworld_night_navigation_phase");
        Check(!AlaalaResumePolicy.ValidateSnapshot(invalid, catalog, owner, out _), "Incomplete startup history not silently manufactured");
        invalid = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(save)); invalid.session.booleans.RemoveAll(x => x.key == "inventory_tutorial_intro_seen");
        Check(!AlaalaResumePolicy.ValidateSnapshot(invalid, catalog, owner, out _), "Unfinished teaching boundary rejected");

        bool accountCurrent = true;
        var lease = new AlaalaResumeAttempt(() => accountCurrent);
        Check(lease.TryBegin(out var ticket) && lease.IsCurrent(ticket), "Authenticated attempt admitted");
        Check(!lease.TryBegin(out _), "Duplicate Continue rejected");
        accountCurrent = false; Check(!lease.IsCurrent(ticket), "Account change invalidates pending callback");
        lease.End(); accountCurrent = true;
        Check(lease.TryBegin(out var second) && second != ticket && !lease.IsCurrent(ticket), "Superseded callback cannot publish"); lease.End();
        Check(!AlaalaResumeCoordinator.TryBegin(null), "Editor cannot start genuine restoration");
        Check(AlaalaResumePolicy.IsSafeMenuScene(AlaalaResumePolicy.MenuScene, true) && AlaalaResumePolicy.IsSafeMenuScene(AlaalaResumePolicy.LoginScene, true), "Recovery recognizes only loaded safe menus");
        Check(!AlaalaResumePolicy.IsSafeMenuScene(save.targetScene, true) && !AlaalaResumePolicy.IsSafeMenuScene(AlaalaResumePolicy.MenuScene, false), "Recovery never releases partial gameplay or an uninitialized menu");

        string root = Path.Combine(Path.GetTempPath(), "alaala-storage-tests-" + Guid.NewGuid().ToString("N"));
        var store = AlaalaLocalSaveStore.CreateIsolatedEditorStore(root, catalog, save.titleId, save.playFabAccountId, () => accountCurrent);
        var preview = EditorSceneManager.NewPreviewScene();
        var flags = (HashSet<string>)typeof(SessionStoryState).GetField("completedFlags", PrivateStatic).GetValue(null); var oldFlags = flags.ToArray();
        var ints = (Dictionary<string, int>)typeof(SessionStoryState).GetField("integerValues", PrivateStatic).GetValue(null); var oldInts = ints.ToArray();
        var strings = (Dictionary<string, string>)typeof(SessionStoryState).GetField("stringValues", PrivateStatic).GetValue(null); var oldStrings = strings.ToArray();
        var oldWorld = (AlaalaWorldSnapshot)typeof(AlaalaSaveFoundation).GetMethod("CaptureWorld", PrivateStatic).Invoke(null, null);
        string oldSpawn = SpawnData.spawnPointName;
        bool oldRestore = AlaalaSaveFoundation.RestorationInProgress;
        int events = 0; Action<string, SaveValueKind> listen = (a, b) => events++;
        SessionStoryState.ProgressValueChanged += listen;
        try
        {
            Check((await store.SaveIsolatedFixtureAsync(save)).Status == LocalSaveStatus.Committed, "Fixture save committed only to OS temp");
            var available = await store.InspectSaveAvailabilityAsync();
            Check(AlaalaResumePolicy.CanOfferContinue(available, catalog, owner, out _), "Validated owner can discover Continue availability");
            string primary = Path.Combine(store.EditorTestDirectory, "checkpoint.json"), backup = Path.Combine(store.EditorTestDirectory, "checkpoint.backup.json");
            string original = File.ReadAllText(primary);
            File.WriteAllText(backup, "broken backup");
            Check(AlaalaResumePolicy.CanOfferContinue(await store.InspectSaveAvailabilityAsync(), catalog, owner, out _), "Valid primary with corrupt backup remains recoverable");
            Check((await store.RecoverBackupAsync()).Status == LocalSaveStatus.Recovered && File.ReadAllText(primary) == original, "Controlled backup repair preserves primary");
            Check(Directory.GetFiles(store.EditorTestDirectory, "quarantine-backup-*.json").Length == 1, "Corrupt backup retained for diagnosis");
            File.WriteAllText(primary, "broken primary");
            Check(AlaalaResumePolicy.CanOfferContinue(await store.InspectSaveAvailabilityAsync(), catalog, owner, out _), "Validated recovery backup enables Continue");
            Check((await store.RecoverBackupAsync()).Status == LocalSaveStatus.Recovered, "Explicit Continue recovery uses validated backup");
            var futureBackup = JsonUtility.FromJson<AlaalaSaveSnapshot>(original); futureBackup.saveRevision++;
            File.WriteAllText(backup, JsonUtility.ToJson(futureBackup));
            Check(!AlaalaResumePolicy.CanOfferContinue(await store.InspectSaveAvailabilityAsync(), catalog, owner, out _), "Newer-backup conflict is not guessed");
            File.WriteAllText(backup, original);
            var newRun = JsonUtility.FromJson<AlaalaSaveSnapshot>(original); newRun.runId = "00000000000000000000000000000002";
            Check((await store.SaveIsolatedFixtureAsync(newRun)).Status == LocalSaveStatus.RecoveryRequired && File.ReadAllText(primary) == original, "New Game cannot overwrite the previous saved run");
            accountCurrent = false;
            Check((await store.ReadValidatedSnapshotAsync()).Status == LocalSaveStatus.OwnerChanged, "Account-switch discovery cannot read prior owner"); accountCurrent = true;

            var ending = Ready((AlaalaSaveSnapshot)typeof(AlaalaSaveFoundationTests).GetMethod("EndingFixture", PrivateStatic).Invoke(null, new object[] { catalog }));
            Check(AlaalaResumePolicy.ValidateSnapshot(ending, catalog, owner, out error), "Completed-epilogue checkpoint supported: " + error);
            var registry = new AlaalaDurableStateRegistry(catalog);
            Check(SessionStoryState.TryRestoreDurableSilently(ending.session, registry, out error), "Install all typed state before destination caches");
            typeof(AlaalaSaveFoundation).GetProperty("RestorationInProgress").SetValue(null, true);
            var journalRoot = new GameObject("IsolatedResumeJournal") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(journalRoot, preview);
            var people = journalRoot.AddComponent<ReconstructionPeople>(); var observations = journalRoot.AddComponent<ReconstructionObservations>();
            var fragments = journalRoot.AddComponent<ReconstructionFragments>(); var reflections = journalRoot.AddComponent<ReconstructionReflections>();
            var journal = journalRoot.AddComponent<ReconstructionJournalManager>();
            void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
            Set(people, "allPeople", catalog.People.ToList()); Set(observations, "allObservations", catalog.Observations.ToList());
            Set(fragments, "allFragments", catalog.Fragments.ToList()); Set(reflections, "allReflections", catalog.Reflections.Where(x => x.reflectionID != "part3_final_pagninilay").ToList());
            Set(journal, "people", people); Set(journal, "observations", observations); Set(journal, "fragments", fragments); Set(journal, "reflections", reflections);
            Check(!journal.CanHydrateSaveJournal(catalog, ending.session, out _), "Missing final reflection asset is detected");
            var args = new object[] { catalog, ending.session, null };
            Check((bool)typeof(ReconstructionJournalManager).GetMethod("PrepareSaveAssets", PrivateInstance).Invoke(journal, args), "Approved final reflection supplemented locally");
            int discoveries = 0; people.EntryUnlocked += x => discoveries++; reflections.EntryUnlocked += x => discoveries++;
            Check(journal.TryHydrateSaveJournalSilently(ending.session, catalog, out error), "Destination journal silently hydrates");
            Check(people.IsPersonUnlocked("padre_agaton") && reflections.GetSubmittedResponse("part3_final_pagninilay") ==
                ending.session.strings.First(x => x.key == "journal_reflection_response:part3_final_pagninilay").value, "Final educational answer and Padre remain exact");
            Check(discoveries == 0 && events == 0, "Hydration does not emit reward/discovery/progression events");
            var arrival = new GameObject("ArrivalStart", typeof(SelfDialogueTrigger)) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(arrival, preview);
            var arrivalTrigger = arrival.GetComponent<SelfDialogueTrigger>();
            var restoreOpening = typeof(SelfDialogueTrigger).GetMethod("RestoreCompletedOpeningTrigger", PrivateInstance);
            restoreOpening.Invoke(arrivalTrigger, null);
            Check((bool)typeof(SelfDialogueTrigger).GetField("hasTriggered", PrivateInstance).GetValue(arrivalTrigger), "Completed opening does not replay its dialogue");
            var unrelated = new GameObject("UnrelatedOpening", typeof(SelfDialogueTrigger)) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(unrelated, preview);
            restoreOpening.Invoke(unrelated.GetComponent<SelfDialogueTrigger>(), null);
            Check(!(bool)typeof(SelfDialogueTrigger).GetField("hasTriggered", PrivateInstance).GetValue(unrelated.GetComponent<SelfDialogueTrigger>()), "Unrelated story triggers remain unchanged");

            var gateType = typeof(AlaalaSaveFoundation).Assembly.GetType("Alaala.Saving.AlaalaResumeWorldGate");
            var player = new GameObject("IsolatedPlayer", typeof(Rigidbody), typeof(BoxCollider), typeof(TutorialState)) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player, preview);
            var route = new GameObject("MaestroSpawn", typeof(PlayerSpawner)) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(route, preview);
            Set(route.GetComponent<PlayerSpawner>(), "player", player.transform);
            var movement = player.AddComponent<DialogueMovementLock>(); Set(movement, "playerMovementScript", player.GetComponent<TutorialState>());
            var gate = gateType.GetConstructor(PrivateInstance, null, new[] { typeof(UnityEngine.SceneManagement.Scene), typeof(string) }, null).Invoke(new object[] { preview, "MaestroSpawn" });
            var hold = gateType.GetMethod("Hold", PrivateInstance); var release = gateType.GetMethod("Release", PrivateInstance);
            hold.Invoke(gate, null);
            Check(!player.GetComponent<TutorialState>().enabled && !player.GetComponent<BoxCollider>().enabled && player.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll,
                "Input/physics/trigger fence established before destination Start");
            release.Invoke(gate, null);
            Check(player.GetComponent<TutorialState>().enabled && player.GetComponent<BoxCollider>().enabled && player.GetComponent<Rigidbody>().constraints != RigidbodyConstraints.FreezeAll,
                "Input ownership released on completion/failure");
            var sequenceField = typeof(StorySequenceCoordinator).GetField("instance", PrivateStatic);
            var previousSequence = sequenceField.GetValue(null);
            var sequenceRoot = new GameObject("IsolatedResumeOwner") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sequenceRoot, preview);
            var sequence = sequenceRoot.AddComponent<StorySequenceCoordinator>();
            sequenceField.SetValue(null, sequence);
            try
            {
                var acquire = typeof(StorySequenceCoordinator).GetMethod("AcquireInternal", PrivateInstance);
                var owned = (StorySequenceToken)acquire.Invoke(sequence, new object[] { 98765431, "Resume owner fixture" });
                var stable = typeof(AlaalaSaveFoundation).GetMethod("IsStableEnvironmentForRestore", PrivateStatic);
                Check((bool)stable.Invoke(null, new object[] { null, owned }), "Verification accepts only its own loading owner");
                Check(!(bool)typeof(AlaalaSaveFoundation).GetMethod("IsStableEnvironment", PrivateStatic).Invoke(null, new object[] { null }), "Autosave remains blocked under the loading owner");
                var other = (StorySequenceToken)acquire.Invoke(sequence, new object[] { 98765432, "Unrelated owner fixture" });
                Check(!(bool)stable.Invoke(null, new object[] { null, owned }), "Unrelated cinematic ownership cannot be ignored");
                other.Release(); owned.Release();
            }
            finally { sequenceField.SetValue(null, previousSequence); }

            if (TaskManager.Instance == null && InventoryManager.Instance == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == AlaalaResumePolicy.MenuScene)
            {
                var planType = typeof(AlaalaSaveRestorePlan);
                var plan = planType.GetConstructor(PrivateInstance, null, new[] { typeof(AlaalaSaveSnapshot), typeof(AlaalaSaveCatalog), typeof(AlaalaSaveOwner), typeof(string) }, null)
                    .Invoke(new object[] { ending, catalog, owner, ending.runId });
                Set(plan, "installed", true); Set(plan, "previousMenuWorld", oldWorld);
                Check((bool)planType.GetMethod("AbortInMenu", PrivateInstance).Invoke(plan, new object[] { null }), "Failed staged session silently rolls back in safe menu");
                Check(flags.Count == 0 && ints.Count == 0 && strings.Count == 0 && !AlaalaSaveFoundation.RestorationInProgress, "Failed import leaves no mixed progression");
            }
            Check(File.ReadAllText(primary) == original && File.ReadAllText(backup) == original, "Failed live-stage fixture never rewrites saved data");
            Check(EditorBuildSettings.scenes.First(s => s.enabled).path == AlaalaResumePolicy.LoginScene, "Production startup is the existing FinalLoginPage");
            Check(System.Text.RegularExpressions.Regex.Match(File.ReadAllText("ProjectSettings/EditorBuildSettings.asset"), @"enabled: 1\s+path: ([^\r\n]+)").Groups[1].Value == AlaalaResumePolicy.LoginScene,
                "Authentication startup order is persisted on disk");
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "No Play Mode control used");
        }
        finally
        {
            SessionStoryState.ProgressValueChanged -= listen;
            EditorSceneManager.ClosePreviewScene(preview);
            flags.Clear(); foreach (var key in oldFlags) flags.Add(key);
            ints.Clear(); foreach (var entry in oldInts) ints.Add(entry.Key, entry.Value);
            strings.Clear(); foreach (var entry in oldStrings) strings.Add(entry.Key, entry.Value);
            typeof(AlaalaSaveFoundation).GetMethod("InstallWorld", PrivateStatic).Invoke(null, new object[] { oldWorld });
            typeof(AlaalaSaveFoundation).GetProperty("RestorationInProgress").SetValue(null, oldRestore);
            SpawnData.spawnPointName = oldSpawn;
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(root).StartsWith("alaala-storage-tests-", StringComparison.Ordinal) && Directory.Exists(root)) Directory.Delete(root, true);
        }
        return count + " isolated Phase 2B checks passed.";
    }

    private static AlaalaSaveSnapshot Ready(AlaalaSaveSnapshot snapshot)
    {
        foreach (var key in new[] { "inventory_tutorial_intro_seen", "inventory_tutorial_first_item_seen" })
            if (!snapshot.session.booleans.Any(x => x.key == key)) snapshot.session.booleans.Add(new SaveBoolean { key = key, value = true });
        snapshot.session.integers.RemoveAll(x => x.key == "draftworld_night_navigation_phase");
        snapshot.session.integers.Add(new SaveInteger { key = "draftworld_night_navigation_phase", value = 4 });
        return snapshot;
    }
}
#endif
