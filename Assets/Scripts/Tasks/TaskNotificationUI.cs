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

    private void OnEnable()
    {
        BindToManager();
    }

    private void Start()
    {
        BindToManager();
    }

    private void OnDisable()
    {
        if (notificationRoutine != null)
        {
            StopCoroutine(notificationRoutine);
            notificationRoutine = null;
        }

        notificationVersion++;
        SetVisibleAmount(0f);
        TaskManager.Instance?.UnregisterNotificationUI(this);
    }

    private void BindToManager()
    {
        TaskManager.Instance?.RegisterNotificationUI(this);
    }

    public void ShowTaskStarted(TaskType taskType, Action onFinished = null)
    {
        ShowNotification(GetHeading(taskType, "OBTAINED"), onFinished);
    }

    public void ShowTaskUpdated(TaskType taskType, Action onFinished = null)
    {
        ShowNotification(GetHeading(taskType, "UPDATED"), onFinished);
    }

    public void ShowTaskCompleted(TaskType taskType, Action onFinished = null)
    {
        ShowNotification(GetHeading(taskType, "COMPLETED"), onFinished);
    }

    private void ShowNotification(
        string heading,
        Action onFinished)
    {
        int version = ++notificationVersion;

        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);

        headingText.text = heading;
        detailText.text = string.Empty;
        notificationRoutine = StartCoroutine(
            ShowRoutine(version, onFinished));
    }

    private static string GetHeading(TaskType taskType, string status)
    {
        string typeLabel = taskType == TaskType.Main ? "MAIN TASK" : "SIDE TASK";
        return typeLabel + " " + status;
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
