using UnityEngine;

public class StoryBlocker : MonoBehaviour
{
    [Header("Story State (Optional)")]
    [SerializeField] private string unlockStoryFlagId;

    private void OnEnable()
    {
        if (SessionStoryState.GetFlag(unlockStoryFlagId))
            gameObject.SetActive(false);
    }

    public void Unlock()
    {
        SessionStoryState.SetFlag(unlockStoryFlagId, true);
        gameObject.SetActive(false);
    }
}
