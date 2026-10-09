using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>Unlocks Inventory after the scene-local morning dialogue and camera return succeed.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(12000)]
public sealed class PosadaMorningInventoryIntroduction : MonoBehaviour
{
    private const string SleepCompleted = "posada_first_sleep_completed";
    private const string SelaCompleted = "aling_sela_intro_completed";
    private const string IntroductionCompleted = "posada_morning_inventory_intro_completed";

    [SerializeField] private BoxCollider triggerVolume;
    [SerializeField] private CapsuleCollider playerCollider;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private NPCConversationTrigger selaTrigger;
    [SerializeField] private NPCConversation morningConversation;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;
    [SerializeField] private FadeController incomingFade;
    [SerializeField] private ReconstructionJournalManager journal;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private SceneEntrance[] sceneEntrances;

    private StorySequenceToken storyToken;
    private bool occupied;
    private bool running;
    private bool closed;
    private bool normalClose;
    private bool focusObserved;
    private bool focusSuperseded;
    private UnityEvent focusCallback;
    private bool mustLeaveBeforeRetry;
    private bool outgoingTransition;
    private bool completionPending;
    private float nextUnlockRecoveryTime;

    private void OnEnable()
    {
        ConversationManager.ConversationClosed += HandleClosed;
        if (focusManager != null) focusManager.FocusStarted += HandleFocusStarted;
        if (sceneEntrances != null)
            foreach (var entrance in sceneEntrances)
                if (entrance != null) entrance.EntranceAccepted += HandleEntranceAccepted;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == playerCollider) occupied = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != playerCollider) return;
        occupied = false;
        mustLeaveBeforeRetry = false;
    }

    private void Update()
    {
        // Reconcile actual geometry, including spawn inside the volume and missed enter callbacks.
        occupied = triggerVolume != null && triggerVolume.enabled && triggerVolume.isTrigger &&
            playerCollider != null && playerCollider.enabled && playerCollider.gameObject.activeInHierarchy &&
            Physics.ComputePenetration(triggerVolume, triggerVolume.transform.position,
                triggerVolume.transform.rotation, playerCollider, playerCollider.transform.position,
                playerCollider.transform.rotation, out _, out _);
        if (!occupied) mustLeaveBeforeRetry = false;

        if (running)
        {
            if (ConversationManager.Instance == null || morningConversation == null || selaTrigger == null ||
                !selaTrigger.isActiveAndEnabled || focusManager == null ||
                !focusManager.isActiveAndEnabled || outgoingTransition)
            {
                CancelOwnedPresentation();
                return;
            }
            if (!closed || focusManager.IsFocusing) return;

            bool successful = normalClose && focusObserved && !focusSuperseded &&
                focusManager.LastReturnCompleted;
            running = false;
            ReleaseStoryToken();
            if (successful)
            {
                completionPending = true;
                TryCompleteInventoryUnlock();
            }
            else
                mustLeaveBeforeRetry = true;
            return;
        }

        bool introductionCompleted = SessionStoryState.GetFlag(IntroductionCompleted);
        if (completionPending || (introductionCompleted &&
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory)))
        {
            // Reuse all activation safety gates; a proven completion never needs another dialogue.
            if (Time.unscaledTime >= nextUnlockRecoveryTime && CanBegin(introductionCompleted))
                TryCompleteInventoryUnlock();
            return;
        }

        if (!CanBegin()) return;
        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null || !storyToken.IsValid) return;
        running = true;
        closed = normalClose = focusObserved = focusSuperseded = false;
        focusCallback = null;
        try
        {
            if (!selaTrigger.StartConversationProgrammatically(morningConversation))
            {
                running = false;
                mustLeaveBeforeRetry = true;
                ReleaseStoryToken();
            }
        }
        catch (System.Exception exception)
        {
            CancelOwnedPresentation();
            Debug.LogException(exception, this);
        }
    }

    private void TryCompleteInventoryUnlock()
    {
        nextUnlockRecoveryTime = Time.unscaledTime + 1f;
        try
        {
            GameplaySystemState.SetUnlocked(GameplaySystemId.Inventory, true);
        }
        catch (System.Exception exception)
        {
            // A subscriber can throw after the centralized flag has already been committed.
            Debug.LogError("Morning Inventory unlock notification failed. Check the subscriber exception; " +
                "the unlocked flag will be checked before recording morning completion.", this);
            Debug.LogException(exception, this);
        }

        if (!GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory))
        {
            Debug.LogError("Morning dialogue completed, but Inventory remains locked. " +
                "Unlock recovery will retry when the trigger and presentation are ready.", this);
            return;
        }

        try
        {
            SessionStoryState.SetFlag(IntroductionCompleted, true);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Morning completion notification failed. Check the subscriber exception; " +
                "completed dialogue will not be replayed while completion recovery is pending.", this);
            Debug.LogException(exception, this);
        }
        completionPending = !SessionStoryState.GetFlag(IntroductionCompleted);
    }

    private bool CanBegin() => CanBegin(false);

    private bool CanBegin(bool reconcileCompleted)
    {
        if (!occupied || mustLeaveBeforeRetry || outgoingTransition ||
            SceneManager.GetActiveScene() != gameObject.scene || gameObject.scene.name != "PosadaInterior" ||
            !GameFlags.isMorning || !SessionStoryState.GetFlag(SelaCompleted) ||
            SessionStoryState.GetFlag(IntroductionCompleted) != reconcileCompleted) return false;

        // Legacy sessions must carry actual night progression AND the Journal unlock.
        // The daytime lighting override sets neither; no compatibility flags are synthesized.
        bool slept = SessionStoryState.GetFlag(SleepCompleted) ||
            (!SleepInteraction.HasObservedSleepAttempt && SessionStoryState.GetFlag(SelaCompleted) &&
             GameplaySystemState.IsUnlocked(GameplaySystemId.Journal));
        if (!slept || incomingFade == null || !incomingFade.isActiveAndEnabled ||
            !incomingFade.IncomingFadeCompleted || !string.IsNullOrEmpty(SpawnData.spawnPointName) ||
            playerMovement == null || !playerMovement.isActiveAndEnabled || Time.timeScale <= 0f ||
            morningConversation == null || selaTrigger == null || !selaTrigger.isActiveAndEnabled ||
            selaTrigger.IsTalking || focusManager == null || !focusManager.isActiveAndEnabled ||
            focusManager.IsFocusing || !selaTrigger.OwnsCameraFocus(focusManager, focusPoint) ||
            StorySequenceCoordinator.IsStorySequenceActive || ConversationManager.Instance == null ||
            !ConversationManager.Instance.isActiveAndEnabled || ConversationManager.Instance.IsConversationActive)
            return false;
        if (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered) return false;
        if (journal == null || journal.IsOpen || journal.AttentionRevealPending ||
            (inventoryUI != null && inventoryUI.IsOpen)) return false;
        var entries = JournalEntryPresentationController.Instance;
        if (entries != null && (entries.IsPresenting || entries.PendingCount > 0)) return false;
        var pause = FindAnyObjectByType<AlaalaPauseMenuController>();
        if (pause != null && pause.IsOpen) return false;
        foreach (var viewer in FindObjectsByType<ClarityDocumentViewer>())
            if (viewer.IsOpen) return false;
        return true;
    }

    private void HandleFocusStarted(CameraFocusPoint point, UnityEvent callback)
    {
        if (!running) return;
        if (!focusObserved && point == focusPoint)
        {
            focusObserved = true;
            focusCallback = callback;
        }
        else if (point != focusPoint || callback != focusCallback)
            focusSuperseded = true;
    }

    private void HandleClosed(NPCConversation conversation, bool normal)
    {
        if (!running || conversation != morningConversation) return;
        closed = true;
        normalClose = normal;
    }

    private void HandleEntranceAccepted() => outgoingTransition = true;

    private void ReleaseStoryToken()
    {
        storyToken?.Release();
        storyToken = null;
    }

    private void CancelOwnedPresentation()
    {
        if (running)
        {
            running = false;
            mustLeaveBeforeRetry = true;
            var manager = ConversationManager.Instance;
            bool ownsPendingStart = manager != null && manager.ActiveConversation == null &&
                !manager.IsConversationActive && selaTrigger != null &&
                selaTrigger.LastStartedConversation == morningConversation;
            if (manager != null && (manager.ActiveConversation == morningConversation || ownsPendingStart) &&
                !manager.IsConversationClosing)
                manager.EndConversation();
            if (focusObserved && !focusSuperseded && focusManager != null)
                focusManager.TryReturnToNormal(focusPoint);
        }
        ReleaseStoryToken();
    }

    private void OnDisable()
    {
        ConversationManager.ConversationClosed -= HandleClosed;
        if (focusManager != null) focusManager.FocusStarted -= HandleFocusStarted;
        if (sceneEntrances != null)
            foreach (var entrance in sceneEntrances)
                if (entrance != null) entrance.EntranceAccepted -= HandleEntranceAccepted;
        CancelOwnedPresentation();
        occupied = false;
    }

    private void OnDestroy() => OnDisable();
}
