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
    // Kept serialized so existing scene data remains compatible. Sprite rendering no longer uses this value.
#pragma warning disable 0414
    [SerializeField] private string defaultGlyph = "!";
#pragma warning restore 0414

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
    private Transform reactionSpriteRoot;
    private SpriteRenderer reactionSpriteRenderer;
    private float spriteBaseScale = 0.05f;

    private static CharacterReactionConfig sharedConfig;
    private static bool configResolved;

    public bool IsReacting => isReacting;

    private void Awake()
    {
        if (indicatorRoot == null && reactionText != null)
            indicatorRoot = reactionText.transform;

        if (reactionText != null)
        {
            visibleAlpha = reactionText.color.a;
            reactionText.enabled = false;

            // TMP's world-space renderer is retained in existing scenes for serialization
            // compatibility, but disabled so the legacy glyph can never appear behind the sprite.
            MeshRenderer legacyTextRenderer = reactionText.GetComponent<MeshRenderer>();
            if (legacyTextRenderer != null)
                legacyTextRenderer.enabled = false;
        }

        CharacterReactionConfig config = ResolveConfig();
        if (config != null)
            spriteBaseScale = config.SpriteBaseScale;

        if (indicatorRoot != null)
        {
            reactionSpriteRoot = indicatorRoot.Find("ReactionSprite");
            if (reactionSpriteRoot == null)
            {
                GameObject spriteObject = new GameObject("ReactionSprite");
                reactionSpriteRoot = spriteObject.transform;
                reactionSpriteRoot.SetParent(indicatorRoot, false);
            }

            reactionSpriteRoot.localPosition = Vector3.zero;
            reactionSpriteRoot.localRotation = Quaternion.identity;
            reactionSpriteRoot.localScale = Vector3.one;

            reactionSpriteRenderer = reactionSpriteRoot.GetComponent<SpriteRenderer>();
            if (reactionSpriteRenderer == null)
                reactionSpriteRenderer = reactionSpriteRoot.gameObject.AddComponent<SpriteRenderer>();

            if (reactionSpriteRenderer != null && config != null)
                reactionSpriteRenderer.sprite = config.ReactionSprite;

            if (reactionSpriteRenderer != null)
            {
                // Use the default world sorting layer with a small order offset over same-layer sprites.
                reactionSpriteRenderer.sortingLayerName = "Default";
                reactionSpriteRenderer.sortingOrder = 1;
                reactionSpriteRenderer.enabled = false;

                // Keep the comic mark white; the animation only changes its alpha.
                reactionSpriteRenderer.color = new Color(1f, 1f, 1f, visibleAlpha);
            }
        }

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
        if (isReacting || indicatorRoot == null || reactionSpriteRoot == null ||
            reactionSpriteRenderer == null || reactionSpriteRenderer.sprite == null)
            yield break;

        isReacting = true;

        if (reactionAnchor != null && indicatorRoot != null &&
            indicatorRoot.parent != reactionAnchor)
        {
            indicatorRoot.SetParent(reactionAnchor, false);
        }

        indicatorRoot.gameObject.SetActive(true);
        reactionSpriteRoot.gameObject.SetActive(true);
        reactionSpriteRenderer.enabled = true;
        SetSpriteAlpha(visibleAlpha);
        SetIndicatorScale(startScale);

        if (audioSource != null && reactionAudioClip != null)
            audioSource.PlayOneShot(reactionAudioClip);

        yield return PopIndicator();

        if (lingerDuration > 0f)
            yield return new WaitForSeconds(lingerDuration);

        yield return FadeIndicator();

        HideIndicator();
        SetIndicatorScale(1f);
        SetSpriteAlpha(visibleAlpha);
        isReacting = false;
    }

    private void LateUpdate()
    {
        if (!isReacting || indicatorRoot == null || Camera.main == null)
            return;

        indicatorRoot.forward = Camera.main.transform.forward;
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
            SetSpriteAlpha(0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetSpriteAlpha(Mathf.Lerp(visibleAlpha, 0f, elapsed / fadeDuration));
            yield return null;
        }

        SetSpriteAlpha(0f);
    }

    private void HideIndicator()
    {
        if (reactionSpriteRenderer != null)
            reactionSpriteRenderer.enabled = false;

        if (reactionSpriteRoot != null)
            reactionSpriteRoot.gameObject.SetActive(false);

        if (indicatorRoot != null)
            indicatorRoot.gameObject.SetActive(false);
        else if (reactionText != null)
            reactionText.gameObject.SetActive(false);
    }

    private void SetIndicatorScale(float scale)
    {
        if (indicatorRoot != null)
            indicatorRoot.localScale = indicatorBaseScale * (scale * spriteBaseScale);
    }

    private void SetSpriteAlpha(float alpha)
    {
        if (reactionSpriteRenderer == null)
            return;

        reactionSpriteRenderer.color = new Color(1f, 1f, 1f, alpha);
    }

    private static CharacterReactionConfig ResolveConfig()
    {
        if (!configResolved)
        {
            sharedConfig = Resources.Load<CharacterReactionConfig>("CharacterReactionConfig");
            configResolved = true;
        }

        return sharedConfig;
    }
}
