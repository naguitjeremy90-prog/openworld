using UnityEngine;

public class QuestNPCUnlocker : MonoBehaviour
{
    public GameObject npcToShow;

    void Start()
    {
        npcToShow.SetActive(false);
    }

    public void ShowNPC()
    {
        npcToShow.SetActive(true);
    }
}