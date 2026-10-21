using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One post-credits return, with a scene-local endpoint in Maestro Ben's house.</summary>
[DefaultExecutionOrder(-30000)]
[DisallowMultipleComponent]
public sealed class NewAlaalaPostCreditsEpilogue : MonoBehaviour
{
    internal const string CompletedFlag = "alaala_epilogue_complete";
    private const string DialogueFlag = "alaala_epilogue_dialogue_complete";
    private const string ReturnFlag = "alaala_epilogue_return_in_progress";
    private const string ReturnModeKey = "alaala_epilogue_return_mode";
    private const string HouseScene = "MaestroBenHouse";
    private const string ReturnSpawn = "AlaalaEpilogueReturnPoint";
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string FinalLine = "Kumusta ang iyong pagsisiyasat?";
    private static NewAlaalaPostCreditsEpilogue activeReturn;

    [SerializeField] private bool houseEndpoint;
    [SerializeField] private FadeController sceneFade;
    [SerializeField] private AudioSource endingMusicSource;
    [Header("House endpoint only")]
    [SerializeField] private Transform transportOrigin, player, maestro;
    [SerializeField] private GameObject mysteriousLight;
    [SerializeField] private GameObject originalMaestro;
    [SerializeField] private NPCConversationTrigger standingInteraction;
    [SerializeField] private PlayerSpawner houseSpawner;
    [SerializeField] private SimpleSampleCharacterControl movement;
    [SerializeField] private Rigidbody body;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private NPCConversation epilogueConversation;
    [SerializeField] private TaskNotificationUI completionNotification;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField] private ReconstructionJournalManager journal;
    [SerializeField] private ReconstructionReflections reflections;
    [Header("Transition safety")]
    [SerializeField, Min(0f)] private float postCreditsPause = 2f;
    [SerializeField, Min(0.1f)] private float blackFadeDuration = 1f;
    [SerializeField, Min(1f)] private float readinessTimeout = 10f;
    [SerializeField, Min(1f)] private float loadingTimeout = 30f;

    private NewAlaalaPostCreditsEpilogue house;
    private FadeController coverFade, incomingFade;
    private Canvas coverCanvas;
    private CanvasGroup coverGroup;
    private StorySequenceToken token;
    private Coroutine routine;
    private IEnumerator runner;
    private bool attempted, running, ownsMovement, waitingForDialogue, dialogueClosed, dialogueSucceeded;
    private bool ownsMaestroFacing;
    private bool ownsIncomingFade;
    private bool deferredMovementRelease;
    private DialogueMovementLock ownedMovementLock;
    private InventoryUI ownedInventoryUI;
    private Quaternion savedMaestroRotation;
    private int verifiedEntryMode; // 1: historical transport, 2: sanctioned Editor direct test.
    private AsyncOperation pendingLoad;
    private bool ownsHouseInteraction;
    private bool savedHouseInteractionEnabled;
    private bool housePrepared;

    internal static bool IsVerifiedHouseReturn(Scene scene)
    {
#if UNITY_EDITOR
        if (PostCreditsEpilogueDevelopmentTesting.IsPreparingHouse(scene)) return true;
#endif
        return scene.name == HouseScene && SessionStoryState.GetFlag(ReturnFlag) &&
        (SessionStoryState.GetInt(ReturnModeKey) == 1 || SessionStoryState.GetInt(ReturnModeKey) == 2) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter13.CompletedFlag) &&
        SessionStoryState.GetFlag(NewMakamisaPart3Chapter12.CompletedFlag) && !SessionStoryState.GetFlag(CompletedFlag);
    }

