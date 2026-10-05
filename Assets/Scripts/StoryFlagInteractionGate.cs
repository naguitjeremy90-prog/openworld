using UnityEngine;
using System;

/// <summary>Allows an interaction when a session story flag has the configured value.</summary>
[DisallowMultipleComponent]
public sealed class StoryFlagInteractionGate : MonoBehaviour, IInteractionAvailabilityCondition
{
    [SerializeField] private string requiredFlagId;
    [SerializeField] private bool expectedValue = true;
    [SerializeField] private string[] blockedFlagIds = Array.Empty<string>();

    public bool IsAvailable()
    {
        if (string.IsNullOrWhiteSpace(requiredFlagId) ||
            SessionStoryState.GetFlag(requiredFlagId) != expectedValue)
            return false;

        if (blockedFlagIds == null)
            return true;

        foreach (string flagId in blockedFlagIds)
            if (!string.IsNullOrWhiteSpace(flagId) && SessionStoryState.GetFlag(flagId))
                return false;

        return true;
    }
}
