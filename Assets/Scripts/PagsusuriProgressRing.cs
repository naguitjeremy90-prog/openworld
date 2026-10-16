using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One shared lens-adjacent progress ring that scans whichever highlighted target is focused.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class PagsusuriProgressRing : MonoBehaviour
{
    [Header("Existing Pagsusuri")]
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField] private ClarityLensUI clarityLensUI;

    [Header("Ring")]
    [SerializeField] private GameObject progressRingPrefab;
    [SerializeField, Min(16f)] private float ringSize = 54f;
    [SerializeField] private Vector2 ringOffset;
    [SerializeField, Min(0f)] private float completedHoldDuration = 0.12f;

    private RectTransform canvasRect;
    private Image progressImage;
    private GameObject ringObject;
    private PagsusuriScanTarget activeTarget;
    private InventoryUI inventory;
    private ReconstructionJournalManager journal;
    private AlaalaPauseMenuController pauseMenu;
    private ClarityDocumentViewer documentViewer;
    private float completedHoldUntil;

    private void Awake()
    {
        if (clarityManager == null)
            clarityManager = FindAnyObjectByType<ClarityManager>();
        if (clarityLensUI == null)
            clarityLensUI = FindAnyObjectByType<ClarityLensUI>();
        inventory = FindAnyObjectByType<InventoryUI>();
        journal = ReconstructionJournalManager.Instance;
        pauseMenu = FindAnyObjectByType<AlaalaPauseMenuController>();
        documentViewer = FindAnyObjectByType<ClarityDocumentViewer>();

        canvasRect = transform as RectTransform;
        if (progressRingPrefab != null)
        {
            ringObject = Instantiate(progressRingPrefab, transform);
            ringObject.name = "Pagsusuri Progress Ring";
            progressImage = ringObject.GetComponent<Image>();
            RectTransform ringRect = ringObject.transform as RectTransform;
            if (ringRect != null)
                ringRect.sizeDelta = Vector2.one * ringSize;
            if (progressImage != null)
            {
                progressImage.fillAmount = 0f;
                progressImage.raycastTarget = false;
            }

            ringObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < completedHoldUntil)
        {
            SetRingVisible(true);
            if (progressImage != null)
                progressImage.fillAmount = 1f;
            PositionRing();
            return;
        }

        if (!CanScan())
        {
            ClearActiveTarget();
            return;
        }

        PagsusuriScanTarget candidate = FindHighlightedTarget();
        if (candidate != activeTarget)
        {
            ClearActiveTarget();
            activeTarget = candidate;
        }

        if (activeTarget == null)
        {
            HideRing();
            return;
        }

        if (activeTarget.AdvanceScan(Time.deltaTime))
        {
            if (progressImage != null)
                progressImage.fillAmount = 1f;
            PositionRing();
            completedHoldUntil = Time.unscaledTime + completedHoldDuration;
            activeTarget = null;
            return;
        }

        if (progressImage != null)
            progressImage.fillAmount = activeTarget.Progress;
        PositionRing();
        SetRingVisible(true);
    }

    private bool CanScan()
    {
        if (clarityManager == null ||
            !GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity) ||
            !clarityManager.IsClarityActive ||
            clarityManager.IsDocumentMode ||
            Time.timeScale <= 0f ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            IsModalUIOpen())
        {
            return false;
        }

        ConversationManager conversation = ConversationManager.Instance;
        return conversation == null || !conversation.IsConversationActive;
    }

    private bool IsModalUIOpen()
    {
        if (inventory == null)
            inventory = FindAnyObjectByType<InventoryUI>();
        if (journal == null)
            journal = ReconstructionJournalManager.Instance;
        if (pauseMenu == null)
            pauseMenu = FindAnyObjectByType<AlaalaPauseMenuController>();
        if (documentViewer == null)
            documentViewer = FindAnyObjectByType<ClarityDocumentViewer>();

        return (inventory != null && inventory.IsOpen) ||
               (journal != null && journal.IsOpen) ||
               (pauseMenu != null && pauseMenu.IsOpen) ||
               (documentViewer != null && documentViewer.IsOpen);
    }

    private PagsusuriScanTarget FindHighlightedTarget()
    {
        IReadOnlyList<PagsusuriScanTarget> targets = PagsusuriScanTarget.ActiveTargets;
        PagsusuriScanTarget closest = null;
        float closestDistance = float.PositiveInfinity;
        Camera camera = Camera.main;

        for (int i = 0; i < targets.Count; i++)
        {
            PagsusuriScanTarget candidate = targets[i];
            if (candidate == null || !candidate.isActiveAndEnabled ||
                candidate.IsAvailable() || candidate.Target == null ||
                !candidate.Target.IsCurrentlyHighlighted)
            {
                continue;
            }

            float distance = GetLensDistance(candidate.Target, camera);
            if (closest == null || distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private float GetLensDistance(ClarityTarget target, Camera camera)
    {
        if (target == null || clarityLensUI == null || camera == null ||
            !target.TryGetScreenRect(camera, out Rect targetRect))
        {
            return float.PositiveInfinity;
        }

        Vector2 lens = clarityLensUI.LensCenterScreenPosition;
        Vector2 nearest = new Vector2(
            Mathf.Clamp(lens.x, targetRect.xMin, targetRect.xMax),
            Mathf.Clamp(lens.y, targetRect.yMin, targetRect.yMax));
        return Vector2.Distance(lens, nearest);
    }

    private void ClearActiveTarget()
    {
        if (activeTarget != null)
            activeTarget.ResetIncompleteProgress();
        activeTarget = null;
        HideRing();
    }

    private void PositionRing()
    {
        if (ringObject == null || canvasRect == null || clarityLensUI == null)
            return;

        Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                clarityLensUI.LensCenterScreenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            RectTransform ringRect = ringObject.transform as RectTransform;
            if (ringRect != null)
                ringRect.anchoredPosition = localPoint + ringOffset;
        }
    }

    private void SetRingVisible(bool visible)
    {
        if (ringObject != null && ringObject.activeSelf != visible)
            ringObject.SetActive(visible);
    }

    private void HideRing()
    {
        if (progressImage != null)
            progressImage.fillAmount = 0f;
        SetRingVisible(false);
    }

    private void OnDisable()
    {
        ClearActiveTarget();
    }
}
