using UnityEngine;

/// <summary>Animates the Inventory notification badge without changing its HUD button.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class InventoryNotificationPulse : MonoBehaviour
{
    private const float CycleDuration = 1.8f;
    private const float PeakScale = 1.10f;
    private const float MinimumAlpha = 0.45f;

    private CanvasGroup group;
    private Vector3 baselineScale;
    private float baselineAlpha;
    private float elapsed;
    private bool initialized;
    private bool visible;

    private void Awake()
    {
        Initialize();
        SetVisible(false);
        group.alpha = 0f;
    }

    private void Initialize()
    {
        if (initialized) return;
        group = GetComponent<CanvasGroup>();
        baselineScale = transform.localScale;
        baselineAlpha = group.alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
        initialized = true;
    }

    public void SetVisible(bool show)
    {
        Initialize();
        if (visible == show) return;
        visible = show;
        elapsed = 0f;
        transform.localScale = baselineScale;
        group.alpha = show ? baselineAlpha : 0f;
    }

    public void SetBaselineScale(Vector3 scale)
    {
        Initialize();
        baselineScale = scale;
        transform.localScale = scale;
    }

    private void Update()
    {
        if (!visible) return;
        elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, CycleDuration);
        float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * elapsed / CycleDuration);
        transform.localScale = baselineScale * Mathf.Lerp(1f, PeakScale, wave);
        group.alpha = baselineAlpha * Mathf.Lerp(1f, MinimumAlpha, wave);
    }

    private void OnDisable()
    {
        if (!initialized) return;
        visible = false;
        elapsed = 0f;
        transform.localScale = baselineScale;
        group.alpha = 0f;
    }
}
