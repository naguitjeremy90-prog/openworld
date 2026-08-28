using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReconstructionReflections : MonoBehaviour
{
    // Keep this list deliberately small and easy to edit. Matching is done
    // against whole words/tokens, so innocent words containing these letters
    // are not rejected.
    private static readonly string[] BlockedWords =
    {
        "fuck", "shit", "bitch", "asshole", "bastard", "cunt",
        "nigger", "faggot", "slut", "whore", "dick", "cock", "pussy",
        "putangina", "putang ina", "gago", "tanga", "ulol", "tarantado",
        "kantot", "iyot", "jakol"
    };

    private static readonly string[] KeyboardSmashPatterns =
    {
        "asdfghjkl", "qwertyuiop", "zxcvbnm", "asdfasdfasdf",
        "qwertyqwerty", "zxcvzxcv", "hjklhjkl"
    };

    // Developer-editable theme keywords. Matching is token-based, so these
    // entries do not accidentally match inside unrelated words.
    private static readonly string[] FearKeywords =
    {
        "takot", "natatakot", "natakot", "pangamba", "nangangamba",
        "fear", "afraid", "scared"
    };

    private static readonly string[] BlameOrJudgmentKeywords =
    {
        "sisi", "sinisi", "sisihin", "kasalanan", "husga", "hinusgahan",
        "blame", "blamed", "fault", "judge", "judged"
    };

    private static readonly string[] AuthorityKeywords =
    {
        "awtoridad", "kapangyarihan", "pari", "padre", "authority", "power"
    };

    private static readonly string[] ConformityOrPressureKeywords =
    {
        "napilitan", "pinilit", "sumusunod", "sumunod", "pressure", "pressured",
        "forced", "obey", "obeyed"
    };

    private static readonly string[] UncertaintyKeywords =
    {
        "hindi sigurado", "di sigurado", "di sure", "hindi sure", "baka",
        "marahil", "siguro", "sigurado", "maaaring", "unsure", "not sure",
        "maybe", "perhaps"
    };

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
    [SerializeField] private TMP_Text followUpLabelText;
    [SerializeField] private TMP_Text followUpText;

    [Header("Temporary Testing")]
    [SerializeField] private Button testUnlockButton;
    [SerializeField] private string testReflectionID = "test_reflection";

    private readonly HashSet<string> unlockedReflectionIDs = new HashSet<string>();
    private readonly Dictionary<string, string> submittedResponses =
        new Dictionary<string, string>();
    private readonly Dictionary<string, string> selectedFollowUps =
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

        string validationMessage;
        if (!ValidateReflectionResponse(response, out validationMessage))
        {
            if (validationText != null)
            {
                validationText.text = validationMessage;
                validationText.gameObject.SetActive(true);
            }

            Debug.LogWarning(
                "Reconstruction Journal: A Reflection response cannot be empty.");
            return false;
        }

        if (validationText != null)
            validationText.gameObject.SetActive(false);

        selectedFollowUps[selectedReflection.reflectionID] =
            GetBestThemeFollowUp(selectedReflection, response);
        submittedResponses[selectedReflection.reflectionID] = response;
        ShowReflection(selectedReflection);

        Debug.Log(
            "Reconstruction Journal: Completed reflection '" +
            selectedReflection.reflectionID +
            "'.");
        return true;
    }

    private bool ValidateReflectionResponse(
        string response,
        out string validationMessage)
    {
        validationMessage = "";

        if (string.IsNullOrWhiteSpace(response))
        {
            validationMessage = "Magsulat muna bago magnilay.";
            return false;
        }

        if (!ContainsUsableCharacters(response))
        {
            validationMessage = "Subukang magsulat ng malinaw na sagot.";
            return false;
        }

        if (ContainsBlockedLanguage(response))
        {
            validationMessage =
                "Gumamit ng angkop na pananalita sa iyong pagninilay.";
            return false;
        }

        if (HasExcessiveRepeatedCharacters(response) ||
            LooksLikeKeyboardSmash(response))
        {
            validationMessage = "Subukang magsulat ng malinaw na sagot.";
            return false;
        }

        return true;
    }

    private bool HasExcessiveRepeatedCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        char previous = text[0];
        int repeatCount = 1;

        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] == previous)
            {
                repeatCount++;
                if (repeatCount >= 6)
                    return true;
            }
            else
            {
                previous = text[i];
                repeatCount = 1;
            }
        }

        return false;
    }

    private bool LooksLikeKeyboardSmash(string text)
    {
        string normalized = NormalizeForPatternChecks(text);

        foreach (string pattern in KeyboardSmashPatterns)
        {
            if (normalized == pattern)
                return true;
        }

        if (HasRepeatedCharacterSequence(normalized))
            return true;

        if (IsRepetitivePatternDominant(normalized))
            return true;

        // Catch a clearly repeated key sequence without trying to judge
        // whether the response is grammatically correct or in a dictionary.
        for (int sequenceLength = 2; sequenceLength <= 5; sequenceLength++)
        {
            if (normalized.Length < sequenceLength * 3)
                continue;

            string sequence = normalized.Substring(0, sequenceLength);
            bool repeats = true;
            for (int i = sequenceLength; i < normalized.Length; i += sequenceLength)
            {
                int remaining = normalized.Length - i;
                if (remaining < sequenceLength ||
                    normalized.Substring(i, sequenceLength) != sequence)
                {
                    repeats = false;
                    break;
                }
            }

            if (repeats && normalized.Length % sequenceLength == 0)
                return true;
        }

        // A long, vowel-free run with several different keys is also a
        // conservative signal of keyboard smashing (e.g. "xjsklqwe").
        if (normalized.Length >= 8)
        {
            bool hasVowel = false;
            int uniqueCharacters = 0;
            HashSet<char> seen = new HashSet<char>();

            foreach (char character in normalized)
            {
                if ("aeiou".IndexOf(character) >= 0)
                    hasVowel = true;
                if (seen.Add(character))
                    uniqueCharacters++;
            }

            if (!hasVowel && uniqueCharacters >= 5)
                return true;
        }

        return false;
    }

    private bool HasRepeatedCharacterSequence(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 4)
            return false;

        // Two- and three-character sequences repeated across the whole input
        // catch short filler such as "qwqw" and "qweqwe". A two-character
        // sequence with vowels is allowed twice so words such as "haha" are
        // not rejected, but three or more repetitions are still considered
        // obvious filler.
        for (int sequenceLength = 2; sequenceLength <= 3; sequenceLength++)
        {
            if (text.Length % sequenceLength != 0)
                continue;

            int repetitions = text.Length / sequenceLength;
            if (repetitions < 2)
                continue;

            string sequence = text.Substring(0, sequenceLength);
            bool repeats = true;
            for (int i = sequenceLength; i < text.Length; i += sequenceLength)
            {
                if (text.Substring(i, sequenceLength) != sequence)
                {
                    repeats = false;
                    break;
                }
            }

            if (!repeats)
                continue;

            bool sequenceHasVowel = false;
            foreach (char character in sequence)
            {
                if ("aeiou".IndexOf(character) >= 0)
                {
                    sequenceHasVowel = true;
                    break;
                }
            }

            if (sequenceLength == 2 && sequenceHasVowel && repetitions < 3)
                continue;

            return true;
        }

        // Also catch an alternating two-character filler with a partial final
        // sequence, such as "abababa".
        if (text.Length >= 6 && text[0] != text[1])
        {
            for (int i = 2; i < text.Length; i++)
            {
                if (text[i] != text[i % 2])
                    return false;
            }

            return true;
        }

        return false;
    }

    private bool IsRepetitivePatternDominant(string text)
    {
        // Keep short genuine expressions such as "haha" and "hahaha"
        // acceptable. This check only applies when the cleaned response is
        // long enough for repetition to clearly dominate the whole input.
        if (string.IsNullOrEmpty(text) || text.Length < 8)
            return false;

        // Allow one small imperfection in an otherwise repeating 2- or
        // 3-character rhythm (for example, "hahhahahahaha").
        int mismatchLimit = Mathf.Max(1, text.Length / 5);
        for (int sequenceLength = 2; sequenceLength <= 3; sequenceLength++)
        {
            int mismatches = 0;

            for (int i = sequenceLength; i < text.Length; i++)
            {
                if (text[i] != text[i % sequenceLength])
                {
                    mismatches++;
                    if (mismatches > mismatchLimit)
                        break;
                }
            }

            if (mismatches <= mismatchLimit)
                return true;
        }

        return false;
    }

    private bool ContainsBlockedLanguage(string text)
    {
        List<string> tokens = Tokenize(text);

        foreach (string blockedWord in BlockedWords)
        {
            string[] blockedTokens = Tokenize(blockedWord).ToArray();
            if (blockedTokens.Length == 0 || blockedTokens.Length > tokens.Count)
                continue;

            for (int start = 0; start <= tokens.Count - blockedTokens.Length; start++)
            {
                bool matches = true;
                for (int offset = 0; offset < blockedTokens.Length; offset++)
                {
                    if (tokens[start + offset] != blockedTokens[offset])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return true;
            }
        }

        return false;
    }

    private bool ContainsUsableCharacters(string text)
    {
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
                return true;
        }

        return false;
    }

    private string NormalizeForPatternChecks(string text)
    {
        StringBuilder builder = new StringBuilder();
        foreach (char character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private List<string> Tokenize(string text)
    {
        List<string> tokens = new List<string>();
        StringBuilder current = new StringBuilder();

        foreach (char character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
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

    private List<ReflectionTheme> DetectThemes(string response)
    {
        List<ReflectionTheme> themes = new List<ReflectionTheme>();
        List<string> tokens = Tokenize(response);

        AddThemeIfMatched(
            themes,
            tokens,
            ReflectionTheme.Fear,
            FearKeywords);
        AddThemeIfMatched(
            themes,
            tokens,
            ReflectionTheme.BlameOrJudgment,
            BlameOrJudgmentKeywords);
        AddThemeIfMatched(
            themes,
            tokens,
            ReflectionTheme.Authority,
            AuthorityKeywords);
        AddThemeIfMatched(
            themes,
            tokens,
            ReflectionTheme.ConformityOrPressure,
            ConformityOrPressureKeywords);
        AddThemeIfMatched(
            themes,
            tokens,
            ReflectionTheme.Uncertainty,
            UncertaintyKeywords);

        return themes;
    }

    private void AddThemeIfMatched(
        List<ReflectionTheme> themes,
        List<string> responseTokens,
        ReflectionTheme theme,
        string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (ContainsTokenSequence(responseTokens, Tokenize(keyword)))
            {
                themes.Add(theme);
                return;
            }
        }
    }

    private bool ContainsTokenSequence(
        List<string> sourceTokens,
        List<string> searchTokens)
    {
        if (searchTokens.Count == 0 || searchTokens.Count > sourceTokens.Count)
            return false;

        for (int start = 0; start <= sourceTokens.Count - searchTokens.Count; start++)
        {
            bool matches = true;
            for (int offset = 0; offset < searchTokens.Count; offset++)
            {
                if (sourceTokens[start + offset] != searchTokens[offset])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }

    private string GetBestThemeFollowUp(
        ReflectionData reflection,
        string response)
    {
        if (reflection == null || reflection.themeResponses == null)
            return "";

        List<ReflectionTheme> detectedThemes = DetectThemes(response);
        ReflectionThemeResponse bestResponse = null;

        foreach (ReflectionThemeResponse candidate in reflection.themeResponses)
        {
            if (candidate == null ||
                string.IsNullOrWhiteSpace(candidate.journalFollowUp) ||
                !detectedThemes.Contains(candidate.theme))
            {
                continue;
            }

            if (bestResponse == null || candidate.priority > bestResponse.priority)
                bestResponse = candidate;
        }

        return bestResponse != null ? bestResponse.journalFollowUp : "";
    }

    private void ShowReflectionFollowUp(string followUp)
    {
        bool show = !string.IsNullOrWhiteSpace(followUp);

        if (followUpLabelText != null)
            followUpLabelText.gameObject.SetActive(show);

        if (followUpText != null)
        {
            followUpText.text = show ? followUp : "";
            followUpText.gameObject.SetActive(show);
        }
    }

    private void HideReflectionFollowUp()
    {
        ShowReflectionFollowUp("");
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

        string storedFollowUp;
        if (isCompleted &&
            selectedFollowUps.TryGetValue(reflection.reflectionID, out storedFollowUp))
        {
            ShowReflectionFollowUp(storedFollowUp);
        }
        else
        {
            HideReflectionFollowUp();
        }

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

        HideReflectionFollowUp();
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
