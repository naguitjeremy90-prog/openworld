using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class HistorianPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 10f;

    private CharacterController controller;
    private Animator animator;
    private InputAction moveAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Cache the Move action from the project-wide actions
        moveAction = InputSystem.actions.FindAction("Move");
        
        if (moveAction == null)
        {
            Debug.LogError("Move action not found in InputSystem.actions. Please ensure a 'Move' action is defined in the Player map.");
        }
    }

    private void Update()
    {
        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);

        if (move.magnitude > 0.1f)
        {
            // Move the character
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Rotate to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Apply gravity
        if (!controller.isGrounded)
        {
            controller.Move(Vector3.down * 9.81f * Time.deltaTime);
        }

        // Update Animator Speed parameter
        animator.SetFloat("Speed", move.magnitude);
    }
}
