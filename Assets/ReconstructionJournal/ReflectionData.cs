using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Reflection",
    menuName = "Reconstruction Journal/Reflection")]
public class ReflectionData : ScriptableObject
{
    public string reflectionID;
    public string title;
    public ObservationContext context;

    [TextArea(3, 8)]
    public string prompt;

    [TextArea(2, 5)]
    public List<string> suggestedResponses = new List<string>();

    public string GetContextLabel()
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
