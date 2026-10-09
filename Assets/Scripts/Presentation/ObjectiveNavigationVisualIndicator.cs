using TargetIndicators;
using TargetIndicators.Samples;
using UnityEngine;

/// <summary>Presentation smoothing only; the installed asset supplies all poses and boundary state.</summary>
[DefaultExecutionOrder(11010)]
public sealed class ObjectiveNavigationVisualIndicator : VisualIndicator
{
    public enum PresentationMode { TrackedArrow, FixedGuidance, TrackedDestination }

    [SerializeField] private PresentationMode presentationMode;
    [SerializeField, Tooltip("Keep the authored arrow rotation instead of the tracked target direction.")]
    private bool usePrefabArrowRotation;
    [SerializeField] private CanvasGroup iconGroup;
    [SerializeField] private CanvasGroup arrowGroup;
    [SerializeField, Min(0f)] private float onScreenResponseTime = 0.065f;
    [SerializeField, Min(0f)] private float offScreenResponseTime = 0.025f;
    [SerializeField, Min(0f)] private float stateFadeDuration = 0.12f;

    [Header("Floating Pulse")]
    [SerializeField] private bool enableFloatingPulse = true;
    [SerializeField, Min(0f)] private float horizontalFloatAmplitude = 6f;
    [SerializeField, Min(0.01f)] private float floatCycleDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float minimumPulseOpacity = 0.55f;
    [SerializeField, Range(0f, 1f)] private float maximumPulseOpacity = 1f;

    private RectTransform arrowVisual;
    private CanvasGroup pulseGroup;
    private Transform animationCanvas;
    private Vector2 arrowBaseline;
    private float pulsePhase;
    private RectTransform destinationVisual;
    private CanvasGroup destinationPulseGroup;
    private Vector2 destinationBaseline;

    private Vector2 desiredPosition;
    private bool outside;
    private bool hasPose;
    private float previousCanvasScale;
    private int poseFrame;
    private TargetIndicatorManager trackingManager;
    private bool hasViewportReference;
    private bool outsideViewport;

    protected override void Awake()
    {
        base.Awake();
        if (presentationMode == PresentationMode.FixedGuidance)
            _rectTransform.anchorMin = _rectTransform.anchorMax = new Vector2(1f, 0.5f);
        trackingManager = GetComponentInParent<TargetIndicatorManager>();
        UnityEngine.UI.Image arrowImage = _rotationContent.GetComponentInChildren<UnityEngine.UI.Image>(true);
        if (arrowImage != null)
        {
            arrowVisual = arrowImage.rectTransform;
            arrowBaseline = arrowVisual.anchoredPosition;
            pulseGroup = arrowVisual.GetComponent<CanvasGroup>();
            Canvas canvas = arrowVisual.GetComponentInParent<Canvas>();
            animationCanvas = canvas != null ? canvas.transform : null;
        }
        if (presentationMode == PresentationMode.TrackedDestination)
        {
            UnityEngine.UI.Image destinationImage = _coreContent.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (destinationImage != null)
            {
                destinationVisual = destinationImage.rectTransform;
                destinationBaseline = destinationVisual.anchoredPosition;
                destinationPulseGroup = destinationVisual.GetComponent<CanvasGroup>();
            }
        }
        // Keep eligible branches alive for fading, while respecting Never visibility.
        _contentGO.SetActive(CoreContentVisibility != IndicatorVisibility.Never);
        _rotationContentGO.SetActive(RotationContentVisibility != IndicatorVisibility.Never);
        ApplyAlpha(iconGroup, 0f);
        ApplyAlpha(arrowGroup, 0f);
    }

    public override void UpdateVisualIndicator(TargetIndicator targetIndicator)
    {
        Camera camera = trackingManager != null ? trackingManager.Camera : null;
        hasViewportReference = camera != null && targetIndicator.Target != null;
        outsideViewport = false;
        if (hasViewportReference)
        {
            Vector3 viewport = camera.WorldToViewportPoint(targetIndicator.Target.position);
            outsideViewport = !(viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f);
        }
        base.UpdateVisualIndicator(targetIndicator);
    }

