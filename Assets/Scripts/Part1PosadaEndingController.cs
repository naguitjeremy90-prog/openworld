using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using Supercyan.FreeSample;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the explicit Part 1 return arrival and beta ending in PosadaRoom.</summary>
public sealed class Part1PosadaEndingController : MonoBehaviour
{
    private const string TransportDestinationStateId = "makamisa_transport_destination";
    private const string Part1PosadaDestination = "posada_part1";
    private const string SceneMenuName = "SceneMenu";

#if UNITY_EDITOR
    [Header("DIRECT POSADA ENDING TESTING (EDITOR ONLY)")]
    [Tooltip("Development-only: start the same Part 1 Posada ending when Play Mode begins in PosadaRoom.")]
    [SerializeField] private bool enableDirectPosadaEndingTest;
    private bool directEditorTest;
#endif

    [Header("Part 1 arrival")]
    [SerializeField] private Transform wakePosition;
    [SerializeField] private Transform miguelRoot;
    [SerializeField] private Rigidbody miguelBody;
    [SerializeField] private SimpleSampleCharacterControl movementController;
    [SerializeField] private Animator miguelAnimator;
    [SerializeField] private RuntimeAnimatorController sleepingAnimatorController;
    [SerializeField] private Camera roomCamera;
    [SerializeField, Min(0.1f)] private float groundingTimeout = 2f;

    [Header("Sleeping reactions")]
    [SerializeField, Min(0.1f)] private float firstSleepGlyphDuration = 1f;
    [SerializeField, Min(0.1f)] private float secondSleepGlyphDuration = 1f;
    [SerializeField, Min(0.1f)] private float finalSleepGlyphDuration = 2f;
    [SerializeField, Min(0f)] private float quietAfterSleepDuration = 0.75f;
    [SerializeField, Min(0.05f)] private float roomRevealDuration = 0.85f;
    [SerializeField, Min(0f)] private float visibleSleepBeforeWakeDuration = 1.25f;
    [SerializeField, Min(0f)] private float pauseBetweenShockReactions = 0.85f;
    [SerializeField, Min(0.1f)] private float firstShockDuration = 2f;
    [SerializeField, Min(0.1f)] private float secondShockDuration = 3f;

    [Header("Production reactions")]
    [SerializeField] private SelfDialogueTrigger firstShockDialogue;
    [SerializeField] private SelfDialogueTrigger secondShockDialogue;
    [SerializeField] private CameraShakeController reactionCameraShake;
    [SerializeField, Min(0.1f)] private float secondShockShakeDuration = 1.2f;
    [SerializeField, Min(0.01f)] private float secondShockShakeStrength = 0.65f;
    [SerializeField] private SelfDialogueTrigger posadaDialogue;
    [SerializeField] private RuntimeAnimatorController posadaVictoryLoopController;
    [SerializeField] private CharacterReactionController reactionController;
    [SerializeField] private FadeController fadeController;
    [SerializeField] private TMP_Text screenSleepText;
    [SerializeField] private AlaalaRollingCreditsController rollingCredits;

    [Header("Startled look")]
    [SerializeField] private Transform scanTransform;
    [SerializeField, Range(10f, 70f)] private float leftYaw = 50f;
    [SerializeField, Range(10f, 70f)] private float rightYaw = 50f;
    [SerializeField, Min(0.05f)] private float turnToLeftDuration = 0.55f;
    [SerializeField, Min(0f)] private float leftLookHoldDuration = 0.3f;
    [SerializeField, Min(0.05f)] private float turnAcrossToRightDuration = 0.8f;
    [SerializeField, Min(0f)] private float rightLookHoldDuration = 0.3f;
    [SerializeField, Min(0.05f)] private float returnToCameraDuration = 0.55f;

    private StorySequenceToken storyToken;
    private bool part1Arrival;
    private RuntimeAnimatorController originalAnimatorController;
    private bool originalMovementEnabled;
    private Quaternion cameraFacingRotation;

