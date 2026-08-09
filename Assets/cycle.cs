using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public float dayDuration = 60f; // seconds for full day
    private float rotationSpeed;

    void Start()
    {
        rotationSpeed = 360f / dayDuration;
    }

    void Update()
    {
        // Rotate the light the script is attached to
        transform.Rotate(Vector3.right, rotationSpeed * Time.deltaTime);
    }
}