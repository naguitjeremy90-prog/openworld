using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(-32000)]
[RequireComponent(typeof(PlayableDirector))]
public sealed class AndayRecollectionSequenceController : MonoBehaviour
{
    private const float InitialBlackHoldDuration = 2f;
    private const float FirstOpeningDuration = 1.2f;
    private const float FirstOpeningAmount = 0.12f;
    private const float FirstClosingDuration = 0.5f;
    private const float SecondOpeningDuration = 1.5f;
    private const float SecondOpeningAmount = 0.65f;
    private const float BlinkDuration = 0.35f;
    private const float BlinkClosingFraction = 0.43f;
    private const float FinalOpeningDuration = 1f;
    private const float AwakeHoldDuration = 1f;
    private const string FelicidadFragment2AnimationState = "Base Layer.Sad Idle Tamed";
    private const float Fragment2ClosingDuration = 1f;
    private const string Fragment2ReturnScene = "NEWMAKAMISA";
    private const string RecollectionReturnPoint = "AndayRecollectionReturnPoint";

    [SerializeField] private SoftCurvedEyelidController eyelidController;
    [SerializeField] private CinemachineCamera introCamera;
    [SerializeField] private CinemachineCamera competingCamera;
    [SerializeField] private PlayableDirector fragment2Director;
    [SerializeField] private CinemachineCamera fragment2Camera;
    [SerializeField] private Transform andayActor;
    [SerializeField] private Transform felicidadActor;
    [SerializeField] private Transform padreAgatonActor;
    [SerializeField] private Transform andayPosition;
    [SerializeField] private Transform felicidadPosition;
    [SerializeField] private Transform padreAgatonPosition;

    private PlayableDirector director;
    private bool closeRequested;
    private bool fragment2CloseRequested;
    private PrioritySettings originalIntroCameraPriority;
    private bool introCameraPriorityOverridden;
    private PrioritySettings originalFragment2CameraPriority;
    private bool fragment2CameraPriorityOverridden;
    private Animator felicidadAnimator;
    private bool originalFelicidadRootMotion;
    private readonly List<Renderer> hiddenAndayRenderers = new List<Renderer>();
    private readonly List<bool> originalAndayRendererStates = new List<bool>();

    private void Awake()
    {
        if (!TryGetComponent(out director))
        {
            Debug.LogError("Anday recollection requires a PlayableDirector.", this);
            enabled = false;
            return;
        }

        director.playOnAwake = false;
        director.Stop();
        director.time = 0d;

        if (eyelidController == null)
            eyelidController = FindAnyObjectByType<SoftCurvedEyelidController>();

        if (eyelidController == null)
        {
            Debug.LogError("Anday recollection requires a soft curved eyelid controller.", this);
            enabled = false;
            return;
        }

        // The overlay is saved inactive in the scene. Enable it before the
        // first render, then establish the covered state before starting play.
        if (!eyelidController.gameObject.activeSelf)
            eyelidController.gameObject.SetActive(true);
        Transform eyelidCanvas = eyelidController.transform.parent;
        if (eyelidCanvas != null && !eyelidCanvas.gameObject.activeSelf)
            eyelidCanvas.gameObject.SetActive(true);

        if (introCamera == null || competingCamera == null)
        {
            Debug.LogError("Anday recollection requires references to its POV and competing Cinemachine cameras.", this);
            enabled = false;
            return;
        }

        originalIntroCameraPriority = introCamera.Priority;
        PrioritySettings introPriority = introCamera.Priority;
        introPriority.Value = Mathf.Max(introCamera.Priority.Value, competingCamera.Priority.Value) + 1;
        introCamera.Priority = introPriority;
        introCameraPriorityOverridden = true;

        eyelidController.SetClosedImmediately();
        director.Evaluate();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(InitialBlackHoldDuration);

        yield return eyelidController.AnimateTo(FirstOpeningAmount, FirstOpeningDuration);
        yield return eyelidController.AnimateTo(0f, FirstClosingDuration);
        yield return eyelidController.AnimateTo(SecondOpeningAmount, SecondOpeningDuration);
        yield return eyelidController.AnimateTo(0f, BlinkDuration * BlinkClosingFraction);
        yield return eyelidController.AnimateTo(
            SecondOpeningAmount, BlinkDuration * (1f - BlinkClosingFraction));
        yield return eyelidController.AnimateTo(1f, FinalOpeningDuration);
        yield return new WaitForSecondsRealtime(AwakeHoldDuration);

        eyelidController.SetOpenImmediately();
        director.time = 0d;
        director.Evaluate();
        director.Play();

        // Let Timeline's CM_1 shot take control before restoring its saved priority.
        yield return new WaitForEndOfFrame();
        RestoreIntroCameraPriority();
    }

    public void CloseFragment1Eyes()
    {
        if (!Application.isPlaying || closeRequested)
            return;

        closeRequested = true;
        if (eyelidController == null)
        {
            Debug.LogError("Anday recollection cannot close eyes because its eyelid controller is missing.", this);
            return;
        }

        StartCoroutine(CloseAndHandOffToFragment2());
    }

    public void CloseFragment2Eyes()
    {
        if (!Application.isPlaying || fragment2CloseRequested || eyelidController == null ||
            fragment2Director == null)
            return;

        fragment2CloseRequested = true;
        if (fragment2Director.state == PlayState.Playing)
            fragment2Director.Pause();

        StartCoroutine(CloseFragment2EyesAndReturn());
    }