#if UNITY_EDITOR
    private PostCreditsEpilogueDevelopmentTesting developmentTesting;
    internal bool IsHouseEndpointForDevelopment => houseEndpoint;

    // Establish the real return owner before any destination HUD Awake captures state.
    // This is an Editor launch adapter, not another dialogue/completion implementation.
    internal NewAlaalaPostCreditsEpilogue PrepareDirectDevelopmentReturn(PostCreditsEpilogueDevelopmentTesting context)
    {
        Require(Application.isPlaying && houseEndpoint && context.IsActiveTest && activeReturn == null,
            "Direct epilogue startup requires the exclusive Editor test context.");
        Require(sceneFade != null && transportOrigin != null && houseSpawner != null &&
            epilogueConversation != null && completionNotification != null && reflections != null,
            "House epilogue references are incomplete.");
        var coordinator = new GameObject("Editor Epilogue Return Coordinator").AddComponent<NewAlaalaPostCreditsEpilogue>();
        try
        {
            coordinator.developmentTesting = context;
            coordinator.sceneFade = sceneFade;
            coordinator.verifiedEntryMode = 2;
            coordinator.running = true;
            activeReturn = coordinator;
            coordinator.token = StorySequenceCoordinator.Acquire("alaala_epilogue_return");
            Require(coordinator.token != null && coordinator.token.IsValid, "Editor return ownership was rejected.");
            SpawnData.spawnPointName = ReturnSpawn;
            PrepareHouseReturn();
            return coordinator;
        }
        catch
        {
            coordinator.enabled = false; // Existing ownership cleanup also releases an early incoming fade hold.
            Destroy(coordinator.gameObject);
            throw;
        }
    }

    internal FadeController HoldIncomingFadeForDevelopment()
    {
        Require(sceneFade != null, "House incoming fade is missing.");
        sceneFade.HoldBlackForSequence();
        return sceneFade;
    }

    internal void BeginDevelopmentReturn(PostCreditsEpilogueDevelopmentTesting context)
    {
        Require(Application.isPlaying && !houseEndpoint && context.IsActiveTest && runner == null && !attempted &&
            (developmentTesting == null || developmentTesting == context) && !SessionStoryState.GetFlag(CompletedFlag),
            "Editor epilogue launch was rejected or has already been attempted.");
        developmentTesting = context;
        verifiedEntryMode = 2;
        attempted = running = true;
        runner = RunSafely(PlayEpilogue());
        routine = StartCoroutine(runner);
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeReturn = null;

    private void Awake()
    {
        if (!houseEndpoint || activeReturn == null || !activeReturn.running ||
            !IsVerifiedHouseReturn(gameObject.scene)) return;
        PrepareHouseReturn();
    }

    private void PrepareHouseReturn()
    {
        // Register before holding the incoming fade so failure cleanup can always uncover it.
        activeReturn.house = this;
        activeReturn.incomingFade = sceneFade;
        if (housePrepared) return;
        // Select only this return's house actors; no historical story flags are changed.
        if (originalMaestro != null) originalMaestro.SetActive(false);
        if (maestro != null) maestro.gameObject.SetActive(true);
        if (standingInteraction != null)
        {
            if (maestro != null && standingInteraction.transform.parent != maestro)
                standingInteraction.transform.SetParent(maestro, false); // Same authored arrangement as the Part 2 return.
            standingInteraction.SetFacingTransforms(maestro, player);
            savedHouseInteractionEnabled = standingInteraction.enabled;
            standingInteraction.enabled = false;
            ownsHouseInteraction = true;
        }
        if (maestro != null && maestro.TryGetComponent<CapsuleCollider>(out var maestroCapsule)) maestroCapsule.enabled = true;
        if (transportOrigin == null || houseSpawner == null || sceneFade == null) return;
        Transform spawn = transform.Find(ReturnSpawn);
        if (spawn == null) { spawn = new GameObject(ReturnSpawn).transform; spawn.SetParent(transform, false); }
        // This is the existing authored light-transport origin, not the generic house entrance.
        spawn.SetPositionAndRotation(transportOrigin.position, transportOrigin.rotation);
        houseSpawner.enabled = true; // This scene-local spawner is otherwise disabled on ordinary house visits.
        sceneFade.HoldBlackForSequence();
        activeReturn.ownsIncomingFade = true;
        housePrepared = true;
    }

    private void OnEnable()
    {
        if (!houseEndpoint) SessionStoryState.FlagChanged += HandleFlag;
    }

    private void Start()
    {
        if (!houseEndpoint && SessionStoryState.GetFlag(NewMakamisaPart3Chapter13.CompletedFlag)) TryBegin();
    }

    private void HandleFlag(string flag, bool value)
    {
        if (value && flag == NewMakamisaPart3Chapter13.CompletedFlag) TryBegin();
    }

    private void TryBegin()
    {
        if (!Application.isPlaying || houseEndpoint || attempted || running ||
            SessionStoryState.GetFlag(CompletedFlag) || !SessionStoryState.GetFlag(NewMakamisaPart3Chapter13.CompletedFlag)) return;
        attempted = true;
        running = true;
        runner = RunSafely(PlayEpilogue());
        routine = StartCoroutine(runner);
    }

    private IEnumerator RunSafely(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(work);
        bool success = false;
        try
        {
            while (stack.Count > 0)
            {
                object next = null; bool advanced = false; Exception failure = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) next = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogError("ALAALA epilogue stopped safely: " + failure.Message +
                        " No unfinished epilogue was marked complete. Correct the dependency and use Retry Post-Credits Epilogue.", this);
                    yield break;
                }
                if (!advanced) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            success = true;
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            runner = null; routine = null;
            ReleaseOwnedState();
            if (success) Destroy(gameObject);
        }
    }

    private IEnumerator PlayEpilogue()
    {
        Require(pendingLoad == null || pendingLoad.isDone, "The previous house load is still pending; retry after its completion.");
        Require(float.IsFinite(blackFadeDuration) && blackFadeDuration > 0f &&
            float.IsFinite(postCreditsPause) && postCreditsPause >= 0f &&
            float.IsFinite(readinessTimeout) && readinessTimeout >= 1f && float.IsFinite(loadingTimeout) && loadingTimeout >= 1f,
            "Epilogue transition durations must be finite and positive.");
#if UNITY_EDITOR
        // The explicit session-only test ticket substitutes for chapter provenance;
        // no chapter/credits completion flags are fabricated.
        bool editorLaunch = developmentTesting != null && developmentTesting.IsActiveTest;
        if (!editorLaunch)
#endif
        Require(SessionStoryState.GetFlag(NewMakamisaPart3Chapter12.CompletedFlag) &&
            SessionStoryState.GetFlag(NewMakamisaPart3Chapter13.CompletedFlag), "Genuine Chapter 12/credits completion is missing.");
        if (verifiedEntryMode == 0)
        {
            bool direct = false;
            foreach (var testing in FindObjectsByType<NewMakamisaPart3DevelopmentTesting>(FindObjectsInactive.Exclude))
                if (testing.gameObject.scene == gameObject.scene && testing.HasVerifiedDirectArrival) direct = true;
            bool transported = SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag);
            bool arrived = SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag);
            bool historical = SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.HandoffFlag) &&
                SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ReactionFlag);
            Require(transported && arrived && (direct || historical),
                $"Return provenance rejected: transport={transported}, arrival={arrived}, historicalHandoffAndReaction={historical}, sanctionedDirectArrival={direct}.");
            verifiedEntryMode = direct ? 2 : 1;
        }
        bool handoffUnowned = !StorySequenceCoordinator.IsStorySequenceActive;
