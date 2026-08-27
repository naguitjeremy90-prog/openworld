using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReconstructionObservations : MonoBehaviour
{
    [Header("Available Observations")]
    [SerializeField] private List<ObservationData> allObservations = new List<ObservationData>();

    [Header("Observation List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private Button listButtonPrefab;
    [SerializeField] private TMP_Text emptyListText;

    [Header("Observation Details")]
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailContextText;
    [SerializeField] private TMP_Text detailObservationText;

    [Header("Temporary Testing")]
    [SerializeField] private Button testUnlockButton;
    [SerializeField] private string testObservationID = "test_arrival";

    private readonly HashSet<string> unlockedObservationIDs = new HashSet<string>();
    private readonly List<GameObject> spawnedListButtons = new List<GameObject>();

    private ObservationData selectedObservation;

    public int UnlockedObservationCount
    {
        get { return unlockedObservationIDs.Count; }
    }

    private void Awake()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.AddListener(UnlockTestObservation);

        RefreshList();
    }

    private void OnDestroy()
    {
        if (testUnlockButton != null)
            testUnlockButton.onClick.RemoveListener(UnlockTestObservation);
    }

    public bool UnlockObservation(string observationID)
    {
        ObservationData observation = FindObservation(observationID);

        if (observation == null)
        {
            Debug.LogWarning(
                "Reconstruction Journal: Observation ID '" +
                observationID +
                "' was not found.");
            return false;
        }

        if (!unlockedObservationIDs.Add(observationID))
            return false;

        RefreshList();
        Debug.Log("Reconstruction Journal: Unlocked observation '" + observationID + "'.");
        return true;
    }

    public bool IsObservationUnlocked(string observationID)
    {
        return unlockedObservationIDs.Contains(observationID);
    }

    public void UnlockTestObservation()
    {
        UnlockObservation(testObservationID);
    }

    public void RefreshList()
    {
        ClearSpawnedButtons();

        foreach (ObservationData observation in allObservations)
        {
            if (observation == null)
                continue;

            if (!unlockedObservationIDs.Contains(observation.observationID))
                continue;

            CreateListButton(observation);
        }

        if (emptyListText != null)
            emptyListText.gameObject.SetActive(spawnedListButtons.Count == 0);

        if (selectedObservation == null ||
            !unlockedObservationIDs.Contains(selectedObservation.observationID))
        {
            ClearDetails();
        }
    }

    private ObservationData FindObservation(string observationID)
    {
        if (string.IsNullOrWhiteSpace(observationID))
            return null;

        return allObservations.Find(
            observation =>
                observation != null &&
                observation.observationID == observationID);
    }

    private void CreateListButton(ObservationData observation)
    {
        if (listContent == null || listButtonPrefab == null)
            return;

        Button newButton = Instantiate(listButtonPrefab, listContent);
        newButton.gameObject.SetActive(true);
        newButton.name = "Observation_" + observation.observationID;

        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = observation.title;

        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(() => ShowObservation(observation));

        spawnedListButtons.Add(newButton.gameObject);
    }

    private void ShowObservation(ObservationData observation)
    {
        selectedObservation = observation;

        if (detailTitleText != null)
            detailTitleText.text = observation.title;

        if (detailContextText != null)
            detailContextText.text = observation.GetContextLabel();

        if (detailObservationText != null)
            detailObservationText.text = observation.observationText;
    }

    private void ClearDetails()
    {
        selectedObservation = null;

        if (detailTitleText != null)
            detailTitleText.text = "No Observation Selected";

        if (detailContextText != null)
            detailContextText.text = "";

        if (detailObservationText != null)
            detailObservationText.text =
                "Unlock and select an Observation to read Peter's notes.";
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
