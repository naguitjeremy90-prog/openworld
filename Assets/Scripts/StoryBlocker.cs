using UnityEngine;

public class StoryBlocker : MonoBehaviour
{
    public void Unlock()
    {
        gameObject.SetActive(false);
    }
}
