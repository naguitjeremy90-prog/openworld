using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class JournalEntryNotificationView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text titleText;

    public void Configure(
        CanvasGroup group,
        TMP_Text heading,
        TMP_Text category,
        TMP_Text title)
    {
        canvasGroup = group;
        headingText = heading;
        categoryText = category;
        titleText = title;
        SetVisibleAmount(0f);
    }

    public void Show(JournalEntryUnlockedInfo entry)
    {
        if (headingText != null)
            headingText.text = "BAGONG TALA SA TALA-ARAWAN";
        if (categoryText != null)
            categoryText.text = GetCategoryLabel(entry.Category);
        if (titleText != null)
            titleText.text = entry.DisplayTitle;
    }

    public void SetVisibleAmount(float amount)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = Mathf.Clamp01(amount);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static string GetCategoryLabel(JournalEntryCategory category)
    {
        switch (category)
        {
            case JournalEntryCategory.Observation:
                return "OBSERBASYON";
            case JournalEntryCategory.People:
                return "MGA TAO";
            case JournalEntryCategory.Fragment:
                return "PIRA-PIRASO";
            case JournalEntryCategory.Reflection:
                return "PAGNINILAY";
            default:
                return category.ToString().ToUpperInvariant();
        }
    }
}
