using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Global unread-entry attention attached to the existing Journal HUD button.</summary>
[DefaultExecutionOrder(-840)]
[DisallowMultipleComponent]
public sealed class JournalHUDEntryAttention : MonoBehaviour
{
    // Retained for compatibility with existing development controls, not badge state.
    public const string UnseenFlag = "journal.has_unseen_entries";
    private static readonly Vector2 BadgeOffset = new Vector2(8f, -20f);
    private const float RefreshInterval = 0.25f;

    private static JournalHUDEntryAttention instance;
    private ReconstructionJournalManager journal;
    private RectTransform button;
    private RectTransform canvasRect;
    private RectTransform compensator;
    private JournalDiscoveryPulse badge;
    private GameplayHUDTarget hud;
    private Button selectable;
    private GameObject badgePrefab;
    private readonly List<CanvasGroup> groups = new List<CanvasGroup>(4);
    private int unreadCount;
    private float refreshElapsed;

    public static JournalHUDEntryAttention Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
            new GameObject("Journal HUD Entry Attention").AddComponent<JournalHUDEntryAttention>();
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

    private void OnEnable()
    {
        SessionStoryState.FlagChanged += HandleFlagChanged;
        RefreshUnreadCount();
    }

    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= HandleFlagChanged;
        if (badge != null) badge.SetVisible(false);
    }

    public void Configure(ReconstructionJournalManager owner, RectTransform target)
    {
        journal = owner;
        if (button == target && badge != null)
        {
            RefreshUnreadCount();
            return;
        }

        ReleaseBadge();
        button = target;
        if (button == null) return;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        hud = button.GetComponent<GameplayHUDTarget>();
        selectable = button.GetComponent<Button>();
        if (canvasRect == null) return;

        if (badgePrefab == null)
            badgePrefab = Resources.Load<GameObject>("UI/JournalDiscoveryBadge");
        if (badgePrefab == null)
        {
            Debug.LogError("Journal discovery badge prefab is missing.", this);
            return;
        }

        compensator = new GameObject("JournalBadgeScaleCompensator",
            typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        compensator.SetParent(button, false);
        compensator.GetComponent<LayoutElement>().ignoreLayout = true;
        compensator.anchorMin = compensator.anchorMax = Vector2.one;
        compensator.pivot = new Vector2(0.5f, 0.5f);
        compensator.sizeDelta = Vector2.zero;
        GameObject visual = Instantiate(badgePrefab, compensator, false);
        visual.name = "JournalDiscoveryBadge";
        badge = visual.GetComponent<JournalDiscoveryPulse>();
        if (badge == null)
        {
            Debug.LogError("Journal discovery badge pulse component is missing.", this);
            ReleaseBadge();
            return;
        }
        badge.SetVisible(false);
        RefreshUnreadCount();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (key.StartsWith("journal.e12.", System.StringComparison.Ordinal) ||
            key.StartsWith("journal_", System.StringComparison.Ordinal))
            RefreshUnreadCount();
    }

    private void RefreshUnreadCount()
    {
        unreadCount = JournalEntryReadState.UnreadCount;
        refreshElapsed = 0f;
    }

    // Opening does not consume unread state or dismiss the badge.
    public void JournalOpened() => RefreshUnreadCount();

    public void ResetForNewGame()
    {
        ReleaseBadge();
        journal = null;
        button = null;
        canvasRect = null;
        hud = null;
        selectable = null;
        unreadCount = 0;
        refreshElapsed = 0f;
    }

    private void LateUpdate()
    {
        // Also handles the existing silent New Game session reset without new APIs.
        refreshElapsed += Time.unscaledDeltaTime;
        if (refreshElapsed >= RefreshInterval) RefreshUnreadCount();
        if (badge == null) return;
        bool visible = unreadCount > 0 && journal != null && button != null &&
            !journal.AttentionRevealPending && IsHUDVisible();
        if (visible) visible = UpdateScaleCompensation();
        badge.SetVisible(visible);
    }

    private bool UpdateScaleCompensation()
    {
        if (canvasRect == null || compensator == null) return false;
        // Measure the button axes in root-canvas units, preserving canvas scaling.
        float x = canvasRect.InverseTransformVector(button.TransformVector(Vector3.right)).magnitude;
        float y = canvasRect.InverseTransformVector(button.TransformVector(Vector3.up)).magnitude;
        if (x <= 0.0001f || y <= 0.0001f) return false;
        compensator.localScale = new Vector3(1f / x, 1f / y, 1f);
        compensator.anchoredPosition = new Vector2(BadgeOffset.x / x, BadgeOffset.y / y);
        return true;
    }

    private bool IsHUDVisible()
    {
        if (!button.gameObject.activeInHierarchy || StorySequenceCoordinator.IsStorySequenceActive ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Journal)) return false;
        if (hud != null && !hud.IsAvailable) return false;
        if (selectable != null && (!selectable.enabled || !selectable.IsInteractable())) return false;

        float alpha = 1f;
        bool ignoreGroups = false;
        for (Transform current = button; current != null; current = current.parent)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) return false;
            if (ignoreGroups) continue;
            groups.Clear();
            current.GetComponents(groups);
            foreach (CanvasGroup group in groups)
            {
                if (!group.enabled) continue;
                alpha *= group.alpha;
                if (!group.interactable) return false;
                if (group.ignoreParentGroups) ignoreGroups = true;
            }
        }
        return alpha >= 0.98f && canvasRect != null;
    }

    private void ReleaseBadge()
    {
        if (badge != null) badge.SetVisible(false);
        if (compensator != null)
        {
            compensator.gameObject.SetActive(false);
            Destroy(compensator.gameObject);
        }
        badge = null;
        compensator = null;
    }

    private void OnDestroy()
    {
        ReleaseBadge();
        if (instance == this) instance = null;
    }
}
