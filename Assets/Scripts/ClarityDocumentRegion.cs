using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ClarityDocumentRegion : MonoBehaviour
{
    [Header("Investigation")]
    [SerializeField] private string regionId;
    [SerializeField] private bool required = true;
    [SerializeField, Min(0.01f)] private float dwellDuration = 0.4f;
    [SerializeField] private ClarityDocumentRegion[] prerequisites =
        new ClarityDocumentRegion[0];

    [Header("Optional Feedback")]
    [SerializeField] private Graphic focusVisual;
    [SerializeField] private Color idleColor = Color.clear;
    [SerializeField] private Color focusedColor =
        new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color investigatedColor =
        new Color(1f, 1f, 1f, 0.06f);
    [SerializeField, Min(0f)] private float feedbackSpeed = 8f;

    [Header("Events")]
    [SerializeField] private UnityEvent onInvestigated;

    private readonly Vector3[] worldCorners = new Vector3[4];
    private RectTransform regionTransform;
    private float dwellProgress;
    private bool isFocused;

    public string RegionId => regionId;
    public bool Required => required;
    public float DwellDuration => dwellDuration;
    public float DwellProgress01 => IsInvestigated
        ? 1f
        : Mathf.Clamp01(dwellProgress / Mathf.Max(0.01f, dwellDuration));
    public bool IsInvestigated { get; private set; }
    public ClarityDocumentRegion[] Prerequisites => prerequisites;

    private void Awake()
    {
        regionTransform = GetComponent<RectTransform>();
        ApplyFeedbackImmediately();
    }

    private void OnValidate()
    {
        dwellDuration = Mathf.Max(0.01f, dwellDuration);

        if (prerequisites == null)
            prerequisites = new ClarityDocumentRegion[0];
    }

    private void Update()
    {
        if (focusVisual == null)
            return;

        Color targetColor = GetFeedbackColor();
        focusVisual.color = feedbackSpeed <= 0f
            ? targetColor
            : Color.Lerp(
                focusVisual.color,
                targetColor,
                Mathf.Clamp01(feedbackSpeed * Time.unscaledDeltaTime));
    }

    internal void BeginInvestigationLifecycle()
    {
        IsInvestigated = false;
        dwellProgress = 0f;
        isFocused = false;
        ApplyFeedbackImmediately();
    }

    internal void ResetDwell()
    {
        if (IsInvestigated)
            return;

        dwellProgress = 0f;
        isFocused = false;
    }

    internal bool ProcessLens(
        Vector2 lensCenterScreenPosition,
        float lensRadiusScreenPixels,
        float unscaledDeltaTime,
        Camera uiCamera)
    {
        if (IsInvestigated || !gameObject.activeInHierarchy)
            return false;

        bool overlapping = OverlapsLens(
            lensCenterScreenPosition,
            lensRadiusScreenPixels,
            uiCamera);

        isFocused = overlapping;

        if (!overlapping)
        {
            dwellProgress = 0f;
            return false;
        }

        dwellProgress += Mathf.Max(0f, unscaledDeltaTime);
        if (dwellProgress < dwellDuration)
            return false;

        dwellProgress = dwellDuration;
        IsInvestigated = true;
        isFocused = false;
        onInvestigated?.Invoke();
        return true;
    }

    internal bool ArePrerequisitesSatisfied()
    {
        for (int i = 0; i < prerequisites.Length; i++)
        {
            ClarityDocumentRegion prerequisite = prerequisites[i];
            if (prerequisite == null || !prerequisite.IsInvestigated)
                return false;
        }

        return true;
    }

    private bool OverlapsLens(
        Vector2 lensCenterScreenPosition,
        float lensRadiusScreenPixels,
        Camera uiCamera)
    {
        if (regionTransform == null)
            regionTransform = GetComponent<RectTransform>();

        regionTransform.GetWorldCorners(worldCorners);

        Vector2 minimum = new Vector2(
            float.PositiveInfinity,
            float.PositiveInfinity);
        Vector2 maximum = new Vector2(
            float.NegativeInfinity,
            float.NegativeInfinity);

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                worldCorners[i]);
            minimum = Vector2.Min(minimum, screenPoint);
            maximum = Vector2.Max(maximum, screenPoint);
        }

        Vector2 closestPoint = new Vector2(
            Mathf.Clamp(lensCenterScreenPosition.x, minimum.x, maximum.x),
            Mathf.Clamp(lensCenterScreenPosition.y, minimum.y, maximum.y));

        float radius = Mathf.Max(0f, lensRadiusScreenPixels);
        return (lensCenterScreenPosition - closestPoint).sqrMagnitude <=
               radius * radius;
    }

    private Color GetFeedbackColor()
    {
        if (IsInvestigated)
            return investigatedColor;

        return isFocused ? focusedColor : idleColor;
    }

    private void ApplyFeedbackImmediately()
    {
        if (focusVisual != null)
            focusVisual.color = GetFeedbackColor();
    }
}
