using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CameraFocusTrigger : MonoBehaviour
{
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;

    [Header("Story Event")]
    [Tooltip("Optional runtime ID that keeps this event completed across scene reloads.")]
    [SerializeField] private string eventId;

    [Header("After Focus")]
    public UnityEvent OnFocusFinished;

    private static readonly HashSet<string> completedEventIds = new HashSet<string>();
    private bool hasTriggered = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCompletedEventIds()
    {
        completedEventIds.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || IsEventCompleted())
            return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            if (!string.IsNullOrEmpty(eventId))
                completedEventIds.Add(eventId);

            focusManager.FocusOn(focusPoint, OnFocusFinished);
        }
    }

    private bool IsEventCompleted()
    {
        return !string.IsNullOrEmpty(eventId) &&
               completedEventIds.Contains(eventId);
    }
}
