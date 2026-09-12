using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the single optional gameplay-system tutorial presentation. It never changes
/// time scale, controls, cameras, interactions, or story progression.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class GameplaySystemTutorialManager : MonoBehaviour
{
    private const float JournalPostRevealDelay = 0.5f;

    private static GameplaySystemTutorialManager instance;

    private readonly Dictionary<GameplaySystemId, GameplaySystemTutorialDefinition> definitions =
        new Dictionary<GameplaySystemId, GameplaySystemTutorialDefinition>();
    private readonly Dictionary<GameplaySystemId, GameplaySystemTutorialAnchor> anchors =
        new Dictionary<GameplaySystemId, GameplaySystemTutorialAnchor>();
    private readonly HashSet<GameplaySystemId> presentationReady =
        new HashSet<GameplaySystemId>();

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
    // Inventory has two story-timed teaching moments: the received item, then its return.
    // The intermediate completion is persisted separately from inventory_tutorial_seen.
    private int inventoryStep;
    private bool inventoryOpen;
    private int clarityStep;
    private bool unreadableDocumentOpen;
    private bool documentClarityUsed;
    private bool journalOpen;
    private bool activeSystemTemporarilyHidden;
    private bool lastStorySequenceActive;

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
        BuildOverlay();

        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        SessionStoryState.FlagChanged += HandleFlagChanged;
        ClarityManager.Activated += HandleClarityActivated;
        ClarityDocumentViewer.AnyDocumentOpened += HandleDocumentOpened;
        ClarityDocumentViewer.AnyDocumentClosed += HandleDocumentClosed;
        lastStorySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
    }

    private void OnDestroy()
    {
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
        bool storySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
        if (storySequenceActive != lastStorySequenceActive)
        {
            lastStorySequenceActive = storySequenceActive;
            RefreshPresentation();
        }

        if (!storySequenceActive)
            PositionVisibleCallout();
    }

    public void RegisterAnchor(GameplaySystemTutorialAnchor anchor)
    {
        if (anchor == null)
            return;

        RemoveAnchorReferences(anchor);
        anchors[anchor.System] = anchor;
        RefreshPresentation();
    }

    public void UnregisterAnchor(GameplaySystemTutorialAnchor anchor)
    {
        if (anchor == null)
            return;

        RemoveAnchorReferences(anchor);

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
        RefreshPresentation();
    }

    /// <summary>Called by a system's existing reveal at its real completion point.</summary>
    public void NotifySystemRevealCompleted(GameplaySystemId system)
    {
        presentationReady.Add(system);

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
            GameplaySystemTutorialState.IsSeen(system))
            return;

        if (system == GameplaySystemId.Inventory)
        {
            inventoryOpen = true;
            CancelDelay();
            if (inventoryStep == 0 && !GameplaySystemTutorialState.InventoryFirstItemSeen)
                inventoryStep = 1;
            else if (inventoryStep == 2)
                inventoryStep = 3;
            RefreshPresentation();
            return;
        }

        activeSystemTemporarilyHidden = true;
        HideAll();
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
        if (!GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory) ||
            GameplaySystemTutorialState.IsSeen(GameplaySystemId.Inventory))
            return;

        inventoryStep = 2;
        inventoryOpen = false;
        if (!hasActiveTutorial || activeSystem != GameplaySystemId.Inventory)
            Activate(GameplaySystemId.Inventory);
        else
        {
            CancelDelay();
            activeSystemTemporarilyHidden = false;
            RefreshPresentation();
        }
    }

    public void CompleteActiveTutorial()
    {
        if (!hasActiveTutorial)
            return;

        GameplaySystemTutorialState.SetSeen(activeSystem, true);
    }

#if UNITY_EDITOR
    public void RequestDevelopmentTutorial(
        GameplaySystemId system,
        GameplaySystemTutorialDefinition definition = null)
    {
        if (definition != null)
            RegisterDefinition(definition);
        presentationReady.Add(system);
        Activate(system);
        RefreshPresentation();
    }
