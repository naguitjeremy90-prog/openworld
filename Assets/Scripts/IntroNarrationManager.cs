using System.Collections;
using TMPro;
using UnityEngine;

public class IntroNarrationManager : MonoBehaviour
{
    [System.Serializable]
    public class NarrationEntry
    {
        [TextArea(3, 6)]
        public string Text;

        [Min(0f)]
        public float HoldDuration = 4f;

        [Min(0f)]
        public float PauseAfter = 1f;
    }

    [Header("References")]
    [SerializeField] private TMP_Text narrationText;
    [SerializeField] private CanvasGroup narrationCanvasGroup;

    [Header("Narration")]
    [SerializeField] private NarrationEntry[] narrationLines =
        new NarrationEntry[0];

    [Header("Fade Settings")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 1f;

    private Coroutine narrationCoroutine;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        SetNarrationAlpha(0f);
    }

    private void OnDisable()
    {
        if (narrationCoroutine != null)
            StopCoroutine(narrationCoroutine);

        narrationCoroutine = null;
        IsPlaying = false;
        SetNarrationAlpha(0f);
    }

    public void PlayNarration()
    {
        if (IsPlaying)
            return;

        if (narrationText == null || narrationCanvasGroup == null)
        {
            Debug.LogWarning(
                "IntroNarrationManager requires a TMP text and CanvasGroup.",
                this
            );
            return;
        }

        IsPlaying = true;
        narrationCoroutine = StartCoroutine(
            PlayNarrationSequence()
        );
    }

    private IEnumerator PlayNarrationSequence()
    {
        narrationCanvasGroup.alpha = 0f;

        if (narrationLines != null)
        {
            foreach (NarrationEntry entry in narrationLines)
            {
                if (entry == null)
                    continue;

                narrationText.text = entry.Text ?? string.Empty;

                yield return StartCoroutine(
                    FadeNarration(0f, 1f)
                );

                if (entry.HoldDuration > 0f)
                {
                    yield return new WaitForSeconds(
                        entry.HoldDuration
                    );
                }

                yield return StartCoroutine(
                    FadeNarration(1f, 0f)
                );

                if (entry.PauseAfter > 0f)
                {
                    yield return new WaitForSeconds(
                        entry.PauseAfter
                    );
                }
            }
        }

        narrationCanvasGroup.alpha = 0f;
        narrationCoroutine = null;
        IsPlaying = false;
    }

    private IEnumerator FadeNarration(
        float startAlpha,
        float endAlpha)
    {
        narrationCanvasGroup.alpha = startAlpha;

        if (fadeDuration <= 0f)
        {
            narrationCanvasGroup.alpha = endAlpha;
            yield break;
        }

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            float progress = Mathf.Clamp01(
                time / fadeDuration
            );

            narrationCanvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                endAlpha,
                progress
            );

            yield return null;
        }

        narrationCanvasGroup.alpha = endAlpha;
    }

    private void SetNarrationAlpha(float alpha)
    {
        if (narrationCanvasGroup != null)
            narrationCanvasGroup.alpha = alpha;
    }
}
