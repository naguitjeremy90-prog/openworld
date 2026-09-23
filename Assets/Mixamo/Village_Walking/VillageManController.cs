using UnityEngine;

public class VillageManController : MonoBehaviour
{
    public enum DestinationMode
    {
        Random,
        Sequential
    }

    [Header("Random NPC Destinations")]
    public Transform[] destinations;

    [Header("Destination Order")]
    public DestinationMode destinationMode = DestinationMode.Random;
    [Min(0f)] public float sequentialWaypointPause = 0.25f;

    [Header("Optional Final Exit")]
    public bool deactivateAtFinalDestination;
    public Transform finalDestination;

    [Header("Waiting")]
    public float minWaitTime = 2f;
    public float maxWaitTime = 6f;

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float stoppingDistance = 0.5f;
    public float rotationSpeed = 5f;

    [Header("Obstacle Avoidance")]
    public float obstacleCheckDistance = 1.5f;
    public float obstacleCheckHeight = 0.8f;
    public float avoidanceTurnAngle = 90f;
    public float avoidanceDuration = 1.2f;
    public LayerMask obstacleLayer;

    [Header("Reaction")]
    public float reactionDuration = 1.5f;
    public float reactionCooldown = 1f;

    private Animator animator;

    private bool isReacting = false;
    private bool canReact = true;
    private bool isWaiting = false;
    private bool isAvoiding = false;

    private float avoidanceTimer = 0f;

    private int currentDestination = -1;

    void Start()
    {
        animator = GetComponent<Animator>();

        animator.SetBool("IsWalking", true);
        animator.SetBool("IsReacting", false);

        if (destinationMode == DestinationMode.Sequential)
            currentDestination = destinations != null && destinations.Length > 0 ? 0 : -1;
        else
            ChooseRandomDestination();
    }

    void Update()
    {
        if (isReacting)
            return;

        if (isWaiting)
            return;

        if (destinations == null || destinations.Length == 0)
            return;

        Transform target = destinations[currentDestination];

        if (target == null)
        {
            if (destinationMode == DestinationMode.Sequential)
            {
                if (currentDestination < destinations.Length - 1)
                    currentDestination++;
                else
                {
                    animator.SetBool("IsWalking", false);
                    isWaiting = true;
                }
            }
            else
                ChooseRandomDestination();
            return;
        }

        // --------------------------------
        // AVOIDANCE MODE
        // --------------------------------

        if (isAvoiding)
        {
            avoidanceTimer -= Time.deltaTime;

            if (avoidanceTimer <= 0f)
            {
                isAvoiding = false;
            }
            else
            {
                transform.position +=
                    transform.forward *
                    moveSpeed *
                    Time.deltaTime;

                animator.SetBool("IsWalking", true);

                return;
            }
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0;

        float distance = direction.magnitude;

        // Reached destination
        if (distance <= stoppingDistance)
        {
            animator.SetBool("IsWalking", false);

            if (deactivateAtFinalDestination && target == finalDestination)
            {
                gameObject.SetActive(false);
                return;
            }

            StartWaiting();

            return;
        }

        // --------------------------------
        // CHECK FOR OBSTACLE
        // --------------------------------

        Vector3 rayOrigin =
            transform.position +
            Vector3.up * obstacleCheckHeight;

        if (Physics.Raycast(
            rayOrigin,
            transform.forward,
            obstacleCheckDistance,
            obstacleLayer))
        {
            StartAvoidance();

            return;
        }

        // --------------------------------
        // NORMAL MOVEMENT
        // --------------------------------

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        transform.position +=
            transform.forward *
            moveSpeed *
            Time.deltaTime;

        animator.SetBool("IsWalking", true);
    }

    void StartAvoidance()
    {
        isAvoiding = true;

        avoidanceTimer = avoidanceDuration;

        transform.Rotate(
            Vector3.up,
            avoidanceTurnAngle
        );

        animator.SetBool("IsWalking", true);
    }

    void ChooseRandomDestination()
    {
        if (destinations == null || destinations.Length == 0)
            return;

        int newDestination;

        do
        {
            newDestination = Random.Range(
                0,
                destinations.Length
            );

        } while (
            destinations.Length > 1 &&
            newDestination == currentDestination
        );

        currentDestination = newDestination;
    }

    void StartWaiting()
    {
        if (isWaiting)
            return;

        isWaiting = true;

        if (destinationMode == DestinationMode.Sequential)
        {
            if (currentDestination < destinations.Length - 1)
                Invoke(nameof(GoToNextDestination), sequentialWaypointPause);
            return;
        }

        float waitTime = Random.Range(
            minWaitTime,
            maxWaitTime
        );

        Invoke(nameof(GoToNextDestination), waitTime);
    }

    void GoToNextDestination()
    {
        isWaiting = false;

        if (destinationMode == DestinationMode.Sequential)
            currentDestination++;
        else
            ChooseRandomDestination();

        animator.SetBool("IsWalking", true);
    }

    // --------------------------------
    // PLAYER COLLISION / REACTION
    // --------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && canReact)
        {
            StartReaction();
        }
    }

    void StartReaction()
    {
        if (isReacting)
            return;

        isReacting = true;
        canReact = false;

        animator.SetBool("IsWalking", false);
        animator.SetBool("IsReacting", true);

        Invoke(nameof(EndReaction), reactionDuration);
    }

    void EndReaction()
    {
        isReacting = false;

        animator.SetBool("IsReacting", false);

        if (!isWaiting &&
            destinations != null &&
            destinations.Length > 0)
        {
            animator.SetBool("IsWalking", true);
        }

        Invoke(nameof(EnableReaction), reactionCooldown);
    }

    void EnableReaction()
    {
        canReact = true;
    }
}
