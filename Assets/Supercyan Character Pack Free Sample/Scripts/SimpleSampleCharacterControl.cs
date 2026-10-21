using System.Collections.Generic;
using UnityEngine;

namespace Supercyan.FreeSample
{
    public class SimpleSampleCharacterControl : MonoBehaviour
    {
        /// <summary>Raised after a grounded jump passes the cooldown check and its impulse is applied.</summary>
        public event System.Action JumpStarted;

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

        // Opt-in only. Ordinary gameplay, spawners and older cinematics never enter this contract.
        public enum ControlledRelocationResult { Rejected, Waiting, Ready, Failed }
        private MonoBehaviour relocationOwner;
        private int relocationVersion, relocationSteps;
        private int relocationContactStep, relocationConsecutiveContacts, relocationPhysicsEpoch;
        private bool relocationActive, relocationFreshContact, relocationRolledBack;
        private Vector3 relocationOrigin, relocationTarget;
        private Quaternion relocationRotation;
        private Collider relocationSupport, relocationOriginTrigger;
        private RaycastHit[] relocationHits;
        private Collider[] relocationOverlaps;
        private List<AnimatorClipInfo> relocationClipInfo;
        private const int RelocationStepBudget = 8;

        public bool OwnsControlledRelocation(MonoBehaviour owner, int operation) =>
            relocationActive && relocationOwner == owner && owner != null && operation == relocationVersion;

        public bool HasGroundedIdlePresentation => IsGrounded && ControlledRelocationIdle();

        // Read-only Chapter 12 entry observations. No input, physics or Animator writes.
        public bool HasPendingControlledIntent => m_jumpInput || IsMoving || IsRunning ||
            m_currentV != 0f || m_currentH != 0f || m_currentDirection.sqrMagnitude > 0f;

        public string DescribeControlledRelocationEntry()
        {
            string observation = $"grounded={IsGrounded} (contact={m_isGrounded}, count={m_collisions.Count}, airborneAnimation={m_isAirborneAnimating}); " +
                $"verticalVelocity={(m_rigidBody != null ? m_rigidBody.linearVelocity.y : float.NaN):F4} (limit +/-0.1); " +
                $"intent={HasPendingControlledIntent} (jump={m_jumpInput}, moving={IsMoving}, running={IsRunning}, axes={m_currentH:F4}/{m_currentV:F4}, direction={m_currentDirection}); ";
            if (m_animator == null) return observation + "Animator missing.";
            observation += $"Animator enabled={m_animator.isActiveAndEnabled}, initialized={m_animator.isInitialized}, rootMotion={m_animator.applyRootMotion}, layers={m_animator.layerCount}; ";
            if (!m_animator.isInitialized || m_animator.layerCount != 2) return observation;
            bool hasGrounded = false, hasMoveSpeed = false;
            foreach (AnimatorControllerParameter parameter in m_animator.parameters)
            {
                hasGrounded |= parameter.name == "Grounded" && parameter.type == AnimatorControllerParameterType.Bool;
                hasMoveSpeed |= parameter.name == "MoveSpeed" && parameter.type == AnimatorControllerParameterType.Float;
            }
            observation += $"transitions={m_animator.IsInTransition(0)}/{m_animator.IsInTransition(1)}; " +
                $"baseMovement={m_animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash("Base Layer.Movement")} (hash={m_animator.GetCurrentAnimatorStateInfo(0).fullPathHash}); " +
                $"overlayEmpty={m_animator.GetCurrentAnimatorStateInfo(1).shortNameHash == Animator.StringToHash("Empty")} (hash={m_animator.GetCurrentAnimatorStateInfo(1).shortNameHash}); " +
                $"GroundedParameter={(hasGrounded ? m_animator.GetBool("Grounded").ToString() : "missing")}; " +
                $"MoveSpeed={(hasMoveSpeed ? m_animator.GetFloat("MoveSpeed").ToString("F6") : "missing")} (abs <0.001); ";
            var clips = new List<AnimatorClipInfo>();
            m_animator.GetCurrentAnimatorClipInfo(0, clips);
            float idleWeight = 0f;
            var weights = new List<string>();
            foreach (AnimatorClipInfo clip in clips)
            {
                if (clip.weight <= 0.0001f) continue;
                weights.Add($"{(clip.clip != null ? clip.clip.name : "missing")}:{clip.weight:F6}");
                if (clip.clip != null && clip.clip.name == "idle") idleWeight += clip.weight;
            }
            return observation + $"idleWeight={idleWeight:F6} (>=0.9999, no other weighted clip); clips=[{string.Join(",", weights)}].";
        }

