using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Church arrival POV before the existing intro Timeline begins.</summary>
public sealed class MiguelWakeUpSequence : MonoBehaviour
{
    public const string IntroSeenFlag = "makamisa_church_intro_seen";

    [Header("Existing scene objects")]
    [SerializeField] private CinemachineCamera povCamera;
    [SerializeField] private Transform headPivot;
    [SerializeField] private PlayableDirector introDirector;
    [SerializeField] private Canvas eyelidCanvas;
    [SerializeField] private Image eyelidImage;
    [SerializeField] private SoftCurvedEyelidController eyelidController;
    [SerializeField] private ChurchNPCDepartureController churchDeparture;
    [SerializeField] private OneWayNPCDeparture padreDeparture;

    [Header("Task objective handoff after intro completion")]
    [SerializeField] private string taskId;
    [SerializeField] private string requiredCurrentStageId;
    [SerializeField] private string requiredCurrentObjective;
    [SerializeField] private string nextObjective;

    [Header("Eye timing (seconds)")]
    [SerializeField, Min(0f)] private float initialBlackDuration = 0.75f;
    [SerializeField, Min(0.01f)] private float firstOpeningDuration = 0.85f;
    [SerializeField, Range(0f, 1f)] private float firstOpeningAmount = 0.12f;
    [SerializeField, Min(0.01f)] private float firstClosingDuration = 0.50f;
    [SerializeField, Min(0.01f)] private float secondOpeningDuration = 1.10f;
    [SerializeField, Range(0f, 1f)] private float secondOpeningAmount = 0.65f;
    [SerializeField, Min(0.01f)] private float blinkDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float finalOpeningDuration = 0.95f;
    [SerializeField, Min(0f)] private float awakeHoldDuration = 1.00f;

    [Header("Head movement (degrees)")]
    [SerializeField, Range(0f, 2f)] private float headMovementStrength = 1f;
    [SerializeField, Range(0f, 3f)] private float headTiltDegrees = 0.9f;
    [SerializeField, Range(0f, 3f)] private float headSwayDegrees = 1.1f;
    [SerializeField, Range(0.5f, 4f)] private float headSettlingPower = 2f;

    private StorySequenceToken storyToken;
    private float elapsed;
    private bool introStarted;
    private bool introReachedTerminalSignal;

    private float TotalDuration =>
        initialBlackDuration + firstOpeningDuration + firstClosingDuration +
        secondOpeningDuration + blinkDuration + finalOpeningDuration +
        awakeHoldDuration;

    private void Awake()
    {
        if (SessionStoryState.GetFlag(IntroSeenFlag))
        {
            // A return visit starts with the scene's normal gameplay camera.
            // Do this before acquiring a story token or enabling any wake-up UI.
            churchDeparture?.CompleteDepartureImmediately();
            padreDeparture?.CompleteDepartureImmediately();
            if (introDirector != null)
            {
                introDirector.playOnAwake = false;
                introDirector.Stop();
            }
            if (povCamera != null)
                povCamera.enabled = false;
            if (headPivot != null)
                headPivot.localRotation = Quaternion.identity;
            if (eyelidCanvas != null)
                eyelidCanvas.enabled = false;

            enabled = false;
            return;
        }

        // This token is acquired before HUD targets register in the new scene.
        // It also overlaps the transportation token and the complete intro.
        storyToken = StorySequenceCoordinator.Acquire(this);

        if (eyelidController == null && eyelidImage != null)
            eyelidImage.TryGetComponent(out eyelidController);

        if (povCamera == null || headPivot == null || introDirector == null ||
            eyelidCanvas == null || eyelidImage == null || eyelidController == null)
        {
            Debug.LogError("Miguel wake-up sequence has missing scene references.", this);
            enabled = false;
            return;
        }

        eyelidCanvas.enabled = true;
        eyelidController.SetClosedImmediately();
        headPivot.localRotation = Quaternion.identity;
        povCamera.Priority = 100;
    }

