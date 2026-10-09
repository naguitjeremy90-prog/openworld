using DialogueEditor;
using TargetIndicators;
using TargetIndicators.Samples;
using UnityEngine;

/// <summary>Scene-local, lifecycle-driven onboarding presentation; never changes gameplay progression.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed class ArrivalNavigationController : MonoBehaviour
{
    public enum NavigationPhase { WaitingForTutorial, ForwardGuidance, SignageSequence, DoorGuidance, Finished }

    [Header("Existing sequence")]
    [SerializeField] private TutorialStartDelay movementTutorial;
    [SerializeField] private SelfDialogueTrigger arrivalDialogue;
    [SerializeField] private CameraFocusTrigger signageFocus;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private SelfDialogueTrigger signageDialogue;
    [SerializeField] private SceneEntrance destinationEntrance;
    [SerializeField] private SceneEntrance[] otherEntrances;
    [SerializeField] private DayNightManager dayNightManager;

    [Header("Session")]
    [SerializeField] private string sessionStateId = "draftworld_night_navigation_phase";

    [Header("Forward presentation")]
    [SerializeField] private ObjectiveNavigationVisualIndicator forwardPrefab;
    [SerializeField] private RectTransform forwardContainer;
    [SerializeField] private CanvasGroup forwardGroup;
    [SerializeField, Tooltip("UI offset from the right-edge midpoint of the navigation Canvas.")]
    private Vector2 forwardPosition = new Vector2(-96f, 0f);

    [Header("Destination presentation")]
    [SerializeField] private Transform destinationAnchor;
    [SerializeField] private ObjectiveNavigationVisualIndicator destinationPrefab;
    [SerializeField] private TargetIndicatorManager trackingManager;
    [SerializeField] private VisualIndicatorManager visualManager;
    [SerializeField] private CanvasGroup destinationGroup;
    [SerializeField] private Vector4 referencePadding = new Vector4(96f, 96f, 96f, 96f);
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    public NavigationPhase Phase => phase;

    private NavigationPhase phase;
    private ObjectiveNavigationVisualIndicator forwardInstance;
    private TargetIndicatorId destinationId;
    private bool registered;
    private bool initialized;
    private bool focusReturnedSuccessfully;
    private bool signageDialogueCompleted;
    private bool transitionAccepted;
    private int screenWidth;
    private int screenHeight;

    private void OnEnable()
    {
        SetAlpha(forwardGroup, 0f);
        SetAlpha(destinationGroup, 0f);
        if (movementTutorial != null) movementTutorial.TutorialCompleted += HandleTutorialCompleted;
        if (arrivalDialogue != null) arrivalDialogue.ConversationFinished += HidePresentation;
        if (signageFocus != null)
        {
            signageFocus.FocusStarted += HandleFocusStarted;
            signageFocus.OnFocusFinished.AddListener(HandleFocusFinished);
        }
        if (signageDialogue != null) signageDialogue.ConversationFinished += HandleSignageDialogueFinished;
        if (destinationEntrance != null) destinationEntrance.EntranceAccepted += HandleDestinationEntered;
        if (otherEntrances != null)
            foreach (var entrance in otherEntrances)
                if (entrance != null && entrance != destinationEntrance)
                    entrance.EntranceAccepted += HandleOtherEntrance;
        ConversationManager.OnConversationStarted += HidePresentation;
    }

    private void Start() => InitializePhase();

    private void InitializePhase()
    {
        if (initialized) return;
        initialized = true;
        int stored = SessionStoryState.GetInt(sessionStateId);
        phase = (NavigationPhase)Mathf.Clamp(stored, 0, (int)NavigationPhase.Finished);
        if (!IsNighttime())
            SetPhase(NavigationPhase.Finished);
        else if (phase == NavigationPhase.WaitingForTutorial && movementTutorial != null &&
                 movementTutorial.IsCompletedAndHidden)
            SetPhase(NavigationPhase.ForwardGuidance);
    }

    private bool IsNighttime() => !GameFlags.isMorning &&
        (dayNightManager == null || dayNightManager.IsNight);

    private void HandleTutorialCompleted()
    {
        InitializePhase();
        if (phase == NavigationPhase.WaitingForTutorial && movementTutorial.IsCompletedAndHidden)
            SetPhase(NavigationPhase.ForwardGuidance);
    }

    private void HandleFocusStarted()
    {
        InitializePhase();
        if (phase == NavigationPhase.Finished || transitionAccepted) return;
        focusReturnedSuccessfully = false;
        signageDialogueCompleted = false;
        SetPhase(NavigationPhase.SignageSequence);
        HidePresentation();
    }

    private void HandleFocusFinished()
    {
        if (phase == NavigationPhase.SignageSequence)
            focusReturnedSuccessfully = focusManager != null && focusManager.LastReturnCompleted;
    }

    private void HandleSignageDialogueFinished()
    {
        if (phase == NavigationPhase.SignageSequence)
            signageDialogueCompleted = true;
    }

    private void HandleDestinationEntered()
    {
        transitionAccepted = true;
        initialized = true;
        SetPhase(NavigationPhase.Finished);
        HidePresentation();
        RemoveDestination();
        RemoveForward();
    }

    private void HandleOtherEntrance()
    {
        transitionAccepted = true;
        HidePresentation();
        RemoveDestination();
        RemoveForward();
    }

    private void SetPhase(NavigationPhase value)
    {
        phase = value;
        SessionStoryState.SetInt(sessionStateId, (int)value);
        if (value != NavigationPhase.ForwardGuidance) RemoveForward();
        if (value != NavigationPhase.DoorGuidance) RemoveDestination();
    }

    private void LateUpdate()
    {
        InitializePhase();
        if (!IsNighttime() && phase != NavigationPhase.Finished)
            SetPhase(NavigationPhase.Finished);

        var conversation = ConversationManager.Instance;
        bool suppressed = transitionAccepted || !IsNighttime() ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            (conversation != null && conversation.IsConversationActive) ||
            (focusManager != null && focusManager.IsFocusing);

        // End-of-dialogue events precede the UI closing fade; wait for actual safe gameplay.
        if (!suppressed && phase == NavigationPhase.SignageSequence &&
            focusReturnedSuccessfully && signageDialogueCompleted)
            SetPhase(NavigationPhase.DoorGuidance);

        if (!transitionAccepted && phase == NavigationPhase.ForwardGuidance && forwardInstance == null &&
            forwardPrefab != null && forwardContainer != null)
        {
            forwardInstance = Instantiate(forwardPrefab, forwardContainer);
            forwardInstance.name = "ForwardIndicator";
        }
        if (forwardInstance != null)
            ((RectTransform)forwardInstance.transform).anchoredPosition = forwardPosition;

        UpdateDestination();
        FadePresentation(forwardGroup, !suppressed && phase == NavigationPhase.ForwardGuidance && forwardInstance != null);
        FadePresentation(destinationGroup, !suppressed && phase == NavigationPhase.DoorGuidance && registered &&
            trackingManager != null && trackingManager.Camera != null && trackingManager.Camera.isActiveAndEnabled);
    }

    private void UpdateDestination()
    {
        bool available = !transitionAccepted && phase == NavigationPhase.DoorGuidance && destinationAnchor != null &&
            destinationAnchor.gameObject.activeInHierarchy && destinationPrefab != null &&
            trackingManager != null && visualManager != null && visualManager.isActiveAndEnabled;
        if (registered && !available) RemoveDestination();
        if (available && !registered)
        {
            visualManager.AddIndicatorMode = AddIndicatorMode.Manual;
            registered = visualManager.TryAddVisualIndicator(destinationAnchor, destinationPrefab, out destinationId);
        }
        if (trackingManager == null) return;
        if (screenWidth != Screen.width || screenHeight != Screen.height)
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            float x = screenWidth / Mathf.Max(1f, referenceResolution.x);
            float y = screenHeight / Mathf.Max(1f, referenceResolution.y);
            trackingManager.LeftPadding = referencePadding.x * x;
            trackingManager.TopPadding = referencePadding.y * y;
            trackingManager.RightPadding = referencePadding.z * x;
            trackingManager.BottomPadding = referencePadding.w * y;
        }
        trackingManager.GetChanges();
    }

    private void FadePresentation(CanvasGroup group, bool visible)
    {
        float alpha = visible ? 1f : 0f;
        if (group != null && visible && fadeInDuration > 0f)
            alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / fadeInDuration);
        SetAlpha(group, alpha);
    }

    private void HidePresentation()
    {
        SetAlpha(forwardGroup, 0f);
        SetAlpha(destinationGroup, 0f);
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = group.blocksRaycasts = false;
    }

    private void RemoveForward()
    {
        SetAlpha(forwardGroup, 0f);
        if (forwardInstance != null) Destroy(forwardInstance.gameObject);
        forwardInstance = null;
    }

    private void RemoveDestination()
    {
        SetAlpha(destinationGroup, 0f);
        if (!registered) return;
        if (visualManager != null) visualManager.RemoveTargetIndicator(destinationId);
        else if (trackingManager != null) trackingManager.TryRemoveTarget(destinationId);
        registered = false;
        destinationId = default;
    }

    private void OnDisable()
    {
        if (movementTutorial != null) movementTutorial.TutorialCompleted -= HandleTutorialCompleted;
        if (arrivalDialogue != null) arrivalDialogue.ConversationFinished -= HidePresentation;
        if (signageFocus != null)
        {
            signageFocus.FocusStarted -= HandleFocusStarted;
            signageFocus.OnFocusFinished.RemoveListener(HandleFocusFinished);
        }
        if (signageDialogue != null) signageDialogue.ConversationFinished -= HandleSignageDialogueFinished;
        if (destinationEntrance != null) destinationEntrance.EntranceAccepted -= HandleDestinationEntered;
        if (otherEntrances != null)
            foreach (var entrance in otherEntrances)
                if (entrance != null && entrance != destinationEntrance)
                    entrance.EntranceAccepted -= HandleOtherEntrance;
        ConversationManager.OnConversationStarted -= HidePresentation;
        HidePresentation();
        RemoveDestination();
        RemoveForward();
    }
}
