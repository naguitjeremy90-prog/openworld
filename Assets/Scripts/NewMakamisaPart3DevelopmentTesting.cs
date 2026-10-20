using Unity.Cinemachine;
using UnityEngine;

/// <summary>Opt-in Editor startup context for a post-arrival Part 3 exploration snapshot.</summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3DevelopmentTesting : MonoBehaviour
{
    [SerializeField] private bool enablePart3DirectTesting = false;

    [Header("Existing scene arrival infrastructure")]
    [SerializeField] private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField] private Transform part3Spawn;
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private Supercyan.FreeSample.SimpleSampleCharacterControl player;
    [SerializeField] private FadeController incomingFade;
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CinemachineBrain cameraBrain;

#if UNITY_EDITOR
    private const string ScenePath = "Assets/Scenes/NEWMAKAMISA.unity";
    private const string SpawnPointName = "Part3Spawn";
    private const string LaunchSceneKey = "ALAALA.Part3DirectTest.LaunchScene";
    private const string LaunchTicketKey = "ALAALA.Part3DirectTest.LaunchTicket";
    private const float ReadinessTimeout = 10f;

    private static string startupScenePath;
    private static bool startupWindowOpen;
    private bool initialized;
    private bool ownsSpawnRoute;
    private bool ownsArrivalFlag;

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditorLaunchCapture()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= CaptureEditorLaunch;
        UnityEditor.EditorApplication.playModeStateChanged += CaptureEditorLaunch;
    }

    private static void CaptureEditorLaunch(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.ExitingEditMode)
            return;

        UnityEditor.SceneAsset startScene = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
        string path = startScene != null
            ? UnityEditor.AssetDatabase.GetAssetPath(startScene)
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        // SessionState carries this launch-only identity across the domain reload.
        // An additive open-scene setup without an explicit start scene is ambiguous.
        bool unambiguous = startScene != null || UnityEngine.SceneManagement.SceneManager.sceneCount == 1;
        bool reloadSupported = ReloadSettingsSupported();
        if (path == ScenePath && !reloadSupported)
            Debug.LogWarning("Part 3 direct testing is unavailable with Domain Reload or Scene Reload disabled. No startup ticket will be issued.");
        UnityEditor.SessionState.SetString(LaunchSceneKey, path);
        UnityEditor.SessionState.SetBool(LaunchTicketKey,
            unambiguous && reloadSupported && string.Equals(path, ScenePath, System.StringComparison.Ordinal));
    }

    private static bool ReloadSettingsSupported()
    {
        return !UnityEditor.EditorSettings.enterPlayModeOptionsEnabled ||
            (UnityEditor.EditorSettings.enterPlayModeOptions &
             (UnityEditor.EnterPlayModeOptions.DisableDomainReload |
              UnityEditor.EnterPlayModeOptions.DisableSceneReload)) == 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStartupWindow()
    {
        startupScenePath = null;
        startupWindowOpen = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= CloseStartupWindow;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OpenStartupWindow()
    {
        // SessionStoryState's existing SubsystemRegistration reset has already run.
        startupScenePath = UnityEditor.SessionState.GetString(LaunchSceneKey, string.Empty);
        startupWindowOpen = UnityEditor.SessionState.GetBool(LaunchTicketKey, false);
        UnityEditor.SessionState.EraseString(LaunchSceneKey);
        UnityEditor.SessionState.EraseBool(LaunchTicketKey);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += CloseStartupWindow;
    }

    private static void CloseStartupWindow(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // The ticket is usable only in the initial scene's Awake, never on a later visit.
        startupWindowOpen = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= CloseStartupWindow;
    }

    private void Awake()
    {
        if (!Application.isPlaying || !enablePart3DirectTesting || initialized)
            return;

        if (!ReloadSettingsSupported())
        {
            Debug.LogWarning("Part 3 direct testing requires both Domain Reload and Scene Reload. No test state was applied.", this);
            return;
        }

        var scene = gameObject.scene;
        if (!startupWindowOpen || startupScenePath != ScenePath || scene.path != ScenePath ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene() != scene ||
            UnityEngine.SceneManagement.SceneManager.sceneCount != 1)
            return;

        startupWindowOpen = false; // Claim once, including failed attempts.
        // A fresh, normally reloaded startup is required; never replace an incoming route
        // or services/story ownership supplied by another bootstrap or a real playthrough.
        if (!string.IsNullOrEmpty(SpawnData.spawnPointName) ||
            TaskManager.Instance != null || InventoryManager.Instance != null ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag) ||
            SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) ||
            SessionStoryState.GetFlag("alaala_part2_complete"))
        {
            Debug.LogWarning("Part 3 direct testing found an existing route or story session. Existing state was preserved.", this);
            return;
        }

        string referenceErrors = GetReferenceErrors(false);
        if (!string.IsNullOrEmpty(referenceErrors))
        {
            Debug.LogError("Part 3 Direct Testing: Invalid scene references:\n" + referenceErrors +
                "\nNo test state was applied.", this);
            return;
        }

        if (focusManager.IsFocusing)
        {
            Debug.LogWarning("Part 3 direct testing found an existing camera focus owner. Existing state was preserved.", this);
            return;
        }

        initialized = true;
        ownsSpawnRoute = true;
        SpawnData.spawnPointName = SpawnPointName;
        SessionStoryState.SetFlag(MaestroBenSulatinLightEncounter.TransportFlag, true);
        // ReturnToNormal requires an owned focus and can unlock movement.
        // Match its priority selection once, before the incoming local fade can reveal
        // a tied focus camera. Verify the Brain's actual selection after Start below.
        focusCamera.Priority = 0;
        normalCamera.Priority = 10;
        // The existing -31000 population Awake owns all presentation and safety decisions.
    }

    private string GetReferenceErrors(bool requireLifecycleReady)
    {
        var errors = new System.Collections.Generic.List<string>();
        CheckReference(errors, nameof(presentation), presentation, true, requireLifecycleReady);
        CheckReference(errors, nameof(part3Spawn), part3Spawn, false, requireLifecycleReady);
        if (part3Spawn != null && part3Spawn.name != SpawnPointName)
            errors.Add(nameof(part3Spawn) + " — expected marker named " + SpawnPointName + ".");
        CheckReference(errors, nameof(playerSpawner), playerSpawner, true, requireLifecycleReady);
        // Movement may legitimately be held by another owner during initialization.
        CheckReference(errors, nameof(player), player, false, requireLifecycleReady);
        CheckReference(errors, nameof(incomingFade), incomingFade, true, requireLifecycleReady);
        CheckReference(errors, nameof(focusManager), focusManager, true, requireLifecycleReady);
        CheckReference(errors, nameof(normalCamera), normalCamera, true, requireLifecycleReady);
        if (normalCamera != null && player != null && normalCamera.Follow != player.transform)
            errors.Add(nameof(normalCamera) + ".Follow — must reference player.transform.");
        CheckReference(errors, nameof(focusCamera), focusCamera, true, requireLifecycleReady);
        CheckReference(errors, nameof(cameraBrain), cameraBrain, true, requireLifecycleReady);
        if (cameraBrain != null)
            CheckReference(errors, nameof(cameraBrain) + ".Camera", cameraBrain.GetComponent<Camera>(), true, requireLifecycleReady);
        return string.Join("\n", errors);
    }

    private void CheckReference(System.Collections.Generic.List<string> errors, string field,
        Component component, bool requireEnabled, bool requireLifecycleReady)
    {
        if (component == null)
        {
            errors.Add(field + " — missing reference.");
            return;
        }
        if (component.gameObject.scene != gameObject.scene)
            errors.Add(field + " — must belong to this scene.");
        if (!component.gameObject.activeInHierarchy)
            errors.Add(field + " — GameObject is inactive in the hierarchy.");
        var behaviour = component as Behaviour;
        if (requireEnabled && behaviour != null)
        {
            // At -32000, later scripts have not received Awake/OnEnable yet.
            // isActiveAndEnabled includes OnEnable readiness, not just authored state.
            if (!behaviour.enabled)
                errors.Add(field + " — component is disabled.");
            else if (requireLifecycleReady && component.gameObject.activeInHierarchy && !behaviour.isActiveAndEnabled)
                errors.Add(field + " — component has not become active after initialization.");
        }
    }

    private System.Collections.IEnumerator Start()
    {
        if (!initialized)
            yield break;

        // All normal Start methods, including PlayerSpawner, must have run first.
        yield return null;
        float deadline = Time.realtimeSinceStartup + ReadinessTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            string referenceErrors = GetReferenceErrors(true);
            if (!string.IsNullOrEmpty(referenceErrors))
            {
                FailArrival("invalid scene references:\n" + referenceErrors);
                yield break;
            }
            if (!string.IsNullOrEmpty(SpawnData.spawnPointName) && SpawnData.spawnPointName != SpawnPointName)
            {
                FailArrival("the spawn route changed during initialization");
                yield break;
            }

            bool spawnConsumed = string.IsNullOrEmpty(SpawnData.spawnPointName);
            if (spawnConsumed)
                ownsSpawnRoute = false;

            var dialogue = DialogueEditor.ConversationManager.Instance;
            bool controlsUnowned = !StorySequenceCoordinator.IsStorySequenceActive &&
                (dialogue == null || !dialogue.IsConversationActive) && !focusManager.IsFocusing;
            if (spawnConsumed && presentation.IsPart3PresentationReady && controlsUnowned)
            {
                if (incomingFade.IncomingFadeCompleted && player.isActiveAndEnabled &&
                    !cameraBrain.IsBlending && ReferenceEquals(cameraBrain.ActiveVirtualCamera, normalCamera))
                {
                    SessionStoryState.SetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag, true);
                    ownsArrivalFlag = true;
                    yield break;
                }
            }
            yield return null;
        }

        FailArrival("timed out waiting for spawn consumption, Part 3 presentation, local fade, normal camera or player readiness");
    }

    private void FailArrival(string reason)
    {
        ClearOwnedSpawnRoute();
        Debug.LogError("Part 3 direct arrival failed: " + reason + ". Arrival-complete was not set; movement was not forced.", this);
    }

    private void ClearOwnedSpawnRoute()
    {
        if (ownsSpawnRoute && SpawnData.spawnPointName == SpawnPointName)
            SpawnData.spawnPointName = string.Empty;
        ownsSpawnRoute = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearOwnedSpawnRoute();
        if (initialized)
        {
            // Eligibility required these flags to be absent. Release only this
            // test's two injected keys when its scene/context ends, never session data.
            if (ownsArrivalFlag)
                SessionStoryState.SetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag, false);
            SessionStoryState.SetFlag(MaestroBenSulatinLightEncounter.TransportFlag, false);
            ownsArrivalFlag = false;
            initialized = false;
        }
    }
#endif
}
