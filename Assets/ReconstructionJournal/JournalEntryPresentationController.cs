using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public sealed class JournalEntryPresentationController : MonoBehaviour
{
    private const int NotificationSortingOrder = 250;
    private static JournalEntryPresentationController instance;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float holdDuration = 0.4f;
    [SerializeField, Min(0f)] private float notificationFadeDuration = 0.2f;

    [Header("Presentation")]
    [SerializeField] private bool enableDiscoveryPopup = false;
    [SerializeField] private Sprite notificationBackgroundSprite;

    private readonly Queue<PresentationRequest> requests = new Queue<PresentationRequest>();
    private readonly HashSet<JournalEntryUnlockedInfo> queuedKeys = new HashSet<JournalEntryUnlockedInfo>();
    private JournalEntryNotificationView notificationView;
    private Coroutine presentationRoutine;
    private bool disabledPresentationCleared;
    private InventoryUI inventoryUI;
    private ClarityDocumentViewer documentViewer;
    private CameraFocusManager cameraFocusManager;
    private string resolvedScenePath = string.Empty;

    public static JournalEntryPresentationController Instance => instance;
    public int PendingCount => enableDiscoveryPopup ? requests.Count : 0;
    public bool IsPresenting => enableDiscoveryPopup && presentationRoutine != null;
    public JournalEntryUnlockedInfo LastCompletedPresentation { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        JournalEntryPresentationController prefab = Resources.Load<JournalEntryPresentationController>("JournalEntryPresentationController");
        if (prefab != null) { instance = Instantiate(prefab); instance.name = "JournalEntryPresentationController"; return; }
        instance = new GameObject("JournalEntryPresentationController").AddComponent<JournalEntryPresentationController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildNotificationView();
    }

    private void OnEnable()
    {
        ReconstructionJournalManager.NewEntryUnlocked += Enqueue;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        ReconstructionJournalManager.NewEntryUnlocked -= Enqueue;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>Cancels notifications from the previous playthrough without unsubscribing the service.</summary>
    public void ResetForNewGame()
    {
        StopAllCoroutines();
        presentationRoutine = null;
        requests.Clear();
        queuedKeys.Clear();
        LastCompletedPresentation = default;
        notificationView?.SetVisibleAmount(0f);
        inventoryUI = null;
        documentViewer = null;
        cameraFocusManager = null;
        resolvedScenePath = string.Empty;
    }

    private void Update()
    {
        if (!enableDiscoveryPopup)
        {
            if (!disabledPresentationCleared)
            {
                ClearDisabledPresentation();
                disabledPresentationCleared = true;
            }
            return;
        }

        disabledPresentationCleared = false;
        ResolveSceneReferencesIfNeeded();
        if (presentationRoutine == null && requests.Count > 0 && IsPresentationSafe())
            presentationRoutine = StartCoroutine(PresentFrontRequest());
    }

    private void Enqueue(JournalEntryUnlockedInfo entry)
    {
        if (!enableDiscoveryPopup)
        {
            if (!disabledPresentationCleared)
            {
                ClearDisabledPresentation();
                disabledPresentationCleared = true;
            }
            return;
        }

        disabledPresentationCleared = false;
        if (string.IsNullOrWhiteSpace(entry.EntryID) || !queuedKeys.Add(entry)) return;
        requests.Enqueue(new PresentationRequest(entry));
        Debug.Log("Journal presentation queued: " + entry.Category + " / " + entry.EntryID + " / " + entry.DisplayTitle + ".");
    }

    private void ClearDisabledPresentation()
    {
        if (presentationRoutine != null || requests.Count > 0)
            StopAllCoroutines();

        presentationRoutine = null;
        requests.Clear();
        queuedKeys.Clear();
        notificationView?.SetVisibleAmount(0f);
    }

    private IEnumerator PresentFrontRequest()
    {
        PresentationRequest request = requests.Peek();
        JournalEntryUnlockedInfo entry = request.Entry;
        ResolveSceneReferences(true);
        if (!IsPresentationSafe()) { presentationRoutine = null; yield break; }
        notificationView.Show(entry);
        notificationView.SetVisibleAmount(0f);
        bool interrupted = false;
        yield return AnimatePhase(notificationFadeDuration, t => notificationView.SetVisibleAmount(t), () => interrupted = true);
        if (!interrupted)
        {
            notificationView.SetVisibleAmount(1f);
            yield return AnimatePhase(holdDuration, null, () => interrupted = true);
        }
        if (!interrupted)
            yield return AnimatePhase(notificationFadeDuration, t => notificationView.SetVisibleAmount(1f - t), () => interrupted = true);
        notificationView.SetVisibleAmount(0f);
        if (!interrupted)
        {
            requests.Dequeue();
            queuedKeys.Remove(entry);
            LastCompletedPresentation = entry;
        }
        presentationRoutine = null;
    }

    private IEnumerator AnimatePhase(float duration, System.Action<float> update, System.Action interrupted)
    {
        if (duration <= 0f) { update?.Invoke(1f); yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsPresentationSafe()) { interrupted?.Invoke(); yield break; }
            elapsed += Time.unscaledDeltaTime;
            update?.Invoke(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        update?.Invoke(1f);
    }

    private bool IsPresentationSafe() => !HasExternalPresentationOwner() && !HasVisibleTransition();

    private bool HasExternalPresentationOwner()
    {
        if (StorySequenceCoordinator.IsStorySequenceActive) return true;
        if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive) return true;
        if (GameplaySystemTutorialManager.HasInstance)
        {
            GameplaySystemTutorialManager tutorial = GameplaySystemTutorialManager.Instance;
            if (tutorial.IsPresentationVisible) return true;
        }
        if (ReconstructionJournalManager.Instance != null && ReconstructionJournalManager.Instance.IsOpen) return true;
        if (inventoryUI != null && inventoryUI.IsOpen) return true;
        if (documentViewer != null && documentViewer.IsOpen) return true;
        return cameraFocusManager != null && cameraFocusManager.IsFocusing;
    }

    private bool HasVisibleTransition()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy || canvas.sortingOrder < 32100) continue;
            CanvasGroup[] groups = canvas.GetComponentsInChildren<CanvasGroup>(true);
            for (int g = 0; g < groups.Length; g++)
                if (groups[g].gameObject.activeInHierarchy && groups[g].alpha > 0.01f) return true;
            if (groups.Length > 0) continue;
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
                if (graphics[g].gameObject.activeInHierarchy && graphics[g].enabled && graphics[g].color.a > 0.01f) return true;
        }
        return false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) { resolvedScenePath = string.Empty; ResolveSceneReferences(true); }

    private void ResolveSceneReferencesIfNeeded()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(resolvedScenePath, activeScene.path, System.StringComparison.Ordinal)) ResolveSceneReferences(false);
    }

    private void ResolveSceneReferences(bool force)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!force && string.Equals(resolvedScenePath, activeScene.path, System.StringComparison.Ordinal)) return;
        resolvedScenePath = activeScene.path;
        inventoryUI = FindAnyObjectByType<InventoryUI>();
        documentViewer = FindAnyObjectByType<ClarityDocumentViewer>();
        cameraFocusManager = FindAnyObjectByType<CameraFocusManager>();
    }

    private void BuildNotificationView()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = NotificationSortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        GameObject panelObject = new GameObject("Journal Entry Notification", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -48f);
        panel.sizeDelta = new Vector2(560f, 138f);
        Image background = panelObject.GetComponent<Image>();
        background.sprite = notificationBackgroundSprite;
        background.type = Image.Type.Sliced;
        background.color = notificationBackgroundSprite != null
            ? new Color(1f, 1f, 1f, 0.92f)
            : new Color(60f / 255f, 48f / 255f, 40f / 255f, 0.92f);
        background.raycastTarget = false;
        TMP_Text heading = CreateText(panel, "Heading", -14f, 22f, 17f, FontStyles.Bold, new Color(182f / 255f, 155f / 255f, 112f / 255f, 1f));
        TMP_Text category = CreateText(panel, "Category", -40f, 20f, 15f, FontStyles.Normal, new Color(213f / 255f, 199f / 255f, 175f / 255f, 1f));
        TMP_Text title = CreateText(panel, "Title", -66f, 58f, 24f, FontStyles.Normal, new Color(245f / 255f, 232f / 255f, 212f / 255f, 1f));
        notificationView = panelObject.AddComponent<JournalEntryNotificationView>();
        notificationView.Configure(group, heading, category, title);
    }

    private static TMP_Text CreateText(RectTransform parent, string name, float top, float height, float fontSize, FontStyles fontStyle, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(24f, top - height);
        rect.offsetMax = new Vector2(-24f, top);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private sealed class PresentationRequest
    {
        public JournalEntryUnlockedInfo Entry { get; }
        public PresentationRequest(JournalEntryUnlockedInfo entry) { Entry = entry; }
    }
}
