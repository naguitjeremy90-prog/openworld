using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Presents a short, character-local reaction indicator without owning movement,
/// dialogue, or camera state.
/// </summary>
public sealed class CharacterReactionController : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform reactionAnchor;
    [SerializeField] private Transform indicatorRoot;
    [SerializeField] private TMP_Text reactionText;
    [SerializeField] private string defaultGlyph = "!";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float popDuration = 0.15f;
    [SerializeField, Min(0f)] private float lingerDuration = 0.7f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    [Header("Pop Scale")]
    [SerializeField] private float startScale = 0f;
    [SerializeField] private float peakScale = 1.2f;
    [SerializeField] private float normalScale = 1f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip reactionAudioClip;

    private bool isReacting;
    private float visibleAlpha = 1f;
    private Vector3 indicatorBaseScale = Vector3.one;

    public bool IsReacting => isReacting;

    private void Awake()
    {
        if (reactionText != null)
            visibleAlpha = reactionText.color.a;

        if (indicatorRoot == null && reactionText != null)
            indicatorRoot = reactionText.transform;

        // Capture the authored scale once, before any reaction animation.
        if (indicatorRoot != null)
            indicatorBaseScale = indicatorRoot.localScale;

        HideIndicator();
    }

    /// <summary>Starts the configured reaction for UnityEvents.</summary>
    public void PlayDefaultReaction()
    {
        if (!isReacting)
            StartCoroutine(PlayReaction());
    }

    /// <summary>Runs the configured reaction and completes when it is hidden.</summary>
    public IEnumerator PlayReaction()
    {
        if (isReacting || reactionText == null)
            yield break;

        isReacting = true;

        if (reactionAnchor != null && indicatorRoot != null &&
            indicatorRoot.parent != reactionAnchor)
        {
            indicatorRoot.SetParent(reactionAnchor, false);
        }

        reactionText.text = defaultGlyph;
        SetTextAlpha(visibleAlpha);
        SetIndicatorScale(startScale);

        if (indicatorRoot != null)
            indicatorRoot.gameObject.SetActive(true);
        else
            reactionText.gameObject.SetActive(true);

        if (audioSource != null && reactionAudioClip != null)
            audioSource.PlayOneShot(reactionAudioClip);

        yield return PopIndicator();

        if (lingerDuration > 0f)
            yield return new WaitForSeconds(lingerDuration);

        yield return FadeIndicator();

        HideIndicator();
        SetIndicatorScale(1f);
        SetTextAlpha(visibleAlpha);
        isReacting = false;
    }

    private void LateUpdate()
    {
        if (!isReacting || reactionText == null || Camera.main == null)
            return;

        reactionText.transform.forward = Camera.main.transform.forward;
    }

    private IEnumerator PopIndicator()
    {
        if (popDuration <= 0f)
        {
            SetIndicatorScale(normalScale);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            if (t < 0.5f)
                SetIndicatorScale(Mathf.Lerp(startScale, peakScale, t * 2f));
            else
                SetIndicatorScale(Mathf.Lerp(peakScale, normalScale, (t - 0.5f) * 2f));

            yield return null;
        }

        SetIndicatorScale(normalScale);
    }

    private IEnumerator FadeIndicator()
    {
        if (fadeDuration <= 0f)
        {
            SetTextAlpha(0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetTextAlpha(Mathf.Lerp(visibleAlpha, 0f, elapsed / fadeDuration));
            yield return null;
        }

        SetTextAlpha(0f);
    }

    private void HideIndicator()
    {
        if (indicatorRoot != null)
            indicatorRoot.gameObject.SetActive(false);
        else if (reactionText != null)
            reactionText.gameObject.SetActive(false);
    }

    private void SetIndicatorScale(float scale)
    {
        if (indicatorRoot != null)
            indicatorRoot.localScale = indicatorBaseScale * scale;
    }

    private void SetTextAlpha(float alpha)
    {
        if (reactionText == null)
            return;

        Color color = reactionText.color;
        color.a = alpha;
        reactionText.color = color;
    }
}
