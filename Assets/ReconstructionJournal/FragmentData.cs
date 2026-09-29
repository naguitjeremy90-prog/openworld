using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Fragment",
    menuName = "Reconstruction Journal/Fragment")]
public class FragmentData : ScriptableObject
{
    public string fragmentID;
    public string title;
    public Sprite fragmentImage;
    public ObservationContext recoveredIn;
    public ObservationContext relatedTo;

    [TextArea(3, 8)]
    public List<string> interpretationStages = new List<string>();

    public string GetRecoveredInLabel()
    {
        return "NAKALAP SA: " + GetContextLabel(recoveredIn);
    }

    public string GetRelatedToLabel()
    {
        return "KAUGNAY SA: " + GetContextLabel(relatedTo);
    }

    private string GetContextLabel(ObservationContext context)
    {
        switch (context)
        {
            case ObservationContext.MakamisaPili:
                return "MAKAMISA";
            default:
                return "KASALUKUYANG PILI";
        }
    }
}
