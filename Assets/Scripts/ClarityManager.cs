using System;
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

    [Header("Magnifying Lens")]
    [SerializeField] private ClarityLensUI lensUI;

    [Header("Locked HUD Presentation")]
    [SerializeField] private CanvasGroup clarityIconCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float lockedIconAlpha = 0.5f;

    private static readonly HashSet<ClarityTarget> targets =
        new HashSet<ClarityTarget>();

    private static ClarityManager instance;

    private ClarityVignetteEffect vignetteEffect;
    private bool createdVignetteEffect;
    private float currentStrength;
    private bool clarityUnlocked;
    private float originalClarityIconAlpha = 1f;
    private bool capturedClarityIconAlpha;

    public bool IsClarityActive { get; private set; }
    public bool IsDocumentMode { get; private set; }
    public static event Action<bool> Activated;

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
        clarityUnlocked = GameplaySystemState.IsUnlocked(
            GameplaySystemId.Clarity);
        ResolveClarityIconCanvasGroup();
        ApplyClarityIconAlpha(clarityUnlocked);
    }

    private void OnEnable()
    {
        if (instance != this)
            return;

        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        clarityUnlocked = GameplaySystemState.IsUnlocked(
            GameplaySystemId.Clarity);
        ResolveClarityIconCanvasGroup();
        ApplyClarityIconAlpha(clarityUnlocked);
    }

    private void Start()
    {
        SubscribeToSystemUnlockChanges();
        SetupLens();
        SetupVignette();
        ApplyVisualStrength(0f);
    }

    private void SubscribeToSystemUnlockChanges()
    {
        if (!enabled)
            return;

        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;
        GameplaySystemState.UnlockChanged += HandleSystemUnlockChanged;
        clarityUnlocked = GameplaySystemState.IsUnlocked(
            GameplaySystemId.Clarity);
        ResolveClarityIconCanvasGroup();
        ApplyClarityIconAlpha(clarityUnlocked);
    }

    private void Update()
    {
        bool wasActive = IsClarityActive;
        if (StorySequenceCoordinator.IsStorySequenceActive ||
            !clarityUnlocked)
            IsClarityActive = false;
        else
            IsClarityActive = Input.GetKey(clarityKey);

        if (IsClarityActive && !wasActive)
            Activated?.Invoke(IsDocumentMode);

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

        bool strengthChanged =
            !Mathf.Approximately(nextStrength, currentStrength);

        if (!strengthChanged && !IsClarityActive)
            return;

        currentStrength = nextStrength;
        ApplyVisualStrength(currentStrength);
    }

    private void OnDisable()
    {
        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;

        if (instance != this)
            return;

        IsClarityActive = false;
        IsDocumentMode = false;
        currentStrength = 0f;
        ApplyVisualStrength(0f);
    }

    private void OnDestroy()
    {
        GameplaySystemState.UnlockChanged -= HandleSystemUnlockChanged;

        if (instance != this)
            return;

        ApplyVisualStrength(0f);

        if (createdVignetteEffect && vignetteEffect != null)
            Destroy(vignetteEffect);

        instance = null;
    }

    private void HandleSystemUnlockChanged(
        GameplaySystemId system,
        bool unlocked)
    {
        if (system != GameplaySystemId.Clarity)
            return;

        clarityUnlocked = unlocked;
        ApplyClarityIconAlpha(unlocked);
        if (unlocked)
            return;

        IsClarityActive = false;
        currentStrength = 0f;
        ApplyVisualStrength(0f);
    }

    private void ResolveClarityIconCanvasGroup()
    {
        if (clarityIconCanvasGroup == null)
        {
            CanvasGroup[] groups = FindObjectsByType<CanvasGroup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group != null && group.gameObject.name == "ClarityIcon")
                {
                    clarityIconCanvasGroup = group;
                    break;
                }
            }
        }

        if (clarityIconCanvasGroup == null || capturedClarityIconAlpha)
            return;

        originalClarityIconAlpha = clarityIconCanvasGroup.alpha;
        capturedClarityIconAlpha = true;
    }

    private void ApplyClarityIconAlpha(bool unlocked)
    {
        ResolveClarityIconCanvasGroup();
        if (clarityIconCanvasGroup == null)
            return;

        clarityIconCanvasGroup.alpha = unlocked
            ? originalClarityIconAlpha
            : lockedIconAlpha;
    }

    public static void RegisterTarget(ClarityTarget target)
    {
        if (target == null)
            return;

        targets.Add(target);

        if (instance != null)
        {
            float targetStrength = instance.IsDocumentMode
                ? 0f
                : instance.GetTargetStrength(target, instance.currentStrength);
            target.SetClarityVisual(
                targetStrength,
                instance.targetHighlightColor,
                instance.targetHighlightIntensity);
        }
    }

    public static void UnregisterTarget(ClarityTarget target)
    {
        if (target != null)
            targets.Remove(target);
    }

    public void EnterDocumentMode()
    {
        if (IsDocumentMode)
            return;

        IsDocumentMode = true;
        ApplyVisualStrength(currentStrength);
    }

    public void ExitDocumentMode()
    {
        if (!IsDocumentMode)
            return;

        IsDocumentMode = false;
        ApplyVisualStrength(currentStrength);
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

    private void SetupLens()
    {
        if (lensUI == null)
            lensUI = FindAnyObjectByType<ClarityLensUI>();
    }

    private void ApplyVisualStrength(float strength)
    {
        targets.RemoveWhere(target => target == null);

        foreach (ClarityTarget target in targets)
        {
            float targetStrength = GetTargetStrength(target, strength);
            target.SetClarityVisual(
                targetStrength,
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

    private float GetTargetStrength(
        ClarityTarget target,
        float globalStrength)
    {
        if (IsDocumentMode ||
            globalStrength <= 0.0001f ||
            target == null)
        {
            return 0f;
        }

        // Preserve the original global behavior in scenes that have not yet
        // been given a lens presentation.
        if (lensUI == null)
            return globalStrength;

        if (targetCamera == null ||
            !target.TryGetScreenRect(targetCamera, out Rect targetRect))
        {
            return 0f;
        }

        Vector2 lensCenter = lensUI.LensCenterScreenPosition;
        Vector2 closestPoint = new Vector2(
            Mathf.Clamp(lensCenter.x, targetRect.xMin, targetRect.xMax),
            Mathf.Clamp(lensCenter.y, targetRect.yMin, targetRect.yMax));
        float distance = Vector2.Distance(lensCenter, closestPoint);

        return distance <= lensUI.DetectionRadiusScreenPixels
            ? globalStrength
            : 0f;
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
