using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alaala.Saving
{
    public sealed class AlaalaResumeCoordinator : MonoBehaviour
    {
        private static AlaalaResumeCoordinator active;
        public static bool IsBusy => active != null;
        public static string LastFailureMessage { get; private set; }
        internal static void ClearAccountFailure()
        { if (!IsBusy) LastFailureMessage = null; }
        public static event Action Finished;
        private AlaalaResumeAttempt attempt;
        private long ticket;
        private AlaalaSaveCatalog catalog;
        private AlaalaSaveRestorePlan plan;
        private AlaalaSaveSnapshot snapshot;
        private AlaalaResumeWorldGate world;
        private StorySequenceToken token;
        private GameObject overlay, createdTasks, createdInventory;
        private bool success, authorized, loadingStarted, recovering;
        private string failure;
        private AsyncOperation loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { active = null; LastFailureMessage = null; Finished = null; }

        public static bool TryBegin(AlaalaContinueMenu view)
        {
            if (IsBusy || !AlaalaContinueMenu.Production || SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene ||
                !AlaalaSaveOwner.TryGetAuthenticated(out var owner) ||
                !Application.CanStreamedLevelBeLoaded(AlaalaResumePolicy.MenuScene) ||
                !Application.CanStreamedLevelBeLoaded(AlaalaResumePolicy.LoginScene)) return false;
            var login = Authentication.AuthenticationServices.Current.Session;
            var root = new GameObject("AlaalaResumeCoordinator");
            var coordinator = root.AddComponent<AlaalaResumeCoordinator>(); active = coordinator;
            DontDestroyOnLoad(root);
            coordinator.catalog = Resources.Load<AlaalaSaveCatalog>("SaveFoundation/AlaalaSaveCatalog");
            coordinator.attempt = new AlaalaResumeAttempt(() => ReferenceEquals(login, Authentication.AuthenticationServices.Current.Session) &&
                AlaalaSaveOwner.TryGetAuthenticated(out var current) && current.TitleId == owner.TitleId && current.AccountId == owner.AccountId);
            if (!coordinator.attempt.TryBegin(out coordinator.ticket)) { active = null; Destroy(root); return false; }
            LastFailureMessage = null;
            SceneManager.sceneLoaded += coordinator.DestinationLoaded;
            Application.logMessageReceived += coordinator.InitializationLog;
            coordinator.StartCoroutine(coordinator.Run()); return true;
        }

        private IEnumerator Run()
        {
            yield return Drive(Restore());
            if (!success) yield return Drive(Recover());
            if (!success && SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene)
            {
                // Do not expose a partially initialized gameplay scene if the first
                // recovery path itself failed. Both safe scenes are build-validated.
                try { SceneManager.LoadScene(AlaalaResumePolicy.MenuScene, LoadSceneMode.Single); }
                catch (Exception)
                {
                    try { SceneManager.LoadScene(AlaalaResumePolicy.LoginScene, LoadSceneMode.Single); }
                    catch (Exception) { failure = "Neither safe scene could be requested; restart required."; }
                }
                float deadline = Time.realtimeSinceStartup + 15;
                while (!AlaalaResumePolicy.IsSafeMenuScene(SceneManager.GetActiveScene().path, SceneManager.GetActiveScene().isLoaded) && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
            if (!success && !AlaalaResumePolicy.IsSafeMenuScene(SceneManager.GetActiveScene().path, SceneManager.GetActiveScene().isLoaded))
            {
                // If Unity itself cannot activate either safe scene, releasing the input
                // fence would expose partial gameplay. Fail closed with an explicit restart message.
                if (overlay == null) CreateOverlay();
                var label = new GameObject("RestartRequired", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(overlay.transform, false);
                var text = label.GetComponent<TextMeshProUGUI>(); text.text = "Hindi maibalik nang ligtas ang laro. Isara at buksan itong muli. Napanatili ang save.";
                text.fontSize = 26; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(1000, 150);
                AlaalaSaveRunContext.InvalidateRun(); AlaalaSaveFoundation.RestorationInProgress = true;
                Debug.LogWarning("[AlaalaResume] Both safe-scene recovery paths failed. Gameplay remains fenced; restart required.");
                yield break;
            }
            if (!success && authorized) AlaalaSaveRunContext.InvalidateRun();
            world?.Release(); world = null;
            token?.Release(); token = null;
            if (overlay != null) Destroy(overlay);
            attempt.End();
            SceneManager.sceneLoaded -= DestinationLoaded;
            Application.logMessageReceived -= InitializationLog;
            if (!success)
            {
                LastFailureMessage = "Hindi maibalik ang naitalang laro. Napanatili ang mga save file.";
                Debug.LogWarning("[AlaalaResume] " + (failure ?? "Restoration rejected."));
            }
            active = null;
            Finished?.Invoke(); Destroy(gameObject);
        }

        private IEnumerator Drive(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception exception) { failure = exception is ResumeRejected ? exception.Message : "Restoration failed: " + exception.GetType().Name; break; }
                yield return next;
            }
            (routine as IDisposable)?.Dispose();
        }

        private void RequireOwner()
        { if (!attempt.IsCurrent(ticket)) throw new ResumeRejected("Authenticated session changed; restoration cancelled."); }

        private IEnumerator Restore()
        {
            RequireOwner();
            if (!AlaalaLocalSaveStore.TryOpenAuthenticated(catalog, out var store, out var error)) throw new ResumeRejected(error);
            var read = store.ReadValidatedSnapshotAsync(); float deadline = Time.realtimeSinceStartup + 30;
            while (!read.IsCompleted) { RequireOwner(); if (Time.realtimeSinceStartup > deadline) throw new ResumeRejected("Save discovery timed out."); yield return null; }
            RequireOwner();
            var available = read.GetAwaiter().GetResult();
            AlaalaSaveOwner.TryGetAuthenticated(out var owner);
            if (!AlaalaResumePolicy.CanOfferContinue(available, catalog, owner, out var message)) throw new ResumeRejected(message);
            if (available.Status == LocalSaveStatus.ValidBackup || available.BackupStatus != LocalSaveStatus.ValidSave)
            {
                var recovery = store.RecoverBackupAsync();
                while (!recovery.IsCompleted) { RequireOwner(); if (Time.realtimeSinceStartup > deadline) throw new ResumeRejected("Backup recovery timed out."); yield return null; }
                RequireOwner(); var recovered = recovery.GetAwaiter().GetResult();
                if (recovered.Status != LocalSaveStatus.Recovered && recovered.Status != LocalSaveStatus.ValidSave) throw new ResumeRejected("Controlled backup recovery was rejected.");
                read = store.ReadValidatedSnapshotAsync();
                while (!read.IsCompleted) { RequireOwner(); if (Time.realtimeSinceStartup > deadline) throw new ResumeRejected("Save revalidation timed out."); yield return null; }
                available = read.GetAwaiter().GetResult(); RequireOwner();
                if (!AlaalaResumePolicy.CanOfferContinue(available, catalog, owner, out message)) throw new ResumeRejected(message);
            }
            snapshot = available.Snapshot;
            if (!AlaalaResumePolicy.FreshMenu(out error) || !AlaalaSaveRunContext.TryAuthorizeResume(snapshot, catalog, out error)) throw new ResumeRejected(error);
            authorized = true;
            AlaalaLocalAutosave.SuspendForRestore();
            if (!AlaalaSaveFoundation.TryPrepareRestore(snapshot, catalog, out plan, out error)) throw new ResumeRejected(error);
            if (TaskManager.Instance == null) createdTasks = Instantiate(catalog.TaskManagerTemplate);
            if (InventoryManager.Instance == null) createdInventory = new GameObject("RestoredInventory", typeof(InventoryManager));
            RequireOwner();
            if (!plan.TryInstallBeforeSceneInitialization(TaskManager.Instance, InventoryManager.Instance, out error)) throw new ResumeRejected(error);
            CreateOverlay();
            token = StorySequenceCoordinator.Acquire(this);
            if (token == null || !token.IsValid) throw new ResumeRejected("Could not acquire loading ownership.");
            loadingStarted = true;
            loading = SceneManager.LoadSceneAsync(plan.TargetScene, LoadSceneMode.Single);
            if (loading == null) throw new ResumeRejected("Approved scene could not be loaded.");
            deadline = Time.realtimeSinceStartup + 30;
            while (!loading.isDone)
            {
                if (!attempt.IsCurrent(ticket)) failure = "Account changed during scene initialization.";
                if (Time.realtimeSinceStartup > deadline) throw new ResumeRejected("Scene initialization timed out.");
                yield return null;
            }
            RequireOwner(); if (failure != null) throw new ResumeRejected(failure);
            yield return null; yield return null; // Destination Start and first presentation updates finish under the input fence.
            if (world == null || !world.IsReady(out error)) throw new ResumeRejected(error ?? "Destination input/spawn gate is missing.");
            var journal = ReconstructionJournalManager.Instance;
            if (journal == null || !journal.PrepareSaveAssets(catalog, snapshot.session, out error) ||
                !journal.TryHydrateSaveJournalSilently(snapshot.session, catalog, out error)) throw new ResumeRejected(error ?? "Journal is unavailable.");
            journal.RefreshRestoredViews();
            foreach (var view in FindObjectsByType<GameplaySystemLockView>(FindObjectsInactive.Include))
                if (view.gameObject.scene.path == plan.TargetScene) view.Refresh();
            if (failure != null) throw new ResumeRejected(failure);
            deadline = Time.realtimeSinceStartup + 15;
            while (!plan.TryVerifyWithRestorationOwner(journal, token, out error))
            {
                RequireOwner(); if (failure != null || Time.realtimeSinceStartup > deadline) throw new ResumeRejected(failure ?? error);
                yield return null;
            }
            RequireOwner(); if (failure != null) throw new ResumeRejected(failure);
            world.Release(); world = null; token.Release(); token = null;
            if (failure != null) throw new ResumeRejected(failure);
            try { AlaalaLocalAutosave.ResumeVerifiedRun(); }
            catch (Exception exception) { Debug.LogWarning("[AlaalaResume] Autosave reattachment failed: " + exception.GetType().Name); }
            success = true;
        }

        private void DestinationLoaded(Scene scene, LoadSceneMode mode)
        {
            if (recovering || snapshot == null || scene.path != snapshot.targetScene) return;
            try
            {
                world = new AlaalaResumeWorldGate(scene, snapshot.spawnRoute);
                world.Hold(); // sceneLoaded is before Start, Update and the first physics step.
            }
            catch (Exception exception) { failure = "Destination preparation failed: " + exception.GetType().Name; }
        }

        private void InitializationLog(string condition, string stackTrace, LogType type)
        {
            if (loadingStarted && !recovering && !success && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                failure = "Destination initialization reported an error; staged progress will be discarded.";
        }

        private IEnumerator Recover()
        {
            recovering = true;
            if (authorized) AlaalaLocalAutosave.SuspendForRestore();
            if (loadingStarted && (SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene || loading != null && !loading.isDone))
            {
                // Synchronous recovery also finishes any outstanding Unity async activation.
                // It is restricted to failure recovery and never substitutes a gameplay scene.
                SceneManager.LoadScene(AlaalaResumePolicy.MenuScene, LoadSceneMode.Single);
                float deadline = Time.realtimeSinceStartup + 15;
                while (SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene || !SceneManager.GetActiveScene().isLoaded)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new ResumeRejected("Safe-menu activation timed out.");
                    yield return null;
                }
                yield return null;
            }
            if (plan != null && plan.IsInstalled)
            {
                if (GameplaySystemTutorialManager.HasInstance) GameplaySystemTutorialManager.Instance.ResetForNewGame();
                foreach (var presentation in FindObjectsByType<JournalEntryPresentationController>()) presentation.ResetForNewGame();
                foreach (var attention in FindObjectsByType<JournalHUDEntryAttention>()) attention.ResetForNewGame();
                if (!plan.AbortInMenu(out var error)) failure = "Staged-session cleanup failed; restart is required: " + error;
            }
            if (authorized) AlaalaSaveRunContext.InvalidateRun();
            if (createdTasks != null) Destroy(createdTasks);
            if (createdInventory != null) Destroy(createdInventory);
            yield return null;
        }

        private void CreateOverlay()
        {
            overlay = new GameObject("SaveLoadingOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            var panel = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(overlay.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = Color.black;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= DestinationLoaded;
            Application.logMessageReceived -= InitializationLog;
            world?.Release(); token?.Release();
            if (active == this) active = null;
        }
        private sealed class ResumeRejected : Exception { internal ResumeRejected(string message) : base(message) { } }
    }
}