#endif

    private void RegisterJournalDefinition()
    {
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Journal,
            "JOURNAL",
            "Use the Journal to keep track of important discoveries, people, fragments, and reflections.",
            JournalPostRevealDelay,
            false));
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Inventory,
            "Inventory",
            "Stores important items Miguel receives during his investigation.",
            JournalPostRevealDelay,
            false));
        RegisterDefinition(new GameplaySystemTutorialDefinition(
            GameplaySystemId.Clarity,
            "Clarity",
            "Hold C to reveal important details that may be difficult to notice.",
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

        if (!GameplaySystemTutorialState.IsSeen(system))
            Activate(system);

        if (presentationReady.Contains(system))
            StartDelayedPresentation(system);
        else
            RefreshPresentation();
    }

    private void HandleFlagChanged(string flagId, bool value)
    {
        if (!hasActiveTutorial || !value ||
            flagId != GameplaySystemTutorialState.GetSeenFlagId(activeSystem))
            return;

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

    private void Activate(GameplaySystemId system)
    {
        CancelDelay();
        hasActiveTutorial = true;
        activeSystem = system;
        activeSystemTemporarilyHidden = false;
        journalOpen = system == GameplaySystemId.Journal &&
            ReconstructionJournalManager.Instance != null &&
            ReconstructionJournalManager.Instance.IsOpen;
        RefreshPresentation();
    }

    private void ClearActive()
    {
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
    }

    private void StartDelayedPresentation(GameplaySystemId system)
    {
        CancelDelay();
        GameplaySystemTutorialDefinition definition;
        float delay = definitions.TryGetValue(system, out definition)
            ? definition.PostRevealDelay
            : 0f;
        delayedPresentation = StartCoroutine(ShowAfterDelay(system, delay));
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
        RefreshPresentation();
    }

    private void CancelDelay()
    {
        if (delayedPresentation != null)
            StopCoroutine(delayedPresentation);
        delayedPresentation = null;
    }

    private void RefreshPresentation()
    {
        HideAll();
        if (!hasActiveTutorial || StorySequenceCoordinator.IsStorySequenceActive ||
            GameplaySystemTutorialState.IsSeen(activeSystem) ||
            !presentationReady.Contains(activeSystem) || delayedPresentation != null ||
            activeSystemTemporarilyHidden)
            return;

        if (activeSystem == GameplaySystemId.Journal && journalOpen && journalStep > 0)
        {
            ShowJournalStep();
            return;
        }

        if (activeSystem == GameplaySystemId.Inventory)
        {
            if (inventoryStep == 1 && inventoryOpen)
            {
                ShowInventoryStep("Item Details", "You can select an item to view its description.");
                return;
            }

            if (inventoryStep == 3 && inventoryOpen)
            {
                ShowInventoryStep(
                    "Quest Items",
                    "Quest Items may be removed when they are no longer needed.");
                return;
            }

            if (inventoryStep == 4 && inventoryOpen)
            {
                ShowInventoryStep(
                    "Key Items and Documents",
                    "Key Items and Documents hold important items and information worth keeping.");
                return;
            }
        }

        if (activeSystem == GameplaySystemId.Clarity && clarityStep == 1)
        {
            initialTitle.text = "Clarity — Documents";
            initialBody.text =
                "You can also hold C while examining unreadable documents to reveal hidden details.";
            initialPointer.gameObject.SetActive(false);
            initialCallout.gameObject.SetActive(true);
            presentationGroup.alpha = 1f;
            return;
        }

        GameplaySystemTutorialDefinition definition;
        GameplaySystemTutorialAnchor anchor;
        if (!definitions.TryGetValue(activeSystem, out definition) ||
            !anchors.TryGetValue(activeSystem, out anchor) || anchor == null ||
            anchor.Target == null)
            return;

        initialTitle.text = definition.Title;
        initialBody.text = definition.Description;
        if (activeSystem == GameplaySystemId.Inventory && inventoryStep == 2)
        {
            initialTitle.text = "Inventory Updated";
            initialBody.text = "Open your Inventory to see what changed.";
        }
        initialPointer.gameObject.SetActive(definition.ShowInitialPointer);
        initialCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;
    }

    private void ShowJournalStep()
    {
        RectTransform target = journalWindow;
        journalTitle.text = "JOURNAL";

        switch (journalStep)
        {
            case 1:
                journalBody.text =
                    "The Journal records the important things Miguel discovers during his investigation.";
                journalNextLabel.text = "Next";
                break;
            case 2:
                target = GetTabTarget(0);
                journalTitle.text = "OBSERVATIONS";
                journalBody.text =
                    "Important discoveries and details Miguel notices during the investigation.";
                journalNextLabel.text = "Next";
                break;
            case 3:
                target = GetTabTarget(1);
                journalTitle.text = "PEOPLE";
                journalBody.text =
                    "Information about people Miguel meets and what he learns about them.";
                journalNextLabel.text = "Next";
                break;
            case 4:
                target = GetTabTarget(2);
                journalTitle.text = "FRAGMENTS";
                journalBody.text =
                    "Documents, clues, and pieces of information connected to Makamisa.";
                journalNextLabel.text = "Next";
                break;
            default:
                target = GetTabTarget(3);
                journalTitle.text = "REFLECTIONS";
                journalBody.text =
                    "Questions that let Miguel reflect on what he has learned.";
                journalNextLabel.text = "Got it";
                break;
        }

        journalCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;
        PositionCallout(journalCallout, target, journalStep == 1 ? Vector2.zero : new Vector2(0f, -105f));
    }

    private void ShowInventoryStep(string title, string body)
    {
        journalTitle.text = title;
        journalBody.text = body;
        journalNextLabel.text = inventoryStep >= 4 ? "Got it" : "Next";
        bool itemDetailsStep = inventoryStep == 1;
        RectTransform target = null;
        if (itemDetailsStep)
        {
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            if (inventoryUI != null)
                inventoryUI.TryGetItemSlot("aling_ika_payment", out target);
        }

        if (journalPointer != null)
            journalPointer.gameObject.SetActive(itemDetailsStep && target != null);
        journalCallout.gameObject.SetActive(true);
        presentationGroup.alpha = 1f;

        if (target == null)
        {
            GameplaySystemTutorialAnchor anchor;
            if (anchors.TryGetValue(GameplaySystemId.Inventory, out anchor) && anchor != null)
                target = anchor.Target;
        }

        if (target != null)
            PositionCallout(journalCallout, target, itemDetailsStep ? new Vector2(0f, 180f) : Vector2.zero);
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
            if (!hasActiveTutorial || !inventoryOpen)
                return;

            if (inventoryStep == 1)
            {
                GameplaySystemTutorialState.SetInventoryFirstItemSeen(true);
                hasActiveTutorial = false;
                HideAll();
                return;
            }

            if (inventoryStep == 3)
            {
                inventoryStep = 4;
                RefreshPresentation();
                return;
            }

            if (inventoryStep == 4)
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
                PositionCallout(initialCallout, anchor.Target, new Vector2(0f, -100f));
        }
        else if (journalCallout.gameObject.activeSelf)
        {
            if (activeSystem == GameplaySystemId.Inventory)
            {
                RectTransform target = null;
                if (inventoryStep == 1)
                {
                    InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
                    if (inventoryUI != null)
                        inventoryUI.TryGetItemSlot("aling_ika_payment", out target);
                }

                if (target == null)
                {
                    GameplaySystemTutorialAnchor anchor;
                    if (anchors.TryGetValue(GameplaySystemId.Inventory, out anchor) && anchor != null)
                        target = anchor.Target;
                }

                if (target != null)
                    PositionCallout(journalCallout, target, inventoryStep == 1 ? new Vector2(0f, 180f) : Vector2.zero);
            }
            else
            {
                RectTransform target = journalStep <= 1
                    ? journalWindow
                    : GetTabTarget(Mathf.Clamp(journalStep - 2, 0, 3));
                PositionCallout(journalCallout, target,
                    journalStep == 1 ? Vector2.zero : new Vector2(0f, -105f));
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
        Vector2 half = callout.rect.size * 0.5f;
        Rect bounds = canvasRect.rect;
        desired.x = Mathf.Clamp(desired.x, bounds.xMin + half.x + 12f, bounds.xMax - half.x - 12f);
        desired.y = Mathf.Clamp(desired.y, bounds.yMin + half.y + 12f, bounds.yMax - half.y - 12f);
        callout.anchoredPosition = desired;

        if (callout == initialCallout)
            RotatePointerToTarget(initialPointer.rectTransform, target);
        else if (callout == journalCallout && journalPointer != null && journalPointer.gameObject.activeSelf)
            RotatePointerToTarget(journalPointer.rectTransform, target);
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

    private void BuildOverlay()
    {
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        presentationGroup = gameObject.AddComponent<CanvasGroup>();
        presentationGroup.interactable = true;
        presentationGroup.blocksRaycasts = true;
        canvasRect = transform as RectTransform;
        GameplayHUDTarget.AttachTo(gameObject);

        initialCallout = CreateCallout("System Callout", new Vector2(390f, 132f), false,
            out initialTitle, out initialBody, out initialPointer, out _, out _);
        journalCallout = CreateCallout("Journal Walkthrough", new Vector2(470f, 146f), true,
            out journalTitle, out journalBody, out journalPointer, out journalNextButton, out journalNextLabel);
        journalNextButton.onClick.AddListener(AdvanceActiveTutorial);
        HideAll();
    }

    private RectTransform CreateCallout(
        string name,
        Vector2 size,
        bool includeButton,
        out TMP_Text title,
        out TMP_Text body,
        out TMP_Text pointer,
        out Button actionButton,
        out TMP_Text actionLabel)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = size;
        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.105f, 0.09f, 0.075f, 0.94f);
        background.raycastTarget = includeButton;

        title = CreateText(panel, "Title", new Vector2(16f, -12f),
            new Vector2(includeButton ? -105f : -16f, 31f), 20f, FontStyles.Bold);
        body = CreateText(panel, "Body", new Vector2(16f, -43f),
            new Vector2(includeButton ? -105f : -16f, -12f), 16f, FontStyles.Normal);

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(size.x - (includeButton ? 121f : 32f), 28f);
        titleRect.anchoredPosition = new Vector2(includeButton ? -44.5f : 0f, -10f);

        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = new Vector2(size.x - (includeButton ? 121f : 32f), 88f);
        bodyRect.anchoredPosition = new Vector2(includeButton ? -44.5f : 0f, -42f);

        pointer = CreateText(
            panel, "Pointer", Vector2.zero, Vector2.zero, 24f, FontStyles.Bold);
        RectTransform pointerRect = pointer.rectTransform;
        pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 1f);
        pointerRect.pivot = new Vector2(0.5f, 0f);
        pointerRect.anchoredPosition = new Vector2(0f, 4f);
        pointerRect.sizeDelta = new Vector2(36f, 28f);
        pointer.alignment = TextAlignmentOptions.Center;
        pointer.text = "▲";

        actionButton = null;
        actionLabel = null;
        if (includeButton)
        {
            GameObject buttonObject = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(panel, false);
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-14f, 0f);
            buttonRect.sizeDelta = new Vector2(80f, 38f);
            buttonObject.GetComponent<Image>().color = new Color(0.68f, 0.55f, 0.34f, 1f);
            actionButton = buttonObject.GetComponent<Button>();
            actionLabel = CreateText(buttonRect, "Label", Vector2.zero, Vector2.zero, 15f, FontStyles.Bold);
            RectTransform labelRect = actionLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            actionLabel.alignment = TextAlignmentOptions.Center;
            actionLabel.raycastTarget = false;
        }

        return panel;
    }

    private TMP_Text CreateText(
        RectTransform parent,
        string name,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = new Color(0.95f, 0.9f, 0.78f, 1f);
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}
