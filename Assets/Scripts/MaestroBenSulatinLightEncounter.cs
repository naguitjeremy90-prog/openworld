using System.Collections;
using DialogueEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local Part 3 handoff. Persists only while carrying its white overlay across loading.</summary>
[DefaultExecutionOrder(-11000)]
[DisallowMultipleComponent]
public sealed class MaestroBenSulatinLightEncounter : MonoBehaviour
{
    public const string HandoffFlag = "alaala_part3_sulatin_handoff_complete";
    public const string LightFlag = "alaala_part3_light_revealed";
    public const string ReactionVisualFlag = "alaala_part3_light_reaction_shown";
    public const string ReactionFlag = "alaala_part3_light_reaction_complete";
    public const string TransportFlag = "alaala_part3_light_transport_started";
    public const string ArrivalFlag = "alaala_part3_arrival_complete";
    private const string TaskId = "main_collect_remaining_sulatin";
    private const string ReturnStage = "return_to_maestro_ben";
    private const string Destination = "NEWMAKAMISA";
    private const string SpawnPoint = "Part3Spawn";
    private static readonly string[] RequiredItems =
    {
        "present_pili_archival_rizal_novel",
        "present_pili_tagalog_espanol_manuscripts",
        "present_pili_letter_sealed_envelope",
        "present_pili_filipino_archive_note",
        "church_lumang_sulatin"
    };

    [SerializeField] private GameObject mysteriousLight;
    [SerializeField] private NPCConversationTrigger standingInteraction;
    [SerializeField] private GameObject standingMaestro;
    [SerializeField] private Transform player;
    [SerializeField] private DialogueMovementLock movementLock;
    [SerializeField] private CharacterReactionController reaction;
    [SerializeField] private NPCConversation handoffConversation;
    [SerializeField] private NPCConversation reactionConversation;
    [SerializeField] private Collider lightTrigger;
    [SerializeField] private FadeController whiteFade;
    [SerializeField] private CanvasGroup whiteGroup;
    [SerializeField] private Canvas whiteCanvas;

    private StorySequenceToken token;
    private NPCConversation expectedConversation;
    private bool ownsMovement;
    private bool presenting;
    private bool transporting;
    private bool ownsFacingHold;
    private Coroutine presentationRoutine;
    private Rigidbody playerBody;

    private void Awake()
    {
        if (lightTrigger != null)
            lightTrigger.enabled = false;
        if (mysteriousLight != null)
        {
            // Move only the separate trigger; never alter the authored effect.
            transform.position = mysteriousLight.transform.position;
            mysteriousLight.SetActive(SessionStoryState.GetFlag(LightFlag) &&
                                      !SessionStoryState.GetFlag(ArrivalFlag));
        }
        if (player != null)
            playerBody = player.GetComponent<Rigidbody>();
        if (whiteCanvas != null)
            whiteCanvas.gameObject.SetActive(false);
        ConversationManager.ConversationClosed += HandleClosed;
    }

    private void Start()
    {
        if (lightTrigger != null)
            lightTrigger.enabled = SessionStoryState.GetFlag(ReactionFlag) &&
                                   !SessionStoryState.GetFlag(TransportFlag);
    }

    private bool HasRequiredTaskAndItems()
    {
        TaskManager tasks = TaskManager.Instance;
        InventoryManager inventory = InventoryManager.Instance;
        // IsCurrentStage consults the registered task's runtime stage cache.
        if (tasks == null || inventory == null ||
            tasks.GetTaskState(TaskId) != TaskState.Active ||
            !tasks.IsCurrentStage(TaskId, ReturnStage))
            return false;
        foreach (string id in RequiredItems)
            if (!inventory.HasItem(id))
                return false;
        return true;
    }

