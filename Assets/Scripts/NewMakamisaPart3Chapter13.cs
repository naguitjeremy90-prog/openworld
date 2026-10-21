using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Supercyan.FreeSample;
using Unity.Cinemachine;

/// <summary>Scene-local storyboard ending with validated player choreography; preserves the white camera.</summary>
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3Chapter13 : MonoBehaviour
{
    internal const string CompletedFlag = "alaala_part3_ch13_complete";
    public static readonly IReadOnlyList<string> Narration = Array.AsReadOnly(new[]
    {
        "May mga kuwentong hindi umabot sa kanilang wakas.",
        "Ang Makamisa ay isa sa mga akdang hindi natapos ni José Rizal.",
        "Sa mga pahinang naiwan, nakilala natin ang ilang tauhan, pangyayari, at bahagi ng lipunang nais niyang ilarawan.",
        "Ngunit may mga tanong na nanatiling walang kasagutan, at mga salaysay na hindi na niya naipagpatuloy.",
        "Sa pamamagitan ng pagsasaliksik at pangangalaga sa mga natitirang sulatin, patuloy nating nakikilala ang bahaging ito ng ating panitikan.",
        "Hindi natin kailangang likhain ang nawawalang wakas upang pahalagahan ang kuwentong naiwan."
    });
    public const string Subtitle = "Mga kuwentong naiwan.\nMga alaalang dapat pangalagaan.";

    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private NewMakamisaPart3SimpleWhiteVoid whiteWall;
    [SerializeField] private ReconstructionJournalManager journal;
    [SerializeField] private ReconstructionReflections reflections;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private FadeController fade;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private Canvas endingCanvas;
    [SerializeField] private TMP_Text narrationText;
    [SerializeField] private Image blackBackdrop;
    [SerializeField] private TMP_Text titleText, subtitleText, completedCreditsText;
    [SerializeField] private AlaalaRollingCreditsController credits;
    [SerializeField] private NewMakamisaPart3EndingMusic endingMusic;
    [Header("Narration typing SFX (dedicated instance of existing dialogue audio)")]
    [SerializeField] private bool narrationTypingSoundEnabled = true;
    [SerializeField, Range(0f, 1f)] private float narrationTypingVolume = 0.25f;
    [SerializeField] private DialogueTypingAudio narrationTypingAudio;
    private bool narrationTypingLineActive;
    [Header("Chapter 13 only: the existing player and normal tracking camera")]
    [SerializeField] private SimpleSampleCharacterControl character;
    [SerializeField] private Rigidbody body;
    [SerializeField] private CapsuleCollider capsule;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera renderingCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private CinemachineCamera normalCamera;
    [Header("Short, validated narration choreography")]
    [SerializeField, Range(.1f, 3f)] private float firstRunDistance = 2.5f;
    [SerializeField, Range(.1f, 3f)] private float secondRunDistance = 2.5f;
    [SerializeField, Range(.5f, 5f)] private float cinematicRunSpeed = 3.5f;
    [SerializeField, Range(20f, 180f)] private float lookTurnSpeed = 70f;
    [Header("Calm, scene-local ending pacing")]
    [SerializeField, Range(10f, 60f)] private float charactersPerSecond = 28f;
    [SerializeField, Min(1f)] private float passageHold = 3f;
    [SerializeField, Min(0f)] private float passageGap = 1f;
    [SerializeField, Min(.1f)] private float endingFadeDuration = 1f;
    [SerializeField, Min(1f)] private float titleHold = 4f;

    private StorySequenceToken token;
    private Coroutine routine;
    private IEnumerator runner;
    private bool attempted, ownsMovement, ownsFade, showingEnding;
    private AlaalaRollingCreditsController activeCredits;
    private enum MotionKind { Turn, Run, Settle, Hold }
    private struct MotionCue
    {
        public MotionKind kind;
        public Vector3 position;
        public Quaternion rotation;
        public float duration;
    }
    private readonly MotionCue[] motionCues = new MotionCue[8];
    private readonly RaycastHit[] sweepHits = new RaycastHit[32];
    private int cueCount, cueIndex;
    private bool narrationMotionActive, cueStarted;
    private float cueBudget;
    private string motionFailure;
    private Vector3 screenRight, firstRunEnd, secondRunEnd;
    private Collider roadSupport;

    private bool Qualified => Application.isPlaying &&
        gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity" &&
        presentation != null && presentation.isActiveAndEnabled &&
        presentation.gameObject.scene == gameObject.scene && presentation.IsPart3PresentationReady &&
        SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.CompletedFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter11.WhitePresentationFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter12.DialogueFlag) &&
        SessionStoryState.GetFlag("alaala_part3_ch12_ellipsis_complete") &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter12.CompletedFlag) &&
        whiteWall != null && whiteWall.IsWhiteCameraActive && whiteWall.HasCrossedPlayer;

    private void OnEnable() => SessionStoryState.FlagChanged += FlagChanged;
    private void FlagChanged(string key, bool value)
    {
        if (value && key == NewMakamisaPart3Chapter12.CompletedFlag) TryBegin();
    }
    private void Update()
    {
        if (narrationTypingLineActive && !CanPlayNarrationTyping()) StopNarrationTyping();
        if (showingEnding && !Qualified) { StopEnding(); return; }
        if (!attempted) TryBegin();
    }

    private void TryBegin()
    {
        if (attempted || !Qualified || journal == null || journal.IsOpen ||
            StorySequenceCoordinator.IsStorySequenceActive || focusManager == null || focusManager.IsFocusing ||
            ConversationManager.Instance == null || ConversationManager.Instance.IsConversationActive || Time.timeScale <= 0f) return;
        attempted = true;
        runner = RunEndingSafely();
        routine = StartCoroutine(runner);
    }

    private IEnumerator RunEndingSafely()
    {
        var work = new Stack<IEnumerator>();
        work.Push(PlayEnding());
        bool succeeded = false;
        try
        {
            while (work.Count > 0)
            {
                bool next = false; object yielded = null; Exception failure = null;
                try
                {
                    Require(Qualified, "Qualified Part 3 white presentation was lost.");
                    next = work.Peek().MoveNext();
                    if (next) yielded = work.Peek().Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogError("Chapter 13 stopped safely: " + failure.Message +
                        " The saved reflection and white camera were preserved. Correct the dependency, then use Retry Chapter 13 Ending.", this);
                    yield break;
                }
                if (!next) { (work.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) work.Push(nested);
                else yield return yielded;
            }
            succeeded = true;
        }
        finally
        {
            while (work.Count > 0) (work.Pop() as IDisposable)?.Dispose();
            routine = null; runner = null;
            if (!succeeded) ReleaseOwnedState();
            // A completed ending intentionally retains its one input owner on a stable credits screen.
        }
    }

    private IEnumerator PlayEnding()
    {
        Require(movementLock != null && !movementLock.OwnsConversationLock &&
            endingCanvas != null && narrationText != null && blackBackdrop != null &&
            titleText != null && subtitleText != null && completedCreditsText != null && credits != null &&
            fade != null && fade.isActiveAndEnabled && fadeGroup != null && fadeGroup.alpha <= .001f && fade.IncomingFadeCompleted,
            "Missing ending UI, movement owner or idle scene-local FadeController.");
        Require(reflections != null && !string.IsNullOrWhiteSpace(reflections.GetSubmittedResponse(ReconstructionReflections.FinalReflectionId)) &&
            SessionStoryState.GetString("journal_reflection_response:" + ReconstructionReflections.FinalReflectionId) ==
                reflections.GetSubmittedResponse(ReconstructionReflections.FinalReflectionId), "The original final reflection answer is not stored.");
        Require(charactersPerSecond >= 10f && passageHold >= 1f && passageGap >= 0f && endingFadeDuration > 0f && titleHold >= 1f,
            "Ending presentation timing is invalid.");
        if (!SessionStoryState.GetFlag(CompletedFlag))
        {
            ValidateMotionReferences();
            Require(NormalCameraReady() && character.HasGroundedIdlePresentation,
                "Narration choreography requires Miguel in grounded Idle and the existing normal camera fully restored.");
            PrepareMotionPaths(body.position); // Actual Chapter 12 endpoint; no relocation or assumed world X.
        }
        token = StorySequenceCoordinator.Acquire(this);
        movementLock.HoldMovementForStorySequence(); ownsMovement = true; showingEnding = true;
        endingCanvas.gameObject.SetActive(true);
        blackBackdrop.gameObject.SetActive(false);
        titleText.gameObject.SetActive(false); subtitleText.gameObject.SetActive(false);
        completedCreditsText.gameObject.SetActive(false); credits.gameObject.SetActive(false);
        if (SessionStoryState.GetFlag(CompletedFlag))
        {
            ShowCompletedCredits(); yield break;
        }
        narrationMotionActive = true;
        motionFailure = null;
        narrationText.gameObject.SetActive(true);
        // Same TMP maxVisibleCharacters technique as the existing dialogue typewriter,
        // without ConversationManager's speaker, camera or dialogue ownership.
        int passageIndex = 0;
        foreach (string passage in Narration)
        {
            BeginPassageMotion(passageIndex++);
            narrationText.text = passage; narrationText.maxVisibleCharacters = 0;
            narrationText.ForceMeshUpdate();
            // BeginPassageMotion has advanced this to the one-based passage number.
            if (passageIndex == 1 && endingMusic != null) endingMusic.BeginNarration();
            int total = narrationText.textInfo.characterCount;
            float revealed = 0f;
            while (narrationText.maxVisibleCharacters < total)
            {
                CheckOwnership();
                revealed += charactersPerSecond * Time.deltaTime;
                int previousVisible = narrationText.maxVisibleCharacters;
                narrationText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(revealed));
                NotifyNarrationTyping(previousVisible, narrationText.maxVisibleCharacters);
                yield return null;
            }
            StopNarrationTyping();
            yield return Pause(passageHold);
            narrationText.text = "";
            yield return Pause(passageGap);
        }
        StopNarrationMotion();
        Require(character.HasGroundedIdlePresentation &&
            character.TryValidateControlledPose(body.position, body.rotation, false, null, out _, out _, out _),
            "Miguel must finish narration on verified ground in Idle before the existing ending fade.");
        narrationText.gameObject.SetActive(false);
        ownsFade = true;
        yield return fade.FadeToBlack(endingFadeDuration);
        Require(fade.IncomingFadeCompleted && fadeGroup.alpha >= .999f, "Ending fade did not confirm opaque black.");
        // Transfer the black backdrop below the existing ESC canvas, then release the shared fade.
        blackBackdrop.gameObject.SetActive(true);
        yield return fade.FadeFromBlack(0f);
        ownsFade = false;
        Require(fade.IncomingFadeCompleted && fadeGroup.alpha <= .001f, "Ending fade did not release its transition overlay.");
        titleText.text = "ALAALA"; subtitleText.text = Subtitle;
        titleText.gameObject.SetActive(true); subtitleText.gameObject.SetActive(true);
        yield return Pause(titleHold);
        titleText.gameObject.SetActive(false); subtitleText.gameObject.SetActive(false);
        // Clone this inactive, scene-authored instance so an interrupted roll can be
        // retried without resetting the shared credit controller's one-shot state.
        activeCredits = Instantiate(credits, transform, false);
        activeCredits.name = "Chapter13 Credits Presentation";
        activeCredits.BeginCredits();
        while (activeCredits != null && !activeCredits.IsComplete) { CheckOwnership(); yield return null; }
        Require(activeCredits != null && activeCredits.IsComplete, "The owned credit presentation was removed before completion.");
        ShowCompletedCredits();
        SessionStoryState.SetFlag(CompletedFlag, true);
    }

    private void ShowCompletedCredits()
    {
        StopNarrationTyping();
        // Reached only after the owned roll confirms IsComplete, or completed-state reentry.
        if (endingMusic != null) endingMusic.CompleteCredits();
        narrationText.gameObject.SetActive(false); blackBackdrop.gameObject.SetActive(true);
        credits.gameObject.SetActive(false);
        if (activeCredits != null) { activeCredits.gameObject.SetActive(false); Destroy(activeCredits.gameObject); activeCredits = null; }
        titleText.gameObject.SetActive(false); subtitleText.gameObject.SetActive(false);
        completedCreditsText.gameObject.SetActive(false);
        // Preserve completion and music fade; the epilogue owns the post-roll pause.
        // Retain only this ending's movement hold until teardown; no unseen gameplay runs behind it.
        token?.Release(); token = null;
    }

    private bool CanPlayNarrationTyping() => isActiveAndEnabled && showingEnding &&
        narrationTypingSoundEnabled && Time.timeScale > 0f && narrationText != null &&
        narrationText.isActiveAndEnabled && narrationText.gameObject.activeInHierarchy &&
        narrationTypingAudio != null && narrationTypingAudio.isActiveAndEnabled &&
        narrationTypingAudio.gameObject.scene == gameObject.scene;

    private void NotifyNarrationTyping(int previousCount, int currentCount)
    {
        if (!CanPlayNarrationTyping()) { StopNarrationTyping(); return; }
        if (currentCount <= previousCount) return;
        if (!narrationTypingLineActive)
        {
            narrationTypingAudio.BeginNarrationLine(narrationTypingVolume);
            narrationTypingLineActive = true;
        }
        TMP_TextInfo info = narrationText.textInfo;
        for (int i = Mathf.Max(0, previousCount); i < Mathf.Min(currentCount, info.characterCount); ++i)
            narrationTypingAudio.OnCharacterRevealed(info.characterInfo[i].character);
    }

    private void StopNarrationTyping()
    {
        if (narrationTypingLineActive && narrationTypingAudio != null)
            narrationTypingAudio.EndNarrationLine();
        narrationTypingLineActive = false;
    }

    private IEnumerator Pause(float seconds)
    {
        while (seconds > 0f) { CheckOwnership(); seconds -= Time.deltaTime; yield return null; }
    }
    private void CheckOwnership()
    {
        Require(motionFailure == null, motionFailure);
        Require(Qualified && token != null && token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1 &&
            journal != null && !journal.IsOpen && focusManager != null && !focusManager.IsFocusing &&
            ConversationManager.Instance != null && !ConversationManager.Instance.IsConversationActive,
            "Ending ownership, journal closure or white rendering was interrupted.");
    }
    private void ReleaseOwnedState()
    {
        StopNarrationTyping();
        StopNarrationMotion();
        if (ownsFade && fade != null && fade.isActiveAndEnabled)
        {
            // Finish our cancelled transition through the existing zero-duration API,
            // including its completion state used by ESC. No other coroutine is stopped.
            IEnumerator releaseFade = fade.FadeFromBlack(0f);
            try { releaseFade.MoveNext(); }
            finally { (releaseFade as IDisposable)?.Dispose(); }
        }
        else if (ownsFade && fadeGroup != null) fadeGroup.alpha = 0f;
        ownsFade = false;
        if (endingCanvas != null) endingCanvas.gameObject.SetActive(false);
        if (credits != null) credits.gameObject.SetActive(false);
        if (activeCredits != null) { activeCredits.gameObject.SetActive(false); Destroy(activeCredits.gameObject); activeCredits = null; }
        if (ownsMovement && movementLock != null && !movementLock.OwnsConversationLock)
            movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
        token?.Release(); token = null; showingEnding = false;
    }
    private void StopEnding()
    {
        IEnumerator active = runner; runner = null;
        if (routine != null) StopCoroutine(routine);
        routine = null; (active as IDisposable)?.Dispose(); ReleaseOwnedState();
    }
    [ContextMenu("Retry Chapter 13 Ending")]
    private void RetryForDevelopment()
    {
#if UNITY_EDITOR
        if (Qualified && routine == null && !showingEnding && !SessionStoryState.GetFlag(CompletedFlag))
        { attempted = false; TryBegin(); }
#endif
    }
    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= FlagChanged;
        StopEnding();
    }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private bool NormalCameraReady() => renderingCamera != null && renderingCamera.isActiveAndEnabled &&
        brain != null && brain.isActiveAndEnabled && !brain.IsBlending && normalCamera != null &&
        normalCamera.isActiveAndEnabled && ReferenceEquals(brain.ActiveVirtualCamera, normalCamera);

    private void ValidateMotionReferences()
    {
        Component[] references = { character, body, capsule, animator, renderingCamera, brain, normalCamera };
        foreach (Component reference in references)
            Require(reference != null && reference.gameObject.scene == gameObject.scene,
                "Missing or cross-scene Chapter 13 player/physics/tracking-camera reference.");
        Require(body.transform == character.transform && capsule.attachedRigidbody == body &&
            animator.transform == body.transform && !body.isKinematic && body.useGravity &&
            capsule.enabled && animator.isActiveAndEnabled && !animator.applyRootMotion && normalCamera.Follow == body.transform,
            "Chapter 13 requires the existing dynamic, grounded player capsule, Animator and player-follow camera.");
        Require(firstRunDistance >= .1f && firstRunDistance <= 3f && secondRunDistance >= .1f && secondRunDistance <= 3f &&
            cinematicRunSpeed >= .5f && cinematicRunSpeed <= 5f && lookTurnSpeed >= 20f && lookTurnSpeed <= 180f,
            "Chapter 13 run distances/speeds exceed the inspected short-motion range.");
    }

    private void PrepareMotionPaths(Vector3 start)
    {
        screenRight = renderingCamera.ViewportToWorldPoint(new Vector3(1f, .5f, 10f)) -
            renderingCamera.ViewportToWorldPoint(new Vector3(0f, .5f, 10f));
        screenRight.y = 0f;
        Require(screenRight.sqrMagnitude > .5f, "The rendering camera has no usable horizontal screen basis.");
        screenRight.Normalize();
        Require(character.TryValidateControlledPose(start, body.rotation, false, null,
            out _, out roadSupport, out string reason) && roadSupport is TerrainCollider,
            reason ?? "Chapter 13 must begin on the verified terrain road after Chapter 12.");
        firstRunEnd = start - screenRight * firstRunDistance;
        secondRunEnd = firstRunEnd + screenRight * secondRunDistance;
        ValidateMotionPath(start, firstRunEnd);
        ValidateMotionPath(firstRunEnd, secondRunEnd);
    }

    private void ValidateMotionPath(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < .0001f) return;
        Quaternion facing = Quaternion.LookRotation(direction);
        int samples = Mathf.CeilToInt(direction.magnitude / .1f);
        for (int i = 0; i <= samples; ++i)
            Require(character.TryValidateControlledPose(Vector3.Lerp(from, to, (float)i / samples), facing,
                true, null, out Vector3 ground, out Collider support, out string reason) && support == roadSupport &&
                Mathf.Abs(ground.y - from.y) < .025f, reason ?? "Narration run leaves the validated flat road.");
        Vector3 scale = body.transform.lossyScale;
        float radius = capsule.radius * scale.x, half = capsule.height * scale.y * .5f;
        Vector3 center = from + facing * Vector3.Scale(capsule.center, scale);
        int hits = Physics.CapsuleCastNonAlloc(center - Vector3.up * (half - radius), center + Vector3.up * (half - radius),
            radius, direction.normalized, sweepHits, direction.magnitude, ~0, QueryTriggerInteraction.Collide);
        Require(hits < sweepHits.Length, "Narration capsule sweep exceeded its query capacity.");
        for (int i = 0; i < hits; ++i)
            Require(sweepHits[i].collider == roadSupport || sweepHits[i].collider.attachedRigidbody == body,
                "Narration run intersects collider/trigger: " + sweepHits[i].collider.name);
    }

    private void BeginPassageMotion(int passage)
    {
        // Passage lifecycle is the sole cue source. Cancel an unfinished cue at
        // its current physical pose; narration never waits for decorative motion.
        StopOwnedHorizontalMotion();
        cueCount = cueIndex = 0; cueStarted = false;
        Vector3 towardCamera = renderingCamera.transform.position - body.position; towardCamera.y = 0f;
        Require(towardCamera.sqrMagnitude > .01f, "Cannot determine a grounded look toward the existing camera.");
        towardCamera.Normalize();
        Quaternion leftLook = Quaternion.LookRotation(towardCamera - screenRight * .7f);
        Quaternion rightLook = Quaternion.LookRotation(towardCamera + screenRight * .7f);
        AddCue(new MotionCue { kind = MotionKind.Settle });
        switch (passage)
        {
            case 0:
            case 2:
                AddCue(new MotionCue { kind = MotionKind.Turn, rotation = leftLook });
                AddCue(new MotionCue { kind = MotionKind.Hold, duration = .35f });
                AddCue(new MotionCue { kind = MotionKind.Turn, rotation = rightLook });
                break;
            case 1: AddRun(firstRunEnd); break;
            case 3:
                AddRun(secondRunEnd);
                AddCue(new MotionCue { kind = MotionKind.Turn, rotation = leftLook });
                AddCue(new MotionCue { kind = MotionKind.Hold, duration = .25f });
                AddCue(new MotionCue { kind = MotionKind.Turn, rotation = rightLook });
                break;
            case 4: AddCue(new MotionCue { kind = MotionKind.Turn, rotation = Quaternion.LookRotation(towardCamera) }); break;
            // Passage six intentionally contains only grounded Idle.
        }
    }

    private void AddCue(MotionCue cue)
    {
        Require(cueCount < motionCues.Length, "Narration motion cue capacity exceeded.");
        motionCues[cueCount++] = cue;
    }
    private void AddRun(Vector3 target)
    {
        Vector3 delta = target - body.position; delta.y = 0f;
        if (delta.sqrMagnitude < .000225f) return;
        AddCue(new MotionCue { kind = MotionKind.Turn, rotation = Quaternion.LookRotation(delta) });
        AddCue(new MotionCue { kind = MotionKind.Run, position = target });
        AddCue(new MotionCue { kind = MotionKind.Settle });
    }

    private void FixedUpdate()
    {
        if (!narrationMotionActive || cueIndex >= cueCount || motionFailure != null) return;
        try
        {
            CheckOwnership();
            Require(ownsMovement && character != null && !character.enabled && body != null && !body.isKinematic &&
                animator != null && animator.isActiveAndEnabled && !animator.applyRootMotion && NormalCameraReady(),
                "Narration movement lost its exclusive disabled-controller hold or normal tracking camera.");
            Require(animator.GetBool("Grounded") && !animator.IsInTransition(0) &&
                animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash("Base Layer.Movement") &&
                animator.layerCount == 2 && !animator.IsInTransition(1) &&
                animator.GetCurrentAnimatorStateInfo(1).shortNameHash == Animator.StringToHash("Empty"),
                "Narration movement requires grounded Movement animation; no Jump/Fall/Land state is forced.");
            Require(character.TryValidateControlledPose(body.position, body.rotation, false, null,
                out _, out Collider support, out string reason) && support == roadSupport,
                reason ?? "Narration movement lost its verified road support.");
            MotionCue cue = motionCues[cueIndex];
            if (!cueStarted)
            {
                cueStarted = true;
                cueBudget = cue.kind == MotionKind.Turn ? Quaternion.Angle(body.rotation, cue.rotation) / lookTurnSpeed + 1f :
                    cue.kind == MotionKind.Run ? Vector3.Distance(body.position, cue.position) / cinematicRunSpeed + 1f :
                    cue.kind == MotionKind.Hold ? cue.duration : 1f;
                if (cue.kind == MotionKind.Run) ValidateMotionPath(body.position, cue.position);
            }
            cueBudget -= Time.fixedDeltaTime;
            bool complete = false;
            switch (cue.kind)
            {
                case MotionKind.Turn:
                    Quaternion nextRotation = Quaternion.RotateTowards(body.rotation, cue.rotation, lookTurnSpeed * Time.fixedDeltaTime);
                    Require(character.TryValidateControlledPose(body.position, nextRotation, false, null,
                        out _, out Collider turnSupport, out reason) && turnSupport == roadSupport,
                        reason ?? "Narration look-turn's next capsule pose is unsafe.");
                    body.MoveRotation(nextRotation);
                    complete = Quaternion.Angle(body.rotation, cue.rotation) <= .5f;
                    break;
                case MotionKind.Run:
                    complete = Vector3.Distance(body.position, cue.position) <= .015f;
                    if (complete) { StopOwnedHorizontalMotion(); break; }
                    Vector3 next = Vector3.MoveTowards(body.position, cue.position, cinematicRunSpeed * Time.fixedDeltaTime);
                    Require(character.TryValidateControlledPose(next, body.rotation, false, null,
                        out _, out Collider nextSupport, out reason) && nextSupport == roadSupport,
                        reason ?? "Narration run's next capsule position is unsafe.");
                    body.MovePosition(next);
                    animator.SetFloat("MoveSpeed", 1f); // Existing verified running blend.
                    break;
                case MotionKind.Settle:
                    animator.SetFloat("MoveSpeed", 0f);
                    complete = character.HasGroundedIdlePresentation && Mathf.Abs(body.linearVelocity.y) <= .1f;
                    break;
                case MotionKind.Hold: complete = cueBudget <= 0f; break;
            }
            Require(complete || cueBudget >= 0f, "Narration motion could not finish safely within its bounded cue budget.");
            if (complete) { ++cueIndex; cueStarted = false; }
        }
        catch (Exception error)
        {
            motionFailure = error.Message;
            StopNarrationMotion(); // The existing narration runner reports once and releases its owned resources.
        }
    }

    private void StopOwnedHorizontalMotion()
    {
        if (!narrationMotionActive || !ownsMovement) return;
        if (animator != null) animator.SetFloat("MoveSpeed", 0f);
        if (body != null)
        { Vector3 velocity = body.linearVelocity; velocity.x = velocity.z = 0f; body.linearVelocity = velocity; }
    }
    private void StopNarrationMotion()
    {
        StopOwnedHorizontalMotion();
        narrationMotionActive = false; cueCount = cueIndex = 0; cueStarted = false;
    }

#if UNITY_EDITOR
    public string ValidateAuthoredChoreographyForEditor(Vector3 chapter12Endpoint)
    {
        Require(!Application.isPlaying, "Authored choreography validation is Edit Mode only.");
        ValidateMotionReferences();
        PrepareMotionPaths(chapter12Endpoint);
        return $"Start {chapter12Endpoint:F6}; screen-right {screenRight:F6}; first end {firstRunEnd:F6}; second end {secondRunEnd:F6}; support {roadSupport.name}. Both full capsule sweeps and sampled ground/trigger checks passed.";
    }
#endif
}
