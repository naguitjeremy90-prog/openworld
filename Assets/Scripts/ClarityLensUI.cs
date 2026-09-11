using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))]
public sealed class ClarityLensUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField] private Image lensImage;

    [Header("Lens Alignment")]
    [Tooltip("Displayed width in canvas units. Height follows the sprite aspect ratio.")]
    [SerializeField, Min(1f)] private float lensUISize = 320f;
    [Tooltip("Moves the artwork around the mouse-controlled functional lens center.")]
    [SerializeField] private Vector2 visualOffset = new Vector2(47f, -48f);
    [Tooltip("Functional world-target detection radius in canvas units, independent of the artwork size.")]
    [SerializeField, Min(0f)] private float detectionRadius = 76f;

    private RectTransform lensTransform;
    private Canvas parentCanvas;

    public Vector2 LensCenterScreenPosition => Input.mousePosition;

    public float DetectionRadiusScreenPixels
    {
        get
        {
            float scaleFactor = parentCanvas != null
                ? Mathf.Max(0.0001f, parentCanvas.scaleFactor)
                : 1f;
            return detectionRadius * scaleFactor;
        }
    }

    private void Awake()
    {
        CacheReferences();
        ApplyPresentationSettings();
        SetVisible(false);
    }

    private void OnValidate()
    {
        lensUISize = Mathf.Max(1f, lensUISize);
        detectionRadius = Mathf.Max(0f, detectionRadius);
        CacheReferences();

        if (lensImage != null)
        {
            lensImage.raycastTarget = false;
            lensImage.preserveAspect = true;
        }
    }

    private void LateUpdate()
    {
        if (clarityManager == null)
            clarityManager = FindAnyObjectByType<ClarityManager>();

        bool visible = clarityManager != null &&
                       clarityManager.IsClarityActive;
        SetVisible(visible);

        if (!visible || lensTransform == null)
            return;

        RectTransform parentRect = lensTransform.parent as RectTransform;
        Camera uiCamera = null;

        if (parentCanvas != null &&
            parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                LensCenterScreenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            lensTransform.anchoredPosition = localPoint + visualOffset;
        }
    }

    private void CacheReferences()
    {
        if (lensTransform == null)
            lensTransform = GetComponent<RectTransform>();

        if (lensImage == null)
            lensImage = GetComponent<Image>();

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
    }

    private void ApplyPresentationSettings()
    {
        if (lensImage != null)
        {
            lensImage.raycastTarget = false;
            lensImage.preserveAspect = true;
        }

        if (lensTransform == null)
            return;

        float aspect = 1f;
        if (lensImage != null && lensImage.sprite != null)
        {
            Rect spriteRect = lensImage.sprite.rect;
            if (spriteRect.height > 0f)
                aspect = spriteRect.width / spriteRect.height;
        }

        lensTransform.sizeDelta = new Vector2(
            lensUISize,
            lensUISize / Mathf.Max(0.0001f, aspect));
    }

    private void SetVisible(bool visible)
    {
        if (lensImage != null && lensImage.enabled != visible)
            lensImage.enabled = visible;
    }
}
