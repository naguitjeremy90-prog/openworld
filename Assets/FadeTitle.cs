using System.Collections;
using UnityEngine;
using TMPro;

public class ChapterTitleFade : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public float fadeInDuration = 2f;
    public float stayDuration = 2f;
    public float fadeOutDuration = 2f;

    void Start()
    {
        StartCoroutine(PlayChapterTitle());
    }

    IEnumerator PlayChapterTitle()
    {
        canvasGroup.alpha = 0f;

        // Fade in
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;

        // Stay visible
        yield return new WaitForSeconds(stayDuration);

        // Fade out
        timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}