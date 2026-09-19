using System.Collections;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    private const int FullScreenTransitionSortingOrder = 32100;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Start Behaviour")]
    [SerializeField] private bool fadeInOnStart = true;

    private void Awake()
    {
        Canvas canvas = fadeCanvasGroup != null
            ? fadeCanvasGroup.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // Ordinary door/room fades must participate in the same topmost
            // screen-space presentation layer as the persistent iris overlay.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = FullScreenTransitionSortingOrder;
        }

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 1f;
    }

    private void Start()
    {
        if (fadeCanvasGroup == null)
            return;

        if (fadeInOnStart)
            StartCoroutine(FadeFromBlack());
    }

    public IEnumerator FadeToBlack()
    {
        if (fadeCanvasGroup == null)
            yield break;

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            fadeCanvasGroup.alpha = Mathf.Lerp(
                0f,
                1f,
                time / fadeDuration
            );

            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeFromBlack()
    {
        if (fadeCanvasGroup == null)
            yield break;

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            fadeCanvasGroup.alpha = Mathf.Lerp(
                1f,
                0f,
                time / fadeDuration
            );

            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
    }
}
