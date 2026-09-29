using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a soft side-only vignette above the transport video.</summary>
public sealed class TransportationSideVignette : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float intensity = 0.82f;
    [SerializeField, Range(0.1f, 1f)] private float softness = 0.72f;
    [SerializeField, Range(0f, 1f), Tooltip("Maximum opacity of the added corner shadow layer.")]
    private float cornerShadowIntensity = 0.42f;
    [SerializeField, Range(0.1f, 1f), Tooltip("How far the corner shadows extend toward the center of the frame.")]
    private float cornerShadowSize = 0.76f;

    private GameObject overlayRoot;
    private Material vignetteMaterial;
    private Material cornerShadowMaterial;

    private void OnEnable()
    {
        if (Application.isPlaying)
            CreateOverlay();
    }

    private void CreateOverlay()
    {
        if (overlayRoot != null)
            return;

        Shader shader = Resources.Load<Shader>("Clarity/TransportationSideVignette");
        if (shader == null)
        {
            Debug.LogWarning("Transportation side vignette shader could not be loaded.", this);
            return;
        }

        vignetteMaterial = new Material(shader)
        {
            name = "Transportation Side Vignette (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        vignetteMaterial.SetFloat("_Intensity", intensity);
        vignetteMaterial.SetFloat("_Softness", softness);

        overlayRoot = new GameObject("TransportationSideVignetteOverlay");
        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new GameObject("Side Vignette", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.material = vignetteMaterial;
        image.raycastTarget = false;

        Shader cornerShader = Resources.Load<Shader>("Clarity/TransportationCornerShadow");
        if (cornerShader == null)
        {
            Debug.LogWarning("Transportation corner shadow shader could not be loaded.", this);
            return;
        }

        cornerShadowMaterial = new Material(cornerShader)
        {
            name = "Transportation Corner Shadow (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        cornerShadowMaterial.SetFloat("_Intensity", cornerShadowIntensity);
        cornerShadowMaterial.SetFloat("_Size", cornerShadowSize);

        GameObject shadowObject = new GameObject("Corner Shadow", typeof(RectTransform), typeof(Image));
        shadowObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = Vector2.zero;
        shadowRect.offsetMax = Vector2.zero;

        Image shadowImage = shadowObject.GetComponent<Image>();
        shadowImage.color = Color.white;
        shadowImage.material = cornerShadowMaterial;
        shadowImage.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
        {
            if (Application.isPlaying)
                Destroy(overlayRoot);
            else
                DestroyImmediate(overlayRoot);
        }

        if (vignetteMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(vignetteMaterial);
            else
                DestroyImmediate(vignetteMaterial);
        }

        if (cornerShadowMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(cornerShadowMaterial);
            else
                DestroyImmediate(cornerShadowMaterial);
        }
    }
}
