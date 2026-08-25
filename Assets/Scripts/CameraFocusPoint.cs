using UnityEngine;

public class CameraFocusPoint : MonoBehaviour
{
    public enum ReturnMode
    {
        AfterDuration,
        AfterConversation,
        Manual
    }

    [Header("Focus Settings")]
    public float focusDuration = 2f;
    public float moveSpeed = 3f;
    public float returnSpeed = 3f;

    [Header("Behavior")]
    public ReturnMode returnMode = ReturnMode.AfterDuration;
    public bool lockPlayer = true;
}
