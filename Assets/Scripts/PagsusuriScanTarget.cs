using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Marks one ClarityTarget as continuously scannable while its existing highlight is active.</summary>
[DisallowMultipleComponent]
public sealed class PagsusuriScanTarget : MonoBehaviour, IInteractionAvailabilityCondition
{
    private static readonly List<PagsusuriScanTarget> activeTargets =
        new List<PagsusuriScanTarget>();

    [Header("Target")]
    [SerializeField] private ClarityTarget clarityTarget;
    [SerializeField] private string completionFlagId = "church_lumang_sulatin_pagsusuri_complete";

    [Header("Scan")]
    [SerializeField, Min(0.1f)] private float scanDuration = 1.5f;

    [Header("Completion Audio")]
    [SerializeField] private AudioClip completionSfx;
    [SerializeField] private AudioSource audioSource;

    private float progress;

    public ClarityTarget Target => clarityTarget;
    public float Progress => IsAvailable() ? 1f : progress;
    public float ScanDuration => Mathf.Max(0.1f, scanDuration);
    public event Action<PagsusuriScanTarget> OnScanCompleted;

    private void Awake()
    {
        if (clarityTarget == null)
            clarityTarget = GetComponent<ClarityTarget>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        progress = IsAvailable() ? 1f : 0f;
    }

    private void OnEnable()
    {
        if (!activeTargets.Contains(this))
            activeTargets.Add(this);
        SessionStoryState.FlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
        SessionStoryState.FlagChanged -= HandleFlagChanged;
        if (!IsAvailable())
            progress = 0f;
    }

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId) &&
               SessionStoryState.GetFlag(completionFlagId);
    }

    internal static IReadOnlyList<PagsusuriScanTarget> ActiveTargets => activeTargets;

    internal bool AdvanceScan(float deltaTime)
    {
        if (IsAvailable())
            return true;

        progress = Mathf.Clamp01(progress + Mathf.Max(0f, deltaTime) / ScanDuration);
        if (progress < 1f)
            return false;

        SessionStoryState.SetFlag(completionFlagId, true);
        if (completionSfx != null && audioSource != null)
            audioSource.PlayOneShot(completionSfx);
        OnScanCompleted?.Invoke(this);
        return true;
    }

    internal void ResetIncompleteProgress()
    {
        if (!IsAvailable())
            progress = 0f;
    }

    private void HandleFlagChanged(string flagId, bool completed)
    {
        if (!string.Equals(flagId, completionFlagId, StringComparison.Ordinal))
            return;

        progress = completed ? 1f : 0f;
    }
}
