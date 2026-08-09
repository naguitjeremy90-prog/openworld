using UnityEngine;

public class CameraRotate : MonoBehaviour
{
    public static CameraRotate Instance;

    public Transform target;
    public float distance = 45f;
    public float sensitivity = 400f;

    public float yaw = 45f;
    public float pitch = 20f;

    public bool updatingRotation = false;

    private void Awake()
    {
        Instance = this;
    }

    void LateUpdate()
    {
        if (updatingRotation) return;
        if (ShopInteraction.IsShopOpen) return;

        if (Input.GetMouseButton(1)) // hold right click
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * distance);

        transform.position = position;
        transform.rotation = rotation;
    }
}