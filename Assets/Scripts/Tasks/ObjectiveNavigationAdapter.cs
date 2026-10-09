using TargetIndicators;
using TargetIndicators.Samples;
using UnityEngine;

/// <summary>Read-only task binding for one explicitly configured navigation destination.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)] // Synchronize the asset after gameplay/Cinemachine camera movement.
public sealed class ObjectiveNavigationAdapter : MonoBehaviour
{
    [Header("Objective condition")]
    [SerializeField] private string taskId;
    [SerializeField] private string requiredStageId;
    [Tooltip("Opt in for stage-less objectives. Exact-stage matching remains the default.")]
    [SerializeField] private bool useStoryFlagEligibility = false;
    [SerializeField] private string requiredStoryFlagId = string.Empty;
    [SerializeField] private bool expectedStoryFlagValue = true;

    [Header("Destination and style")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private VisualIndicator indicatorPrefab;

    [Header("Asset components (dedicated to this destination)")]
    [SerializeField] private TargetIndicatorManager trackingManager;
    [SerializeField] private VisualIndicatorManager visualManager;
    [SerializeField] private CanvasGroup presentationGroup;
    [Tooltip("Left, top, right, bottom padding at the reference resolution.")]
    [SerializeField] private Vector4 referencePadding = new Vector4(96f, 96f, 96f, 96f);
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    [Header("Optional presentation safety (existing bindings retain their behavior)")]
    [SerializeField] private bool suppressDuringTransitions;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private FadeController fadeController;
    [SerializeField] private SceneEntrance[] sceneEntrances;

    private readonly System.Collections.Generic.HashSet<SceneEntrance> subscribedEntrances =
        new System.Collections.Generic.HashSet<SceneEntrance>();
    private bool entranceAccepted;
    private bool awaitingCameraReturn;

    private TaskManager boundTaskManager;
    private TargetIndicatorId indicatorId;
    private Transform registeredAnchor;
    private bool registered;
    private int screenWidth;
    private int screenHeight;

    private void OnEnable()
    {
        entranceAccepted = false;
        awaitingCameraReturn = false;
        SubscribeToEntrances();
        SetPresentationAlpha(0f);
        BindTaskManager();
    }

    private void LateUpdate()
    {
        // Singleton identity and configured references only; no scene searches.
        BindTaskManager();
        UpdatePadding();
        RefreshRegistration(); // Also handles a target becoming available/unavailable.

        if (trackingManager != null)
            trackingManager.GetChanges(); // Core component is disabled for manual late synchronization.

        var conversation = DialogueEditor.ConversationManager.Instance;
        bool suppressed = StorySequenceCoordinator.IsStorySequenceActive ||
            (conversation != null && conversation.isActiveAndEnabled && conversation.IsConversationActive);
        if (focusManager != null && focusManager.IsFocusing)
            awaitingCameraReturn = true;
        else if (awaitingCameraReturn && focusManager != null && focusManager.LastReturnCompleted)
            awaitingCameraReturn = false;
        suppressed |= entranceAccepted || awaitingCameraReturn ||
            (focusManager != null && focusManager.IsFocusing);
        if (suppressDuringTransitions)
            suppressed |= Time.timeScale <= 0f ||
                (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered) ||
                (fadeController != null && !fadeController.IncomingFadeCompleted);
        // Hide immediately for story/dialogue, then fade back without recreating the marker.
        float alpha = registered && !suppressed && trackingManager != null &&
            trackingManager.Camera != null && trackingManager.Camera.isActiveAndEnabled ? 1f : 0f;
        if (presentationGroup != null && alpha > 0f && fadeInDuration > 0f)
            alpha = Mathf.MoveTowards(presentationGroup.alpha, alpha, Time.unscaledDeltaTime / fadeInDuration);
        SetPresentationAlpha(alpha);
    }

    private void BindTaskManager()
    {
        TaskManager current = TaskManager.Instance;
        if (boundTaskManager == current)
            return;
        UnbindTaskManager();
        boundTaskManager = current;
        if (boundTaskManager != null)
            boundTaskManager.TaskChanged += RefreshRegistration;
        RefreshRegistration();
    }

    private bool IsObjectiveEligible()
    {
        if (boundTaskManager == null || string.IsNullOrWhiteSpace(taskId) ||
            boundTaskManager.GetTaskState(taskId) != TaskState.Active)
            return false;

        if (useStoryFlagEligibility)
            return !string.IsNullOrWhiteSpace(requiredStoryFlagId) &&
                SessionStoryState.GetFlag(requiredStoryFlagId.Trim()) == expectedStoryFlagValue;

        return !string.IsNullOrWhiteSpace(requiredStageId) &&
            boundTaskManager.IsCurrentStage(taskId, requiredStageId);
    }

    private void RefreshRegistration()
    {
        Transform anchor = targetAnchor != null ? targetAnchor : target;
        bool shouldRegister = isActiveAndEnabled && boundTaskManager != null &&
            !entranceAccepted &&
            IsObjectiveEligible() &&
            target != null && target.gameObject.activeInHierarchy &&
            anchor != null && anchor.gameObject.activeInHierarchy &&
            trackingManager != null && visualManager != null && visualManager.isActiveAndEnabled &&
            indicatorPrefab != null;

        if (registered && (!shouldRegister || registeredAnchor != anchor))
            Unregister();

        if (!shouldRegister || registered)
            return;

        // Manual mode prevents the asset from creating an additional default indicator.
        visualManager.AddIndicatorMode = AddIndicatorMode.Manual;
        if (visualManager.TryAddVisualIndicator(anchor, indicatorPrefab, out indicatorId))
        {
            registered = true;
            registeredAnchor = anchor;
            SetPresentationAlpha(0f);
        }
    }

    private void UpdatePadding()
    {
        if (trackingManager == null || (screenWidth == Screen.width && screenHeight == Screen.height))
            return;
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        float xScale = screenWidth / Mathf.Max(1f, referenceResolution.x);
        float yScale = screenHeight / Mathf.Max(1f, referenceResolution.y);
        trackingManager.LeftPadding = referencePadding.x * xScale;
        trackingManager.TopPadding = referencePadding.y * yScale;
        trackingManager.RightPadding = referencePadding.z * xScale;
        trackingManager.BottomPadding = referencePadding.w * yScale;
    }

    private void Unregister()
    {
        if (!registered)
            return;
        // Remove through the asset's visual API while the target is still registered.
        if (visualManager != null)
            visualManager.RemoveTargetIndicator(indicatorId);
        else if (trackingManager != null)
            trackingManager.TryRemoveTarget(indicatorId);
        registered = false;
        registeredAnchor = null;
        indicatorId = default;
        SetPresentationAlpha(0f);
    }

    private void SetPresentationAlpha(float alpha)
    {
        if (presentationGroup == null)
            return;
        presentationGroup.alpha = alpha;
        presentationGroup.interactable = false;
        presentationGroup.blocksRaycasts = false;
    }

    private void UnbindTaskManager()
    {
        if (boundTaskManager != null)
            boundTaskManager.TaskChanged -= RefreshRegistration;
        boundTaskManager = null;
    }

    private void SubscribeToEntrances()
    {
        if (sceneEntrances == null)
            return;
        foreach (SceneEntrance entrance in sceneEntrances)
            if (entrance != null && subscribedEntrances.Add(entrance))
                entrance.EntranceAccepted += OnEntranceAccepted;
    }

    private void OnEntranceAccepted()
    {
        // Scene-local handoff only: entering a house never completes the objective.
        entranceAccepted = true;
        Unregister();
        SetPresentationAlpha(0f);
    }

    private void UnsubscribeFromEntrances()
    {
        foreach (SceneEntrance entrance in subscribedEntrances)
            if (entrance != null)
                entrance.EntranceAccepted -= OnEntranceAccepted;
        subscribedEntrances.Clear();
    }

    private void OnDisable()
    {
        UnsubscribeFromEntrances();
        Unregister();
        UnbindTaskManager();
        SetPresentationAlpha(0f);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEntrances();
        Unregister();
        UnbindTaskManager();
    }
}
