using System.Collections.Generic;
using UnityEngine;

public class ClarityManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode clarityKey = KeyCode.C;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.3f;

    [Header("Target Highlight")]
    [SerializeField, Min(0f)] private float targetHighlightIntensity = 0.75f;
    [SerializeField, ColorUsage(false, true)]
    private Color targetHighlightColor = new Color(1f, 0.78f, 0.45f, 1f);

    [Header("Screen Vignette")]
    [SerializeField, Range(0f, 0.5f)] private float vignetteIntensity = 0.22f;
    [SerializeField, Range(0.1f, 1f)] private float vignetteSoftness = 0.65f;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Shader vignetteShader;

    private static readonly HashSet<ClarityTarget> targets =
        new HashSet<ClarityTarget>();

    private static ClarityManager instance;

    private ClarityVignetteEffect vignetteEffect;
    private bool createdVignetteEffect;
    private float currentStrength;

    public bool IsClarityActive { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                "Only one ClarityManager should be active in a scene.",
                this);
            enabled = false;
            return;
        }

        instance = this;
    }

    private void Start()
    {
        SetupVignette();
        ApplyVisualStrength(0f);
    }

    private void Update()
    {
        IsClarityActive = Input.GetKey(clarityKey);

        float targetStrength = IsClarityActive ? 1f : 0f;
        float nextStrength;

        if (transitionDuration <= 0f)
        {
            nextStrength = targetStrength;
        }
        else
        {
            nextStrength = Mathf.MoveTowards(
                currentStrength,
                targetStrength,
                Time.deltaTime / transitionDuration);
        }

        if (Mathf.Approximately(nextStrength, currentStrength))
            return;

        currentStrength = nextStrength;
        ApplyVisualStrength(currentStrength);
    }

    private void OnDisable()
    {
        if (instance != this)
            return;

        IsClarityActive = false;
        currentStrength = 0f;
        ApplyVisualStrength(0f);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        ApplyVisualStrength(0f);

        if (createdVignetteEffect && vignetteEffect != null)
            Destroy(vignetteEffect);

        instance = null;
    }

    public static void RegisterTarget(ClarityTarget target)
    {
        if (target == null)
            return;

        targets.Add(target);

        if (instance != null)
        {
            target.SetClarityVisual(
                instance.currentStrength,
                instance.targetHighlightColor,
                instance.targetHighlightIntensity);
        }
    }

    public static void UnregisterTarget(ClarityTarget target)
    {
        if (target != null)
            targets.Remove(target);
    }

    private void SetupVignette()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogWarning(
                "ClarityManager could not find a Main Camera for the vignette.",
                this);
            return;
        }

        if (vignetteShader == null)
        {
            vignetteShader = Resources.Load<Shader>(
                "Clarity/ClarityVignette");
        }

        if (vignetteShader == null)
        {
            Debug.LogWarning(
                "Clarity vignette shader could not be loaded.",
                this);
            return;
        }

        vignetteEffect =
            targetCamera.GetComponent<ClarityVignetteEffect>();

        if (vignetteEffect == null)
        {
            vignetteEffect =
                targetCamera.gameObject.AddComponent<ClarityVignetteEffect>();
            createdVignetteEffect = true;
        }

        vignetteEffect.Initialize(vignetteShader);
        vignetteEffect.SetVignette(0f, vignetteSoftness);
    }

    private void ApplyVisualStrength(float strength)
    {
        targets.RemoveWhere(target => target == null);

        foreach (ClarityTarget target in targets)
        {
            target.SetClarityVisual(
                strength,
                targetHighlightColor,
                targetHighlightIntensity);
        }

        if (vignetteEffect != null)
        {
            vignetteEffect.SetVignette(
                vignetteIntensity * strength,
                vignetteSoftness);
        }
    }
}

[DisallowMultipleComponent]
internal sealed class ClarityVignetteEffect : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId =
        Shader.PropertyToID("_Softness");

    private Material vignetteMaterial;
    private float intensity;
    private float softness;

    public void Initialize(Shader shader)
    {
        if (vignetteMaterial != null)
            return;

        vignetteMaterial = new Material(shader)
        {
            name = "Clarity Vignette (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void SetVignette(float newIntensity, float newSoftness)
    {
        intensity = Mathf.Clamp01(newIntensity);
        softness = Mathf.Clamp01(newSoftness);
        enabled = vignetteMaterial != null && intensity > 0.0001f;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (vignetteMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        vignetteMaterial.SetFloat(IntensityId, intensity);
        vignetteMaterial.SetFloat(SoftnessId, softness);
        Graphics.Blit(source, destination, vignetteMaterial);
    }

    private void OnDestroy()
    {
        if (vignetteMaterial != null)
            Destroy(vignetteMaterial);
    }
}
