using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Person",
    menuName = "Reconstruction Journal/Person")]
public class PersonData : ScriptableObject
{
    public string personID;
    public string characterName;
    public Sprite portrait;
    public PersonContext context;

    [TextArea(3, 8)]
    public List<string> descriptionStages = new List<string>();

    public string GetContextLabel()
    {
        switch (context)
        {
            case PersonContext.MakamisaPili:
                return "MAKAMISA";
            default:
                return "PRESENT PILI";
        }
    }
}
