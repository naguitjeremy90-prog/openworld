using TMPro;
using UnityEngine;

public sealed class TaskTrackerUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text objectiveText;

    private void Awake()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void ShowTask(string objective)
    {
        objectiveText.text = objective;
        canvasGroup.alpha = 1f;
    }

    public void HideTask()
    {
        objectiveText.text = string.Empty;
        canvasGroup.alpha = 0f;
    }
}
