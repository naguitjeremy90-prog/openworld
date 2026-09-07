using TMPro;
using UnityEngine;

public sealed class TaskTrackerUI : MonoBehaviour
{
    [SerializeField] private TaskType displayedTaskType = TaskType.Side;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMPro.TMP_Text typeLabel;
    [SerializeField] private TMPro.TMP_Text titleText;
    [SerializeField] private TMP_Text objectiveText;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (TaskManager.Instance != null)
            TaskManager.Instance.TaskChanged -= Refresh;
    }

    private void Subscribe()
    {
        if (TaskManager.Instance != null)
            TaskManager.Instance.TaskChanged -= Refresh;
        if (TaskManager.Instance != null)
            TaskManager.Instance.TaskChanged += Refresh;
    }

    public void ShowTask(string objective)
    {
        if (objectiveText != null)
            objectiveText.text = objective;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void HideTask()
    {
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
}
