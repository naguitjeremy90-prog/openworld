using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Scene-local final encounter. Owns neither tasks nor the white rendering implementation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class NewMakamisaPart3Chapter11 : MonoBehaviour
{
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string ClaimFlag = "alaala_part3_ch11_owner_active";
    private const string FirstDialogueFlag = "alaala_part3_ch11_first_dialogue_complete";
    private const string FinalDialogueFlag = "alaala_part3_ch11_final_dialogue_complete";
    internal const string WhitePresentationFlag = "alaala_part3_ch11_white_presented";
    internal const string CompletedFlag = "alaala_part3_ch11_complete";

    [Header("Verified scene-local dependencies")]
    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private NewMakamisaPart3Chapter10 chase;
    [SerializeField] private NewMakamisaPart3SimpleWhiteVoid whiteWall;
    [SerializeField] private NewMakamisaPart3ChaseMusic chaseMusic;
    [SerializeField] private NewMakamisaPart3EndingMusic endingMusic;
    [SerializeField] private FadeController incomingFade;
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private Transform padre;
    [Header("Approved reusable camera setup (never edited)")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CinemachineBrain cameraBrain;
    [Header("Manual dialogue definitions")]
    [SerializeField] private NPCConversation conversation11A;
    [SerializeField] private NPCConversation conversation11B;
    [SerializeField, Min(1f), Tooltip("Temporary world +X speed only after 11A completes, until the physical wall crosses Miguel. Normal Travel Speed is never changed.")]
    private float chapter11TakeoverSpeed = 20f;
    [SerializeField, Tooltip("One-time world X for the final 11A-to-takeover staging. Must be ahead of the current wall and safely behind Miguel and the camera. Invalid values abort; no substitute is chosen.")]
    private float chapter11WallStagingX = 872f;

    private TaskManager tasks;
    private StorySequenceToken token;
    private NPCConversation waitingConversation;
    private bool running, ownsWall, ownsMovement, ownsFocus, cameraReturned;
    private bool returningCamera;
    private bool dialogueClosed, dialogueSucceeded;
    private PrioritySettings savedNormalPriority, savedFocusPriority;
    private Vector3 savedFocusPosition;
    private Quaternion savedFocusRotation, lookRotation;

    internal bool AllowsWallStaging => running && ownsWall && ownsMovement && ownsFocus &&
        Qualified && SessionStoryState.GetFlag(FirstDialogueFlag) &&
        !SessionStoryState.GetFlag(CompletedFlag) && token != null && token.IsValid &&
        ConversationManager.Instance != null && !ConversationManager.Instance.IsConversationActive;

    private bool Qualified => Application.isPlaying && isActiveAndEnabled &&
        gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity" &&
        presentation != null && presentation.isActiveAndEnabled &&
        presentation.gameObject.scene == gameObject.scene && presentation.IsPart3PresentationReady &&
        SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.ArrivalDialogueFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.FindNotificationFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.EmptyTownDialogueFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.InvestigateNotificationFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter10.DialogueFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter10.NotificationFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter10.ChaseFlag);

    private void OnTriggerEnter(Collider other)
    {
        if (!Qualified || running || ownsMovement || playerBody == null || other.attachedRigidbody != playerBody ||
            SessionStoryState.GetFlag(ClaimFlag) || SessionStoryState.GetFlag(CompletedFlag))
            return;
        try
        {
            ValidateReferences();
            tasks = TaskManager.Instance;
            Require(tasks != null && tasks.IsCurrentStage(TaskId, NewMakamisaPart3Chapter10.RunStage),
                "Expected the active part3_run_to_church stage; no task was changed.");
            if (!incomingFade.IncomingFadeCompleted || !string.IsNullOrEmpty(SpawnData.spawnPointName) ||
                Time.timeScale <= 0f || StorySequenceCoordinator.IsStorySequenceActive ||
                ConversationManager.Instance.IsConversationActive || focusManager.IsFocusing ||
                !tasks.CanPresentNotifications || tasks.HasPendingNotifications ||
                !playerMovement.isActiveAndEnabled)
                return;
            Require(!whiteWall.IsWhiteCameraActive || SessionStoryState.GetFlag(WhitePresentationFlag),
                "White mode already hid the encounter outside Chapter 11. Use the existing Reset workflow.");
            running = true;
            SessionStoryState.SetFlag(ClaimFlag, true);
            StartCoroutine(RunSafely(PlayEncounter()));
        }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            if (running) Cleanup();
        }
    }

    private void ValidateReferences()
    {
        Component[] refs = { presentation, chase, whiteWall, incomingFade, player, playerBody,
            playerMovement, movementLock, padre, focusManager, focusPoint, normalCamera,
            focusCamera, cameraBrain, conversation11A, conversation11B };
        string[] names = { "presentation", "chase", "whiteWall", "incomingFade", "player", "playerBody",
            "playerMovement", "movementLock", "padre", "focusManager", "focusPoint", "normalCamera",
            "focusCamera", "cameraBrain", "conversation11A", "conversation11B" };
        for (int i = 0; i < refs.Length; i++)
            Require(refs[i] != null && refs[i].gameObject.scene == gameObject.scene,
                "Missing or cross-scene dependency: " + names[i] + ".");
        Require(chase.isActiveAndEnabled && whiteWall.isActiveAndEnabled &&
            playerBody.transform == player && playerMovement.transform == player &&
            normalCamera.Follow == player, "Chase or Miguel movement/follow references are unavailable.");
        Require(GetComponent<BoxCollider>().isTrigger, "Encounter collider must remain a trigger.");
        Require(focusPoint.returnMode == CameraFocusPoint.ReturnMode.Manual && !focusPoint.lockPlayer,
            "Approved focus point must retain Manual return and separate movement ownership.");
        Require(ConversationManager.Instance != null && ConversationManager.Instance.isActiveAndEnabled,
            "ConversationManager is unavailable.");
        Require(padre.gameObject.activeInHierarchy, "Protected PadreAgatonFinal is not active.");
    }

    // Flatten only this chapter's routines so nested failures also run the same cleanup.
    private IEnumerator RunSafely(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                object next = null;
                bool advanced = false;
                Exception failure = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) next = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure, this); yield break; }
                if (!advanced) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            Cleanup();
        }
    }

    private IEnumerator PlayEncounter()
    {
        bool resumingWhite = SessionStoryState.GetFlag(WhitePresentationFlag) && whiteWall.IsWhiteCameraActive;
        Require(resumingWhite ? whiteWall.TryTransferHeld(whiteWall, this) : chase.TryBeginFinalEncounter(this),
            "Chapter 10 could not atomically hand over its wall hold and navigation marker.");
        ownsWall = true;
        movementLock.HoldMovementForStorySequence();
        ownsMovement = true;
        if (chaseMusic != null) chaseMusic.BeginPadreEncounter();
        playerBody.linearVelocity = Vector3.zero;
        playerBody.angularVelocity = Vector3.zero;
        Vector3 direction = padre.position - player.position;
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float turn = Mathf.Clamp(Mathf.DeltaAngle(player.eulerAngles.y, targetYaw), -35f, 35f);
        lookRotation = player.rotation * Quaternion.Euler(0f, turn, 0f);

        AcquireSequence();
        yield return FocusAndWait();
        if (!SessionStoryState.GetFlag(FirstDialogueFlag))
        {
            yield return Say(conversation11A);
            CheckSession();
            SessionStoryState.SetFlag(FirstDialogueFlag, true);
        }

        // End 11A. Retain the one movement hold and approved focus throughout takeover.
        CheckSession();
        token.Release();
        token = null;

        if (!whiteWall.IsWhiteCameraActive)
        {
            // A separate transition token retains cinematic HUD ownership, without
            // starting 11B or overlapping the completed 11A sequence.
            token = StorySequenceCoordinator.Acquire("alaala_part3_ch11_wall_takeover");
            Require(token != null && token.IsValid, "Wall-takeover story ownership was rejected.");
            Require(whiteWall.TryStageChapter11Takeover(this, chapter11WallStagingX,
                chapter11TakeoverSpeed, out string reason),
                reason ?? "The physical wall takeover was rejected.");
            float distance = Mathf.Max(0f, player.position.x - whiteWall.BoundaryX);
            // Failure watchdog only. It never triggers crossing or advances the story.
            float maximumTravelTime = 2f * distance / chapter11TakeoverSpeed + 2f;
            float travelTime = 0f;
            while (!whiteWall.IsWhiteCameraActive)
            {
                CheckSession();
                Require(whiteWall.IsChapter11TakeoverActive(this),
                    "Physical takeover stopped or crossing failed to activate the existing white camera.");
                travelTime += Time.deltaTime;
                Require(travelTime <= maximumTravelTime,
                    "Physical wall crossing exceeded its distance/speed verification budget; 11B was not started.");
                yield return null;
            }
            CheckSession();
            Require(whiteWall.HasCrossedPlayer && whiteWall.IsHeldBy(this),
                "White mode did not result from the owned physical crossing.");
            SessionStoryState.SetFlag(WhitePresentationFlag, true);
            token.Release();
            token = null;
        }
        Require(whiteWall.HasCrossedPlayer && whiteWall.IsWhiteCameraActive,
            "Sequence 11B requires a confirmed physical wall crossing and active white camera.");
        AcquireSequence();

        if (!SessionStoryState.GetFlag(FinalDialogueFlag))
        {
            yield return Say(conversation11B);
            CheckSession();
            SessionStoryState.SetFlag(FinalDialogueFlag, true);
        }
        CheckSession();
        returningCamera = true;
        Require(focusManager.TryReturnToNormal(focusPoint), "Owned camera return was rejected.");
        while (!cameraReturned) { CheckSession(); yield return null; }
        CheckSession();
        Require(focusManager.LastReturnCompleted && !cameraBrain.IsBlending &&
            ReferenceEquals(cameraBrain.ActiveVirtualCamera, normalCamera),
            "Normal camera ownership was not restored; Chapter 11 completion was not recorded.");
        Require(whiteWall.TryRetainWhitePresentation(this), "White presentation retention failed.");
        ownsWall = false;
        chase.FinishFinalEncounter(this);
        token.Release();
        token = null;
        ReleaseMovementWhenSafe();
        SessionStoryState.SetFlag(CompletedFlag, true);
        // No task mutation, wall reset, or Chapter 12 activation.
    }

    private void AcquireSequence()
    {
        Require(!StorySequenceCoordinator.IsStorySequenceActive,
            "Another Story Sequence owns the cinematic handoff.");
        token = StorySequenceCoordinator.Acquire(this);
        Require(token != null && token.IsValid, "Story Sequence ownership could not be acquired.");
    }

    private IEnumerator Say(NPCConversation conversation)
    {
        CheckSession();
        Require(!ConversationManager.Instance.IsConversationActive, "Another dialogue is already active.");
        dialogueClosed = dialogueSucceeded = false;
        waitingConversation = conversation;
        ConversationManager.ConversationClosed += HandleClosed;
        ConversationManager.Instance.StartConversation(conversation);
        Require(ConversationManager.Instance.ActiveConversation == conversation, "Encounter dialogue did not start.");
        while (!dialogueClosed)
        {
            CheckSession();
            Require(focusManager.IsFocusing, "Miguel's cinematic focus was interrupted.");
            TurnTowardPadre();
            yield return null;
        }
        ConversationManager.ConversationClosed -= HandleClosed;
        Require(dialogueSucceeded, "Dialogue was cancelled; its completion was not recorded.");
    }

    private IEnumerator FocusAndWait()
    {
        Require(cameraBrain.isActiveAndEnabled && normalCamera.isActiveAndEnabled &&
            focusCamera.isActiveAndEnabled && focusManager.isActiveAndEnabled,
            "Required Cinemachine camera, Brain or focus manager is disabled.");
        savedNormalPriority = normalCamera.Priority;
        savedFocusPriority = focusCamera.Priority;
        savedFocusPosition = focusCamera.transform.position;
        savedFocusRotation = focusCamera.transform.rotation;
        cameraReturned = false;
        returningCamera = false;
        var callback = new UnityEvent();
        callback.AddListener(OnCameraReturned);
        ownsFocus = focusManager.TryFocusOn(focusPoint, callback);
        Require(ownsFocus, "Approved reusable focus was rejected.");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (cameraBrain.IsBlending || !ReferenceEquals(cameraBrain.ActiveVirtualCamera, focusCamera))
        {
            CheckSession();
            Require(focusManager.IsFocusing && Time.realtimeSinceStartup < deadline,
                "Cinematic camera did not become live before its verification timeout.");
            yield return null;
        }
    }

    private void HandleClosed(NPCConversation conversation, bool normal)
    {
        if (waitingConversation != conversation || dialogueClosed) return;
        waitingConversation = null;
        dialogueSucceeded = normal;
        dialogueClosed = true;
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

    private void TurnTowardPadre()
    {
        var rotation = Quaternion.RotateTowards(player.rotation, lookRotation, 40f * Time.deltaTime);
        player.rotation = rotation;
        playerBody.rotation = rotation;
    }

    private void CheckSession()
    {
        Require(Qualified && chase != null && chase.isActiveAndEnabled && tasks != null &&
            tasks == TaskManager.Instance && tasks.IsCurrentStage(TaskId, NewMakamisaPart3Chapter10.RunStage) &&
            tasks.CanPresentNotifications && !tasks.HasPendingNotifications &&
            ConversationManager.Instance != null && ConversationManager.Instance.isActiveAndEnabled,
            "Arrival, chase, task stage, notification queue or dialogue service was interrupted.");
        Require(ownsWall && whiteWall != null && whiteWall.IsControlledBy(this),
            "Chapter 11 lost its wall ownership (possibly manual Reset); no other owner was changed.");
        Require(!ownsFocus || (focusManager != null && focusManager.IsFocusing),
            "Chapter 11 camera focus was interrupted.");
        Require(ownsFocus || (returningCamera && cameraReturned),
            "The owned cinematic camera returned before Chapter 11 requested it.");
        Require(player != null && playerBody != null && movementLock != null && padre != null &&
            padre.gameObject.activeInHierarchy && cameraBrain != null && cameraBrain.isActiveAndEnabled &&
            normalCamera != null && normalCamera.isActiveAndEnabled &&
            focusCamera != null && focusCamera.isActiveAndEnabled,
            "Miguel, Padre, movement hold or a required camera dependency disappeared.");
        Require(token != null && token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1,
            "Story Sequence ownership was lost or another cinematic started.");
        Require(!ConversationManager.Instance.IsConversationActive ||
            ConversationManager.Instance.ActiveConversation == waitingConversation,
            "An unrelated conversation interrupted the encounter.");
    }

    private void Cleanup()
    {
        if (!running) return;
        ConversationManager.ConversationClosed -= HandleClosed;
        var manager = ConversationManager.Instance;
        if (waitingConversation != null && manager != null && manager.ActiveConversation == waitingConversation)
            manager.EndConversation();
        waitingConversation = null;
        if (ownsFocus && focusManager != null)
        {
            if (!focusManager.TryReturnToNormal(focusPoint) && !focusManager.IsFocusing)
                OnCameraReturned();
        }
        token?.Release();
        token = null;
        ReleaseMovementWhenSafe();
        if (ownsWall && whiteWall != null && whiteWall.IsControlledBy(this))
        {
            whiteWall.CancelChapter11Takeover(this);
            whiteWall.TryHold(this);
            if (whiteWall.IsWhiteCameraActive)
            {
                whiteWall.TryRetainWhitePresentation(this);
                if (chase != null) chase.FinishFinalEncounter(this);
            }
            else if (chase == null || !chase.TryReturnFromFinalEncounter(this))
                Debug.LogWarning("Part 3 Chapter 11: encounter aborted; its wall remains held because the Chapter 10 owner could not safely resume. No other wall owner was released.", this);
        }
        ownsWall = false;
        running = false;
        SessionStoryState.SetFlag(ClaimFlag, false);
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("Part 3 Chapter 11: " + message);
    }

    private void ReleaseMovementWhenSafe()
    {
        ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
        if (!ownsMovement) return;
        // An unexpected replacement dialogue captures disabled movement. Retain this
        // hold until its own lock releases, then restore our original movement state.
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
        StopAllCoroutines();
        Cleanup();
    }

    private void OnDestroy()
    {
        ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
        if (ownsMovement && movementLock != null) movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
    }
}
