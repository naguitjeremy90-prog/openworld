using DialogueEditor;
using TargetIndicators;
using TargetIndicators.Samples;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local bed guidance; sleeping and story progression remain authoritative.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed class BedNavigationController : MonoBehaviour
{
    [SerializeField] private SleepInteraction sleepInteraction;
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private VisualIndicator indicatorPrefab;
    [SerializeField] private TargetIndicatorManager trackingManager;
    [SerializeField] private VisualIndicatorManager visualManager;
    [SerializeField] private CanvasGroup presentationGroup;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private SceneEntrance roomExit;
    [SerializeField] private Vector4 referencePadding = new Vector4(96f, 96f, 96f, 96f);
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    private SleepInteraction subscribedSleep;
    private SceneEntrance subscribedExit;
    private TargetIndicatorId indicatorId;
    private Transform registeredAnchor;
    private bool registered;
    private bool finished;
    private bool awaitingFocusReturn;
    private bool focusReturnBlocked;
    private int screenWidth;
    private int screenHeight;

    private void OnEnable()
    {
        HidePresentation();
        subscribedSleep = sleepInteraction;
        subscribedExit = roomExit;
        if (subscribedSleep != null)
        {
            finished |= subscribedSleep.HasSleepBeenAccepted;
            subscribedSleep.SleepAccepted += FinishGuidance;
        }
        if (subscribedExit != null)
            subscribedExit.EntranceAccepted += FinishGuidance;
        ConversationManager.OnConversationStarted += HidePresentation;
    }

    private void FinishGuidance()
    {
        finished = true;
        RemoveIndicator();
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

        bool eligible = !finished && !GameFlags.isMorning &&
            gameObject.scene == SceneManager.GetActiveScene() &&
            sleepInteraction != null && sleepInteraction.isActiveAndEnabled &&
            !sleepInteraction.HasSleepBeenAccepted && roomExit != null &&
            targetAnchor != null && targetAnchor.gameObject.activeInHierarchy &&
            gameplayCamera != null && indicatorPrefab != null &&
            trackingManager != null && trackingManager.gameObject.activeInHierarchy &&
            visualManager != null && visualManager.isActiveAndEnabled && presentationGroup != null;
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
            trackingManager.GetChanges();
        }
        if (!registered || suppressed)
            HidePresentation();
        else
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
        if (subscribedSleep != null)
            subscribedSleep.SleepAccepted -= FinishGuidance;
        if (subscribedExit != null)
            subscribedExit.EntranceAccepted -= FinishGuidance;
        subscribedSleep = null;
        subscribedExit = null;
        ConversationManager.OnConversationStarted -= HidePresentation;
        RemoveIndicator();
    }

    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
}
