using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public Transform cameraTransform;

    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float staminaUseMultiplier = 1f;

    private Rigidbody rb;
    private Animator anim;
    private PlayerStats playerStats;

    public GameObject moveText;
    private bool hasMoved = false;
    public static bool movementDone = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;
        playerStats = GetComponent<PlayerStats>();
    }

    void FixedUpdate()
    {
        if (!hasMoved && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)))
        {
            hasMoved = true;
            movementDone = true;
            if (moveText != null)
                moveText.SetActive(false);
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;

        Vector3 move = (camForward * v + camRight * h).normalized;

        float currentSpeed = walkSpeed * speedMultiplier;

        if (playerStats != null)
        {
            bool wantsToSprint = Input.GetKey(KeyCode.LeftShift);
            bool isMoving = move.magnitude > 0f;
            bool hasStamina = playerStats.currentStamina > 0f;

            if (wantsToSprint && isMoving && hasStamina)
            {
                currentSpeed = sprintSpeed * speedMultiplier;
                playerStats.currentStamina -= 20f * staminaUseMultiplier * Time.fixedDeltaTime;

                if (playerStats.currentStamina < 0f)
                    playerStats.currentStamina = 0f;
            }
            else
            {
                currentSpeed = walkSpeed * speedMultiplier;
            }
        }

        rb.MovePosition(rb.position + move * currentSpeed * Time.fixedDeltaTime);

        if (anim != null)
        {
            anim.SetFloat("X", h);
            anim.SetFloat("Y", v);
        }
    }
}