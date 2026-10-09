using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the single optional gameplay-system tutorial presentation. It never changes
/// time scale, controls, cameras, interactions, or story progression.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class GameplaySystemTutorialManager : MonoBehaviour
{
    private const float JournalPostRevealDelay = 0.5f;
    private const string VisualResourcePath = "UI/GameplaySystemTutorialVisual";

    private static GameplaySystemTutorialManager instance;

    [Header("Temporary Diagnostics")]
    [SerializeField, Tooltip("Log tutorial presentation events and blockers during manual scene-transition testing. Observational only.")]
    private bool tutorialPresentationDiagnostics = false;

    // Diagnostic-only caches; never participate in tutorial decisions.
    private string lastDiagnosticSnapshot;
    private string lastDiagnosticScene;
    private string lastDiagnosticVisualState;
    private string lastDiagnosticSteps;
    private int diagnosticActivationId;

    private readonly Dictionary<GameplaySystemId, GameplaySystemTutorialDefinition> definitions =
        new Dictionary<GameplaySystemId, GameplaySystemTutorialDefinition>();
    private readonly Dictionary<GameplaySystemId, GameplaySystemTutorialAnchor> anchors =
        new Dictionary<GameplaySystemId, GameplaySystemTutorialAnchor>();
    private readonly HashSet<GameplaySystemId> presentationReady =
        new HashSet<GameplaySystemId>();

    private GameplaySystemTutorialVisual visual;
    private bool presentationFailed;
    private Canvas overlayCanvas;
    private RectTransform canvasRect;
    private CanvasGroup presentationGroup;
    private RectTransform initialCallout;
    private TMP_Text initialPointer;
    private TMP_Text initialTitle;
    private TMP_Text initialBody;
    private RectTransform journalCallout;
    private TMP_Text journalTitle;
    private TMP_Text journalBody;
    private TMP_Text journalPointer;
    private Button journalNextButton;
    private TMP_Text journalNextLabel;

    private RectTransform journalWindow;
    private readonly Button[] journalTabs = new Button[4];
    private Coroutine delayedPresentation;
    private bool hasActiveTutorial;
    private GameplaySystemId activeSystem;
    private int journalStep;
    // Inventory introduction, item details, and payment return have independent session completion.
    private int inventoryStep;
    private GameplaySystemTutorialState.InventoryPhase inventoryPhase;
    private Vector2 inventoryPointerAnchorMin, inventoryPointerAnchorMax, inventoryPointerPivot, inventoryPointerPosition;
    private Quaternion inventoryPointerRotation;
    private bool inventoryOpen;
    private int clarityStep;
    private bool unreadableDocumentOpen;
    private bool documentClarityUsed;
    private bool journalOpen;
    private bool activeSystemTemporarilyHidden;
    private bool lastStorySequenceActive;
    private bool lastEntryPresentationDeferred;
    private readonly List<SceneEntrance> inventorySceneEntrances = new List<SceneEntrance>();
    private bool inventorySceneExiting;
    private float inventoryPresentationAfter;
#if UNITY_EDITOR
    private readonly HashSet<GameplaySystemId> developmentTutorialSuppressed =
        new HashSet<GameplaySystemId>();
#endif

    public static bool HasInstance => instance != null;

    public static GameplaySystemTutorialManager Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                GameObject root = new GameObject(
                    "GameplaySystemTutorialManager",
                    typeof(RectTransform));
                instance = root.AddComponent<GameplaySystemTutorialManager>();
            }

            return instance;
        }
    }

    public bool HasActiveTutorial => hasActiveTutorial;
    public GameplaySystemId ActiveSystem => activeSystem;
    public bool IsPresentationVisible =>
        presentationGroup != null && presentationGroup.alpha > 0.01f &&
        (initialCallout.gameObject.activeSelf || journalCallout.gameObject.activeSelf);
    public int JournalStep => journalStep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameplaySystemTutorialManager ignored = Instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        RegisterJournalDefinition();
        if (!BuildOverlay())
            return;

        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        SessionStoryState.FlagChanged += HandleFlagChanged;
        ClarityManager.Activated += HandleClarityActivated;
        ClarityDocumentViewer.AnyDocumentOpened += HandleDocumentOpened;
        ClarityDocumentViewer.AnyDocumentClosed += HandleDocumentClosed;
        lastStorySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
        SceneManager.sceneLoaded += HandleInventorySceneLoaded;
        SceneManager.activeSceneChanged += HandleInventoryActiveSceneChanged;
        BindInventorySceneEntrances();
        RecoverInventoryRequest();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleInventorySceneLoaded;
        SceneManager.activeSceneChanged -= HandleInventoryActiveSceneChanged;
        UnbindInventorySceneEntrances();
        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;
        SessionStoryState.FlagChanged -= HandleFlagChanged;
        ClarityManager.Activated -= HandleClarityActivated;
        ClarityDocumentViewer.AnyDocumentOpened -= HandleDocumentOpened;
        ClarityDocumentViewer.AnyDocumentClosed -= HandleDocumentClosed;
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (presentationFailed)
            return;

        UpdateInventoryTeaching();

        bool storySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
        bool entryPresentationDeferred = ShouldDeferInitialPresentation();
        if (storySequenceActive != lastStorySequenceActive ||
            entryPresentationDeferred != lastEntryPresentationDeferred)
        {
            lastStorySequenceActive = storySequenceActive;
            lastEntryPresentationDeferred = entryPresentationDeferred;
            RefreshPresentation();
        }

        if (!storySequenceActive)
            PositionVisibleCallout();

        ObserveDiagnosticPresentationChanges();
    }

    public void RegisterAnchor(GameplaySystemTutorialAnchor anchor)
    {
        if (anchor == null)
            return;

        RemoveAnchorReferences(anchor);
        anchors[anchor.System] = anchor;
        LogTutorialDiagnostics("Anchor registered: " + anchor.System + "/" + anchor.name);
        RefreshPresentation();
    }

    public void UnregisterAnchor(GameplaySystemTutorialAnchor anchor)
    {
        if (anchor == null)
            return;

        RemoveAnchorReferences(anchor);
        LogTutorialDiagnostics("Anchor unregistered: " + anchor.System + "/" + anchor.name);

        RefreshPresentation();
    }

    public void RegisterDefinition(GameplaySystemTutorialDefinition definition)
    {
        if (definition == null)
            return;

        definitions[definition.System] = definition;
        RefreshPresentation();
    }

    public void ConfigureJournal(
        RectTransform window,
        Button observations,
        Button people,
        Button fragments,
        Button reflections)
    {
        journalWindow = window;
        journalTabs[0] = observations;
        journalTabs[1] = people;
        journalTabs[2] = fragments;
        journalTabs[3] = reflections;
        LogTutorialDiagnostics("ConfigureJournal: window=" + (window != null ? window.name : "missing"));
        RefreshPresentation();
    }

    /// <summary>Called by a system's existing reveal at its real completion point.</summary>
    public void NotifySystemRevealCompleted(GameplaySystemId system)
    {
        presentationReady.Add(system);
        LogTutorialDiagnostics("Reveal completed: " + system);
        if (system == GameplaySystemId.Inventory)
        {
            GameplaySystemTutorialState.InventoryRevealReady = true;
            QueueInventoryTeaching();
            return;
        }
        if (IsDevelopmentTutorialSuppressed(system))
            return;

        if (!hasActiveTutorial && GameplaySystemState.IsUnlocked(system) &&
            !GameplaySystemTutorialState.IsSeen(system))
        {
            Activate(system);
        }

        if (!hasActiveTutorial || activeSystem != system)
            return;

        StartDelayedPresentation(system);
    }

    public void NotifyJournalOpened()
    {
        journalOpen = true;
        if (!hasActiveTutorial || activeSystem != GameplaySystemId.Journal ||
            GameplaySystemTutorialState.IsSeen(GameplaySystemId.Journal))
            return;

        CancelDelay();
        if (journalStep == 0)
            journalStep = 1;

        int tabIndex = journalStep - 2;
        if (tabIndex >= 0 && tabIndex < journalTabs.Length && journalTabs[tabIndex] != null)
            journalTabs[tabIndex].onClick.Invoke();
        RefreshPresentation();
    }

    public void NotifyJournalClosed()
    {
        journalOpen = false;
        if (hasActiveTutorial && activeSystem == GameplaySystemId.Journal &&
            !GameplaySystemTutorialState.IsSeen(GameplaySystemId.Journal))
        {
            RefreshPresentation();
        }
    }

    public void NotifySystemOpened(GameplaySystemId system)
    {
        if (!hasActiveTutorial || activeSystem != system ||
            (system == GameplaySystemId.Inventory ? IsActiveInventoryPhaseSeen() : GameplaySystemTutorialState.IsSeen(system)))
            return;

        if (system == GameplaySystemId.Inventory)
        {
            inventoryOpen = true;
            CancelDelay();
            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction && inventoryStep == 0)
                inventoryStep = 1;
            else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn)
                inventoryStep = 3; // Opening the notification, or resuming the former final step.
            SaveInventoryStep();
            RestoreInventoryGuidedTab();
            RefreshPresentation();
            return;
        }

        activeSystemTemporarilyHidden = true;
        HideAll();
        LogTutorialDiagnostics("Hidden: active system opened");
    }

    public void NotifySystemClosed(GameplaySystemId system)
    {
        if (!hasActiveTutorial || activeSystem != system)
            return;

        if (system == GameplaySystemId.Inventory)
        {
            inventoryOpen = false;
            RefreshPresentation();
            return;
        }

        activeSystemTemporarilyHidden = false;
        RefreshPresentation();
    }

    /// <summary>Called only after the payment item was successfully removed.</summary>
    public void NotifyInventoryPaymentReturned()
    {
        if (IsDevelopmentTutorialSuppressed(GameplaySystemId.Inventory))
            return;

        if (!GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory) ||
            GameplaySystemTutorialState.IsSeen(GameplaySystemId.Inventory))
            return;

        QueueInventoryTeaching(true);
    }

    public void CompleteActiveTutorial()
    {
        if (!hasActiveTutorial)
            return;

        if (activeSystem == GameplaySystemId.Inventory)
        {
            AdvanceActiveTutorial();
            return;
        }

        GameplaySystemTutorialState.SetSeen(activeSystem, true);
    }

