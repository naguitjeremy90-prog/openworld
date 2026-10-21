using System;
using System.Collections;
using DialogueEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Scene-local Chapters 8–9, composed from the existing story, dialogue, focus and task systems.</summary>
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3Chapters89 : MonoBehaviour
{
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string ReturnStage = "return_to_maestro_ben";
    private const string FindStage = "part3_find_anday";
    private const string InvestigateStage = "part3_investigate_quiet_town";
    private const string ClaimFlag = "alaala_part3_ch89_owner_active";
    internal const string ArrivalDialogueFlag = "alaala_part3_ch8_dialogue_complete";
    internal const string FindNotificationFlag = "alaala_part3_ch8_task_presented";
    internal const string EmptyTownDialogueFlag = "alaala_part3_ch9_dialogue_complete";
    internal const string InvestigateNotificationFlag = "alaala_part3_ch9_task_presented";

    [Header("Verified scene-local arrival and player")]
    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private FadeController incomingFade;
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField, Tooltip("Suppressed andayFINAL transform, read only as an empty-location look reference.")]
    private Transform formerAndayLocation;

    [Header("Existing camera focus infrastructure")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CinemachineBrain cameraBrain;
    [SerializeField, Tooltip("Authored world-space camera position and rotation for Chapters 8–9. Adjust Chapters89CameraFocusPoint in the scene; the sequence does not reposition it.")]
    private CameraFocusPoint focusPoint;

    [Header("Scene-local dialogue definitions")]
    [SerializeField] private NPCConversation arrivalConversation;
    [SerializeField] private NPCConversation firstCallConversation;
    [SerializeField] private NPCConversation secondCallConversation;
    [SerializeField] private NPCConversation emptyTownConversation;
    [SerializeField, Min(0f), Tooltip("Authored silence between manually completed calls; never used to time task notifications.")]
    private float callSilence = 0.8f;

    [Header("Existing task presentation")]
    [SerializeField] private TaskNotificationUI notificationUI;
    [SerializeField] private Canvas notificationCanvas;
    [SerializeField, Tooltip("Existing persistent prefab, used only if a qualified fresh Editor Part 3 test has no TaskManager.")]
    private TaskManager taskManagerPrefab;

    private TaskManager tasks;
    private StorySequenceToken storyToken;
    private NPCConversation expectedConversation;
    private bool conversationClosed;
    private bool conversationSucceeded;
    private bool ownsMovement;
    private bool ownsFocus;
    private bool ownsClaim;
    private bool dialogueEventsBound;
    private bool notificationsExposed;
    private bool cameraReturned;
    private int generation;
    private Quaternion sequenceFacing;
    private Quaternion lookRotation;
    private PrioritySettings savedNormalPriority;
    private PrioritySettings savedFocusPriority;
    private Vector3 savedFocusPosition;
    private Quaternion savedFocusRotation;
    private Transform notificationParent;
    private int notificationSibling;
    private Vector3 notificationLocalPosition;
    private Quaternion notificationLocalRotation;
    private Vector3 notificationLocalScale;
    private Vector2 notificationAnchorMin, notificationAnchorMax, notificationPivot, notificationSize;
    private Vector3 notificationAnchoredPosition;

    private bool Qualified => presentation != null && presentation.isActiveAndEnabled &&
        presentation.gameObject.scene == gameObject.scene && presentation.IsPart3PresentationReady &&
        gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity";

    private IEnumerator Start()
    {
        // Part 3 presentation Awake captures the route before PlayerSpawner consumes it.
        yield return null;
        if (!Qualified || SessionStoryState.GetFlag(InvestigateNotificationFlag))
            yield break;

        ValidateReferences();
        while (!SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) ||
               !incomingFade.IncomingFadeCompleted || !string.IsNullOrEmpty(SpawnData.spawnPointName))
        {
            Require(Qualified, "Part 3 presentation ended before transport completed.");
            yield return null;
        }
        if (SessionStoryState.GetFlag(ClaimFlag))
            yield break;
        SessionStoryState.SetFlag(ClaimFlag, true);
        ownsClaim = true;

        try
        {
            EnsureTaskManager();
            tasks.RegisterNotificationUI(notificationUI);
            Require(tasks.GetTaskState(TaskId) == TaskState.Active &&
                (tasks.IsCurrentStage(TaskId, ReturnStage) || tasks.IsCurrentStage(TaskId, FindStage) ||
                 tasks.IsCurrentStage(TaskId, InvestigateStage)),
                "Expected active five-sulatin task at return_to_maestro_ben or an existing Part 3 stage; no task progress was reset.");
            Require(!tasks.IsCurrentStage(TaskId, FindStage) || SessionStoryState.GetFlag(ArrivalDialogueFlag),
                "Part 3 find stage has no completed Chapter 8 dialogue.");
            Require(!tasks.IsCurrentStage(TaskId, InvestigateStage) || SessionStoryState.GetFlag(EmptyTownDialogueFlag),
                "Part 3 investigation stage has no completed Chapter 9 dialogue.");
            Require(!SessionStoryState.GetFlag(EmptyTownDialogueFlag) || SessionStoryState.GetFlag(FindNotificationFlag),
                "Chapter 9 state is missing the completed Chapter 8 task notification; no task stage was moved backward.");

            BindDialogueEvents();
            ExposeNotificationOnly();
            yield return WaitForFreePresentation();
            if (!SessionStoryState.GetFlag(ArrivalDialogueFlag))
            {
                yield return BeginSequence();
                yield return Say(arrivalConversation);
                SessionStoryState.SetFlag(ArrivalDialogueFlag, true);
                yield return EndSequence();
            }
            yield return UpdateObjective(FindStage, FindNotificationFlag);
            yield return WaitForFreePresentation();

            if (!SessionStoryState.GetFlag(EmptyTownDialogueFlag))
            {
                yield return BeginSequence();
                Vector3 direction = formerAndayLocation.position - player.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    float turn = Mathf.DeltaAngle(sequenceFacing.eulerAngles.y,
                        Quaternion.LookRotation(direction).eulerAngles.y);
                    lookRotation = sequenceFacing * Quaternion.Euler(0f, Mathf.Clamp(turn, -35f, 35f), 0f);
                }
                yield return Say(firstCallConversation);
                yield return Silence();
                yield return Say(secondCallConversation);
                yield return Silence();
                yield return Say(emptyTownConversation);
                SessionStoryState.SetFlag(EmptyTownDialogueFlag, true);
                yield return EndSequence();
            }
            yield return UpdateObjective(InvestigateStage, InvestigateNotificationFlag);
        }
        finally
        {
            Cleanup();
        }
    }

    private void ValidateReferences()
    {
        Component[] references = { presentation, incomingFade, player, playerBody, movementLock,
            formerAndayLocation, focusManager, normalCamera, focusCamera, cameraBrain, focusPoint,
            arrivalConversation, firstCallConversation, secondCallConversation, emptyTownConversation,
            notificationUI, notificationCanvas };
        string[] names = { "presentation", "incomingFade", "player", "playerBody", "movementLock",
            "formerAndayLocation", "focusManager", "normalCamera", "focusCamera", "cameraBrain", "focusPoint",
            "arrivalConversation", "firstCallConversation", "secondCallConversation", "emptyTownConversation",
            "notificationUI", "notificationCanvas" };
        for (int i = 0; i < references.Length; i++)
            Require(references[i] != null && references[i].gameObject.scene == gameObject.scene,
                "Missing or cross-scene reference: " + names[i] + ".");
        Require(playerBody.transform == player && normalCamera.Follow == player,
            "Player Rigidbody or normal camera follow target is mismatched.");
        Require(notificationUI.transform is RectTransform && notificationCanvas.isRootCanvas &&
            notificationCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            notificationCanvas.GetComponent<GameplayHUDTarget>() == null,
            "Task notification needs its existing unsuppressed root overlay Canvas.");
        Require(focusPoint.returnMode == CameraFocusPoint.ReturnMode.Manual && !focusPoint.lockPlayer,
            "Owned focus point must be Manual with lockPlayer disabled; DialogueMovementLock owns movement.");
    }

    private void EnsureTaskManager()
    {
#if UNITY_EDITOR
        // The existing direct Part 3 helper intentionally creates no task service.
        // Seed only an absent test task before the existing prefab restores its cache.
        if (TaskManager.Instance == null &&
            !SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.HandoffFlag))
        {
            Require(taskManagerPrefab != null, "Fresh direct Part 3 testing needs the existing TaskManager prefab.");
            Require(!SessionStoryState.GetFlag("task_active:" + TaskId) &&
                !SessionStoryState.GetFlag("task_completed:" + TaskId) &&
                string.IsNullOrEmpty(SessionStoryState.GetString("task_stage:" + TaskId)),
                "Direct Part 3 testing found task state without its manager; existing state was preserved.");
            SessionStoryState.SetFlag("task_active:" + TaskId, true);
            SessionStoryState.SetString("task_stage:" + TaskId, ReturnStage);
            try
            {
                Instantiate(taskManagerPrefab);
                Require(TaskManager.Instance != null, "Configured task prefab did not create TaskManager.");
            }
            catch
            {
                // These two keys were verified absent; rollback only this test's seed.
                SessionStoryState.SetFlag("task_active:" + TaskId, false);
                SessionStoryState.SetString("task_stage:" + TaskId, string.Empty);
                throw;
            }
        }
