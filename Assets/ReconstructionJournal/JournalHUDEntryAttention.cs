using UnityEngine;
using UnityEngine.UI;

/// <summary>Session unread state and native Journal particle presentation.</summary>
[DefaultExecutionOrder(-840)]
[DisallowMultipleComponent]
public sealed class JournalHUDEntryAttention : MonoBehaviour
{
    public const string UnseenFlag = "journal.has_unseen_entries";
    private const string PendingFlag = "journal.pending_entry_attention";
    private const float CoalesceDelay = 0.18f;
    private const float RefreshInterval = 3f;
    private const int EffectLayer = 30;
    private const int RenderSize = 512;
    private const float BaseViewSize = 200f;
    private const float CaptureHalfSize = 15f;

    // Artistic settings are authored on ReconstructionJournalManager.
    private Vector2 journalSparkleOffset = Vector2.zero;
    private float journalSparkleScale = 1f;

    private static JournalHUDEntryAttention instance;
    private ReconstructionJournalManager journal;
    private RectTransform button;
    private JournalHUDEntryPulse pulse;
    private RawImage sparkleView;
    private Material compositeMaterial;
    private Transform outerContainer;
    private GameObject effect;
    private ParticleSystem rootParticles;
    private ParticleSystemRenderer[] renderers;
    private Camera effectCamera;
    private RenderTexture effectTexture;
    private float requestTime;
    private float lastBurst = float.NegativeInfinity;
    private float safeSince = -1f;
    private bool visualRunning;

