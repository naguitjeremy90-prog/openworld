using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>One exploration trigger and the Chapter 10 reaction/task/chase handoff.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class NewMakamisaPart3Chapter10 : MonoBehaviour
{
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string InvestigateStage = "part3_investigate_quiet_town";
    internal const string RunStage = "part3_run_to_church";
    private const string ClaimFlag = "alaala_part3_ch10_owner_active";
    internal const string DialogueFlag = "alaala_part3_ch10_dialogue_complete";
    internal const string NotificationFlag = "alaala_part3_ch10_task_presented";
    internal const string ChaseFlag = "alaala_part3_ch10_chase_started";

    [Header("Existing Part 3 scene references")]
    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private FadeController incomingFade;
    [SerializeField] private Transform player;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private NewMakamisaPart3SimpleWhiteVoid whiteWall;
    [SerializeField] private NewMakamisaPart3ChaseMusic chaseMusic;

    [Header("Existing reusable focus setup — never repositioned")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CinemachineBrain cameraBrain;

    [Header("Chapter 10 definitions")]
    [SerializeField, Tooltip("Main MeshRenderer on DRI SA/Houses/poorhouse2. Its world bounds define the white wall's halfway stop.")]
    private MeshRenderer halfwayHouseRenderer;
    [SerializeField, Tooltip("Miguel's existing three-mark reaction controller. No new animation or visual is created.")]
    private CharacterReactionController playerReaction;
    [SerializeField, Min(1f), Tooltip("Degrees per second only for the Chapter 10 look-back after the reaction. Does not affect normal gameplay turning.")]
    private float chapter10LookBackTurnSpeed = 80f;
    [SerializeField] private NPCConversation reactionConversation;
    [SerializeField] private Canvas chaseNavigationCanvas;
    [SerializeField] private ObjectiveNavigationAdapter churchNavigation;
    [SerializeField, Tooltip("Scene-local limit before the church's western bounds. Only guards the story chase until Chapter 11 takes ownership.")]
    private Transform finalEncounterBoundaryLimit;

    [Header("Non-blocking chase warning")]
    [SerializeField, Tooltip("Dedicated scene-local instance of the existing timed text presenter. Never invokes ConversationManager.")]
    private IntroNarrationManager chaseWarning;
    [Header("Chapter 10 gameplay recovery only")]
    [SerializeField, Min(0.1f), Tooltip("World-X distance over which the story chase wall smoothly slows. Does not stop it or change Chapter 11.")]
    private float chaseSlowdownDistance = 8f;
    [SerializeField, Min(0.001f), Tooltip("Positive world units/second near Miguel, only during the Chapter 10 gameplay chase.")]
    private float chaseNearPlayerWallSpeed = 0.05f;
    [SerializeField] private CapsuleCollider recoveryPlayerCapsule;
    [SerializeField, Tooltip("CanvasGroup already owned by incomingFade. Reuses its black overlay, never the white transport.")]
    private CanvasGroup recoveryFadeGroup;
    [SerializeField, Tooltip("Existing Chapter 11 encounter trigger, used only to exclude recovery destinations.")]
    private BoxCollider recoveryEncounterExclusion;
    [SerializeField, Tooltip("Audited terrain, bridge and road pavement colliders. Never accept a roof, water or NPC as recovery ground.")]
    private Collider[] recoveryGroundColliders;
    [SerializeField, Min(10f)] private float recoveryWallSeparation = 10f;
    [SerializeField, Min(0.01f)] private float recoveryFadeToBlackDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float recoveryFadeFromBlackDuration = 0.5f;
    [SerializeField, Tooltip("Verified church approach road center in world Z; candidates stay within two units of it.")]
    private float recoveryRouteZ = 674.11f;
    private const float RecoveryRouteStartX = 200f;
    private const float RecoveryMaxAdvance = 24f;
    private const float RecoveryClearance = 0.1f;
    private readonly RaycastHit[] recoveryGroundHits = new RaycastHit[32];
    private readonly Collider[] recoveryOverlapHits = new Collider[64];
    private bool recovering, recoveryFailed, recoveryBodyCaptured, recoveryWasKinematic;
    private bool recoveryFadeOwned;
    private float recoveryOriginalAlpha;
    private Coroutine recoveryRoutine;
    private IEnumerator recoveryRunner;
    private bool recoveryDependencyReported;
    [SerializeField, Min(0.1f), Tooltip("Warn once when entering this world-X distance ahead of the wall, unless actively travelling toward the church.")]
    private float chaseWarningDistance = 12f;
    [SerializeField, Min(0f), Tooltip("Minimum gameplay seconds between fresh warning approach events.")]
    private float chaseWarningCooldown = 8f;
    private bool chaseSafetySuspended;
    private bool warningPositionCaptured;
    private bool warningLatched;
    private bool warningDependencyReported;
    private Vector3 previousWarningPosition;
    private float previousWarningFixedTime;
    private float sampledWarningVelocityX;
    private float nextWarningTime;

    private MonoBehaviour encounterOwner;
    private bool savedNavigationActive;

    public bool TryBeginFinalEncounter(MonoBehaviour owner)
    {
        var manager = TaskManager.Instance;
        if (!isActiveAndEnabled || !Qualified || running || recovering || !ownsWall || encounterOwner != null ||
            !SessionStoryState.GetFlag(ChaseFlag) || !SessionStoryState.GetFlag(NotificationFlag) ||
            manager == null || !manager.IsCurrentStage(TaskId, RunStage) ||
            whiteWall == null || whiteWall.IsWhiteCameraActive ||
            !whiteWall.TryTransferHeld(this, owner))
            return false;
        encounterOwner = owner;
        ownsWall = false;
        StopChaseWarning();
        warningPositionCaptured = warningLatched = chaseSafetySuspended = false;
        savedNavigationActive = chaseNavigationCanvas.gameObject.activeSelf;
        chaseNavigationCanvas.gameObject.SetActive(false); // Adapter unregisters its own marker.
        return true;
    }

    public bool TryReturnFromFinalEncounter(MonoBehaviour owner)
    {
        var manager = TaskManager.Instance;
        if (owner == null || encounterOwner != owner || !isActiveAndEnabled || !Qualified ||
            manager == null || !manager.IsCurrentStage(TaskId, RunStage) ||
            whiteWall == null || whiteWall.IsWhiteCameraActive || finalEncounterBoundaryLimit == null ||
            !whiteWall.TryTransferHeld(owner, this))
            return false;
        ownsWall = true;
        encounterOwner = null;
        if (!ConfigureChaseGuard() ||
            !whiteWall.TryResume(this))
            return false;
        chaseNavigationCanvas.gameObject.SetActive(savedNavigationActive);
        return true;
    }

    public void FinishFinalEncounter(MonoBehaviour owner)
    {
        if (owner == null || encounterOwner != owner) return;
        encounterOwner = null;
        if (chaseNavigationCanvas != null) chaseNavigationCanvas.gameObject.SetActive(false);
    }

    private TaskManager tasks;
    private StorySequenceToken storyToken;
    private bool running;
    private bool ownsClaim;
    private bool ownsMovement;
    private bool ownsWall;
    private bool ownsFocus;
    private bool cameraReturned;
    private bool waitingForDialogue;
    private bool dialogueClosed;
    private bool dialogueSucceeded;
    private int generation;
    private Quaternion lookRotation;
    private PrioritySettings savedNormalPriority, savedFocusPriority;
    private Vector3 savedFocusPosition;
    private Quaternion savedFocusRotation;

    private bool Qualified => Application.isPlaying && presentation != null &&
        presentation.isActiveAndEnabled && presentation.gameObject.scene == gameObject.scene &&
        presentation.IsPart3PresentationReady &&
        gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity" &&
        SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.ArrivalDialogueFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.FindNotificationFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.EmptyTownDialogueFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.InvestigateNotificationFlag);

    private void OnEnable()
    {
        SessionStoryState.FlagChanged += HandleSessionFlag;
        RestoreChaseIndicator();
    }

    private void Start() => RestoreChaseIndicator();

    private bool ChaseGameplayReady => Qualified && !running && ownsWall && encounterOwner == null &&
        SessionStoryState.GetFlag(DialogueFlag) && SessionStoryState.GetFlag(NotificationFlag) &&
        SessionStoryState.GetFlag(ChaseFlag) &&
        !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.WhitePresentationFlag) &&
        !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.CompletedFlag) &&
        tasks != null && tasks == TaskManager.Instance && tasks.GetTaskState(TaskId) == TaskState.Active &&
        tasks.IsCurrentStage(TaskId, RunStage) && !tasks.HasPendingNotifications &&
        incomingFade != null && incomingFade.IncomingFadeCompleted &&
        string.IsNullOrEmpty(SpawnData.spawnPointName) &&
        !StorySequenceCoordinator.IsStorySequenceActive &&
        ConversationManager.Instance != null && !ConversationManager.Instance.IsConversationActive &&
        focusManager != null && !focusManager.IsFocusing &&
        player != null && playerMovement != null && playerMovement.isActiveAndEnabled;

    private void Update()
    {
        if (recovering)
        {
            // Reset, ownership loss, scene exit or a changed objective cancels only our transition.
            if (!RecoverySessionValid()) CancelRecovery();
            return;
        }
        // No searches, input-key assumptions or movement/camera/Animator writes.
        if (!Application.isPlaying || running || !ownsWall || whiteWall == null ||
            !whiteWall.IsControlledBy(this))
        {
            StopChaseWarning();
            warningPositionCaptured = warningLatched = chaseSafetySuspended = false;
            return;
        }
        if (!ChaseGameplayReady)
        {
            StopChaseWarning();
            warningPositionCaptured = warningLatched = false;
            if (!chaseSafetySuspended)
                chaseSafetySuspended = whiteWall.TrySuspendEncounterGuard(this);
            return;
        }
        if (chaseSafetySuspended)
        {
            if (finalEncounterBoundaryLimit == null ||
                !ConfigureChaseGuard() ||
                !whiteWall.TryResume(this))
                return; // Remain safely held; never release someone else's control.
            chaseSafetySuspended = false;
        }
        if (Time.timeScale <= 0f)
        {
            StopChaseWarning();
            warningPositionCaptured = false;
            return;
        }
        // Use the real capsule's western extent, not its root or a keyboard key.
        if (recoveryPlayerCapsule != null &&
            recoveryPlayerCapsule.bounds.min.x <= whiteWall.BoundaryX + RecoveryClearance)
        {
            BeginChaseRecovery();
            return;
        }
        if (chaseWarning == null)
        {
            if (!warningDependencyReported)
            {
                Debug.LogError("Part 3 Chapter 10 chase warning requires its scene-local IntroNarrationManager reference. Existing wall protection remains active.", this);
                warningDependencyReported = true;
            }
            return;
        }
        Vector3 position = player.position;
        if (!warningPositionCaptured)
        {
            previousWarningPosition = position;
            previousWarningFixedTime = Time.fixedTime;
            sampledWarningVelocityX = 0f;
            warningPositionCaptured = true;
            return;
        }
        if (Time.fixedTime > previousWarningFixedTime)
        {
            sampledWarningVelocityX = (position.x - previousWarningPosition.x) /
                (Time.fixedTime - previousWarningFixedTime);
            previousWarningPosition = position;
            previousWarningFixedTime = Time.fixedTime;
        }
        float velocityX = playerMovement.IsMoving ? sampledWarningVelocityX : 0f;
        float gap = position.x - whiteWall.BoundaryX;
        bool movingTowardWall = playerMovement.IsMoving && velocityX < -0.1f;
        bool movingTowardChurch = playerMovement.IsMoving && velocityX > 0.1f;
        bool near = gap <= chaseWarningDistance;
        if (!near && !movingTowardWall && gap > chaseWarningDistance + 3f)
            warningLatched = false;
        if (chaseWarning.gameObject.activeSelf && !chaseWarning.IsPlaying)
            StopChaseWarning();
        if (!movingTowardChurch && (movingTowardWall || near) && !warningLatched)
        {
            warningLatched = true; // Consume the entry even during cooldown; never queue spam.
            if (Time.time < nextWarningTime || chaseWarning.IsPlaying) return;
            chaseWarning.gameObject.SetActive(true);
            chaseWarning.PlayNarration();
            if (!chaseWarning.IsPlaying)
            {
                StopChaseWarning();
                Debug.LogError("Part 3 Chapter 10 chase warning could not start its existing timed text presenter; check its TMP text and CanvasGroup.", this);
                return;
            }
            nextWarningTime = Time.time + chaseWarningCooldown;
        }
    }

    private bool ConfigureChaseGuard() => finalEncounterBoundaryLimit != null &&
        whiteWall.TryGuardEncounter(this, finalEncounterBoundaryLimit.position.x,
            chaseSlowdownDistance, chaseNearPlayerWallSpeed, recoveryPlayerCapsule);

    private bool RecoverySessionValid() => isActiveAndEnabled && Qualified && !running && ownsWall &&
        encounterOwner == null && whiteWall != null && whiteWall.IsControlledBy(this) &&
        !whiteWall.IsWhiteCameraActive && !whiteWall.HasCrossedPlayer &&
        SessionStoryState.GetFlag(DialogueFlag) && SessionStoryState.GetFlag(NotificationFlag) &&
        SessionStoryState.GetFlag(ChaseFlag) &&
        !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.WhitePresentationFlag) &&
        !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.CompletedFlag) &&
        tasks != null && tasks == TaskManager.Instance && tasks.GetTaskState(TaskId) == TaskState.Active &&
        tasks.IsCurrentStage(TaskId, RunStage);

    private void CheckRecovery()
    {
        Require(recovering && RecoverySessionValid() && whiteWall.IsHeldBy(this),
            "Recovery lost its qualified chase, task or wall hold.");
        Require(incomingFade != null && incomingFade.isActiveAndEnabled &&
            recoveryFadeGroup != null && recoveryFadeGroup.gameObject.activeInHierarchy &&
            playerBody != null && recoveryPlayerCapsule != null && recoveryPlayerCapsule.enabled &&
            movementLock != null && ownsMovement && !playerMovement.enabled &&
            !StorySequenceCoordinator.IsStorySequenceActive &&
            ConversationManager.Instance != null && !ConversationManager.Instance.IsConversationActive &&
            focusManager != null && !focusManager.IsFocusing && !tasks.HasPendingNotifications,
            "Recovery fade, capsule or input ownership was interrupted by another presentation.");
    }

    private void BeginChaseRecovery()
    {
        if (recovering || !ChaseGameplayReady) return;
        if (recoveryFadeGroup == null || recoveryFadeGroup.alpha > 0.001f ||
            recoveryEncounterExclusion == null || recoveryGroundColliders == null ||
            recoveryGroundColliders.Length == 0 || recoveryPlayerCapsule == null ||
            recoveryPlayerCapsule.transform != player || recoveryPlayerCapsule.direction != 1 ||
            recoveryWallSeparation < 10f || !NormalCameraReady())
        {
            // No guessed destination, fade or lock if the authored prerequisites are missing.
            whiteWall.TryHold(this);
            if (!recoveryDependencyReported)
                Debug.LogError("Part 3 Chapter 10 recovery cannot start: check the existing black fade group, upright player capsule, audited ground references, encounter exclusion and normal camera. Wall held; no teleport or task change occurred.", this);
            recoveryDependencyReported = true;
            return;
        }
        if (!whiteWall.TryHold(this)) return;
        recovering = true;
        recoveryFailed = false;
        StopChaseWarning();
        warningPositionCaptured = warningLatched = false;
        movementLock.HoldMovementForStorySequence();
        ownsMovement = true;
        recoveryWasKinematic = playerBody.isKinematic;
        recoveryBodyCaptured = true;
        if (!playerBody.isKinematic)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }
        playerBody.isKinematic = true;
        recoveryOriginalAlpha = recoveryFadeGroup.alpha;
        recoveryFadeOwned = true;
        recoveryRunner = RunRecoverySafely(RecoverChase());
        recoveryRoutine = StartCoroutine(recoveryRunner);
    }

    // Flatten just our fade iterators so disposal and failures cannot strand a partial fade.
    private IEnumerator RunRecoverySafely(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        bool completed = false;
        try
        {
            while (stack.Count > 0)
            {
                object next = null;
                bool advanced = false;
                Exception failure = null;
                try
                {
                    CheckRecovery();
                    advanced = stack.Peek().MoveNext();
                    if (advanced) next = stack.Peek().Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogError("Part 3 Chapter 10 recovery stopped safely under black: " +
                        failure.Message + " Use Retry Chase Recovery after correcting the dependency, or Reset White Wall to cancel. No story/task progress was changed.", this);
                    yield break;
                }
                if (!advanced) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            completed = true;
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            recoveryRoutine = null;
            recoveryRunner = null;
            if (recovering)
            {
                if (completed) FinishRecovery();
                else
                {
                    recoveryFailed = true;
                    if (incomingFade != null && recoveryFadeOwned) incomingFade.HoldBlackForSequence();
                }
            }
        }
    }

    private IEnumerator RecoverChase()
    {
        CheckRecovery();
        if (!recoveryFailed) yield return incomingFade.FadeToBlack(recoveryFadeToBlackDuration);
        Require(recoveryFadeGroup.alpha >= 0.999f &&
            (recoveryFailed || incomingFade.IncomingFadeCompleted),
            "Black fade did not complete at full opacity; Miguel was not relocated.");
        Physics.SyncTransforms();
        Require(TryFindRecoveryPoint(player.position, whiteWall.BoundaryX, out Vector3 destination),
            $"No grounded, capsule-clear road destination was found ahead of Miguel X={player.position.x:F3} and wall X={whiteWall.BoundaryX:F3}, at least {recoveryWallSeparation:F1} units ahead of the wall and outside the Chapter 11 trigger. Screen remains black and controls held.");

        Vector3 delta = destination - player.position;
        // Same Rigidbody relocation pattern as PlayerSpawner; preserve the current rotation.
        playerBody.position = destination;
        player.SetPositionAndRotation(destination, playerBody.rotation);
        Physics.SyncTransforms();
        normalCamera.OnTargetObjectWarped(player, delta);
        normalCamera.PreviousStateIsValid = false;
        yield return null; // Let the existing Brain evaluate the warped follow target under black.
        CheckRecovery();
        Require(NormalCameraReady(), "Gameplay camera tracking was not available after relocation.");
        Require(ConfigureChaseGuard(), "The Chapter 10 slowdown guard could not be restored.");
        recoveryFailed = false;
        yield return incomingFade.FadeFromBlack(recoveryFadeFromBlackDuration);
        Require(incomingFade.IncomingFadeCompleted && recoveryFadeGroup.alpha <= 0.001f,
            "Fade from black did not finish; gameplay was not released.");
    }

    private bool IsRecoveryGround(Collider ground)
    {
        for (int i = 0; i < recoveryGroundColliders.Length; ++i)
            if (ground == recoveryGroundColliders[i]) return true;
        return false;
    }

    private bool ProbeRecoveryGround(float x, float z, out RaycastHit ground)
    {
        ground = default;
        int count = Physics.RaycastNonAlloc(new Vector3(x, 80f, z), Vector3.down,
            recoveryGroundHits, 100f, ~0, QueryTriggerInteraction.Ignore);
        if (count == 0 || count == recoveryGroundHits.Length) return false;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; ++i)
        {
            var hit = recoveryGroundHits[i];
            if (hit.collider.transform.IsChildOf(player)) continue;
            if (hit.distance < nearest) { nearest = hit.distance; ground = hit; }
        }
        return ground.collider != null && IsRecoveryGround(ground.collider) && ground.normal.y >= 0.95f;
    }

    private bool TryFindRecoveryPoint(Vector3 currentPosition, float boundaryX, out Vector3 destination)
    {
        destination = default;
        Vector3 scale = player.lossyScale;
        // Audited upright, uniformly scaled capsule; reject a changed player shape instead of guessing.
        if (scale.x <= 0f || Mathf.Abs(scale.x - scale.y) > 0.001f ||
            Mathf.Abs(scale.x - scale.z) > 0.001f || Vector3.Dot(player.up, Vector3.up) < 0.999f)
            return false;
        float radius = recoveryPlayerCapsule.radius * scale.x;
        float height = Mathf.Max(recoveryPlayerCapsule.height * scale.y, radius * 2f);
        Vector3 centerOffset = player.TransformVector(recoveryPlayerCapsule.center);
        float firstX = Mathf.Max(RecoveryRouteStartX, currentPosition.x,
            boundaryX + recoveryWallSeparation);
        float lastX = Mathf.Min(firstX + RecoveryMaxAdvance,
            recoveryEncounterExclusion.bounds.min.x - radius - Mathf.Abs(centerOffset.x) - 0.2f);
        // Bounded candidate search on the audited road, never backward or through the encounter.
        for (float x = firstX; x <= lastX; x += 2f)
        {
            for (int lane = 0; lane < 3; ++lane)
            {
                float z = recoveryRouteZ + (lane == 0 ? 0f : lane == 1 ? -2f : 2f);
                if (!ProbeRecoveryGround(x, z, out RaycastHit ground)) continue;
                bool supported = true;
                float footHeight = ground.point.y;
                for (int side = 0; side < 4; ++side)
                {
                    float dx = side == 0 ? radius : side == 1 ? -radius : 0f;
                    float dz = side == 2 ? radius : side == 3 ? -radius : 0f;
                    if (!ProbeRecoveryGround(x + dx, z + dz, out RaycastHit edge) ||
                        Mathf.Abs(edge.point.y - ground.point.y) > 0.3f) { supported = false; break; }
                    footHeight = Mathf.Max(footHeight, edge.point.y);
                }
                if (!supported) continue;
                Vector3 candidate = new Vector3(x, footHeight + 0.15f + height * 0.5f - centerOffset.y, z);
                Vector3 center = candidate + centerOffset;
                Vector3 half = Vector3.up * (height * 0.5f - radius);
                int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half,
                    radius + RecoveryClearance, recoveryOverlapHits, ~0, QueryTriggerInteraction.Collide);
                if (count == recoveryOverlapHits.Length) continue;
                bool clear = true;
                for (int i = 0; i < count; ++i)
                    if (!recoveryOverlapHits[i].transform.IsChildOf(player)) { clear = false; break; }
                if (!clear) continue; // Includes all trigger volumes, not just solid scenery.
                destination = candidate;
                return true;
            }
        }
        return false;
    }

    private void RestoreRecoveryBody()
    {
        if (!recoveryBodyCaptured) return;
        if (playerBody != null)
        {
            playerBody.isKinematic = recoveryWasKinematic;
            if (!playerBody.isKinematic)
            {
                playerBody.linearVelocity = Vector3.zero;
                playerBody.angularVelocity = Vector3.zero;
            }
        }
        recoveryBodyCaptured = false;
    }

    private void FinishRecovery()
    {
        // Resume only our existing wall after the genuine final fade, never another owner's wall.
        if (!RecoverySessionValid() || !whiteWall.TryResume(this))
        {
            recoveryFailed = true;
            incomingFade.HoldBlackForSequence();
            Debug.LogError("Part 3 Chapter 10 recovery lost wall ownership at completion; screen held black for a controlled retry/reset.", this);
            return;
        }
        RestoreRecoveryBody();
        ReleaseMovementWhenSafe();
        recovering = recoveryFailed = recoveryFadeOwned = false;
        warningPositionCaptured = warningLatched = chaseSafetySuspended = false;
    }

    private void CancelRecovery()
    {
        if (!recovering) return;
        recovering = false; // Prevent a disposed iterator from entering its failure/black branch.
        IEnumerator runner = recoveryRunner;
        recoveryRunner = null;
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        recoveryRoutine = null;
        // Unity stopping a coroutine alone is not our disposal contract. Explicitly
        // dispose our iterator so FadeController releases its observed-fade entry.
        (runner as IDisposable)?.Dispose();
        RestoreRecoveryBody();
        if (recoveryFadeOwned && recoveryFadeGroup != null)
        {
            // Dispose our active fade first, then complete the existing zero-duration
            // opening API so its readiness state is restored as well as its alpha.
            if (incomingFade != null && incomingFade.isActiveAndEnabled && recoveryOriginalAlpha <= 0.001f)
            {
                IEnumerator opening = incomingFade.FadeFromBlack(0f);
                try { opening.MoveNext(); }
                finally { (opening as IDisposable)?.Dispose(); }
            }
            recoveryFadeGroup.alpha = recoveryOriginalAlpha;
        }
        recoveryFadeOwned = recoveryFailed = false;
        ReleaseMovementWhenSafe();
        warningPositionCaptured = warningLatched = false;
    }

    [ContextMenu("Retry Chase Recovery")]
    private void RetryChaseRecovery()
    {
        if (Application.isPlaying && recovering && recoveryFailed && recoveryRoutine == null &&
            RecoverySessionValid() && whiteWall.IsHeldBy(this))
        {
            recoveryRunner = RunRecoverySafely(RecoverChase());
            recoveryRoutine = StartCoroutine(recoveryRunner);
        }
    }

    private void StopChaseWarning()
    {
        // Deactivating the dedicated root stops the presenter's parent and fade
        // coroutines, without changing shared dialogue or any other HUD.
        if (chaseWarning != null && chaseWarning.gameObject.activeSelf)
            chaseWarning.gameObject.SetActive(false);
    }

    private void HandleSessionFlag(string flag, bool value)
    {
        if (value && flag == MaestroBenSulatinLightEncounter.ArrivalFlag)
            RestoreChaseIndicator();
    }

    private void RestoreChaseIndicator()
    {
        // Reentry restores only the existing chase marker, never repeats the chapter.
        var manager = TaskManager.Instance;
        if (!running && encounterOwner == null && Qualified &&
            !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.WhitePresentationFlag) &&
            !SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.CompletedFlag) &&
            SessionStoryState.GetFlag(ChaseFlag) && manager != null &&
            manager.IsCurrentStage(TaskId, RunStage) && chaseNavigationCanvas != null)
            chaseNavigationCanvas.gameObject.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || running || ownsMovement || !Qualified || playerBody == null ||
            other.attachedRigidbody != playerBody || SessionStoryState.GetFlag(ClaimFlag) ||
            SessionStoryState.GetFlag(ChaseFlag))
            return;

        ValidateReferences();
        tasks = TaskManager.Instance;
        Require(tasks != null && tasks.GetTaskState(TaskId) == TaskState.Active,
            "The existing Part 3 main task is missing or inactive.");
        Require(tasks.IsCurrentStage(TaskId, InvestigateStage) ||
            (tasks.IsCurrentStage(TaskId, RunStage) && SessionStoryState.GetFlag(DialogueFlag)),
            "Expected investigation stage or an interrupted Chapter 10 handoff; no task stage was reset.");

        // An entry during another presentation is ignored. Leave and re-enter to retry.
        if (IsPresentationBusy() || !playerMovement.isActiveAndEnabled ||
            !incomingFade.IncomingFadeCompleted || !string.IsNullOrEmpty(SpawnData.spawnPointName))
            return;

        running = true;
        ownsClaim = true;
        SessionStoryState.SetFlag(ClaimFlag, true);
        StartCoroutine(PlayChapter());
    }

    private void ValidateReferences()
    {
        Component[] refs = { presentation, incomingFade, player, playerMovement, playerBody,
            movementLock, whiteWall, focusManager, focusPoint, normalCamera, focusCamera,
            cameraBrain, reactionConversation, chaseNavigationCanvas, churchNavigation,
            halfwayHouseRenderer, playerReaction, recoveryPlayerCapsule, recoveryFadeGroup,
            recoveryEncounterExclusion };
        string[] names = { "presentation", "incomingFade", "player", "playerMovement", "playerBody",
            "movementLock", "whiteWall", "focusManager", "focusPoint", "normalCamera", "focusCamera",
            "cameraBrain", "reactionConversation", "chaseNavigationCanvas", "churchNavigation",
            "halfwayHouseRenderer", "playerReaction", "recoveryPlayerCapsule", "recoveryFadeGroup",
            "recoveryEncounterExclusion" };
        for (int i = 0; i < refs.Length; i++)
            Require(refs[i] != null && refs[i].gameObject.scene == gameObject.scene,
                "Missing or cross-scene reference: " + names[i] + ".");
        Require(playerBody.transform == player && playerMovement.transform == player &&
            normalCamera.Follow == player, "Miguel's physics/movement/follow references do not match.");
        Require(recoveryPlayerCapsule.transform == player && recoveryPlayerCapsule.direction == 1 &&
            recoveryGroundColliders != null && recoveryGroundColliders.Length > 0 &&
            recoveryEncounterExclusion.isTrigger,
            "Recovery requires Miguel's upright capsule, audited road ground and the existing encounter exclusion.");
        Require(GetComponent<BoxCollider>().isTrigger, "Exploration collider must remain a trigger.");
        Require(focusPoint.returnMode == CameraFocusPoint.ReturnMode.Manual && !focusPoint.lockPlayer,
            "Existing reusable focus point must retain Manual return and its separate movement-lock ownership.");
        Require(chaseNavigationCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            churchNavigation.transform.IsChildOf(chaseNavigationCanvas.transform),
            "Church indicator must use the owned overlay branch, outside the suppressed ordinary navigation Canvas.");
        Require(ConversationManager.Instance != null && ConversationManager.Instance.isActiveAndEnabled,
            "ConversationManager is unavailable.");
        Require(finalEncounterBoundaryLimit != null &&
            finalEncounterBoundaryLimit.gameObject.scene == gameObject.scene,
            "Missing scene-local Chapter 11 church boundary safety limit.");
        Require(halfwayHouseRenderer.enabled && halfwayHouseRenderer.gameObject.activeInHierarchy &&
            halfwayHouseRenderer.GetComponent<MeshFilter>() != null &&
            halfwayHouseRenderer.GetComponent<MeshFilter>().sharedMesh != null,
            "The assigned house must have its visible main MeshRenderer and mesh.");
        Require(playerReaction.transform == player && playerReaction.isActiveAndEnabled,
            "Miguel's existing reaction controller is missing, disabled or assigned to another character.");
    }

    private bool IsPresentationBusy()
    {
        return Time.timeScale <= 0f || StorySequenceCoordinator.IsStorySequenceActive ||
            ConversationManager.Instance.IsConversationActive || focusManager.IsFocusing ||
            tasks == null || !tasks.CanPresentNotifications || tasks.HasPendingNotifications;
    }

    private IEnumerator PlayChapter()
    {
        bool chaseStarted = false;
        try
        {
            // Lock immediately, before revealing or advancing the boundary.
            movementLock.HoldMovementForStorySequence();
            ownsMovement = true;
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;

            float houseMidX = 0f;
            if (!SessionStoryState.GetFlag(DialogueFlag))
            {
                Bounds bounds = halfwayHouseRenderer.bounds;
                houseMidX = (bounds.min.x + bounds.max.x) * 0.5f;
                Require(!float.IsNaN(houseMidX) && !float.IsInfinity(houseMidX) &&
                    whiteWall.BoundaryX < houseMidX,
                    "White wall is already at/beyond poorhouse2's visual midpoint, or the bounds are invalid. It was not reversed or teleported.");
                Require(houseMidX < player.position.x,
                    "The house midpoint is not behind Miguel; the cinematic cannot safely hold the boundary there.");
            }
            Require(whiteWall.TryStartHeld(this),
                "White wall could not be held safely. It may already have overtaken Miguel or have another owner; use its existing Reset before retrying.");
            ownsWall = true;

            if (!SessionStoryState.GetFlag(DialogueFlag))
            {
                storyToken = StorySequenceCoordinator.Acquire(this);
                Require(storyToken != null && storyToken.IsValid, "Story Sequence ownership could not be acquired.");
                CheckOpeningSession();
                // Reuse the wall's existing normal-speed, exact-boundary clamp.
                // It suppresses player crossing while approaching and holds at target.
                Require(whiteWall.TryApproachEncounter(this, houseMidX),
                    "Wall owner rejected the house approach. Check normal speed, maximum endpoint and current ownership.");
                if (chaseMusic != null) chaseMusic.BeginReveal();
                while (!whiteWall.IsHeldBy(this))
                {
                    CheckOpeningSession(true);
                    yield return null;
                }
                CheckOpeningSession();
                Require(Mathf.Abs(whiteWall.BoundaryX - houseMidX) <= 0.001f,
                    "Wall was held before reaching poorhouse2's actual midpoint.");

                Require(!playerReaction.IsReacting, "Another Miguel reaction is already playing; it was not skipped or replaced.");
                // The existing controller owns its authored one-shot, so it still
                // hides itself if this coordinator is disabled during the reaction.
                playerReaction.PlayDefaultReaction();
                Require(playerReaction.IsReacting,
                    "Miguel's reaction refused playback. Check its existing indicator root and configured reaction sprite.");
                while (playerReaction.IsReacting)
                {
                    CheckOpeningSession();
                    yield return null;
                }
                CheckOpeningSession();

                // A point on the actual vertical surface at Miguel's Z/Y, not the
                // Part3SimpleWhiteVoid parent origin or the plane's distant Z center.
                Vector3 wallDirection = new Vector3(whiteWall.BoundaryX - player.position.x, 0f, 0f);
                lookRotation = Quaternion.LookRotation(wallDirection, Vector3.up);
                while (Quaternion.Angle(player.rotation, lookRotation) > 0.1f)
                {
                    CheckOpeningSession();
                    Quaternion rotation = Quaternion.RotateTowards(player.rotation, lookRotation, chapter10LookBackTurnSpeed * Time.deltaTime);
                    player.rotation = rotation;
                    playerBody.rotation = rotation;
                    yield return null;
                }
                player.rotation = lookRotation;
                playerBody.rotation = lookRotation;

                // Advance this helper inside the owning routine: a failed camera
                // prerequisite also executes PlayChapter's existing finally cleanup.
                IEnumerator focus = FocusAndWait();
                while (focus.MoveNext()) yield return focus.Current;

                Require(!ConversationManager.Instance.IsConversationActive,
                    "Another dialogue started during camera focus; Chapter 10 did not interrupt it.");
                waitingForDialogue = true;
                dialogueClosed = dialogueSucceeded = false;
                ConversationManager.ConversationClosed += HandleConversationClosed;
                ConversationManager.Instance.StartConversation(reactionConversation);
                Require(ConversationManager.Instance.ActiveConversation == reactionConversation,
                    "Chapter 10 dialogue did not start.");

                while (!dialogueClosed)
                {
                    CheckHeldSession();
                    yield return null;
                }
                ConversationManager.ConversationClosed -= HandleConversationClosed;
                Require(dialogueSucceeded, "Chapter 10 dialogue was cancelled; completion was not recorded.");
                SessionStoryState.SetFlag(DialogueFlag, true);

                Require(focusManager.TryReturnToNormal(focusPoint), "Owned camera return was rejected.");
                while (!cameraReturned)
                {
                    CheckHeldSession();
                    yield return null;
                }
                Require(focusManager.LastReturnCompleted && NormalCameraReady(),
                    "Normal camera return was interrupted; no task update was started.");
                storyToken.Release();
                storyToken = null;
            }

            // Retain the existing movement hold across this non-cinematic UI handoff.
            // Story ownership and dialogue are fully released so the authentic UI can play.
            while (IsPresentationBusy())
            {
                CheckHeldSession();
                yield return null;
            }
            Require(NormalCameraReady(), "Normal gameplay camera is unavailable before the task handoff.");
            if (!SessionStoryState.GetFlag(NotificationFlag))
            {
                int requestGeneration = generation;
                Require(tasks.AdvanceTaskStage(TaskId, RunStage, () =>
                {
                    if (this != null && running && isActiveAndEnabled && generation == requestGeneration &&
                        Qualified && tasks != null && tasks.IsCurrentStage(TaskId, RunStage) &&
                        whiteWall != null && whiteWall.IsHeldBy(this))
                        SessionStoryState.SetFlag(NotificationFlag, true);
                }), "TaskManager rejected part3_run_to_church or its notification UI.");
            }

            while (!SessionStoryState.GetFlag(NotificationFlag) || tasks.HasPendingNotifications)
            {
                CheckHeldSession();
                Require(tasks.IsCurrentStage(TaskId, RunStage), "Run objective changed during its notification.");
                Require(tasks.HasPendingNotifications || SessionStoryState.GetFlag(NotificationFlag),
                    "Run notification ended without its valid final-fade callback.");
                yield return null;
            }
            while (IsPresentationBusy())
            {
                CheckHeldSession();
                yield return null;
            }
            Require(NormalCameraReady(), "Normal gameplay camera was not restored after the notification.");
            ReleaseMovementWhenSafe();
            Require(playerMovement.isActiveAndEnabled, "Another owner still holds Miguel's movement; chase was not resumed.");
            Require(ConfigureChaseGuard() &&
                whiteWall.TryResume(this), "White wall encounter guard/ownership was interrupted before the chase.");
            chaseNavigationCanvas.gameObject.SetActive(true);
            SessionStoryState.SetFlag(ChaseFlag, true);
            chaseStarted = true;
        }
        finally
        {
            Cleanup(chaseStarted);
        }
    }

    private IEnumerator FocusAndWait()
    {
        Require(cameraBrain.isActiveAndEnabled && normalCamera.isActiveAndEnabled &&
            focusCamera.isActiveAndEnabled, "Required Cinemachine camera or Brain is disabled.");
        savedNormalPriority = normalCamera.Priority;
        savedFocusPriority = focusCamera.Priority;
        savedFocusPosition = focusCamera.transform.position;
        savedFocusRotation = focusCamera.transform.rotation;
        cameraReturned = false;
        var callback = new UnityEvent();
        callback.AddListener(OnCameraReturned);
        ownsFocus = focusManager.TryFocusOn(focusPoint, callback);
        Require(ownsFocus, "Existing focus manager rejected the reusable focus point.");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (cameraBrain.IsBlending || !ReferenceEquals(cameraBrain.ActiveVirtualCamera, focusCamera))
        {
            CheckHeldSession();
            Require(focusManager.IsFocusing && Time.realtimeSinceStartup < deadline,
                "Cinematic camera did not become live before its verification timeout.");
            yield return null;
        }
    }

    private bool NormalCameraReady()
    {
        return cameraBrain != null && cameraBrain.isActiveAndEnabled &&
            normalCamera != null && normalCamera.isActiveAndEnabled &&
            !cameraBrain.IsBlending && ReferenceEquals(cameraBrain.ActiveVirtualCamera, normalCamera);
    }

    private void OnCameraReturned()
    {
        if (!ownsFocus) return;
        ownsFocus = false;
        if (normalCamera != null) normalCamera.Priority = savedNormalPriority;
        if (focusCamera != null)
        {
            focusCamera.Priority = savedFocusPriority;
            focusCamera.transform.SetPositionAndRotation(savedFocusPosition, savedFocusRotation);
        }
        cameraReturned = true;
    }

    private void HandleConversationClosed(NPCConversation conversation, bool normal)
    {
        if (!waitingForDialogue || conversation != reactionConversation || dialogueClosed) return;
        waitingForDialogue = false;
        dialogueSucceeded = normal;
        dialogueClosed = true;
    }

    private void CheckOpeningSession(bool approachingHouse = false)
    {
        CheckHeldSession(approachingHouse);
        Require(storyToken != null && storyToken.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1 &&
            !ConversationManager.Instance.IsConversationActive && !tasks.HasPendingNotifications &&
            tasks.IsCurrentStage(TaskId, InvestigateStage),
            "House/reaction staging lost its Story Sequence or investigation stage, or another presentation started.");
        Require(NormalCameraReady() && !focusManager.IsFocusing && !whiteWall.IsWhiteCameraActive,
            "Normal world-view camera was interrupted during the house approach/reaction.");
        Require(player != null && playerBody != null && playerReaction != null &&
            playerReaction.isActiveAndEnabled && halfwayHouseRenderer != null &&
            halfwayHouseRenderer.enabled && halfwayHouseRenderer.gameObject.activeInHierarchy,
            "Miguel, his existing reaction controller or the house renderer disappeared during staging.");
    }

    private void CheckHeldSession(bool approachingHouse = false)
    {
        Require(Qualified && tasks != null && tasks == TaskManager.Instance &&
            tasks.CanPresentNotifications && ConversationManager.Instance != null &&
            ConversationManager.Instance.isActiveAndEnabled,
            "Part 3 arrival, task service, notification UI or dialogue service was interrupted.");
        Require(whiteWall != null && (approachingHouse ? whiteWall.IsControlledBy(this) : whiteWall.IsHeldBy(this)),
            "The Chapter 10 wall ownership/hold was interrupted; cinematic/chase handoff was aborted.");
    }

    private void Cleanup(bool preserveChase)
    {
        StopChaseWarning();
        warningPositionCaptured = warningLatched = chaseSafetySuspended = false;
        ++generation;
        ConversationManager.ConversationClosed -= HandleConversationClosed;
        var manager = ConversationManager.Instance;
        if (waitingForDialogue && manager != null && manager.ActiveConversation == reactionConversation)
            manager.EndConversation();
        waitingForDialogue = false;
        if (ownsFocus && focusManager != null)
        {
            if (!focusManager.TryReturnToNormal(focusPoint) && !focusManager.IsFocusing)
                OnCameraReturned();
        }
        storyToken?.Release();
        storyToken = null;
        ReleaseMovementWhenSafe();
        if (!preserveChase)
        {
            if (ownsWall && whiteWall != null) whiteWall.CancelStoryControl(this);
            ownsWall = false;
            if (chaseNavigationCanvas != null) chaseNavigationCanvas.gameObject.SetActive(false);
        }
        running = false;
        if (ownsClaim) SessionStoryState.SetFlag(ClaimFlag, false);
        ownsClaim = false;
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("Part 3 Chapter 10: " + message);
    }

    private void ReleaseMovementWhenSafe()
    {
        ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
        if (!ownsMovement) return;
        // Same scene-local cleanup pattern as Chapter 11: let an unexpected
        // replacement dialogue release its own lock before restoring our snapshot.
        if (movementLock != null && movementLock.OwnsConversationLock)
        {
            ConversationManager.ConversationClosed += ReleaseDeferredMovement;
            return;
        }
        if (movementLock != null) movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
    }

    private void ReleaseDeferredMovement(NPCConversation conversation, bool normal) =>
        ReleaseMovementWhenSafe();

    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= HandleSessionFlag;
        CancelRecovery();
        StopAllCoroutines();
        Cleanup(false);
    }

    private void OnDestroy()
    {
        ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
        if (ownsMovement && movementLock != null) movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
    }
}
