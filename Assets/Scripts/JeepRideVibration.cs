using UnityEngine;

public class JeepRideVibration : MonoBehaviour
{
    [SerializeField] private float verticalAmount = 0.015f;
    [SerializeField] private float horizontalAmount = 0.008f;
    [SerializeField] private float vibrationSpeed = 12f;

    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * vibrationSpeed) * verticalAmount;
        float x = Mathf.Sin(Time.time * vibrationSpeed * 0.7f) * horizontalAmount;

        transform.localPosition =
            startPosition + new Vector3(x, y, 0f);
    }
}
