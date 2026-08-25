using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

public class CameraFocusManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;

    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    private bool isFocusing = false;

    public void FocusOn(CameraFocusPoint focusPoint, UnityEvent onFinished = null)
    {
        if (!isFocusing)
        {
            StartCoroutine(FocusRoutine(focusPoint, onFinished));
        }
    }

    private IEnumerator FocusRoutine(CameraFocusPoint focusPoint, UnityEvent onFinished)
    {
        isFocusing = true;

        // Lock Peter and force him to idle
        if (focusPoint.lockPlayer)
        {
            if (playerMovementScript != null)
                playerMovementScript.enabled = false;

            if (playerAnimator != null)
                playerAnimator.SetFloat("MoveSpeed", 0f);
        }

        // Move focus camera to the focus point
        focusCamera.transform.position = focusPoint.transform.position;
        focusCamera.transform.rotation = focusPoint.transform.rotation;

        // Switch to focus camera
        focusCamera.Priority = 20;
        normalCamera.Priority = 10;

        // Wait at the focus point
        if (focusPoint.returnMode == CameraFocusPoint.ReturnMode.AfterDuration)
        {
            yield return new WaitForSeconds(focusPoint.focusDuration);

            ReturnToNormal(focusPoint);

            // Give Cinemachine time to blend back
            yield return new WaitForSeconds(0.5f);

            // Run whatever was assigned after the focus
            onFinished?.Invoke();
        }
    }

    public void ReturnToNormal(CameraFocusPoint focusPoint)
    {
        // Return to normal gameplay camera
        focusCamera.Priority = 0;
        normalCamera.Priority = 10;

        // Unlock Peter
        if (focusPoint.lockPlayer && playerMovementScript != null)
            playerMovementScript.enabled = true;

        isFocusing = false;
    }
}
