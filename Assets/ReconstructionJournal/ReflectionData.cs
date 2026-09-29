using System.Collections.Generic;
using UnityEngine;

public enum ReflectionTheme
{
    Fear,
    BlameOrJudgment,
    Authority,
    ConformityOrPressure,
    Uncertainty
}

[System.Serializable]
public class ReflectionThemeResponse
{
    public ReflectionTheme theme;

    [TextArea(2, 5)]
    public string journalFollowUp;

    public int priority;
}

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

    public List<ReflectionThemeResponse> themeResponses =
        new List<ReflectionThemeResponse>();

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
