using DialogueEditor;
using UnityEngine;

public sealed class AlingIkaQuestController : MonoBehaviour
{
    public enum QuestState
    {
        NotStarted,
        Active,
        PaymentReceived,
        Completed
    }

    private const string StartedFlag = "aling_ika_started";
    private const string PaymentReceivedFlag = "aling_ika_payment_received";
    private const string CompletedFlag = "aling_ika_completed";
    private const string PaymentItemId = "aling_ika_payment";

    [Header("Interaction Triggers")]
    [SerializeField] private NPCConversationTrigger alingIkaTrigger;
    [SerializeField] private NPCConversationTrigger purpleScarfGirlTrigger;

    [Header("Aling Ika Conversations")]
    [SerializeField] private NPCConversation alingIkaFirstConversation;
    [SerializeField] private NPCConversation alingIkaActiveConversation;
    [SerializeField] private NPCConversation alingIkaPaymentConversation;
    [SerializeField] private NPCConversation alingIkaCompletedConversation;

    [Header("Purple-Scarf Girl Conversations")]
    [SerializeField] private NPCConversation girlBeforeQuestConversation;
    [SerializeField] private NPCConversation girlActiveConversation;
    [SerializeField] private NPCConversation girlAfterPaymentConversation;

    [Header("Quest Item")]
    [SerializeField] private InventoryItemData paymentItem;

    [Header("Task Feedback")]
    [SerializeField] private string taskId;
    [SerializeField, TextArea(2, 4)] private string returnPaymentObjective;

    private QuestState lastKnownState;

    public QuestState CurrentState
    {
        get
        {
            if (SessionStoryState.GetFlag(CompletedFlag))
                return QuestState.Completed;

            if (SessionStoryState.GetFlag(PaymentReceivedFlag))
                return QuestState.PaymentReceived;

            if (SessionStoryState.GetFlag(StartedFlag))
                return QuestState.Active;

            return QuestState.NotStarted;
        }
    }

    private void OnEnable()
    {
        lastKnownState = CurrentState;

        if (alingIkaTrigger != null)
            alingIkaTrigger.OnConversationFinished.AddListener(
                HandleAlingIkaConversationFinished);

        if (purpleScarfGirlTrigger != null)
            purpleScarfGirlTrigger.OnConversationFinished.AddListener(
                HandleGirlConversationFinished);

        RefreshConversationSelection();
    }

    private void OnDisable()
    {
        if (alingIkaTrigger != null)
            alingIkaTrigger.OnConversationFinished.RemoveListener(
                HandleAlingIkaConversationFinished);

        if (purpleScarfGirlTrigger != null)
            purpleScarfGirlTrigger.OnConversationFinished.RemoveListener(
                HandleGirlConversationFinished);
    }

    private void HandleAlingIkaConversationFinished()
    {
        if (CurrentState == QuestState.PaymentReceived)
            TryCompleteQuest();

        RefreshConversationSelection();
        UpdateTaskFeedback();
    }

    private void HandleGirlConversationFinished()
    {
        if (CurrentState != QuestState.Active)
            return;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || paymentItem == null)
            return;

        if (paymentItem.ItemID != PaymentItemId)
        {
            Debug.LogError(
                $"Aling Ika payment item must use the ID '{PaymentItemId}'.",
                this);
            return;
        }

        if (inventory.HasItem(PaymentItemId) || inventory.AddItem(paymentItem))
        {
            SessionStoryState.SetFlag(PaymentReceivedFlag, true);
            RefreshConversationSelection();
            UpdateTaskFeedback();
        }
    }

    private void TryCompleteQuest()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || !inventory.HasItem(PaymentItemId))
            return;

        if (inventory.RemoveItem(PaymentItemId))
            SessionStoryState.SetFlag(CompletedFlag, true);
    }

    private void RefreshConversationSelection()
    {
        switch (CurrentState)
        {
            case QuestState.NotStarted:
                SetAlingIkaConversations(
                    alingIkaFirstConversation,
                    alingIkaActiveConversation);
                SetGirlConversation(girlBeforeQuestConversation);
                break;

            case QuestState.Active:
                SetAlingIkaConversations(
                    alingIkaActiveConversation,
                    alingIkaActiveConversation);
                SetGirlConversation(girlActiveConversation);
                break;

            case QuestState.PaymentReceived:
                NPCConversation paymentConversation = HasPaymentItem()
                    ? alingIkaPaymentConversation
                    : alingIkaActiveConversation;
                SetAlingIkaConversations(
                    paymentConversation,
                    paymentConversation);
                SetGirlConversation(girlAfterPaymentConversation);
                break;

            case QuestState.Completed:
                SetAlingIkaConversations(
                    alingIkaCompletedConversation,
                    alingIkaCompletedConversation);
                SetGirlConversation(girlAfterPaymentConversation);
                break;
        }
    }

    private void SetAlingIkaConversations(
        NPCConversation firstConversation,
        NPCConversation repeatConversation)
    {
        if (alingIkaTrigger != null)
        {
            alingIkaTrigger.SetConversations(
                firstConversation,
                repeatConversation);
        }
    }

    private void SetGirlConversation(NPCConversation conversation)
    {
        if (purpleScarfGirlTrigger != null)
            purpleScarfGirlTrigger.SetConversations(conversation, conversation);
    }

    private static bool HasPaymentItem()
    {
        return InventoryManager.Instance != null &&
               InventoryManager.Instance.HasItem(PaymentItemId);
    }

    private void UpdateTaskFeedback()
    {
        QuestState currentState = CurrentState;
        if (currentState == lastKnownState || TaskManager.Instance == null)
            return;

        if (lastKnownState == QuestState.NotStarted &&
            currentState == QuestState.Active)
        {
            TaskManager.Instance.StartTask(taskId);
        }
        else if (lastKnownState == QuestState.Active &&
                 currentState == QuestState.PaymentReceived)
        {
            TaskManager.Instance.UpdateTask(taskId, returnPaymentObjective);
        }
        else if (lastKnownState == QuestState.PaymentReceived &&
                 currentState == QuestState.Completed)
        {
            TaskManager.Instance.CompleteTask(taskId);
        }

        lastKnownState = currentState;
    }
}
