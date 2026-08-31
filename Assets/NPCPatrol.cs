using UnityEngine;

public class NPCPatrol : MonoBehaviour
{
    [Header("Patrol Points")]
    public Transform[] patrolPoints;   // Drag in as many points as you want
    public bool loopPatrol = true;     // true = loop back to start, false = ping-pong back and forth

    public float speed = 1.5f;
    public float waitTime = 2f;
    public Animator animator;

    [Header("Ground Snapping")]
    public float raycastHeight = 5f;
    public float raycastDistance = 10f;
    public float footOffset = 0f;
    public LayerMask groundLayer = -1;

    private int currentIndex = 0;
    private int direction = 1; // used only for ping-pong mode
    private Transform target;
    private bool isWaiting = false;
    private float waitTimer = 0f;

    void Start()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: No patrol points assigned!");
            enabled = false;
            return;
        }

        currentIndex = 0;
        target = patrolPoints[currentIndex];
    }

    void Update()
    {
        if (patrolPoints.Length == 0) return;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (animator != null) animator.SetBool("IsWalking", false);

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                AdvanceTarget();
            }
        }
        else
        {
            Vector3 flatTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);

            Vector3 direction3D = (target.position - transform.position);
            direction3D.y = 0;
            if (direction3D.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction3D);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }

            if (animator != null) animator.SetBool("IsWalking", true);

            Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatTargetPos = new Vector3(target.position.x, 0, target.position.z);
            if (Vector3.Distance(flatPos, flatTargetPos) < 0.1f)
            {
                isWaiting = true;
                waitTimer = waitTime;
            }
        }

        SnapToGround();
    }

    void AdvanceTarget()
    {
        if (loopPatrol)
        {
            // 0 -> 1 -> 2 -> ... -> last -> 0 -> ...
            currentIndex = (currentIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Ping-pong: 0 -> 1 -> 2 -> ... -> last -> ... -> 1 -> 0 -> 1 -> ...
            if (patrolPoints.Length > 1)
            {
                if (currentIndex + direction >= patrolPoints.Length || currentIndex + direction < 0)
                {
                    direction *= -1;
                }
                currentIndex += direction;
            }
        }

        target = patrolPoints[currentIndex];
    }

    void SnapToGround()
    {
        Vector3 rayStart = transform.position + Vector3.up * raycastHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + footOffset;
            transform.position = pos;
        }
    }
}