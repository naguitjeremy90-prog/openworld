using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alaala.Saving
{
    /// <summary>Production-only, event-driven deferred checkpoint capture. No loading/hydration.</summary>
    public sealed class AlaalaLocalAutosave : MonoBehaviour
    {
        private static AlaalaLocalAutosave instance;
        private AlaalaSaveCatalog catalog;
        private AlaalaLocalSaveStore store;
        private InventoryManager inventory;
        private TaskManager tasks;
        private Coroutine pending;
        private CancellationTokenSource lifetime;
        private long generation;
        private string lastDiagnostic;
        public LocalSaveResult LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Debug.isDebugBuild || !Application.isPlaying) return;
            new GameObject("AlaalaLocalAutosave").AddComponent<AlaalaLocalAutosave>();
        }

        private void Awake()
        {
            if (Application.isEditor || Debug.isDebugBuild || instance != null) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
            catalog = Resources.Load<AlaalaSaveCatalog>("SaveFoundation/AlaalaSaveCatalog");
            SessionStoryState.ProgressValueChanged += Changed;
            SceneManager.sceneLoaded += SceneReady;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        // One observational hook after the existing New Game reset, before the existing load.
        // Failure disables saving only; it cannot change the menu's reset/navigation behavior.
        public static void NotifyNewGameStarting()
        {
            if (instance == null) return;
            try { instance.BeginRun(); }
            catch (Exception exception)
            {
                instance.CancelPending(); instance.store = null;
                Debug.LogWarning("[AlaalaSave] Run initialization failed: " + exception.GetType().Name + ". New Game navigation is unaffected.");
            }
        }

        private void BeginRun()
        {
            CancelPending(); store = null; lastDiagnostic = null; LastResult = null;
            if (!AlaalaSaveRunContext.TryAuthorizeGenuineRun(out var error) ||
                !AlaalaLocalSaveStore.TryOpenAuthenticated(catalog, out store, out error))
                Diagnose("RunNotAuthorized", error);
        }

        internal static void ResumeVerifiedRun()
        {
            if (instance == null) return;
            instance.CancelPending(); instance.store = null;
            if (!AlaalaSaveRunContext.VerifyOwner(out _, out var error) ||
                !AlaalaLocalSaveStore.TryOpenAuthenticated(instance.catalog, out instance.store, out error))
            { instance.Diagnose("ResumeSaveUnavailable", error); return; }
            instance.BindManagers(); instance.Request();
        }

        internal static void SuspendForRestore()
        { if (instance != null) { instance.CancelPending(); instance.store = null; } }

        internal static void SuspendForAccountSwitch()
        {
            SuspendForRestore();
            if (instance != null) { instance.LastResult = null; instance.lastDiagnostic = null; }
        }

        private void SceneChanged(Scene oldScene, Scene newScene) { CancelPending(); BindManagers(); Request(); }
        private void SceneReady(Scene scene, LoadSceneMode mode)
        {
            BindManagers();
            Request(); // Deferred beyond sceneLoaded/Awake/Start; never captures inside this callback.
        }

        private void BindManagers()
        {
            if (tasks != TaskManager.Instance)
            {
                if (tasks != null) tasks.TaskChanged -= Request;
                tasks = TaskManager.Instance;
                if (tasks != null) tasks.TaskChanged += Request;
            }
            if (inventory != InventoryManager.Instance)
            {
                if (inventory != null) inventory.OnInventoryChanged -= Request;
                inventory = InventoryManager.Instance;
                if (inventory != null) inventory.OnInventoryChanged += Request;
            }
        }

        private void Changed(string key, SaveValueKind type) => Request();
        private void Request()
        {
            generation++;
            if (store == null || pending != null || SceneManager.GetActiveScene().path != "Assets/Scenes/DraftWorld.unity") return;
            lifetime = new CancellationTokenSource();
            pending = StartCoroutine(Drain(lifetime.Token));
        }

        private IEnumerator Drain(CancellationToken token)
        {
            // Let the synchronous reward transaction and destination Start methods finish.
            yield return null; yield return null;
            var retry = new WaitForSecondsRealtime(0.5f);
            while (!token.IsCancellationRequested)
            {
                if (!AlaalaSaveRunContext.VerifyOwner(out _, out _)) break;
                BindManagers();
                if (tasks == null || inventory == null) { yield return retry; continue; }
                if (!SessionStoryState.GetFlag("alaala_part2_complete") ||
                    !SessionStoryState.GetFlag("alaala_post_part2_draftworld_arrival_handled")) break;
                if (!SessionStoryState.GetFlag("alaala_epilogue_complete") &&
                    (SessionStoryState.GetFlag("alaala_part3_sulatin_handoff_complete") ||
                     SessionStoryState.GetFlag("alaala_part3_light_transport_started"))) break;
                string checkpoint = SessionStoryState.GetFlag("alaala_epilogue_complete")
                    ? AlaalaCheckpointRegistry.CompletedEpilogue : AlaalaCheckpointRegistry.SulatinSearch;
                // Flags alone do not authorize saving: capture validates every checkpoint dependency,
                // manager and presentation owner. Polling is only while an event has pending work.
                if (AlaalaSaveFoundation.RestorationInProgress || !AlaalaSaveFoundation.IsStableEnvironment(out _) ||
                    SessionStoryState.HasUnsafeSaveExecutionState(out _))
                { yield return retry; continue; }
                if (!AlaalaSaveFoundation.TryCapture(checkpoint, 1, catalog, tasks, inventory, out _, out var reason))
                { Diagnose("CheckpointUnavailable", reason); break; }
                long capturedGeneration = generation;
                var operation = store.SaveCheckpointAsync(checkpoint, token);
                while (!operation.IsCompleted) yield return null;
                if (token.IsCancellationRequested) break;
                if (operation.IsFaulted || operation.IsCanceled)
                { Diagnose("UnexpectedStorageFailure", "Storage operation did not complete. No gameplay restoration was attempted."); break; }
                LastResult = operation.Result;
                if (LastResult.Status == LocalSaveStatus.UnsafeCheckpoint) { yield return retry; continue; }
                if (LastResult.Status != LocalSaveStatus.Committed && LastResult.Status != LocalSaveStatus.Unchanged)
                { Diagnose(LastResult.Status.ToString(), LastResult.Diagnostic); break; }
                lastDiagnostic = null;
                Debug.Log("[AlaalaSave] " + LastResult.Status + " checkpoint=" + checkpoint + " revision=" + LastResult.Revision);
                if (generation == capturedGeneration) break;
                yield return retry;
            }
            pending = null;
            lifetime?.Dispose(); lifetime = null;
        }

        private void Diagnose(string status, string detail)
        {
            string diagnostic = status + ": " + detail;
            if (lastDiagnostic == diagnostic) return;
            lastDiagnostic = diagnostic;
            // Validation messages contain key/ID names, never entered text, tokens or filesystem paths.
            Debug.LogWarning("[AlaalaSave] " + diagnostic);
        }
        private void CancelPending()
        {
            lifetime?.Cancel();
            if (pending != null) StopCoroutine(pending);
            pending = null; lifetime?.Dispose(); lifetime = null;
        }
        private void OnDestroy()
        {
            CancelPending();
            SessionStoryState.ProgressValueChanged -= Changed;
            SceneManager.sceneLoaded -= SceneReady;
            SceneManager.activeSceneChanged -= SceneChanged;
            if (tasks != null) tasks.TaskChanged -= Request;
            if (inventory != null) inventory.OnInventoryChanged -= Request;
            if (instance == this) instance = null;
        }
    }
}
