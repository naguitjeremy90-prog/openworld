/// <summary>Movement controlled by its own NPC conversation trigger.</summary>
public interface INPCConversationMovement
{
    void SetConversationMovementPaused(bool paused);
}

/// <summary>Optional condition for showing and starting a direct NPC conversation.</summary>
public interface IInteractionAvailabilityCondition
{
    bool IsAvailable();
}
