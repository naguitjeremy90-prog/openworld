using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class ClarityDocumentIdEvent : UnityEvent<string>
{
}

[Serializable]
public sealed class ClarityDocumentRegionEvent :
    UnityEvent<ClarityDocumentRegion>
{
}

[DisallowMultipleComponent]
public sealed class ClarityDocumentViewer : MonoBehaviour
{
    private static readonly int LensCenterId =
        Shader.PropertyToID("_LensCenter");
    private static readonly int LensRadiusId =
        Shader.PropertyToID("_LensRadius");
    private static readonly int EdgeSoftnessId =
        Shader.PropertyToID("_EdgeSoftness");
    private static readonly int RevealId =
        Shader.PropertyToID("_Reveal");
    private static readonly int RevealModeId =
        Shader.PropertyToID("_RevealMode");

    [Header("Clarity")]
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField] private ClarityLensUI clarityLensUI;

    [Header("Document UI")]
    [SerializeField] private GameObject documentPanel;
    [SerializeField] private Graphic modalRaycastBlocker;
    [SerializeField] private RectTransform documentRect;
    [SerializeField] private AspectRatioFitter documentAspectFitter;
    [SerializeField] private Image baseDocumentImage;
    [SerializeField] private Image clearDocumentImage;
    [SerializeField] private Transform regionsRoot;
    [SerializeField] private Button closeButton;

    [Header("Base Presentation")]
    [SerializeField] private Color baseDocumentTint =
        new Color(0.48f, 0.48f, 0.48f, 0.78f);
    [SerializeField] private Color clearDocumentTint = Color.white;

    [Header("Reveal")]
    [SerializeField] private Shader revealShader;
    [SerializeField, Min(0f)] private float revealEdgeSoftnessPixels = 3f;

    [Header("Modal Input")]
    [Tooltip("Assign movement, menu keyboard handlers, and world interaction behaviours that must pause while a document is open.")]
    [SerializeField] private MonoBehaviour[] gameplayBehavioursToDisable =
        new MonoBehaviour[0];
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Inspector Events")]
    [SerializeField] private ClarityDocumentRegionEvent onRegionInvestigated;
    [SerializeField] private ClarityDocumentIdEvent onDocumentInvestigationCompleted;
    [SerializeField] private ClarityDocumentIdEvent onDocumentOpened;
    [SerializeField] private ClarityDocumentIdEvent onDocumentClosed;

    private readonly List<ClarityDocumentRegion> activeRegions =
        new List<ClarityDocumentRegion>();
    private readonly HashSet<ClarityDocumentRegion> invalidRegions =
        new HashSet<ClarityDocumentRegion>();

    private Material runtimeRevealMaterial;
    private bool usingObscuredDocument;
    private GameObject instantiatedRegionLayout;
    private bool[] previousBehaviourStates;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool modalStateCaptured;
    private bool completionRaised;

    public bool IsOpen { get; private set; }
    public string ActiveDocumentId { get; private set; } = string.Empty;

    public event Action<ClarityDocumentRegion> OnRegionInvestigated;
    public event Action<string> OnDocumentInvestigationCompleted;
    public event Action<string> OnDocumentOpened;
    public event Action<string> OnDocumentClosed;
    public static event Action<string, bool> AnyDocumentOpened;
    public static event Action<string, bool> AnyDocumentClosed;

    private void Awake()
    {
        CacheReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDocument);

        SetDocumentPanelVisible(false);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(closeKey))
            CloseDocument();
    }

    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        UpdateRevealMaterial();
        ProcessRegions();
    }

    private void OnDisable()
    {
        if (IsOpen || modalStateCaptured)
            CloseDocument();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseDocument);

        RestoreModalState();
        DestroyRuntimeRevealMaterial();
    }

    public bool OpenDocument(ClarityDocumentSource source)
    {
        if (source == null)
            return false;

        return OpenDocument(
            source.DocumentId,
            source.DocumentSprite,
            source.ObscuredDocumentSprite,
            source.RegionLayoutPrefab);
    }

    public bool OpenDocument(
        string documentId,
        Sprite documentSprite,
        GameObject regionLayoutPrefab = null)
    {
        return OpenDocument(documentId, documentSprite, null, regionLayoutPrefab);
    }

    public bool OpenDocument(
        string documentId,
        Sprite documentSprite,
        Sprite obscuredDocumentSprite,
        GameObject regionLayoutPrefab = null)
    {
        if (documentSprite == null)
        {
            Debug.LogWarning(
                "ClarityDocumentViewer cannot open a document without a sprite.",
                this);
            return false;
        }

        CacheReferences();
        if (clarityManager == null || clarityLensUI == null)
        {
            Debug.LogWarning(
                "ClarityDocumentViewer requires a ClarityManager and ClarityLensUI.",
                this);
            return false;
        }

        if (IsOpen || modalStateCaptured)
            CloseDocument();

        ActiveDocumentId = documentId ?? string.Empty;
        completionRaised = false;

        ConfigureDocumentImages(documentSprite, obscuredDocumentSprite);
        CreateRuntimeRevealMaterial();
        CreateRegionLayout(regionLayoutPrefab);
        CollectAndValidateRegions();

        CaptureModalState();
        IsOpen = true;
        SetDocumentPanelVisible(true);
        clarityManager.EnterDocumentMode();
        UpdateRevealMaterial();

        OnDocumentOpened?.Invoke(ActiveDocumentId);
        onDocumentOpened?.Invoke(ActiveDocumentId);
        AnyDocumentOpened?.Invoke(ActiveDocumentId, usingObscuredDocument);
        return true;
    }

    public void CloseDocument()
    {
        if (!IsOpen && !modalStateCaptured)
            return;

        string closedDocumentId = ActiveDocumentId;
        IsOpen = false;

        ResetRegionFocus();
        SetDocumentPanelVisible(false);

        if (clarityManager != null)
            clarityManager.ExitDocumentMode();

        RestoreModalState();
        DestroyRuntimeRevealMaterial();
        DestroyInstantiatedRegionLayout();

        ActiveDocumentId = string.Empty;
        OnDocumentClosed?.Invoke(closedDocumentId);
        onDocumentClosed?.Invoke(closedDocumentId);
        AnyDocumentClosed?.Invoke(closedDocumentId, usingObscuredDocument);
    }

    public bool IsRegionInvestigated(string regionId)
    {
        for (int i = 0; i < activeRegions.Count; i++)
        {
            ClarityDocumentRegion region = activeRegions[i];
            if (region != null &&
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            {
                return region.IsInvestigated;
            }
        }

        return false;
    }

    public bool AreRequiredRegionsComplete()
    {
        int requiredCount = 0;

        for (int i = 0; i < activeRegions.Count; i++)
        {
            ClarityDocumentRegion region = activeRegions[i];
            if (region == null || !region.Required)
                continue;

            requiredCount++;
            if (invalidRegions.Contains(region) || !region.IsInvestigated)
                return false;
        }

        return requiredCount > 0;
    }

    private void CacheReferences()
    {
        if (clarityManager == null)
            clarityManager = FindAnyObjectByType<ClarityManager>();

        if (clarityLensUI == null)
            clarityLensUI = FindAnyObjectByType<ClarityLensUI>();
    }

    private void ConfigureDocumentImages(Sprite sprite, Sprite obscuredSprite)
    {
        usingObscuredDocument = obscuredSprite != null;
        if (baseDocumentImage != null)
        {
            baseDocumentImage.sprite = sprite;
            baseDocumentImage.preserveAspect = true;
            baseDocumentImage.raycastTarget = false;
            baseDocumentImage.color = usingObscuredDocument
                ? clearDocumentTint
                : baseDocumentTint;
        }

        if (clearDocumentImage != null)
        {
            clearDocumentImage.sprite = usingObscuredDocument
                ? obscuredSprite
                : sprite;
            clearDocumentImage.preserveAspect = true;
            clearDocumentImage.raycastTarget = false;
            clearDocumentImage.color = Color.white;
        }

        if (documentAspectFitter != null && sprite.rect.height > 0f)
        {
            documentAspectFitter.aspectRatio =
                sprite.rect.width / sprite.rect.height;
        }
    }

    private void CreateRuntimeRevealMaterial()
    {
        DestroyRuntimeRevealMaterial();

        if (revealShader == null)
        {
            revealShader = Resources.Load<Shader>(
                "Clarity/ClarityDocumentReveal");
        }

        if (revealShader == null || clearDocumentImage == null)
            return;

        runtimeRevealMaterial = new Material(revealShader)
        {
            name = "Clarity Document Reveal (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        clearDocumentImage.material = runtimeRevealMaterial;
        runtimeRevealMaterial.SetFloat(RevealModeId, usingObscuredDocument ? 1f : 0f);
    }

    private void DestroyRuntimeRevealMaterial()
    {
        if (clearDocumentImage != null &&
            clearDocumentImage.material == runtimeRevealMaterial)
        {
            clearDocumentImage.material = null;
        }

        if (runtimeRevealMaterial != null)
            Destroy(runtimeRevealMaterial);

        runtimeRevealMaterial = null;
    }

    private void UpdateRevealMaterial()
    {
        if (runtimeRevealMaterial == null || clarityLensUI == null)
            return;

        bool reveal = clarityManager != null &&
                      clarityManager.IsDocumentMode &&
                      clarityManager.IsClarityActive;

        runtimeRevealMaterial.SetVector(
            LensCenterId,
            clarityLensUI.LensCenterScreenPosition);
        runtimeRevealMaterial.SetFloat(
            LensRadiusId,
            clarityLensUI.DetectionRadiusScreenPixels);
        runtimeRevealMaterial.SetFloat(
            EdgeSoftnessId,
            revealEdgeSoftnessPixels);
        runtimeRevealMaterial.SetFloat(RevealId, reveal ? 1f : 0f);
        runtimeRevealMaterial.SetFloat(RevealModeId, usingObscuredDocument ? 1f : 0f);
    }

    private void CreateRegionLayout(GameObject regionLayoutPrefab)
    {
        DestroyInstantiatedRegionLayout();

        if (regionLayoutPrefab == null || regionsRoot == null)
            return;

        instantiatedRegionLayout = Instantiate(
            regionLayoutPrefab,
            regionsRoot,
            false);
        instantiatedRegionLayout.name = regionLayoutPrefab.name;
    }

    private void DestroyInstantiatedRegionLayout()
    {
        if (instantiatedRegionLayout != null)
            Destroy(instantiatedRegionLayout);

        instantiatedRegionLayout = null;
    }

    private void CollectAndValidateRegions()
    {
        activeRegions.Clear();
        invalidRegions.Clear();

        Transform searchRoot = instantiatedRegionLayout != null
            ? instantiatedRegionLayout.transform
            : regionsRoot;

        if (searchRoot == null)
            return;

        activeRegions.AddRange(
            searchRoot.GetComponentsInChildren<ClarityDocumentRegion>(true));

        Dictionary<string, ClarityDocumentRegion> regionsById =
            new Dictionary<string, ClarityDocumentRegion>(StringComparer.Ordinal);
        HashSet<ClarityDocumentRegion> activeRegionSet =
            new HashSet<ClarityDocumentRegion>(activeRegions);

        for (int i = 0; i < activeRegions.Count; i++)
        {
            ClarityDocumentRegion region = activeRegions[i];
            region.BeginInvestigationLifecycle();

            if (string.IsNullOrWhiteSpace(region.RegionId))
            {
                invalidRegions.Add(region);
                Debug.LogWarning(
                    "A ClarityDocumentRegion has an empty region ID.",
                    region);
                continue;
            }

            if (regionsById.TryGetValue(
                region.RegionId,
                out ClarityDocumentRegion duplicate))
            {
                invalidRegions.Add(region);
                invalidRegions.Add(duplicate);
                Debug.LogWarning(
                    "Duplicate Clarity document region ID: " +
                    region.RegionId,
                    region);
            }
            else
            {
                regionsById.Add(region.RegionId, region);
            }

            ClarityDocumentRegion[] prerequisites = region.Prerequisites;
            for (int prerequisiteIndex = 0;
                 prerequisiteIndex < prerequisites.Length;
                 prerequisiteIndex++)
            {
                ClarityDocumentRegion prerequisite =
                    prerequisites[prerequisiteIndex];
                if (prerequisite == null ||
                    !activeRegionSet.Contains(prerequisite))
                {
                    invalidRegions.Add(region);
                    Debug.LogWarning(
                        "Clarity document region '" + region.RegionId +
                        "' has a missing or external prerequisite.",
                        region);
                }
            }
        }

        Dictionary<ClarityDocumentRegion, int> visitStates =
            new Dictionary<ClarityDocumentRegion, int>();
        for (int i = 0; i < activeRegions.Count; i++)
        {
            ClarityDocumentRegion region = activeRegions[i];
            if (HasPrerequisiteCycle(region, visitStates))
            {
                invalidRegions.Add(region);
                Debug.LogWarning(
                    "Clarity document prerequisite cycle affects region '" +
                    region.RegionId + "'.",
                    region);
            }
        }
    }

    private bool HasPrerequisiteCycle(
        ClarityDocumentRegion region,
        Dictionary<ClarityDocumentRegion, int> visitStates)
    {
        if (region == null)
            return false;

        if (visitStates.TryGetValue(region, out int state))
            return state == 1;

        visitStates[region] = 1;
        ClarityDocumentRegion[] prerequisites = region.Prerequisites;

        for (int i = 0; i < prerequisites.Length; i++)
        {
            if (HasPrerequisiteCycle(prerequisites[i], visitStates))
                return true;
        }

        visitStates[region] = 2;
        return false;
    }

    private void ProcessRegions()
    {
        bool clarityActive = clarityManager != null &&
                             clarityManager.IsDocumentMode &&
                             clarityManager.IsClarityActive;

        Camera uiCamera = GetUICamera();
        for (int i = 0; i < activeRegions.Count; i++)
        {
            ClarityDocumentRegion region = activeRegions[i];
            if (region == null || region.IsInvestigated)
                continue;

            bool eligible = clarityActive &&
                            !invalidRegions.Contains(region) &&
                            region.ArePrerequisitesSatisfied();
            if (!eligible)
            {
                region.ResetDwell();
                continue;
            }

            bool investigated = region.ProcessLens(
                clarityLensUI.LensCenterScreenPosition,
                clarityLensUI.DetectionRadiusScreenPixels,
                Time.unscaledDeltaTime,
                uiCamera);

            if (investigated)
                HandleRegionInvestigated(region);
        }
    }

    private Camera GetUICamera()
    {
        Canvas canvas = documentRect != null
            ? documentRect.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();

        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private void HandleRegionInvestigated(ClarityDocumentRegion region)
    {
        OnRegionInvestigated?.Invoke(region);
        onRegionInvestigated?.Invoke(region);

        if (completionRaised || !AreRequiredRegionsComplete())
            return;

        completionRaised = true;
        OnDocumentInvestigationCompleted?.Invoke(ActiveDocumentId);
        onDocumentInvestigationCompleted?.Invoke(ActiveDocumentId);
    }

    private void CaptureModalState()
    {
        if (modalStateCaptured)
            return;

        modalStateCaptured = true;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        previousBehaviourStates =
            new bool[gameplayBehavioursToDisable.Length];
        for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
            if (behaviour == null || behaviour == this)
                continue;

            previousBehaviourStates[i] = behaviour.enabled;
            behaviour.enabled = false;
        }
    }

    private void RestoreModalState()
    {
        if (!modalStateCaptured)
            return;

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        if (previousBehaviourStates != null)
        {
            int count = Mathf.Min(
                gameplayBehavioursToDisable.Length,
                previousBehaviourStates.Length);
            for (int i = 0; i < count; i++)
            {
                MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
                if (behaviour != null && behaviour != this)
                    behaviour.enabled = previousBehaviourStates[i];
            }
        }

        previousBehaviourStates = null;
        modalStateCaptured = false;
    }

    private void ResetRegionFocus()
    {
        for (int i = 0; i < activeRegions.Count; i++)
        {
            if (activeRegions[i] != null)
                activeRegions[i].ResetDwell();
        }
    }

    private void SetDocumentPanelVisible(bool visible)
    {
        if (documentPanel != null && documentPanel != gameObject)
            documentPanel.SetActive(visible);

        if (modalRaycastBlocker != null)
            modalRaycastBlocker.raycastTarget = visible;
    }
}
