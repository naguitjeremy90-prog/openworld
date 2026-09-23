using System.Collections;
using TMPro;
using UnityEngine;

public sealed class TaskTrackerUI : MonoBehaviour
{
    [SerializeField] private TaskType displayedTaskType = TaskType.Side;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMPro.TMP_Text typeLabel;
    [SerializeField] private TMPro.TMP_Text titleText;
    [SerializeField] private TMP_Text objectiveText;

    [Header("Progress Presentation")]
    [SerializeField] private Color progressPulseColor = Color.white;
    [SerializeField, Min(0f)] private float progressPulseDuration = 0.45f;

    [Header("Completion Presentation")]
    [SerializeField] private Color completionHighlightColor = new Color(1f, 0.86f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float completionColorDuration = 0.2f;
    [SerializeField, Min(0f)] private float completionHoldDuration = 0.35f;
    [SerializeField, Min(0f)] private float completionFadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float nextObjectiveFadeInDuration = 0.35f;

    private Color normalObjectiveColor = Color.white;
    private Coroutine presentationRoutine;
    private TaskPresentationChange queuedChange;
    private bool hasActiveTask;

    private void Awake()
    {
        GameplayHUDTarget target = GetComponent<GameplayHUDTarget>();
        if (target == null)
            target = gameObject.AddComponent<GameplayHUDTarget>();
        target.Configure(canvasGroup);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (objectiveText != null)
            normalObjectiveColor = objectiveText.color;
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshImmediate();
    }

    private void Start()
    {
        Subscribe();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (TaskManager.Instance != null)
            TaskManager.Instance.PresentationChanged -= HandlePresentationChange;

        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);
        presentationRoutine = null;
        queuedChange = null;
    }

    private void LateUpdate()
    {
        // GameplayHUDTarget can restore a previously visible canvas after a
        // story sequence. Keep an empty tracker hidden until a task exists.
        if (!hasActiveTask && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Subscribe()
    {
        if (TaskManager.Instance != null)
            TaskManager.Instance.PresentationChanged -= HandlePresentationChange;
        if (TaskManager.Instance != null)
            TaskManager.Instance.PresentationChanged += HandlePresentationChange;
    }

    public void ShowTask(string objective)
    {
        hasActiveTask = !string.IsNullOrWhiteSpace(objective);
        if (typeLabel != null)
            typeLabel.text = hasActiveTask
                ? (displayedTaskType == TaskType.Main ? "MAIN TASK" : "SIDE TASK")
                : string.Empty;
        if (objectiveText != null)
            objectiveText.text = objective;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void HideTask()
    {
        hasActiveTask = false;
        if (typeLabel != null)
            typeLabel.text = string.Empty;
        if (objectiveText != null)
            objectiveText.text = string.Empty;
        if (titleText != null)
            titleText.text = string.Empty;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Refresh()
    {
        if (TaskManager.Instance == null)
        {
            HideTask();
            return;
        }

        if (!TaskManager.Instance.TryGetActiveTask(
            displayedTaskType, out TaskData definition, out string objective))
        {
            HideTask();
            return;
        }


        if (typeLabel != null)
            typeLabel.text = displayedTaskType == TaskType.Main ? "MAIN TASK" : "SIDE TASK";
        if (titleText != null)
            titleText.text = definition.Title;
        ShowTask(objective);
    }

    private void RefreshImmediate()
    {
        Refresh();
        if (objectiveText != null)
            objectiveText.color = normalObjectiveColor;
    }

    private void HandlePresentationChange(TaskPresentationChange change)
    {
        if (change == null || change.TaskType != displayedTaskType)
            return;

        queuedChange = change;
        if (presentationRoutine == null)
            presentationRoutine = StartCoroutine(ProcessPresentationQueue());
    }

    private IEnumerator ProcessPresentationQueue()
    {
        while (queuedChange != null)
        {
            TaskPresentationChange change = queuedChange;
            queuedChange = null;

            if (change.StageChanged || change.TaskCompleted)
                yield return PlayCompletion(change);
            else if (change.ProgressIncreased)
                yield return PlayProgressPulse(change);
            else
                ApplyObjective(change.NewObjective);
        }

        presentationRoutine = null;
    }

    private IEnumerator PlayProgressPulse(TaskPresentationChange change)
    {
        ApplyObjective(change.NewObjective);
        yield return LerpObjectiveColor(normalObjectiveColor, progressPulseColor, progressPulseDuration * 0.5f);
        yield return LerpObjectiveColor(progressPulseColor, normalObjectiveColor, progressPulseDuration * 0.5f);
    }

    private IEnumerator PlayCompletion(TaskPresentationChange change)
    {
        ApplyObjective(change.CompletedObjective);
        yield return LerpObjectiveColor(normalObjectiveColor, completionHighlightColor, completionColorDuration);

        if (completionHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(completionHoldDuration);

        yield return FadeCanvas(1f, 0f, completionFadeOutDuration);

        if (change.TaskCompleted || string.IsNullOrEmpty(change.NewObjective))
        {
            HideTask();
            yield break;
        }

        ApplyObjective(change.NewObjective);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        yield return FadeCanvas(0f, 1f, nextObjectiveFadeInDuration);
    }

    private void ApplyObjective(string objective)
    {
        hasActiveTask = !string.IsNullOrWhiteSpace(objective);
        if (typeLabel != null)
            typeLabel.text = hasActiveTask
                ? (displayedTaskType == TaskType.Main ? "MAIN TASK" : "SIDE TASK")
                : string.Empty;
        if (objectiveText != null)
        {
            objectiveText.text = objective ?? string.Empty;
            objectiveText.color = normalObjectiveColor;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = string.IsNullOrEmpty(objective) ? 0f : 1f;
    }

    private IEnumerator LerpObjectiveColor(Color from, Color to, float duration)
    {
        if (objectiveText == null)
            yield break;

        if (duration <= 0f)
        {
            objectiveText.color = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            objectiveText.color = Color.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        objectiveText.color = to;
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
