using System;
using System.Collections.Generic;
using UnityEngine;

public static class SessionStoryState
{
    private static readonly HashSet<string> completedFlags =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> integerValues =
        new Dictionary<string, int>(StringComparer.Ordinal);

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

    public static int GetInt(string valueId)
    {
        if (string.IsNullOrEmpty(valueId))
            return 0;

        return integerValues.TryGetValue(valueId, out int value) ? value : 0;
    }

    public static void SetInt(string valueId, int value)
    {
        if (string.IsNullOrEmpty(valueId))
            return;

        if (value != 0)
            integerValues[valueId] = value;
        else
            integerValues.Remove(valueId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        completedFlags.Clear();
        integerValues.Clear();
    }
}
