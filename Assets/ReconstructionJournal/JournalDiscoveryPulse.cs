using UnityEngine;

/// <summary>Animates the complete badge without changing the HUD button.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class JournalDiscoveryPulse : MonoBehaviour
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

    private static float EvaluateWave(float seconds)
    {
        return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * seconds / CycleDuration);
    }

    private void Update()
    {
        if (!visible) return;
        elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, CycleDuration);
        float wave = EvaluateWave(elapsed);
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