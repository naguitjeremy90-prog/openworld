using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScreenFadeController : MonoBehaviour
{
    private const int FullScreenTransitionSortingOrder = 32100;
    [Header("Pre-Reaction Fade Timing")]
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 1f;
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.5f;

    [Header("Presentation")]
    [SerializeField] private int sortingOrder = 31900;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;

    public float FadeToBlackDuration => fadeToBlackDuration;
    public float BlackHoldDuration => blackHoldDuration;
    public float FadeFromBlackDuration => fadeFromBlackDuration;
    public float CurrentAlpha => overlayGroup != null ? overlayGroup.alpha : 0f;

    private void Awake()
    {
        EnsureOverlay();
        ClearImmediately();
    }

    public IEnumerator FadeOutHoldAndIn()
    {
        EnsureOverlay();
        overlayCanvas.enabled = true;
        overlayGroup.blocksRaycasts = true;

        yield return AnimateAlpha(overlayGroup.alpha, 1f, fadeToBlackDuration);
        yield return WaitUnscaled(blackHoldDuration);
        yield return AnimateAlpha(1f, 0f, fadeFromBlackDuration);

        ClearImmediately();
    }

    public void ClearImmediately()
    {
        if (overlayGroup != null)
        {
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
        }

        if (overlayCanvas != null)
            overlayCanvas.enabled = false;
    }

    private IEnumerator AnimateAlpha(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            overlayGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        overlayGroup.alpha = to;
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "PreReactionFadeOverlay",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        // The pre-reaction black overlay is also a full-screen transition. Keep
        // it above tutorial and gameplay canvases even when an older scene has
        // serialized the historical 31900 value.
        overlayCanvas.sortingOrder = Mathf.Max(sortingOrder, FullScreenTransitionSortingOrder);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        overlayGroup = canvasObject.GetComponent<CanvasGroup>();

        GameObject imageObject = new GameObject(
            "Black",
            typeof(RectTransform),
            typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    private void OnDisable()
    {
        ClearImmediately();
    }
}
