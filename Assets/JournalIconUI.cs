using UnityEngine;
using UnityEngine.UI;

public class JournalIconUI : MonoBehaviour
{
    public JournalManager journalManager;
    public GameObject notificationBadge;
    public Button button;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickJournalIcon);
        }

        // Hide once early, before other Start() methods unlock entries
        if (notificationBadge != null)
            notificationBadge.SetActive(false);
    }

    private void OnClickJournalIcon()
    {
        if (journalManager != null)
        {
            journalManager.ToggleJournal();
            HideBadge();
        }
    }

    public void ShowBadge()
    {
        if (notificationBadge != null)
            notificationBadge.SetActive(true);
    }

    public void HideBadge()
    {
        if (notificationBadge != null)
            notificationBadge.SetActive(false);
    }
}