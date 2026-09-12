using UnityEngine;

/// <summary>
/// Inspector-friendly bridge for a simple authored sequence. The sequence
/// controller or Timeline can call Begin/End without coordinator changes.
/// </summary>
public sealed class StorySequenceParticipant : MonoBehaviour
{
    [SerializeField] private string ownerId;
    [SerializeField] private bool releaseOnDisable = true;

    private StorySequenceToken token;

    public bool IsActive => token != null && token.IsValid;

    public void Begin()
    {
        if (IsActive)
            return;

        token = StorySequenceCoordinator.Acquire(
            string.IsNullOrWhiteSpace(ownerId)
                ? name + "#" + GetInstanceID()
                : ownerId);
    }

    public void End()
    {
        if (token == null)
            return;

        token.Release();
        token = null;
    }

    private void OnDisable()
    {
        if (releaseOnDisable)
            End();
    }
}
