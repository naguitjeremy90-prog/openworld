using System.Collections.Generic;
using UnityEngine;

namespace Supercyan.FreeSample
{
    public class SimpleSampleCharacterControl : MonoBehaviour
    {
        private enum ControlMode
        {
            /// <summary>
            /// Up moves the character forward, left and right turn the character
            /// gradually and down moves the character backwards.
            /// </summary>
            Tank,

            /// <summary>
            /// Character freely moves in the chosen direction
            /// from the perspective of the camera.
            /// </summary>
            Direct
        }

        [SerializeField] private float m_moveSpeed = 4;
        [SerializeField] private float m_turnSpeed = 200;
        [SerializeField] private float m_jumpForce = 4;

        [SerializeField] private Animator m_animator = null;
        [SerializeField] private Rigidbody m_rigidBody = null;

        [SerializeField] private ControlMode m_controlMode = ControlMode.Direct;

        [SerializeField] private bool m_lockVerticalMovement = false;

        private float m_currentV = 0;
        private float m_currentH = 0;

        private readonly float m_interpolation = 10;

        // Normal movement is walking.
        // Holding Shift allows full running speed.
        private readonly float m_walkScale = 0.56f;
        private readonly float m_backwardsWalkScale = 0.16f;
        private readonly float m_backwardRunScale = 0.66f;

        private bool m_wasGrounded;
        private Vector3 m_currentDirection = Vector3.zero;

        private float m_jumpTimeStamp = 0;
        private float m_minJumpInterval = 0.25f;
        private bool m_jumpInput = false;

        private bool m_isGrounded;

        // Prevent tiny bumps from immediately triggering the jump animation.
        private float m_airborneTime = 0f;

        // Peter must be off the ground this long before it counts as a real fall.
        private float m_airborneThreshold = 0.12f;

        // True only when the jump/fall animation has actually started.
        private bool m_isAirborneAnimating = false;

        private List<Collider> m_collisions = new List<Collider>();