    /// <summary>Called only by the standing NPC selector when E starts an interaction.</summary>
    public NPCConversation PrepareInteractionConversation()
    {
        if (!isActiveAndEnabled || presenting || transporting ||
            gameObject.scene.name != "MaestroBenHouse" ||
            SessionStoryState.GetFlag(ReactionFlag) || SessionStoryState.GetFlag(TransportFlag) ||
            !HasRequiredTaskAndItems() || !ReferencesReady() ||
            !standingInteraction.isActiveAndEnabled ||
            !standingMaestro.activeInHierarchy ||
            !standingInteraction.transform.IsChildOf(standingMaestro.transform) ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            ConversationManager.Instance == null || ConversationManager.Instance.IsConversationActive)
            return null;

        // Hold before dialogue captures its movement state, avoiding a disabled-state restore.
        if (!AcquireControls())
            return null;
        standingInteraction.SetFacingTransforms(standingMaestro.transform, player);
        standingInteraction.DeferFacingResetForStorySequence();
        ownsFacingHold = true;
        presenting = true;
        if (SessionStoryState.GetFlag(HandoffFlag))
        {
            // An interrupted reaction can be retried with E without repeating the handoff.
            RevealLight();
            expectedConversation = reactionConversation;
        }
        else
            expectedConversation = handoffConversation;
        return expectedConversation;
    }

    private bool ReferencesReady()
    {
        return mysteriousLight != null && standingInteraction != null && standingMaestro != null && player != null &&
               movementLock != null && reaction != null && handoffConversation != null &&
               reactionConversation != null && lightTrigger != null && whiteFade != null &&
               whiteGroup != null && whiteCanvas != null;
    }