        public bool TryBeginControlledRelocation(MonoBehaviour owner, Vector3 destination,
            Collider allowedOriginTrigger, out int operation, out string reason)
        {
            operation = 0;
            reason = null;
            if (!Application.isPlaying || !isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled ||
                owner.gameObject.scene != gameObject.scene || relocationActive ||
                !StorySequenceCoordinator.IsStorySequenceActive || Physics.simulationMode != SimulationMode.FixedUpdate ||
                m_rigidBody == null || m_rigidBody.isKinematic || !m_rigidBody.useGravity ||
                m_capsuleCollider == null || m_capsuleCollider.transform != transform || !m_capsuleCollider.enabled ||
                m_capsuleCollider.direction != 1 || m_capsuleCollider.isTrigger)
            {
                reason = "Controlled relocation requires exclusive cinematic ownership and the enabled dynamic upright player capsule with normal fixed-step physics.";
                return false;
            }
            if (!IsGrounded || !ControlledRelocationIdle() || Mathf.Abs(m_rigidBody.linearVelocity.y) > 0.1f)
            {
                reason = "Miguel must already be grounded Idle, without an Animator transition or competing overlay animation.";
                return false;
            }
            if (allowedOriginTrigger != null && allowedOriginTrigger.gameObject.scene != gameObject.scene)
            { reason = "Rollback trigger reference belongs to another scene."; return false; }
            if (!TryValidateControlledPose(m_rigidBody.position, m_rigidBody.rotation, false,
                allowedOriginTrigger, out _, out _, out reason)) return false;
            if (!TryValidateControlledPose(destination, m_rigidBody.rotation, true, null,
                out Vector3 grounded, out Collider support, out reason)) return false;

            relocationOrigin = m_rigidBody.position;
            relocationRotation = m_rigidBody.rotation;
            relocationOriginTrigger = allowedOriginTrigger;
            relocationOwner = owner;
            relocationRolledBack = false;
            relocationActive = true;
            PlaceControlledRelocation(grounded, support);
            operation = relocationVersion;
            return true;
        }

        public ControlledRelocationResult ConfirmControlledRelocation(MonoBehaviour owner, int operation, out string reason)
        {
            reason = null;
            if (!OwnsControlledRelocation(owner, operation))
            { reason = "Stale or foreign controlled-relocation operation."; return ControlledRelocationResult.Rejected; }
            if (m_rigidBody == null || m_capsuleCollider == null || !m_capsuleCollider.enabled ||
                m_rigidBody.isKinematic || !m_rigidBody.useGravity || Physics.simulationMode != SimulationMode.FixedUpdate)
            { reason = "The controlled player's Rigidbody/capsule or fixed-step physics configuration was removed or changed."; return ControlledRelocationResult.Failed; }
            if (relocationFreshContact && Vector3.Distance(m_rigidBody.position, relocationTarget) < 0.05f &&
                Mathf.Abs(m_rigidBody.linearVelocity.y) <= 0.1f && ControlledRelocationIdle() &&
                TryValidateControlledPose(m_rigidBody.position, relocationRotation, false,
                    relocationRolledBack ? relocationOriginTrigger : null, out _, out Collider support, out reason) &&
                support == relocationSupport)
            {
                SynchronizeControlledGround(support);
                m_animator.SetBool("Grounded", true);
                m_animator.SetFloat("MoveSpeed", 0f);
                return ControlledRelocationResult.Ready; // Retain ownership until explicit cancellation/handoff.
            }
            if (relocationSteps < RelocationStepBudget) return ControlledRelocationResult.Waiting;
            reason = reason ?? "Fresh destination support and grounded Idle were not confirmed within eight physics evaluations.";
            return ControlledRelocationResult.Failed;
        }

        public bool CancelControlledRelocation(MonoBehaviour owner, int operation, out string reason)
        {
            if (ConfirmControlledRelocation(owner, operation, out reason) != ControlledRelocationResult.Ready)
                return false; // Never publish unsafe gameplay control after a failed placement.
            relocationActive = false;
            relocationOwner = null;
            relocationSupport = relocationOriginTrigger = null;
            ++relocationVersion;
            return true;
        }

