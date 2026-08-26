using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

public class CameraFocusManager : MonoBehaviour
{
    [Header("Cinemachine Cameras (Optional)")]
    [SerializeField] private CinemachineCamera normalCinemachineCamera;
    [SerializeField] private CinemachineCamera focusCinemachineCamera;

    [Header("Regular Camera (Optional)")]
    [SerializeField] private Camera normalCamera;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    private bool isFocusing = false;
    private bool usingCinemachine = false;

    // True normal camera position
    private Vector3 normalCameraPosition;
    private Quaternion normalCameraRotation;

    private Coroutine cameraCoroutine;

    private void Start()
    {
        usingCinemachine =
            normalCinemachineCamera != null &&
            focusCinemachineCamera != null;

        // Save regular camera's REAL gameplay position ONCE
        if (!usingCinemachine && normalCamera != null)
        {
            normalCameraPosition = normalCamera.transform.position;
            normalCameraRotation = normalCamera.transform.rotation;
        }
    }

    public void FocusOn(
        CameraFocusPoint focusPoint,
        UnityEvent onFinished = null)
    {
        if (isFocusing)
            return;

        cameraCoroutine =
            StartCoroutine(FocusRoutine(focusPoint, onFinished));
    }

    private IEnumerator FocusRoutine(
        CameraFocusPoint focusPoint,
        UnityEvent onFinished)
    {
        isFocusing = true;

        // Lock Peter and force idle
        if (focusPoint.lockPlayer)
        {
            if (playerMovementScript != null)
                playerMovementScript.enabled = false;

            if (playerAnimator != null)
                playerAnimator.SetFloat("MoveSpeed", 0f);
        }

        // CINEMACHINE
        if (usingCinemachine)
        {
            focusCinemachineCamera.transform.position =
                focusPoint.transform.position;

            focusCinemachineCamera.transform.rotation =
                focusPoint.transform.rotation;

            focusCinemachineCamera.Priority = 20;
            normalCinemachineCamera.Priority = 10;
        }

        // REGULAR CAMERA
        else if (normalCamera != null)
        {
            yield return StartCoroutine(
                MoveRegularCamera(
                    focusPoint.transform.position,
                    focusPoint.transform.rotation,
                    focusPoint.moveSpeed));
        }

        // For automatic scenery focus
        if (focusPoint.returnMode ==
            CameraFocusPoint.ReturnMode.AfterDuration)
        {
            yield return new WaitForSeconds(
                focusPoint.focusDuration);

            yield return StartCoroutine(
                ReturnRoutine(focusPoint));

            onFinished?.Invoke();
        }
    }

    public void ReturnToNormal(CameraFocusPoint focusPoint)
    {
        if (cameraCoroutine != null)
            StopCoroutine(cameraCoroutine);

        cameraCoroutine =
            StartCoroutine(ReturnRoutine(focusPoint));
    }

    private IEnumerator ReturnRoutine(
        CameraFocusPoint focusPoint)
    {
        // CINEMACHINE
        if (usingCinemachine)
        {
            focusCinemachineCamera.Priority = 0;
            normalCinemachineCamera.Priority = 10;

            yield return new WaitForSeconds(0.5f);
        }

        // REGULAR CAMERA
        else if (normalCamera != null)
        {
            yield return StartCoroutine(
                MoveRegularCamera(
                    normalCameraPosition,
                    normalCameraRotation,
                    focusPoint.returnSpeed));
        }

        // Unlock Peter
        if (focusPoint.lockPlayer &&
            playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        isFocusing = false;
        cameraCoroutine = null;
    }

    private IEnumerator MoveRegularCamera(
        Vector3 targetPosition,
        Quaternion targetRotation,
        float speed)
    {
        while (
            Vector3.Distance(
                normalCamera.transform.position,
                targetPosition) > 0.01f ||
            Quaternion.Angle(
                normalCamera.transform.rotation,
                targetRotation) > 0.1f)
        {
            // Move at a consistent speed
            normalCamera.transform.position =
                Vector3.MoveTowards(
                    normalCamera.transform.position,
                    targetPosition,
                    speed * Time.deltaTime);

            // Rotate smoothly
            normalCamera.transform.rotation =
                Quaternion.RotateTowards(
                    normalCamera.transform.rotation,
                    targetRotation,
                    speed * 30f * Time.deltaTime);

            yield return null;
        }

        // Guarantee exact final position
        normalCamera.transform.position = targetPosition;
        normalCamera.transform.rotation = targetRotation;
    }
}