        private void Awake()
        {
            if (!m_animator)
            {
                m_animator = GetComponent<Animator>();
            }

            if (!m_rigidBody)
            {
                m_rigidBody = GetComponent<Rigidbody>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ContactPoint[] contactPoints = collision.contacts;

            for (int i = 0; i < contactPoints.Length; i++)
            {
                if (Vector3.Dot(contactPoints[i].normal, Vector3.up) > 0.5f)
                {
                    if (!m_collisions.Contains(collision.collider))
                    {
                        m_collisions.Add(collision.collider);
                    }

                    m_isGrounded = true;
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            ContactPoint[] contactPoints = collision.contacts;

            bool validSurfaceNormal = false;

            for (int i = 0; i < contactPoints.Length; i++)
            {
                if (Vector3.Dot(contactPoints[i].normal, Vector3.up) > 0.5f)
                {
                    validSurfaceNormal = true;
                    break;
                }
            }

            if (validSurfaceNormal)
            {
                m_isGrounded = true;

                if (!m_collisions.Contains(collision.collider))
                {
                    m_collisions.Add(collision.collider);
                }
            }
            else
            {
                if (m_collisions.Contains(collision.collider))
                {
                    m_collisions.Remove(collision.collider);
                }

                if (m_collisions.Count == 0)
                {
                    m_isGrounded = false;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (m_collisions.Contains(collision.collider))
            {
                m_collisions.Remove(collision.collider);
            }

            if (m_collisions.Count == 0)
            {
                m_isGrounded = false;
            }
        }

        private void Update()
        {
            if (!m_jumpInput && Input.GetKey(KeyCode.Space))
            {
                m_jumpInput = true;
            }
        }

        private void FixedUpdate()
        {
            switch (m_controlMode)
            {
                case ControlMode.Direct:
                    DirectUpdate();
                    break;

                case ControlMode.Tank:
                    TankUpdate();
                    break;

                default:
                    Debug.LogError("Unsupported state");
                    break;
            }

            m_wasGrounded = m_isGrounded;
            m_jumpInput = false;
        }

        private void TankUpdate()
        {
            float v = Input.GetAxis("Vertical");
            float h = Input.GetAxis("Horizontal");

            if (m_lockVerticalMovement)
            {
                v = 0;
            }

            bool walk = Input.GetKey(KeyCode.LeftShift);

            if (v < 0)
            {
                if (walk)
                {
                    v *= m_backwardsWalkScale;
                }
                else
                {
                    v *= m_backwardRunScale;
                }
            }
            else if (walk)
            {
                v *= m_walkScale;
            }

            m_currentV = Mathf.Lerp(
                m_currentV,
                v,
                Time.deltaTime * m_interpolation
            );

            m_currentH = Mathf.Lerp(
                m_currentH,
                h,
                Time.deltaTime * m_interpolation
            );

            transform.position +=
                transform.forward *
                m_currentV *
                m_moveSpeed *
                Time.deltaTime;

            transform.Rotate(
                0,
                m_currentH * m_turnSpeed * Time.deltaTime,
                0
            );

            if (m_animator)
            {
                m_animator.SetFloat("MoveSpeed", m_currentV);
            }

            JumpingAndLanding();
        }

        private void DirectUpdate()
        {
            float v = Input.GetAxis("Vertical");
            float h = Input.GetAxis("Horizontal");

            if (m_lockVerticalMovement)
            {
                v = 0;
            }

            if (Camera.main == null)
            {
                return;
            }

            Transform camera = Camera.main.transform;

            // Default = walk.
            // Hold Shift = run.
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                v *= m_walkScale;
                h *= m_walkScale;
            }

            m_currentV = Mathf.Lerp(
                m_currentV,
                v,
                Time.deltaTime * m_interpolation
            );

            m_currentH = Mathf.Lerp(
                m_currentH,
                h,
                Time.deltaTime * m_interpolation
            );

            Vector3 direction =
                camera.forward * m_currentV +
                camera.right * m_currentH;

            float directionLength = direction.magnitude;

            direction.y = 0;

            direction =
                direction.normalized *
                directionLength;

            if (direction != Vector3.zero)
            {
                m_currentDirection = Vector3.Slerp(
                    m_currentDirection,
                    direction,
                    Time.deltaTime * m_interpolation
                );

                transform.rotation =
                    Quaternion.LookRotation(m_currentDirection);

                transform.position +=
                    m_currentDirection *
                    m_moveSpeed *
                    Time.deltaTime;

                if (m_animator)
                {
                    m_animator.SetFloat(
                        "MoveSpeed",
                        direction.magnitude
                    );
                }
            }
            else
            {
                if (m_animator)
                {
                    m_animator.SetFloat("MoveSpeed", 0f);
                }
            }

            JumpingAndLanding();
        }

        private void JumpingAndLanding()
        {
            bool jumpCooldownOver =
                (Time.time - m_jumpTimeStamp) >=
                m_minJumpInterval;

            // --------------------------------------
            // ACTUAL JUMP
            // --------------------------------------

            if (jumpCooldownOver &&
                m_isGrounded &&
                m_jumpInput)
            {
                m_jumpTimeStamp = Time.time;

                if (m_rigidBody)
                {
                    m_rigidBody.AddForce(
                        Vector3.up * m_jumpForce,
                        ForceMode.Impulse
                    );
                }

                // Jump animation starts immediately
                // because the player intentionally jumped.
                m_airborneTime = m_airborneThreshold;
                m_isAirborneAnimating = true;

                if (m_animator)
                {
                    m_animator.SetBool(
                        "Grounded",
                        false
                    );

                    m_animator.SetTrigger(
                        "Jump"
                    );
                }

                return;
            }

            // --------------------------------------
            // FALLING / WALKING OFF A LEDGE
            // --------------------------------------

            if (!m_isGrounded)
            {
                m_airborneTime += Time.fixedDeltaTime;

                // Ignore tiny bumps.
                // Only start airborne animation
                // if Peter stays off the ground
                // longer than the threshold.
                if (m_airborneTime >= m_airborneThreshold &&
                    !m_isAirborneAnimating)
                {
                    m_isAirborneAnimating = true;

                    if (m_animator)
                    {
                        m_animator.SetBool(
                            "Grounded",
                            false
                        );

                        m_animator.SetTrigger(
                            "Jump"
                        );
                    }
                }
            }

            // --------------------------------------
            // GROUNDED / LANDING
            // --------------------------------------

            else
            {
                m_airborneTime = 0f;

                if (m_isAirborneAnimating)
                {
                    if (m_animator)
                    {
                        m_animator.SetBool(
                            "Grounded",
                            true
                        );

                        m_animator.SetTrigger(
                            "Land"
                        );
                    }

                    m_isAirborneAnimating = false;
                }
                else
                {
                    // Tiny bump happened but wasn't
                    // long enough to count as airborne.
                    if (m_animator)
                    {
                        m_animator.SetBool(
                            "Grounded",
                            true
                        );
                    }
                }
            }
        }
    }
}