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

        // Small downhill contact recovery for the Rigidbody capsule. Keep this
        // deliberately shorter than a typical stair riser or ledge.
        private const float GroundProbeStartOffset = 0.02f;
        private const float MaxGroundSnapDistance = 0.1f;
        private const float GroundSnapVerticalVelocityLimit = 0.1f;
        private const float GroundNormalThreshold = 0.5f;

        [SerializeField] private Animator m_animator = null;
        [SerializeField] private Rigidbody m_rigidBody = null;
        private CapsuleCollider m_capsuleCollider;

        [SerializeField] private ControlMode m_controlMode = ControlMode.Direct;

        [SerializeField] private bool m_lockVerticalMovement = false;

        private float m_currentV = 0;
        private float m_currentH = 0;

        private readonly float m_interpolation = 10;
        private const float LocomotionStateMovementThreshold = 0.05f;

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
        private bool m_hasGroundContact;
        private bool m_wasStorySequenceActive;
        private readonly RaycastHit[] m_groundProbeHits = new RaycastHit[12];

        // Prevent tiny bumps from immediately triggering the jump animation.
        private float m_airborneTime = 0f;

        // Peter must be off the ground this long before it counts as a real fall.
        private float m_airborneThreshold = 0.12f;

        // True only when the jump/fall animation has actually started.
        private bool m_isAirborneAnimating = false;
        private bool m_intentionalJumpInProgress;

        private global::PlayerMovementAudio m_playerMovementAudio;

        private List<Collider> m_collisions = new List<Collider>();

        // Read-only diagnostics for temporary development instrumentation.
        public bool DebugIsGrounded => m_isGrounded;
        public int DebugGroundContactCount => m_collisions.Count;

        // Read-only locomotion state for systems that need gameplay intent rather than
        // the Animator's temporarily blended child motions.
        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsGrounded => m_isGrounded && !m_isAirborneAnimating;

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

            m_capsuleCollider = GetComponent<CapsuleCollider>();

            m_playerMovementAudio = GetComponent<global::PlayerMovementAudio>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            ContactPoint[] contactPoints = collision.contacts;

            for (int i = 0; i < contactPoints.Length; i++)
            {
                if (IsWalkableGroundNormal(contactPoints[i].normal))
                {
                    if (!m_collisions.Contains(collision.collider))
                    {
                        m_collisions.Add(collision.collider);
                    }

                    m_isGrounded = true;
                    m_hasGroundContact = true;
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            ContactPoint[] contactPoints = collision.contacts;

            bool validSurfaceNormal = false;

            for (int i = 0; i < contactPoints.Length; i++)
            {
                if (IsWalkableGroundNormal(contactPoints[i].normal))
                {
                    validSurfaceNormal = true;
                    break;
                }
            }

            if (validSurfaceNormal)
            {
                m_isGrounded = true;
                m_hasGroundContact = true;

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
            if (StorySequenceCoordinator.IsStorySequenceActive)
            {
                m_jumpInput = false;
                return;
            }

            if (!m_jumpInput && Input.GetKey(KeyCode.Space))
            {
                m_jumpInput = true;
            }
        }

        private void FixedUpdate()
        {
            bool storySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;

            if (storySequenceActive)
            {
                m_wasStorySequenceActive = true;
                m_currentV = 0f;
                m_currentH = 0f;
                m_currentDirection = Vector3.zero;
                m_jumpInput = false;
                IsMoving = false;
                IsRunning = false;

                if (m_animator)
                {
                    m_animator.SetFloat("MoveSpeed", 0f);
                    // Keep the locomotion controller suppressed during story sequences,
                    // while allowing the Animator to reflect the physical ground state.
                    m_animator.SetBool("Grounded", m_isGrounded);
                }

                return;
            }

            if (m_wasStorySequenceActive)
            {
                // Story sequences suppress normal locomotion updates. Once control
                // returns, bring the Animator's grounding parameter back in sync
                // with the physics state before normal locomotion resumes.
                if (m_animator)
                    m_animator.SetBool("Grounded", m_isGrounded);

                m_wasStorySequenceActive = false;
            }

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

            IsMoving = Mathf.Abs(m_currentV) >= LocomotionStateMovementThreshold;
            IsRunning = IsMoving && !walk;

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
                IsMoving = false;
                IsRunning = false;
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

            IsMoving = directionLength >= LocomotionStateMovementThreshold;
            IsRunning = IsMoving && Input.GetKey(KeyCode.LeftShift);

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
            if (!m_isGrounded)
            {
                TryApplyGroundAdhesion();
            }

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
                m_intentionalJumpInProgress = true;

                if (m_rigidBody)
                {
                    m_rigidBody.AddForce(
                        Vector3.up * m_jumpForce,
                        ForceMode.Impulse
                    );

                    m_playerMovementAudio?.PlayJumpSound();
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
                if (m_rigidBody == null ||
                    m_rigidBody.linearVelocity.y <= GroundSnapVerticalVelocityLimit)
                {
                    m_intentionalJumpInProgress = false;
                }

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

                    m_playerMovementAudio?.PlayLandingSound();

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

        private void TryApplyGroundAdhesion()
        {
            if (m_rigidBody == null || m_capsuleCollider == null ||
                m_intentionalJumpInProgress || m_jumpInput || !IsMoving ||
                (!m_wasGrounded &&
                 (!m_hasGroundContact || m_airborneTime > m_airborneThreshold)) ||
                m_rigidBody.linearVelocity.y > GroundSnapVerticalVelocityLimit)
            {
                return;
            }

            float scaleY = Mathf.Abs(m_capsuleCollider.transform.lossyScale.y);
            float scaleRadius = Mathf.Max(
                Mathf.Abs(m_capsuleCollider.transform.lossyScale.x),
                Mathf.Abs(m_capsuleCollider.transform.lossyScale.z));
            float radius = m_capsuleCollider.radius * scaleRadius;
            float halfHeight = Mathf.Max(
                m_capsuleCollider.height * scaleY * 0.5f,
                radius);
            Vector3 capsuleCenter = m_capsuleCollider.transform.TransformPoint(
                m_capsuleCollider.center);
            Vector3 lowerHemisphereCenter = capsuleCenter -
                Vector3.up * (halfHeight - radius);
            Vector3 origin = lowerHemisphereCenter +
                Vector3.up * GroundProbeStartOffset;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                m_groundProbeHits,
                GroundProbeStartOffset + MaxGroundSnapDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_groundProbeHits[i];
                if (hit.collider == null ||
                    hit.collider.attachedRigidbody == m_rigidBody ||
                    !IsWalkableGroundNormal(hit.normal))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                }
            }

            if (float.IsPositiveInfinity(closestDistance))
            {
                return;
            }

            float snapDistance = closestDistance - GroundProbeStartOffset;
            if (snapDistance <= 0f || snapDistance > MaxGroundSnapDistance)
            {
                return;
            }

            // Correct only the small vertical gap. Horizontal input and the
            // configured walking/running speed remain unchanged.
            m_rigidBody.position = transform.position + Vector3.down * snapDistance;
            m_isGrounded = true;
            m_hasGroundContact = true;
        }

        private static bool IsWalkableGroundNormal(Vector3 normal)
        {
            return Vector3.Dot(normal, Vector3.up) > GroundNormalThreshold;
        }
    }
}
