using UnityEngine;

public class ChurchGlow : MonoBehaviour
{
    public Light glowLight;

    void Start()
    {
        glowLight.intensity = 0f;
    }
    void Update()
    {
        if (glowLight.intensity > 0)
        {
            glowLight.intensity = 1.8f + Mathf.PingPong(Time.time * 2f, 0.5f);
        }
    }
    public void UnlockTeleport()
    {
        glowLight.intensity = 2.5f;
    }
}