    private IEnumerator Start()
    {
        // Commit only when the first-arrival wake-up actually begins, as the
        // Maestro Ben entrance commits immediately before playing its Timeline.
        SessionStoryState.SetFlag(IntroSeenFlag, true);

        yield return Wait(initialBlackDuration);

        // The persistent transportation iris is drawn above this overlay.
        // The first opening waits until the arrival iris has fully cleared.
        IrisTransitionController iris = IrisTransitionController.Instance;
        while (iris != null && iris.IsCovered)
            yield return null;

        yield return eyelidController.AnimateTo(firstOpeningAmount, firstOpeningDuration);
        yield return eyelidController.AnimateTo(0f, firstClosingDuration);
        yield return eyelidController.AnimateTo(secondOpeningAmount, secondOpeningDuration);
        yield return eyelidController.AnimateTo(0f, blinkDuration * 0.43f);
        yield return eyelidController.AnimateTo(secondOpeningAmount, blinkDuration * 0.57f);
        yield return eyelidController.AnimateTo(1f, finalOpeningDuration);
        yield return Wait(awakeHoldDuration);

        // Restore the approved world-space framing before the Timeline takes over.
        headPivot.localRotation = Quaternion.identity;
        eyelidController.SetOpenImmediately();
        introReachedTerminalSignal = false;
        introDirector.stopped += HandleIntroStopped;
        introDirector.time = 0d;
        introDirector.Play();
        introDirector.Evaluate();
        // The Timeline has evaluated its first camera shot. The wake-up POV
        // must leave Cinemachine's candidate list for the rest of this scene.
        povCamera.enabled = false;
        introStarted = true;
        eyelidCanvas.enabled = false;
    }

    private void Update()
    {
        if (introStarted || headPivot == null)
            return;

        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, TotalDuration));
        float settling = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, progress),
            headSettlingPower) * headMovementStrength;

        // A slow correction with uneven, small turns reads as a weak head
        // movement. No random noise, positional shake, or repeated clip.
        float pitch = (0.35f + 0.27f * Mathf.Sin(elapsed * 1.17f + 0.7f))
            * headSwayDegrees * settling;
        float yaw = (-0.55f + 0.45f * Mathf.Sin(elapsed * 0.91f + 1.2f))
            * headSwayDegrees * settling;
        float roll = (-0.75f + 0.35f * Mathf.Sin(elapsed * 1.31f + 0.4f))
            * headTiltDegrees * settling;
        headPivot.localRotation = Quaternion.Euler(pitch, yaw, roll);
    }

    private IEnumerator Wait(float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void OnIntroTerminalReached()
    {
        introReachedTerminalSignal = true;
    }

    private void HandleIntroStopped(PlayableDirector director)
    {
        if (director != introDirector)
            return;

        introDirector.stopped -= HandleIntroStopped;
        // A cancelled/early stop also raises stopped; require the terminal Timeline signal.
        if (introStarted && isActiveAndEnabled && director.isActiveAndEnabled &&
            storyToken != null && storyToken.IsValid && director.playableAsset != null &&
            introReachedTerminalSignal)
        {
            CompleteIntroObjective();
        }
        storyToken?.Release();
        storyToken = null;
    }

    private void CompleteIntroObjective()
    {
        TaskManager manager = TaskManager.Instance;
        if (manager == null || string.IsNullOrWhiteSpace(taskId) ||
            string.IsNullOrWhiteSpace(requiredCurrentStageId) ||
            string.IsNullOrWhiteSpace(requiredCurrentObjective) ||
            manager.GetTaskState(taskId) != TaskState.Active ||
            !manager.IsCurrentStage(taskId, requiredCurrentStageId) ||
            !manager.TryGetCurrentObjective(taskId, out string objective) ||
            !string.Equals(objective, requiredCurrentObjective, System.StringComparison.Ordinal))
            return;

        manager.CompleteCurrentObjectiveAndUpdate(taskId, nextObjective);
    }

    private void OnDestroy()
    {
        if (introDirector != null)
            introDirector.stopped -= HandleIntroStopped;
        storyToken?.Release();
        storyToken = null;
    }
}
