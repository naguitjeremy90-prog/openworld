using UnityEngine;
using UnityEngine.Events;

public class CameraFocusTrigger : MonoBehaviour
{
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;

    [Header("After Focus")]
    public UnityEvent OnFocusFinished;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            focusManager.FocusOn(focusPoint, OnFocusFinished);
        }
    }
}
