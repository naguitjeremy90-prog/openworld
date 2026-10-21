using UnityEngine;

/// <summary>Opt-in, fresh Editor launch only. Never grants chapter completion or saves player data.</summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class PostCreditsEpilogueDevelopmentTesting : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField, InspectorName("Start At Final Epilogue For Testing")]
    [Tooltip("Editor only. Start from MaestroBenHouse with Domain Reload and Scene Reload enabled. Creates a temporary test session; does not replay chapters or credits.")]
    private bool startAtFinalEpilogueForTesting = false;
    [SerializeField, InspectorName("Use NEWMAKAMISA Return Transition For Testing")]
    [Tooltip("Requires Start At Final Epilogue For Testing. Initializes Part 3 presentation in NEWMAKAMISA, then runs the real return. Does not replay chapters or credits, or promise identical prior UI history.")]
    private bool useNewMakamisaReturnTransitionForTesting = false;
    [SerializeField] private NewAlaalaPostCreditsEpilogue houseEndpoint;
    [SerializeField] private TaskManager taskManagerPrefab;
    [SerializeField] private InventoryManager inventoryManagerPrefab;

    private const string HousePath = "Assets/Scenes/MaestroBenHouse.unity";
    private const string LaunchKey = "ALAALA.FinalEpilogueTest.Launch";
    private const string TaskId = "main_collect_remaining_sulatin";
    private static bool startupWindow;
    private static PostCreditsEpilogueDevelopmentTesting active;
    private bool initialized, sourceConfigured;
    private NewAlaalaPostCreditsEpilogue coordinator;
    private StorySequenceToken bootstrapToken;
    private FadeController bootstrapFade;
    private NewMakamisaPart3PopulationPresentation sourcePresentation;
    private FadeController sourceFade;
    private Supercyan.FreeSample.SimpleSampleCharacterControl sourcePlayer;
    private CameraFocusManager sourceFocus;
    private Unity.Cinemachine.CinemachineCamera sourceCamera;
    private Unity.Cinemachine.CinemachineBrain sourceBrain;
    private readonly System.Collections.Generic.Dictionary<string, bool> flags = new System.Collections.Generic.Dictionary<string, bool>();
    private readonly System.Collections.Generic.Dictionary<string, string> strings = new System.Collections.Generic.Dictionary<string, string>();
    private int savedReturnMode;

    internal bool IsActiveTest => Application.isPlaying && initialized && active == this;
    internal static bool IsPreparingHouse(UnityEngine.SceneManagement.Scene scene) => active != null &&
        active.IsActiveTest && scene.path == HousePath && !SessionStoryState.GetFlag(NewAlaalaPostCreditsEpilogue.CompletedFlag);

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterLaunchCapture()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= EditorStateChanged;
        UnityEditor.EditorApplication.playModeStateChanged += EditorStateChanged;
    }

    private static void EditorStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            var start = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
            string path = start != null ? UnityEditor.AssetDatabase.GetAssetPath(start) : UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            UnityEditor.SessionState.SetBool(LaunchKey, path == HousePath && ReloadsEnabled() &&
                (start != null || UnityEngine.SceneManagement.SceneManager.sceneCount == 1));
        }
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            if (active != null) active.Cleanup();
            UnityEditor.SessionState.EraseBool(LaunchKey);
        }
    }

    private static bool ReloadsEnabled() => !UnityEditor.EditorSettings.enterPlayModeOptionsEnabled ||
        (UnityEditor.EditorSettings.enterPlayModeOptions & (UnityEditor.EnterPlayModeOptions.DisableDomainReload |
        UnityEditor.EnterPlayModeOptions.DisableSceneReload)) == 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; startupWindow = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OpenStartupWindow()
    {
        startupWindow = UnityEditor.SessionState.GetBool(LaunchKey, false);
        UnityEditor.SessionState.EraseBool(LaunchKey);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += CloseStartupWindow;
    }

    private static void CloseStartupWindow(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        startupWindow = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= CloseStartupWindow;
    }

    private void Awake()
    {
        if (!Application.isPlaying || !startAtFinalEpilogueForTesting || active != null) return;
        if (!startupWindow || !ReloadsEnabled() || gameObject.scene.path != HousePath ||
            UnityEngine.SceneManagement.SceneManager.sceneCount != 1)
        { Debug.LogWarning("Final epilogue testing requires a fresh MaestroBenHouse launch with Domain Reload and Scene Reload enabled. No test state was applied.", this); return; }
        startupWindow = false;
        if (TaskManager.Instance != null || InventoryManager.Instance != null || StorySequenceCoordinator.IsStorySequenceActive ||
            !string.IsNullOrEmpty(SpawnData.spawnPointName) || SessionStoryState.GetFlag(NewAlaalaPostCreditsEpilogue.CompletedFlag) ||
            SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag) ||
            SessionStoryState.GetFlag(NewMakamisaPart3Chapter13.CompletedFlag))
        { Debug.LogError("Final epilogue test rejected an existing session or transition; existing state was preserved.", this); return; }
        if (houseEndpoint == null || !houseEndpoint.IsHouseEndpointForDevelopment || houseEndpoint.gameObject.scene != gameObject.scene ||
            taskManagerPrefab == null || !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(taskManagerPrefab) ||
            inventoryManagerPrefab == null || !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(inventoryManagerPrefab) ||
            (useNewMakamisaReturnTransitionForTesting && !Application.CanStreamedLevelBeLoaded("NEWMAKAMISA")))
        { Debug.LogError("Final epilogue test dependencies are missing; no test state was applied.", this); return; }

        initialized = true; active = this;
        DontDestroyOnLoad(gameObject);
        try
        {
            // Snapshot only keys this fresh test owns. SessionStoryState is memory-only.
            RememberFlag("alaala_epilogue_development_test", true);
            RememberFlag(MaestroBenEncounterController.IntroSeenFlag, true); // Also makes later ordinary reentry usable for Capture B.
            RememberFlag("maestro_ben_lead_received", true); // Existing DraftWorld door prerequisite, only for this test's ordinary Capture B route.
            RememberFlag("task_active:" + TaskId, true);
            RememberFlag("task_completed:" + TaskId, false); // Completed later only by the real epilogue.
            RememberString("task_stage:" + TaskId, NewMakamisaPart3Chapter10.RunStage);
            RememberFlag("journal_reflection_unlocked:" + ReconstructionReflections.FinalReflectionId, true);
            RememberString("journal_reflection_response:" + ReconstructionReflections.FinalReflectionId, "[EDITOR TEST ONLY] Final epilogue HUD diagnostic response.");
            RememberFlag(NewAlaalaPostCreditsEpilogue.CompletedFlag, false);
            RememberFlag("alaala_epilogue_dialogue_complete", false);
            RememberFlag("alaala_epilogue_return_in_progress", false);
            savedReturnMode = SessionStoryState.GetInt("alaala_epilogue_return_mode");
            foreach (GameplaySystemId system in System.Enum.GetValues(typeof(GameplaySystemId)))
            {
                RememberFlag(GameplaySystemState.GetUnlockFlagId(system), true);
                GameplaySystemTutorialManager.Instance.SuppressTutorialForDevelopment(system);
            }
            GameplaySystemState.SetDevelopmentUnlocks(true, true, true);
            Instantiate(taskManagerPrefab); // Exactly one existing service prefab; never a replacement TaskManager implementation.
            // Match existing direct-scene testing bootstraps: supply the missing native
            // backend, with no collected-item seeding or inventory behavior changes.
            if (InventoryManager.Instance == null) Instantiate(inventoryManagerPrefab.gameObject);
            if (!TaskManager.Instance.IsCurrentStage(TaskId, NewMakamisaPart3Chapter10.RunStage))
                throw new System.InvalidOperationException("The existing task prefab did not restore the final main-task stage.");
            if (useNewMakamisaReturnTransitionForTesting)
            {
                bootstrapToken = StorySequenceCoordinator.Acquire("EditorFinalEpilogueBootstrap");
                bootstrapFade = houseEndpoint.HoldIncomingFadeForDevelopment();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += ConfigureSource;
            }
            else coordinator = houseEndpoint.PrepareDirectDevelopmentReturn(this);
            Debug.Log("[FinalEpilogueTest] " + (useNewMakamisaReturnTransitionForTesting ? "NEWMAKAMISA transition" : "House-only") +
                " test context prepared. Chapter completion/credits were not fabricated. Prior full-playthrough UI history is not reproduced.", this);
        }
        catch (System.Exception error) { Fail(error.Message); }
    }

    private System.Collections.IEnumerator Start()
    {
        if (!IsActiveTest) yield break;
        yield return null; // Native Awake/Start, spawner and notification registration must run first.
        if (useNewMakamisaReturnTransitionForTesting)
        {
            RememberFlag(MaestroBenSulatinLightEncounter.TransportFlag, true);
            RememberFlag(MaestroBenSulatinLightEncounter.ArrivalFlag, false);
            SpawnData.spawnPointName = "Part3Spawn";
            AsyncOperation load = null;
            try { load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("NEWMAKAMISA"); }
            catch (System.Exception error) { Fail(error.Message); }
            // Unity loads cannot be cancelled: retain the route/owner through activation.
            while (load != null && !load.isDone) yield return null;
            if (load == null || !load.isDone || !sourceConfigured)
            { Fail("NEWMAKAMISA source load/configuration did not finish. Stop this test before retrying."); yield break; }
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!SourceReady() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!SourceReady()) { Fail("Part 3 source presentation, fade, camera or spawn did not become ready."); yield break; }
            RememberFlag(MaestroBenSulatinLightEncounter.ArrivalFlag, true);
            bootstrapToken?.Release(); bootstrapToken = null;
        }
        try { coordinator.BeginDevelopmentReturn(this); }
        catch (System.Exception error) { Fail(error.Message); }
    }

    private void ConfigureSource(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!IsActiveTest || scene.name != "NEWMAKAMISA") return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ConfigureSource;
        try
        {
            // sceneLoaded is before Start: bypass chapter playback only in this temporary
            // test scene. Its native Part 3 population/HUD Awake has already initialized.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is NewMakamisaPart3Chapters89 || behaviour is NewMakamisaPart3Chapter10 ||
                        behaviour is NewMakamisaPart3Chapter11 || behaviour is NewMakamisaPart3Chapter12 || behaviour is NewMakamisaPart3Chapter13)
                        behaviour.enabled = false;
            var setup = FindObjectsByType<NewMakamisaPart3DevelopmentTesting>(FindObjectsInactive.Include);
            var source = System.Array.Find(setup, item => item.gameObject.scene == scene);
            if (source == null) throw new System.InvalidOperationException("Existing Part 3 arrival references are missing.");
            // Read its authored references without changing its Inspector settings.
            var serialized = new UnityEditor.SerializedObject(source);
            sourcePresentation = Reference<NewMakamisaPart3PopulationPresentation>(serialized, "presentation");
            sourceFade = Reference<FadeController>(serialized, "incomingFade");
            sourcePlayer = Reference<Supercyan.FreeSample.SimpleSampleCharacterControl>(serialized, "player");
            sourceFocus = Reference<CameraFocusManager>(serialized, "focusManager");
            sourceCamera = Reference<Unity.Cinemachine.CinemachineCamera>(serialized, "normalCamera");
            sourceBrain = Reference<Unity.Cinemachine.CinemachineBrain>(serialized, "cameraBrain");
            var focus = Reference<Unity.Cinemachine.CinemachineCamera>(serialized, "focusCamera");
            focus.Priority = 0; sourceCamera.Priority = 10; // Same selection as the sanctioned Part 3 startup.
            coordinator = System.Array.Find(FindObjectsByType<NewAlaalaPostCreditsEpilogue>(FindObjectsInactive.Exclude),
                item => item.gameObject.scene == scene && !item.IsHouseEndpointForDevelopment);
            if (coordinator == null) throw new System.InvalidOperationException("Existing source epilogue coordinator is missing.");
            sourceConfigured = true;
        }
        catch (System.Exception error) { Fail(error.Message); }
    }

    private static T Reference<T>(UnityEditor.SerializedObject owner, string field) where T : Component
    {
        var value = owner.FindProperty(field)?.objectReferenceValue as T;
        if (value == null) throw new System.InvalidOperationException("Missing Part 3 reference: " + field);
        return value;
    }

    private bool SourceReady() => sourcePresentation != null && sourcePresentation.IsPart3PresentationReady &&
        sourceFade != null && sourceFade.IncomingFadeCompleted && sourcePlayer != null && sourcePlayer.isActiveAndEnabled &&
        sourceFocus != null && !sourceFocus.IsFocusing && sourceBrain != null && !sourceBrain.IsBlending &&
        ReferenceEquals(sourceBrain.ActiveVirtualCamera, sourceCamera) && string.IsNullOrEmpty(SpawnData.spawnPointName);

    private void RememberFlag(string key, bool value)
    { if (!flags.ContainsKey(key)) flags.Add(key, SessionStoryState.GetFlag(key)); SessionStoryState.SetFlag(key, value); }
    private void RememberString(string key, string value)
    { if (!strings.ContainsKey(key)) strings.Add(key, SessionStoryState.GetString(key)); SessionStoryState.SetString(key, value); }

    private void Fail(string reason)
    {
        Debug.LogError("[FinalEpilogueTest] Stopped: " + reason + " No unfinished epilogue was marked complete. Stop Play Mode before retrying.", this);
        if (coordinator != null) coordinator.enabled = false;
        Cleanup();
    }

    private void Cleanup()
    {
        if (!initialized) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ConfigureSource;
        bootstrapToken?.Release(); bootstrapToken = null;
        if (bootstrapFade != null)
        {
            var release = bootstrapFade.FadeFromBlack(0f);
            try { release.MoveNext(); } finally { (release as System.IDisposable)?.Dispose(); }
            bootstrapFade = null;
        }
        if (SpawnData.spawnPointName == "Part3Spawn" || SpawnData.spawnPointName == "AlaalaEpilogueReturnPoint") SpawnData.spawnPointName = "";
        // Retain this context after completion and on normal reentry for captures.
        // Restore owned temporary keys only when this test ends, never reset all UI/story data.
        foreach (var pair in flags) SessionStoryState.SetFlag(pair.Key, pair.Value);
        foreach (var pair in strings) SessionStoryState.SetString(pair.Key, pair.Value);
        SessionStoryState.SetInt("alaala_epilogue_return_mode", savedReturnMode);
        flags.Clear(); strings.Clear(); initialized = false;
        if (active == this) active = null;
    }

    private void OnDisable()
    {
        if (!IsActiveTest) return;
        StopAllCoroutines();
        if (coordinator != null) coordinator.enabled = false;
        Cleanup();
    }
    private void OnDestroy() => Cleanup();
#endif
}
