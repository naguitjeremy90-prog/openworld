using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(100)] // Apply tracker alpha after the HUD restoration fade.
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
    private bool hasActiveTask;
    private readonly Queue<CompletionPresentation> completions = new Queue<CompletionPresentation>();
    private readonly Dictionary<string, string> titles = new Dictionary<string, string>();
    // The manager's public lookup only returns active tasks. Cache definitions read-only.
    private static readonly FieldInfo TaskDefinitionsField = typeof(TaskManager).GetField(
        "taskDefinitions", BindingFlags.Instance | BindingFlags.NonPublic);
    private TaskManager boundManager;
    private bool progressPending;
    private bool refreshPending;
    private bool storyWasActive;
    private float presentationAlpha;
    private float phaseElapsed;
    private Phase phase;
    private enum Phase { Idle, ProgressUp, ProgressDown, Highlight, Hold, FadeOut, NextFadeIn }

    private sealed class CompletionPresentation
    {
        public readonly string TaskId;
        public readonly string Title;
        public readonly string Objective;
        public readonly bool TaskCompleted;
        public CompletionPresentation(TaskPresentationChange change, string title)
        {
            TaskId = change.TaskId;
            Title = title;
            Objective = change.CompletedObjective;
            TaskCompleted = change.TaskCompleted;
        }
    }

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
        refreshPending = true;
        ProcessPending();
    }

    private void Start()
    {
        Subscribe();
        if (phase == Phase.Idle)
        {
            refreshPending = true;
            ProcessPending();
        }
    }

    private void OnDisable()
    {
        if (boundManager != null)
            boundManager.PresentationChanged -= HandlePresentationChange;
        boundManager = null;
        completions.Clear();
        titles.Clear();
        phase = Phase.Idle;
        progressPending = false;
        refreshPending = true;
    }

    private void LateUpdate()
    {
        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            SuppressPresentation();
            return; // GameplayHUDTarget owns hiding while suppressed.
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = hasActiveTask ? presentationAlpha : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Subscribe()
    {
        TaskManager manager = TaskManager.Instance;
        if (boundManager == manager)
            return;
        if (boundManager != null)
            boundManager.PresentationChanged -= HandlePresentationChange;
        boundManager = manager;
        titles.Clear();
        if (boundManager == null)
            return;
        if (TaskDefinitionsField?.GetValue(boundManager) is TaskData[] definitions)
        {
            foreach (TaskData definition in definitions)
                if (definition != null && !string.IsNullOrWhiteSpace(definition.TaskId))
                    titles[definition.TaskId.Trim()] = definition.Title;
        }
        boundManager.PresentationChanged += HandlePresentationChange;
        refreshPending = true;
    }

    public void ShowTask(string objective)
    {
        hasActiveTask = !string.IsNullOrWhiteSpace(objective);
        RefreshActiveTitle();
        if (typeLabel != null)
            typeLabel.text = hasActiveTask
                ? (displayedTaskType == TaskType.Main ? "PANGUNAHING GAWAIN" : "KARAGDAGANG GAWAIN")
                : string.Empty;
        if (objectiveText != null)
            objectiveText.text = objective;
        SetPresentationAlpha(hasActiveTask ? 1f : 0f);
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
        SetPresentationAlpha(0f);
    }

    private void Refresh()
    {
        if (TaskManager.Instance == null)
        {
            HideTask();
            return;
        }

        if (!TaskManager.Instance.TryGetActiveTask(
            displayedTaskType, out _, out string objective))
        {
            HideTask();
            return;
        }


        if (typeLabel != null)
            typeLabel.text = displayedTaskType == TaskType.Main ? "PANGUNAHING GAWAIN" : "KARAGDAGANG GAWAIN";
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

        refreshPending = true;
        if (change.StageChanged || change.TaskCompleted || change.ObjectiveCompleted)
        {
            titles.TryGetValue(change.TaskId.Trim(), out string title);
            completions.Enqueue(new CompletionPresentation(change, title ?? string.Empty));
        }
        else if (change.ProgressIncreased)
            progressPending = true;
        ProcessPending();
    }

    private void Update()
    {
        Subscribe();
        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            SuppressPresentation();
            return;
        }
        if (storyWasActive)
        {
            storyWasActive = false;
            refreshPending = true;
        }
        if (phase == Phase.Idle)
            ProcessPending();
        else
            AdvancePresentation();
    }

    private void SuppressPresentation()
    {
        storyWasActive = true;
        refreshPending = true;
        if (phase == Phase.ProgressUp || phase == Phase.ProgressDown)
            progressPending = true;
        // The front meaningful record is removed only after successful presentation.
        phase = Phase.Idle;
        phaseElapsed = 0f;
    }

    private void ProcessPending()
    {
        if (StorySequenceCoordinator.IsStorySequenceActive)
        {
            SuppressPresentation();
            return;
        }
        if (phase != Phase.Idle)
            return;
        if (refreshPending)
        {
            RefreshImmediate();
            refreshPending = false;
        }
        if (completions.Count > 0)
        {
            CompletionPresentation completed = completions.Peek();
            ApplyObjective(completed.Objective);
            if (titleText != null)
                titleText.text = completed.Title;
            BeginPhase(Phase.Highlight);
        }
        else if (progressPending)
        {
            progressPending = false;
            RefreshImmediate();
            if (hasActiveTask)
                BeginPhase(Phase.ProgressUp);
        }
    }

    private void ApplyObjective(string objective)
    {
        hasActiveTask = !string.IsNullOrWhiteSpace(objective);
        RefreshActiveTitle();
        if (typeLabel != null)
            typeLabel.text = hasActiveTask
                ? (displayedTaskType == TaskType.Main ? "PANGUNAHING GAWAIN" : "KARAGDAGANG GAWAIN")
                : string.Empty;
        if (objectiveText != null)
        {
            objectiveText.text = objective ?? string.Empty;
            objectiveText.color = normalObjectiveColor;
        }

        SetPresentationAlpha(hasActiveTask ? 1f : 0f);
    }

    private void RefreshActiveTitle()
    {
        if (titleText == null)
            return;

        if (!hasActiveTask ||
            TaskManager.Instance == null ||
            !TaskManager.Instance.TryGetActiveTask(
                displayedTaskType, out TaskData definition, out _))
        {
            titleText.text = string.Empty;
            return;
        }

        titleText.text = definition.Title;
    }

    private void BeginPhase(Phase next)
    {
        phase = next;
        phaseElapsed = 0f;
    }

    private void AdvancePresentation()
    {
        // Update calls this only without story ownership. No hidden realtime waits.
        phaseElapsed += Time.unscaledDeltaTime;
        float duration;
        switch (phase)
        {
            case Phase.ProgressUp:
            case Phase.ProgressDown: duration = progressPulseDuration * 0.5f; break;
            case Phase.Highlight: duration = completionColorDuration; break;
            case Phase.Hold: duration = completionHoldDuration; break;
            case Phase.FadeOut: duration = completionFadeOutDuration; break;
            default: duration = nextObjectiveFadeInDuration; break;
        }
        float t = duration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / duration);
        if (objectiveText != null)
        {
            if (phase == Phase.ProgressUp)
                objectiveText.color = Color.Lerp(normalObjectiveColor, progressPulseColor, t);
            else if (phase == Phase.ProgressDown)
                objectiveText.color = Color.Lerp(progressPulseColor, normalObjectiveColor, t);
            else if (phase == Phase.Highlight)
                objectiveText.color = Color.Lerp(normalObjectiveColor, completionHighlightColor, t);
        }
        if (phase == Phase.FadeOut)
            SetPresentationAlpha(1f - t);
        else if (phase == Phase.NextFadeIn)
            SetPresentationAlpha(t);
        if (t < 1f)
            return;
        switch (phase)
        {
            case Phase.ProgressUp: BeginPhase(Phase.ProgressDown); break;
            case Phase.Highlight: BeginPhase(Phase.Hold); break;
            case Phase.Hold: BeginPhase(Phase.FadeOut); break;
            case Phase.FadeOut:
                bool taskCompleted = completions.Peek().TaskCompleted;
                RefreshImmediate();
                refreshPending = false;
                if (!taskCompleted && hasActiveTask)
                {
                    SetPresentationAlpha(0f);
                    BeginPhase(Phase.NextFadeIn);
                }
                else
                    FinishCompletion();
                break;
            case Phase.NextFadeIn:
                FinishCompletion();
                break;
            case Phase.ProgressDown:
                BeginPhase(Phase.Idle);
                refreshPending = true;
                ProcessPending();
                break;
        }
    }

    private void FinishCompletion()
    {
        completions.Dequeue();
        BeginPhase(Phase.Idle);
        refreshPending = true;
        ProcessPending();
    }

    private void SetPresentationAlpha(float amount)
    {
        presentationAlpha = amount;
        if (canvasGroup != null && !StorySequenceCoordinator.IsStorySequenceActive)
            canvasGroup.alpha = amount;
    }
}
