using DialogueEditor;
using System.Collections.Generic;
using TargetIndicators;
using TargetIndicators.Samples;
using UnityEngine;

/// <summary>Scene-local guidance for an existing story unlock; never owns story progression.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed class StoryDestinationNavigationController : MonoBehaviour
{
    [Header("Existing story destination")]
    [SerializeField] private string requiredStoryFlagId;
    [SerializeField] private GameObject optionalBlocker;
    [SerializeField] private SceneEntrance destinationEntrance;
    [SerializeField] private SceneEntrance[] otherEntrances;
    [SerializeField] private bool nighttimeOnly = true;

    [Header("Navigation")]
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private VisualIndicator indicatorPrefab;
    [SerializeField] private TargetIndicatorManager trackingManager;
    [SerializeField] private VisualIndicatorManager visualManager;
    [SerializeField] private CanvasGroup presentationGroup;
    [SerializeField] private Vector4 referencePadding = new Vector4(96f, 96f, 96f, 96f);
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Presentation safety")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    private readonly HashSet<SceneEntrance> subscribedEntrances = new HashSet<SceneEntrance>();
    private TargetIndicatorId indicatorId;
    private Transform registeredAnchor;
    private bool registered;
    private bool storyRequirementMet;
    private bool entranceAccepted;
    private bool awaitingFocusReturn;
    private bool focusReturnBlocked;
    private int screenWidth;
    private int screenHeight;

    private void OnEnable()
    {
        HidePresentation();
        storyRequirementMet = SessionStoryState.GetFlag(requiredStoryFlagId);
        SessionStoryState.FlagChanged += HandleStoryFlagChanged;
        ConversationManager.OnConversationStarted += HidePresentation;
        SubscribeEntrance(destinationEntrance);
        // Capture subscriptions so cleanup still addresses the original instances after Inspector edits.
        if (otherEntrances != null)
            foreach (SceneEntrance entrance in otherEntrances)
                SubscribeEntrance(entrance);
    }

    private void SubscribeEntrance(SceneEntrance entrance)
    {
        if (entrance != null && subscribedEntrances.Add(entrance))
            entrance.EntranceAccepted += HandleEntranceAccepted;
    }

    private void HandleStoryFlagChanged(string flagId, bool completed)
    {
        if (flagId != requiredStoryFlagId) return;
        storyRequirementMet = completed;
        if (!completed) RemoveIndicator();
        // Registration waits for the blocker, dialogue UI and focus return in LateUpdate.
    }

    private void HandleEntranceAccepted()
    {
        entranceAccepted = true;
        RemoveIndicator(); // SceneEntrance invokes this before starting its transition fade.
    }

    private void LateUpdate()
    {
        bool focusing = focusManager != null && focusManager.IsFocusing;
        if (focusing)
            awaitingFocusReturn = true;
        else if (awaitingFocusReturn)
        {
            awaitingFocusReturn = false;
            focusReturnBlocked = focusManager == null || !focusManager.LastReturnCompleted;
        }

        bool eligible = !entranceAccepted && storyRequirementMet &&
            (!nighttimeOnly || !GameFlags.isMorning) &&
            (optionalBlocker == null || !optionalBlocker.activeInHierarchy) &&
            destinationEntrance != null && targetAnchor != null &&
            targetAnchor.gameObject.activeInHierarchy && gameplayCamera != null &&
            indicatorPrefab != null && trackingManager != null && visualManager != null &&
            visualManager.isActiveAndEnabled && presentationGroup != null;
        if (!eligible || (registered && registeredAnchor != targetAnchor))
            RemoveIndicator();

        ConversationManager conversation = ConversationManager.Instance;
        bool suppressed = focusing || focusReturnBlocked ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            (conversation != null && conversation.IsConversationActive) ||
            gameplayCamera == null || !gameplayCamera.isActiveAndEnabled;

        if (eligible && !suppressed && !registered)
        {
            trackingManager.Camera = gameplayCamera;
            visualManager.AddIndicatorMode = AddIndicatorMode.Manual;
            registered = visualManager.TryAddVisualIndicator(targetAnchor, indicatorPrefab, out indicatorId);
            if (registered) registeredAnchor = targetAnchor;
        }

        if (registered)
        {
            UpdatePadding();
            trackingManager.GetChanges(); // Asset supplies all projection, boundary and rotation calculations.
        }

        if (!registered || suppressed)
            HidePresentation();
        else if (presentationGroup != null)
            SetAlpha(fadeInDuration > 0f
                ? Mathf.MoveTowards(presentationGroup.alpha, 1f, Time.unscaledDeltaTime / fadeInDuration)
                : 1f);
    }

    private void UpdatePadding()
    {
        if (screenWidth == Screen.width && screenHeight == Screen.height) return;
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        float xScale = screenWidth / Mathf.Max(1f, referenceResolution.x);
        float yScale = screenHeight / Mathf.Max(1f, referenceResolution.y);
        trackingManager.LeftPadding = referencePadding.x * xScale;
        trackingManager.TopPadding = referencePadding.y * yScale;
        trackingManager.RightPadding = referencePadding.z * xScale;
        trackingManager.BottomPadding = referencePadding.w * yScale;
    }

    private void HidePresentation() => SetAlpha(0f);

    private void SetAlpha(float alpha)
    {
        if (presentationGroup == null) return;
        presentationGroup.alpha = alpha;
        presentationGroup.interactable = false;
        presentationGroup.blocksRaycasts = false;
    }

    private void RemoveIndicator()
    {
        HidePresentation();
        if (!registered) return;
        if (visualManager != null)
            visualManager.RemoveTargetIndicator(indicatorId);
        else if (trackingManager != null)
            trackingManager.TryRemoveTarget(indicatorId);
        registered = false;
        registeredAnchor = null;
        indicatorId = default;
    }

    private void Cleanup()
    {
        SessionStoryState.FlagChanged -= HandleStoryFlagChanged;
        ConversationManager.OnConversationStarted -= HidePresentation;
        foreach (SceneEntrance entrance in subscribedEntrances)
            if (entrance != null)
                entrance.EntranceAccepted -= HandleEntranceAccepted;
        subscribedEntrances.Clear();
        RemoveIndicator();
    }

    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
}
