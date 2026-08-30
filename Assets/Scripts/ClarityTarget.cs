using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ClarityTarget : MonoBehaviour
{
    private const string OverlayShaderResourcePath = "Clarity/ClarityOverlay";

    private static readonly int HighlightColorId =
        Shader.PropertyToID("_HighlightColor");
    private static readonly int StrengthId =
        Shader.PropertyToID("_Strength");

    [Header("Target Renderers")]
    [Tooltip("Leave empty to use supported renderers on this object and its children.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Highlight Overlay")]
    [Tooltip("Optional override. If empty, the built-in Clarity overlay shader is loaded automatically.")]
    [SerializeField] private Shader overlayShader;

    private readonly List<OverlayState> overlayStates =
        new List<OverlayState>();

    private Material overlayMaterial;
    private bool overlayResourcesCreated;

    private void Reset()
    {
        targetRenderers = GetSupportedRenderersInChildren();
    }

    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetSupportedRenderersInChildren();
    }

    private void OnEnable()
    {
        ClarityManager.RegisterTarget(this);
    }

    private void OnDisable()
    {
        ClarityManager.UnregisterTarget(this);
        SetOverlaysVisible(false);
    }

    private void OnDestroy()
    {
        DestroyOverlayResources();
    }

    public void SetClarityVisual(
        float strength,
        Color highlightColor,
        float highlightIntensity)
    {
        strength = Mathf.Clamp01(strength);

        if (strength <= 0.0001f)
        {
            SetOverlaysVisible(false);
            return;
        }

        if (!overlayResourcesCreated)
            CreateOverlayResources();

        if (overlayMaterial == null)
            return;

        overlayMaterial.SetColor(HighlightColorId, highlightColor);
        overlayMaterial.SetFloat(
            StrengthId,
            strength * Mathf.Max(0f, highlightIntensity));

        SetOverlaysVisible(true);
    }

    private void CreateOverlayResources()
    {
        overlayResourcesCreated = true;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetSupportedRenderersInChildren();

        if (overlayShader == null)
            overlayShader = Resources.Load<Shader>(OverlayShaderResourcePath);

        if (overlayShader == null)
        {
            Debug.LogWarning(
                "ClarityTarget could not load the Clarity overlay shader.",
                this);
            return;
        }

        overlayMaterial = new Material(overlayShader)
        {
            name = name + " Clarity Overlay (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };

        HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();

        foreach (Renderer sourceRenderer in targetRenderers)
        {
            if (sourceRenderer == null || !uniqueRenderers.Add(sourceRenderer))
                continue;

            Renderer overlayRenderer = CreateOverlayRenderer(sourceRenderer);

            if (overlayRenderer == null)
                continue;

            int materialSlotCount = Mathf.Max(
                1,
                sourceRenderer.sharedMaterials.Length);
            Material[] overlayMaterials = new Material[materialSlotCount];

            for (int index = 0; index < materialSlotCount; index++)
                overlayMaterials[index] = overlayMaterial;

            overlayRenderer.sharedMaterials = overlayMaterials;
            overlayRenderer.enabled = false;

            overlayStates.Add(new OverlayState
            {
                source = sourceRenderer,
                overlay = overlayRenderer
            });
        }
    }

    private Renderer CreateOverlayRenderer(Renderer sourceRenderer)
    {
        GameObject overlayObject = new GameObject(
            sourceRenderer.gameObject.name + " (Clarity Overlay)");
        overlayObject.hideFlags = HideFlags.HideAndDontSave;
        overlayObject.layer = sourceRenderer.gameObject.layer;

        Transform overlayTransform = overlayObject.transform;
        overlayTransform.SetParent(sourceRenderer.transform, false);
        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;

        Renderer overlayRenderer;

        if (sourceRenderer is MeshRenderer sourceMeshRenderer)
        {
            MeshFilter sourceMeshFilter =
                sourceMeshRenderer.GetComponent<MeshFilter>();

            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                DestroyRuntimeObject(overlayObject);
                return null;
            }

            MeshFilter overlayMeshFilter =
                overlayObject.AddComponent<MeshFilter>();
            overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        }
        else if (sourceRenderer is SkinnedMeshRenderer sourceSkinnedRenderer)
        {
            if (sourceSkinnedRenderer.sharedMesh == null)
            {
                DestroyRuntimeObject(overlayObject);
                return null;
            }

            SkinnedMeshRenderer overlaySkinnedRenderer =
                overlayObject.AddComponent<SkinnedMeshRenderer>();
            overlaySkinnedRenderer.sharedMesh = sourceSkinnedRenderer.sharedMesh;
            overlaySkinnedRenderer.rootBone = sourceSkinnedRenderer.rootBone;
            overlaySkinnedRenderer.bones = sourceSkinnedRenderer.bones;
            overlaySkinnedRenderer.localBounds = sourceSkinnedRenderer.localBounds;
            overlaySkinnedRenderer.quality = sourceSkinnedRenderer.quality;
            overlaySkinnedRenderer.updateWhenOffscreen =
                sourceSkinnedRenderer.updateWhenOffscreen;
            overlayRenderer = overlaySkinnedRenderer;
        }
        else if (sourceRenderer is SpriteRenderer sourceSpriteRenderer)
        {
            SpriteRenderer overlaySpriteRenderer =
                overlayObject.AddComponent<SpriteRenderer>();
            overlaySpriteRenderer.sprite = sourceSpriteRenderer.sprite;
            overlaySpriteRenderer.drawMode = sourceSpriteRenderer.drawMode;
            overlaySpriteRenderer.size = sourceSpriteRenderer.size;
            overlaySpriteRenderer.tileMode = sourceSpriteRenderer.tileMode;
            overlaySpriteRenderer.flipX = sourceSpriteRenderer.flipX;
            overlaySpriteRenderer.flipY = sourceSpriteRenderer.flipY;
            overlaySpriteRenderer.spriteSortPoint =
                sourceSpriteRenderer.spriteSortPoint;
            overlayRenderer = overlaySpriteRenderer;
        }
        else
        {
            DestroyRuntimeObject(overlayObject);
            return null;
        }

        CopyRendererSettings(sourceRenderer, overlayRenderer);
        return overlayRenderer;
    }

    private static void CopyRendererSettings(Renderer source, Renderer overlay)
    {
        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.lightProbeUsage = LightProbeUsage.Off;
        overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlay.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
        overlay.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + 1;
    }

    private void SetOverlaysVisible(bool visible)
    {
        foreach (OverlayState state in overlayStates)
        {
            if (state.overlay == null)
                continue;

            state.overlay.enabled =
                visible && state.source != null && state.source.enabled;
        }
    }

    private Renderer[] GetSupportedRenderersInChildren()
    {
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> supportedRenderers = new List<Renderer>();

        foreach (Renderer childRenderer in childRenderers)
        {
            if (childRenderer is MeshRenderer ||
                childRenderer is SkinnedMeshRenderer ||
                childRenderer is SpriteRenderer)
            {
                supportedRenderers.Add(childRenderer);
            }
        }

        return supportedRenderers.ToArray();
    }

    private void DestroyOverlayResources()
    {
        foreach (OverlayState state in overlayStates)
        {
            if (state.overlay != null)
                DestroyRuntimeObject(state.overlay.gameObject);
        }

        overlayStates.Clear();

        if (overlayMaterial != null)
            DestroyRuntimeObject(overlayMaterial);

        overlayMaterial = null;
        overlayResourcesCreated = false;
    }

    private static void DestroyRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeObject);
        else
            DestroyImmediate(runtimeObject);
    }

    private sealed class OverlayState
    {
        public Renderer source;
        public Renderer overlay;
    }
}
