using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tracks Inventory acquisitions and places one unread badge on the current scene HUD button.</summary>
[DefaultExecutionOrder(-839)]
public sealed class InventoryHUDItemAttention : MonoBehaviour
{
    private static InventoryHUDItemAttention instance;
    private InventoryUI owner;
    private RectTransform button;
    private RectTransform compensator;
    private InventoryNotificationPulse badge;
    private GameObject badgePrefab;
    private readonly List<CanvasGroup> groups = new List<CanvasGroup>(4);
    private readonly List<TabBadge> tabBadges = new List<TabBadge>(4);
    private InventoryUI tabOwner;
    private Button[] configuredTabButtons;
    private int unreadCount;
    private readonly int[] categoryUnreadCounts = new int[4];

    public static InventoryHUDItemAttention Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
            new GameObject("Inventory HUD Item Attention").AddComponent<InventoryHUDItemAttention>();
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
        InventoryItemReadState.StateChanged += HandleStateChanged;
        RefreshUnreadCount();
    }

    private void OnDisable() => InventoryItemReadState.StateChanged -= HandleStateChanged;

    public void Configure(InventoryUI nextOwner, RectTransform target)
    {
        if (owner == nextOwner && button == target && badge != null)
        {
            RefreshBadge();
            return;
        }
        ReleaseBadge();
        owner = nextOwner;
        button = target;
        if (button == null) return;

        badge = CreateBadge(button, "InventoryDiscoveryBadge", out compensator);
        if (badge == null) return;
        badge.SetVisible(false);
        RefreshUnreadCount();
        RefreshBadge();
    }

    public void ConfigureTabBadges(
        InventoryUI nextOwner, Button all, Button quest, Button key, Button document)
    {
        Button[] buttons = { all, quest, key, document };
        bool unchanged = tabOwner == nextOwner && configuredTabButtons != null;
        if (unchanged)
            for (int i = 0; i < buttons.Length; i++)
                if (configuredTabButtons[i] != buttons[i]) { unchanged = false; break; }
        if (unchanged)
        {
            RefreshUnreadCount();
            RefreshTabBadges();
            return;
        }

        ReleaseTabBadges();
        tabOwner = nextOwner;
        configuredTabButtons = buttons;
        ItemCategory?[] categories = { null, ItemCategory.Quest, ItemCategory.Key, ItemCategory.Document };
        string[] names = { "All", "Quest", "Key", "Document" };
        for (int i = 0; i < buttons.Length; i++)
        {
            Button tab = buttons[i];
            RectTransform target = tab != null ? tab.transform as RectTransform : null;
            if (target == null) continue;
            InventoryNotificationPulse pulse = CreateBadge(
                target, "Inventory" + names[i] + "UnreadBadge", out RectTransform scaleRoot);
            if (pulse == null) continue;
            pulse.SetVisible(false);
            tabBadges.Add(new TabBadge(target, scaleRoot, pulse, categories[i], new Vector2(7f, -7f)));
        }
        RefreshUnreadCount();
        RefreshTabBadges();
    }

    public void Detach(InventoryUI source)
    {
        if (owner == source)
        {
            ReleaseBadge();
            owner = null;
            button = null;
        }
        if (tabOwner == source) ReleaseTabBadges();
    }

    private void HandleStateChanged()
    {
        RefreshUnreadCount();
        RefreshBadge();
        RefreshTabBadges();
    }

    private void RefreshUnreadCount()
    {
        unreadCount = InventoryItemReadState.GetUnreadCount();
        categoryUnreadCounts[(int)ItemCategory.General] = InventoryItemReadState.GetUnreadCount(ItemCategory.General);
        categoryUnreadCounts[(int)ItemCategory.Quest] = InventoryItemReadState.GetUnreadCount(ItemCategory.Quest);
        categoryUnreadCounts[(int)ItemCategory.Key] = InventoryItemReadState.GetUnreadCount(ItemCategory.Key);
        categoryUnreadCounts[(int)ItemCategory.Document] = InventoryItemReadState.GetUnreadCount(ItemCategory.Document);
    }

    private InventoryNotificationPulse CreateBadge(
        RectTransform target, string visualName, out RectTransform scaleRoot)
    {
        scaleRoot = null;
        if (target == null) return null;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas == null) return null;
        badgePrefab = badgePrefab != null ? badgePrefab : Resources.Load<GameObject>("UI/InventoryDiscoveryBadge");
        if (badgePrefab == null)
        {
            Debug.LogError("Inventory notification badge visual is missing.", this);
            return null;
        }
        scaleRoot = new GameObject(visualName + "ScaleCompensator",
            typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        scaleRoot.SetParent(target, false);
        scaleRoot.GetComponent<LayoutElement>().ignoreLayout = true;
        scaleRoot.anchorMin = scaleRoot.anchorMax = Vector2.one;
        scaleRoot.pivot = new Vector2(0.5f, 0.5f);
        scaleRoot.sizeDelta = Vector2.zero;
        GameObject visual = Instantiate(badgePrefab, scaleRoot, false);
        visual.name = visualName;
        InventoryNotificationPulse pulse = visual.GetComponent<InventoryNotificationPulse>();
        if (pulse == null)
        {
            Debug.LogError("Inventory notification visual has no pulse component.", this);
            scaleRoot.gameObject.SetActive(false);
            Destroy(scaleRoot.gameObject);
            scaleRoot = null;
            return null;
        }
        return pulse;
    }

    private void RefreshBadge()
    {
        if (badge == null) return;
        bool visible = unreadCount > 0 && owner != null &&
                       button != null && IsHUDVisible();
        if (visible)
        {
            visible = UpdateBadgeTransform(button, compensator, new Vector2(8f, -20f));
        }
        badge.SetVisible(visible);
    }

    private void RefreshTabBadges()
    {
        foreach (TabBadge tabBadge in tabBadges)
        {
            if (tabBadge.Pulse == null || tabBadge.Button == null) continue;
            int count = tabBadge.Category.HasValue
                ? categoryUnreadCounts[(int)tabBadge.Category.Value]
                : unreadCount;
            bool visible = count > 0 && tabOwner != null && tabOwner.IsOpen &&
                           IsRectVisible(tabBadge.Button) &&
                           UpdateBadgeTransform(tabBadge.Button, tabBadge.ScaleRoot, tabBadge.Offset, 0.35f);
            tabBadge.Pulse.SetVisible(visible);
        }
    }

    private bool UpdateBadgeTransform(
        RectTransform target, RectTransform scaleRoot, Vector2 offset, float visualScale = 1f)
    {
        if (target == null || scaleRoot == null) return false;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null) return false;
        float x = canvasRect.InverseTransformVector(target.TransformVector(Vector3.right)).magnitude;
        float y = canvasRect.InverseTransformVector(target.TransformVector(Vector3.up)).magnitude;
        if (x <= 0.0001f || y <= 0.0001f) return false;
        scaleRoot.localScale = new Vector3(visualScale / x, visualScale / y, 1f);
        scaleRoot.anchoredPosition = new Vector2(offset.x / x, offset.y / y);
        return true;
    }

    private bool IsRectVisible(RectTransform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        float alpha = 1f;
        for (Transform current = target; current != null; current = current.parent)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) return false;
            groups.Clear();
            current.GetComponents(groups);
            foreach (CanvasGroup group in groups)
                if (group.enabled) alpha *= group.alpha;
        }
        return alpha >= 0.98f;
    }

    private bool IsHUDVisible()
    {
        if (!button.gameObject.activeInHierarchy || StorySequenceCoordinator.IsStorySequenceActive ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory)) return false;
        Button selectable = button.GetComponent<Button>();
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
        return alpha >= 0.98f;
    }

    private void LateUpdate()
    {
        if (button == null && owner != null) Detach(owner);
        else RefreshBadge();
        RefreshTabBadges();
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

    private void ReleaseTabBadges()
    {
        foreach (TabBadge tabBadge in tabBadges)
        {
            if (tabBadge.Pulse != null) tabBadge.Pulse.SetVisible(false);
            if (tabBadge.ScaleRoot != null)
            {
                tabBadge.ScaleRoot.gameObject.SetActive(false);
                Destroy(tabBadge.ScaleRoot.gameObject);
            }
        }
        tabBadges.Clear();
        tabOwner = null;
        configuredTabButtons = null;
    }

    private void OnDestroy()
    {
        ReleaseBadge();
        ReleaseTabBadges();
        if (instance == this) instance = null;
    }

    private sealed class TabBadge
    {
        public readonly RectTransform Button;
        public readonly RectTransform ScaleRoot;
        public readonly InventoryNotificationPulse Pulse;
        public readonly ItemCategory? Category;
        public readonly Vector2 Offset;

        public TabBadge(RectTransform button, RectTransform scaleRoot,
            InventoryNotificationPulse pulse, ItemCategory? category, Vector2 offset)
        {
            Button = button;
            ScaleRoot = scaleRoot;
            Pulse = pulse;
            Category = category;
            Offset = offset;
        }
    }
}
