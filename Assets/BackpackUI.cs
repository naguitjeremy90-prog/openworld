using UnityEngine;

public class BackpackUI : MonoBehaviour
{
    public GameObject backpackPanel;

    public void ToggleBackpack()
    {
        if (backpackPanel != null)
            backpackPanel.SetActive(!backpackPanel.activeSelf);
    }

    public void CloseBackpack()
    {
        if (backpackPanel != null)
            backpackPanel.SetActive(false);
    }
}