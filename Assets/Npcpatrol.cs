using UnityEngine;

public class NPCPatrol : MonoBehaviour
{
    private const string IsWalkingParameterName = "IsWalking";
    private const int GroundHitBufferSize = 16;
    private static readonly int IsWalkingParameterHash = Animator.StringToHash(IsWalkingParameterName);

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
    private bool canSetIsWalking = false;
    private bool invalidPatrolWarningLogged = false;
    private bool isPatrolPaused = false;
    private readonly RaycastHit[] groundHits = new RaycastHit[GroundHitBufferSize];

    void Start()
    {
        CacheIsWalkingParameter();
        TryGetCurrentTarget(out _);
    }

    void Update()
    {
        if (isPatrolPaused)
        {
            SetWalkingAnimation(false);
            return;
        }

        if (!TryGetCurrentTarget(out Transform target)) return;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            SetWalkingAnimation(false);

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

            SetWalkingAnimation(true);

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

    public void SetPatrolPaused(bool paused)
    {
        if (isPatrolPaused == paused) return;

        isPatrolPaused = paused;
        SetWalkingAnimation(!isPatrolPaused && enabled && !isWaiting);
    }

    void SnapToGround()
    {
        Vector3 rayStart = transform.position + Vector3.up * raycastHeight;
        int hitCount = Physics.RaycastNonAlloc(
            rayStart,
            Vector3.down,
            groundHits,
            raycastDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        bool foundGround = false;
        float closestDistance = float.PositiveInfinity;
        Vector3 closestPoint = default;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null) continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

            if (groundHits[i].distance < closestDistance)
            {
                foundGround = true;
                closestDistance = groundHits[i].distance;
                closestPoint = groundHits[i].point;
            }
        }

        if (foundGround)
        {
            Vector3 pos = transform.position;
            pos.y = closestPoint.y + footOffset;
            transform.position = pos;
        }
    }

    private void CacheIsWalkingParameter()
    {
        canSetIsWalking = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                parameters[i].nameHash == IsWalkingParameterHash)
            {
                canSetIsWalking = true;
                return;
            }
        }
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (canSetIsWalking)
        {
            animator.SetBool(IsWalkingParameterHash, isWalking);
        }
    }

    private bool TryGetCurrentTarget(out Transform target)
    {
        target = null;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            WarnInvalidPatrolOnce(gameObject.name + ": No patrol points assigned!");
            enabled = false;
            return false;
        }

        if (currentPointIndex < 0 || currentPointIndex >= patrolPoints.Length)
        {
            currentPointIndex = 0;
        }

        int missingIndex = currentPointIndex;
        for (int offset = 0; offset < patrolPoints.Length; offset++)
        {
            int candidateIndex = (currentPointIndex + offset) % patrolPoints.Length;
            Transform candidate = patrolPoints[candidateIndex];
            if (candidate == null) continue;

            if (offset > 0)
            {
                WarnInvalidPatrolOnce(
                    gameObject.name + ": Patrol point at index " + missingIndex +
                    " is missing. Skipping to the next valid point.");
            }

            currentPointIndex = candidateIndex;
            target = candidate;
            return true;
        }

        WarnInvalidPatrolOnce(gameObject.name + ": All assigned patrol points are missing. Patrol stopped.");
        enabled = false;
        return false;
    }

    private void WarnInvalidPatrolOnce(string message)
    {
        if (invalidPatrolWarningLogged) return;

        invalidPatrolWarningLogged = true;
        Debug.LogWarning(message, this);
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