    private void Awake()
    {
#if UNITY_EDITOR
        directEditorTest = Application.isPlaying &&
                           SessionStoryState.GetString(TransportDestinationStateId) != Part1PosadaDestination &&
                           enableDirectPosadaEndingTest;
        if (directEditorTest)
        {
            // Prepare only the same transient route value used by a real arrival.
            // The production route check below remains authoritative in builds.
            SessionStoryState.SetString(TransportDestinationStateId, Part1PosadaDestination);
        }
#endif
        part1Arrival = SessionStoryState.GetString(TransportDestinationStateId) ==
                       Part1PosadaDestination;

        if (!part1Arrival)
        {
            enabled = false;
            return;
        }

        // Claim presentation ownership before the incoming iris releases the
        // TransportationScene token. This keeps movement and HUD continuously locked.
        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("Part 1 Posada ending could not acquire story-sequence ownership.", this);
            enabled = false;
            return;
        }

        SessionStoryState.SetString(TransportDestinationStateId, string.Empty);
        SpawnData.spawnPointName = string.Empty;

        InitializeSceneLocalPresentation();
        if (fadeController != null)
            fadeController.HoldBlackForSequence();

        PlaceMiguelAtWakePosition();
        BeginSleepingPose();
        if (rollingCredits != null)
        {
            rollingCredits.CreditsCompleted += OnCreditsCompleted;
            rollingCredits.ReturnRequested += ReturnToMainMenu;
        }
        if (roomCamera == null)
            Debug.LogError("Part 1 Posada ending needs the room-positioned camera reference.", this);
        else
            roomCamera.enabled = true;
    }

    private IEnumerator Start()
    {
        if (!part1Arrival || storyToken == null)
            yield break;

        // The existing fade canvas is held opaque through the sleep text beats.
        // Direct Editor testing uses the same black-screen presentation.
        bool waitForIncomingIris = true;
#if UNITY_EDITOR
        waitForIncomingIris = !directEditorTest;
#endif
        if (waitForIncomingIris)
        {
            IrisTransitionController iris = IrisTransitionController.Instance;
            while (iris != null && iris.IsCovered)
                yield return null;
        }

        // Let the placed rigidbody settle while the actor is already in the
        // authored sleep pose and remains under the story-sequence lock.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        yield return PresentCumulativeSleepText();

        if (quietAfterSleepDuration > 0f)
            yield return new WaitForSeconds(quietAfterSleepDuration);

        if (fadeController != null)
            yield return fadeController.FadeFromBlack(roomRevealDuration);
        if (visibleSleepBeforeWakeDuration > 0f)
            yield return new WaitForSeconds(visibleSleepBeforeWakeDuration);

        yield return PlaySelfDialogue(firstShockDialogue, "Ha?!");
        if (pauseBetweenShockReactions > 0f)
            yield return new WaitForSeconds(pauseBetweenShockReactions);
        yield return PlaySelfDialogue(secondShockDialogue, "HAAAAAAA?!", () =>
        {
            if (reactionCameraShake != null)
                reactionCameraShake.Shake(secondShockShakeDuration, secondShockShakeStrength);
        });

        RestoreStandingLocomotion();
        yield return WaitForGrounding();
        WakeMiguelFacingCamera();
        if (reactionController != null)
            yield return reactionController.PlayReaction();

        scanTransform = scanTransform != null ? scanTransform : miguelRoot;
        if (scanTransform != null)
        {
            cameraFacingRotation = GetCameraFacingRotation(scanTransform.position);
            Quaternion leftLook = GetCameraRelativeLookRotation(-leftYaw);
            Quaternion rightLook = GetCameraRelativeLookRotation(rightYaw);
            yield return RotateActorTo(leftLook, turnToLeftDuration);
            if (leftLookHoldDuration > 0f)
                yield return new WaitForSeconds(leftLookHoldDuration);
            yield return RotateActorTo(rightLook, turnAcrossToRightDuration);
            if (rightLookHoldDuration > 0f)
                yield return new WaitForSeconds(rightLookHoldDuration);
            yield return RotateActorTo(cameraFacingRotation, returnToCameraDuration);
        }

        yield return PlaySelfDialogue(posadaDialogue, "POSADA!!!", BeginPosadaVictory);

        if (fadeController != null)
            yield return fadeController.FadeToBlack();

        if (rollingCredits != null)
            rollingCredits.BeginCredits();
        else
            Debug.LogError("Part 1 Posada ending has no AlaalaRollingCredits prefab reference.", this);
    }

    private void PlaceMiguelAtWakePosition()
    {
        if (wakePosition == null || miguelRoot == null)
        {
            Debug.LogError("Part 1 Posada ending is missing Miguel or Part1ReturnWakePosition.", this);
            return;
        }

        if (miguelBody != null)
        {
            miguelBody.position = wakePosition.position;
            miguelBody.rotation = wakePosition.rotation;
            miguelBody.linearVelocity = Vector3.zero;
            miguelBody.angularVelocity = Vector3.zero;
        }
        else
        {
            miguelRoot.SetPositionAndRotation(wakePosition.position, wakePosition.rotation);
        }

        if (scanTransform == null)
            scanTransform = miguelRoot;
        Physics.SyncTransforms();
    }

    private void BeginSleepingPose()
    {
        if (miguelAnimator == null && miguelRoot != null)
            miguelAnimator = miguelRoot.GetComponent<Animator>();
        originalMovementEnabled = movementController != null && movementController.enabled;
        if (miguelAnimator == null || sleepingAnimatorController == null)
        {
            Debug.LogWarning("A humanoid laying-idle controller is not configured; Miguel will use his normal grounded pose during the sleep beats.", this);
            return;
        }

        originalAnimatorController = miguelAnimator.runtimeAnimatorController;
        miguelAnimator.runtimeAnimatorController = sleepingAnimatorController;
        if (movementController != null)
            movementController.enabled = false;

        int layingIdle = Animator.StringToHash("Base Layer.Laying Idle");
        if (miguelAnimator.HasState(0, layingIdle))
            miguelAnimator.Play(layingIdle, 0, 0f);
        else
            Debug.LogWarning("The configured sleep controller has no Base Layer.Laying Idle state.", this);
    }

    private void RestoreStandingLocomotion()
    {
        if (miguelAnimator != null && originalAnimatorController != null)
            miguelAnimator.runtimeAnimatorController = originalAnimatorController;
        if (movementController != null)
            movementController.enabled = originalMovementEnabled;
    }

    private void BeginPosadaVictory()
    {
        if (miguelAnimator == null || posadaVictoryLoopController == null)
        {
            Debug.LogWarning("The Part 1 POSADA reaction has no looping victory Animator Controller configured.", this);
            return;
        }

        // This ending-only controller reuses FreeAnimations and its existing
        // victory state, with that state's exit transition looping back to itself.
        // Keep the story-locked movement component from changing locomotion
        // parameters while the victory continues through the dialogue and fade.
        if (movementController != null)
            movementController.enabled = false;

        miguelAnimator.runtimeAnimatorController = posadaVictoryLoopController;
        int victoryLayer = miguelAnimator.GetLayerIndex("Animations");
        int victoryState = Animator.StringToHash("Animations.victory");
        if (victoryLayer >= 0 && miguelAnimator.HasState(victoryLayer, victoryState))
            miguelAnimator.Play(victoryState, victoryLayer, 0f);
        else
            Debug.LogWarning("The Part 1 victory controller has no Animations layer victory state.", this);
    }

    private void WakeMiguelFacingCamera()
    {
        scanTransform = scanTransform != null ? scanTransform : miguelRoot;
        if (scanTransform != null)
        {
            cameraFacingRotation = GetCameraFacingRotation(scanTransform.position);
            ApplyScanRotation(scanTransform, cameraFacingRotation);
        }
    }

    private Quaternion GetCameraFacingRotation(Vector3 actorPosition)
    {
        Vector3 direction = roomCamera != null
            ? roomCamera.transform.position - actorPosition
            : Vector3.forward;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f && roomCamera != null)
            direction = Vector3.ProjectOnPlane(-roomCamera.transform.forward, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Quaternion GetCameraRelativeLookRotation(float signedAngle)
    {
        if (roomCamera == null)
            return cameraFacingRotation * Quaternion.Euler(0f, signedAngle, 0f);

        Vector3 cameraLeft = Vector3.ProjectOnPlane(-roomCamera.transform.right, Vector3.up).normalized;
        Vector3 cameraRight = -cameraLeft;
        Vector3 side = signedAngle < 0f ? cameraLeft : cameraRight;
        Vector3 center = cameraFacingRotation * Vector3.forward;
        Vector3 direction = (center + side * Mathf.Tan(Mathf.Abs(signedAngle) * Mathf.Deg2Rad)).normalized;
        return Quaternion.LookRotation(direction, Vector3.up);
    }

    private IEnumerator WaitForGrounding()
    {
        if (movementController == null)
            yield break;

        // The story token makes SimpleSampleCharacterControl discard input while
        // still running its normal collision and Animator Grounded synchronization.
        float deadline = Time.realtimeSinceStartup + groundingTimeout;
        int groundedFixedFrames = 0;
        yield return new WaitForFixedUpdate(); // Player is positioned before this point.

        while (Time.realtimeSinceStartup < deadline && groundedFixedFrames < 2)
        {
            yield return new WaitForFixedUpdate();
            groundedFixedFrames = movementController.DebugIsGrounded
                ? groundedFixedFrames + 1
                : 0;
        }

        if (groundedFixedFrames < 2)
            Debug.LogWarning("Miguel did not report grounded before the Posada wake sequence timeout; continuing.", this);
    }

    private IEnumerator PlaySelfDialogue(SelfDialogueTrigger trigger, string expectedText,
        System.Action onStarted = null)
    {
        ConversationManager manager = ConversationManager.Instance;
        if (trigger == null || manager == null)
        {
            Debug.LogWarning("Posada ending could not start reaction dialogue: " + expectedText, this);
            yield break;
        }

        bool started = false;
        ConversationManager.ConversationStartEvent startHandler = () =>
        {
            started = true;
            onStarted?.Invoke();
        };
        ConversationManager.OnConversationStarted += startHandler;
        trigger.StartSelfDialogue();

        float deadline = Time.realtimeSinceStartup + 2f;
        while (!started && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!started)
        {
            Debug.LogWarning("Posada ending reaction did not start: " + expectedText, trigger);
        }
        else
        {
            // In particular, POSADA!!! is configured for manual advancement.
            // Keep the ending sequence and final fade behind the real dialogue close.
            while (manager.IsConversationActive)
                yield return null;
            yield return null;
        }

        ConversationManager.OnConversationStarted -= startHandler;
    }

    private IEnumerator RotateActorTo(Quaternion target, float duration)
    {
        Transform actor = scanTransform;
        if (actor == null)
            yield break;

        Quaternion start = actor.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Quaternion rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            ApplyScanRotation(actor, rotation);
            yield return null;
        }

        ApplyScanRotation(actor, target);
    }

    private void ApplyScanRotation(Transform actor, Quaternion rotation)
    {
        if (miguelBody != null && actor == miguelRoot)
            miguelBody.rotation = rotation;
        else
            actor.rotation = rotation;
        Physics.SyncTransforms();
    }

    public void ReturnToMainMenu()
    {
        if (!part1Arrival)
            return;

        SessionStoryState.SetString(TransportDestinationStateId, string.Empty);
        SpawnData.spawnPointName = string.Empty;

        IrisTransitionController iris = IrisTransitionController.Instance;
        Coroutine handoff = iris != null
            ? iris.TransitionToScene(SceneMenuName, storyToken)
            : null;
        if (handoff != null)
        {
            storyToken = null;
            return;
        }

        storyToken?.Release();
        storyToken = null;
        SceneManager.LoadScene(SceneMenuName);
    }

    private void OnDestroy()
    {
        if (rollingCredits != null)
        {
            rollingCredits.CreditsCompleted -= OnCreditsCompleted;
            rollingCredits.ReturnRequested -= ReturnToMainMenu;
        }
        storyToken?.Release();
    }

    private void OnCreditsCompleted()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void InitializeSceneLocalPresentation()
    {
        if (firstShockDialogue == null)
            firstShockDialogue = CreateSelfDialogue("Part1 Ha Shock Dialogue", "Ha?!", firstShockDuration, true);
        if (secondShockDialogue == null)
            secondShockDialogue = CreateSelfDialogue("Part1 HAAAAAAA Shock Dialogue", "HAAAAAAA?!", secondShockDuration, true);
        if (posadaDialogue == null)
            posadaDialogue = CreateSelfDialogue("Part1 POSADA Reaction Dialogue", "POSADA!!!", 0f, false);

        if (fadeController == null)
        {
            GameObject fadeObject = GameObject.Find("FadeImageCanvas");
            if (fadeObject != null)
                fadeController = fadeObject.GetComponent<FadeController>();
        }
        CreateSleepTextPresentation();
    }

    private void CreateSleepTextPresentation()
    {
        if (screenSleepText != null)
            return;

        GameObject canvasObject = new GameObject(
            "Part1 Sleep Screen Text Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32110;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject(
            "Sleep Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        rect.sizeDelta = new Vector2(500f, 160f);

        screenSleepText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            TMP_Text existingText = FindAnyObjectByType<TMP_Text>();
            if (existingText != null)
                font = existingText.font;
        }
        if (font != null)
            screenSleepText.font = font;
        screenSleepText.text = string.Empty;
        screenSleepText.fontSize = 68f;
        screenSleepText.fontStyle = FontStyles.Italic;
        screenSleepText.alignment = TextAlignmentOptions.Center;
        screenSleepText.color = new Color(0.84f, 0.84f, 0.81f, 1f);
        screenSleepText.raycastTarget = false;
        textObject.SetActive(false);
    }

    private IEnumerator PresentCumulativeSleepText()
    {
        if (screenSleepText == null)
        {
            Debug.LogWarning("Part 1 Posada ending has no screen-space sleep text.", this);
            yield return new WaitForSeconds(firstSleepGlyphDuration + secondSleepGlyphDuration + finalSleepGlyphDuration);
            yield break;
        }

        screenSleepText.text = "z";
        screenSleepText.gameObject.SetActive(true);
        yield return new WaitForSeconds(firstSleepGlyphDuration);

        screenSleepText.text = "zz";
        yield return new WaitForSeconds(secondSleepGlyphDuration);

        screenSleepText.text = "zzz";
        yield return new WaitForSeconds(finalSleepGlyphDuration);

        screenSleepText.gameObject.SetActive(false);
    }

    private SelfDialogueTrigger CreateSelfDialogue(string objectName, string text,
        float duration, bool autoAdvance)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(transform, false);
        NPCConversation conversation = root.AddComponent<NPCConversation>();
        conversation.ParameterList = new List<EditableParameter>();
        EditableConversation editable = new EditableConversation
        {
            Parameters = new List<EditableParameter>()
        };
        EditableSpeechNode speech = new EditableSpeechNode
        {
            ID = 0,
            Text = text,
            Name = "Miguel",
            AdvanceDialogueAutomatically = autoAdvance,
            AutoAdvanceShouldDisplayOption = false,
            TimeUntilAdvance = duration
        };
        speech.EditorInfo.isRoot = true;
        editable.SpeechNodes.Add(speech);
        conversation.Serialize(editable);

        SelfDialogueTrigger trigger = root.AddComponent<SelfDialogueTrigger>();
        trigger.ConfigureConversation(conversation);
        return trigger;
    }

}
