using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalEntryButtonUI : MonoBehaviour
{
    public TMP_Text titleText;
    public Image backgroundImage;

    [Header("Colors")]
    public Color normalTextColor = new Color32(78, 47, 26, 255);       // dark brown
    public Color completedTextColor = new Color32(120, 120, 120, 255); // gray
    public Color normalBackgroundColor = new Color32(255, 255, 255, 255);
    public Color completedBackgroundColor = new Color32(210, 190, 160, 255);

    private JournalEntryData entryData;
    private JournalManager journalManager;

    public void Setup(JournalEntryData data, JournalManager manager)
    {
        entryData = data;
        journalManager = manager;

        if (titleText != null)
        {
            string displayTitle = data.title;

            if (data.type == EntryType.Quest)
            {
                displayTitle += data.isCompleted ? " [Completed]" : " [Active]";
            }

            titleText.text = displayTitle;
        }

        ApplyVisualState();

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickEntry);
        }
        else
        {
            Debug.LogError("JournalEntryButtonUI: No Button component found on " + gameObject.name);
        }
    }

    private void ApplyVisualState()
    {
        if (entryData == null)
            return;

        if (titleText != null)
        {
            if (entryData.type == EntryType.Quest && entryData.isCompleted)
                titleText.color = completedTextColor;
            else
                titleText.color = normalTextColor;
        }

        if (backgroundImage != null)
        {
            if (entryData.type == EntryType.Quest && entryData.isCompleted)
                backgroundImage.color = completedBackgroundColor;
            else
                backgroundImage.color = normalBackgroundColor;
        }
    }

    private void OnClickEntry()
    {
        if (journalManager != null)
        {
            journalManager.ShowEntryDetails(entryData);
        }
    }
}