using UnityEngine;
using UnityEngine.Playables;

public class AndayTimelineTriggerTest : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.CompareTag("Player") || body.name != "FInalChar1")
            return;

        Debug.Log("[AndayTimelineTest] Miguel entered trigger.");
        director.time = 0;
        Debug.Log("[AndayTimelineTest] Playing TimelineAnday.");
        director.Play();
    }
}
