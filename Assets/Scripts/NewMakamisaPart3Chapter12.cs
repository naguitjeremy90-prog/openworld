using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Chapter 12 only: validated relocation, one cinematic, and the final existing journal entry.</summary>
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3Chapter12 : MonoBehaviour
{
    internal const string DialogueFlag = "alaala_part3_ch12_dialogue_complete";
    internal const string CompletedFlag = "alaala_part3_ch12_complete";
    private const string StepKey = "alaala_part3_ch12_cinematic_step";
    private const string ReflectionId = "part3_final_pagninilay";
    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private NewMakamisaPart3SimpleWhiteVoid whiteWall;
    [SerializeField] private Transform player;
    [SerializeField] private SimpleSampleCharacterControl movement;
    [SerializeField] private Rigidbody body;
    [SerializeField] private CapsuleCollider capsule;
    [SerializeField] private Animator animator;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private BoxCollider encounterTrigger;
    [SerializeField] private Camera renderingCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;
    [SerializeField] private NPCConversation firstLine, secondLine, thirdLine;
    [Header("Chapter 12 initialization settling")]
    [SerializeField, Min(0.1f)] private float initializationSettlingTimeout = 3f;
    [Header("White-void road staging; height is queried, never authored as a teleport height")]
    [SerializeField] private Vector2 stagingXZ = new Vector2(920f, 674.11f);
    [InspectorName("First Run Distance"), SerializeField, Min(0.1f)] private float firstWalkDistance = 2.5f;
    [InspectorName("Second Run Distance"), SerializeField, Min(0.1f)] private float secondWalkDistance = 3.5f;
    [InspectorName("Cinematic Run Speed")]
    [SerializeField, Range(0.1f, 5f)] private float cinematicWalkSpeed = 3.5f;
    [Header("Existing Talaarawan content and UI")]
    [SerializeField] private ReconstructionJournalManager journal;
    [SerializeField] private ReconstructionReflections reflections;
    [SerializeField] private ReflectionData finalReflection;
    [SerializeField] private Transform reflectionListContent;
    [SerializeField] private Button journalButton;
    [Header("Final submitted-answer presentation (saved text is never animated)")]
    [SerializeField, Min(0.1f)] private float submissionDotInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float submissionEllipsisHold = 0.6f;

    private StorySequenceToken token;
    private Coroutine routine;
    private IEnumerator runner;
    private int relocationOperation;
    private bool settlingEntry, retainedSettlingFailure;
    private bool attempted, running, terminalFailure, ownsMovement, ownsFocus, returned;
    private bool dialogueClosed, dialogueSucceeded, journalSeenOpen, journalException;
    private bool savedJournalAvailable;
    private bool reflectionFailureReported;
    private Selectable[] submissionControls;
    private bool[] submissionControlStates;
    private bool ownsSubmissionPresentation;
    private NPCConversation waitingConversation;
    private GameplayHUDTarget journalHud;
    private Vector3 screenRight, savedFocusPosition;
    private Quaternion savedFocusRotation;
    private PrioritySettings savedNormalPriority, savedFocusPriority;
    private readonly RaycastHit[] sweepHits = new RaycastHit[32];
    private readonly WaitForFixedUpdate fixedStep = new WaitForFixedUpdate();

    private bool QualifiedArrival => Application.isPlaying && presentation != null &&
        presentation.isActiveAndEnabled && presentation.gameObject.scene == gameObject.scene &&
        presentation.IsPart3PresentationReady && gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity" &&
        SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.CompletedFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.WhitePresentationFlag);

    private bool Qualified => QualifiedArrival && whiteWall != null &&
        whiteWall.IsWhiteCameraActive && whiteWall.HasCrossedPlayer;

    private void OnEnable()
    {
        // Re-enabling after an interrupted final submission can reopen the same
        // stored response; it never replays completed cinematic segments or clears data.
        if (Application.isPlaying && SessionStoryState.GetFlag(DialogueFlag) &&
            !SessionStoryState.GetFlag(CompletedFlag)) reflectionFailureReported = false;
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (!Qualified)
        {
            if (!running && retainedSettlingFailure)
            {
                token?.Release(); token = null;
                settlingEntry = retainedSettlingFailure = terminalFailure = false;
            }
            // Reset/eligibility loss aborts the story. Once fresh support is safe,
            // release only our outstanding initialization; never re-enter white mode.
            if (!running && movement != null && movement.OwnsControlledRelocation(this, relocationOperation) &&
                movement.CancelControlledRelocation(this, relocationOperation, out _))
            { relocationOperation = 0; terminalFailure = false; }
            if (!running && ownsMovement && movement != null && body != null && movementLock != null &&
                !StorySequenceCoordinator.IsStorySequenceActive &&
                !movementLock.OwnsConversationLock && movement.TryValidateControlledPose(body.position,
                    body.rotation, false, encounterTrigger, out _, out _, out _)) ReleaseMovement();
            return;
        }
        if (SessionStoryState.GetFlag(CompletedFlag)) return;
        if (SessionStoryState.GetFlag(DialogueFlag))
        {
            if (journal == null || reflections == null || journalButton == null || reflectionListContent == null)
            {
                if (!reflectionFailureReported) Debug.LogError("Chapter 12 final Pagninilay lost its journal, reflection presenter, entry list or reopen button reference. Restore that dependency and use Reopen Final Pagninilay.", this);
                reflectionFailureReported = true;
                return;
            }
            if (!journalException && !reflectionFailureReported && !StorySequenceCoordinator.IsStorySequenceActive)
            {
                try { OpenFinalReflection(); }
                catch (Exception error)
                {
                    reflectionFailureReported = true;
                    Debug.LogError("Chapter 12 final Pagninilay could not open: " + error.Message +
                        " Correct the journal dependency and use Reopen Final Pagninilay. No completion was recorded.", this);
                }
            }
            if (journal != null && journal.IsOpen) journalSeenOpen = true;
            if (!running && !reflectionFailureReported && journalSeenOpen && journal.IsOpen &&
                reflections.GetReflectionStatus(ReflectionId) == ReflectionStatus.Completed &&
                !string.IsNullOrWhiteSpace(reflections.GetSubmittedResponse(ReflectionId)) &&
                SessionStoryState.GetString("journal_reflection_response:" + ReflectionId) ==
                    reflections.GetSubmittedResponse(ReflectionId))
            {
                running = true;
                runner = RunSafely(FinishFinalSubmission());
                routine = StartCoroutine(runner);
            }
            return;
        }
        if (attempted || running || terminalFailure || Time.timeScale <= 0f ||
            SessionStoryState.GetFlag("alaala_part3_ch11_owner_active") ||
            StorySequenceCoordinator.IsStorySequenceActive || ConversationManager.Instance == null ||
            ConversationManager.Instance.IsConversationActive || focusManager == null || focusManager.IsFocusing ||
            !NormalCameraReady() || movement == null || !movement.isActiveAndEnabled) return;
        attempted = running = true;
        runner = RunSafely(PlayChapter());
        routine = StartCoroutine(runner);
    }

    private IEnumerator RunSafely(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(work);
        try
        {
            while (stack.Count > 0)
            {
                bool advanced = false; object next = null; Exception failure = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) next = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    string entryDiagnostic = "";
                    if (settlingEntry)
                    {
                        retainedSettlingFailure = isActiveAndEnabled && Qualified && token != null && token.IsValid;
                        terminalFailure |= retainedSettlingFailure;
                        entryDiagnostic = " Entry state: " + DescribeSettlingEntry();
                    }
                    Debug.LogError("Part 3 Chapter 12 stopped: " + failure.Message + entryDiagnostic +
                        " No unfinished chapter/reflection was marked complete. Use the Chapter 12 development context actions after correcting the dependency.", this);
                    yield break;
                }
                if (!advanced) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            routine = null; runner = null;
            Cleanup();
        }
    }

    private IEnumerator PlayChapter()
    {
        ValidateReferences();
        Require(!StorySequenceCoordinator.IsStorySequenceActive, "A cinematic already owns the player.");
        token = StorySequenceCoordinator.Acquire("alaala_part3_ch12_initialization");
        Require(token != null && token.IsValid, "Initialization ownership was rejected.");
        settlingEntry = true;
        yield return SettleBeforeRelocation();
        settlingEntry = false;
        Vector3 right = renderingCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 10f)) -
            renderingCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, 10f));
        right.y = 0f;
        Require(right.sqrMagnitude > 0.5f, "Rendering camera has no usable horizontal screen basis.");
        screenRight = right.normalized;
        Vector3 stage = new Vector3(stagingXZ.x, 0f, stagingXZ.y);
        Require(movement.TryValidateControlledPose(stage, body.rotation, true, null,
            out stage, out Collider road, out string reason) && road is TerrainCollider,
            reason ?? "Staging must use the audited terrain road.");
        Vector3 leftEnd = stage - screenRight * firstWalkDistance;
        Vector3 rightEnd = leftEnd + screenRight * secondWalkDistance;
        ValidatePath(stage, leftEnd, road);
        ValidatePath(leftEnd, rightEnd, road);
        int step = SessionStoryState.GetInt(StepKey);
        Vector3 destination = step >= 4 ? rightEnd : step >= 2 ? leftEnd : stage;
        Vector3 before = body.position;
        Require(movement.TryBeginControlledRelocation(this, destination, encounterTrigger,
            out relocationOperation, out reason), reason ?? "Controlled relocation was rejected.");
        WarpCamera(before);
        yield return AwaitPlacement();
        Require(movement.CancelControlledRelocation(this, relocationOperation, out reason), reason);
        relocationOperation = 0;
        movementLock.HoldMovementForStorySequence(); ownsMovement = true;
        token.Release();
        token = StorySequenceCoordinator.Acquire(this); // One 12A owns silence, all three lines and both walks.
        Require(token != null && token.IsValid, "Sequence 12A ownership was rejected.");
        if (step == 0)
        {
            float silence = 0f;
            while (silence < 2f) { CheckSession(); silence += Time.deltaTime; yield return null; }
        }
        if (step < 1) { yield return Say(firstLine); SessionStoryState.SetInt(StepKey, 1); }
        if (step < 2) { yield return WalkTo(leftEnd); SessionStoryState.SetInt(StepKey, 2); }
        if (step < 3) { yield return Say(secondLine); SessionStoryState.SetInt(StepKey, 3); }
        if (step < 4) { yield return WalkTo(rightEnd); SessionStoryState.SetInt(StepKey, 4); }
        if (step < 5) { yield return Say(thirdLine); SessionStoryState.SetInt(StepKey, 5); }
        yield return ReturnCamera();
        CheckSession();
        token.Release(); token = null;
        ReleaseMovement();
        SessionStoryState.SetFlag(DialogueFlag, true);
        OpenFinalReflection();
    }

    private bool GroundedIdleEntryReady => movement != null && body != null &&
        movement.HasGroundedIdlePresentation && Mathf.Abs(body.linearVelocity.y) <= 0.1f &&
        !movement.HasPendingControlledIntent;

    private string DescribeSettlingEntry() =>
        $"cinematicOwners={StorySequenceCoordinator.ActiveOwnerCount}, ownTokenValid={token != null && token.IsValid}, " +
        $"conversationLock={movementLock != null && movementLock.OwnsConversationLock}; " +
        (movement != null ? movement.DescribeControlledRelocationEntry() : "Movement controller missing.");

    private IEnumerator SettleBeforeRelocation()
    {
        Require(float.IsFinite(initializationSettlingTimeout) && initializationSettlingTimeout >= 0.1f,
            "Initialization settling timeout must be finite and at least 0.1 seconds.");
        float elapsed = 0f;
        float lastPhysicsTime = Time.fixedTime;
        int stableEvaluations = 0;
        while (stableEvaluations < 2)
        {
            // The existing Story Sequence branch clears input while keeping grounding
            // synchronization enabled. Do not acquire DialogueMovementLock yet.
            yield return null;
            CheckSession();
            Require(movement.isActiveAndEnabled && !movementLock.OwnsConversationLock &&
                Physics.simulationMode == SimulationMode.FixedUpdate && NormalCameraReady() && !focusManager.IsFocusing,
                "Settling lost its enabled grounding controller, fixed-step physics or exclusive cinematic ownership.");
            if (Time.timeScale <= 0f) continue;
            elapsed += Time.deltaTime;
            Require(elapsed <= initializationSettlingTimeout,
                $"Grounded Idle did not stabilize within {initializationSettlingTimeout:F2} active seconds.");
            // Sample on separate rendered frames following distinct physics updates.
            // This lets the normal Animator evaluate between successful observations.
            if (Time.fixedTime <= lastPhysicsTime) continue;
            lastPhysicsTime = Time.fixedTime;
            stableEvaluations = GroundedIdleEntryReady ? stableEvaluations + 1 : 0;
        }
    }

    private IEnumerator AwaitPlacement()
    {
        bool rollback = false;
        while (true)
        {
            CheckSession();
            var result = movement.ConfirmControlledRelocation(this, relocationOperation, out string reason);
            if (result == SimpleSampleCharacterControl.ControlledRelocationResult.Ready)
            {
                Require(!rollback, "Destination grounding failed; the single rollback succeeded. Initialization was cancelled safely.");
                yield break;
            }
            if (result == SimpleSampleCharacterControl.ControlledRelocationResult.Failed)
            {
                Vector3 before = body.position;
                string rollbackReason = rollback ? "The single automatic rollback was already attempted." : null;
                if (!rollback && movement.TryRollbackControlledRelocation(this, relocationOperation,
                    out int next, out rollbackReason))
                { rollback = true; relocationOperation = next; WarpCamera(before); }
                else
                {
                    terminalFailure = true;
                    throw new InvalidOperationException("Grounding/rollback failed. Player remains in an explicitly owned Idle hold. Use Recover Chapter 12 Origin, or Cancel Chapter 12 Initialization When Grounded. " + reason + " Rollback: " + rollbackReason);
                }
            }
            Require(result != SimpleSampleCharacterControl.ControlledRelocationResult.Rejected,
                reason ?? "Relocation ownership was lost.");
            yield return fixedStep;
        }
    }

    private void WarpCamera(Vector3 previous)
    {
        normalCamera.OnTargetObjectWarped(player, body.position - previous);
        normalCamera.PreviousStateIsValid = false;
    }

