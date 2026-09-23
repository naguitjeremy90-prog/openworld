using UnityEngine;

/// <summary>Moves an NPC once toward an authored destination when externally released.</summary>
public sealed class OneWayNPCDeparture : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField, Min(0f)] private float movementSpeed = 3.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 180f;
    [SerializeField, Min(0f)] private float arrivalThreshold = 0.25f;
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParameter = "IsWalking";
    [SerializeField] private bool deactivateOnArrival = true;

    private bool departing;
    private bool completed;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        SetWalking(false);
    }

    public void BeginDeparture()
    {
        if (departing || completed)
            return;

        if (destination == null || animator == null)
        {
            Debug.LogWarning("One-way NPC departure needs a destination and Animator.", this);
            return;
        }

        departing = true;
        SetWalking(true);
    }

    private void Update()
    {
        if (!departing)
            return;

        Vector3 position = transform.position;
        Vector3 target = new Vector3(destination.position.x, position.y, destination.position.z);
        Vector3 direction = target - position;

        if (direction.magnitude <= arrivalThreshold)
        {
            FinishDeparture();
            return;
        }

        Quaternion facing = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, rotationSpeed * Time.deltaTime);

        // Turn toward the doorway before translating so the walk does not slide sideways.
        if (Quaternion.Angle(transform.rotation, facing) <= 10f)
            transform.position = Vector3.MoveTowards(position, target, movementSpeed * Time.deltaTime);
    }

    private void FinishDeparture()
    {
        departing = false;
        completed = true;
        SetWalking(false);

        if (deactivateOnArrival)
            gameObject.SetActive(false);
    }

    private void SetWalking(bool walking)
    {
        if (animator != null && !string.IsNullOrEmpty(walkingParameter))
            animator.SetBool(walkingParameter, walking);
    }
}
