using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryDocumentViewer : MonoBehaviour
{
    private const int SortingOrderAboveInventory = 302;
    private const string StoryPagePrefabResourcePath = "Inventory/CeciliaYsaganiStoryViewer";
    private static int openViewerCount;

    private int storyPageIndex;
    private int storyPageCount;
    private IReadOnlyList<Sprite> storyPages;

    private GameObject viewerRoot;
    private GameObject documentPanel;
    private GameObject storyPageLayoutInstance;
    private Canvas storyPageCanvas;
    private Image storyPageImage;
    private Button previousPageButton;
    private Button nextPageButton;
    private CanvasGroup previousPageCanvasGroup;
    private CanvasGroup nextPageCanvasGroup;
    private Image documentImage;
    private Button closeButton;
    private GameObject previousSelectedObject;
    private bool isOpen;

    public event Action<InventoryDocumentViewer, string> StoryPagesClosed;
    public static bool IsAnyOpen => openViewerCount > 0;
    public bool IsOpen => isOpen;
    public string OpenStoryItemID { get; private set; }

    private void Update()
    {
        if (!isOpen || storyPageLayoutInstance == null || !storyPageLayoutInstance.activeInHierarchy)
            return;

        UpdateArrowVisual(previousPageButton, previousPageCanvasGroup);
        UpdateArrowVisual(nextPageButton, nextPageCanvasGroup);
    }

    public void Configure(Transform inventoryRoot)
    {
        if (viewerRoot != null || inventoryRoot == null)
            return;

        viewerRoot = new GameObject(
            "InventoryDocumentCloseUp",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        viewerRoot.transform.SetParent(inventoryRoot, false);
        viewerRoot.layer = inventoryRoot.gameObject.layer;

        RectTransform rootRect = (RectTransform)viewerRoot.transform;
        StretchToParent(rootRect);

        Canvas canvas = viewerRoot.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrderAboveInventory;

        Canvas parentCanvas = inventoryRoot.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvas.renderMode = parentCanvas.rootCanvas.renderMode;
            canvas.worldCamera = parentCanvas.rootCanvas.worldCamera;
            canvas.planeDistance = parentCanvas.rootCanvas.planeDistance;
        }

        CanvasGroup canvasGroup = viewerRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        documentPanel = new GameObject("DimBackground", typeof(RectTransform), typeof(Image));
        documentPanel.transform.SetParent(viewerRoot.transform, false);
        documentPanel.layer = viewerRoot.layer;
        RectTransform panelRect = (RectTransform)documentPanel.transform;
        StretchToParent(panelRect);

        Image backgroundImage = documentPanel.GetComponent<Image>();
        backgroundImage.color = new Color(0.055f, 0.045f, 0.035f, 0.84f);
        backgroundImage.raycastTarget = true;

        GameObject documentBounds = new GameObject("DocumentBounds", typeof(RectTransform));
        documentBounds.transform.SetParent(viewerRoot.transform, false);
        documentBounds.layer = viewerRoot.layer;
        RectTransform boundsRect = (RectTransform)documentBounds.transform;
        boundsRect.anchorMin = new Vector2(0.05f, 0.05f);
        boundsRect.anchorMax = new Vector2(0.95f, 0.95f);
        boundsRect.offsetMin = Vector2.zero;
        boundsRect.offsetMax = Vector2.zero;

        GameObject imageObject = new GameObject(
            "DocumentImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(documentBounds.transform, false);
        imageObject.layer = viewerRoot.layer;
        RectTransform imageRect = (RectTransform)imageObject.transform;
        StretchToParent(imageRect);

        documentImage = imageObject.GetComponent<Image>();
        documentImage.preserveAspect = true;
        documentImage.raycastTarget = false;

        AspectRatioFitter aspectFitter = imageObject.GetComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        ConfigureStoryPageLayout();

        CreateCloseButton(viewerRoot.transform);
        viewerRoot.SetActive(false);
    }

    public bool Open(Sprite sprite)
    {
        if (sprite == null || viewerRoot == null || documentImage == null)
            return false;

        BeginOpenIfNeeded();
        ClearStoryPageSprites();
        documentImage.gameObject.SetActive(true);
        documentImage.sprite = sprite;
        documentImage.preserveAspect = true;
        SetAspectRatio(documentImage, sprite);

        viewerRoot.SetActive(true);
        SelectCloseButton();
        return true;
    }

    public bool OpenPages(IReadOnlyList<Sprite> sprites)
    {
        return OpenPages(sprites, string.Empty);
    }

    public bool OpenPages(IReadOnlyList<Sprite> sprites, string storyItemID)
    {
        if (sprites == null || sprites.Count == 0 ||
            viewerRoot == null || storyPageLayoutInstance == null || storyPageImage == null)
            return false;

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null)
                return false;
        }

        BeginOpenIfNeeded();
        documentImage.sprite = null;
        documentImage.gameObject.SetActive(false);
        storyPageCount = sprites.Count;
        storyPages = sprites;
        storyPageIndex = 0;
        OpenStoryItemID = storyItemID ?? string.Empty;
        storyPageImage.gameObject.SetActive(true);

        storyPageLayoutInstance.SetActive(true);
        viewerRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ShowCurrentStoryPage(sprites);
        SelectCloseButton();
        return true;
    }

    public void Close()
    {
        CloseInternal(true);
    }

    internal void CloseSilently()
    {
        CloseInternal(false);
    }

    private void CloseInternal(bool notifyStoryViewerClose)
    {
        if (!isOpen && (viewerRoot == null || !viewerRoot.activeSelf))
            return;

        bool wasOpen = isOpen;
        string closedStoryItemID = storyPageCount > 0 ? OpenStoryItemID : string.Empty;
        if (isOpen)
        {
            isOpen = false;
            openViewerCount = Mathf.Max(0, openViewerCount - 1);
        }

        if (documentImage != null)
            documentImage.sprite = null;
        ClearStoryPageSprites();
        if (viewerRoot != null)
            viewerRoot.SetActive(false);

        if (EventSystem.current != null)
        {
            GameObject selectionToRestore = previousSelectedObject;
            Selectable selectable = selectionToRestore != null
                ? selectionToRestore.GetComponent<Selectable>()
                : null;
            if (selectionToRestore != null &&
                (!selectionToRestore.activeInHierarchy ||
                 (selectable != null && !selectable.IsInteractable())))
            {
                selectionToRestore = null;
            }

            EventSystem.current.SetSelectedGameObject(selectionToRestore);
        }
        previousSelectedObject = null;

        if (notifyStoryViewerClose && wasOpen && !string.IsNullOrWhiteSpace(closedStoryItemID))
            StoryPagesClosed?.Invoke(this, closedStoryItemID);
    }

    public static bool TryCloseTopmost()
    {
        if (!IsAnyOpen)
            return false;

        InventoryDocumentViewer[] viewers = FindObjectsByType<InventoryDocumentViewer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = viewers.Length - 1; i >= 0; i--)
        {
            if (viewers[i] == null || !viewers[i].IsOpen)
                continue;

            viewers[i].Close();
            return true;
        }

        openViewerCount = 0;
        return false;
    }

    private void BeginOpenIfNeeded()
    {
        if (isOpen)
            return;

        isOpen = true;
        openViewerCount++;
        previousSelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
    }

    private void SelectCloseButton()
    {
        if (EventSystem.current != null && closeButton != null)
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void ConfigureStoryPageLayout()
    {
        GameObject prefab = Resources.Load<GameObject>(StoryPagePrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                "Inventory story-page viewer prefab was not found at Resources/" +
                StoryPagePrefabResourcePath + ".prefab",
                this);
            return;
        }

        storyPageLayoutInstance = Instantiate(prefab, viewerRoot.transform, false);
        storyPageLayoutInstance.name = prefab.name;
        storyPageLayoutInstance.SetActive(false);

        // Keep the shared prefab's Canvas for Prefab Mode editing, while making
        // its runtime canvas match this modal in every Inventory scene.
        Canvas modalCanvas = viewerRoot.GetComponent<Canvas>();
        storyPageCanvas = storyPageLayoutInstance.GetComponent<Canvas>();
        if (modalCanvas != null && storyPageCanvas != null)
        {
            storyPageCanvas.renderMode = modalCanvas.renderMode;
            storyPageCanvas.worldCamera = modalCanvas.worldCamera;
            storyPageCanvas.planeDistance = modalCanvas.planeDistance;
            storyPageCanvas.overrideSorting = true;
            storyPageCanvas.sortingLayerID = modalCanvas.sortingLayerID;
            storyPageCanvas.sortingOrder = modalCanvas.sortingOrder;
        }

        Transform pageTransform = storyPageLayoutInstance.transform.Find("StoryPage");
        Transform imageTransform = pageTransform != null ? pageTransform.Find("PageArtwork") : null;
        storyPageImage = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        previousPageButton = FindArrow("PreviousArrow", out previousPageCanvasGroup);
        nextPageButton = FindArrow("NextArrow", out nextPageCanvasGroup);

        if (pageTransform == null || storyPageImage == null ||
            previousPageButton == null || nextPageButton == null ||
            previousPageCanvasGroup == null || nextPageCanvasGroup == null)
        {
            Debug.LogError(
                "CeciliaYsaganiStoryViewer needs StoryPage/PageArtwork and both navigation arrows.",
                this);
            storyPageImage = null;
            previousPageButton = null;
            nextPageButton = null;
            previousPageCanvasGroup = null;
            nextPageCanvasGroup = null;
            return;
        }

        storyPageImage.preserveAspect = true;
        storyPageImage.raycastTarget = false;
        previousPageButton.onClick.AddListener(ShowPreviousStoryPage);
        nextPageButton.onClick.AddListener(ShowNextStoryPage);
        RefreshStoryPageNavigation();
    }

    private Button FindArrow(string objectName, out CanvasGroup canvasGroup)
    {
        Transform arrowTransform = storyPageLayoutInstance.transform.Find(objectName);
        Button button = arrowTransform != null ? arrowTransform.GetComponent<Button>() : null;
        canvasGroup = arrowTransform != null ? arrowTransform.GetComponent<CanvasGroup>() : null;
        return button;
    }

    private void ShowPreviousStoryPage()
    {
        if (storyPageIndex <= 0 || storyPageCount <= 1)
            return;

        storyPageIndex--;
        ShowCurrentStoryPage(storyPages);
    }

    private void ShowNextStoryPage()
    {
        if (storyPageIndex >= storyPageCount - 1)
            return;

        storyPageIndex++;
        ShowCurrentStoryPage(storyPages);
    }

    private void ShowCurrentStoryPage(IReadOnlyList<Sprite> sprites)
    {
        if (sprites == null || storyPageIndex < 0 || storyPageIndex >= sprites.Count)
            return;

        storyPageImage.sprite = sprites[storyPageIndex];
        storyPageImage.preserveAspect = true;
        SetAspectRatio(storyPageImage, sprites[storyPageIndex]);
        RefreshStoryPageNavigation();
    }

    private void RefreshStoryPageNavigation()
    {
        if (previousPageButton != null)
        {
            bool hasPrevious = storyPageIndex > 0;
            previousPageButton.gameObject.SetActive(hasPrevious);
            previousPageButton.interactable = hasPrevious;
        }

        if (nextPageButton != null)
        {
            bool hasNext = storyPageIndex < storyPageCount - 1;
            nextPageButton.gameObject.SetActive(hasNext);
            nextPageButton.interactable = hasNext;
        }
    }

    private void UpdateArrowVisual(Button button, CanvasGroup canvasGroup)
    {
        if (button == null || canvasGroup == null || !button.gameObject.activeInHierarchy)
            return;

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
            return;

        Camera eventCamera = storyPageCanvas != null &&
                             storyPageCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? storyPageCanvas.worldCamera
            : null;
        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            Input.mousePosition,
            eventCamera);

        if (hovered)
        {
            canvasGroup.alpha = 1f;
            rect.localScale = Vector3.one * 1.1f;
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
        canvasGroup.alpha = Mathf.Lerp(0.58f, 0.82f, pulse);
        rect.localScale = Vector3.one * (1f + 0.018f * pulse);
    }

    private void ClearStoryPageSprites()
    {
        storyPageIndex = 0;
        storyPageCount = 0;
        storyPages = null;
        OpenStoryItemID = string.Empty;
        if (storyPageImage != null)
            storyPageImage.sprite = null;
        if (previousPageButton != null)
            previousPageButton.gameObject.SetActive(false);
        if (nextPageButton != null)
            nextPageButton.gameObject.SetActive(false);
        if (storyPageLayoutInstance != null)
            storyPageLayoutInstance.SetActive(false);
    }

    private static void SetAspectRatio(Image image, Sprite sprite)
    {
        Rect spriteRect = sprite.rect;
        if (spriteRect.height <= 0f)
            return;

        image.GetComponent<AspectRatioFitter>().aspectRatio = spriteRect.width / spriteRect.height;
    }

    private void OnDisable()
    {
        CloseSilently();
    }

    private void OnDestroy()
    {
        CloseSilently();
        if (viewerRoot != null)
            Destroy(viewerRoot);
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "CloseButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.layer = viewerRoot.layer;

        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.anchoredPosition = new Vector2(-28f, -28f);
        buttonRect.sizeDelta = new Vector2(64f, 64f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.19f, 0.13f, 0.96f);
        buttonImage.raycastTarget = true;

        closeButton = buttonObject.GetComponent<Button>();
        closeButton.targetGraphic = buttonImage;
        closeButton.transition = Selectable.Transition.ColorTint;
        closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = closeButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        closeButton.colors = colors;
        closeButton.onClick.AddListener(Close);

        GameObject labelObject = new GameObject(
            "X",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        labelObject.layer = viewerRoot.layer;
        RectTransform labelRect = (RectTransform)labelObject.transform;
        StretchToParent(labelRect);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "X";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

}