#if UNITY_EDITOR
    /// <summary>Read-only authored-geometry check; does not enter relocation or move any scene object.</summary>
    public string ValidateAuthoredStagingForEditor()
    {
        Require(!Application.isPlaying, "Authored geometry validation is Edit Mode only.");
        ValidateReferences();
        Vector3 right = renderingCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 10f)) -
            renderingCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, 10f));
        right.y = 0f; right.Normalize();
        Require(movement.TryValidateControlledPose(new Vector3(stagingXZ.x, 0f, stagingXZ.y), body.rotation,
            true, null, out Vector3 stage, out Collider road, out string reason) && road is TerrainCollider, reason);
        Vector3 left = stage - right * firstWalkDistance;
        Vector3 end = left + right * secondWalkDistance;
        ValidatePath(stage, left, road); ValidatePath(left, end, road);
        return $"Stage {stage:F6}; screen-right {right:F6}; first stop {left:F6}; second stop {end:F6}; support {road.name}. Both full capsule sweeps and sampled support/trigger clearance passed.";
    }
#endif

    private void ValidatePath(Vector3 from, Vector3 to, Collider road)
    {
        Require(firstWalkDistance > 0f && secondWalkDistance > 0f &&
            cinematicWalkSpeed >= 0.1f && cinematicWalkSpeed <= 5f &&
            firstWalkDistance <= 5f && secondWalkDistance <= 5f,
            "Cinematic distances must remain inside the audited five-unit range, with a 0.1-to-5 unit/second running pace.");
        int samples = Mathf.CeilToInt(Vector3.Distance(from, to) / 0.2f);
        Quaternion pathRotation = Quaternion.LookRotation((to - from).normalized);
        for (int i = 0; i <= samples; ++i)
        {
            Vector3 candidate = Vector3.Lerp(from, to, samples == 0 ? 0f : (float)i / samples);
            Require(movement.TryValidateControlledPose(candidate, pathRotation, true, null,
                out Vector3 grounded, out Collider support, out string reason) && support == road &&
                Mathf.Abs(grounded.y - from.y) < 0.025f,
                reason ?? "Walking path leaves the validated flat road support.");
        }
        Vector3 scale = player.lossyScale;
        float radius = capsule.radius * scale.x;
        float half = capsule.height * scale.y * 0.5f;
        Vector3 center = from + pathRotation * Vector3.Scale(capsule.center, scale);
        Vector3 direction = to - from;
        int hits = Physics.CapsuleCastNonAlloc(center - Vector3.up * (half - radius),
            center + Vector3.up * (half - radius), radius, direction.normalized,
            sweepHits, direction.magnitude, ~0, QueryTriggerInteraction.Collide);
        Require(hits < sweepHits.Length, "Full walking sweep exceeded its query capacity.");
        for (int i = 0; i < hits; ++i)
            Require(sweepHits[i].collider == road || sweepHits[i].collider.attachedRigidbody == body,
                "Full cinematic walking sweep hits collider/trigger: " + sweepHits[i].collider.name);
    }

    private IEnumerator WalkTo(Vector3 target)
    {
        yield return ReturnCamera();
        CheckSession();
        Vector3 direction = target - body.position; direction.y = 0f;
        Quaternion facing = Quaternion.LookRotation(direction);
        float turnBudget = Quaternion.Angle(body.rotation, facing) / 120f + 1f;
        while (Quaternion.Angle(body.rotation, facing) > 0.5f)
        {
            CheckSession();
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, facing, 120f * Time.fixedDeltaTime));
            turnBudget -= Time.fixedDeltaTime;
            Require(turnBudget >= 0f, "Cinematic facing could not complete through the existing Rigidbody.");
            yield return fixedStep;
        }
        float remainingBudget = Vector3.Distance(body.position, target) / cinematicWalkSpeed + 2f;
        while (Vector3.Distance(body.position, target) > 0.015f)
        {
            yield return fixedStep;
            CheckSession();
            Require(NormalCameraReady(), "Gameplay camera tracking was interrupted during cinematic walking.");
            Vector3 next = Vector3.MoveTowards(body.position, target, cinematicWalkSpeed * Time.fixedDeltaTime);
            Require(movement.TryValidateControlledPose(next, body.rotation, false, null,
                out _, out _, out string reason), reason);
            body.MovePosition(next);
            animator.SetFloat("MoveSpeed", 1f); // Verified existing run blend; normal controller settings are untouched.
            remainingBudget -= Time.fixedDeltaTime;
            Require(remainingBudget >= 0f, "Cinematic walking failed to reach its validated endpoint.");
        }
        animator.SetFloat("MoveSpeed", 0f);
        Vector3 velocity = body.linearVelocity; velocity.x = velocity.z = 0f; body.linearVelocity = velocity;
        int evaluations = 0;
        while (!movement.IsGrounded || Mathf.Abs(body.linearVelocity.y) > 0.1f)
        {
            Require(++evaluations <= 8, "Grounded Idle was not confirmed after the scripted walk.");
            CheckSession(); yield return fixedStep;
        }
        Require(movement.TryValidateControlledPose(body.position, body.rotation, false, null,
            out _, out _, out string stoppedReason), stoppedReason);
        animator.SetBool("Grounded", true); // Written only after physical support was verified.
        float idleBudget = 1f;
        do
        {
            yield return null;
            CheckSession(); idleBudget -= Time.deltaTime;
        } while (!movement.HasGroundedIdlePresentation && idleBudget > 0f);
        Require(movement.HasGroundedIdlePresentation,
            "The scripted walk did not settle to the verified Movement/Idle presentation.");
    }

    private IEnumerator Say(NPCConversation conversation)
    {
        CheckSession();
        savedNormalPriority = normalCamera.Priority; savedFocusPriority = focusCamera.Priority;
        savedFocusPosition = focusCamera.transform.position; savedFocusRotation = focusCamera.transform.rotation;
        returned = false;
        var finished = new UnityEvent(); finished.AddListener(OnCameraReturned);
        ownsFocus = focusManager.TryFocusOn(focusPoint, finished);
        Require(ownsFocus, "Approved Miguel focus was rejected.");
        float budget = 10f;
        while (brain.IsBlending || !ReferenceEquals(brain.ActiveVirtualCamera, focusCamera))
        { CheckSession(); if (Time.timeScale > 0f) budget -= Time.unscaledDeltaTime; Require(budget > 0f, "Focus camera did not become live."); yield return null; }
        dialogueClosed = dialogueSucceeded = false;
        waitingConversation = conversation;
        ConversationManager.ConversationClosed += Closed;
        ConversationManager.Instance.StartConversation(conversation);
        Require(ConversationManager.Instance.ActiveConversation == conversation, "Chapter 12 dialogue was rejected.");
        while (!dialogueClosed) { CheckSession(); Require(focusManager.IsFocusing, "Dialogue focus was interrupted."); yield return null; }
        ConversationManager.ConversationClosed -= Closed;
        Require(dialogueSucceeded, "Dialogue was cancelled; its step was not completed.");
        yield return ReturnCamera();
    }

    private IEnumerator ReturnCamera()
    {
        if (!ownsFocus) yield break;
        Require(focusManager.TryReturnToNormal(focusPoint), "Owned focus return was rejected.");
        while (!returned) { CheckSession(); yield return null; }
        Require(focusManager.LastReturnCompleted && NormalCameraReady(), "Normal camera return was not confirmed.");
    }

    private void OnCameraReturned()
    {
        if (!ownsFocus) return;
        if (normalCamera != null) normalCamera.Priority = savedNormalPriority;
        if (focusCamera != null)
        {
            focusCamera.Priority = savedFocusPriority;
            focusCamera.transform.SetPositionAndRotation(savedFocusPosition, savedFocusRotation);
        }
        ownsFocus = false; returned = true;
    }

    private void Closed(NPCConversation conversation, bool normal)
    {
        if (waitingConversation != conversation || dialogueClosed) return;
        waitingConversation = null; dialogueClosed = true; dialogueSucceeded = normal;
    }

    private bool NormalCameraReady() => brain != null && brain.isActiveAndEnabled && !brain.IsBlending &&
        normalCamera != null && normalCamera.isActiveAndEnabled && ReferenceEquals(brain.ActiveVirtualCamera, normalCamera);

    private void CheckSession()
    {
        Require(player != null && body != null && movement != null && capsule != null && animator != null &&
            movementLock != null && brain != null && brain.isActiveAndEnabled && renderingCamera != null &&
            renderingCamera.isActiveAndEnabled && normalCamera != null && normalCamera.isActiveAndEnabled &&
            focusCamera != null && focusCamera.isActiveAndEnabled && focusManager != null && focusManager.isActiveAndEnabled,
            "Miguel, physics, input hold or a required existing camera dependency disappeared or was disabled.");
        Require(!ownsFocus || focusManager.IsFocusing, "Owned camera focus was interrupted.");
        Require(Qualified && token != null && token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1 &&
            ConversationManager.Instance != null &&
            (!ConversationManager.Instance.IsConversationActive || ConversationManager.Instance.ActiveConversation == waitingConversation),
            "Qualified white-void session or exclusive cinematic/dialogue ownership was interrupted.");
    }

    private void ValidateReferences()
    {
        Component[] refs = { presentation, whiteWall, player, movement, body, capsule, animator, movementLock,
            encounterTrigger, renderingCamera, brain, normalCamera, focusCamera, focusManager, focusPoint,
            firstLine, secondLine, thirdLine, journal, reflections, reflectionListContent, journalButton };
        foreach (Component reference in refs)
            Require(reference != null && reference.gameObject.scene == gameObject.scene,
                "Missing/cross-scene Chapter 12 reference. Check the scene-local coordinator Inspector.");
        Require(player == body.transform && player == movement.transform && player == capsule.transform &&
            player == animator.transform && normalCamera.Follow == player && focusPoint.returnMode == CameraFocusPoint.ReturnMode.Manual &&
            !focusPoint.lockPlayer && !movementLock.OwnsConversationLock,
            "Miguel, his normal follow camera, approved Manual focus or movement-lock ownership does not match.");
        Require(finalReflection != null && finalReflection.reflectionID == ReflectionId &&
            finalReflection.prompt == "Ano sa palagay mo ang nangyari sa mundong iyong nasaksihan?",
            "The registered final Pagninilay content is missing or differs from the approved question.");
    }

    private void OpenFinalReflection()
    {
        Require(Qualified && !StorySequenceCoordinator.IsStorySequenceActive && !focusManager.IsFocusing,
            "Final Pagninilay requires released cinematic ownership and the retained white view.");
        if (!journalException)
        {
            journalHud = journalButton.GetComponent<GameplayHUDTarget>();
            Require(journalHud != null, "Part 3's existing Talaarawan HUD target is unavailable for the final-entry reopen path.");
            savedJournalAvailable = journalHud.IsAvailable;
            journalException = true;
            GameplaySystemState.SetUnlocked(GameplaySystemId.Journal, true);
            journalHud.IsAvailable = true;
            journalHud.SetPresentationSuppressed(false); // Only this button; Part 3 suppresses it again on cleanup.
            journalButton.onClick.AddListener(SelectFinalReflection);
        }
        journal.UnlockReflection(ReflectionId);
        journal.OpenJournal();
        Require(journal.IsOpen, "Existing Talaarawan did not open; no reflection completion was recorded.");
        SelectFinalReflection();
        journalSeenOpen = true;
    }

    private IEnumerator FinishFinalSubmission()
    {
        reflectionFailureReported = true; // A failed/interrupted transition requires the existing explicit reopen action.
        try
        {
            Require(Qualified && journal.IsOpen && !StorySequenceCoordinator.IsStorySequenceActive &&
                submissionDotInterval >= .1f && submissionEllipsisHold >= .1f,
                "Final submission requires the retained white view, open journal and valid dot timings.");
            Require(reflections.TryBeginFinalSubmissionPresentation(this, out string reason), reason);
            ownsSubmissionPresentation = true;
            // Capture only journal controls. The final-only close guard protects this brief modal transition.
            submissionControls = journal.GetComponentsInChildren<Selectable>(true);
            submissionControlStates = new bool[submissionControls.Length];
            for (int i = 0; i < submissionControls.Length; ++i)
            { submissionControlStates[i] = submissionControls[i].interactable; submissionControls[i].interactable = false; }
            for (int count = 1; count <= 3; ++count)
            {
                Require(reflections.SetFinalSubmissionDots(this, count), "Final submitted-answer display lost ownership or visibility.");
                float remaining = count < 3 ? submissionDotInterval : submissionEllipsisHold;
                while (remaining > 0f)
                {
                    Require(Qualified && journal.IsOpen && !StorySequenceCoordinator.IsStorySequenceActive,
                        "Final Pagninilay transition was interrupted. Reopen it to resume with the stored answer.");
                    remaining -= Time.deltaTime; yield return null;
                }
            }
            string answer = reflections.GetSubmittedResponse(ReflectionId);
            Require(!string.IsNullOrWhiteSpace(answer) &&
                SessionStoryState.GetString("journal_reflection_response:" + ReflectionId) == answer,
                "Final reflection storage could not be confirmed; no completion was recorded.");
            EndSubmissionPresentation();
            journal.CloseJournal(); // This API sets IsOpen=false and deactivates its actual window synchronously.
            Require(!journal.IsOpen, "Talaarawan did not confirm closure after the submission transition.");
            RestoreJournalButton();
            SessionStoryState.SetFlag("alaala_part3_ch12_ellipsis_complete", true);
            SessionStoryState.SetFlag(CompletedFlag, true); // Chapter 13 observes this only after real closure.
            reflectionFailureReported = false;
        }
        finally { EndSubmissionPresentation(); }
    }

    private void EndSubmissionPresentation()
    {
        if (submissionControls != null)
            for (int i = 0; i < submissionControls.Length; ++i)
                if (submissionControls[i] != null) submissionControls[i].interactable = submissionControlStates[i];
        submissionControls = null; submissionControlStates = null;
        if (ownsSubmissionPresentation && reflections != null) reflections.EndFinalSubmissionPresentation(this);
        ownsSubmissionPresentation = false;
    }

    private void SelectFinalReflection()
    {
        if (!Qualified || !journal.IsOpen || !SessionStoryState.GetFlag(DialogueFlag)) return;
        journal.OpenReflectionsTab();
        // Reuse the existing generated entry button and its normal selection/read-state callback.
        for (int i = reflectionListContent.childCount - 1; i >= 0; --i)
        {
            Transform child = reflectionListContent.GetChild(i);
            if (child.name != "Reflection_" + ReflectionId) continue;
            Button button = child.GetComponent<Button>();
            Require(button != null, "Registered final reflection entry has no selection Button.");
            button.onClick.Invoke(); return;
        }
        throw new InvalidOperationException("Final Pagninilay is not registered with the existing reflection list.");
    }

    private void RestoreJournalButton()
    {
        if (!journalException) return;
        if (journalButton != null) journalButton.onClick.RemoveListener(SelectFinalReflection);
        if (journalHud != null) { journalHud.IsAvailable = savedJournalAvailable; journalHud.SetPresentationSuppressed(true); }
        journalException = false;
    }

    private void ReleaseMovement()
    {
        if (!ownsMovement) return;
        Require(!movementLock.OwnsConversationLock, "Another conversation still owns movement.");
        movementLock.ReleaseStorySequenceHold(); ownsMovement = false;
    }

    private void Cleanup()
    {
        if (!running && !ownsMovement && !ownsFocus && relocationOperation == 0 && token == null) return;
        try
        {
        ConversationManager.ConversationClosed -= Closed;
        if (waitingConversation != null && ConversationManager.Instance != null &&
            ConversationManager.Instance.ActiveConversation == waitingConversation) ConversationManager.Instance.EndConversation();
        waitingConversation = null;
        if (ownsFocus && focusManager != null) focusManager.TryReturnToNormal(focusPoint);
        if (animator != null && (ownsMovement || relocationOperation != 0)) animator.SetFloat("MoveSpeed", 0f);
        if (movement != null && movement.OwnsControlledRelocation(this, relocationOperation))
        {
            if (movement.CancelControlledRelocation(this, relocationOperation, out _))
                relocationOperation = 0;
            else terminalFailure = true; // Explicit, per-player hold; development actions remain available.
        }
        if (ownsMovement && movement != null && body != null && movementLock != null &&
            movement.TryValidateControlledPose(body.position, body.rotation, false, null, out _, out _, out _) &&
            !movementLock.OwnsConversationLock &&
            (!StorySequenceCoordinator.IsStorySequenceActive ||
                (token != null && token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1))) ReleaseMovement();
        else if (ownsMovement) terminalFailure = true;
        }
        finally
        {
            // A failed settle has not relocated Miguel. Retain only our cinematic
            // token so input stays blocked while physics/Animator can still settle.
            if (!retainedSettlingFailure || !isActiveAndEnabled || !Qualified)
            {
                token?.Release(); token = null;
                settlingEntry = retainedSettlingFailure = false;
            }
            running = false;
        }
    }

    [ContextMenu("Recover Chapter 12 Origin")]
    private void RecoverOriginForDevelopment()
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled) { Debug.LogError("Re-enable Part3Chapter12 before requesting its development recovery.", this); return; }
        if (!Qualified || running || StorySequenceCoordinator.IsStorySequenceActive ||
            !movement.OwnsControlledRelocation(this, relocationOperation)) return;
        Vector3 before = body.position;
        if (!movement.RetryControlledOriginForDevelopment(this, relocationOperation, out int next, out string reason))
        { Debug.LogError("Chapter 12 development origin recovery rejected: " + reason, this); return; }
        relocationOperation = next; WarpCamera(before);
        token = StorySequenceCoordinator.Acquire("alaala_part3_ch12_development_recovery");
        running = true;
        runner = RunSafely(AwaitDevelopmentRecovery()); routine = StartCoroutine(runner);