#if UNITY_EDITOR
        handoffUnowned |= editorLaunch && activeReturn == this && token != null && token.IsValid &&
            StorySequenceCoordinator.ActiveOwnerCount == 1;
#endif
        Require(handoffUnowned && Time.timeScale > 0f &&
            ConversationManager.Instance != null && !ConversationManager.Instance.IsConversationActive,
            "Another cinematic, conversation or pause owns the handoff.");
        Require(Application.CanStreamedLevelBeLoaded(HouseScene), "MaestroBenHouse is unavailable in build scenes.");
        Require(endingMusicSource != null || SceneManager.GetActiveScene().name == HouseScene,
            "The dedicated ending AudioSource reference is missing; its completion fade cannot be observed safely.");
        TaskManager tasks = TaskManager.Instance;
        ValidateTask(tasks);
        string finalAnswer = SessionStoryState.GetString("journal_reflection_response:" + ReconstructionReflections.FinalReflectionId);
        Require(!string.IsNullOrWhiteSpace(finalAnswer), "The submitted final Pagninilay answer is missing.");
        Require(activeReturn == null || activeReturn == this, "Another epilogue return already owns the transition.");
        activeReturn = this;
#if UNITY_EDITOR
        if (!editorLaunch || token == null)
#endif
        token = StorySequenceCoordinator.Acquire("alaala_epilogue_return");
        Require(token != null && token.IsValid, "Return transition ownership was rejected.");
        DontDestroyOnLoad(gameObject);
        float remainingPause = postCreditsPause;
        while (remainingPause > 0f)
        {
            Require(token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1, "Post-credits pause lost transition ownership.");
            if (Time.timeScale > 0f) remainingPause -= Time.deltaTime;
            yield return null;
        }
        PrepareBlackCover();
        yield return coverFade.FadeToBlack(blackFadeDuration);
        Require(coverFade.IncomingFadeCompleted && coverGroup.alpha >= .999f, "The return fade did not reach opaque black.");
        // Chapter 13 already requested the approved music fade. Observe it; never stop that source.
        yield return WaitFor(() => endingMusicSource == null || !endingMusicSource.isPlaying,
            readinessTimeout, "The ending music did not finish its existing credits-completion fade.");
        if (SceneManager.GetActiveScene().name != HouseScene)
        {
            Require(string.IsNullOrEmpty(SpawnData.spawnPointName), "Another scene-entry route is pending.");
            SessionStoryState.SetInt(ReturnModeKey, verifiedEntryMode);
            SessionStoryState.SetFlag(ReturnFlag, true);
            SpawnData.spawnPointName = ReturnSpawn;
            pendingLoad = SceneManager.LoadSceneAsync(HouseScene);
            Require(pendingLoad != null, "MaestroBenHouse scene loading was rejected.");
            yield return WaitFor(() => pendingLoad.isDone, loadingTimeout, "MaestroBenHouse loading timed out.");
        }
        else
        {
            SessionStoryState.SetInt(ReturnModeKey, verifiedEntryMode);
            SessionStoryState.SetFlag(ReturnFlag, true);
        }
        if (house == null)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var candidate = root.GetComponent<NewAlaalaPostCreditsEpilogue>();
                if (candidate != null && candidate.houseEndpoint) { house = candidate; break; }
            }
        }
        Require(house != null && house.isActiveAndEnabled, "The MaestroBenHouse epilogue endpoint is missing or disabled.");
        if (!house.housePrepared)
        {
            Require(string.IsNullOrEmpty(SpawnData.spawnPointName), "Another house spawn route owns epilogue retry.");
            // A previously timed-out load may have finished after failure cleanup.
            // Use the same native spawner, never a guessed fallback teleport.
            SpawnData.spawnPointName = ReturnSpawn;
            house.PrepareHouseReturn();
        }
        house.ValidateHouse(verifiedEntryMode);
        incomingFade = house.sceneFade;
        yield return WaitFor(() => string.IsNullOrEmpty(SpawnData.spawnPointName), readinessTimeout,
            "PlayerSpawner did not consume the exact transport-origin return route.");
        int stable = 0; float previousPhysicsTime = Time.fixedTime;
        yield return WaitFor(() =>
        {
            if (Time.fixedTime <= previousPhysicsTime) return false;
            previousPhysicsTime = Time.fixedTime;
            stable = house.movement.HasGroundedIdlePresentation && !house.movement.HasPendingControlledIntent &&
                Mathf.Abs(house.body.linearVelocity.y) <= .1f ? stable + 1 : 0;
            return stable >= 2;
        }, readinessTimeout, "The house player did not settle into verified grounded Idle.");
        Require(house.movement.TryValidateControlledPose(house.body.position, house.body.rotation, false, null,
            out _, out _, out string placementReason), placementReason);
        Require(SessionStoryState.GetFlag(DialogueFlag) || Vector2.Distance(new Vector2(house.player.position.x, house.player.position.z),
            new Vector2(house.transportOrigin.position.x, house.transportOrigin.position.z)) < .05f,
            "PlayerSpawner did not place Miguel at the original transport point.");
        Require(house.reflections.GetSubmittedResponse(ReconstructionReflections.FinalReflectionId) == finalAnswer,
            "The house journal did not restore the final Pagninilay answer.");
        if (ownsIncomingFade)
        {
            yield return incomingFade.FadeFromBlack(0f); // Persistent cover still hides this readiness release.
            Require(incomingFade.IncomingFadeCompleted, "The owned house fade did not release its black hold.");
            ownsIncomingFade = false;
        }
        else
            yield return WaitFor(() => incomingFade.IncomingFadeCompleted, readinessTimeout,
                "Another house transition still owns the incoming fade.");
        yield return coverFade.FadeFromBlack(blackFadeDuration);
        Require(coverFade.IncomingFadeCompleted && coverGroup.alpha <= .001f, "The house reveal did not finish.");
        coverCanvas.gameObject.SetActive(false);

        ownedMovementLock = house.movementLock; ownedInventoryUI = house.inventoryUI;
        ownedMovementLock.HoldMovementForStorySequence(); ownsMovement = true;
        // Keep HUD suppression continuous across the return-to-dialogue handoff.
        // Releasing the last owner first starts a restore fade; reacquiring then
        // captures that partial alpha as the HUD's new normal presentation.
        StorySequenceToken finalLineToken = StorySequenceCoordinator.Acquire("alaala_epilogue_final_line");
        Require(finalLineToken != null && finalLineToken.IsValid, "Final Story Sequence ownership was rejected.");
        token.Release(); token = finalLineToken;
        Require(token != null && token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1,
            "Final Story Sequence ownership was rejected.");
        if (!SessionStoryState.GetFlag(DialogueFlag))
        {
            yield return FaceEachOther();
            dialogueClosed = dialogueSucceeded = false;
            waitingForDialogue = true;
            ConversationManager.ConversationClosed += HandleConversationClosed;
            ConversationManager.Instance.StartConversation(house.epilogueConversation);
            Require(ConversationManager.Instance.ActiveConversation == house.epilogueConversation, "The exact epilogue conversation did not start.");
            while (!dialogueClosed) { CheckHouseOwnership(); yield return null; }
            Require(dialogueSucceeded, "Maestro Ben's final line was interrupted, not dismissed.");
            SessionStoryState.SetFlag(DialogueFlag, true);
        }
        Require(!ConversationManager.Instance.IsConversationActive && !house.movementLock.OwnsConversationLock,
            "The final conversation has not released its dialogue ownership.");
        RestoreMaestroFacing();
        token.Release(); token = null; // Completion feedback is non-cinematic and follows the dismissed line.
        ValidateTask(tasks);
        Require(tasks.CanPresentNotifications && house.completionNotification.isActiveAndEnabled,
            "The house's real task completion notification is unavailable.");
        yield return WaitFor(() => !tasks.HasPendingNotifications, readinessTimeout, "A previous task notification did not finish.");
        if (tasks.GetTaskState(TaskId) != TaskState.Completed)
            Require(tasks.CompleteTask(TaskId), "TaskManager rejected the existing main task completion.");
        Require(tasks.GetTaskState(TaskId) == TaskState.Completed, "The real main task did not confirm completion.");
        // Discover only after the manually dismissed epilogue and verified native task completion.
        house.journal.UnlockPerson("padre_agaton");
        yield return WaitFor(() => !tasks.HasPendingNotifications, readinessTimeout, "The main task completion notification did not finish.");
        // A player may open Inventory once the Story Sequence has ended. Let that
        // interface release its captured input state before releasing our earlier hold.
        while (ownedInventoryUI != null && ownedInventoryUI.IsOpen)
        {
            Require(house != null && house.isActiveAndEnabled, "The house endpoint was removed during inventory review.");
            yield return null;
        }
