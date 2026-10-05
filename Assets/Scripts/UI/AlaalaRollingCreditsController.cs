using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class CreditSection
{
    public string role;
    public List<string> names = new List<string>();
}

/// <summary>Reusable, continuously scrolling end-credit presentation.</summary>
public sealed class AlaalaRollingCreditsController : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private Canvas creditsCanvas;
    [SerializeField] private Image blackBackground;
    [SerializeField] private RectTransform creditsViewport;
    [SerializeField] private RectTransform creditsContent;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text returnButtonLabel;

    [Header("Opening")]
    [SerializeField] private string openingTitle = "ALAALA";
    [SerializeField] private string openingByline = "A Game by";
    [SerializeField] private List<string> openingNames = new List<string>
    {
        "Hyacinth John S Forones",
        "Maria Erica S Tabat",
        "Marinze M Mayan",
        "Jeremy A Naguit"
    };

    [Header("Credits")]
    [SerializeField] private List<CreditSection> sections = new List<CreditSection>
    {
        new CreditSection { role = "GAME DESIGNER", names = new List<string> { "Hyacinth John S Forones" } },
        new CreditSection { role = "LEAD DEVELOPER", names = new List<string> { "Hyacinth John S Forones" } },
        new CreditSection { role = "PROGRAMMING", names = new List<string> { "Hyacinth John S Forones", "Jeremy A Naguit" } },
        new CreditSection { role = "NARRATIVE & MANUSCRIPT DEVELOPMENT", names = new List<string> { "Hyacinth John S Forones", "Maria Erica S Tabat" } },
        new CreditSection { role = "HISTORICAL RESEARCH", names = new List<string> { "Maria Erica S Tabat", "Marinze M Mayan" } },
        new CreditSection { role = "3D ENVIRONMENT & LEVEL DESIGN", names = new List<string> { "Marinze M Mayan", "Jeremy A Naguit" } },
        new CreditSection { role = "USER INTERFACE DESIGN", names = new List<string> { "Hyacinth John S Forones", "Maria Erica S Tabat" } },
        new CreditSection { role = "CHARACTER & ASSET IMPLEMENTATION", names = new List<string> { "Marinze M Mayan", "Jeremy A Naguit" } },
        new CreditSection { role = "SOUND DESIGN", names = new List<string> { "Maria Erica S Tabat", "Jeremy A Naguit" } }
    };
    [SerializeField] private string endingTitle = "ALAALA";
    [SerializeField] private string endingSubtitle = "END OF BETA";
    [SerializeField] private TMP_FontAsset creditsFont;

    [Header("Typography")]
    [SerializeField, Min(1f)] private float titleFontSize = 64f;
    [SerializeField, Min(1f)] private float bylineFontSize = 30f;
    [SerializeField, Min(1f)] private float roleFontSize = 32f;
    [SerializeField, Min(1f)] private float nameFontSize = 27f;
    [SerializeField] private Color titleColor = new Color(0.87f, 0.78f, 0.58f, 1f);
    [SerializeField] private Color roleColor = Color.white;
    [SerializeField] private Color nameColor = new Color(0.9f, 0.88f, 0.83f, 1f);

    [Header("Scroll and spacing (Canvas UI units)")]
    [SerializeField, Min(1f)] private float scrollSpeed = 24f;
    [SerializeField, Min(0f)] private float initialBottomPadding = 80f;
    [SerializeField, Min(0f)] private float titleToBylineSpacing = 24f;
    [SerializeField, Min(0f)] private float bylineToNamesSpacing = 18f;
    [SerializeField, Min(0f)] private float openingNameSpacing = 8f;
    [SerializeField, Min(0f)] private float openingToSectionsSpacing = 130f;
    [SerializeField, Min(0f)] private float sectionSpacing = 72f;
    [SerializeField, Min(0f)] private float roleToNamesSpacing = 18f;
    [SerializeField, Min(0f)] private float nameLineSpacing = 6f;
    [SerializeField, Min(0f)] private float endingTopPadding = 190f;
    [SerializeField, Min(0f)] private float endingTitleToSubtitleSpacing = 24f;
    [SerializeField] private bool showReturnButtonOnComplete = true;
    [SerializeField] private string returnButtonText = "Bumalik sa Pangunahing Menu";
    [SerializeField] private UnityEvent onCreditsCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onReturnRequested = new UnityEvent();

    public event Action CreditsCompleted;
    public event Action ReturnRequested;
    public bool IsComplete { get; private set; }

    private Coroutine rollRoutine;
    private bool hasStarted;

    private void Awake()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(HandleReturnClicked);
        if (returnButtonLabel != null)
            returnButtonLabel.text = returnButtonText;
        if (returnButton != null)
            returnButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (returnButton != null)
            returnButton.onClick.RemoveListener(HandleReturnClicked);
    }

    public void BeginCredits()
    {
        if (hasStarted)
            return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        hasStarted = true;
        IsComplete = false;

        if (creditsCanvas != null)
            creditsCanvas.enabled = true;
        if (blackBackground != null)
            blackBackground.gameObject.SetActive(true);
        if (returnButton != null)
            returnButton.gameObject.SetActive(false);

        BuildCreditRoll();
        rollRoutine = StartCoroutine(RollCredits());
    }

    private void BuildCreditRoll()
    {
        if (creditsContent == null)
        {
            Debug.LogError("AlaalaRollingCreditsController needs a CreditsContent RectTransform.", this);
            return;
        }

        for (int i = creditsContent.childCount - 1; i >= 0; i--)
            Destroy(creditsContent.GetChild(i).gameObject);

        float cursor = 0f;
        AddLine(openingTitle, titleFontSize, titleColor, FontStyles.Bold, ref cursor);
        cursor += titleToBylineSpacing;
        AddLine(openingByline, bylineFontSize, nameColor, FontStyles.Italic, ref cursor);
        cursor += bylineToNamesSpacing;
        AddNameList(openingNames, ref cursor, openingNameSpacing);
        cursor += openingToSectionsSpacing;

        foreach (CreditSection section in sections)
        {
            if (section == null || string.IsNullOrWhiteSpace(section.role))
                continue;

            cursor += sectionSpacing;
            AddLine(section.role, roleFontSize, roleColor, FontStyles.Bold, ref cursor);
            cursor += roleToNamesSpacing;
            AddNameList(section.names, ref cursor, nameLineSpacing);
        }

        cursor += endingTopPadding;
        AddLine(endingTitle, titleFontSize, titleColor, FontStyles.Bold, ref cursor);
        cursor += endingTitleToSubtitleSpacing;
        AddLine(endingSubtitle, roleFontSize, roleColor, FontStyles.Bold, ref cursor);

        creditsContent.anchorMin = new Vector2(0f, 1f);
        creditsContent.anchorMax = new Vector2(1f, 1f);
        creditsContent.pivot = new Vector2(0.5f, 1f);
        creditsContent.sizeDelta = new Vector2(0f, cursor);
        creditsContent.anchoredPosition = new Vector2(0f,
            -creditsViewport.rect.height - initialBottomPadding);
    }

    private void AddNameList(IList<string> names, ref float cursor, float spacing)
    {
        if (names == null)
            return;

        for (int i = 0; i < names.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i]))
                continue;
            AddLine(names[i], nameFontSize, nameColor, FontStyles.Normal, ref cursor);
            if (i < names.Count - 1)
                cursor += spacing;
        }
    }

    private void AddLine(string value, float size, Color color, FontStyles style, ref float cursor)
    {
        GameObject lineObject = new GameObject("Credit Line", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lineObject.transform.SetParent(creditsContent, false);

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -cursor);
        rect.sizeDelta = new Vector2(-160f, size * 1.45f);

        TextMeshProUGUI text = lineObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = creditsFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        cursor += size * 1.45f;
    }

    private IEnumerator RollCredits()
    {
        if (creditsViewport == null || creditsContent == null)
        {
            Debug.LogError("AlaalaRollingCreditsController is missing its viewport/content references.", this);
            yield break;
        }

        while (creditsContent.anchoredPosition.y - creditsContent.rect.height <= 0f)
        {
            creditsContent.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        IsComplete = true;
        rollRoutine = null;
        if (showReturnButtonOnComplete && returnButton != null)
            returnButton.gameObject.SetActive(true);
        onCreditsCompleted?.Invoke();
        CreditsCompleted?.Invoke();
    }

    private void HandleReturnClicked()
    {
        if (!IsComplete)
            return;

        onReturnRequested?.Invoke();
        ReturnRequested?.Invoke();
    }
}
