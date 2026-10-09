using System;

/// <summary>Session-scoped tutorial completion state, separate from system ownership.</summary>
public static class GameplaySystemTutorialState
{
    private const string JournalSeenFlag = "journal_tutorial_seen";
    private const string InventorySeenFlag = "inventory_tutorial_seen";
    private const string InventoryFirstItemSeenFlag = "inventory_tutorial_first_item_seen";
    private const string InventoryIntroSeenFlag = "inventory_tutorial_intro_seen";
    private const string InventoryIntroStepKey = "inventory_tutorial_intro_step";
    private const string ClaritySeenFlag = "clarity_tutorial_seen";
    private const string ClarityDocumentSeenFlag = "clarity_document_tutorial_seen";
    private const string InventoryRequestsKey = "inventory_tutorial_pending_phases";
    private const string InventoryStepKey = "inventory_tutorial_pending_step";
    private const string InventoryRevealStartedFlag = "inventory_tutorial_reveal_started";
    private const string InventoryRevealReadyFlag = "inventory_tutorial_reveal_ready";

    internal enum InventoryPhase { Introduction = 1, PaymentReturn = 2, ItemDetails = 4 }

    // Inventory-only request bits. Each phase has its own completion condition.
    internal static bool InventoryTeachingPending =>
        InventoryInitialPending || InventoryReturnPending || InventoryItemPending;
    internal static bool InventoryInitialPending =>
        !InventoryIntroSeen && HasInventoryRequest(InventoryPhase.Introduction);
    internal static bool InventoryReturnPending =>
        !IsSeen(GameplaySystemId.Inventory) && HasInventoryRequest(InventoryPhase.PaymentReturn);
    internal static bool InventoryItemPending =>
        !InventoryFirstItemSeen && HasInventoryRequest(InventoryPhase.ItemDetails);
    internal static bool InventoryIntroSeen => SessionStoryState.GetFlag(InventoryIntroSeenFlag);
    internal static int InventoryIntroStep
    {
        get => SessionStoryState.GetInt(InventoryIntroStepKey);
        set => SessionStoryState.SetInt(InventoryIntroStepKey, value);
    }
    internal static int InventoryPendingStep
    {
        get => SessionStoryState.GetInt(InventoryStepKey);
        set => SessionStoryState.SetInt(InventoryStepKey, value);
    }
    internal static bool InventoryRevealStarted
    {
        get => SessionStoryState.GetFlag(InventoryRevealStartedFlag);
        set => SessionStoryState.SetFlag(InventoryRevealStartedFlag, value);
    }
    internal static bool InventoryRevealReady
    {
        get => SessionStoryState.GetFlag(InventoryRevealReadyFlag);
        set => SessionStoryState.SetFlag(InventoryRevealReadyFlag, value);
    }

    internal static void RequestInventoryTeaching(bool paymentReturned = false)
    {
        int requests = SessionStoryState.GetInt(InventoryRequestsKey);
        if (!InventoryIntroSeen) requests |= (int)InventoryPhase.Introduction;
        if (!InventoryFirstItemSeen) requests |= (int)InventoryPhase.ItemDetails;
        if (paymentReturned && !IsSeen(GameplaySystemId.Inventory))
            requests |= (int)InventoryPhase.PaymentReturn;
        SessionStoryState.SetInt(InventoryRequestsKey, requests);
        if (InventoryReturnPending && InventoryPendingStep < 2)
            InventoryPendingStep = 2;
    }

    private static bool HasInventoryRequest(InventoryPhase phase) =>
        (SessionStoryState.GetInt(InventoryRequestsKey) & (int)phase) != 0;

    private static void RemoveInventoryRequest(InventoryPhase phase) =>
        SessionStoryState.SetInt(InventoryRequestsKey,
            SessionStoryState.GetInt(InventoryRequestsKey) & ~(int)phase);

    internal static bool IsInventoryPhaseSeen(InventoryPhase phase) =>
        phase == InventoryPhase.Introduction ? InventoryIntroSeen :
        phase == InventoryPhase.ItemDetails ? InventoryFirstItemSeen : IsSeen(GameplaySystemId.Inventory);

    internal static bool TryGetNextInventoryPhase(bool hasItem, out InventoryPhase phase)
    {
        // Used only with an idle overlay; this preference never preempts active teaching.
        if (InventoryInitialPending) phase = InventoryPhase.Introduction;
        else if (InventoryReturnPending) phase = InventoryPhase.PaymentReturn;
        else if (InventoryItemPending && hasItem) phase = InventoryPhase.ItemDetails;
        else { phase = default; return false; }
        return true;
    }

    internal static void CompleteInventoryIntroduction()
    {
        RemoveInventoryRequest(InventoryPhase.Introduction);
        SessionStoryState.SetFlag(InventoryIntroSeenFlag, true);
    }

    public static bool IsSeen(GameplaySystemId system)
    {
        return SessionStoryState.GetFlag(GetSeenFlagId(system));
    }

    public static void SetSeen(GameplaySystemId system, bool seen)
    {
        if (system == GameplaySystemId.Inventory && seen)
        {
            RemoveInventoryRequest(InventoryPhase.PaymentReturn);
            InventoryPendingStep = 0;
        }
        SessionStoryState.SetFlag(GetSeenFlagId(system), seen);
    }

    public static bool InventoryFirstItemSeen =>
        SessionStoryState.GetFlag(InventoryFirstItemSeenFlag);

    public static void SetInventoryFirstItemSeen(bool seen)
    {
        if (seen) RemoveInventoryRequest(InventoryPhase.ItemDetails);
        SessionStoryState.SetFlag(InventoryFirstItemSeenFlag, seen);
    }

    public static bool ClarityDocumentSeen =>
        SessionStoryState.GetFlag(ClarityDocumentSeenFlag);

    public static string ClarityDocumentSeenFlagId => ClarityDocumentSeenFlag;

    public static void SetClarityDocumentSeen(bool seen)
    {
        SessionStoryState.SetFlag(ClarityDocumentSeenFlag, seen);
    }

    public static string GetSeenFlagId(GameplaySystemId system)
    {
        switch (system)
        {
            case GameplaySystemId.Journal:
                return JournalSeenFlag;
            case GameplaySystemId.Inventory:
                return InventorySeenFlag;
            case GameplaySystemId.Clarity:
                return ClaritySeenFlag;
            default:
                throw new ArgumentOutOfRangeException(nameof(system), system, null);
        }
    }
}
