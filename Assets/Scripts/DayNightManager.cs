using System;
using UnityEngine;

public class DayNightManager : MonoBehaviour
{

    public GameObject dayEnvironment;
    public GameObject nightEnvironment;

    public Material daySkyBox;
    public Material nightSkyBox;

    [Header("Testing")]
    [SerializeField] private bool forceDaytimeForTesting = false;

    public bool IsNight => forceDaytimeForTesting ? false : (hasCurrentState ? isNight : !GameFlags.isMorning);
    public bool IsDay => !IsNight;

    public event Action<bool> DayNightStateChanged;

    private bool isNight;
    private bool hasCurrentState;

    private void Start()
    {
        if(forceDaytimeForTesting || GameFlags.isMorning)
            SetDay();
        else
        {
            SetNight();
        }
            
    }
    public void SetDay()
    {
        dayEnvironment.SetActive(true);
        nightEnvironment.SetActive(false);
        RenderSettings.skybox = daySkyBox;
        DynamicGI.UpdateEnvironment();
        UpdateCurrentState(false);
    }

    public void SetNight()
    {
        dayEnvironment.SetActive(false);
        nightEnvironment.SetActive(true);
        RenderSettings.skybox = nightSkyBox;
        DynamicGI.UpdateEnvironment();
        UpdateCurrentState(true);
    }

    private void UpdateCurrentState(bool newIsNight)
    {
        bool stateChanged = !hasCurrentState || isNight != newIsNight;

        isNight = newIsNight;
        hasCurrentState = true;

        if (stateChanged)
            DayNightStateChanged?.Invoke(isNight);
    }
}
