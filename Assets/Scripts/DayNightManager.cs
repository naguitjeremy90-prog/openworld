using UnityEngine;

public class DayNightManager : MonoBehaviour
{

    public GameObject dayEnvironment;
    public GameObject nightEnvironment;

    public Material daySkyBox;
    public Material nightSkyBox;

    private void Start()
    {
        if(GameFlags.isMorning)
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
    }

    public void SetNight()
    {
        dayEnvironment.SetActive(false);
        nightEnvironment.SetActive(true);
        RenderSettings.skybox = nightSkyBox;
        DynamicGI.UpdateEnvironment();
    }
}
