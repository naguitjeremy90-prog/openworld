using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReconstructionReflections : MonoBehaviour
{
    [Header("Available Reflections")]
    [SerializeField] private List<ReflectionData> allReflections =
        new List<ReflectionData>();

    [Header("Reflection List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private Button listButtonPrefab;
    [SerializeField] private TMP_Text emptyListText;

    [Header("Reflection Details")]
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailContextText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text suggestionsLabel;
    [SerializeField] private Transform suggestionsContent;
    [SerializeField] private Button suggestionButtonPrefab;
    [SerializeField] private TMP_Text suggestionsSeparatorText;
    [SerializeField] private TMP_Text ownResponseLabel;
    [SerializeField] private TMP_InputField responseInputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text validationText;
    [SerializeField] private TMP_Text completedLabelText;
    [SerializeField] private TMP_Text completedResponseText;

    [Header("Temporary Testing")]
    [SerializeField] private Button testUnlockButton;
    [SerializeField] private string testReflectionID = "test_reflection";

    private readonly HashSet<string> unlockedReflectionIDs = new HashSet<string>();
    private readonly Dictionary<string, string> submittedResponses =
        new Dictionary<string, string>();
    private readonly List<GameObject> spawnedListButtons = new List<GameObject>();
    private readonly List<GameObject> spawnedSuggestionButtons =
        new List<GameObject>();

    private ReflectionData selectedReflection;
    private GameObject suggestionPanel;
    private Coroutine pendingSuggestionHide;
    private bool suppressNextSuggestionOpen;

    public int UnlockedReflectionCount
    {
        get { return unlockedReflectionIDs.Count; }
    }

    private void Awake()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.AddListener(UnlockTestReflection);

        if (submitButton != null)
            submitButton.onClick.AddListener(SubmitSelectedReflectionFromButton);

        if (responseInputField != null)
        {
            responseInputField.onSelect.AddListener(HandleResponseInputSelected);
            responseInputField.onDeselect.AddListener(HandleResponseInputDeselected);
        }

        ResolveSuggestionPanel();
        HideSuggestionPanel();

        RefreshList();
    }

    private void OnDestroy()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.RemoveListener(UnlockTestReflection);

        if (submitButton != null)
            submitButton.onClick.RemoveListener(SubmitSelectedReflectionFromButton);

        if (responseInputField != null)
        {
            responseInputField.onSelect.RemoveListener(HandleResponseInputSelected);
            responseInputField.onDeselect.RemoveListener(HandleResponseInputDeselected);
        }
    }

    public bool UnlockReflection(string reflectionID)
    {
        ReflectionData reflection = FindReflection(reflectionID);

        if (reflection == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Reflection ID '" +
                reflectionID +
                "' was not found.");
            return false;
        }

        if (!unlockedReflectionIDs.Add(reflectionID))
            return false;

        RefreshList();
        Debug.Log(
            "Reconstruction Journal: Unlocked reflection '" + reflectionID + "'.");
        return true;
    }

    public bool SubmitSelectedReflection()
    {
        if (selectedReflection == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Select a Reflection before submitting.");
            return false;
        }

        if (submittedResponses.ContainsKey(selectedReflection.reflectionID))
            return false;

        string response = responseInputField != null ? responseInputField.text : "";

        if (string.IsNullOrWhiteSpace(response))
        {
            if (validationText != null)
            {
                validationText.text = "Magsulat muna bago magnilay.";
                validationText.gameObject.SetActive(true);
            }

            Debug.LogWarning(
                "Reconstruction Journal: A Reflection response cannot be empty.");
            return false;
        }

        submittedResponses[selectedReflection.reflectionID] = response;
        ShowReflection(selectedReflection);

        Debug.Log(
            "Reconstruction Journal: Completed reflection '" +
            selectedReflection.reflectionID +
            "'.");
        return true;
    }

    private void SubmitSelectedReflectionFromButton()
    {
        SubmitSelectedReflection();
    }

    public ReflectionStatus GetReflectionStatus(string reflectionID)
    {
        if (!unlockedReflectionIDs.Contains(reflectionID))
            return ReflectionStatus.Locked;

        if (submittedResponses.ContainsKey(reflectionID))
            return ReflectionStatus.Completed;

        return ReflectionStatus.Unanswered;
    }

    public string GetSubmittedResponse(string reflectionID)
    {
        string response;
        return submittedResponses.TryGetValue(reflectionID, out response)
            ? response
            : "";
    }

    public void UnlockTestReflection()
    {
        UnlockReflection(testReflectionID);
    }

    public void RefreshList()
    {
        ClearSpawnedButtons();

        foreach (ReflectionData reflection in allReflections)
        {
            if (reflection == null ||
                !unlockedReflectionIDs.Contains(reflection.reflectionID))
            {
                continue;
            }

            CreateListButton(reflection);
        }

        if (emptyListText != null)
            emptyListText.gameObject.SetActive(spawnedListButtons.Count == 0);

        if (selectedReflection == null ||
            !unlockedReflectionIDs.Contains(selectedReflection.reflectionID))
        {
            ClearDetails();
        }
        else
        {
            string currentDraft =
                responseInputField != null ? responseInputField.text : "";
            ShowReflection(selectedReflection);

            if (responseInputField != null &&
                GetReflectionStatus(selectedReflection.reflectionID) ==
                ReflectionStatus.Unanswered)
            {
                responseInputField.text = currentDraft;
            }
        }
    }

    private ReflectionData FindReflection(string reflectionID)
    {
        if (string.IsNullOrWhiteSpace(reflectionID))
            return null;

        return allReflections.Find(
            reflection =>
                reflection != null &&
                reflection.reflectionID == reflectionID);
    }

    private void CreateListButton(ReflectionData reflection)
    {
        if (listContent == null || listButtonPrefab == null)
            return;

        Button newButton = Instantiate(listButtonPrefab, listContent);
        newButton.gameObject.SetActive(true);
        newButton.name = "Reflection_" + reflection.reflectionID;

        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = reflection.title;

        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(() => ShowReflection(reflection));
        spawnedListButtons.Add(newButton.gameObject);
    }

    private void ShowReflection(ReflectionData reflection)
    {
        selectedReflection = reflection;

        if (detailTitleText != null)
            detailTitleText.text = reflection.title;

        if (detailContextText != null)
            detailContextText.text = reflection.GetContextLabel();

        if (promptText != null)
            promptText.text = reflection.prompt;

        if (validationText != null)
            validationText.gameObject.SetActive(false);

        string submittedResponse;
        bool isCompleted = submittedResponses.TryGetValue(
            reflection.reflectionID,
            out submittedResponse);

        bool hasSuggestions =
            !isCompleted &&
            reflection.suggestedResponses != null &&
            reflection.suggestedResponses.Count > 0;

        ClearSuggestionButtons();

        HideSuggestionPanel();

        if (suggestionsLabel != null)
            suggestionsLabel.gameObject.SetActive(false);

        if (suggestionsSeparatorText != null)
            suggestionsSeparatorText.gameObject.SetActive(false);

        if (ownResponseLabel != null)
            ownResponseLabel.gameObject.SetActive(false);

        if (hasSuggestions)
        {
            foreach (string suggestion in reflection.suggestedResponses)
            {
                if (!string.IsNullOrWhiteSpace(suggestion))
                    CreateSuggestionButton(suggestion);
            }
        }

        if (responseInputField != null)
        {
            responseInputField.gameObject.SetActive(!isCompleted);

            if (!isCompleted)
                responseInputField.text = "";
        }

        if (submitButton != null)
            submitButton.gameObject.SetActive(!isCompleted);

        if (completedLabelText != null)
            completedLabelText.gameObject.SetActive(isCompleted);

        if (completedResponseText != null)
        {
            completedResponseText.gameObject.SetActive(isCompleted);

            if (isCompleted)
                completedResponseText.text = submittedResponse;
        }
    }

    private void ClearDetails()
    {
        selectedReflection = null;

        if (detailTitleText != null)
            detailTitleText.text = "Walang Napiling Pagninilay";

        if (detailContextText != null)
            detailContextText.text = "";

        if (promptText != null)
            promptText.text =
                "Magbukas at pumili ng pagninilay upang pag-isipan ang mga " +
                "karanasan ni Peter.";

        ClearSuggestionButtons();
        HideSuggestionPanel();

        if (suggestionsLabel != null)
            suggestionsLabel.gameObject.SetActive(false);

        if (suggestionsSeparatorText != null)
            suggestionsSeparatorText.gameObject.SetActive(false);

        if (ownResponseLabel != null)
            ownResponseLabel.gameObject.SetActive(false);

        if (responseInputField != null)
        {
            responseInputField.text = "";
            responseInputField.gameObject.SetActive(false);
        }

        if (submitButton != null)
            submitButton.gameObject.SetActive(false);

        if (validationText != null)
            validationText.gameObject.SetActive(false);

        if (completedLabelText != null)
            completedLabelText.gameObject.SetActive(false);

        if (completedResponseText != null)
            completedResponseText.gameObject.SetActive(false);
    }

    private void ClearSpawnedButtons()
    {
        foreach (GameObject buttonObject in spawnedListButtons)
        {
            if (buttonObject != null)
            {
                buttonObject.SetActive(false);
                Destroy(buttonObject);
            }
        }

        spawnedListButtons.Clear();
    }

    private void CreateSuggestionButton(string suggestion)
    {
        if (suggestionsContent == null || suggestionButtonPrefab == null)
            return;

        Button newButton = Instantiate(suggestionButtonPrefab, suggestionsContent);
        newButton.gameObject.SetActive(true);
        newButton.name = "Suggestion";

        Image buttonBackground = newButton.GetComponent<Image>();
        if (buttonBackground != null)
        {
            buttonBackground.sprite = null;
            buttonBackground.color = Color.white;
        }

        ColorBlock thoughtColors = newButton.colors;
        thoughtColors.normalColor = new Color(1f, 0.98f, 0.91f, 0.03f);
        thoughtColors.highlightedColor = new Color(0.91f, 0.79f, 0.57f, 0.16f);
        thoughtColors.pressedColor = new Color(0.84f, 0.69f, 0.45f, 0.24f);
        thoughtColors.selectedColor = new Color(0.91f, 0.79f, 0.57f, 0.1f);
        newButton.colors = thoughtColors;

        LayoutElement layoutElement = newButton.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = suggestion.Length > 60
                ? 46f
                : suggestion.Length > 35
                    ? 40f
                    : 34f;
        }

        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = suggestion;
            buttonText.fontSize = 20f;
            buttonText.fontStyle = FontStyles.Italic;
        }

        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(() => SelectSuggestion(suggestion));
        spawnedSuggestionButtons.Add(newButton.gameObject);
    }

    private void SelectSuggestion(string suggestion)
    {
        if (responseInputField == null)
            return;

        responseInputField.text = suggestion;
        HideSuggestionPanel();

        if (validationText != null)
            validationText.gameObject.SetActive(false);

        StartCoroutine(FocusResponseInputAfterSuggestion());
    }

    private void ClearSuggestionButtons()
    {
        foreach (GameObject buttonObject in spawnedSuggestionButtons)
        {
            if (buttonObject != null)
            {
                buttonObject.SetActive(false);
                Destroy(buttonObject);
            }
        }

        spawnedSuggestionButtons.Clear();
    }

    private void ResolveSuggestionPanel()
    {
        if (suggestionPanel != null || suggestionsContent == null)
            return;

        Transform viewport = suggestionsContent.parent;
        suggestionPanel = viewport != null && viewport.parent != null
            ? viewport.parent.gameObject
            : suggestionsContent.gameObject;
    }

    private void HandleResponseInputSelected(string unused)
    {
        if (suppressNextSuggestionOpen)
        {
            suppressNextSuggestionOpen = false;
            return;
        }

        ShowSuggestionPanel();
    }

    private void HandleResponseInputDeselected(string unused)
    {
        if (pendingSuggestionHide != null)
            StopCoroutine(pendingSuggestionHide);

        pendingSuggestionHide = StartCoroutine(HideSuggestionPanelAfterFocusChange());
    }

    private IEnumerator HideSuggestionPanelAfterFocusChange()
    {
        yield return null;
        pendingSuggestionHide = null;

        if (!IsPointerOrSelectionInsideSuggestionArea())
            HideSuggestionPanel();
    }

    private IEnumerator FocusResponseInputAfterSuggestion()
    {
        yield return null;

        if (responseInputField == null || !responseInputField.isActiveAndEnabled)
            yield break;

        suppressNextSuggestionOpen = true;
        responseInputField.Select();
        responseInputField.ActivateInputField();
        responseInputField.caretPosition = responseInputField.text.Length;
        responseInputField.MoveTextEnd(false);
    }

    private void ShowSuggestionPanel()
    {
        ResolveSuggestionPanel();

        if (suggestionPanel == null || selectedReflection == null)
            return;

        if (GetReflectionStatus(selectedReflection.reflectionID) !=
            ReflectionStatus.Unanswered)
        {
            return;
        }

        if (spawnedSuggestionButtons.Count == 0)
            return;

        suggestionPanel.SetActive(true);

        RectTransform contentRect = suggestionsContent as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void HideSuggestionPanel()
    {
        ResolveSuggestionPanel();

        if (suggestionPanel != null)
            suggestionPanel.SetActive(false);
    }

    private bool IsPointerOrSelectionInsideSuggestionArea()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
        {
            Transform selected = eventSystem.currentSelectedGameObject.transform;

            if (responseInputField != null &&
                (selected == responseInputField.transform ||
                 selected.IsChildOf(responseInputField.transform)))
            {
                return true;
            }

            if (suggestionPanel != null &&
                (selected == suggestionPanel.transform ||
                 selected.IsChildOf(suggestionPanel.transform)))
            {
                return true;
            }
        }

        if (eventSystem == null || !Input.mousePresent)
            return false;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            Transform hit = result.gameObject.transform;

            if (responseInputField != null &&
                (hit == responseInputField.transform ||
                 hit.IsChildOf(responseInputField.transform)))
            {
                return true;
            }

            if (suggestionPanel != null &&
                (hit == suggestionPanel.transform ||
                 hit.IsChildOf(suggestionPanel.transform)))
            {
                return true;
            }
        }

        return false;
    }
}
