using System;
using System.Collections.Generic;
using UnityEngine;

public static class SessionStoryState
{
    private static readonly HashSet<string> completedFlags =
        new HashSet<string>(StringComparer.Ordinal);

    public static bool GetFlag(string flagId)
    {
        return !string.IsNullOrEmpty(flagId) &&
               completedFlags.Contains(flagId);
    }

    public static void SetFlag(string flagId, bool completed)
    {
        if (string.IsNullOrEmpty(flagId))
            return;

        if (completed)
            completedFlags.Add(flagId);
        else
            completedFlags.Remove(flagId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        completedFlags.Clear();
    }
}