#if UNITY_EDITOR
    /// <summary>Allow direct-scene UI testing without consuming the story tutorial.</summary>
    public void SuppressTutorialForDevelopment(GameplaySystemId system)
    {
        developmentTutorialSuppressed.Add(system);
        if (hasActiveTutorial && activeSystem == system)
            ClearActive();
    }

    public void RequestDevelopmentTutorial(
        GameplaySystemId system,
        GameplaySystemTutorialDefinition definition = null)
    {
        if (definition != null)
            RegisterDefinition(definition);
        presentationReady.Add(system);
        if (system == GameplaySystemId.Inventory)
        {
            GameplaySystemTutorialState.InventoryRevealReady = true;
            QueueInventoryTeaching();
            return;
        }
        Activate(system);
        RefreshPresentation();
    }
#endif

    private void RegisterJournalDefinition()
    {
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Journal,
            "TALA-ARAWAN",
            "Gamitin ang tala-arawan upang itala ang mahahalagang napansin, tao, bahagi ng sulatin, at pagninilay.",
            JournalPostRevealDelay,
            false));
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Inventory,
            "Imbentaryo",
            "Dito inilalagay ang mahahalagang gamit na natatanggap ni Miguel habang nag-iimbestiga.",
            JournalPostRevealDelay,
            false));
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Clarity,
            "Pagsusuri",
            "Pindutin at hawakan ang C para makita ang mahahalagang detalyeng mahirap mapansin.",
            JournalPostRevealDelay,
            false));
    }

    private void HandleSystemUnlockChanged(GameplaySystemId system, bool unlocked)
    {
        if (!unlocked)
        {
            if (hasActiveTutorial && activeSystem == system)
                ClearActive();
            return;
        }

        if (IsDevelopmentTutorialSuppressed(system))
            return;

        if (system == GameplaySystemId.Inventory)
        {
            RecoverInventoryRequest();
            return;
        }

        if (!GameplaySystemTutorialState.IsSeen(system))
            Activate(system);

        if (presentationReady.Contains(system))
            StartDelayedPresentation(system);
        else
            RefreshPresentation();
    }

    private void HandleFlagChanged(string flagId, bool value)
    {
        if (hasActiveTutorial && activeSystem == GameplaySystemId.Inventory)
        {
            if (IsActiveInventoryPhaseSeen()) ClearActive();
            return;
        }
        string seenFlagId = activeSystem == GameplaySystemId.Clarity && clarityStep == 1
            ? GameplaySystemTutorialState.ClarityDocumentSeenFlagId
            : GameplaySystemTutorialState.GetSeenFlagId(activeSystem);
        if (!hasActiveTutorial || !value ||
            flagId != seenFlagId)
            return;

        LogTutorialDiagnostics("Completed/seen flag set: " + flagId);
        ClearActive();
    }

    private void HandleClarityActivated(bool documentMode)
    {
        if (documentMode)
        {
            documentClarityUsed = true;
            if (!GameplaySystemTutorialState.ClarityDocumentSeen)
                GameplaySystemTutorialState.SetClarityDocumentSeen(true);

            if (hasActiveTutorial && activeSystem == GameplaySystemId.Clarity && clarityStep == 1)
                ClearActive();
            return;
        }

        if (GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity) &&
            !GameplaySystemTutorialState.IsSeen(GameplaySystemId.Clarity))
        {
            GameplaySystemTutorialState.SetSeen(GameplaySystemId.Clarity, true);
        }
    }

    private void HandleDocumentOpened(string documentId, bool unreadable)
    {
        unreadableDocumentOpen = unreadable;
        documentClarityUsed = false;
        if (!unreadable || GameplaySystemTutorialState.ClarityDocumentSeen)
            return;

        if (hasActiveTutorial && activeSystem == GameplaySystemId.Clarity && clarityStep == 1)
        {
            activeSystemTemporarilyHidden = true;
            HideAll();
            LogTutorialDiagnostics("Hidden: unreadable document opened");
        }
    }

    private void HandleDocumentClosed(string documentId, bool unreadable)
    {
        bool shouldHint = unreadableDocumentOpen && unreadable && !documentClarityUsed &&
            !GameplaySystemTutorialState.ClarityDocumentSeen &&
            GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity);
        unreadableDocumentOpen = false;
        documentClarityUsed = false;

        if (!shouldHint)
            return;

        clarityStep = 1;
        if (!hasActiveTutorial || activeSystem != GameplaySystemId.Clarity)
            Activate(GameplaySystemId.Clarity);
        else
        {
            activeSystemTemporarilyHidden = false;
            RefreshPresentation();
        }
    }

    private static bool IsJournalTeachingUnfinished()
    {
        return GameplaySystemState.IsUnlocked(GameplaySystemId.Journal) &&
            !GameplaySystemTutorialState.IsSeen(GameplaySystemId.Journal);
    }

    private void QueueInventoryTeaching(bool paymentReturned = false)
    {
        if (IsDevelopmentTutorialSuppressed(GameplaySystemId.Inventory) ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory)) return;
        GameplaySystemTutorialState.RequestInventoryTeaching(paymentReturned);
        // LateUpdate handles the handoff after the caller's conversation/flag callbacks finish.
    }

    private void RecoverInventoryRequest()
    {
        if (GameplaySystemTutorialState.InventoryRevealReady)
            presentationReady.Add(GameplaySystemId.Inventory);
        QueueInventoryTeaching(SessionStoryState.GetFlag("aling_ika_completed"));
    }

    public void NotifyInventoryItemsChanged() => QueueInventoryTeaching();

    private bool IsActiveInventoryPhaseSeen() => GameplaySystemTutorialState.IsInventoryPhaseSeen(inventoryPhase);

    private void SaveInventoryStep()
    {
        if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
            GameplaySystemTutorialState.InventoryIntroStep = inventoryStep;
        else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn)
            GameplaySystemTutorialState.InventoryPendingStep = inventoryStep;
    }

    private InventoryUI GetInventoryTutorialUI()
    {
        foreach (var ui in FindObjectsByType<InventoryUI>())
            if (ui.IsTutorialUIAvailable) return ui;
        return null;
    }

    private void RestoreInventoryGuidedTab()
    {
        if (inventoryPhase != GameplaySystemTutorialState.InventoryPhase.Introduction || inventoryStep < 2) return;
        var ui = GetInventoryTutorialUI();
        if (ui != null) ui.SelectTutorialTab(inventoryStep - 2);
    }

    private bool IsInventoryGuided => inventoryOpen &&
        (inventoryPhase != GameplaySystemTutorialState.InventoryPhase.PaymentReturn || inventoryStep >= 3) &&
        (inventoryPhase != GameplaySystemTutorialState.InventoryPhase.Introduction || inventoryStep > 0);

    private bool TryGetInventoryGuidedTarget(out RectTransform target)
    {
        target = null;
        var ui = GetInventoryTutorialUI();
        if (ui == null || !ui.IsOpen || ui.IsWindowTransitioning) return false;
        if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails)
            return ui.TryGetTutorialItemTarget(out target);
        if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
        {
            if (inventoryStep >= 2)
            {
                ui.SelectTutorialTab(inventoryStep - 2);
                target = ui.GetTutorialTabTarget(inventoryStep - 2);
            }
            else target = ui.TutorialWindow;
        }
        else
        {
            GameplaySystemTutorialAnchor anchor;
            if (anchors.TryGetValue(GameplaySystemId.Inventory, out anchor) && anchor != null)
                target = anchor.Target;
        }
        return target != null && target.gameObject.activeInHierarchy;
    }

    private void UpdateInventoryTeaching()
    {
        if (IsDevelopmentTutorialSuppressed(GameplaySystemId.Inventory)) return;
        bool pending = GameplaySystemTutorialState.InventoryTeachingPending &&
            GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory);
        if (hasActiveTutorial && activeSystem == GameplaySystemId.Inventory)
        {
            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn &&
                inventoryStep > 3 && !IsActiveInventoryPhaseSeen())
            {
                inventoryStep = 3;
                SaveInventoryStep();
            }
            var activeUI = GetInventoryTutorialUI();
            if (IsActiveInventoryPhaseSeen() || !pending || IsJournalTeachingUnfinished() ||
                (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails &&
                 (activeUI == null || !activeUI.HasTutorialItem)) ||
                !IsInventoryPresentationEnvironmentSafe(true))
                ClearActive(); // The session request and saved step remain intact.
            else
                RefreshPresentation();
            return;
        }

        if (!pending || hasActiveTutorial || IsJournalTeachingUnfinished() ||
            !GameplaySystemTutorialState.InventoryRevealReady ||
            !IsInventoryPresentationEnvironmentSafe()) return;

        var ui = GetInventoryTutorialUI();
        if (!GameplaySystemTutorialState.TryGetNextInventoryPhase(ui != null && ui.HasTutorialItem, out inventoryPhase)) return;
        if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
        {
            inventoryStep = Mathf.Clamp(GameplaySystemTutorialState.InventoryIntroStep, 0, 5);
        }
        else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn)
        {
            inventoryStep = Mathf.Clamp(GameplaySystemTutorialState.InventoryPendingStep, 2, 3);
            SaveInventoryStep(); // Canonicalize saved progress from the removed step without completing it.
        }
        else
        {
            inventoryStep = 1;
        }
        inventoryOpen = false;
        presentationReady.Add(GameplaySystemId.Inventory);
        inventoryPresentationAfter = Time.unscaledTime + JournalPostRevealDelay;
        Activate(GameplaySystemId.Inventory);
    }

    // Also used by InventoryUI to retry its own reveal without changing unlock/access state.
    public bool IsInventoryPresentationEnvironmentSafe(bool allowInventoryOpen = false)
    {
        if (!isActiveAndEnabled || presentationFailed || visual == null ||
            !visual.gameObject.activeInHierarchy || overlayCanvas == null || !overlayCanvas.enabled ||
            presentationGroup == null || !presentationGroup.isActiveAndEnabled ||
            initialCallout == null || journalCallout == null || journalNextButton == null ||
            inventorySceneExiting || !SceneManager.GetActiveScene().isLoaded || Time.timeScale <= 0f ||
            !string.IsNullOrEmpty(SpawnData.spawnPointName) || StorySequenceCoordinator.IsStorySequenceActive)
            return false;
        if (DialogueEditor.ConversationManager.Instance != null &&
            DialogueEditor.ConversationManager.Instance.IsConversationActive) return false;
        if (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered) return false;
        var journal = ReconstructionJournalManager.Instance;
        if (journal != null && (journal.IsOpen || journal.AttentionRevealPending)) return false;
        var entries = JournalEntryPresentationController.Instance;
        if (entries != null && (entries.PendingCount > 0 || entries.IsPresenting)) return false;
        foreach (var focus in FindObjectsByType<CameraFocusManager>())
            if (focus.IsFocusing) return false;
        foreach (var fade in FindObjectsByType<FadeController>())
            if (fade.isActiveAndEnabled && !fade.IncomingFadeCompleted) return false;
        foreach (var pause in FindObjectsByType<AlaalaPauseMenuController>())
            if (pause.IsOpen) return false;
        foreach (var document in FindObjectsByType<ClarityDocumentViewer>())
            if (document.IsOpen) return false;

        bool inventoryAvailable = false;
        foreach (var ui in FindObjectsByType<InventoryUI>())
        {
            if ((ui.IsOpen || ui.IsWindowTransitioning) && !allowInventoryOpen) return false;
            inventoryAvailable |= ui.IsTutorialUIAvailable;
        }
        if (!inventoryAvailable || !definitions.ContainsKey(GameplaySystemId.Inventory)) return false;
        GameplaySystemTutorialAnchor anchor;
        if (!anchors.TryGetValue(GameplaySystemId.Inventory, out anchor) || anchor == null ||
            !anchor.isActiveAndEnabled || anchor.Target == null || !anchor.Target.gameObject.activeInHierarchy)
            return false;
        var hudTarget = anchor.GetComponent<GameplayHUDTarget>();
        if (hudTarget != null && !hudTarget.IsAvailable) return false;
        var button = anchor.GetComponent<Selectable>();
        if (button != null && (!button.enabled || !button.IsInteractable())) return false;
        foreach (var canvas in anchor.Target.GetComponentsInParent<Canvas>())
            if (!canvas.enabled) return false;

        // Matches the existing Journal entry transition check, including iris closing before IsCovered.
        foreach (var canvas in FindObjectsByType<Canvas>())
        {
            if (!canvas.enabled || canvas.sortingOrder < 32100) continue;
            var groups = canvas.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var group in groups)
                if (group.gameObject.activeInHierarchy && group.alpha > 0.01f) return false;
            if (groups.Length != 0) continue;
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject.activeInHierarchy && graphic.enabled && graphic.color.a > 0.01f)
                    return false;
        }
        return true;
    }

    private void HandleInventorySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        inventorySceneExiting = false;
        BindInventorySceneEntrances();
        RecoverInventoryRequest();
    }

    private void HandleInventoryActiveSceneChanged(Scene previous, Scene next)
    {
        inventorySceneExiting = false;
        BindInventorySceneEntrances();
    }

    private void BindInventorySceneEntrances()
    {
        UnbindInventorySceneEntrances();
        foreach (var entrance in FindObjectsByType<SceneEntrance>(FindObjectsInactive.Include))
        {
            inventorySceneEntrances.Add(entrance);
            entrance.EntranceAccepted += HandleInventoryEntranceAccepted;
        }
    }

    private void UnbindInventorySceneEntrances()
    {
        foreach (var entrance in inventorySceneEntrances)
            if (entrance != null) entrance.EntranceAccepted -= HandleInventoryEntranceAccepted;
        inventorySceneEntrances.Clear();
    }

    private void HandleInventoryEntranceAccepted() => inventorySceneExiting = true;

    private void OnDisable()
    {
        if (hasActiveTutorial && activeSystem == GameplaySystemId.Inventory) ClearActive();
    }

    private void Activate(GameplaySystemId system)
    {
        // Inventory has its own deferred entry point; never let a direct request steal the slot.
        if (system == GameplaySystemId.Inventory &&
            (hasActiveTutorial || IsJournalTeachingUnfinished() ||
             !IsInventoryPresentationEnvironmentSafe())) return;
        if (presentationFailed)
            return;

        if (IsDevelopmentTutorialSuppressed(system))
            return;

        CancelDelay();
        hasActiveTutorial = true;
        activeSystem = system;
        activeSystemTemporarilyHidden = false;
        journalOpen = system == GameplaySystemId.Journal &&
            ReconstructionJournalManager.Instance != null &&
            ReconstructionJournalManager.Instance.IsOpen;
        if (tutorialPresentationDiagnostics)
            diagnosticActivationId++;
        LogTutorialDiagnostics("ACTIVATED: Activate invoked for " + system);
        RefreshPresentation();
    }

    /// <summary>Discards prior playthrough presentation without changing flags or live subscriptions.</summary>
    public void ResetForNewGame()
    {
        StopAllCoroutines();
        delayedPresentation = null;
        ClearActive();
        inventoryPhase = default;
        presentationReady.Clear();
        anchors.Clear();
        journalWindow = null;
        System.Array.Clear(journalTabs, 0, journalTabs.Length);
        UnbindInventorySceneEntrances();
        inventorySceneExiting = false;
        inventoryPresentationAfter = 0f;
        lastStorySequenceActive = false;
        lastEntryPresentationDeferred = false;
        lastDiagnosticSnapshot = lastDiagnosticScene = lastDiagnosticVisualState = lastDiagnosticSteps = null;
        diagnosticActivationId = 0;
#if UNITY_EDITOR
        developmentTutorialSuppressed.Clear();
#endif
        if (journalPointer != null)
        {
            var pointer = journalPointer.rectTransform;
            pointer.anchorMin = inventoryPointerAnchorMin;
            pointer.anchorMax = inventoryPointerAnchorMax;
            pointer.pivot = inventoryPointerPivot;
            pointer.anchoredPosition = inventoryPointerPosition;
            pointer.localRotation = inventoryPointerRotation;
        }
    }

    private void ClearActive()
    {
        LogTutorialDiagnostics("ClearActive requested");
        CancelDelay();
        hasActiveTutorial = false;
        journalStep = 0;
        inventoryStep = 0;
        inventoryOpen = false;
        clarityStep = 0;
        unreadableDocumentOpen = false;
        documentClarityUsed = false;
        journalOpen = false;
        activeSystemTemporarilyHidden = false;
        HideAll();
        LogTutorialDiagnostics("Active tutorial cleared");
    }

    private void StartDelayedPresentation(GameplaySystemId system)
    {
        CancelDelay();
        GameplaySystemTutorialDefinition definition;
        float delay = definitions.TryGetValue(system, out definition)
            ? definition.PostRevealDelay
            : 0f;
        delayedPresentation = StartCoroutine(ShowAfterDelay(system, delay));
        LogTutorialDiagnostics("Presentation delay began: " + system + ", seconds=" + delay);
    }

    private IEnumerator ShowAfterDelay(GameplaySystemId system, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (!hasActiveTutorial || activeSystem != system)
                yield break;

            if (!StorySequenceCoordinator.IsStorySequenceActive)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        delayedPresentation = null;
        LogTutorialDiagnostics("Presentation delay finished: " + system);
        RefreshPresentation();
    }

    private void CancelDelay()
    {
        if (delayedPresentation != null)
        {
            LogTutorialDiagnostics("Presentation delay cancelled");
            StopCoroutine(delayedPresentation);
        }
        delayedPresentation = null;
    }

    private bool ShouldDeferInitialPresentation()
    {
        // Guided interfaces and the document-specific instruction keep their
        // existing ownership. Only initial/HUD callouts yield to queued entries.
        if (!hasActiveTutorial ||
            (activeSystem == GameplaySystemId.Journal && journalOpen && journalStep > 0) ||
            (activeSystem == GameplaySystemId.Inventory && IsInventoryGuided) ||
            (activeSystem == GameplaySystemId.Clarity && clarityStep == 1))
            return false;

        var entries = JournalEntryPresentationController.Instance;
        return entries != null && (entries.PendingCount > 0 || entries.IsPresenting);
    }

    private string GetDiagnosticVisualState()
    {
        return (initialCallout != null && initialCallout.gameObject.activeSelf) + "/" +
            (journalCallout != null && journalCallout.gameObject.activeSelf) + "/" +
            (presentationGroup != null && presentationGroup.alpha > 0.01f) + "/" +
            (presentationGroup != null && presentationGroup.interactable) + "/" +
            (presentationGroup != null && presentationGroup.blocksRaycasts);
    }

    private void ObserveDiagnosticPresentationChanges()
    {
        if (!tutorialPresentationDiagnostics)
            return;

        // Observe scene changes and discrete visual changes, never each fade frame.
        string scene = SceneManager.GetActiveScene().name;
        string visualState = GetDiagnosticVisualState();
        if (scene != lastDiagnosticScene || visualState != lastDiagnosticVisualState)
            LogTutorialDiagnostics("Observed scene/visual state change");
    }

    private void LogTutorialDiagnostics(string eventName)
    {
        if (!tutorialPresentationDiagnostics)
            return;

        string scene = SceneManager.GetActiveScene().name;
        string steps = activeSystem + "/" + journalStep + "/" + inventoryStep + "/" + clarityStep;
        string changes = scene != lastDiagnosticScene ? " SCENE_CHANGED" : "";
        if (lastDiagnosticSteps != null && steps != lastDiagnosticSteps)
            changes += " SYSTEM_OR_STEP_CHANGED";

        GameplaySystemTutorialAnchor anchor;
        bool registered = anchors.TryGetValue(activeSystem, out anchor);
        RectTransform target = anchor != null ? anchor.Target : null;
        var entries = JournalEntryPresentationController.Instance;
        int entryPending = entries != null ? entries.PendingCount : 0;
        bool entryPresenting = entries != null && entries.IsPresenting;
        bool storyActive = StorySequenceCoordinator.IsStorySequenceActive;
        bool documentInstruction = activeSystem == GameplaySystemId.Clarity && clarityStep == 1;
        bool seen = documentInstruction
            ? GameplaySystemTutorialState.ClarityDocumentSeen
            : activeSystem == GameplaySystemId.Inventory ? IsActiveInventoryPhaseSeen()
            : GameplaySystemTutorialState.IsSeen(activeSystem);
        bool guided = (activeSystem == GameplaySystemId.Journal && journalOpen && journalStep > 0) ||
            (activeSystem == GameplaySystemId.Inventory && IsInventoryGuided);
        bool entryDeferred = hasActiveTutorial && !guided && !documentInstruction &&
            (entryPending > 0 || entryPresenting);

        // Mirror actual RefreshPresentation gates for reporting only.
        // Inactive targets are observations, not a gate in the current manager.
        var blockers = new List<string>();
        if (presentationFailed) blockers.Add("presentation failed");
        if (!hasActiveTutorial) blockers.Add("no active tutorial");
        if (storyActive) blockers.Add("story sequence active");
        if (seen) blockers.Add("tutorial already completed/seen");
        if (!presentationReady.Contains(activeSystem)) blockers.Add("presentation not ready");
        if (delayedPresentation != null) blockers.Add("presentation delay pending");
        if (activeSystemTemporarilyHidden) blockers.Add("active system temporarily hidden");
        if (!guided && !documentInstruction)
        {
            if (entryDeferred) blockers.Add("Journal entry presentation pending/presenting");
            if (!definitions.ContainsKey(activeSystem)) blockers.Add("tutorial definition missing");
            if (!registered || anchor == null) blockers.Add("required tutorial anchor missing");
            else if (target == null) blockers.Add("target RectTransform missing");
        }

        string snapshot = "scene=" + scene + ", manager=" + GetInstanceID() +
            ", activation=" + diagnosticActivationId + ", active=" + hasActiveTutorial +
            ", system=" + activeSystem + ", steps[J/I/C]=" + journalStep + "/" + inventoryStep + "/" + clarityStep +
            ", ready=" + presentationReady.Contains(activeSystem) + ", delay=" + (delayedPresentation != null) +
            ", temporaryHidden=" + activeSystemTemporarilyHidden + ", failed=" + presentationFailed +
            ", anchorRegistered=" + registered + ", anchor=" + (anchor != null ? anchor.name : "missing") +
            ", target=" + (target != null ? target.name : "missing") +
            ", targetActive=" + (target != null && target.gameObject.activeInHierarchy) +
            ", story=" + storyActive + ", storyOwners=" + StorySequenceCoordinator.ActiveOwnerCount +
            ", entryPending=" + entryPending + ", entryPresenting=" + entryPresenting +
            ", entryDeferred=" + entryDeferred + ", seen=" + seen +
            ", seenFlag=" + (documentInstruction ? GameplaySystemTutorialState.ClarityDocumentSeenFlagId :
                GameplaySystemTutorialState.GetSeenFlagId(activeSystem)) +
            ", inventoryFirstItemSeen=" + GameplaySystemTutorialState.InventoryFirstItemSeen +
            ", windows[J/I]=" + journalOpen + "/" + inventoryOpen +
            ", panels[system/guided]=" + (initialCallout != null && initialCallout.gameObject.activeSelf) + "/" +
                (journalCallout != null && journalCallout.gameObject.activeSelf) +
            ", alpha=" + (presentationGroup != null ? presentationGroup.alpha.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture) : "missing") +
            ", interactable=" + (presentationGroup != null && presentationGroup.interactable) +
            ", blocksRaycasts=" + (presentationGroup != null && presentationGroup.blocksRaycasts) +
            ", " + (blockers.Count > 0 ? "BLOCKED: " + string.Join("; ", blockers) : "refresh gates clear");

        string key = eventName + " | " + snapshot;
        bool duplicate = key == lastDiagnosticSnapshot;
        lastDiagnosticSnapshot = key;
        lastDiagnosticScene = scene;
        lastDiagnosticSteps = steps;
        lastDiagnosticVisualState = GetDiagnosticVisualState();
        if (!duplicate)
            Debug.Log("[TutorialDiagnostics] " + eventName + changes + " | " + snapshot, this);
    }

    private void RefreshPresentation()
    {
        if (presentationFailed)
        {
            LogTutorialDiagnostics("Refresh BLOCKED");
            ClearActive();
            return;
        }

        HideAll();
        if (hasActiveTutorial && activeSystem == GameplaySystemId.Inventory &&
            (IsJournalTeachingUnfinished() || !IsInventoryPresentationEnvironmentSafe(true) ||
             Time.unscaledTime < inventoryPresentationAfter)) return;
        bool activeTutorialSeen = activeSystem == GameplaySystemId.Clarity && clarityStep == 1
            ? GameplaySystemTutorialState.ClarityDocumentSeen
            : activeSystem == GameplaySystemId.Inventory ? IsActiveInventoryPhaseSeen()
            : GameplaySystemTutorialState.IsSeen(activeSystem);
        if (!hasActiveTutorial || StorySequenceCoordinator.IsStorySequenceActive ||
            activeTutorialSeen ||
            !presentationReady.Contains(activeSystem) || delayedPresentation != null ||
            activeSystemTemporarilyHidden)
        {
            LogTutorialDiagnostics("Refresh BLOCKED");
            return;
        }

        if (activeSystem == GameplaySystemId.Journal && journalOpen && journalStep > 0)
        {
            ShowJournalStep();
            LogTutorialDiagnostics("Refresh SHOWN: Journal guided");
            return;
        }

        if (activeSystem == GameplaySystemId.Inventory)
        {
            if (IsInventoryGuided)
            {
                if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
                    ShowInventoryIntroductionStep();
                else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails)
                    ShowInventoryStep("Mga Detalye ng Gamit", "Pumili ng gamit para makita ang paglalarawan nito.");
                else if (inventoryStep == 3)
                    ShowInventoryStep("Mga Gamit sa Gawain", "Maaaring alisin ang mga gamit sa gawain kapag hindi na kailangan.");
                LogTutorialDiagnostics("Refresh SHOWN: Inventory guided");
                return;
            }
            // Closing a guided lesson hides it without replaying the completed HUD introduction.
            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails ||
                (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction && inventoryStep > 0) ||
                (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn && inventoryStep > 2)) return;
        }

        if (activeSystem == GameplaySystemId.Clarity && clarityStep == 1)
        {
            initialTitle.text = "Pagsusuri sa mga Kasulatan";
            initialBody.text =
                "Pindutin at hawakan ang C habang sinusuri ang mga hindi mabasang kasulatan para makita ang mga nakatagong detalye.";
            initialPointer.gameObject.SetActive(false);
            initialCallout.gameObject.SetActive(true);
            presentationGroup.alpha = 1f;
            LogTutorialDiagnostics("Refresh SHOWN: Pagsusuri document");
            return;
        }

        GameplaySystemTutorialDefinition definition;
        GameplaySystemTutorialAnchor anchor;
        if (ShouldDeferInitialPresentation())
        {
            LogTutorialDiagnostics("Refresh BLOCKED");
            return;
        }
        if (!definitions.TryGetValue(activeSystem, out definition) ||
            !anchors.TryGetValue(activeSystem, out anchor) || anchor == null ||
            anchor.Target == null)
        {
            LogTutorialDiagnostics("Refresh BLOCKED");
            return;
        }

        initialTitle.text = definition.Title;
        initialBody.text = definition.Description;
        if (activeSystem == GameplaySystemId.Inventory &&
            inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn && inventoryStep == 2)
        {
            initialTitle.text = "May Bago sa Imbentaryo";
            initialBody.text = "Buksan ang Imbentaryo para makita kung ano ang nagbago.";
        }
        initialPointer.gameObject.SetActive(definition.ShowInitialPointer);
        initialCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;
        LogTutorialDiagnostics("Refresh SHOWN: System Callout");
    }

    private void ShowJournalStep()
    {
        RectTransform target = journalWindow;
        journalTitle.text = "TALA-ARAWAN";

        switch (journalStep)
        {
            case 1:
                journalBody.text =
                    "Itinatala rito ang mahahalagang natutuklasan ni Miguel habang nagsasaliksik.";
                journalNextLabel.text = "Susunod";
                break;
            case 2:
                target = GetTabTarget(0);
                journalTitle.text = "MGA NAPANSIN";
                journalBody.text =
                    "Mahahalagang natuklasan at detalyeng napapansin ni Miguel sa pagsasaliksik.";
                journalNextLabel.text = "Susunod";
                break;
            case 3:
                target = GetTabTarget(1);
                journalTitle.text = "MGA TAO";
                journalBody.text =
                    "Impormasyon tungkol sa mga taong nakikilala ni Miguel at mga natututuhan niya tungkol sa kanila.";
                journalNextLabel.text = "Susunod";
                break;
            case 4:
                target = GetTabTarget(2);
                journalTitle.text = "MGA BAHAGI";
                journalBody.text =
                    "Mga dokumento, pahiwatig, at impormasyong kaugnay ng Makamisa.";
                journalNextLabel.text = "Susunod";
                break;
            default:
                target = GetTabTarget(3);
                journalTitle.text = "MGA PAGNINILAY";
                journalBody.text =
                    "Mga tanong na tumutulong kay Miguel na magnilay sa kanyang mga natutuhan.";
                journalNextLabel.text = "Sige";
                break;
        }

        journalCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;
        PositionCallout(journalCallout, target, journalStep == 1 ? visual.JournalOverviewOffset : visual.JournalGuidedOffset);
    }

    private void ShowInventoryIntroductionStep()
    {
        switch (inventoryStep)
        {
            case 1:
                ShowInventoryStep("Imbentaryo", "Dito makikita ang mga nakolektang gamit ni Miguel.");
                break;
            case 2:
                ShowInventoryStep("Lahat", "Dito makikita ang lahat ng gamit na nakuha ni Miguel.");
                break;
            case 3:
                ShowInventoryStep("Mga Gamit sa Gawain", "Dito makikita ang mga gamit na kailangan ni Miguel sa kaniyang mga gawain.");
                break;
            case 4:
                ShowInventoryStep("Mahahalagang Gamit", "Dito makikita ang mahahalagang gamit na nakuha ni Miguel habang naglalakbay.");
                break;
            case 5:
                ShowInventoryStep("Mga Kasulatan", "Dito makikita ang mga kasulatan at iba pang nakasulat na bagay na nakolekta ni Miguel.");
                break;
        }
    }

    private void ShowInventoryStep(string title, string body)
    {
        RectTransform target;
        if (!TryGetInventoryGuidedTarget(out target)) return;
        journalTitle.text = title;
        journalBody.text = body;
        journalNextLabel.text = inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction && inventoryStep == 5
            ? "Sige" : inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn && inventoryStep == 3
            ? "Naintindihan ko" : "Susunod";
        bool itemDetailsStep = inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails;

        if (journalPointer != null)
        {
            journalPointer.gameObject.SetActive(itemDetailsStep ||
                (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction && inventoryStep >= 2));
            if (!itemDetailsStep)
            {
                var pointer = journalPointer.rectTransform;
                pointer.anchorMin = inventoryPointerAnchorMin;
                pointer.anchorMax = inventoryPointerAnchorMax;
                pointer.pivot = inventoryPointerPivot;
                pointer.anchoredPosition = inventoryPointerPosition;
                pointer.localRotation = inventoryPointerRotation;
            }
        }
        journalCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;

        if (target != null)
        {
            if (itemDetailsStep)
                PositionInventoryItemDetailsCallout(journalCallout, target);
            else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
                PositionCallout(journalCallout, target, inventoryStep == 1 ? visual.JournalOverviewOffset : visual.JournalGuidedOffset);
            else
                PositionCallout(journalCallout, target, visual.InventoryGeneralOffset);
        }
    }

    private RectTransform GetTabTarget(int index)
    {
        return journalTabs[index] != null
            ? journalTabs[index].transform as RectTransform
            : journalWindow;
    }

    private void AdvanceJournalTutorial()
    {
        if (!hasActiveTutorial || activeSystem != GameplaySystemId.Journal || !journalOpen)
            return;

        if (journalStep >= 5)
        {
            GameplaySystemTutorialState.SetSeen(GameplaySystemId.Journal, true);
            return;
        }

        journalStep++;
        int tabIndex = journalStep - 2;
        if (tabIndex >= 0 && tabIndex < journalTabs.Length && journalTabs[tabIndex] != null)
            journalTabs[tabIndex].onClick.Invoke();

        RefreshPresentation();
    }

    private void AdvanceActiveTutorial()
    {
        if (activeSystem == GameplaySystemId.Inventory)
        {
            if (!hasActiveTutorial || !inventoryOpen || IsJournalTeachingUnfinished() ||
                !IsInventoryPresentationEnvironmentSafe(true) ||
                Time.unscaledTime < inventoryPresentationAfter)
                return;

            RectTransform target;
            if (IsActiveInventoryPhaseSeen() || !TryGetInventoryGuidedTarget(out target)) return;

            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
            {
                if (inventoryStep < 1 || inventoryStep > 5) return;
                if (inventoryStep == 5)
                {
                    GameplaySystemTutorialState.CompleteInventoryIntroduction();
                    ClearActive();
                }
                else
                {
                    inventoryStep++;
                    SaveInventoryStep();
                    RestoreInventoryGuidedTab();
                    RefreshPresentation();
                }
                return;
            }

            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails)
            {
                GameplaySystemTutorialState.SetInventoryFirstItemSeen(true);
                LogTutorialDiagnostics("Inventory first-item teaching completed");
                ClearActive();
                LogTutorialDiagnostics("Inventory first-item presentation ended");
                return;
            }

            if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.PaymentReturn && inventoryStep == 3)
            {
                GameplaySystemTutorialState.SetSeen(GameplaySystemId.Inventory, true);
                return;
            }

            return;
        }

        AdvanceJournalTutorial();
    }

    private void PositionVisibleCallout()
    {
        if (initialCallout.gameObject.activeSelf)
        {
            GameplaySystemTutorialAnchor anchor;
            if (anchors.TryGetValue(activeSystem, out anchor) && anchor != null)
            {
                Vector2 offset;
                if (activeSystem == GameplaySystemId.Inventory)
                    offset = visual.InventoryInitialTargetOffset;
                else if (activeSystem == GameplaySystemId.Clarity)
                    offset = clarityStep == 1
                        ? visual.PagsusuriDocumentOffset
                        : visual.PagsusuriInitialTargetOffset;
                else
                    offset = visual.JournalInitialTargetOffset;
                PositionCallout(initialCallout, anchor.Target, offset);
            }
        }
        else if (journalCallout.gameObject.activeSelf)
        {
            if (activeSystem == GameplaySystemId.Inventory)
            {
                RectTransform target;
                if (!TryGetInventoryGuidedTarget(out target))
                {
                    HideAll();
                    return;
                }

                if (target != null)
                {
                    if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.ItemDetails)
                        PositionInventoryItemDetailsCallout(journalCallout, target);
                    else if (inventoryPhase == GameplaySystemTutorialState.InventoryPhase.Introduction)
                        PositionCallout(journalCallout, target, inventoryStep == 1 ? visual.JournalOverviewOffset : visual.JournalGuidedOffset);
                    else
                        PositionCallout(journalCallout, target, visual.InventoryGeneralOffset);
                }
            }
            else
            {
                RectTransform target = journalStep <= 1
                    ? journalWindow
                    : GetTabTarget(Mathf.Clamp(journalStep - 2, 0, 3));
                PositionCallout(journalCallout, target,
                    journalStep == 1 ? visual.JournalOverviewOffset : visual.JournalGuidedOffset);
            }
        }
    }

    private void PositionCallout(RectTransform callout, RectTransform target, Vector2 offset)
    {
        if (callout == null || target == null || canvasRect == null)
            return;

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        Vector3 targetWorld = target.TransformPoint(target.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, targetWorld);
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, overlayCanvas.worldCamera, out localPoint))
            return;

        Vector2 desired = localPoint + offset;
        Rect footprint = GetCalloutFootprint(callout);
        desired = ClampCalloutPosition(desired, footprint, visual.ScreenClampMargin);
        callout.anchoredPosition = desired;

        if (callout == initialCallout)
            RotatePointerToTarget(initialPointer.rectTransform, target);
        else if (callout == journalCallout && journalPointer != null && journalPointer.gameObject.activeSelf)
            RotatePointerToTarget(journalPointer.rectTransform, target);
    }

    // Transformed panel corners in overlay space, relative to anchoredPosition.
    // Compose the child-to-overlay transform directly so a serialized zero scale
    // on the overlay root does not require inverting a singular world matrix.
    private Rect GetCalloutFootprint(RectTransform callout)
    {
        Matrix4x4 toOverlay = Matrix4x4.identity;
        for (Transform current = callout; current != canvasRect; current = current.parent)
            toOverlay = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * toOverlay;

        Vector3[] corners = new Vector3[4];
        callout.GetLocalCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 corner = (Vector2)toOverlay.MultiplyPoint3x4(corners[i]) - callout.anchoredPosition;
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Vector2 ClampCalloutPosition(Vector2 desired, Rect footprint, float margin)
    {
        Rect bounds = canvasRect.rect;
        float minX = bounds.xMin + margin - footprint.xMin;
        float maxX = bounds.xMax - margin - footprint.xMax;
        float minY = bounds.yMin + margin - footprint.yMin;
        float maxY = bounds.yMax - margin - footprint.yMax;
        // Center only an axis whose authored footprint cannot fit.
        desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : bounds.center.x - footprint.center.x;
        desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : bounds.center.y - footprint.center.y;
        return desired;
    }

    private bool IsDevelopmentTutorialSuppressed(GameplaySystemId system)
    {
#if UNITY_EDITOR
        return developmentTutorialSuppressed.Contains(system);
#else
        return false;
#endif
    }

    private void PositionInventoryItemDetailsCallout(RectTransform callout, RectTransform target)
    {
        if (callout == null || target == null || canvasRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera sourceCamera = targetCanvas != null &&
            targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);
        Vector2 itemMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 itemMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCorners[i]);
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, overlayCanvas.worldCamera, out localPoint))
                return;

            itemMin = Vector2.Min(itemMin, localPoint);
            itemMax = Vector2.Max(itemMax, localPoint);
        }

        float gap = visual.InventoryItemGap;
        float margin = visual.InventoryEdgeMargin;
        Rect footprint = GetCalloutFootprint(callout);
        Rect bounds = canvasRect.rect;
        bool spaceBelow = itemMin.y - bounds.yMin >= footprint.height + gap + margin;
        Vector2 desired = new Vector2(
            (itemMin.x + itemMax.x) * 0.5f - footprint.center.x,
            spaceBelow
                ? itemMin.y - gap - footprint.yMax
                : itemMax.y + gap - footprint.yMin);

        desired += visual.InventoryItemDetailsOffset;
        desired = ClampCalloutPosition(desired, footprint, margin);
        callout.anchoredPosition = desired;

        // The approved item-details layout places the panel below the item and
        // keeps the arrow attached to the panel edge facing that item. The
        // above-item arrangement is only a genuine screen-space fallback.
        if (journalPointer != null)
        {
            RectTransform pointer = journalPointer.rectTransform;
            if (spaceBelow)
            {
                pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 1f);
                pointer.pivot = new Vector2(0.5f, 0f);
                pointer.anchoredPosition = visual.PointerBelowItemOffset;
                pointer.localRotation = Quaternion.identity;
            }
            else
            {
                pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0f);
                pointer.pivot = new Vector2(0.5f, 1f);
                pointer.anchoredPosition = visual.PointerAboveItemOffset;
                pointer.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
        }
    }

    /// <summary>
    /// The generated pointer glyph naturally faces up (the TMP "▲" character).
    /// Rotate it from that +Y baseline toward the live system anchor.
    /// </summary>
    private void RotatePointerToTarget(RectTransform pointer, RectTransform target)
    {
        if (pointer == null || target == null || canvasRect == null)
            return;

        Vector2 pointerPosition = GetOverlayLocalPosition(pointer);
        Vector2 targetPosition = GetOverlayLocalPosition(target);
        Vector2 direction = targetPosition - pointerPosition;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        pointer.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetOverlayLocalPosition(RectTransform rect)
    {
        Canvas sourceCanvas = rect.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null &&
            sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera
            : null;
        Vector3 worldPosition = rect.TransformPoint(rect.rect.center);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPosition);
        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, overlayCanvas.worldCamera, out localPosition);
        return localPosition;
    }

    private void HideAll()
    {
        if (initialCallout != null)
            initialCallout.gameObject.SetActive(false);
        if (journalCallout != null)
            journalCallout.gameObject.SetActive(false);
        if (presentationGroup != null)
            presentationGroup.alpha = 0f;
    }

    private void RemoveAnchorReferences(GameplaySystemTutorialAnchor anchor)
    {
        foreach (GameplaySystemId system in System.Enum.GetValues(typeof(GameplaySystemId)))
        {
            GameplaySystemTutorialAnchor current;
            if (anchors.TryGetValue(system, out current) && current == anchor)
                anchors.Remove(system);
        }
    }

    private bool BuildOverlay()
    {
        GameplaySystemTutorialVisual prefab =
            Resources.Load<GameplaySystemTutorialVisual>(VisualResourcePath);
        string error;
        if (prefab == null)
            return FailPresentation("Required prefab is missing at Resources/" + VisualResourcePath + ".");
        if (!prefab.ValidateReferences(out error))
            return FailPresentation(error);

        visual = Instantiate(prefab, transform, false);
        visual.name = "GameplaySystemTutorialVisual";
        overlayCanvas = visual.RootCanvas;
        canvasRect = overlayCanvas.transform as RectTransform;
        presentationGroup = visual.PresentationGroup;
        initialCallout = visual.InitialCallout;
        initialTitle = visual.InitialTitle;
        initialBody = visual.InitialBody;
        initialPointer = visual.InitialPointer;
        journalCallout = visual.GuidedCallout;
        journalTitle = visual.GuidedTitle;
        journalBody = visual.GuidedBody;
        journalPointer = visual.GuidedPointer;
        inventoryPointerAnchorMin = journalPointer.rectTransform.anchorMin;
        inventoryPointerAnchorMax = journalPointer.rectTransform.anchorMax;
        inventoryPointerPivot = journalPointer.rectTransform.pivot;
        inventoryPointerPosition = journalPointer.rectTransform.anchoredPosition;
        inventoryPointerRotation = journalPointer.rectTransform.localRotation;
        journalNextButton = visual.NextButton;
        journalNextLabel = visual.NextLabel;

        // Match the generated overlay's normal state before HUD suppression captures it.
        presentationGroup.alpha = 1f;
        presentationGroup.interactable = true;
        presentationGroup.blocksRaycasts = true;
        GameplayHUDTarget.AttachTo(visual.gameObject);
        journalNextButton.onClick.AddListener(AdvanceActiveTutorial);
        HideAll();
        return true;
    }

    private bool FailPresentation(string reason)
    {
        presentationFailed = true;
        ClearActive();
        Debug.LogError("Gameplay system tutorial presentation unavailable: " + reason +
            " No tutorial completion flags were changed.", this);
        return false;
    }
}
