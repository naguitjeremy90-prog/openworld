using System;
using System.Collections.Generic;
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
    private const string MainTaskId = "main_investigate_pili";
    private const string InquiryStage = "ask_aling_ika_about_maestro";
    private const string MaestroStage = "maestro_ben_lead";
    private const string ResidentLeadFlag = "randomhouse_maestro_lead_received";
    private const string MaestroLeadFlag = "maestro_ben_lead_received";

    [Header("Interaction Triggers")]
    [SerializeField] private NPCConversationTrigger alingIkaTrigger;
    [SerializeField] private NPCConversationTrigger purpleScarfGirlTrigger;

    [Header("Aling Ika Conversations")]
    [SerializeField] private NPCConversation alingIkaFirstConversation;
    [SerializeField] private NPCConversation alingIkaActiveConversation;
    [SerializeField] private NPCConversation alingIkaPaymentConversation;
    [SerializeField] private NPCConversation alingIkaCompletedConversation;

    [Header("Existing Maestro Ben House Focus")]
    [SerializeField] private CameraFocusTrigger maestroHouseFocus;
    [SerializeField] private CameraFocusManager houseFocusManager;

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
    private enum Branch { None, Leave, Offer, Reminder, ReturnPayment, Maestro }
    private NPCConversation choiceConversation;
    private NPCConversation ownedChoiceConversation;
    private Branch selectedBranch;
    private QuestState choiceStartedState;
    private QuestState choiceBuiltState;
    private bool choiceBuiltWithMaestro;
    private bool choiceBuiltWithPayment;
    private bool girlAttemptActive;
    private bool pendingMaestroCompletion;
    private NodeEventHolder girlStartNode;

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

        ConversationManager.ConversationClosed += HandleConversationClosed;
        if (girlActiveConversation != null)
        {
            girlStartNode = girlActiveConversation.GetNodeData(
                girlActiveConversation.DeserializeForEditor().GetRootNode().ID);
            girlStartNode.Event.AddListener(HandleGirlConversationStarted);
        }

        RefreshConversationSelection();
    }

    private void OnDisable()
    {
        ConversationManager.ConversationClosed -= HandleConversationClosed;
        if (girlStartNode != null)
            girlStartNode.Event.RemoveListener(HandleGirlConversationStarted);
        girlStartNode = null;
        selectedBranch = Branch.None;
        ownedChoiceConversation = null;
        girlAttemptActive = false;
        pendingMaestroCompletion = false;
    }

    private void Update()
    {
        TryCommitMaestroLead();
        RefreshConversationSelection();
        UpdateTaskFeedback();
    }

    private void HandleGirlConversationStarted()
    {
        girlAttemptActive = isActiveAndEnabled && CurrentState == QuestState.Active &&
            purpleScarfGirlTrigger != null && purpleScarfGirlTrigger.isActiveAndEnabled &&
            purpleScarfGirlTrigger.LastStartedConversation == girlActiveConversation &&
            ConversationManager.Instance != null &&
            ConversationManager.Instance.ActiveConversation == girlActiveConversation;
    }

    private void HandleConversationClosed(NPCConversation conversation, bool completedNormally)
    {
        if (conversation == girlActiveConversation)
        {
            bool awardPayment = girlAttemptActive && completedNormally &&
                isActiveAndEnabled && purpleScarfGirlTrigger != null &&
                purpleScarfGirlTrigger.isActiveAndEnabled &&
                purpleScarfGirlTrigger.LastStartedConversation == conversation;
            girlAttemptActive = false;
            if (awardPayment)
                HandleGirlConversationFinished();
        }

        if (conversation != ownedChoiceConversation)
            return;
        Branch branch = selectedBranch;
        selectedBranch = Branch.None;
        ownedChoiceConversation = null;
        if (!completedNormally || !isActiveAndEnabled || alingIkaTrigger == null ||
            !alingIkaTrigger.isActiveAndEnabled ||
            alingIkaTrigger.LastStartedConversation != conversation)
            return;

        if (branch == Branch.Offer && choiceStartedState == QuestState.NotStarted &&
            CurrentState == QuestState.NotStarted)
            SessionStoryState.SetFlag(StartedFlag, true);
        else if (branch == Branch.ReturnPayment &&
                 choiceStartedState == QuestState.PaymentReceived &&
                 CurrentState == QuestState.PaymentReceived)
            TryCompleteQuest();
        else if (branch == Branch.Maestro && CanAskAboutMaestro())
        {
            pendingMaestroCompletion = true;
            TryCommitMaestroLead();
        }

        RefreshConversationSelection();
        UpdateTaskFeedback();
    }

    private bool CanAskAboutMaestro()
    {
        TaskManager manager = TaskManager.Instance;
        return SessionStoryState.GetFlag(ResidentLeadFlag) &&
            !SessionStoryState.GetFlag(MaestroLeadFlag) && manager != null &&
            manager.GetTaskState(MainTaskId) == TaskState.Active &&
            manager.IsCurrentStage(MainTaskId, InquiryStage);
    }

    private void TryCommitMaestroLead()
    {
        if (!pendingMaestroCompletion)
            return;
        if (!CanAskAboutMaestro() || alingIkaTrigger == null ||
            !alingIkaTrigger.isActiveAndEnabled)
        {
            pendingMaestroCompletion = false;
            return;
        }
        // Keep this accepted branch pending while an existing presentation owns the camera.
        if (Time.timeScale <= 0f || StorySequenceCoordinator.IsStorySequenceActive ||
            (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive) ||
            (IrisTransitionController.Instance != null && IrisTransitionController.Instance.IsCovered) ||
            houseFocusManager == null || !houseFocusManager.isActiveAndEnabled ||
            houseFocusManager.IsFocusing || maestroHouseFocus == null ||
            !maestroHouseFocus.isActiveAndEnabled)
            return;

        TaskManager manager = TaskManager.Instance;
        try { manager.AdvanceTaskStage(MainTaskId, MaestroStage); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        if (!manager.IsCurrentStage(MainTaskId, MaestroStage))
        {
            pendingMaestroCompletion = false;
            Debug.LogWarning("Aling Ika's Maestro lead could not advance the Main Task; the inquiry remains retryable.", this);
            return;
        }
        pendingMaestroCompletion = false;
        // Subscribers can throw after SetFlag stores its value; still invoke the authored focus.
        try { SessionStoryState.SetFlag(MaestroLeadFlag, true); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        if (SessionStoryState.GetFlag(MaestroLeadFlag))
            maestroHouseFocus.TriggerFocus();
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
        {
            SessionStoryState.SetFlag(CompletedFlag, true);
            if (GameplaySystemTutorialManager.HasInstance)
                GameplaySystemTutorialManager.Instance.NotifyInventoryPaymentReturned();
        }
    }

    private void RefreshConversationSelection()
    {
        ConversationManager manager = ConversationManager.Instance;
        if ((alingIkaTrigger != null && alingIkaTrigger.IsTalking) ||
            (manager != null && manager.ActiveConversation == choiceConversation && choiceConversation != null))
            return;

        bool canAsk = CanAskAboutMaestro() && !pendingMaestroCompletion;
        if (choiceConversation == null || choiceBuiltState != CurrentState ||
            choiceBuiltWithMaestro != canAsk || choiceBuiltWithPayment != HasPaymentItem())
            BuildChoiceConversation(canAsk);
        SetAlingIkaConversations(choiceConversation, choiceConversation);
        switch (CurrentState)
        {
            case QuestState.NotStarted:
                SetGirlConversation(girlBeforeQuestConversation);
                break;

            case QuestState.Active:
                SetGirlConversation(girlActiveConversation);
                break;

            case QuestState.PaymentReceived:
                SetGirlConversation(girlAfterPaymentConversation);
                break;

            case QuestState.Completed:
                SetGirlConversation(girlAfterPaymentConversation);
                break;
        }
    }

    private void BuildChoiceConversation(bool canAsk)
    {
        if (choiceConversation != null)
            Destroy(choiceConversation.gameObject);
        GameObject root = new GameObject("Aling Ika Conditional Conversation");
        root.transform.SetParent(transform, false);
        NPCConversation conversation = root.AddComponent<NPCConversation>();
        conversation.ParameterList = new List<EditableParameter>();
        EditableConversation editable = new EditableConversation();
        EditableSpeechNode greeting = new EditableSpeechNode
        {
            ID = 0, Name = "Aling Ika", Text = CurrentState == QuestState.Completed
                ? alingIkaCompletedConversation.DeserializeForEditor().GetRootNode().Text
                : "Ano ang maitutulong ko sa iyo, iho?"
        };
        greeting.EditorInfo.isRoot = true;
        editable.SpeechNodes.Add(greeting);
        int nextId = 1;
        if (canAsk)
            AddChoice(conversation, editable, greeting, ref nextId,
                "May kilala po ba kayong Maestro Ben?", Branch.Maestro, null, new[]
                {
                    "May kilala po ba kayong Maestro Ben?",
                    "Oo, iho. Kilala ko siya. Paminsan-minsan siyang bumibili rito.",
                    "Alam po ba ninyo kung saan siya nakatira?",
                    "Malapit sa simbahan ang bahay niya. Doon mo siya maaaring puntahan."
                });
        if (CurrentState == QuestState.NotStarted)
            AddChoice(conversation, editable, greeting, ref nextId,
                "May maitutulong po ba ako sa inyo?", Branch.Offer, alingIkaFirstConversation);
        else if (CurrentState == QuestState.Active || CurrentState == QuestState.PaymentReceived)
        {
            bool returningPayment = CurrentState == QuestState.PaymentReceived && HasPaymentItem();
            AddChoice(conversation, editable, greeting, ref nextId,
                returningPayment ? "Ito po ang bayad." : "Tungkol po sa inyong ipinapagawa...",
                returningPayment ? Branch.ReturnPayment : Branch.Reminder,
                returningPayment ? alingIkaPaymentConversation : alingIkaActiveConversation);
        }
        AddChoice(conversation, editable, greeting, ref nextId,
            "Sa susunod na lang po.", Branch.Leave, null);
        conversation.GetNodeData(0).Event.AddListener(() =>
        {
            ownedChoiceConversation = conversation;
            selectedBranch = Branch.None;
            choiceStartedState = CurrentState;
        });
        conversation.Serialize(editable);
        choiceConversation = conversation;
        choiceBuiltState = CurrentState;
        choiceBuiltWithMaestro = canAsk;
        choiceBuiltWithPayment = HasPaymentItem();
    }

    private void AddChoice(NPCConversation conversation, EditableConversation editable,
        EditableSpeechNode greeting, ref int nextId, string optionText, Branch branch,
        NPCConversation source, string[] maestroText = null)
    {
        EditableOptionNode option = new EditableOptionNode { ID = nextId++, Text = optionText };
        option.parentUIDs.Add(greeting.ID);
        greeting.Connections.Add(new EditableOptionConnection(option));
        editable.Options.Add(option);
        conversation.GetNodeData(option.ID).Event.AddListener(() =>
        {
            if (isActiveAndEnabled && ownedChoiceConversation == conversation &&
                alingIkaTrigger != null && alingIkaTrigger.isActiveAndEnabled &&
                alingIkaTrigger.LastStartedConversation == conversation &&
                ConversationManager.Instance != null &&
                ConversationManager.Instance.ActiveConversation == conversation)
                selectedBranch = branch;
        });
        List<EditableSpeechNode> nodes = source != null
            ? source.DeserializeForEditor().SpeechNodes : new List<EditableSpeechNode>();
        if (maestroText != null)
            for (int index = 0; index < maestroText.Length; index++)
                nodes.Add(new EditableSpeechNode
                {
                    Name = index % 2 == 0 ? "Miguel" : "Aling Ika", Text = maestroText[index]
                });
        EditableConversationNode previous = option;
        foreach (EditableSpeechNode node in nodes)
        {
            node.ID = nextId++;
            node.EditorInfo.isRoot = false;
            node.Connections.Clear();
            node.parentUIDs.Clear();
            node.parentUIDs.Add(previous.ID);
            previous.Connections.Add(new EditableSpeechConnection(node));
            editable.SpeechNodes.Add(node);
            previous = node;
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
