using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

public class CameraFocusManager : MonoBehaviour
{
    // The existing completion callback is also the identity of the originating request.
    public event System.Action<CameraFocusPoint, UnityEvent> FocusStarted;
    private bool notifyingFocusStarted;

    [Header("Cinemachine Cameras (Optional)")]
    [SerializeField] private CinemachineCamera normalCinemachineCamera;
    [SerializeField] private CinemachineCamera focusCinemachineCamera;

    [Header("Regular Camera (Optional)")]
    [SerializeField] private Camera normalCamera;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    [Header("Return Completion")]
    [SerializeField, Min(0.1f)] private float cinemachineReturnTimeout = 8f;

    private bool isFocusing = false;
    private bool usingCinemachine = false;
    private bool isReturning = false;

    private CameraFocusPoint activeFocusPoint;
    private UnityEvent focusFinishedCallback;

    // True normal camera position
    private Vector3 normalCameraPosition;
    private Quaternion normalCameraRotation;

    private Coroutine cameraCoroutine;

    public bool IsFocusing => isFocusing;
    public bool LastReturnCompleted { get; private set; }

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
        TryFocusOn(focusPoint, onFinished);
    }

    internal bool TryFocusOn(
        CameraFocusPoint focusPoint,
        UnityEvent onFinished = null)
    {
        if (notifyingFocusStarted || !isActiveAndEnabled || isFocusing || focusPoint == null)
            return false;

        isFocusing = true;
        isReturning = false;
        LastReturnCompleted = false;
        activeFocusPoint = focusPoint;
        focusFinishedCallback = onFinished;

        cameraCoroutine =
            StartCoroutine(FocusRoutine(focusPoint));
        return true;
    }

    private IEnumerator FocusRoutine(CameraFocusPoint focusPoint)
    {
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
            if (focusCinemachineCamera == null || !focusCinemachineCamera.isActiveAndEnabled ||
                normalCinemachineCamera == null || !normalCinemachineCamera.isActiveAndEnabled)
            {
                Debug.LogWarning("Camera focus could not start because a Cinemachine camera is unavailable.", this);
                yield return ReturnRoutine(focusPoint);
                yield break;
            }

            focusCinemachineCamera.transform.position =
                focusPoint.transform.position;

            focusCinemachineCamera.transform.rotation =
                focusPoint.transform.rotation;

            focusCinemachineCamera.Priority = 20;
            normalCinemachineCamera.Priority = 10;
            NotifyFocusStarted(focusPoint);
        }

        // REGULAR CAMERA
        else if (normalCamera != null)
        {
            bool cameraAvailable = normalCamera.isActiveAndEnabled;
            Coroutine moveRoutine = StartCoroutine(
                MoveRegularCamera(
                    focusPoint.transform.position,
                    focusPoint.transform.rotation,
                    focusPoint.moveSpeed));
            if (cameraAvailable && normalCamera != null && normalCamera.isActiveAndEnabled)
                NotifyFocusStarted(focusPoint);
            yield return moveRoutine;
        }

        // For automatic scenery focus
        if (focusPoint.returnMode ==
            CameraFocusPoint.ReturnMode.AfterDuration)
        {
            yield return new WaitForSeconds(
                focusPoint.focusDuration);

            isReturning = true;
            yield return ReturnRoutine(focusPoint);
        }
    }

    public void ReturnToNormal(CameraFocusPoint focusPoint)
    {
        TryReturnToNormal(focusPoint);
    }

    internal bool TryReturnToNormal(CameraFocusPoint focusPoint)
    {
        if (notifyingFocusStarted || !isFocusing || isReturning || focusPoint == null ||
            focusPoint != activeFocusPoint)
        {
            return false;
        }

        if (cameraCoroutine != null)
            StopCoroutine(cameraCoroutine);

        isReturning = true;
        cameraCoroutine =
            StartCoroutine(ReturnRoutine(focusPoint));
        return true;
    }

    private void NotifyFocusStarted(CameraFocusPoint focusPoint)
    {
        var listeners = FocusStarted;
        if (listeners == null)
            return;
        UnityEvent requestCallback = focusFinishedCallback;
        notifyingFocusStarted = true;
        try
        {
            foreach (System.Action<CameraFocusPoint, UnityEvent> listener in listeners.GetInvocationList())
            {
                try { listener(focusPoint, requestCallback); }
                catch (System.Exception exception) { Debug.LogException(exception, this); }
            }
        }
        finally { notifyingFocusStarted = false; }
    }

    private IEnumerator ReturnRoutine(
        CameraFocusPoint focusPoint)
    {
        LastReturnCompleted = false;
        // CINEMACHINE
        if (usingCinemachine)
        {
            RestoreCinemachinePriorities();

            Camera renderingCamera = Camera.main;
            CinemachineBrain brain = renderingCamera != null
                ? renderingCamera.GetComponent<CinemachineBrain>()
                : null;
            if (brain == null || !renderingCamera.isActiveAndEnabled ||
                normalCinemachineCamera == null || !normalCinemachineCamera.isActiveAndEnabled)
            {
                Debug.LogWarning("Camera focus return could not be verified: rendering Brain or normal Cinemachine camera is unavailable.", this);
            }
            else
            {
                // Allow Cinemachine to process the new priorities before testing completion.
                yield return null;
                float deadline = Time.unscaledTime + cinemachineReturnTimeout;
                while (Time.unscaledTime < deadline && brain != null &&
                       normalCinemachineCamera != null &&
                       brain.isActiveAndEnabled && normalCinemachineCamera.isActiveAndEnabled &&
                       (brain.IsBlending ||
                        !ReferenceEquals(brain.ActiveVirtualCamera, normalCinemachineCamera)))
                    yield return null;

                LastReturnCompleted = brain != null && normalCinemachineCamera != null &&
                    brain.isActiveAndEnabled && normalCinemachineCamera.isActiveAndEnabled &&
                    !brain.IsBlending &&
                    ReferenceEquals(brain.ActiveVirtualCamera, normalCinemachineCamera);
                if (!LastReturnCompleted)
                    Debug.LogWarning("Camera focus return did not complete before the timeout or was interrupted.", this);
            }

            RestoreCinemachinePriorities();
        }

        // REGULAR CAMERA
        else if (normalCamera != null)
        {
            yield return StartCoroutine(
                MoveRegularCamera(
                    normalCameraPosition,
                    normalCameraRotation,
                    focusPoint.returnSpeed));
            LastReturnCompleted = true;
        }

        // Unlock Peter
        if (focusPoint.lockPlayer &&
            playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        UnityEvent finishedCallback = focusFinishedCallback;

        isFocusing = false;
        isReturning = false;
        activeFocusPoint = null;
        focusFinishedCallback = null;
        cameraCoroutine = null;

        finishedCallback?.Invoke();
    }

    private void RestoreCinemachinePriorities()
    {
        if (focusCinemachineCamera != null)
            focusCinemachineCamera.Priority = 0;
        if (normalCinemachineCamera != null)
            normalCinemachineCamera.Priority = 10;
    }

    private void OnDisable()
    {
        if (!isFocusing)
            return;

        if (cameraCoroutine != null)
            StopCoroutine(cameraCoroutine);
        if (usingCinemachine)
            RestoreCinemachinePriorities();
        if (activeFocusPoint != null && activeFocusPoint.lockPlayer && playerMovementScript != null)
            playerMovementScript.enabled = true;

        UnityEvent finishedCallback = focusFinishedCallback;
        LastReturnCompleted = false;
        isFocusing = false;
        isReturning = false;
        activeFocusPoint = null;
        focusFinishedCallback = null;
        cameraCoroutine = null;
        Debug.LogWarning("Camera focus was interrupted because its manager was disabled.", this);
        finishedCallback?.Invoke();
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
