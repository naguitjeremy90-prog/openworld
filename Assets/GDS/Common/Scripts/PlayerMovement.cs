using UnityEngine;
using UnityEngine.InputSystem;

namespace GDS.Common.Scripts {
    /// <summary>
    /// Adds character WASD movement
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour {
        public float speed = 10f;
        Rigidbody rb;
        Vector2 movement = new(0, 0);

        void Awake() => rb = GetComponent<Rigidbody>();

        void FixedUpdate() {
            Vector3 dir = new(movement.x, 0, movement.y);
            if (dir.sqrMagnitude > 0.01f) {
                Vector3 move = dir.normalized * speed * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + move);
                rb.MoveRotation(Quaternion.LookRotation(dir));
            }
        }

        public void OnMove(InputValue value) => movement = value.Get<Vector2>();
    }
}