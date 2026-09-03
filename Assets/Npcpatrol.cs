using UnityEngine;

public class NPCPatrol : MonoBehaviour
{
    [Header("Patrol Points (add as many as you want, in order)")]
    public Transform[] patrolPoints;

    [Header("Movement Settings")]
    public float speed = 3f;
    public float waitTime = 1f;

    [Header("Animation")]
    public Animator animator; // drag this NPC's own Animator component here

    [Header("Ground Snapping")]
    public float raycastHeight = 5f;      // how far above the NPC the ray starts
    public float raycastDistance = 10f;   // how far down it checks
    public float footOffset = 0f;         // fine-tune if feet float or sink slightly
    public LayerMask groundLayer;         // set this to ONLY your ground/terrain layer (never "Everything" and never the Player layer)

    private int currentPointIndex = 0;
    private bool isWaiting = false;
    private float waitTimer = 0f;

    void Start()
    {
        if (patrolPoints.Length == 0)
        {
            Debug.LogWarning(gameObject.name + ": No patrol points assigned!");
            enabled = false;
        }
    }

    void Update()
    {
        if (patrolPoints.Length == 0) return;

        Transform target = patrolPoints[currentPointIndex];

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (animator != null) animator.SetBool("IsWalking", false);

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                // Move to the next point in the list, looping back to 0 at the end
                currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
            }
        }
        else
        {
            // Move toward target on X/Z only — Y is handled separately by SnapToGround
            Vector3 flatTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);

            // Face movement direction
            Vector3 direction = target.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }

            if (animator != null) animator.SetBool("IsWalking", true);

            // Check X/Z distance only (ignore height difference)
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

    // Draws lines between patrol points in the Scene view so you can see the route while editing
    void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;
            Transform next = patrolPoints[(i + 1) % patrolPoints.Length];
            if (next == null) continue;
            Gizmos.DrawLine(patrolPoints[i].position, next.position);
            Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);
        }
    }
}