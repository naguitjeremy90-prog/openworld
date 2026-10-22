using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class SessionStoryState
{
    public static event Action<string, bool> FlagChanged;

    private static readonly HashSet<string> completedFlags =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> integerValues =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> stringValues =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static bool GetFlag(string flagId)
    {
        return !string.IsNullOrEmpty(flagId) &&
               completedFlags.Contains(flagId);
    }

    public static void SetFlag(string flagId, bool completed)
    {
        if (string.IsNullOrEmpty(flagId))
            return;

        bool wasCompleted = completedFlags.Contains(flagId);
        if (wasCompleted == completed)
            return;

        if (completed)
            completedFlags.Add(flagId);
        else
            completedFlags.Remove(flagId);

        FlagChanged?.Invoke(flagId, completed);
        NotifySaveValueChanged(flagId, Alaala.Saving.SaveValueKind.Boolean);
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

        bool changed = GetInt(valueId) != value;

        if (value != 0)
            integerValues[valueId] = value;
        else
            integerValues.Remove(valueId);
        if (changed) NotifySaveValueChanged(valueId, Alaala.Saving.SaveValueKind.Integer);
    }

    public static string GetString(string valueId, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(valueId))
            return defaultValue;

        return stringValues.TryGetValue(valueId, out string value)
            ? value
            : defaultValue;
    }

    public static void SetString(string valueId, string value)
    {
        if (string.IsNullOrEmpty(valueId))
            return;

        bool changed = GetString(valueId) != (value ?? "");

        if (!string.IsNullOrEmpty(value))
            stringValues[valueId] = value;
        else
            stringValues.Remove(valueId);
        if (changed) NotifySaveValueChanged(valueId, Alaala.Saving.SaveValueKind.String);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        completedFlags.Clear();
        integerValues.Clear();
        stringValues.Clear();
        FlagChanged = null;
    }

    /// <summary>Clears playthrough data silently while preserving live service subscriptions.</summary>
    public static void ResetDataForNewGame()
    {
        completedFlags.Clear();
        integerValues.Clear();
        stringValues.Clear();
    }
}
