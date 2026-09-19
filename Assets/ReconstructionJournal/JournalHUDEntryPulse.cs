using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class JournalHUDEntryPulse : MonoBehaviour
{
    [SerializeField, Min(1f)] private float peakScaleMultiplier = 1.08f;
    [SerializeField, Min(0f)] private float growDuration = 0.14f;
    [SerializeField, Min(0f)] private float settleDuration = 0.2f;

    private RectTransform target;
    private Vector3 normalScale;
    private bool capturedScale;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        Configure(transform as RectTransform);
    }

    public void Configure(RectTransform pulseTarget)
    {
        target = pulseTarget != null ? pulseTarget : transform as RectTransform;
        if (!capturedScale && target != null)
        {
            normalScale = target.localScale;
            capturedScale = true;
        }
    }

    public bool PlayPulse()
    {
        if (!isActiveAndEnabled || target == null || !capturedScale)
            return false;

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            target.localScale = normalScale;
        }

        pulseRoutine = StartCoroutine(PulseRoutine());
        return true;
    }

    private IEnumerator PulseRoutine()
    {
        Vector3 peakScale = normalScale * peakScaleMultiplier;
        yield return AnimateScale(normalScale, peakScale, growDuration);
        yield return AnimateScale(peakScale, normalScale, settleDuration);
        target.localScale = normalScale;
        pulseRoutine = null;
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        target.localScale = to;
    }

    private void OnDisable()
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);
        pulseRoutine = null;

        if (target != null && capturedScale)
            target.localScale = normalScale;
    }
}
