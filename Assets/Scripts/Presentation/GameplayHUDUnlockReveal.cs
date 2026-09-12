using System;
using System.Collections;
using UnityEngine;

/// <summary>Plays a one-shot, relative-scale reveal for an available HUD root.</summary>
[DisallowMultipleComponent]
public sealed class GameplayHUDUnlockReveal : MonoBehaviour
{
    [SerializeField, Min(0f)] private float growDuration = 0.4f;
    [SerializeField, Min(0f)] private float settleDuration = 0.2f;
    [SerializeField, Min(0f)] private float storySequenceRestoreDelay = 0.2f;
    [SerializeField] private float startScaleMultiplier = 0.88f;
    [SerializeField] private float overshootScaleMultiplier = 1.07f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Coroutine revealRoutine;
    private Vector3 normalScale;
    private float normalAlpha = 1f;
    private bool normalInteractable;
    private bool normalBlocksRaycasts;
    private bool capturedNormalState;

    /// <summary>Raised after the reveal has settled back to its exact captured scale.</summary>
    public event Action RevealCompleted;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        CaptureNormalState();
    }

    public void Configure(CanvasGroup group)
    {
        if (group != null)
            canvasGroup = group;

        CaptureNormalState();
    }

    public bool PlayReveal()
    {
        if (!isActiveAndEnabled || rectTransform == null || canvasGroup == null ||
            StorySequenceCoordinator.IsStorySequenceActive)
        {
            return false;
        }

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine());
        return true;
    }

    private void CaptureNormalState()
    {
        if (capturedNormalState || rectTransform == null || canvasGroup == null)
            return;

        normalScale = rectTransform.localScale;
        normalAlpha = canvasGroup.alpha;
        normalInteractable = canvasGroup.interactable;
        normalBlocksRaycasts = canvasGroup.blocksRaycasts;
        capturedNormalState = true;
    }

    private IEnumerator RevealRoutine()
    {
        Vector3 startScale = normalScale * startScaleMultiplier;
        Vector3 overshootScale = normalScale * overshootScaleMultiplier;

        rectTransform.localScale = startScale;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            if (StorySequenceCoordinator.IsStorySequenceActive)
            {
                CancelForStorySequence();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = growDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / growDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = Mathf.Lerp(0f, normalAlpha, eased);
            rectTransform.localScale = Vector3.Lerp(startScale, overshootScale, eased);
            yield return null;
        }

        canvasGroup.alpha = normalAlpha;
        rectTransform.localScale = overshootScale;

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            if (StorySequenceCoordinator.IsStorySequenceActive)
            {
                CancelForStorySequence();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = settleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settleDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            rectTransform.localScale = Vector3.Lerp(overshootScale, normalScale, eased);
            yield return null;
        }

        rectTransform.localScale = normalScale;
        canvasGroup.alpha = normalAlpha;
        canvasGroup.interactable = normalInteractable;
        canvasGroup.blocksRaycasts = normalBlocksRaycasts;
        revealRoutine = null;
        RevealCompleted?.Invoke();
    }

    private void CancelForStorySequence()
    {
        rectTransform.localScale = normalScale;
        revealRoutine = StartCoroutine(RestoreAfterStorySequence());
    }

    private IEnumerator RestoreAfterStorySequence()
    {
        while (StorySequenceCoordinator.IsStorySequenceActive)
            yield return null;

        float elapsed = 0f;
        while (elapsed < storySequenceRestoreDelay)
        {
            if (StorySequenceCoordinator.IsStorySequenceActive)
            {
                elapsed = 0f;
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!StorySequenceCoordinator.IsStorySequenceActive)
        {
            rectTransform.localScale = normalScale;
            canvasGroup.alpha = normalAlpha;
            canvasGroup.interactable = normalInteractable;
            canvasGroup.blocksRaycasts = normalBlocksRaycasts;
            RevealCompleted?.Invoke();
        }

        revealRoutine = null;
    }
}
