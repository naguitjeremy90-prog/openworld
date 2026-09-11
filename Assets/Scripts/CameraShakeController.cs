using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraShakeController : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCameraOffset cameraOffset;
    [SerializeField, Min(0.1f)] private float shakeFrequency = 24f;

    private Coroutine shakeRoutine;

    public bool IsShaking => shakeRoutine != null;

    private void Awake()
    {
        if (cameraOffset == null)
            cameraOffset = FindAnyObjectByType<CinemachineCameraOffset>();
    }

    public Coroutine Shake(float duration, float strength)
    {
        if (cameraOffset == null || duration <= 0f || strength <= 0f)
            return null;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

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
            cameraOffset.Offset = new Vector3(horizontal, vertical, 0f) * strength * envelope;
            yield return null;
        }

        cameraOffset.Offset = Vector3.zero;
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        if (cameraOffset != null)
            cameraOffset.Offset = Vector3.zero;

        shakeRoutine = null;
    }
}