    public static JournalHUDEntryAttention Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
            new GameObject("Journal HUD Entry Attention").AddComponent<JournalHUDEntryAttention>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        ReconstructionJournalManager.NewEntryUnlocked += HandleEntryUnlocked;
        Camera.onPreCull += HandleCameraPreCull;
        Camera.onPostRender += HandleCameraPostRender;
    }

    private void OnDisable()
    {
        ReconstructionJournalManager.NewEntryUnlocked -= HandleEntryUnlocked;
        Camera.onPreCull -= HandleCameraPreCull;
        Camera.onPostRender -= HandleCameraPostRender;
        InterruptAttention(true);
        StopVisual();
    }

    public void Configure(ReconstructionJournalManager owner, RectTransform target,
        JournalHUDEntryPulse entryPulse, Vector2 sparkleOffset, float sparkleScale)
    {
        InterruptAttention(true);
        StopVisual();
        if (sparkleView != null) Destroy(sparkleView.gameObject);
        journal = owner;
        button = target;
        pulse = entryPulse;
        journalSparkleOffset = sparkleOffset;
        journalSparkleScale = sparkleScale;
        safeSince = -1f;
        if (button != null) CreateSparkleView();
    }

    private void HandleEntryUnlocked(JournalEntryUnlockedInfo entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EntryID) ||
            (ReconstructionJournalManager.Instance != null && ReconstructionJournalManager.Instance.IsOpen)) return;
        SessionStoryState.SetFlag(UnseenFlag, true);
        if ((pulse != null && pulse.IsPulsing) ||
            (Time.unscaledTime - lastBurst < RefreshInterval && IsAttentionSafe())) return;
        requestTime = Time.unscaledTime;
        SessionStoryState.SetFlag(PendingFlag, true);
    }

    public void JournalOpened()
    {
        SessionStoryState.SetFlag(UnseenFlag, false);
        SessionStoryState.SetFlag(PendingFlag, false);
        InterruptAttention(false);
        StopVisual();
    }

    private void LateUpdate()
    {
        if (button == null || journal == null || sparkleView == null)
        {
            InterruptAttention(true);
            StopVisual();
            return;
        }
        // Read the manager's live Inspector values before applying the existing transform path.
        journalSparkleOffset = journal.JournalSparkleOffset;
        journalSparkleScale = journal.JournalSparkleScale;
        UpdatePresentationTransform();
        bool safe = IsAttentionSafe();
        if (!safe)
        {
            safeSince = -1f;
            InterruptAttention(!journal.IsOpen);
            StopVisual();
            return;
        }
        if (safeSince < 0f) safeSince = Time.unscaledTime;
        if (!SessionStoryState.GetFlag(UnseenFlag))
        {
            InterruptAttention(false);
            StopVisual();
            return;
        }
        bool pending = SessionStoryState.GetFlag(PendingFlag);
        if (pending && Time.unscaledTime - safeSince >= 0.25f &&
            Time.unscaledTime - requestTime >= CoalesceDelay && Time.unscaledTime - lastBurst >= RefreshInterval)
        {
            if (EnsureEffect() && pulse != null && pulse.PlayPulse())
            {
                // Restart the intact hierarchy together, using authored Play/Prewarm.
                StopVisual();
                BeginVisual();
                SessionStoryState.SetFlag(PendingFlag, false);
                lastBurst = Time.unscaledTime;
            }
        }
        else if (!pending && EnsureEffect())
        {
            // Restore native looping unread presentation without another scale bump.
            BeginVisual();
        }
    }

    private bool IsAttentionSafe()
    {
        if (journal == null || button == null || journal.IsOpen || journal.AttentionRevealPending || !IsHUDVisible()) return false;
        return true;
    }

    private bool IsHUDVisible()
    {
        if (!button.gameObject.activeInHierarchy || StorySequenceCoordinator.IsStorySequenceActive ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Journal)) return false;
        var hud = button.GetComponent<GameplayHUDTarget>();
        if (hud != null && !hud.IsAvailable) return false;
        var selectable = button.GetComponent<Button>();
        if (selectable != null && (!selectable.enabled || !selectable.IsInteractable())) return false;
        float alpha = 1f;
        bool ignoreGroups = false;
        for (Transform current = button; current != null; current = current.parent)
        {
            var canvas = current.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) return false;
            if (ignoreGroups) continue;
            foreach (var group in current.GetComponents<CanvasGroup>())
            {
                if (!group.enabled) continue;
                alpha *= group.alpha;
                if (!group.interactable) return false;
                if (group.ignoreParentGroups) ignoreGroups = true;
            }
        }
        return alpha >= 0.98f && button.GetComponentInParent<Canvas>() != null;
    }

    private void CreateSparkleView()
    {
        Canvas canvas = button.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("Journal Entry Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        sparkleView = go.GetComponent<RawImage>();
        sparkleView.raycastTarget = false;
        sparkleView.color = Color.white;
        sparkleView.enabled = false;
        UpdatePresentationTransform();
    }

    private void UpdatePresentationTransform()
    {
        if (sparkleView == null) return;
        RectTransform rect = sparkleView.rectTransform;
        RectTransform canvasRect = rect.parent as RectTransform;
        if (canvasRect == null || canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f) return;
        Vector3 center = canvasRect.InverseTransformPoint(button.TransformPoint(button.rect.center));
        Vector2 anchor = new Vector2(
            (center.x - canvasRect.rect.xMin) / canvasRect.rect.width,
            (center.y - canvasRect.rect.yMin) / canvasRect.rect.height);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = journalSparkleOffset;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        float scale = Mathf.Max(0.01f, journalSparkleScale);
        rect.sizeDelta = Vector2.one * (BaseViewSize * scale);

        // The complete captured star/light composition must remain visible,
        // rather than being occluded by the opaque book artwork.
        Transform branch = button;
        while (branch.parent != null && branch.parent != rect.parent) branch = branch.parent;
        if (branch.parent == rect.parent)
        {
            int branchIndex = branch.GetSiblingIndex();
            rect.SetSiblingIndex(branchIndex + (rect.GetSiblingIndex() < branchIndex ? 0 : 1));
        }
        if (outerContainer != null)
        {
            // Scale ONLY the outer container. All prefab-local transforms are untouched.
            outerContainer.localScale = Vector3.one * scale;
            effectCamera.transform.localPosition = new Vector3(0f, 0f, -40f * scale);
            effectCamera.orthographicSize = CaptureHalfSize * scale;
            effectCamera.nearClipPlane = Mathf.Max(0.01f, 0.1f * scale);
            effectCamera.farClipPlane = Mathf.Max(1f, 80f * scale);
        }
    }

    private bool EnsureEffect()
    {
        if (effect != null)
        {
            sparkleView.texture = effectTexture;
            sparkleView.material = compositeMaterial;
            return true;
        }
        // Resources wrapper is a native nested reference to the ORIGINAL prefab.
        // Its inactive outer root prevents playback until the entire setup is ready.
        var prefab = Resources.Load<GameObject>("JournalAttentionSparkle");
        var shader = Resources.Load<Shader>("JournalAttentionAdditive");
        if (prefab == null || shader == null) return false;
        outerContainer = new GameObject("Journal Sparkle Outer Container").transform;
        outerContainer.SetParent(transform, false);
        effect = Instantiate(prefab, outerContainer);
        rootParticles = effect.GetComponentInChildren<ParticleSystem>(true);
        renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        // Layer is rendering routing only. Never modify particle modules, materials,
        // renderer sizes, or the imported root/child local transforms.
        foreach (Transform node in effect.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = EffectLayer;
        SetNativeRendererVisibility(false);

        var cameraObject = new GameObject("Journal Sparkle Capture Camera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        effectCamera = cameraObject.GetComponent<Camera>();
        effectCamera.enabled = false;
        effectCamera.orthographic = true;
        effectCamera.aspect = 1f;
        effectCamera.depth = -1000f;
        effectCamera.cullingMask = 1 << EffectLayer;
        effectCamera.scene = gameObject.scene;
        effectCamera.clearFlags = CameraClearFlags.SolidColor;
        effectCamera.backgroundColor = Color.clear;
        effectCamera.allowHDR = false;
        effectCamera.allowMSAA = false;
        effectTexture = new RenderTexture(RenderSize, RenderSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "Journal Native Sparkle Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        effectTexture.Create();
        effectCamera.targetTexture = effectTexture;
        compositeMaterial = new Material(shader);
        sparkleView.texture = effectTexture;
        sparkleView.material = compositeMaterial;
        UpdatePresentationTransform();
        return rootParticles != null;
    }

    private void BeginVisual()
    {
        if (visualRunning) return;
        // Unity handles the complete original hierarchy's authored simulation,
        // prewarm, looping, billboard rendering, and shared materials normally.
        effect.SetActive(true);
        rootParticles.Play(true);
        visualRunning = true;
        sparkleView.enabled = true;
        effectCamera.enabled = true;
    }

    private void HandleCameraPreCull(Camera renderingCamera)
    {
        // Route only our instance to its capture camera. Gameplay camera properties
        // remain untouched, even when their culling masks include the effect layer.
        SetNativeRendererVisibility(visualRunning && renderingCamera == effectCamera);
    }

    private void HandleCameraPostRender(Camera renderingCamera)
    {
        if (renderingCamera == effectCamera) SetNativeRendererVisibility(false);
    }

    private void SetNativeRendererVisibility(bool visible)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers)
            if (renderer != null) renderer.forceRenderingOff = !visible;
    }

    private void InterruptAttention(bool preservePending)
    {
        if (pulse == null) return;
        if (preservePending && pulse.IsPulsing && SessionStoryState.GetFlag(UnseenFlag))
            SessionStoryState.SetFlag(PendingFlag, true);
        pulse.CancelPulse();
    }

    private void StopVisual()
    {
        if (effectCamera != null) effectCamera.enabled = false;
        if (visualRunning && rootParticles != null)
            rootParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (effect != null) effect.SetActive(false);
        visualRunning = false;
        SetNativeRendererVisibility(false);
        if (sparkleView != null) sparkleView.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sparkleView != null) Destroy(sparkleView.gameObject);
        if (effectCamera != null) effectCamera.targetTexture = null;
        if (effectTexture != null) { effectTexture.Release(); Destroy(effectTexture); }
        if (compositeMaterial != null) Destroy(compositeMaterial);
    }
}
