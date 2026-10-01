using UnityEngine;

/// <summary>Allows an interaction when a session story flag has the configured value.</summary>
[DisallowMultipleComponent]
public sealed class StoryFlagInteractionGate : MonoBehaviour, IInteractionAvailabilityCondition
{
    [SerializeField] private string requiredFlagId;
    [SerializeField] private bool expectedValue = true;

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(requiredFlagId) &&
               SessionStoryState.GetFlag(requiredFlagId) == expectedValue;
    }
}
