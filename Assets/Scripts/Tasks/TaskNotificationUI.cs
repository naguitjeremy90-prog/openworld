using System;
using System.Collections;
using TMPro;
using UnityEngine;

public sealed class TaskNotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float visibleDuration = 2.75f;

    private Coroutine notificationRoutine;
    private int notificationVersion;

    private void Awake()
    {
        SetVisibleAmount(0f);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void ShowTaskStarted(string objective, Action onFinished = null)
    {
        ShowNotification("BAGONG GAWAIN", objective, onFinished);
    }

    public void ShowTaskUpdated(string objective, Action onFinished = null)
    {
        ShowNotification("NA-UPDATE ANG GAWAIN", objective, onFinished);
    }

    public void ShowTaskCompleted(string title, Action onFinished = null)
    {
        ShowNotification("NATAPOS ANG GAWAIN", title, onFinished);
    }

    private void ShowNotification(
        string heading,
        string detail,
        Action onFinished)
    {
        int version = ++notificationVersion;

        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);

        headingText.text = heading;
        detailText.text = detail;
        notificationRoutine = StartCoroutine(
            ShowRoutine(version, onFinished));
    }

    private IEnumerator ShowRoutine(int version, Action onFinished)
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(visibleDuration);
        yield return Fade(1f, 0f);

        if (version != notificationVersion)
            yield break;

        notificationRoutine = null;
        onFinished?.Invoke();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            SetVisibleAmount(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetVisibleAmount(Mathf.Lerp(from, to, elapsed / fadeDuration));
            yield return null;
        }

        SetVisibleAmount(to);
    }

    private void SetVisibleAmount(float amount)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = amount;
    }
}
