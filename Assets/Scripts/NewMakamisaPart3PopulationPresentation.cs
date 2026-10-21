using System;
using UnityEngine;

/// <summary>Applies the deserted-town presentation before NEWMAKAMISA initializes.</summary>
[DefaultExecutionOrder(-31000)]
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3PopulationPresentation : MonoBehaviour
{
    [SerializeField] private GameObject[] townspeople = Array.Empty<GameObject>();
    [SerializeField] private GameObject[] part1BehaviourRoots = Array.Empty<GameObject>();
    [SerializeField] private GameObject padreAgatonFinal;
    [SerializeField] private GameObject[] cinematicHudRoots = Array.Empty<GameObject>();

    [Header("Verified Part 3 scene safety")]
    [SerializeField] private SceneEntrance churchEntrance;
    [SerializeField] private Collider churchEntranceTrigger;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ClarityManager clarityManager;
    [SerializeField, Tooltip("Only the existing scene-local DRI SA/BGMusic AudioSource. Authored settings are preserved outside Part 3.")]
    private AudioSource backgroundMusic;

    public bool IsPart3PresentationReady { get; private set; }

    private GameplayHUDTarget tutorialHudTarget;
    private GameplayHUDTarget unlockedTaskHud;
    private GameObject suppressedSideTracker;
    private bool sideTrackerWasActive;
    private bool ownsBackgroundMusicSuppression;
    private float originalBackgroundMusicVolume;
    private bool originalBackgroundMusicEnabled, originalBackgroundMusicPlayOnAwake;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        SessionStoryState.FlagChanged += HandleTaskPresentationFlag;
        if (ownsBackgroundMusicSuppression)
        {
            // A retained instance may be initialized again after a legitimate session reset.
            // Never restore audio just because a chapter or presentation was disabled.
            if (!SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag) &&
                !SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) &&
                !string.Equals(SpawnData.spawnPointName, "Part3Spawn", StringComparison.Ordinal))
                RestoreBackgroundMusicForNormalGameplay();
            else
                SuppressBackgroundMusic();
        }
        RestoreCompletedMainTracker();
    }

    private void HandleTaskPresentationFlag(string flag, bool value)
    {
        if (value && (flag == NewMakamisaPart3Chapters89.FindNotificationFlag ||
                      flag == MaestroBenSulatinLightEncounter.ArrivalFlag))
            RestoreCompletedMainTracker();
    }

    private void RestoreCompletedMainTracker()
    {
        // This existing flag is written only by the successful final-fade callback.
        // Reentry uses the same session state; no new unlock/save state is created.
        if (unlockedTaskHud != null || !isActiveAndEnabled || !IsPart3PresentationReady ||
            gameObject.scene.name != "NEWMAKAMISA" ||
            !SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag) ||
            !SessionStoryState.GetFlag(NewMakamisaPart3Chapters89.FindNotificationFlag))
            return;

        foreach (GameObject root in cinematicHudRoots)
        {
            if (root == null || root.name != "TaskUI")
                continue;

            Transform main = root.transform.Find("MainTaskTracker");
            Transform side = root.transform.Find("SideTaskTracker");
            GameplayHUDTarget target = root.GetComponent<GameplayHUDTarget>();
            if (main == null || side == null || main.GetComponent<TaskTrackerUI>() == null ||
                side.GetComponent<TaskTrackerUI>() == null || target == null ||
                !root.activeInHierarchy || !main.gameObject.activeSelf)
            {
                Debug.LogError("Part 3 Main Task Tracker could not unlock: verified TaskUI/MainTaskTracker/SideTaskTracker or its HUD target is unavailable.", this);
                return;
            }

            // The notification can currently be reparented by Chapters 8-9.
            // Refuse to lift this parent if future edits add any unrelated HUD.
            foreach (Transform child in root.transform)
            {
                if (child != main && child != side && child.GetComponent<TaskNotificationUI>() == null)
                {
                    Debug.LogError("Part 3 Main Task Tracker stayed hidden: TaskUI contains an unexpected HUD child: " + child.name, this);
                    return;
                }
            }

            // TaskTrackerUI owns its own alpha, so retain Side suppression through
            // its runtime active state rather than competing CanvasGroup writes.
            suppressedSideTracker = side.gameObject;
            sideTrackerWasActive = suppressedSideTracker.activeSelf;
            suppressedSideTracker.SetActive(false);
            unlockedTaskHud = target;
            target.SetPresentationSuppressed(false);
            return;
        }

        Debug.LogError("Part 3 Main Task Tracker could not unlock: TaskUI is missing from cinematicHudRoots.", this);
    }

    private void RestoreTaskHudSuppression()
    {
        // Hide the parent first, then restore only the child state we captured.
        if (unlockedTaskHud != null)
            unlockedTaskHud.SetPresentationSuppressed(true);
        if (suppressedSideTracker != null)
            suppressedSideTracker.SetActive(sideTrackerWasActive);
        unlockedTaskHud = null;
        suppressedSideTracker = null;
    }

    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= HandleTaskPresentationFlag;
        RestoreTaskHudSuppression();
    }

    private void Awake()
    {
        if (gameObject.scene.name != "NEWMAKAMISA")
            return;

        // PlayerSpawner consumes the destination in Start. Capture the actual
        // transport route now; persistent flags alone must never empty the town.
        bool part3Arrival = string.Equals(
            SpawnData.spawnPointName, "Part3Spawn", StringComparison.Ordinal) &&
            SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.TransportFlag);

        if (padreAgatonFinal != null)
            padreAgatonFinal.SetActive(part3Arrival);

        if (!part3Arrival)
            return;

        // Run in early destination Awake, stopping any autoplay independently of HUD safety.
        // The outgoing MaestroBenHouse scene does not own this destination AudioSource.
        SuppressBackgroundMusic();

        // Disable complete verified character roots: visuals, physics, movement,
        // conversations, quest markers and any character audio stop together.
        DisableRoots(townspeople);
        DisableRoots(part1BehaviourRoots);
        HideCinematicHud();
        IsPart3PresentationReady = ApplyPart3SceneSafety();
        // The existing intro controller checks Part3Spawn in its early Awake
        // and disables its cinematic camera without completing Part 1 content.
    }

    private void SuppressBackgroundMusic()
    {
        if (backgroundMusic == null || backgroundMusic.gameObject.scene != gameObject.scene ||
            backgroundMusic.name != "BGMusic" || backgroundMusic.transform.parent == null ||
            backgroundMusic.transform.parent.name != "DRI SA")
            return;

        if (!ownsBackgroundMusicSuppression)
        {
            originalBackgroundMusicVolume = backgroundMusic.volume;
            originalBackgroundMusicEnabled = backgroundMusic.enabled;
            originalBackgroundMusicPlayOnAwake = backgroundMusic.playOnAwake;
            ownsBackgroundMusicSuppression = true;
        }
        // Arrival must already be silent; no delayed fade can spill into Chapter 8.
        // Disable only this source to prevent reactivation from restarting its loop.
        backgroundMusic.playOnAwake = false;
        backgroundMusic.volume = 0f;
        backgroundMusic.Stop();
        backgroundMusic.enabled = false;
    }

    private void RestoreBackgroundMusicForNormalGameplay()
    {
        if (!ownsBackgroundMusicSuppression || backgroundMusic == null) return;
        ownsBackgroundMusicSuppression = false;
        backgroundMusic.volume = originalBackgroundMusicVolume;
        backgroundMusic.playOnAwake = originalBackgroundMusicPlayOnAwake;
        backgroundMusic.enabled = originalBackgroundMusicEnabled;
        if (originalBackgroundMusicEnabled && originalBackgroundMusicPlayOnAwake &&
            backgroundMusic.gameObject.activeInHierarchy && !backgroundMusic.isPlaying)
            backgroundMusic.Play();
    }

    private bool ApplyPart3SceneSafety()
    {
        if (churchEntrance == null || churchEntranceTrigger == null ||
            churchEntranceTrigger.gameObject != churchEntrance.gameObject ||
            inventoryUI == null || clarityManager == null)
        {
            Debug.LogError("NEWMAKAMISA Part 3 scene safety is missing its verified entrance or input owners.", this);
            return false;
        }

        // The dedicated door trigger is separate from the church scenery and PlayerSpawner.
        churchEntrance.enabled = false;
        churchEntranceTrigger.enabled = false;
        inventoryUI.GameplayShortcutSuppressed = true;
        clarityManager.GameplayShortcutSuppressed = true;
        return true;
    }

    private void HideCinematicHud()
    {
        // Explicit visual targets only. Keep managers, fullscreen interfaces,
        // dialogue, reactions and the separate ESC menu initialized and usable.
        foreach (GameObject root in cinematicHudRoots)
            if (root != null)
                GameplayHUDTarget.AttachTo(root).SetPresentationSuppressed(true);

        // The existing BeforeSceneLoad bootstrap owns this persistent overlay.
        // Resolve its registered target once, without searching other HUD/menu UI.
        if (!GameplaySystemTutorialManager.HasInstance)
            return;

        GameplaySystemTutorialVisual visual = GameplaySystemTutorialManager.Instance
            .GetComponentInChildren<GameplaySystemTutorialVisual>(true);
        if (visual == null)
            return;

        tutorialHudTarget = visual.GetComponent<GameplayHUDTarget>();
        if (tutorialHudTarget != null)
            tutorialHudTarget.SetPresentationSuppressed(true);
    }

    private void OnDestroy()
    {
        RestoreTaskHudSuppression();
        // This target outlives the scene; release only our visual policy when
        // leaving NEWMAKAMISA, without clearing any pending tutorial/read state.
        if (tutorialHudTarget != null)
            tutorialHudTarget.SetPresentationSuppressed(false);
    }

    private void DisableRoots(GameObject[] roots)
    {
        foreach (GameObject root in roots)
        {
            // Also protect the final character if a serialized group is extended.
            if (root == null || root == padreAgatonFinal ||
                (padreAgatonFinal != null && padreAgatonFinal.transform.IsChildOf(root.transform)))
                continue;

            root.SetActive(false);
        }
    }
}