#if UNITY_EDITOR
        if (verifiedEntryMode == 2)
        {
            // The sanctioned direct launch has no earlier UI-unlock history.
            // Reuse its existing session-only development API, never quest/history flags.
            foreach (GameplaySystemId system in Enum.GetValues(typeof(GameplaySystemId)))
                GameplaySystemTutorialManager.Instance?.SuppressTutorialForDevelopment(system);
            GameplaySystemState.SetDevelopmentUnlocks(true, true, true);
        }
#endif
        Require(!house.inventoryUI.GameplayShortcutSuppressed && !house.clarityManager.GameplayShortcutSuppressed &&
            GameplaySystemState.IsUnlocked(GameplaySystemId.Inventory) && GameplaySystemState.IsUnlocked(GameplaySystemId.Journal) &&
            GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity) && !house.focusManager.IsFocusing,
            "Normal house UI access or camera ownership has not been restored.");
        ownedMovementLock.ReleaseStorySequenceHold(); ownsMovement = false;
        Require(house.movement.isActiveAndEnabled && !house.movementLock.OwnsConversationLock &&
            !StorySequenceCoordinator.IsStorySequenceActive && !ConversationManager.Instance.IsConversationActive,
            "Normal free-roam controls are still owned by another presentation.");
        house.ReleaseHouseInteraction();
        SessionStoryState.SetFlag(CompletedFlag, true);
    }

    private static void ValidateTask(TaskManager tasks)
    {
        Require(tasks != null, "TaskManager is missing.");
        if (SessionStoryState.GetFlag(DialogueFlag) && tasks.GetTaskState(TaskId) == TaskState.Completed) return;
        bool hasMain = tasks.TryGetActiveTask(TaskType.Main, out TaskData definition, out _);
        Require(hasMain && definition != null && definition.TaskId == TaskId &&
            tasks.IsCurrentStage(TaskId, NewMakamisaPart3Chapter10.RunStage),
            $"Expected {TaskId} at final part3_run_to_church stage; state={tasks.GetTaskState(TaskId)}, " +
            $"activeMain={(hasMain && definition != null ? definition.TaskId : "none")}, sessionStage={SessionStoryState.GetString("task_stage:" + TaskId)}.");
    }

    private void ValidateHouse(int returnMode)
    {
        Component[] refs = { sceneFade, transportOrigin, player, maestro, mysteriousLight != null ? mysteriousLight.transform : null, houseSpawner, movement, body,
            movementLock, focusManager, epilogueConversation, completionNotification, inventoryUI, clarityManager, journal, reflections,
            originalMaestro != null ? originalMaestro.transform : null, standingInteraction };
        string[] names = { "sceneFade", "transportOrigin", "player", "maestro", "mysteriousLight", "houseSpawner", "movement", "body",
            "movementLock", "focusManager", "epilogueConversation", "completionNotification", "inventoryUI", "clarityManager", "journal", "reflections", "originalMaestro", "standingInteraction" };
        for (int i = 0; i < refs.Length; ++i)
            Require(refs[i] != null && refs[i].gameObject.scene == gameObject.scene,
                "Missing or cross-scene house dependency: " + names[i] + ".");
        Require(gameObject.scene.name == HouseScene && houseSpawner.isActiveAndEnabled && movement.isActiveAndEnabled && player == body.transform &&
            movement.transform == player && !body.isKinematic && body.useGravity && maestro.gameObject.activeInHierarchy &&
            !focusManager.IsFocusing, "House player physics, standing Maestro Ben or the normal camera is unavailable.");
        Require(transportOrigin.GetComponent<MaestroBenSulatinLightEncounter>() != null,
            $"Authored transport-origin component is missing on {transportOrigin.name} in {gameObject.scene.name}.");
        bool transported = SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag);
        bool arrived = SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag);
        Require(returnMode == 2 || (returnMode == 1 && transported && arrived),
            $"House return provenance rejected: mode={returnMode}, transport={transported}, arrival={arrived}. Historical transport flags are required for full-story returns.");
        Require(!mysteriousLight.activeInHierarchy && transportOrigin.GetComponent<Collider>() != null &&
            !transportOrigin.GetComponent<Collider>().enabled, "The original mysterious light or transport trigger is still active.");
        EditableConversation definition = epilogueConversation.DeserializeForEditor();
        Require(definition != null && definition.SpeechNodes.Count == 1 && definition.Options.Count == 0 &&
            definition.GetRootNode() != null && definition.SpeechNodes[0].Text == FinalLine &&
            definition.SpeechNodes[0].Name == "Maestro Ben" && !definition.SpeechNodes[0].AdvanceDialogueAutomatically &&
            definition.SpeechNodes[0].Connections.Count == 0 && definition.SpeechNodes[0].ParamActions.Count == 0,
            "The epilogue must contain only Maestro Ben's exact manually dismissed line, without choices or progression actions.");
    }

    private void PrepareBlackCover()
    {
        if (coverCanvas != null) { coverCanvas.gameObject.SetActive(true); return; }
        Require(sceneFade != null, "The scene-local black FadeController template is missing.");
        Canvas template = sceneFade.GetComponentInParent<Canvas>(true);
        Require(template != null, "The existing black fade canvas is missing.");
        coverCanvas = Instantiate(template, transform, false);
        coverCanvas.name = "Epilogue Black Transition";
        coverCanvas.enabled = false;
        coverFade = coverCanvas.GetComponentInChildren<FadeController>(true);
        coverGroup = coverFade != null ? coverFade.GetComponent<CanvasGroup>() : null;
        Require(coverFade != null && coverGroup != null, "The cloned black fade configuration is incomplete.");
        coverFade.HoldBlackForSequence();
        coverGroup.alpha = 0f;
        coverCanvas.enabled = true;
    }

    private IEnumerator WaitFor(Func<bool> ready, float timeout, string failure)
    {
        float elapsed = 0f;
        while (!ready())
        {
            Require(isActiveAndEnabled, "The epilogue coordinator was disabled.");
            Require(token == null || (token.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1),
                "Another cinematic interrupted epilogue transition ownership.");
            Require(elapsed < timeout, failure);
            yield return null;
            if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
        }
    }

    private IEnumerator FaceEachOther()
    {
        Vector3 towardMaestro = house.maestro.position - house.player.position; towardMaestro.y = 0f;
        Require(towardMaestro.sqrMagnitude > .01f, "Miguel and Maestro Ben have no usable facing direction.");
        savedMaestroRotation = house.maestro.rotation; ownsMaestroFacing = true;
        Quaternion miguelTarget = Quaternion.LookRotation(towardMaestro);
        Quaternion maestroTarget = Quaternion.LookRotation(-towardMaestro);
        float budget = 4f;
        while (Quaternion.Angle(house.player.rotation, miguelTarget) > .5f || Quaternion.Angle(house.maestro.rotation, maestroTarget) > .5f)
        {
            CheckHouseOwnership();
            Require(budget > 0f, "Mutual facing did not finish within its turn budget.");
            house.body.rotation = Quaternion.RotateTowards(house.body.rotation, miguelTarget, 90f * Time.deltaTime);
            house.player.rotation = house.body.rotation;
            house.maestro.rotation = Quaternion.RotateTowards(house.maestro.rotation, maestroTarget, 90f * Time.deltaTime);
            budget -= Time.deltaTime; yield return null;
        }
    }

    private void CheckHouseOwnership()
    {
        Require(house != null && house.isActiveAndEnabled && token != null && token.IsValid &&
            StorySequenceCoordinator.ActiveOwnerCount == 1 && house.movementLock != null && ownsMovement &&
            !house.movement.enabled && ConversationManager.Instance != null,
            "The final Story Sequence lost its house, movement or conversation ownership.");
        Require(!ConversationManager.Instance.IsConversationActive ||
            (waitingForDialogue && ConversationManager.Instance.ActiveConversation == house.epilogueConversation),
            "An unrelated conversation interrupted the epilogue.");
        Require(!waitingForDialogue || dialogueClosed || ConversationManager.Instance.ActiveConversation == house.epilogueConversation,
            "The epilogue conversation disappeared without its dismissal callback.");
    }

    private void HandleConversationClosed(NPCConversation conversation, bool normal)
    {
        if (!waitingForDialogue || house == null || conversation != house.epilogueConversation) return;
        waitingForDialogue = false; dialogueClosed = true; dialogueSucceeded = normal;
        ConversationManager.ConversationClosed -= HandleConversationClosed;
    }

    private void RestoreMaestroFacing()
    {
        if (ownsMaestroFacing && house != null && house.maestro != null) house.maestro.rotation = savedMaestroRotation;
        ownsMaestroFacing = false;
    }

    private static void Uncover(FadeController fade)
    {
        if (fade == null) return;
        // Run the zero-duration API directly, including when premature disable
        // has stopped normal fade updates, so our owned overlay cannot stay black.
        IEnumerator work = fade.FadeFromBlack(0f);
        try { work.MoveNext(); } finally { (work as IDisposable)?.Dispose(); }
    }

    private void ReleaseOwnedState()
    {
        ConversationManager.ConversationClosed -= HandleConversationClosed;
        if (waitingForDialogue && house != null && ConversationManager.Instance != null &&
            ConversationManager.Instance.ActiveConversation == house.epilogueConversation) ConversationManager.Instance.EndConversation();
        waitingForDialogue = false;
        RestoreMaestroFacing();
        if (house != null) house.ReleaseHouseInteraction();
        if (ownsMovement && ownedMovementLock != null)
        {
            if (ownedMovementLock.OwnsConversationLock || (ownedInventoryUI != null && ownedInventoryUI.IsOpen))
            {
                // Keep the same hold until the replacement modal releases its captured state.
                deferredMovementRelease = true;
                ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
                ConversationManager.ConversationClosed += ReleaseDeferredMovement;
                if (ownedInventoryUI != null)
                {
                    ownedInventoryUI.InventoryClosed -= HandleInventoryClosed;
                    ownedInventoryUI.InventoryClosed += HandleInventoryClosed;
                }
            }
            else ownedMovementLock.ReleaseStorySequenceHold();
        }
        ownsMovement = false;
        token?.Release(); token = null;
        if (ownsIncomingFade) Uncover(incomingFade);
        ownsIncomingFade = false;
        Uncover(coverFade);
        if (coverCanvas != null) coverCanvas.gameObject.SetActive(false);
        if (SpawnData.spawnPointName == ReturnSpawn) SpawnData.spawnPointName = "";
        if (activeReturn == this)
        {
            activeReturn = null;
            // A timed-out Unity load cannot be cancelled. Keep the entry guard until
            // destination Awake has run, then clear it; never expose the first-visit Timeline.
            if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.completed += ClearReturnContext;
            else ClearReturnContext(null);
        }
        running = false;
    }

    private static void ClearReturnContext(AsyncOperation _) { SessionStoryState.SetFlag(ReturnFlag, false); SessionStoryState.SetInt(ReturnModeKey, 0); }

    private void ReleaseHouseInteraction()
    {
        if (!ownsHouseInteraction) return;
        if (standingInteraction != null) standingInteraction.enabled = savedHouseInteractionEnabled;
        ownsHouseInteraction = false;
    }

    [ContextMenu("Retry Post-Credits Epilogue")]
    private void RetryForDevelopment()
    {
#if UNITY_EDITOR
        if (Application.isPlaying && !houseEndpoint && !running && !SessionStoryState.GetFlag(CompletedFlag))
        { attempted = false; TryBegin(); }
#endif
    }

    private void ReleaseDeferredMovement(NPCConversation conversation, bool normal)
    {
        if (!deferredMovementRelease || (ownedMovementLock != null && ownedMovementLock.OwnsConversationLock) ||
            (ownedInventoryUI != null && ownedInventoryUI.IsOpen)) return;
        if (ownedMovementLock != null) ownedMovementLock.ReleaseStorySequenceHold();
        deferredMovementRelease = false;
        ConversationManager.ConversationClosed -= ReleaseDeferredMovement;
        if (ownedInventoryUI != null) ownedInventoryUI.InventoryClosed -= HandleInventoryClosed;
    }

    private void HandleInventoryClosed(InventoryUI closed) => ReleaseDeferredMovement(null, false);

    private void OnDisable()
    {
        if (houseEndpoint) return;
        SessionStoryState.FlagChanged -= HandleFlag;
        IEnumerator work = runner; runner = null;
        if (routine != null) StopCoroutine(routine);
        routine = null; (work as IDisposable)?.Dispose();
        ReleaseOwnedState();
    }

    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
}
