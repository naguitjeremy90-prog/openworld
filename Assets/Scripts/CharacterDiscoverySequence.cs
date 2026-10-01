using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public sealed class CharacterDiscoverySequence : MonoBehaviour
{
    private const string TaskId = "main_investigate_pili";
    private const string StageId = "find_anday";
    private const string DiscoveredFlagId = "makamisa_anday_discovered";
    private static readonly int WaveHash = Animator.StringToHash("Wave");

    [Header("Characters")]
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private CharacterReactionController playerReaction;
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody targetRigidbody;

    [Header("Camera Focus")]
    [SerializeField] private CameraFocusManager cameraFocusManager;
    [SerializeField] private CameraFocusPoint cameraFocusPoint;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float facingSpeed = 5f;
    [SerializeField, Min(0.1f)] private float facingTolerance = 1f;
    [SerializeField, Min(0.1f)] private float waveTimeout = 6f;

    private StorySequenceToken storyToken;
    private Coroutine sequenceRoutine;
    private RigidbodyConstraints originalPlayerConstraints;
    private bool constraintsCaptured;
    private bool playerInside;
    private bool running;
    private bool ownsCameraFocus;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || playerInside || running || !CanStartDiscovery())
            return;

        playerInside = true;
        Debug.Log("[AndayDiscoveryTest] Player entered trigger.", this);

        if (!HasReferences())
            return;

        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogWarning("[AndayDiscoveryTest] Could not acquire story sequence token.", this);
            return;
        }

        originalPlayerConstraints = playerRigidbody.constraints;
        constraintsCaptured = true;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.constraints = originalPlayerConstraints |
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezePositionZ;
        playerAnimator.SetFloat("MoveSpeed", 0f);

        running = true;
        Debug.Log("[AndayDiscoveryTest] Sequence started.", this);
        sequenceRoutine = StartCoroutine(RunSequence());
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInside = false;
    }

    private bool IsPlayer(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        return body != null && body == playerRigidbody && body.CompareTag("Player");
    }

    private static bool CanStartDiscovery()
    {
        TaskManager manager = TaskManager.Instance;
        return manager != null &&
            manager.GetTaskState(TaskId) == TaskState.Active &&
            manager.IsCurrentStage(TaskId, StageId) &&
            !SessionStoryState.GetFlag(DiscoveredFlagId);
    }

    private bool HasReferences()
    {
        if (player != null && playerRigidbody != null &&
            playerAnimator != null && playerReaction != null &&
            target != null && targetRigidbody != null &&
            playerRigidbody.transform == player &&
            playerAnimator.transform == player &&
            playerReaction.transform == player &&
            targetRigidbody.transform == target &&
            playerAnimator.runtimeAnimatorController != null &&
            playerAnimator.GetLayerIndex("Animations") >= 0)
            return true;

        Debug.LogWarning("[AndayDiscoveryTest] Missing or mismatched character references or Animations layer.", this);
        return false;
    }

    private IEnumerator RunSequence()
    {
        yield return FaceEachOther();
        Debug.Log("[AndayDiscoveryTest] Characters facing each other.", this);

        Debug.Log("[AndayDiscoveryTest] Playing reaction.", this);
        yield return StartCoroutine(playerReaction.PlayReaction());
        Debug.Log("[AndayDiscoveryTest] Reaction completed.", this);

        if (cameraFocusManager != null && cameraFocusPoint != null)
        {
            Debug.Log("[AndayDiscoveryTest] Starting Anday camera focus.", this);
            bool cameraReturned = false;
            UnityEvent onCameraFinished = new UnityEvent();
            onCameraFinished.AddListener(() => cameraReturned = true);
            if (cameraFocusManager.TryFocusOn(cameraFocusPoint, onCameraFinished))
            {
                ownsCameraFocus = true;
                Debug.Log("[AndayDiscoveryTest] Anday camera focus started.", this);
                while (!cameraReturned && cameraFocusManager != null &&
                       cameraFocusManager.isActiveAndEnabled)
                    yield return null;

                ownsCameraFocus = false;
                if (!cameraReturned || cameraFocusManager == null ||
                    !cameraFocusManager.LastReturnCompleted)
                {
                    Debug.LogWarning("[AndayDiscoveryTest] Camera return was interrupted or could not be verified; skipping Wave.", this);
                    FinishSequence(false);
                    yield break;
                }

                Debug.Log("[AndayDiscoveryTest] Camera fully returned.", this);
            }
            else
                Debug.LogWarning("[AndayDiscoveryTest] Anday camera focus could not start; continuing to Wave.", this);
        }
        else
            Debug.LogWarning("[AndayDiscoveryTest] Missing Anday camera focus references; continuing to Wave.", this);

        int waveLayer = playerAnimator.GetLayerIndex("Animations");
        Debug.Log("[AndayDiscoveryTest] Playing Wave.", this);
        playerAnimator.SetTrigger(WaveHash);

        float deadline = Time.unscaledTime + waveTimeout;
        while (Time.unscaledTime < deadline &&
               playerAnimator.GetCurrentAnimatorStateInfo(waveLayer).shortNameHash != WaveHash)
            yield return null;

        if (playerAnimator.GetCurrentAnimatorStateInfo(waveLayer).shortNameHash != WaveHash)
        {
            playerAnimator.ResetTrigger(WaveHash);
            Debug.LogWarning("[AndayDiscoveryTest] Wave did not enter before timeout.", this);
            FinishSequence(false);
            yield break;
        }

        Debug.Log("[AndayDiscoveryTest] Wave entered.", this);
        while (Time.unscaledTime < deadline &&
               (playerAnimator.GetCurrentAnimatorStateInfo(waveLayer).shortNameHash == WaveHash ||
                playerAnimator.IsInTransition(waveLayer)))
            yield return null;

        if (playerAnimator.GetCurrentAnimatorStateInfo(waveLayer).shortNameHash == WaveHash ||
            playerAnimator.IsInTransition(waveLayer))
        {
            Debug.LogWarning("[AndayDiscoveryTest] Wave did not exit before timeout.", this);
            FinishSequence(false);
            yield break;
        }

        Debug.Log("[AndayDiscoveryTest] Wave completed.", this);
        FinishSequence(true);
    }

    private IEnumerator FaceEachOther()
    {
        WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
        while (true)
        {
            Quaternion playerFacing = FlatFacing(player.position, target.position, player.rotation);
            Quaternion targetFacing = FlatFacing(target.position, player.position, target.rotation);
            bool playerDone = Quaternion.Angle(player.rotation, playerFacing) <= facingTolerance;
            bool targetDone = Quaternion.Angle(target.rotation, targetFacing) <= facingTolerance;
            if (playerDone && targetDone)
            {
                player.rotation = playerFacing;
                targetRigidbody.MoveRotation(targetFacing);
                yield break;
            }

            yield return fixedUpdate;
            float turnFraction = Mathf.Clamp01(Time.fixedDeltaTime * facingSpeed);
            if (!playerDone)
                player.rotation = Quaternion.Slerp(player.rotation, playerFacing, turnFraction);
            if (!targetDone)
                targetRigidbody.MoveRotation(Quaternion.Slerp(
                    target.rotation, targetFacing, turnFraction));
        }
    }

    private static Quaternion FlatFacing(Vector3 from, Vector3 to, Quaternion current)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : current;
    }

    private void FinishSequence(bool completed)
    {
        sequenceRoutine = null;
        Cleanup();
        if (completed)
        {
            SessionStoryState.SetFlag(DiscoveredFlagId, true);
            Debug.Log("[AndayDiscoveryTest] Sequence completed. Gameplay restored.", this);
        }
    }

    private void OnDisable()
    {
        Cleanup();
        playerInside = false;
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (ownsCameraFocus)
        {
            ownsCameraFocus = false;
            if (cameraFocusManager != null && cameraFocusPoint != null)
                cameraFocusManager.TryReturnToNormal(cameraFocusPoint);
        }

        if (constraintsCaptured)
        {
            if (playerRigidbody != null)
                playerRigidbody.constraints = originalPlayerConstraints;
            constraintsCaptured = false;
        }

        storyToken?.Release();
        storyToken = null;
        running = false;
    }
}
