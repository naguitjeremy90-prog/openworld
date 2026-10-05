using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders and animates the project's shared soft curved eyelid effect.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class SoftCurvedEyelidController : MonoBehaviour
{
    private const string ShaderResourcePath = "Clarity/MiguelWakeUpEyelids";

    private static readonly int OpenId = Shader.PropertyToID("_Open");
    private static readonly int FeatherId = Shader.PropertyToID("_Feather");
    private static readonly int CurvatureId = Shader.PropertyToID("_Curvature");

    [SerializeField] private Image eyelidImage;
    [SerializeField, Range(0.001f, 0.08f)] private float eyelidFeather = 0.022f;
    [SerializeField, Range(0f, 0.8f)] private float eyelidCurvature = 0.32f;

    private Material runtimeMaterial;
    private Material originalMaterial;
    private Coroutine animationCoroutine;
    private float openingAmount;
    private bool initialized;
    private bool initializationFailed;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>Sets the eyelids to fully closed without animating.</summary>
    public void SetClosedImmediately()
    {
        StopAnimation();
        if (!EnsureInitialized())
            return;

        openingAmount = 0f;
        ApplyOpeningAmount();
    }

    /// <summary>Sets the eyelids to fully open without animating.</summary>
    public void SetOpenImmediately()
    {
        StopAnimation();
        if (!EnsureInitialized())
            return;

        openingAmount = 1f;
        ApplyOpeningAmount();
    }

    /// <summary>Opens the eyelids smoothly from their current amount.</summary>
    public Coroutine Open(float duration)
    {
        return AnimateTo(1f, duration);
    }

    /// <summary>Closes the eyelids smoothly from their current amount.</summary>
    public Coroutine Close(float duration)
    {
        return AnimateTo(0f, duration);
    }

    /// <summary>
    /// Animates to any opening amount. This primitive lets a sequence retain
    /// its own choreography while sharing the same rendering and easing.
    /// </summary>
    public Coroutine AnimateTo(float targetOpeningAmount, float duration)
    {
        if (!EnsureInitialized())
            return null;

        StopAnimation();
        targetOpeningAmount = Mathf.Clamp01(targetOpeningAmount);
        if (duration <= 0f)
        {
            openingAmount = targetOpeningAmount;
            ApplyOpeningAmount();
            return null;
        }

        animationCoroutine = StartCoroutine(
            AnimateRoutine(openingAmount, targetOpeningAmount, duration));
        return animationCoroutine;
    }

    private IEnumerator AnimateRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            openingAmount = Mathf.Lerp(from, to, t);
            ApplyOpeningAmount();
            yield return null;
        }

        openingAmount = to;
        ApplyOpeningAmount();
        animationCoroutine = null;
    }

    private bool EnsureInitialized()
    {
        if (initialized)
            return true;
        if (initializationFailed)
            return false;

        if (eyelidImage == null && !TryGetComponent(out eyelidImage))
        {
            Debug.LogError("Soft curved eyelids require a UI Image.", this);
            initializationFailed = true;
            return false;
        }

        Shader shader = Resources.Load<Shader>(ShaderResourcePath);
        if (shader == null)
        {
            Debug.LogError("The shared Miguel wake-up eyelid shader is missing.", this);
            initializationFailed = true;
            return false;
        }

        originalMaterial = eyelidImage.material;
        runtimeMaterial = new Material(shader)
        {
            name = "Soft Curved Eyelids (Runtime)"
        };
        eyelidImage.material = runtimeMaterial;
        eyelidImage.color = Color.white;
        // This full-screen visual sits above dialogue canvases, so it must
        // never intercept pointer input when the eyelids are transparent.
        eyelidImage.raycastTarget = false;
        eyelidImage.enabled = true;

        initialized = true;
        ApplyOpeningAmount();
        return true;
    }

    private void ApplyOpeningAmount()
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(OpenId, Mathf.Clamp01(openingAmount));
        runtimeMaterial.SetFloat(FeatherId, eyelidFeather);
        runtimeMaterial.SetFloat(CurvatureId, eyelidCurvature);
    }

    private void StopAnimation()
    {
        if (animationCoroutine == null)
            return;

        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }

    private void OnDestroy()
    {
        StopAnimation();
        if (eyelidImage != null && eyelidImage.material == runtimeMaterial)
            eyelidImage.material = originalMaterial;

        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
}
