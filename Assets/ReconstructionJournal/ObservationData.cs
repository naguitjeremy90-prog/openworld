using UnityEngine;

[CreateAssetMenu(
    fileName = "NewObservation",
    menuName = "Reconstruction Journal/Observation")]
public class ObservationData : ScriptableObject
{
    [Header("Identity")]
    public string observationID;
    public string title;

    [Header("Content")]
    public ObservationContext context;

    [TextArea(4, 10)]
    public string observationText;

    public string GetContextLabel()
    {
        switch (context)
        {
            case ObservationContext.PresentPili:
                return "PRESENT PILI";

            case ObservationContext.MakamisaPili:
                return "MAKAMISA";

            default:
                return context.ToString();
        }
    }
}
