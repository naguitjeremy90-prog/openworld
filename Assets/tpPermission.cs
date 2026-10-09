using UnityEngine;

public class GameFlags : MonoBehaviour
{
    public static bool canTeleport = false;
    public static bool isMorning = false;
    public bool churchUnlocked = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNewGame()
    {
        canTeleport = false;
        isMorning = false;
    }
}
