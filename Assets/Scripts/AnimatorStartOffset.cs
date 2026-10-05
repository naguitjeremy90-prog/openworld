using UnityEngine;

/// <summary>
/// Starts an Animator in a serialized state at a deterministic point in its cycle.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimatorStartOffset : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private string stateName = "Sitting";
    [SerializeField, Range(0f, 1f)] private float normalizedStartTime;

    private void Reset()
    {
        targetAnimator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        targetAnimator.Play(stateName, 0, normalizedStartTime);
        targetAnimator.Update(0f);
    }
}