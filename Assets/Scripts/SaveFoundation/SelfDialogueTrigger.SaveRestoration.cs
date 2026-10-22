public partial class SelfDialogueTrigger
{
    // Only the audited one-shot opening trigger, whose completed chapter is a
    // prerequisite of both resume checkpoints. No conversation/events are invoked.
    internal void RestoreCompletedOpeningTrigger()
    {
        if (triggerOnce && gameObject.name == "ArrivalStart" && SessionStoryState.GetFlag("posada_first_sleep_completed"))
            hasTriggered = true;
    }
}
