using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only; read transitions belong to the category button callbacks.</summary>
[DisallowMultipleComponent]
public sealed class JournalEntryDiscoveryVisual : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private Image unreadDot;
    [SerializeField] private CanvasGroup readCheck;

    private const float CycleDuration = 1.8f;
    private const float PeakScale = 1.10f;
    private const float MinimumAlpha = 0.45f;
    private const float AcknowledgmentDuration = 0.7f;
    private static readonly Color UnreadTitleColor = new Color32(159, 53, 53, 255);
    private static readonly Color ReadColor = new Color32(57, 115, 77, 255);

    private RectTransform row;
    private Color neutralColor;
    private Vector3 neutralScale;
    private JournalEntryCategory category;
    private string entryID;
    private string readKey;
    private bool initialized;
    private bool bound;
    private bool unread;
    private bool acknowledging;
    private float elapsed;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized || title == null || visualRoot == null || visualGroup == null ||
            unreadDot == null || readCheck == null) return;
        row = transform as RectTransform;
        neutralColor = title.color;
        neutralScale = visualRoot.localScale;
        initialized = true;
        FitVisualToRow();
    }

    private void OnEnable()
    {
        Initialize();
        SessionStoryState.FlagChanged += HandleFlagChanged;
        if (bound) RefreshState();
    }

    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= HandleFlagChanged;
        acknowledging = false;
        elapsed = 0f;
        RestoreNeutral();
    }

    public void Bind(JournalEntryCategory entryCategory, string id)
    {
        Initialize();
        category = entryCategory;
        entryID = id;
        readKey = "journal.e12.read:" + category + ":" + id;
        bound = true;
        RefreshState();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (bound && key == readKey && !acknowledging) RefreshState();
    }

    private void RefreshState()
    {
        if (!initialized) return;
        acknowledging = false;
        elapsed = 0f;
        RestoreNeutral();
        unread = JournalEntryReadState.IsUnread(category, entryID);
        if (unread)
        {
            title.color = UnreadTitleColor;
            unreadDot.gameObject.SetActive(true);
        }
    }

    // Called only after the existing MarkRead reports a successful first transition.
    public void AcknowledgeFirstRead()
    {
        if (!initialized || !bound || !isActiveAndEnabled) return;
        unread = false;
        acknowledging = true;
        elapsed = 0f;
        visualRoot.localScale = neutralScale;
        visualGroup.alpha = 1f;
        title.color = ReadColor;
        unreadDot.gameObject.SetActive(false);
        readCheck.gameObject.SetActive(true);
        readCheck.alpha = 1f;
    }

    private static float EvaluateWave(float seconds)
    {
        return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * seconds / CycleDuration);
    }

    private void Update()
    {
        if (!bound || !initialized) return;
        if (acknowledging)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / AcknowledgmentDuration));
            // Fade the green highlight back to neutral without blanking the title.
            title.color = Color.Lerp(ReadColor, neutralColor, t);
            readCheck.alpha = 1f - t;
            if (elapsed >= AcknowledgmentDuration)
            {
                acknowledging = false;
                RestoreNeutral();
            }
        }
        else if (unread)
        {
            elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, CycleDuration);
            float wave = EvaluateWave(elapsed);
            visualRoot.localScale = neutralScale * Mathf.Lerp(1f, PeakScale, wave);
            visualGroup.alpha = Mathf.Lerp(1f, MinimumAlpha, wave);
        }
    }

    private void RestoreNeutral()
    {
        if (!initialized) return;
        visualRoot.localScale = neutralScale;
        visualGroup.alpha = 1f;
        title.color = neutralColor;
        unreadDot.gameObject.SetActive(false);
        readCheck.gameObject.SetActive(false);
        readCheck.alpha = 1f;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (initialized) FitVisualToRow();
    }

    private void FitVisualToRow()
    {
        // Reserve the maximum pulse footprint once per row size change.
        // Only this child is resized/scaled; the layout-driven row stays untouched.
        float width = Mathf.Max(0f, row.rect.width - 16f) / PeakScale;
        float height = Mathf.Max(0f, row.rect.height - 6f) / PeakScale;
        visualRoot.anchorMin = visualRoot.anchorMax = new Vector2(0f, 0.5f);
        visualRoot.pivot = new Vector2(0f, 0.5f);
        visualRoot.anchoredPosition = new Vector2(8f, 0f);
        visualRoot.sizeDelta = new Vector2(width, height);
    }
}