    private bool AcquireControls()
    {
        token = StorySequenceCoordinator.Acquire(this);
        if (token == null || !token.IsValid)
            return false;
        movementLock.HoldMovementForStorySequence();
        ownsMovement = true;
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }
        return true;
    }

    private void HandleClosed(NPCConversation closed, bool normal)
    {
        if (!presenting || expectedConversation == null || closed != expectedConversation)
            return;
        expectedConversation = null; // Consume the identity before running any callbacks.
        if (!normal)
        {
            presenting = false;
            ReleaseControls();
            return;
        }
        if (closed == handoffConversation)
        {
            SessionStoryState.SetFlag(HandoffFlag, true);
            presentationRoutine = StartCoroutine(PresentLight());
        }
        else if (closed == reactionConversation)
        {
            SessionStoryState.SetFlag(ReactionFlag, true);
            presentationRoutine = StartCoroutine(FinishReaction());
        }
    }

    private IEnumerator FinishReaction()
    {
        yield return new WaitForSecondsRealtime(0.45f);
        presentationRoutine = null;
        presenting = false;
        ReleaseControls();
        lightTrigger.enabled = true;
    }

    private void RevealLight()
    {
        mysteriousLight.SetActive(true);
        if (!SessionStoryState.GetFlag(LightFlag))
        {
            mysteriousLight.GetComponent<ParticleSystem>()?.Play(true);
            SessionStoryState.SetFlag(LightFlag, true);
        }
    }

    private IEnumerator PresentLight()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        RevealLight();
        yield return new WaitForSecondsRealtime(0.5f);
        Vector3 direction = mysteriousLight.transform.position - player.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            // Same horizontal Slerp pattern as the existing Part 2 Maestro controller,
            // applied to Miguel's actual locomotion/Rigidbody transform.
            Quaternion start = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                SetPlayerRotation(Quaternion.Slerp(start, target,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.5f))));
                yield return null;
            }
            SetPlayerRotation(target);
        }
        yield return new WaitForSecondsRealtime(0.35f);
        if (!SessionStoryState.GetFlag(ReactionVisualFlag))
        {
            while (reaction.IsReacting)
                yield return null;
            SessionStoryState.SetFlag(ReactionVisualFlag, true);
            yield return reaction.PlayReaction();
        }
        yield return new WaitForSecondsRealtime(0.5f);
        ConversationManager manager = ConversationManager.Instance;
        presentationRoutine = null;
        if (manager == null || manager.IsConversationActive)
        {
            presenting = false;
            ReleaseControls();
            yield break;
        }
        expectedConversation = reactionConversation;
        manager.StartConversation(reactionConversation);
    }

    private void SetPlayerRotation(Quaternion rotation)
    {
        player.rotation = rotation;
        if (playerBody != null)
            playerBody.rotation = rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (transporting || presenting || !SessionStoryState.GetFlag(ReactionFlag) ||
            SessionStoryState.GetFlag(TransportFlag) || !HasRequiredTaskAndItems() ||
            !other.CompareTag("Player") ||
            (other.transform != player && other.attachedRigidbody != playerBody) ||
            StorySequenceCoordinator.IsStorySequenceActive ||
            ConversationManager.Instance == null || ConversationManager.Instance.IsConversationActive)
            return;
        if (!ReferencesReady() || !Application.CanStreamedLevelBeLoaded(Destination))
        {
            Debug.LogError("Part 3 light transport is missing required references or destination scene.", this);
            return;
        }
        if (!AcquireControls())
            return;
        transporting = true;
        lightTrigger.enabled = false;
        SessionStoryState.SetFlag(TransportFlag, true);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Transport());
    }

    private IEnumerator Transport()
    {
        // Awake on the inactive FadeController writes alpha=1. Keep rendering disabled
        // until that initialization has finished and transparency has been restored.
        whiteCanvas.enabled = false;
        whiteGroup.alpha = 0f;
        whiteCanvas.gameObject.SetActive(true);
        whiteCanvas.sortingOrder = 32760;
        whiteGroup.alpha = 0f;
        whiteGroup.blocksRaycasts = true;
        whiteCanvas.enabled = true;
        // Render gameplay unchanged before sampling any elapsed fade time.
        yield return null;
        yield return FadeWhiteFromGameplay();
        // Let the opaque overlay render before loading begins.
        yield return null;
        SpawnData.spawnPointName = SpawnPoint;
        SceneManager.sceneLoaded += HandleDestinationLoaded;
        AsyncOperation load = SceneManager.LoadSceneAsync(Destination);
        if (load == null)
        {
            SceneManager.sceneLoaded -= HandleDestinationLoaded;
            SpawnData.spawnPointName = string.Empty;
            SessionStoryState.SetFlag(TransportFlag, false);
            yield return whiteFade.FadeFromBlack(1f);
            whiteCanvas.gameObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetSceneByName("MaestroBenHouse"));
            transporting = false;
            ReleaseControls();
            lightTrigger.enabled = true;
            yield break;
        }
        yield return load;
        // sceneLoaded runs before destination Start: it holds movement before PlayerSpawner runs.
        yield return null;
        Scene destination = SceneManager.GetSceneByName(Destination);
        FadeController incomingFade = FindInScene<FadeController>(destination);
        while (incomingFade != null && !incomingFade.IncomingFadeCompleted)
            yield return null;
        if (player == null || movementLock == null ||
            !string.IsNullOrEmpty(SpawnData.spawnPointName))
        {
            Debug.LogError("Part 3 arrival did not resolve its player, movement lock, or Part3Spawn routing; white cover retained.", this);
            yield break;
        }
        yield return whiteFade.FadeFromBlack(1.5f);
        SessionStoryState.SetFlag(ArrivalFlag, true);
        ReleaseControls();
        Destroy(gameObject);
    }

    private IEnumerator FadeWhiteFromGameplay()
    {
        // Only this outgoing transition changes. Existing FadeController still owns
        // the destination reveal and load-failure recovery.
        const float duration = 3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            whiteGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        whiteGroup.alpha = 1f;
    }

    private void HandleDestinationLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Destination)
            return;
        SceneManager.sceneLoaded -= HandleDestinationLoaded;
        movementLock = FindInScene<DialogueMovementLock>(scene);
        var spawner = FindInScene<PlayerSpawner>(scene);
        // The scene's configured PlayerSpawner owns positioning; this controller never teleports.
        var character = FindInScene<Supercyan.FreeSample.SimpleSampleCharacterControl>(scene);
        player = character != null ? character.transform : null;
        ownsMovement = false;
        if (movementLock != null && spawner != null)
        {
            movementLock.HoldMovementForStorySequence();
            ownsMovement = true;
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>();
            if (component != null)
                return component;
        }
        return null;
    }

    private void ReleaseControls()
    {
        if (ownsFacingHold)
        {
            if (standingInteraction != null)
                standingInteraction.ReleaseFacingResetForStorySequence();
            ownsFacingHold = false;
        }
        if (ownsMovement && movementLock != null)
            movementLock.ReleaseStorySequenceHold();
        ownsMovement = false;
        token?.Release();
        token = null;
    }

    private void OnDisable()
    {
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);
        presentationRoutine = null;
        presenting = false;
        expectedConversation = null;
        ConversationManager.ConversationClosed -= HandleClosed;
        SceneManager.sceneLoaded -= HandleDestinationLoaded;
        ReleaseControls();
    }
}
