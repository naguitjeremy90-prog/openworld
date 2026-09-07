using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DreamWhisperController : MonoBehaviour
{
    [Serializable]
    public sealed class WhisperEntry
    {
        [TextArea]
        [SerializeField] private string text;
        [SerializeField, Min(0f)] private float delayBefore = 0.5f;
        [SerializeField, Min(0f)] private float displayDuration = 1.25f;
        [SerializeField] private AudioClip audioClip;

        public string Text => text;
        public float DelayBefore => delayBefore;
        public float DisplayDuration => displayDuration;
        public AudioClip AudioClip => audioClip;
    }

    [Header("Presentation")]
    [SerializeField] private TMP_Text whisperText;
    [SerializeField, Min(0f)] private float textFadeDuration = 0.2f;

    [Header("Sequence")]
    [SerializeField, Min(0f)] private float endDelay = 1f;
    [SerializeField] private List<WhisperEntry> entries = new List<WhisperEntry>();

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip endAudioClip;

    private bool isPlaying;
    private float visibleAlpha = 1f;

    private void Awake()
    {
        if (whisperText == null)
            return;

        visibleAlpha = whisperText.color.a;
        whisperText.gameObject.SetActive(false);
    }

    public IEnumerator PlaySequence()
    {
        if (isPlaying || whisperText == null)
            yield break;

        isPlaying = true;

        for (int i = 0; i < entries.Count; i++)
        {
            WhisperEntry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.DelayBefore > 0f)
                yield return new WaitForSeconds(entry.DelayBefore);

            whisperText.text = entry.Text;
            SetTextAlpha(textFadeDuration > 0f ? 0f : visibleAlpha);
            whisperText.gameObject.SetActive(true);

            if (audioSource != null && entry.AudioClip != null)
                audioSource.PlayOneShot(entry.AudioClip);

            if (textFadeDuration > 0f)
                yield return FadeText(0f, visibleAlpha);

            if (entry.DisplayDuration > 0f)
                yield return new WaitForSeconds(entry.DisplayDuration);

            if (textFadeDuration > 0f)
                yield return FadeText(visibleAlpha, 0f);

            whisperText.gameObject.SetActive(false);
        }

        if (audioSource != null && endAudioClip != null)
        {
            audioSource.PlayOneShot(endAudioClip);
            yield return new WaitForSeconds(endAudioClip.length);
        }

        whisperText.gameObject.SetActive(false);

        if (endDelay > 0f)
            yield return new WaitForSeconds(endDelay);

        isPlaying = false;
    }

    private IEnumerator FadeText(float from, float to)
    {
        float elapsed = 0f;

        while (elapsed < textFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetTextAlpha(Mathf.Lerp(from, to, elapsed / textFadeDuration));
            yield return null;
        }

        SetTextAlpha(to);
    }

    private void SetTextAlpha(float alpha)
    {
        Color color = whisperText.color;
        color.a = alpha;
        whisperText.color = color;
    }
}
