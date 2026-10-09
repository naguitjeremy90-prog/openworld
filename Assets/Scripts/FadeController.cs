using System.Collections;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    private const int FullScreenTransitionSortingOrder = 32100;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Start Behaviour")]
    [SerializeField] private bool fadeInOnStart = true;
    private bool holdBlackForSequence;

    public bool IncomingFadeCompleted { get; private set; }
    private int fadeVersion;
    private readonly System.Collections.Generic.HashSet<int> activeFades =
        new System.Collections.Generic.HashSet<int>();

    private int BeginObservedFade()
    {
        IncomingFadeCompleted = false;
        int version = ++fadeVersion;
        activeFades.Add(version);
        return version;
    }

    private void FinishObservedFade(int version, bool completed)
    {
        activeFades.Remove(version);
        if (version == fadeVersion)
            IncomingFadeCompleted = completed && activeFades.Count == 0 && isActiveAndEnabled;
    }

    private void InvalidateFadeReadiness()
    {
        IncomingFadeCompleted = false;
        ++fadeVersion;
    }

    private void OnDisable()
    {
        InvalidateFadeReadiness();
        activeFades.Clear();
    }

    private void OnDestroy() => OnDisable();

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

        if (fadeInOnStart && !holdBlackForSequence)
            StartCoroutine(FadeFromBlack());
    }

    /// <summary>Keeps the existing fade canvas black for a scene-local authored sequence.</summary>
    public void HoldBlackForSequence()
    {
        InvalidateFadeReadiness();
        holdBlackForSequence = true;
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeToBlack()
    {
        int version = BeginObservedFade();
        bool completed = false;
        try
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
        finally
        {
            FinishObservedFade(version, completed);
        }
    }

    public IEnumerator FadeFromBlack()
    {
        int version = BeginObservedFade();
        bool completed = false;
        try
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
            completed = true;
        }
        finally
        {
            FinishObservedFade(version, completed);
        }
    }

    public IEnumerator FadeFromBlack(float duration)
    {
        int version = BeginObservedFade();
        bool completed = false;
        try
        {
            if (fadeCanvasGroup == null)
                yield break;

            fadeCanvasGroup.alpha = 1f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, time / duration);
                yield return null;
            }

            fadeCanvasGroup.alpha = 0f;
            completed = true;
        }
        finally
        {
            FinishObservedFade(version, completed);
        }
    }
}