        public bool TryRollbackControlledRelocation(MonoBehaviour owner, int operation,
            out int rollbackOperation, out string reason)
        {
            rollbackOperation = operation;
            reason = null;
            if (!OwnsControlledRelocation(owner, operation) || relocationRolledBack)
            { reason = "Rollback rejected: stale ownership or the single automatic rollback was already used."; return false; }
            return RetryControlledOrigin(out rollbackOperation, out reason);
        }

#if UNITY_EDITOR
        // Only the explicit scene-local development action calls this; never an automatic retry loop.
        public bool RetryControlledOriginForDevelopment(MonoBehaviour owner, int operation,
            out int retryOperation, out string reason)
        {
            retryOperation = operation;
            if (!Application.isPlaying || !OwnsControlledRelocation(owner, operation))
            { reason = "Development origin recovery requires the current owner and operation in Play Mode."; return false; }
            return RetryControlledOrigin(out retryOperation, out reason);
        }
#endif

        private bool RetryControlledOrigin(out int operation, out string reason)
        {
            operation = relocationVersion;
            if (m_rigidBody == null || m_capsuleCollider == null || m_animator == null)
            { reason = "Restore the controller's original Rigidbody, capsule and Animator references before retrying the captured origin."; return false; }
            if (!TryValidateControlledPose(relocationOrigin, relocationRotation, false, relocationOriginTrigger,
                out Vector3 grounded, out Collider support, out reason)) return false;
            relocationRolledBack = true;
            PlaceControlledRelocation(grounded, support);
            operation = relocationVersion;
            return true;
        }

        private void ClearControlledIntent()
        {
            m_jumpInput = false;
            m_currentV = m_currentH = 0f;
            m_currentDirection = Vector3.zero;
            IsMoving = IsRunning = false;
        }

        private void PlaceControlledRelocation(Vector3 destination, Collider support)
        {
            ++relocationVersion;
            relocationSteps = 0;
            relocationContactStep = relocationConsecutiveContacts = relocationPhysicsEpoch = 0;
            relocationFreshContact = false;
            relocationTarget = destination;
            relocationSupport = support;
            ClearControlledIntent();
            m_collisions.Clear();
            m_isGrounded = m_hasGroundContact = m_wasGrounded = false;
            m_airborneTime = 0f;
            m_isAirborneAnimating = m_intentionalJumpInProgress = false;
            m_wasStorySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
            // Keep the previously validated Idle presentation while contacts are rebuilt.
            // Never write transient Grounded=false, fire triggers, Rebind, or change physics settings.
            m_animator.SetFloat("MoveSpeed", 0f);
            m_rigidBody.position = destination;
            m_rigidBody.rotation = relocationRotation;
            // Match the existing Chapter 10 placement pattern so the interpolated
            // render Transform and the physics teleport share this same pose immediately.
            transform.SetPositionAndRotation(destination, relocationRotation);
            m_rigidBody.linearVelocity = Vector3.zero;
            m_rigidBody.angularVelocity = Vector3.zero;
            m_rigidBody.WakeUp();
            Physics.SyncTransforms();
        }

        private void SynchronizeControlledGround(Collider support)
        {
            ClearControlledIntent();
            m_collisions.Clear();
            m_collisions.Add(support);
            m_isGrounded = m_hasGroundContact = m_wasGrounded = true;
            m_airborneTime = 0f;
            m_isAirborneAnimating = m_intentionalJumpInProgress = false;
            m_wasStorySequenceActive = StorySequenceCoordinator.IsStorySequenceActive;
        }

        private bool ControlledRelocationIdle()
        {
            if (m_animator == null || !m_animator.isActiveAndEnabled || m_animator.applyRootMotion ||
                m_animator.layerCount != 2 || m_animator.IsInTransition(0) || m_animator.IsInTransition(1) ||
                m_animator.GetCurrentAnimatorStateInfo(0).fullPathHash != Animator.StringToHash("Base Layer.Movement") ||
                m_animator.GetCurrentAnimatorStateInfo(1).shortNameHash != Animator.StringToHash("Empty")) return false;
            if (!m_animator.GetBool("Grounded") || Mathf.Abs(m_animator.GetFloat("MoveSpeed")) >= 0.001f) return false;
            if (relocationClipInfo == null) relocationClipInfo = new List<AnimatorClipInfo>(5);
            relocationClipInfo.Clear();
            m_animator.GetCurrentAnimatorClipInfo(0, relocationClipInfo);
            float idleWeight = 0f;
            foreach (AnimatorClipInfo clip in relocationClipInfo)
            {
                if (clip.weight <= 0.0001f) continue;
                if (clip.clip == null || clip.clip.name != "idle") return false;
                idleWeight += clip.weight;
            }
            return idleWeight >= 0.9999f;
        }

