using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ClarityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text clarityText;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Visibility")]
    [SerializeField, Range(0f, 1f)] private float normalAlpha = 0.6f;
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField, Min(0f)] private float transitionSpeed = 8f;

    [Header("Scale")]
    [SerializeField, Min(0f)] private float normalScale = 1f;
    [SerializeField, Min(0f)] private float activeScale = 1.05f;

    [Header("Default HUD")]
    [SerializeField] private bool createDefaultHudIfUnassigned = true;
    [SerializeField, Min(0)] private int sortingOrder = 110;

    private Canvas createdCanvas;
    private RectTransform hudTransform;
    private float currentAlpha;
    private float currentScale;

    private void Awake()
    {
        if (clarityManager == null)
            clarityManager = FindFirstObjectByType<ClarityManager>();

        if (createDefaultHudIfUnassigned &&
            (canvasGroup == null || clarityText == null || keyText == null))
        {
            CreateDefaultHud();
        }

        currentAlpha = normalAlpha;
        currentScale = normalScale;

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;

        if (hudTransform != null)
            hudTransform.localScale = Vector3.one * currentScale;
    }

    private void Update()
    {
        if (clarityManager == null)
            clarityManager = FindFirstObjectByType<ClarityManager>();

        bool isActive = clarityManager != null &&
                        clarityManager.IsClarityActive;
        float targetAlpha = isActive ? activeAlpha : normalAlpha;
        float targetScale = isActive ? activeScale : normalScale;
        float step = transitionSpeed * Time.deltaTime;

        currentAlpha = transitionSpeed <= 0f
            ? targetAlpha
            : Mathf.MoveTowards(currentAlpha, targetAlpha, step);
        currentScale = transitionSpeed <= 0f
            ? targetScale
            : Mathf.MoveTowards(currentScale, targetScale, step);

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;

        if (hudTransform != null)
            hudTransform.localScale = Vector3.one * currentScale;
    }

    private void CreateDefaultHud()
    {
        createdCanvas = new GameObject(
            "ClarityHUDCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)).GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = createdCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject hudObject = new GameObject(
            "ClarityUI",
            typeof(RectTransform),
            typeof(CanvasGroup));
        hudObject.transform.SetParent(createdCanvas.transform, false);
        hudTransform = hudObject.GetComponent<RectTransform>();
        hudTransform.anchorMin = new Vector2(1f, 0f);
        hudTransform.anchorMax = new Vector2(1f, 0f);
        hudTransform.pivot = new Vector2(1f, 0f);
        hudTransform.anchoredPosition = new Vector2(-24f, 24f);
        hudTransform.sizeDelta = new Vector2(240f, 36f);

        canvasGroup = hudObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.ignoreParentGroups = true;

        clarityText = CreateText(
            hudObject.transform,
            "ClarityText",
            "CLARITY");
        keyText = CreateText(
            hudObject.transform,
            "KeyText",
            "[C]");

        RectTransform clarityRect = clarityText.rectTransform;
        clarityRect.anchorMin = new Vector2(0f, 0f);
        clarityRect.anchorMax = new Vector2(0.68f, 1f);
        clarityRect.offsetMin = Vector2.zero;
        clarityRect.offsetMax = Vector2.zero;
        clarityText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform keyRect = keyText.rectTransform;
        keyRect.anchorMin = new Vector2(0.68f, 0f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.offsetMin = Vector2.zero;
        keyRect.offsetMax = Vector2.zero;
        keyText.alignment = TextAlignmentOptions.MidlineRight;
    }

    private TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Normal;
        text.color = new Color(1f, 0.9f, 0.72f, 1f);
        text.raycastTarget = false;
        text.maskable = true;

        if (fontAsset != null)
            text.font = fontAsset;

        return text;
    }

    private void OnDestroy()
    {
        if (createdCanvas != null)
            Destroy(createdCanvas.gameObject);
    }
}
