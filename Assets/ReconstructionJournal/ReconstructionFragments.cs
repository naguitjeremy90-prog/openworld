using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReconstructionFragments : MonoBehaviour
{
    [Header("Available Fragments")]
    [SerializeField] private List<FragmentData> allFragments = new List<FragmentData>();

    [Header("Fragment List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private Button listButtonPrefab;
    [SerializeField] private TMP_Text emptyListText;

    [Header("Fragment Details")]
    [SerializeField] private Image fragmentImage;
    [SerializeField] private TMP_Text imagePlaceholderText;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text recoveredInText;
    [SerializeField] private TMP_Text relatedToText;
    [SerializeField] private TMP_Text interpretationText;

    [Header("Temporary Testing")]
    [SerializeField] private Button testUnlockButton;
    [SerializeField] private Button testStageOneButton;
    [SerializeField] private string testFragmentID = "test_fragment";

    private readonly HashSet<string> unlockedFragmentIDs = new HashSet<string>();
    private readonly Dictionary<string, int> currentStages = new Dictionary<string, int>();
    private readonly List<GameObject> spawnedListButtons = new List<GameObject>();

    private FragmentData selectedFragment;

    public int UnlockedFragmentCount
    {
        get { return unlockedFragmentIDs.Count; }
    }

    private void Awake()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.AddListener(UnlockTestFragment);

        if (testStageOneButton != null)
            testStageOneButton.onClick.AddListener(SetTestFragmentStageOne);

        RefreshList();
    }

    private void OnDestroy()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.RemoveListener(UnlockTestFragment);

        if (testStageOneButton != null)
            testStageOneButton.onClick.RemoveListener(SetTestFragmentStageOne);
    }

    public bool UnlockFragment(string fragmentID)
    {
        FragmentData fragment = FindFragment(fragmentID);

        if (fragment == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Fragment ID '" + fragmentID + "' was not found.");
            return false;
        }

        if (!unlockedFragmentIDs.Add(fragmentID))
            return false;

        currentStages[fragmentID] = 0;
        RefreshList();
        Debug.Log("Reconstruction Journal: Unlocked fragment '" + fragmentID + "'.");
        return true;
    }

    public bool SetFragmentStage(string fragmentID, int stageIndex)
    {
        FragmentData fragment = FindFragment(fragmentID);

        if (fragment == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Fragment ID '" + fragmentID + "' was not found.");
            return false;
        }

        if (!unlockedFragmentIDs.Contains(fragmentID))
        {
            Debug.LogWarning(
                "Reconstruction Journal: Fragment '" + fragmentID +
                "' must be unlocked before changing its stage.");
            return false;
        }

        if (stageIndex < 0 || stageIndex >= fragment.interpretationStages.Count)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Stage " + stageIndex +
                " is invalid for fragment '" + fragmentID + "'.");
            return false;
        }

        currentStages[fragmentID] = stageIndex;

        if (selectedFragment == fragment)
            ShowFragment(fragment);

        Debug.Log(
            "Reconstruction Journal: Set fragment '" + fragmentID +
            "' to stage " + stageIndex + ".");
        return true;
    }

    public bool IsFragmentUnlocked(string fragmentID)
    {
        return unlockedFragmentIDs.Contains(fragmentID);
    }

    public int GetFragmentStage(string fragmentID)
    {
        int stageIndex;
        return currentStages.TryGetValue(fragmentID, out stageIndex) ? stageIndex : -1;
    }

    public void UnlockTestFragment()
    {
        UnlockFragment(testFragmentID);
    }

    public void SetTestFragmentStageOne()
    {
        SetFragmentStage(testFragmentID, 1);
    }

    public void RefreshList()
    {
        ClearSpawnedButtons();

        foreach (FragmentData fragment in allFragments)
        {
            if (fragment == null || !unlockedFragmentIDs.Contains(fragment.fragmentID))
                continue;

            CreateListButton(fragment);
        }

        if (emptyListText != null)
            emptyListText.gameObject.SetActive(spawnedListButtons.Count == 0);

        if (selectedFragment == null ||
            !unlockedFragmentIDs.Contains(selectedFragment.fragmentID))
        {
            ClearDetails();
        }
    }

    private FragmentData FindFragment(string fragmentID)
    {
        if (string.IsNullOrWhiteSpace(fragmentID))
            return null;

        return allFragments.Find(
            fragment => fragment != null && fragment.fragmentID == fragmentID);
    }

    private void CreateListButton(FragmentData fragment)
    {
        if (listContent == null || listButtonPrefab == null)
            return;

        Button newButton = Instantiate(listButtonPrefab, listContent);
        newButton.gameObject.SetActive(true);
        newButton.name = "Fragment_" + fragment.fragmentID;

        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = fragment.title;

        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(() => ShowFragment(fragment));
        spawnedListButtons.Add(newButton.gameObject);
    }

    private void ShowFragment(FragmentData fragment)
    {
        selectedFragment = fragment;

        if (fragmentImage != null)
        {
            fragmentImage.sprite = fragment.fragmentImage;
            fragmentImage.gameObject.SetActive(fragment.fragmentImage != null);
        }

        if (imagePlaceholderText != null)
            imagePlaceholderText.gameObject.SetActive(fragment.fragmentImage == null);

        if (detailTitleText != null)
            detailTitleText.text = fragment.title;

        if (recoveredInText != null)
            recoveredInText.text = fragment.GetRecoveredInLabel();

        if (relatedToText != null)
            relatedToText.text = fragment.GetRelatedToLabel();

        int stageIndex = GetFragmentStage(fragment.fragmentID);
        if (interpretationText != null)
            interpretationText.text = fragment.interpretationStages[stageIndex];
    }

    private void ClearDetails()
    {
        selectedFragment = null;

        if (fragmentImage != null)
        {
            fragmentImage.sprite = null;
            fragmentImage.gameObject.SetActive(false);
        }

        if (imagePlaceholderText != null)
        {
            imagePlaceholderText.gameObject.SetActive(true);
            imagePlaceholderText.text = "No Image";
        }

        if (detailTitleText != null)
            detailTitleText.text = "No Fragment Selected";

        if (recoveredInText != null)
            recoveredInText.text = "";

        if (relatedToText != null)
            relatedToText.text = "";

        if (interpretationText != null)
            interpretationText.text =
                "Unlock and select a Fragment to examine Peter's interpretation.";
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
}