        public bool TryValidateControlledPose(Vector3 candidate, Quaternion rotation, bool alignToGround,
            Collider permittedTrigger, out Vector3 grounded, out Collider support, out string reason)
        {
            grounded = candidate;
            support = null;
            reason = null;
            CapsuleCollider capsule = m_capsuleCollider != null ? m_capsuleCollider : GetComponent<CapsuleCollider>();
            Rigidbody body = m_rigidBody != null ? m_rigidBody : GetComponent<Rigidbody>();
            Vector3 scale = transform.lossyScale;
            if (capsule == null || body == null || capsule.direction != 1 || scale.x <= 0f ||
                Mathf.Abs(scale.x - scale.y) > 0.001f || Mathf.Abs(scale.x - scale.z) > 0.001f ||
                Vector3.Dot(rotation * Vector3.up, Vector3.up) < 0.999f ||
                !float.IsFinite(candidate.x) || !float.IsFinite(candidate.y) || !float.IsFinite(candidate.z))
            { reason = "Controlled pose requires a finite position and the audited upright uniformly scaled capsule."; return false; }
            if (relocationHits == null || relocationHits.Length != 32) relocationHits = new RaycastHit[32];
            if (relocationOverlaps == null || relocationOverlaps.Length != 64) relocationOverlaps = new Collider[64];
            int count = Physics.RaycastNonAlloc(candidate + Vector3.up * 0.5f, Vector3.down,
                relocationHits, 1f, ~0, QueryTriggerInteraction.Ignore);
            if (count == relocationHits.Length) { reason = "Ground query capacity exceeded."; return false; }
            RaycastHit best = default;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; ++i)
                if (relocationHits[i].collider.attachedRigidbody != body && relocationHits[i].distance < distance)
                { best = relocationHits[i]; distance = best.distance; }
            if (best.collider == null || best.normal.y < 0.95f || best.collider.attachedRigidbody != null)
            { reason = "No static walkable support was found at the requested pose."; return false; }
            float radius = capsule.radius * scale.x;
            float half = Mathf.Max(capsule.height * scale.y * 0.5f, radius);
            Vector3 offset = rotation * Vector3.Scale(capsule.center, scale);
            float rootY = best.point.y + half - offset.y;
            if (!alignToGround && Mathf.Abs(candidate.y - rootY) > 0.04f)
            { reason = "Capsule feet are not in ground-contact range."; return false; }
            if (alignToGround) grounded.y = rootY;
            if (alignToGround)
            {
                // Validate the support beneath the footprint, not only its center ray.
                for (int side = 0; side < 4; ++side)
                {
                    Vector3 edge = new Vector3(candidate.x + (side == 0 ? radius : side == 1 ? -radius : 0f),
                        best.point.y + 0.3f, candidate.z + (side == 2 ? radius : side == 3 ? -radius : 0f));
                    int edgeCount = Physics.RaycastNonAlloc(edge, Vector3.down, relocationHits, 0.6f,
                        ~0, QueryTriggerInteraction.Ignore);
                    bool supported = false;
                    if (edgeCount == relocationHits.Length) { reason = "Footprint support query capacity exceeded."; return false; }
                    for (int i = 0; i < edgeCount; ++i)
                        if (relocationHits[i].collider == best.collider && relocationHits[i].normal.y >= 0.95f &&
                            Mathf.Abs(relocationHits[i].point.y - best.point.y) < 0.025f) supported = true;
                    if (!supported) { reason = "The full capsule footprint lacks continuous flat support."; return false; }
                }
            }
            Vector3 center = grounded + offset;
            int overlaps = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * (half - radius),
                center + Vector3.up * (half - radius), radius, relocationOverlaps, ~0, QueryTriggerInteraction.Collide);
            if (overlaps == relocationOverlaps.Length) { reason = "Capsule clearance query capacity exceeded."; return false; }
            for (int i = 0; i < overlaps; ++i)
            {
                Collider other = relocationOverlaps[i];
                if (other.attachedRigidbody == body || other == permittedTrigger) continue;
                if (other == best.collider && (!Physics.ComputePenetration(capsule, grounded, rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) || depth <= 0.02f)) continue;
                reason = "Controlled pose overlaps collider/trigger: " + other.name;
                return false;
            }
            support = best.collider;
            return true;
        }

        private void ObserveControlledSupport(Collision collision)
        {
            if (!relocationActive || relocationSteps == 0 || m_capsuleCollider == null ||
                m_rigidBody == null || collision.collider != relocationSupport) return;
            Vector3 feet = m_capsuleCollider.bounds.center - Vector3.up * m_capsuleCollider.bounds.extents.y;
            for (int i = 0; i < collision.contactCount; ++i)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 horizontal = contact.point - feet; horizontal.y = 0f;
                if (contact.normal.y >= 0.95f && horizontal.magnitude <= m_capsuleCollider.bounds.extents.x + 0.1f &&
                    Mathf.Abs(contact.point.y - feet.y) <= 0.04f &&
                    Vector3.Distance(m_rigidBody.position, relocationTarget) < 0.05f)
                {
                    if (relocationContactStep != relocationPhysicsEpoch)
                    {
                        relocationConsecutiveContacts = relocationContactStep == relocationPhysicsEpoch - 1 ?
                            Mathf.Min(2, relocationConsecutiveContacts + 1) : 1;
                        relocationContactStep = relocationPhysicsEpoch;
                    }
                    relocationFreshContact = relocationConsecutiveContacts >= 2;
                }
            }
        }

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
            ObserveControlledSupport(collision);
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
            ObserveControlledSupport(collision);
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
            if (relocationActive) { ClearControlledIntent(); return; }
            if (StorySequenceCoordinator.IsStorySequenceActive)
            {
                m_jumpInput = false;
                return;
            }

            if (ReconstructionJournalManager.IsPagninilayResponseInputFocusedForGameplay)
            {
                m_jumpInput = false;
                return;
            }

            if (InventoryDocumentViewer.IsAnyOpen)
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
            if (relocationActive)
            {
                ClearControlledIntent();
                ++relocationPhysicsEpoch; // Passive contact observation also permits an explicit late safe cancel.
                if (relocationSteps < RelocationStepBudget) ++relocationSteps;
                return; // Normal Story Sequence Grounded writes also stay out of this opt-in operation.
            }
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

            if (InventoryDocumentViewer.IsAnyOpen)
            {
                m_currentV = 0f;
                m_currentH = 0f;
                m_currentDirection = Vector3.zero;
                m_jumpInput = false;
                IsMoving = false;
                IsRunning = false;
                if (m_animator)
                {
                    m_animator.SetFloat("MoveSpeed", 0f);
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
            bool suppressInput =
                InventoryDocumentViewer.IsAnyOpen ||
                ReconstructionJournalManager.IsPagninilayResponseInputFocusedForGameplay;
            float v = suppressInput ? 0f : Input.GetAxis("Vertical");
            float h = suppressInput ? 0f : Input.GetAxis("Horizontal");

            if (m_lockVerticalMovement)
            {
                v = 0;
            }

            bool walk = !suppressInput && Input.GetKey(KeyCode.LeftShift);

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

            m_currentV = suppressInput ? 0f : Mathf.Lerp(
                m_currentV, v, Time.deltaTime * m_interpolation);
            m_currentH = suppressInput ? 0f : Mathf.Lerp(
                m_currentH, h, Time.deltaTime * m_interpolation);
            if (suppressInput)
                m_currentDirection = Vector3.zero;

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
            bool suppressInput =
                InventoryDocumentViewer.IsAnyOpen ||
                ReconstructionJournalManager.IsPagninilayResponseInputFocusedForGameplay;
            float v = suppressInput ? 0f : Input.GetAxis("Vertical");
            float h = suppressInput ? 0f : Input.GetAxis("Horizontal");

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
            if (suppressInput || !Input.GetKey(KeyCode.LeftShift))
            {
                v *= m_walkScale;
                h *= m_walkScale;
            }

            m_currentV = suppressInput ? 0f : Mathf.Lerp(
                m_currentV, v, Time.deltaTime * m_interpolation);
            m_currentH = suppressInput ? 0f : Mathf.Lerp(
                m_currentH, h, Time.deltaTime * m_interpolation);
            if (suppressInput)
                m_currentDirection = Vector3.zero;

            Vector3 direction =
                camera.forward * m_currentV +
                camera.right * m_currentH;

            float directionLength = direction.magnitude;

            IsMoving = directionLength >= LocomotionStateMovementThreshold;
            IsRunning = !suppressInput && IsMoving && Input.GetKey(KeyCode.LeftShift);

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
                    NotifyJumpStarted();
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

        private void NotifyJumpStarted()
        {
            System.Action listeners = JumpStarted;
            if (listeners == null)
                return;

            foreach (System.Action listener in listeners.GetInvocationList())
            {
                try { listener(); }
                catch (System.Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
