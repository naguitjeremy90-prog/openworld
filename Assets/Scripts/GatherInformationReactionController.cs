using System;
using System.Collections.Generic;
using DialogueEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local presentation and recovery for the authored three-source reaction.</summary>
public sealed class GatherInformationReactionController : MonoBehaviour
{
    private const string TaskId = "main_investigate_pili";
    private const string CompletedStageId = "gather_information";
    private const string EligibleStageId = "find_knowledgeable_person";
    private const string SeenFlag = "gather_information_reaction_seen";
    private const string ResourcePath = "GatherInformationReaction";
    private const float RetryDelay = 1f;
    private const float IdleCheckInterval = 0.25f;

    [SerializeField, Min(0f)] private float zoomInDuration = 0.5f;
    [SerializeField, Min(0f)] private float zoomOutDuration = 0.5f;
    [SerializeField, Range(0.01f, 0.2f)] private float perspectiveFovReduction = 0.08f;
    [SerializeField, Range(0.01f, 0.2f)] private float orthographicSizeReduction = 0.08f;

    private enum Phase { Idle, ZoomIn, Dialogue, ZoomOut }
    private static GatherInformationReactionController playbackOwner;
    private readonly List<SceneEntrance> entrances = new List<SceneEntrance>();
    private readonly List<FadeController> fades = new List<FadeController>();
    private TaskManager subscribedManager;
    private GameObject conversationPrefab;
    private GameObject runtimeRoot;
    private NPCConversation runtimeConversation;
    private ConversationManager dialogueManager;
    private Action<NPCConversation, bool> closeListener;
    private StorySequenceToken storySequenceToken;
    private LensState lensState;
    private Phase phase;
    private int attemptId;
    private bool attemptValid;
    private bool completedNormally;
    private bool cleaningUp;
    private bool outgoingTransition;
    private bool tokenAcquisitionAttempted;
    private float phaseElapsed;
    private float nextIdleCheck;
    private float nextDependencyCheck;
    private float retryAfter;
    private bool reportedMissingPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaybackOwnership() => playbackOwner = null;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        nextIdleCheck = nextDependencyCheck = 0f;
        try
        {
            Subscribe();
            DiscoverSceneDependencies();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Cleanup(true);
        }
    }

    private void Start()
    {
        try { Subscribe(); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Cleanup(true);
        }
        nextIdleCheck = 0f;
    }

    private void Update()
    {
        try
        {
            if (phase != Phase.Idle)
            {
                if (!CanContinueAttempt())
                {
                    Cleanup(true);
                    return;
                }
                if (phase == Phase.Dialogue)
                {
                    // UI inactivity is failure, never evidence of normal completion.
                    if (dialogueManager.ActiveConversation != runtimeConversation ||
                        !dialogueManager.IsConversationActive) Cleanup(true);
                    return;
                }
                if (dialogueManager.IsConversationActive)
                {
                    Cleanup(true);
                    return;
                }
                phaseElapsed += Time.unscaledDeltaTime;
                bool zoomingIn = phase == Phase.ZoomIn;
                float duration = zoomingIn ? zoomInDuration : zoomOutDuration;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / duration);
                float amount = Mathf.Lerp(zoomingIn ? 0f : 1f, zoomingIn ? 1f : 0f,
                    Mathf.SmoothStep(0f, 1f, t));
                lensState.SetZoomAmount(amount, perspectiveFovReduction, orthographicSizeReduction);
                if (t < 1f) return;
                if (zoomingIn) StartOwnedDialogue();
                else Cleanup(false);
                return;
            }
            if (Time.unscaledTime < nextIdleCheck || Time.unscaledTime < retryAfter) return;
            nextIdleCheck = Time.unscaledTime + IdleCheckInterval;
            Subscribe();
            if (!IsEligible()) return;
            if (Time.unscaledTime >= nextDependencyCheck)
            {
                DiscoverSceneDependencies();
                nextDependencyCheck = Time.unscaledTime + RetryDelay;
            }
            TryBegin();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Cleanup(true);
        }
    }

    private void Subscribe()
    {
        TaskManager current = TaskManager.Instance;
        if (subscribedManager == current) return;
        if (subscribedManager != null) subscribedManager.StageChanged -= HandleStageChanged;
        subscribedManager = current;
        if (subscribedManager != null) subscribedManager.StageChanged += HandleStageChanged;
    }

    private void HandleStageChanged(string taskId, string stageId)
    {
        // RegisterProgress publishes the completed stage after storing the new stage.
        if (string.Equals(taskId, TaskId, StringComparison.Ordinal) &&
            string.Equals(stageId, CompletedStageId, StringComparison.Ordinal)) nextIdleCheck = 0f;
    }

    private bool IsGameplayScene()
    {
        Scene scene = gameObject.scene;
        return Application.isPlaying && isActiveAndEnabled && scene.IsValid() &&
            scene.isLoaded && scene == SceneManager.GetActiveScene();
    }

    private bool IsEligible()
    {
        TaskManager manager = TaskManager.Instance;
        return IsGameplayScene() && manager != null &&
            manager.GetTaskState(TaskId) == TaskState.Active &&
            manager.IsCurrentStage(TaskId, EligibleStageId) &&
            !SessionStoryState.GetFlag(SeenFlag);
    }

    private bool PresentationReady()
    {
        if (outgoingTransition || Time.timeScale <= 0f) return false;
        foreach (FadeController fade in fades)
            if (fade == null || !fade.isActiveAndEnabled || !fade.IncomingFadeCompleted) return false;
        return true;
    }

    private bool ManagerUsable(ConversationManager manager)
    {
        return manager != null && manager.isActiveAndEnabled &&
            manager == ConversationManager.Instance && manager.gameObject.scene == gameObject.scene;
    }

    private void DiscoverSceneDependencies()
    {
        foreach (SceneEntrance entrance in entrances)
            if (entrance != null) entrance.EntranceAccepted -= HandleEntranceAccepted;
        entrances.Clear();
        fades.Clear();
        foreach (SceneEntrance entrance in FindObjectsByType<SceneEntrance>(FindObjectsInactive.Include))
            if (entrance.gameObject.scene == gameObject.scene)
            {
                entrances.Add(entrance);
                entrance.EntranceAccepted += HandleEntranceAccepted;
            }
        foreach (FadeController fade in FindObjectsByType<FadeController>())
            if (fade.gameObject.scene == gameObject.scene) fades.Add(fade);
    }

    private void HandleEntranceAccepted()
    {
        outgoingTransition = true;
        Cleanup(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextDependencyCheck = nextIdleCheck = 0f;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        if (current != gameObject.scene) Cleanup(true);
        nextDependencyCheck = nextIdleCheck = 0f;
    }

    private void TryBegin()
    {
        ConversationManager manager = ConversationManager.Instance;
        if (!IsEligible() || !PresentationReady() || !ManagerUsable(manager) ||
            manager.IsConversationActive || StorySequenceCoordinator.IsStorySequenceActive) return;

        // A destroyed/disabled owner cannot retain the reaction-specific claim.
        if (playbackOwner != null && (!playbackOwner.isActiveAndEnabled ||
            !playbackOwner.attemptValid || !playbackOwner.IsGameplayScene())) playbackOwner.Cleanup(true);
        if (playbackOwner != null) return;
        if (conversationPrefab == null) conversationPrefab = Resources.Load<GameObject>(ResourcePath);
        if (conversationPrefab == null || conversationPrefab.GetComponent<NPCConversation>() == null)
        {
            if (!reportedMissingPrefab)
            {
                reportedMissingPrefab = true;
                Debug.LogError("Gather information reaction requires its authored Resources prefab and NPCConversation.", this);
            }
            return;
        }
        LensState captured = LensState.CaptureCurrent();
        if (captured == null || !IsEligible()) return;

        // No yield occurs between checking and claiming ownership.
        playbackOwner = this;
        attemptValid = true;
        completedNormally = false;
        ++attemptId;
        phase = Phase.ZoomIn;
        phaseElapsed = 0f;
        dialogueManager = manager;
        lensState = captured;
        runtimeRoot = Instantiate(conversationPrefab, transform, false);
        runtimeConversation = runtimeRoot.GetComponent<NPCConversation>();
        int ownedAttempt = attemptId;
        closeListener = (conversation, normal) => HandleClosed(ownedAttempt, conversation, normal);
        ConversationManager.ConversationClosed += closeListener;
        tokenAcquisitionAttempted = true;
        StorySequenceToken acquired = StorySequenceCoordinator.Acquire(this);
        // HUD callbacks may disable this component synchronously during Acquire.
        if (!attemptValid || playbackOwner != this)
        {
            if (acquired != null) acquired.Release();
            return;
        }
        storySequenceToken = acquired;
        if (!CanContinueAttempt()) Cleanup(true);
    }

    private bool CanContinueAttempt()
    {
        if (!attemptValid || playbackOwner != this || !IsGameplayScene() ||
            !ManagerUsable(dialogueManager) || !PresentationReady() ||
            runtimeConversation == null || storySequenceToken == null || !storySequenceToken.IsValid ||
            StorySequenceCoordinator.ActiveOwnerCount != 1 || lensState == null || !lensState.StillOwnsCamera())
            return false;
        TaskManager manager = TaskManager.Instance;
        return manager != null && manager.GetTaskState(TaskId) == TaskState.Active &&
            manager.IsCurrentStage(TaskId, EligibleStageId) &&
            (completedNormally || !SessionStoryState.GetFlag(SeenFlag));
    }

    private void StartOwnedDialogue()
    {
        if (!CanContinueAttempt() || !IsEligible() || dialogueManager.IsConversationActive)
        {
            Cleanup(true);
            return;
        }
        phase = Phase.Dialogue;
        ConversationManager manager = dialogueManager;
        NPCConversation conversation = runtimeConversation;
        manager.StartConversation(conversation);
        // Startup callbacks may cancel/disable ownership before StartConversation returns.
        if (!attemptValid)
        {
            if (manager != null && manager.ActiveConversation == conversation && !manager.IsConversationClosing)
                manager.EndConversation();
            return;
        }
        if (!CanContinueAttempt() || manager.ActiveConversation != conversation || !manager.IsConversationActive)
            Cleanup(true);
    }

    private void HandleClosed(int ownedAttempt, NPCConversation conversation, bool normal)
    {
        if (!attemptValid || ownedAttempt != attemptId || phase != Phase.Dialogue ||
            !ReferenceEquals(conversation, runtimeConversation)) return;
        try
        {
            if (!normal || !CanContinueAttempt() || !IsEligible())
            {
                Cleanup(true);
                return;
            }
            // Commit at our normal-close signal, before zoom-out or a possible scene exit.
            try { SessionStoryState.SetFlag(SeenFlag, true); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (!SessionStoryState.GetFlag(SeenFlag))
            {
                Cleanup(true);
                return;
            }
            if (!attemptValid) return;
            completedNormally = true;
            phase = Phase.ZoomOut;
            phaseElapsed = 0f;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Cleanup(true);
        }
    }

    private void Cleanup(bool retry)
    {
        if (cleaningUp) return;
        cleaningUp = true;
        attemptValid = false;
        ++attemptId;
        phase = Phase.Idle;
        if (retry) retryAfter = Time.unscaledTime + RetryDelay;
        try
        {
            Action<NPCConversation, bool> listener = closeListener;
            closeListener = null;
            if (listener != null) ConversationManager.ConversationClosed -= listener;
            ConversationManager manager = dialogueManager;
            NPCConversation conversation = runtimeConversation;
            dialogueManager = null;
            runtimeConversation = null;
            ProtectCleanup(() =>
            {
                if (manager != null && conversation != null &&
                    manager.ActiveConversation == conversation && !manager.IsConversationClosing) manager.EndConversation();
            });
            LensState captured = lensState;
            lensState = null;
            ProtectCleanup(() => captured?.Restore());
            StorySequenceToken token = storySequenceToken;
            storySequenceToken = null;
            bool recoverToken = tokenAcquisitionAttempted;
            tokenAcquisitionAttempted = false;
            ProtectCleanup(() =>
            {
                // Acquire can throw from a HUD listener after registering our token.
                // Reacquiring the same owner retrieves that token for cleanup.
                if (token == null && recoverToken && StorySequenceCoordinator.HasInstance)
                    token = StorySequenceCoordinator.Acquire(this);
                token?.Release();
            });
            GameObject root = runtimeRoot;
            runtimeRoot = null;
            ProtectCleanup(() => { if (root != null) Destroy(root); });
        }
        finally
        {
            if (ReferenceEquals(playbackOwner, this)) playbackOwner = null;
            completedNormally = false;
            phaseElapsed = 0f;
            cleaningUp = false;
        }
    }

    private void ProtectCleanup(Action action)
    {
        try { action(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        if (subscribedManager != null) subscribedManager.StageChanged -= HandleStageChanged;
        subscribedManager = null;
        foreach (SceneEntrance entrance in entrances)
            if (entrance != null) entrance.EntranceAccepted -= HandleEntranceAccepted;
        entrances.Clear();
        fades.Clear();
        Cleanup(true);
    }

    private void OnDestroy() => OnDisable();

    private sealed class LensState
    {
        private readonly Camera outputCamera;
        private readonly CinemachineBrain brain;
        private readonly CinemachineCamera virtualCamera;
        private readonly bool usesVirtualCamera;
        private readonly bool orthographic;
        private readonly float originalValue;

        private LensState(Camera camera, CinemachineBrain cameraBrain, CinemachineCamera current, bool isOrtho, float value)
        { outputCamera = camera; brain = cameraBrain; virtualCamera = current; usesVirtualCamera = current != null; orthographic = isOrtho; originalValue = value; }

        public static LensState CaptureCurrent()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return null;
            CinemachineBrain cameraBrain = camera.GetComponent<CinemachineBrain>();
            if (cameraBrain != null && cameraBrain.isActiveAndEnabled)
            {
                CinemachineCamera current = cameraBrain.ActiveVirtualCamera as CinemachineCamera;
                if (cameraBrain.IsBlending || current == null || !current.isActiveAndEnabled) return null;
                LensSettings lens = current.Lens;
                return new LensState(camera, cameraBrain, current, lens.Orthographic,
                    lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView);
            }
            // A disabled Brain does not drive the output camera's lens.
            return new LensState(camera, null, null, camera.orthographic,
                camera.orthographic ? camera.orthographicSize : camera.fieldOfView);
        }

        public bool StillOwnsCamera()
        {
            if (outputCamera == null || !outputCamera.isActiveAndEnabled || Camera.main != outputCamera) return false;
            if (usesVirtualCamera)
                return brain != null && brain.isActiveAndEnabled && !brain.IsBlending &&
                    virtualCamera != null && virtualCamera.isActiveAndEnabled &&
                    virtualCamera.Lens.Orthographic == orthographic &&
                    ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera);
            CinemachineBrain currentBrain = outputCamera.GetComponent<CinemachineBrain>();
            return outputCamera.orthographic == orthographic &&
                (currentBrain == null || !currentBrain.isActiveAndEnabled);
        }

        public void SetZoomAmount(float amount, float perspectiveReduction, float orthoReduction)
        {
            float value = Mathf.Lerp(originalValue, originalValue *
                (1f - (orthographic ? orthoReduction : perspectiveReduction)), Mathf.Clamp01(amount));
            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                if (orthographic) lens.OrthographicSize = value; else lens.FieldOfView = value;
                virtualCamera.Lens = lens;
            }
            else if (outputCamera != null && !usesVirtualCamera)
            {
                if (orthographic) outputCamera.orthographicSize = value; else outputCamera.fieldOfView = value;
            }
        }

        public void Restore()
        {
            // Restore only the original target; never write to a replacement camera.
            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                if (orthographic) lens.OrthographicSize = originalValue; else lens.FieldOfView = originalValue;
                virtualCamera.Lens = lens;
            }
            else if (outputCamera != null && !usesVirtualCamera)
            {
                if (orthographic) outputCamera.orthographicSize = originalValue; else outputCamera.fieldOfView = originalValue;
            }
        }
    }
}
