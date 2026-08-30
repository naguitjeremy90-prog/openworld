using System.Collections;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Start Behaviour")]
    [SerializeField] private bool fadeInOnStart = true;

    private void Awake()
    {
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
