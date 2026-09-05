using UnityEngine;

public class NPCSchedule : MonoBehaviour
{
    [SerializeField] private GameObject npcObject;
    [SerializeField] private bool activeDuringDay = true;
    [SerializeField] private bool activeDuringNight = true;

    private DayNightManager dayNightManager;

    private void OnEnable()
    {
        dayNightManager = FindAnyObjectByType<DayNightManager>();

        if (dayNightManager == null)
        {
            Debug.LogWarning("NPCSchedule could not find a DayNightManager in the scene.", this);
            return;
        }

        dayNightManager.DayNightStateChanged += HandleDayNightStateChanged;
        ApplySchedule(dayNightManager.IsNight);
    }

    private void OnDisable()
    {
        if (dayNightManager != null)
            dayNightManager.DayNightStateChanged -= HandleDayNightStateChanged;

        dayNightManager = null;
    }

    private void HandleDayNightStateChanged(bool isNight)
    {
        ApplySchedule(isNight);
    }

    private void ApplySchedule(bool isNight)
    {
        if (npcObject == null)
            return;

        bool shouldBeActive = isNight ? activeDuringNight : activeDuringDay;

        if (npcObject.activeSelf != shouldBeActive)
            npcObject.SetActive(shouldBeActive);
    }
}
