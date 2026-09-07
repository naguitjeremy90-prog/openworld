using UnityEngine;

/// <summary>Bridges a completed conversation to generic TaskManager progress.</summary>
public sealed class TaskProgressTrigger : MonoBehaviour
{
    [SerializeField] private string taskId;
    [SerializeField] private string progressId;
    [SerializeField] private bool registerOnCompletion = true;
    [SerializeField] private bool once = true;
    [SerializeField] private int amount = 1;
    [SerializeField] private NPCConversationTrigger npcConversationTrigger;
    [SerializeField] private SelfDialogueTrigger selfDialogueTrigger;

    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // References can be assigned by scene/prefab deserialization after OnEnable.
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || !registerOnCompletion)
            return;

        bool attached = false;
        if (npcConversationTrigger != null)
        {
            npcConversationTrigger.ConversationFinished += RegisterProgress;
            attached = true;
        }
        if (selfDialogueTrigger != null)
        {
            selfDialogueTrigger.ConversationFinished += RegisterProgress;
            attached = true;
        }

        subscribed = attached;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (npcConversationTrigger != null)
            npcConversationTrigger.ConversationFinished -= RegisterProgress;
        if (selfDialogueTrigger != null)
            selfDialogueTrigger.ConversationFinished -= RegisterProgress;

        subscribed = false;
    }

    private void RegisterProgress()
    {
        if (TaskManager.Instance != null)
            TaskManager.Instance.RegisterProgress(taskId, progressId, amount, once);
    }
}
