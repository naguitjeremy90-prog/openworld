using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraShakeController : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCameraOffset cameraOffset;
    [SerializeField] private Camera directCamera;
    [SerializeField, Min(0.1f)] private float shakeFrequency = 24f;

    private Coroutine shakeRoutine;
    private Vector3 directCameraStartLocalPosition;
    private bool directCameraPositionCaptured;

    public bool IsShaking => shakeRoutine != null;

    private void Awake()
    {
        if (cameraOffset == null && directCamera == null)
            cameraOffset = FindAnyObjectByType<CinemachineCameraOffset>();
    }

    public Coroutine Shake(float duration, float strength)
    {
        if ((cameraOffset == null && directCamera == null) || duration <= 0f || strength <= 0f)
            return null;

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreCameraPresentation();
        }

        if (cameraOffset == null && directCamera != null)
        {
            directCameraStartLocalPosition = directCamera.transform.localPosition;
            directCameraPositionCaptured = true;
        }
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
        return shakeRoutine;
    }

    public IEnumerator ShakeAndWait(float duration, float strength)
    {
        Shake(duration, strength);
        while (IsShaking)
            yield return null;
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float horizontalSeed = Random.value * 1000f;
        float verticalSeed = Random.value * 1000f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float envelope = 1f - Mathf.SmoothStep(0f, 1f, normalizedTime);
            float sampleTime = elapsed * shakeFrequency;
            float horizontal = Mathf.PerlinNoise(horizontalSeed, sampleTime) * 2f - 1f;
            float vertical = Mathf.PerlinNoise(verticalSeed, sampleTime) * 2f - 1f;
            Vector3 shakeOffset = new Vector3(horizontal, vertical, 0f) * strength * envelope;
            if (cameraOffset != null)
                cameraOffset.Offset = shakeOffset;
            else if (directCamera != null)
                directCamera.transform.localPosition = directCameraStartLocalPosition + shakeOffset;
            yield return null;
        }

        RestoreCameraPresentation();
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        RestoreCameraPresentation();

        shakeRoutine = null;
    }

    private void RestoreCameraPresentation()
    {
        if (cameraOffset != null)
            cameraOffset.Offset = Vector3.zero;

        if (directCameraPositionCaptured && directCamera != null)
            directCamera.transform.localPosition = directCameraStartLocalPosition;

        directCameraPositionCaptured = false;
    }
}
