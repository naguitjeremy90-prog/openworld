using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alaala.Saving
{
    /// <summary>Explicit in-memory operations only. No scene loading, event subscriptions or storage.</summary>
    public static class AlaalaSaveFoundation
    {
        public static bool RestorationInProgress { get; internal set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRestorationStatus() { RestorationInProgress = false; }
        public static bool TryCapture(string checkpointId, long revision, AlaalaSaveCatalog catalog,
            TaskManager tasks, InventoryManager inventory, out AlaalaSaveSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (RestorationInProgress) return AlaalaSaveCatalog.Fail("Restoration is still in progress.", out error);
            if (!AlaalaSaveRunContext.VerifyOwner(out var owner, out error)) return false;
            if (catalog == null || !catalog.Validate(out error)) return false;
            if (!AlaalaCheckpointRegistry.TryGet(checkpointId, out var checkpoint))
                return AlaalaSaveCatalog.Fail("Unsupported checkpoint.", out error);
            if (SceneManager.GetActiveScene().path != checkpoint.ScenePath || tasks == null || tasks != TaskManager.Instance ||
                inventory == null || inventory != InventoryManager.Instance)
                return AlaalaSaveCatalog.Fail("Capture requires the approved scene and live managers.", out error);
            if (!IsStableEnvironment(out error) || SessionStoryState.HasUnsafeSaveExecutionState(out error)) return false;
            if (!SessionStoryState.TryCaptureDurable(new AlaalaDurableStateRegistry(catalog), out var state, out error)) return false;
            var candidate = new AlaalaSaveSnapshot
            {
                titleId = owner.TitleId, playFabAccountId = owner.AccountId, gameVersion = Application.version,
                saveRevision = revision, runKind = AlaalaSaveRunContext.Kind, runId = AlaalaSaveRunContext.RunId,
                checkpointId = checkpoint.Id, targetScene = checkpoint.ScenePath, spawnRoute = checkpoint.SpawnRoute,
                session = state, tasks = tasks.CaptureSaveTasks(), inventory = inventory.CaptureSaveInventory(),
                world = CaptureWorld()
            };
            var main = candidate.tasks.FirstOrDefault(t => t.state == TaskState.Active && catalog.Tasks.First(d => d.TaskId == t.taskId).Type == TaskType.Main);
            candidate.activeMainTaskId = main?.taskId ?? "";
            if (!AlaalaSaveValidation.Validate(candidate, catalog, owner, out error)) return false;
            snapshot = candidate;
            return true;
        }

        public static bool TryPrepareRestore(AlaalaSaveSnapshot snapshot, AlaalaSaveCatalog catalog,
            out AlaalaSaveRestorePlan plan, out string error)
        {
            plan = null;
            if (!AlaalaSaveRunContext.VerifyOwner(out var owner, out error)) return false;
            if (!AlaalaSaveValidation.Validate(snapshot, catalog, owner, out error)) return false;
            // Detach from caller-owned mutable lists before granting a restoration plan.
            var detached = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(snapshot));
            Canonicalize(detached);
            plan = new AlaalaSaveRestorePlan(detached, catalog, owner, AlaalaSaveRunContext.RunId);
            return true;
        }

        internal static void Canonicalize(AlaalaSaveSnapshot value)
        {
            value.session.booleans = value.session.booleans.Where(x => x.value).OrderBy(x => x.key, StringComparer.Ordinal).ToList();
            value.session.integers = value.session.integers.Where(x => x.value != 0).OrderBy(x => x.key, StringComparer.Ordinal).ToList();
            value.session.strings = value.session.strings.Where(x => x.value.Length > 0).OrderBy(x => x.key, StringComparer.Ordinal).ToList();
            value.tasks = value.tasks.OrderBy(x => x.taskId, StringComparer.Ordinal).ToList();
            value.inventory.owned.Sort(StringComparer.Ordinal);
            value.inventory.collected.Sort(StringComparer.Ordinal);
            value.inventory.consumed.Sort(StringComparer.Ordinal);
        }

        internal static AlaalaWorldSnapshot CaptureWorld() => new AlaalaWorldSnapshot
        {
            isMorning = GameFlags.isMorning, canTeleport = GameFlags.canTeleport,
            movementDone = TutorialState.movementDone, playerMovementDone = PlayerMovement.movementDone
        };
        internal static void InstallWorld(AlaalaWorldSnapshot world)
        {
            GameFlags.isMorning = world.isMorning; GameFlags.canTeleport = world.canTeleport;
            TutorialState.movementDone = world.movementDone; PlayerMovement.movementDone = world.playerMovementDone;
        }
        internal static bool IsStableEnvironment(out string error)
            => IsStableEnvironmentCore(out error, null);

        internal static bool IsStableEnvironmentForRestore(out string error, StorySequenceToken restorationOwner)
            => IsStableEnvironmentCore(out error, restorationOwner);

        private static bool IsStableEnvironmentCore(out string error, StorySequenceToken restorationOwner)
        {
            error = null;
            bool ownsOnlySequence = restorationOwner != null && restorationOwner.IsValid &&
                StorySequenceCoordinator.TryGetExisting(out var coordinator) && restorationOwner.Coordinator == coordinator &&
                StorySequenceCoordinator.ActiveOwnerCount == 1;
            if (Time.timeScale <= 0 || StorySequenceCoordinator.IsStorySequenceActive && !ownsOnlySequence || !string.IsNullOrEmpty(SpawnData.spawnPointName) ||
                InventoryDocumentViewer.IsAnyOpen || (ReconstructionJournalManager.Instance != null && ReconstructionJournalManager.Instance.IsOpen) ||
                (DialogueEditor.ConversationManager.Instance != null && DialogueEditor.ConversationManager.Instance.IsConversationActive) ||
                (GameplaySystemTutorialManager.HasInstance && GameplaySystemTutorialManager.Instance.HasActiveTutorial) ||
                (TaskManager.Instance != null && TaskManager.Instance.HasPendingNotifications) ||
                (JournalEntryPresentationController.Instance != null && (JournalEntryPresentationController.Instance.IsPresenting || JournalEntryPresentationController.Instance.PendingCount > 0)) ||
                (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered))
                return AlaalaSaveCatalog.Fail("A presentation, modal or transition still owns gameplay.", out error);
            foreach (var ui in UnityEngine.Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (ui.IsOpen || ui.IsWindowTransitioning) return AlaalaSaveCatalog.Fail("Inventory is open/transitioning.", out error);
            foreach (var ui in UnityEngine.Object.FindObjectsByType<ClarityDocumentViewer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (ui.IsOpen) return AlaalaSaveCatalog.Fail("Document investigation is open.", out error);
            foreach (var focus in UnityEngine.Object.FindObjectsByType<CameraFocusManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (focus.IsFocusing) return AlaalaSaveCatalog.Fail("Camera focus is active.", out error);
            foreach (var fade in UnityEngine.Object.FindObjectsByType<FadeController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (fade.isActiveAndEnabled && !fade.IncomingFadeCompleted) return AlaalaSaveCatalog.Fail("Incoming fade has not completed.", out error);
            foreach (var director in UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (director.state == UnityEngine.Playables.PlayState.Playing) return AlaalaSaveCatalog.Fail("A Timeline is playing.", out error);
            return true;
        }
    }

    /// <summary>
    /// Explicit staging/verification used by AlaalaResumeCoordinator. The caller holds input
    /// across destination initialization and releases it only after consistency verification.
    /// </summary>
    public sealed class AlaalaSaveRestorePlan
    {
        private readonly AlaalaSaveSnapshot snapshot;
        private readonly AlaalaSaveCatalog catalog;
        private readonly AlaalaSaveOwner owner;
        private readonly string authorizedRunId;
        private bool installed, verified;
        private AlaalaWorldSnapshot previousMenuWorld;
        public string TargetScene => snapshot.targetScene;
        public string SpawnRoute => snapshot.spawnRoute;
        public bool IsInstalled => installed;
        public bool IsVerified => verified;
        internal AlaalaSaveRestorePlan(AlaalaSaveSnapshot snapshot, AlaalaSaveCatalog catalog, AlaalaSaveOwner owner, string runId)
        { this.snapshot = snapshot; this.catalog = catalog; this.owner = owner; authorizedRunId = runId; }

        private bool Revalidate(out string error)
        {
            if (!AlaalaSaveRunContext.VerifyOwner(out var current, out error)) return false;
            if (current.TitleId != owner.TitleId || current.AccountId != owner.AccountId || authorizedRunId != AlaalaSaveRunContext.RunId)
                return AlaalaSaveCatalog.Fail("Restore plan was superseded by an account/run change.", out error);
            return AlaalaSaveValidation.Validate(snapshot, catalog, owner, out error);
        }

        public bool TryInstallBeforeSceneInitialization(TaskManager tasks, InventoryManager inventory, out string error)
        {
            if (!Revalidate(out error)) return false;
            if (installed || SceneManager.GetActiveScene().path != "Assets/Scenes/SceneMenu.unity" ||
                tasks == null || tasks != TaskManager.Instance || inventory == null || inventory != InventoryManager.Instance ||
                tasks.CaptureSaveTasks().Any(t => t.state != TaskState.Inactive) || inventory.GetOwnedItems().Count != 0 || !SessionStoryState.IsEmptyForSaveRestore)
                return AlaalaSaveCatalog.Fail("Prepare fresh, empty managers in SceneMenu before hydration. Live-playthrough replacement is unsupported.", out error);
            if (!AlaalaSaveFoundation.IsStableEnvironment(out error)) return false;
            if (!tasks.CanHydrateSaveTasks(snapshot.tasks, snapshot.session, catalog, out error)) return false;
            var previousWorld = AlaalaSaveFoundation.CaptureWorld();
            previousMenuWorld = previousWorld;
            var empty = new AlaalaSessionSnapshot(); var previousTasks = tasks.CaptureSaveTasks(); var previousInventory = inventory.CaptureSaveInventory();
            try
            {
                AlaalaSaveFoundation.RestorationInProgress = true;
                if (!SessionStoryState.TryRestoreDurableSilently(snapshot.session, new AlaalaDurableStateRegistry(catalog), out error) ||
                    !tasks.TryHydrateSaveTasksSilently(snapshot.tasks, snapshot.session, catalog, out error) ||
                    !inventory.TryHydrateSaveInventorySilently(snapshot.inventory, snapshot.session, catalog, out error))
                    throw new InvalidOperationException(error);
                AlaalaSaveFoundation.InstallWorld(snapshot.world);
                // This route exists in DraftWorld. No Part3Spawn/white-wall context is manufactured.
                SpawnData.spawnPointName = snapshot.spawnRoute;
                installed = true;
                return true;
            }
            catch (Exception)
            {
                AlaalaSaveFoundation.RestorationInProgress = false;
                SessionStoryState.TryRestoreDurableSilently(empty, new AlaalaDurableStateRegistry(catalog), out _);
                tasks.TryHydrateSaveTasksSilently(previousTasks, empty, catalog, out _);
                inventory.TryHydrateSaveInventorySilently(previousInventory, empty, catalog, out _);
                AlaalaSaveFoundation.InstallWorld(previousWorld); SpawnData.spawnPointName = "";
                return AlaalaSaveCatalog.Fail("Pre-initialization hydration failed; the empty prepared session was restored.", out error);
            }
        }

        public bool TryVerifyAfterSceneInitialization(ReconstructionJournalManager journal, out string error)
            => VerifyDestination(journal, null, out error);

        internal bool TryVerifyWithRestorationOwner(ReconstructionJournalManager journal, StorySequenceToken token, out string error)
            => VerifyDestination(journal, token, out error);

        private bool VerifyDestination(ReconstructionJournalManager journal, StorySequenceToken token, out string error)
        {
            if (!Revalidate(out error)) return false;
            if (!installed || verified || SceneManager.GetActiveScene().path != TargetScene || !string.IsNullOrEmpty(SpawnData.spawnPointName) ||
                TaskManager.Instance == null || InventoryManager.Instance == null || journal == null || journal != ReconstructionJournalManager.Instance)
                return AlaalaSaveCatalog.Fail("Destination managers/spawn are not ready for verification.", out error);
            if (!AlaalaSaveFoundation.IsStableEnvironmentForRestore(out error, token)) return false;
            if (SessionStoryState.HasUnsafeSaveExecutionState(out error)) return false;
            if (!SessionStoryState.TryCaptureDurable(new AlaalaDurableStateRegistry(catalog), out var live, out error)) return false;
            var comparison = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(snapshot));
            comparison.session = live; comparison.tasks = TaskManager.Instance.CaptureSaveTasks();
            comparison.inventory = InventoryManager.Instance.CaptureSaveInventory(); comparison.world = AlaalaSaveFoundation.CaptureWorld();
            if (!AlaalaSaveValidation.Validate(comparison, catalog, owner, out error)) return false;
            if (JsonUtility.ToJson(comparison) != JsonUtility.ToJson(snapshot))
                return AlaalaSaveCatalog.Fail("Scene initialization changed progression; input must remain held for recovery.", out error);
            if (!journal.TryHydrateSaveJournalSilently(snapshot.session, catalog, out error)) return false;
            verified = true;
            AlaalaSaveFoundation.RestorationInProgress = false;
            return true;
        }

        internal bool AbortInMenu(out string error)
        {
            if (!installed || SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene)
                return AlaalaSaveCatalog.Fail("Abort requires the failed staged session back in SceneMenu.", out error);
            var empty = new AlaalaSessionSnapshot();
            if (!SessionStoryState.TryRestoreDurableSilently(empty, new AlaalaDurableStateRegistry(catalog), out error)) return false;
            var tasks = TaskManager.Instance;
            if (tasks != null && !tasks.TryHydrateSaveTasksSilently(tasks.CaptureSaveTasks(), empty, catalog, out error)) return false;
            if (InventoryManager.Instance != null && !InventoryManager.Instance.TryHydrateSaveInventorySilently(new AlaalaInventorySnapshot(), empty, catalog, out error)) return false;
            AlaalaSaveFoundation.InstallWorld(previousMenuWorld); SpawnData.spawnPointName = "";
            installed = false; AlaalaSaveFoundation.RestorationInProgress = false;
            return true;
        }
    }
}
