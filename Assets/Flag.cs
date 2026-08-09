using UnityEngine;

public class Flag : MonoBehaviour
{
    public ChurchGlow churchGlow;
    public void CorrectChoice()
    {
        GameFlags.canTeleport = true;
        churchGlow.UnlockTeleport();
    }
}
