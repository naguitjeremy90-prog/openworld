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

    public bool IsPart3PresentationReady { get; private set; }

    private GameplayHUDTarget tutorialHudTarget;

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

        // Disable complete verified character roots: visuals, physics, movement,
        // conversations, quest markers and any character audio stop together.
        DisableRoots(townspeople);
        DisableRoots(part1BehaviourRoots);
        HideCinematicHud();
        IsPart3PresentationReady = ApplyPart3SceneSafety();
        // The existing intro controller checks Part3Spawn in its early Awake
        // and disables its cinematic camera without completing Part 1 content.
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