#endif
    }

    private IEnumerator AwaitDevelopmentRecovery()
    {
        while (true)
        {
            CheckSession();
            var state = movement.ConfirmControlledRelocation(this, relocationOperation, out string reason);
            if (state == SimpleSampleCharacterControl.ControlledRelocationResult.Ready)
            {
                Require(movement.CancelControlledRelocation(this, relocationOperation, out reason), reason);
                relocationOperation = 0; terminalFailure = false; yield break;
            }
            Require(state == SimpleSampleCharacterControl.ControlledRelocationResult.Waiting, reason);
            yield return fixedStep;
        }
    }

    [ContextMenu("Cancel Chapter 12 Initialization When Grounded")]
    private void CancelInitializationForDevelopment()
    {
#if UNITY_EDITOR
        bool ownsFailedSettlingToken = retainedSettlingFailure && token != null && token.IsValid &&
            StorySequenceCoordinator.ActiveOwnerCount == 1;
        if (!QualifiedArrival || running || (StorySequenceCoordinator.IsStorySequenceActive && !ownsFailedSettlingToken)) return;
        if (ownsFailedSettlingToken && !GroundedIdleEntryReady)
        {
            Debug.LogError("Chapter 12 initialization remains held: " + DescribeSettlingEntry(), this);
            return;
        }
        if (movement.OwnsControlledRelocation(this, relocationOperation) &&
            !movement.CancelControlledRelocation(this, relocationOperation, out _))
        { Debug.LogError("Chapter 12 cancel requires fresh grounded Idle. Correct the ground dependency and use Recover Chapter 12 Origin.", this); return; }
        if (!movement.TryValidateControlledPose(body.position, body.rotation, false, encounterTrigger, out _, out _, out _)) return;
        relocationOperation = 0; terminalFailure = false;
        retainedSettlingFailure = settlingEntry = false;
        Cleanup();
#endif
    }

    [ContextMenu("Retry Chapter 12 Initialization")]
    private void RetryInitializationForDevelopment()
    {
#if UNITY_EDITOR
        if (Qualified && !running && !terminalFailure && relocationOperation == 0) attempted = false;
#endif
    }

    [ContextMenu("Reopen Final Pagninilay")]
    private void ReopenReflectionForDevelopment()
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled || !Qualified || !SessionStoryState.GetFlag(DialogueFlag) || running ||
            SessionStoryState.GetFlag(CompletedFlag) || StorySequenceCoordinator.IsStorySequenceActive) return;
        reflectionFailureReported = false;
        try { OpenFinalReflection(); }
        catch (Exception error) { reflectionFailureReported = true; Debug.LogError(error.Message, this); }
#endif
    }

    private static void Require(bool valid, string reason)
    { if (!valid) throw new InvalidOperationException("Chapter 12: " + reason); }

    private void OnDisable()
    {
        IEnumerator active = runner; runner = null;
        if (routine != null) StopCoroutine(routine); routine = null;
        (active as IDisposable)?.Dispose();
        EndSubmissionPresentation(); Cleanup(); RestoreJournalButton();
    }
}