    public override void UpdateVisualIndicator(Pose screenPose, bool isOutsideBoundary, float distance, float lookAtDot)
    {
        float scale = Mathf.Max(0.0001f, CanvasScale);
        desiredPosition = (Vector2)screenPose.position / scale;
        outside = isOutsideBoundary;
        // Tracked arrows use the asset's current direction without rotation smoothing.
        if (presentationMode == PresentationMode.TrackedDestination ||
            !usePrefabArrowRotation)
            _rotationContent.rotation = screenPose.rotation;
        if (!hasPose || !Mathf.Approximately(previousCanvasScale, scale))
        {
            _rectTransform.anchoredPosition = desiredPosition;
            ApplyAlpha(iconGroup, GetVisibilityAlpha(CoreContentVisibility));
            ApplyAlpha(arrowGroup, GetVisibilityAlpha(RotationContentVisibility));
        }
        previousCanvasScale = scale;
        poseFrame = Time.frameCount;
        hasPose = true;
    }

    private void LateUpdate()
    {
        if (presentationMode == PresentationMode.FixedGuidance)
        {
            float fixedStep = stateFadeDuration > 0f ? Time.unscaledDeltaTime / stateFadeDuration : 1f;
            if (arrowGroup != null)
                ApplyAlpha(arrowGroup, Mathf.MoveTowards(arrowGroup.alpha, 1f, fixedStep));
            UpdateFloatingPulse();
            return;
        }
        if (!hasPose || poseFrame != Time.frameCount)
            return;
        float responseTime = outside ? offScreenResponseTime : onScreenResponseTime;
        float blend = responseTime > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / responseTime) : 1f;
        _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, desiredPosition, blend);
        float step = stateFadeDuration > 0f ? Time.unscaledDeltaTime / stateFadeDuration : 1f;
        if (iconGroup != null)
            ApplyAlpha(iconGroup, Mathf.MoveTowards(iconGroup.alpha, GetVisibilityAlpha(CoreContentVisibility), step));
        if (arrowGroup != null)
            ApplyAlpha(arrowGroup, Mathf.MoveTowards(arrowGroup.alpha, GetVisibilityAlpha(RotationContentVisibility), step));
        UpdateFloatingPulse();
    }

    private void UpdateFloatingPulse()
    {
        if (arrowVisual == null || pulseGroup == null || animationCanvas == null)
            return;
        if (!enableFloatingPulse)
        {
            arrowVisual.anchoredPosition = arrowBaseline;
            ApplyAlpha(pulseGroup, 1f);
            RestoreDestinationVisual();
            return;
        }

        pulsePhase = Mathf.Repeat(pulsePhase + Time.unscaledDeltaTime * (2f * Mathf.PI) /
            Mathf.Max(0.01f, floatCycleDuration), 2f * Mathf.PI);
        float offset = Mathf.Sin(pulsePhase) * Mathf.Max(0f, horizontalFloatAmplitude);
        // Counter the directional parent's rotation so only the image moves horizontally on screen.
        Vector3 screenHorizontal = animationCanvas.TransformVector(Vector3.right * offset);
        Vector3 localOffset = arrowVisual.parent.InverseTransformVector(screenHorizontal);
        arrowVisual.anchoredPosition = arrowBaseline + (Vector2)localOffset;

        float minimum = Mathf.Clamp01(minimumPulseOpacity);
        float maximum = Mathf.Max(minimum, Mathf.Clamp01(maximumPulseOpacity));
        // A separate child group multiplies the existing visibility/story alpha without replacing it.
        float pulse = Mathf.Lerp(minimum, maximum, (Mathf.Cos(pulsePhase) + 1f) * 0.5f);
        ApplyAlpha(pulseGroup, pulse);
        if (destinationVisual != null && destinationPulseGroup != null)
        {
            destinationVisual.anchoredPosition = destinationBaseline +
                (Vector2)destinationVisual.parent.InverseTransformVector(screenHorizontal);
            ApplyAlpha(destinationPulseGroup, pulse);
        }
    }

    private void OnDisable()
    {
        if (arrowVisual != null)
            arrowVisual.anchoredPosition = arrowBaseline;
        ApplyAlpha(pulseGroup, 1f);
        RestoreDestinationVisual();
    }

    private void RestoreDestinationVisual()
    {
        if (destinationVisual != null)
            destinationVisual.anchoredPosition = destinationBaseline;
        ApplyAlpha(destinationPulseGroup, 1f);
    }

    private float GetVisibilityAlpha(IndicatorVisibility visibility)
    {
        if (!hasViewportReference)
            return 0f;
        return visibility switch
        {
            IndicatorVisibility.Always => 1f,
            IndicatorVisibility.OutsideBoundary => outsideViewport ? 1f : 0f,
            IndicatorVisibility.InsideBoundary => outsideViewport ? 0f : 1f,
            _ => 0f
        };
    }

    private static void ApplyAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
            return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
