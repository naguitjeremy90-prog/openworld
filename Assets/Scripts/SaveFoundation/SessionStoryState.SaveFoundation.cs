using System;
using System.Linq;
using Alaala.Saving;

public static partial class SessionStoryState
{
    // Accepted backing-state changes only; typing into a TMP field does not raise this.
    public static event Action<string, SaveValueKind> ProgressValueChanged;
    private static void NotifySaveValueChanged(string key, SaveValueKind kind) => ProgressValueChanged?.Invoke(key, kind);
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSaveObservers() { ProgressValueChanged = null; }
    internal static bool IsEmptyForSaveRestore => completedFlags.Count == 0 && integerValues.Count == 0 && stringValues.Count == 0;
    public static bool TryCaptureDurable(AlaalaDurableStateRegistry registry, out AlaalaSessionSnapshot snapshot, out string error)
    {
        snapshot = new AlaalaSessionSnapshot();
        error = null;
        if (registry == null) { error = "Missing durable-state registry."; return false; }
        foreach (var key in completedFlags.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!registry.TryGet(key, out var rule)) { error = "Unclassified Boolean key: " + key; snapshot = null; return false; }
            if (rule.Classification == SaveStateClass.Development) { error = "Development state is not genuine progress."; snapshot = null; return false; }
            if (rule.Durable) snapshot.booleans.Add(new SaveBoolean { key = key, value = true });
        }
        foreach (var pair in integerValues.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!registry.TryGet(pair.Key, out var rule)) { error = "Unclassified integer key: " + pair.Key; snapshot = null; return false; }
            if (rule.Durable) snapshot.integers.Add(new SaveInteger { key = pair.Key, value = pair.Value });
        }
        foreach (var pair in stringValues.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!registry.TryGet(pair.Key, out var rule)) { error = "Unclassified string key: " + pair.Key; snapshot = null; return false; }
            if (rule.Durable) snapshot.strings.Add(new SaveString { key = pair.Key, value = pair.Value });
        }
        if (registry.Validate(snapshot, out error)) return true;
        snapshot = null;
        return false;
    }

    /// <summary>Validate first, then replace all session values without any FlagChanged callbacks.</summary>
    public static bool TryRestoreDurableSilently(AlaalaSessionSnapshot snapshot, AlaalaDurableStateRegistry registry, out string error)
    {
        if (registry == null) { error = "Missing durable-state registry."; return false; }
        if (!registry.Validate(snapshot, out error)) return false;
        // Materialize before mutation: caller-owned collections cannot alias the live stores.
        var flags = snapshot.booleans.Where(x => x.value).Select(x => x.key).ToArray();
        var ints = snapshot.integers.Where(x => x.value != 0).Select(x => new SaveInteger { key = x.key, value = x.value }).ToArray();
        var strings = snapshot.strings.Where(x => x.value.Length > 0).Select(x => new SaveString { key = x.key, value = x.value }).ToArray();
        completedFlags.Clear(); integerValues.Clear(); stringValues.Clear();
        foreach (var key in flags) completedFlags.Add(key);
        foreach (var pair in ints) integerValues.Add(pair.key, pair.value);
        foreach (var pair in strings) stringValues.Add(pair.key, pair.value);
        return true;
    }

    internal static bool HasUnsafeSaveExecutionState(out string error)
    {
        foreach (var key in new[] { "alaala_part3_ch89_owner_active", "alaala_part3_ch10_owner_active", "alaala_part3_ch11_owner_active",
            "alaala_epilogue_return_in_progress", "alaala_post_part2_draftworld_arrival_pending" })
            if (GetFlag(key)) { error = "Scene/cinematic handoff still active: " + key; return true; }
        if ((!GetFlag("alaala_part3_ch12_complete") && GetInt("alaala_part3_ch12_cinematic_step") != 0) ||
            (!GetFlag("inventory_tutorial_intro_seen") && GetInt("inventory_tutorial_intro_step") != 0))
        { error = "An unfinished cinematic/tutorial cannot be captured."; return true; }
        foreach (var key in new[] { "alaala_epilogue_return_mode", "inventory_tutorial_pending_phases", "inventory_tutorial_pending_step" })
            if (GetInt(key) != 0) { error = "Partial presentation is unsupported: " + key; return true; }
        foreach (var key in new[] { "makamisa_transport_destination", "makamisa_kalesa_pending_destination" })
            if (GetString(key).Length != 0) { error = "Pending transport: " + key; return true; }
        error = null;
        return false;
    }

    internal static bool MatchesDurableSnapshot(AlaalaSessionSnapshot expected, AlaalaSaveCatalog catalog, out string error)
    {
        var registry = new AlaalaDurableStateRegistry(catalog);
        if (!registry.Validate(expected, out error) || !TryCaptureDurable(registry, out var live, out error)) return false;
        bool matches = expected.booleans.Where(x => x.value).Select(x => x.key).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(live.booleans.Select(x => x.key)) &&
            expected.integers.Where(x => x.value != 0).OrderBy(x => x.key, StringComparer.Ordinal)
                .Select(x => (x.key, x.value)).SequenceEqual(live.integers.Select(x => (x.key, x.value))) &&
            expected.strings.Where(x => x.value.Length > 0).OrderBy(x => x.key, StringComparer.Ordinal)
                .Select(x => (x.key, x.value)).SequenceEqual(live.strings.Select(x => (x.key, x.value)));
        if (!matches) error = "Install the exact validated durable session before manager hydration.";
        return matches;
    }
}