    private IEnumerator CloseFragment2EyesAndReturn()
    {
        // The final scene transition is requested only after _Open has reached
        // exactly zero and the curved eyelids fully cover the screen.
        yield return eyelidController.Close(Fragment2ClosingDuration);

        StorySequenceToken token = StorySequenceCoordinator.Acquire(this);
        SpawnData.spawnPointName = RecollectionReturnPoint;
        SessionStoryState.SetFlag(AndayRecollectionEntryController.CompleteFlag, true);

        IrisTransitionController transition = IrisTransitionController.Instance;
        if (transition == null)
            transition = FindAnyObjectByType<IrisTransitionController>();
        if (transition == null)
            transition = new GameObject("IrisTransitionController")
                .AddComponent<IrisTransitionController>();

        Coroutine handoff = transition.TransitionToScene(Fragment2ReturnScene, token);
        if (handoff == null)
        {
            Debug.LogError("Anday recollection reached full black but could not load NEWMAKAMISA.", this);
            token?.Release();
            yield break;
        }
    }

    private IEnumerator CloseAndHandOffToFragment2()
    {
        // Do not stage actors or switch cameras until the eyelid animation has
        // completed and the shared controller has set _Open to exactly zero.
        yield return eyelidController.Close(0.5f);

        if (!HasFragment2References())
        {
            Debug.LogError("Fragment 2 handoff references are incomplete; keeping the screen closed.", this);
            yield break;
        }

        fragment2Director.playOnAwake = false;
        fragment2Director.Stop();
        fragment2Director.time = 0d;

        DisableFelicidadRootMotionForPOV();
        MoveActorToMarker(felicidadActor, felicidadPosition);
        PlayFelicidadFragment2Animation();
        MoveActorToMarker(padreAgatonActor, padreAgatonPosition);
        MoveActorToMarker(andayActor, andayPosition);
        HideAndayBodyFromHerOwnPOV();

        TakeFragment2CameraPriority();
        fragment2Director.Evaluate();

        yield return new WaitForSecondsRealtime(0.5f);

        // Fragment 2's camera remains at its authored time-zero pose while
        // the same curved eyelids reopen.
        yield return eyelidController.Open(1f);

        eyelidController.SetOpenImmediately();
        fragment2Director.time = 0d;
        fragment2Director.Evaluate();
        fragment2Director.Play();

        // Match the established Fragment 1 intro handoff: let Timeline's
        // Cinemachine shot become live before restoring the saved priority.
        yield return new WaitForEndOfFrame();
        RestoreFragment2CameraPriority();
    }

    private bool HasFragment2References()
    {
        return eyelidController != null && fragment2Director != null && fragment2Camera != null &&
               andayActor != null && felicidadActor != null && padreAgatonActor != null &&
               andayPosition != null && felicidadPosition != null && padreAgatonPosition != null;
    }

    private static void MoveActorToMarker(Transform actor, Transform marker)
    {
        Vector3 position = marker.position;
        Quaternion rotation = marker.rotation;
        actor.SetPositionAndRotation(position, rotation);

        // Both physics-backed actors use kinematic rigidbodies. Synchronize
        // their physics pose directly so no velocity or gravity is introduced.
        Rigidbody body = actor.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
        }
    }

    private void DisableFelicidadRootMotionForPOV()
    {
        felicidadAnimator = felicidadActor.GetComponent<Animator>();
        if (felicidadAnimator == null)
            return;

        originalFelicidadRootMotion = felicidadAnimator.applyRootMotion;
        felicidadAnimator.applyRootMotion = false;
    }

    private void PlayFelicidadFragment2Animation()
    {
        if (felicidadAnimator == null)
            return;

        int stateHash = Animator.StringToHash(FelicidadFragment2AnimationState);
        if (!felicidadAnimator.HasState(0, stateHash))
        {
            Debug.LogError("Felicidad's Animator Controller does not contain the Sad Idle Tamed state.", this);
            return;
        }

        felicidadAnimator.Play(stateHash, 0, 0f);
        felicidadAnimator.Update(0f);
    }

    private void HideAndayBodyFromHerOwnPOV()
    {
        foreach (Renderer renderer in andayActor.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled)
                continue;

            hiddenAndayRenderers.Add(renderer);
            originalAndayRendererStates.Add(renderer.enabled);
            renderer.enabled = false;
        }
    }

    private void TakeFragment2CameraPriority()
    {
        originalFragment2CameraPriority = fragment2Camera.Priority;
        int highestPriority = fragment2Camera.Priority.Value;
        foreach (CinemachineCamera camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            highestPriority = Mathf.Max(highestPriority, camera.Priority.Value);

        PrioritySettings priority = fragment2Camera.Priority;
        priority.Value = highestPriority + 1;
        fragment2Camera.Priority = priority;
        fragment2CameraPriorityOverridden = true;
    }

    private void RestoreFragment2CameraPriority()
    {
        if (!fragment2CameraPriorityOverridden || fragment2Camera == null)
            return;

        fragment2Camera.Priority = originalFragment2CameraPriority;
        fragment2CameraPriorityOverridden = false;
    }

    private void RestoreIntroCameraPriority()
    {
        if (!introCameraPriorityOverridden || introCamera == null)
            return;

        introCamera.Priority = originalIntroCameraPriority;
        introCameraPriorityOverridden = false;
    }

    private void OnDestroy()
    {
        RestoreIntroCameraPriority();
        RestoreFragment2CameraPriority();

        if (felicidadAnimator != null)
            felicidadAnimator.applyRootMotion = originalFelicidadRootMotion;

        for (int i = 0; i < hiddenAndayRenderers.Count; i++)
            if (hiddenAndayRenderers[i] != null)
                hiddenAndayRenderers[i].enabled = originalAndayRendererStates[i];
    }
}