#endif
        tasks = TaskManager.Instance;
        Require(tasks != null, "TaskManager is missing after verified Part 3 arrival.");
    }

    private IEnumerator WaitForFreePresentation()
    {
        while (true)
        {
            CheckSession();
            if (!StorySequenceCoordinator.IsStorySequenceActive &&
                !ConversationManager.Instance.IsConversationActive && !focusManager.IsFocusing &&
                !tasks.HasPendingNotifications && Time.timeScale > 0f)
                yield break;
            yield return null;
        }
    }

    private IEnumerator BeginSequence()
    {
        yield return WaitForFreePresentation();
        Require(cameraBrain.isActiveAndEnabled && normalCamera.isActiveAndEnabled &&
            focusCamera.isActiveAndEnabled, "A required Cinemachine camera/Brain is disabled.");
        storyToken = StorySequenceCoordinator.Acquire(this);
        Require(storyToken != null && storyToken.IsValid, "Could not acquire Story Sequence ownership.");
        movementLock.HoldMovementForStorySequence();
        ownsMovement = true;
        playerBody.linearVelocity = Vector3.zero;
        playerBody.angularVelocity = Vector3.zero;
        sequenceFacing = player.rotation;
        lookRotation = sequenceFacing;
        savedNormalPriority = normalCamera.Priority;
        savedFocusPriority = focusCamera.Priority;
        savedFocusPosition = focusCamera.transform.position;
        savedFocusRotation = focusCamera.transform.rotation;

        // CameraFocusManager reads the scene-authored marker pose without modifying it.
        cameraReturned = false;
        var callback = new UnityEvent();
        callback.AddListener(OnCameraReturned);
        ownsFocus = true;
        Require(focusManager.TryFocusOn(focusPoint, callback), "Existing camera focus owner rejected the request.");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (cameraBrain.IsBlending || !ReferenceEquals(cameraBrain.ActiveVirtualCamera, focusCamera))
        {
            CheckSession();
            Require(focusManager.IsFocusing && Time.realtimeSinceStartup < deadline,
                "Cinematic camera did not become live; sequence aborted.");
            yield return null;
        }
    }

    private IEnumerator EndSequence()
    {
        Require(focusManager.TryReturnToNormal(focusPoint), "Owned cinematic camera could not return.");
        while (!cameraReturned)
        {
            CheckSession();
            yield return null;
        }
        Require(focusManager.LastReturnCompleted && !cameraBrain.IsBlending &&
            ReferenceEquals(cameraBrain.ActiveVirtualCamera, normalCamera),
            "Normal camera return was interrupted; task update was not started.");
        ReleaseStoryControls();
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

    private IEnumerator Say(NPCConversation conversation)
    {
        CheckSession();
        Require(!ConversationManager.Instance.IsConversationActive, "Another dialogue owns the presentation.");
        expectedConversation = conversation;
        conversationClosed = conversationSucceeded = false;
        ConversationManager.Instance.StartConversation(conversation);
        Require(ConversationManager.Instance.ActiveConversation == conversation, "Required dialogue did not start: " + conversation.name);
        while (!conversationClosed)
        {
            CheckSession();
            TurnGently();
            yield return null;
        }
        Require(conversationSucceeded, "Required dialogue was cancelled: " + conversation.name + ". Chapter completion was not recorded.");
    }

    private void HandleConversationClosed(NPCConversation conversation, bool normal)
    {
        if (expectedConversation != conversation || conversationClosed) return;
        expectedConversation = null; // Consume once, before any following sequence work.
        conversationSucceeded = normal;
        conversationClosed = true;
    }

    private IEnumerator Silence()
    {
        // Authored story pacing follows the existing WaitForSecondsRealtime pattern.
        float end = Time.realtimeSinceStartup + callSilence;
        while (Time.realtimeSinceStartup < end)
        {
            CheckSession();
            TurnGently();
            yield return null;
        }
    }

    private void TurnGently()
    {
        Quaternion rotation = Quaternion.RotateTowards(player.rotation, lookRotation, 40f * Time.deltaTime);
        player.rotation = rotation;
        playerBody.rotation = rotation;
    }

    private void LookLeft() => lookRotation = sequenceFacing * Quaternion.Euler(0f, -12f, 0f);
    private void LookRight() => lookRotation = sequenceFacing * Quaternion.Euler(0f, 12f, 0f);
    private void LookToRoad() => lookRotation = player.rotation * Quaternion.Euler(0f, -12f, 0f);

    private void BindDialogueEvents()
    {
        ConversationManager.ConversationClosed += HandleConversationClosed;
        arrivalConversation.GetNodeData(0).Event.AddListener(LookLeft);
        arrivalConversation.GetNodeData(1).Event.AddListener(LookRight);
        emptyTownConversation.GetNodeData(1).Event.AddListener(LookToRoad);
        dialogueEventsBound = true;
    }

    private IEnumerator UpdateObjective(string stage, string notificationFlag)
    {
        if (SessionStoryState.GetFlag(notificationFlag)) yield break;
        yield return WaitForFreePresentation();
        Require(storyToken == null && !ownsMovement && !ownsFocus,
            "Story controls must be released before updating a task.");
        int requestGeneration = generation;
        Require(tasks.AdvanceTaskStage(TaskId, stage, () =>
        {
            if (this != null && isActiveAndEnabled && generation == requestGeneration &&
                notificationsExposed && Qualified && tasks != null && tasks.IsCurrentStage(TaskId, stage))
                SessionStoryState.SetFlag(notificationFlag, true);
        }), "TaskManager rejected required Part 3 stage or its notification UI: " + stage);
        while (!SessionStoryState.GetFlag(notificationFlag) || tasks.HasPendingNotifications)
        {
            CheckSession();
            Require(tasks.IsCurrentStage(TaskId, stage), "Task stage changed during the required notification: " + stage);
            Require(tasks.HasPendingNotifications || SessionStoryState.GetFlag(notificationFlag),
                "Task notification ended without a valid completion callback: " + stage);
            yield return null;
        }
    }

    private void ExposeNotificationOnly()
    {
        RectTransform rect = (RectTransform)notificationUI.transform;
        notificationParent = rect.parent;
        notificationSibling = rect.GetSiblingIndex();
        notificationLocalPosition = rect.localPosition;
        notificationLocalRotation = rect.localRotation;
        notificationLocalScale = rect.localScale;
        notificationAnchorMin = rect.anchorMin;
        notificationAnchorMax = rect.anchorMax;
        notificationPivot = rect.pivot;
        notificationSize = rect.sizeDelta;
        notificationAnchoredPosition = rect.anchoredPosition3D;
        rect.SetParent(notificationCanvas.transform, true);
        notificationsExposed = true;
    }

    private void CheckSession()
    {
        Require(Qualified && SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag),
            "Qualified Part 3 arrival was interrupted.");
        Require(tasks != null && tasks == TaskManager.Instance && notificationUI != null &&
            notificationUI.isActiveAndEnabled && tasks.CanPresentNotifications,
            "TaskManager or TaskNotificationUI became unavailable.");
        Require(ConversationManager.Instance != null && ConversationManager.Instance.isActiveAndEnabled,
            "ConversationManager is unavailable.");
    }

    private void ReleaseStoryControls()
    {
        if (ownsMovement && movementLock != null) movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
        storyToken?.Release();
        storyToken = null;
    }

    private void Cleanup()
    {
        ++generation;
        if (dialogueEventsBound)
        {
            ConversationManager.ConversationClosed -= HandleConversationClosed;
            if (arrivalConversation != null)
            {
                arrivalConversation.GetNodeData(0).Event.RemoveListener(LookLeft);
                arrivalConversation.GetNodeData(1).Event.RemoveListener(LookRight);
            }
            if (emptyTownConversation != null) emptyTownConversation.GetNodeData(1).Event.RemoveListener(LookToRoad);
            dialogueEventsBound = false;
        }
        var manager = ConversationManager.Instance;
        if (expectedConversation != null && manager != null && manager.ActiveConversation == expectedConversation)
            manager.EndConversation();
        expectedConversation = null;
        if (ownsFocus && focusManager != null)
        {
            if (!focusManager.TryReturnToNormal(focusPoint) && !focusManager.IsFocusing)
                OnCameraReturned();
        }
        ReleaseStoryControls();
        if (notificationsExposed && notificationUI != null && notificationParent != null)
        {
            RectTransform rect = (RectTransform)notificationUI.transform;
            rect.SetParent(notificationParent, false);
            rect.SetSiblingIndex(notificationSibling);
            rect.anchorMin = notificationAnchorMin;
            rect.anchorMax = notificationAnchorMax;
            rect.pivot = notificationPivot;
            rect.sizeDelta = notificationSize;
            rect.anchoredPosition3D = notificationAnchoredPosition;
            rect.localPosition = notificationLocalPosition;
            rect.localRotation = notificationLocalRotation;
            rect.localScale = notificationLocalScale;
        }
        notificationsExposed = false;
        if (ownsClaim) SessionStoryState.SetFlag(ClaimFlag, false);
        ownsClaim = false;
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("Part 3 Chapters 8–9: " + message);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Cleanup();
    }
}
