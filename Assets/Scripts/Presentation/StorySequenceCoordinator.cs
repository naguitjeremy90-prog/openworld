using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent, owner-based presentation state for authored story sequences.
/// It knows only generic sequence owners and registered HUD targets.
/// </summary>
public sealed class StorySequenceCoordinator : MonoBehaviour
{
    private static StorySequenceCoordinator instance;
    private static bool isShuttingDown;
    private readonly Dictionary<int, StorySequenceToken> activeTokens =
        new Dictionary<int, StorySequenceToken>();
    private readonly HashSet<GameplayHUDTarget> targets =
        new HashSet<GameplayHUDTarget>();

    public static StorySequenceCoordinator Instance
    {
        get
        {
            if (instance == null && Application.isPlaying && !isShuttingDown)
            {
                GameObject root = new GameObject("StorySequenceCoordinator");
                instance = root.AddComponent<StorySequenceCoordinator>();
            }

            return instance;
        }
    }

    public static bool HasInstance => instance != null;

    public static bool TryGetExisting(out StorySequenceCoordinator coordinator)
    {
        coordinator = instance;
        return coordinator != null;
    }

    public static bool IsStorySequenceActive =>
        instance != null && instance.activeTokens.Count > 0;

    public static int ActiveOwnerCount =>
        instance == null ? 0 : instance.activeTokens.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isShuttingDown = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static StorySequenceToken Acquire(object owner)
    {
        if (owner == null)
            return null;

        StorySequenceCoordinator coordinator = Instance;
        return coordinator == null ? null : coordinator.AcquireInternal(
            owner.GetHashCode(), owner.ToString());
    }

    public static StorySequenceToken Acquire(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        StorySequenceCoordinator coordinator = Instance;
        return coordinator == null ? null : coordinator.AcquireInternal(
            ownerId.GetHashCode(), ownerId);
    }

    public static StorySequenceToken AcquireSequence(object owner)
    {
        return Acquire(owner);
    }

    public static StorySequenceToken AcquireSequence(string ownerId)
    {
        return Acquire(ownerId);
    }

    public static void ReleaseSequence(StorySequenceToken token)
    {
        if (token != null)
            token.Release();
    }

    public void Register(GameplayHUDTarget target)
    {
        if (target == null)
            return;

        targets.Add(target);
        target.ApplyPresentationState(IsStorySequenceActive, false);
    }

    public void Unregister(GameplayHUDTarget target)
    {
        if (target != null)
            targets.Remove(target);
    }

    internal void Release(StorySequenceToken token)
    {
        if (token == null || !token.IsValid || token.Coordinator != this)
            return;

        bool wasActive = activeTokens.Count > 0;
        if (!activeTokens.Remove(token.OwnerKey))
            return;

        token.Invalidate();
        bool active = activeTokens.Count > 0;
        if (wasActive != active)
            ApplyState(active, true);
    }

    private StorySequenceToken AcquireInternal(int ownerKey, string ownerLabel)
    {
        StorySequenceToken existing;
        if (activeTokens.TryGetValue(ownerKey, out existing) && existing.IsValid)
            return existing;

        bool wasActive = activeTokens.Count > 0;
        StorySequenceToken token = new StorySequenceToken(this, ownerKey, ownerLabel);
        activeTokens[ownerKey] = token;

        if (!wasActive)
            ApplyState(true, true);

        return token;
    }

    private void ApplyState(bool active, bool animate)
    {
        GameplayHUDTarget[] snapshot = new GameplayHUDTarget[targets.Count];
        targets.CopyTo(snapshot);

        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] != null)
                snapshot[i].ApplyPresentationState(active, animate);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }
}

public sealed class StorySequenceToken
{
    internal readonly StorySequenceCoordinator Coordinator;
    internal readonly int OwnerKey;
    public string OwnerLabel { get; private set; }
    public bool IsValid { get; private set; }

    internal StorySequenceToken(
        StorySequenceCoordinator coordinator,
        int ownerKey,
        string ownerLabel)
    {
        Coordinator = coordinator;
        OwnerKey = ownerKey;
        OwnerLabel = ownerLabel;
        IsValid = true;
    }

    public void Release()
    {
        if (!IsValid)
            return;

        if (Coordinator != null)
            Coordinator.Release(this);
        else
            Invalidate();
    }

    internal void Invalidate()
    {
        IsValid = false;
    }
}
